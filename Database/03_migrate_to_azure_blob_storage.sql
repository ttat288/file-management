-- Database Migration: Switch from Cloudinary to Azure Blob Storage
-- This migration updates the files table schema and related functions

-- NOTE:
-- - This script is intended for migrating an *existing* Cloudinary-based schema
--   (columns: cloudinary_url, public_id) to Azure Blob fields (blob_url, blob_name).
-- - If your DB was created from `Database/01_create_tables.sql` (already has blob_url/blob_name),
--   then the Cloudinary columns do not exist and the rename steps are skipped automatically.

-- Step 1: Drop existing functions that depend on the files table
DROP FUNCTION IF EXISTS public.fn_file_delete(UUID);
DROP FUNCTION IF EXISTS public.fn_file_search(TEXT, UUID, INT, INT);
DROP FUNCTION IF EXISTS public.fn_file_search(VARCHAR, UUID, INT, INT);
DROP FUNCTION IF EXISTS public.fn_file_get_list(UUID, INT, INT);
DROP FUNCTION IF EXISTS public.fn_file_rename(UUID, VARCHAR);
DROP FUNCTION IF EXISTS public.fn_file_get_by_id(UUID);
DROP FUNCTION IF EXISTS public.fn_file_create(VARCHAR, BIGINT, VARCHAR, TEXT, VARCHAR, UUID);

DROP FUNCTION IF EXISTS fn_file_delete(UUID);
DROP FUNCTION IF EXISTS fn_file_search(TEXT, UUID, INT, INT);
DROP FUNCTION IF EXISTS fn_file_search(VARCHAR, UUID, INT, INT);
DROP FUNCTION IF EXISTS fn_file_get_list(UUID, INT, INT);
DROP FUNCTION IF EXISTS fn_file_rename(UUID, VARCHAR);
DROP FUNCTION IF EXISTS fn_file_get_by_id(UUID);
DROP FUNCTION IF EXISTS fn_file_create(VARCHAR, BIGINT, VARCHAR, TEXT, VARCHAR, UUID);

-- Step 2: Drop old indexes
DROP INDEX IF EXISTS idx_files_public_id;

-- Step 3: Rename old columns (backup)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'files'
          AND column_name = 'cloudinary_url'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'files'
          AND column_name = 'cloudinary_url_old'
    ) THEN
        ALTER TABLE public.files RENAME COLUMN cloudinary_url TO cloudinary_url_old;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'files'
          AND column_name = 'public_id'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'files'
          AND column_name = 'public_id_old'
    ) THEN
        ALTER TABLE public.files RENAME COLUMN public_id TO public_id_old;
    END IF;
END;
$$;

-- Step 4: Add new columns
ALTER TABLE public.files
    ADD COLUMN IF NOT EXISTS blob_url TEXT DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS blob_name VARCHAR(255) DEFAULT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        WHERE n.nspname = 'public'
          AND t.relname = 'files'
          AND c.contype = 'u'
          AND c.conname = 'uq_files_blob_name'
    ) THEN
        ALTER TABLE public.files
            ADD CONSTRAINT uq_files_blob_name UNIQUE (blob_name);
    END IF;
END;
$$;

-- Step 5: Migrate existing data if needed (copy URLs to avoid data loss)
-- Note: If you want to keep existing files, you'll need to upload them to Azure Blob Storage
-- For now, we'll just copy the Cloudinary URLs as a reference
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'files'
          AND column_name = 'cloudinary_url_old'
    ) THEN
        EXECUTE 'UPDATE public.files SET blob_url = cloudinary_url_old WHERE cloudinary_url_old IS NOT NULL';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'files'
          AND column_name = 'public_id_old'
    ) THEN
        EXECUTE 'UPDATE public.files SET blob_name = public_id_old WHERE public_id_old IS NOT NULL';
    END IF;
END;
$$;

-- Step 6: Drop old columns (optional, comment out if you want to keep backup)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'files'
          AND column_name = 'cloudinary_url_old'
    ) THEN
        ALTER TABLE public.files DROP COLUMN cloudinary_url_old;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'files'
          AND column_name = 'public_id_old'
    ) THEN
        ALTER TABLE public.files DROP COLUMN public_id_old;
    END IF;
END;
$$;

-- Step 7: Recreate indexes
CREATE INDEX IF NOT EXISTS idx_files_blob_name ON public.files(blob_name);

-- Step 8: Recreate functions with new column names

-- Function: Create file
CREATE OR REPLACE FUNCTION public.fn_file_create(
    p_name VARCHAR,
    p_size BIGINT,
    p_content_type VARCHAR,
    p_blob_url TEXT,
    p_blob_name VARCHAR,
    p_folder_id UUID
)
RETURNS TABLE(
    id UUID,
    name VARCHAR,
    size BIGINT,
    content_type VARCHAR,
    blob_url TEXT,
    blob_name VARCHAR,
    folder_id UUID,
    created_at TIMESTAMP
) AS $$
DECLARE
    v_file_id UUID;
BEGIN
    v_file_id := gen_random_uuid();
    
    INSERT INTO files (id, name, size, content_type, blob_url, blob_name, folder_id, created_at)
    VALUES (v_file_id, p_name, p_size, p_content_type, p_blob_url, p_blob_name, p_folder_id, CURRENT_TIMESTAMP);
    
    RETURN QUERY
    SELECT 
        files.id,
        files.name,
        files.size,
        files.content_type,
        files.blob_url,
        files.blob_name,
        files.folder_id,
        files.created_at
    FROM files
    WHERE files.id = v_file_id;
END;
$$ LANGUAGE plpgsql;

-- Function: Get file by ID
CREATE OR REPLACE FUNCTION public.fn_file_get_by_id(p_file_id UUID)
RETURNS TABLE(
    id UUID,
    name VARCHAR,
    size BIGINT,
    content_type VARCHAR,
    blob_url TEXT,
    blob_name VARCHAR,
    folder_id UUID,
    created_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        files.id,
        files.name,
        files.size,
        files.content_type,
        files.blob_url,
        files.blob_name,
        files.folder_id,
        files.created_at
    FROM files
    WHERE files.id = p_file_id;
END;
$$ LANGUAGE plpgsql;

-- Function: Get paginated list of files
CREATE OR REPLACE FUNCTION public.fn_file_get_list(
    p_folder_id UUID,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE(
    id UUID,
    name VARCHAR,
    size BIGINT,
    content_type VARCHAR,
    blob_url TEXT,
    blob_name VARCHAR,
    folder_id UUID,
    created_at TIMESTAMP,
    total_count BIGINT
) AS $$
DECLARE
    v_offset INT;
    v_total BIGINT;
BEGIN
    v_offset := (p_page_number - 1) * p_page_size;
    
    v_total := COALESCE(
        (SELECT COUNT(*) FROM public.files f WHERE (p_folder_id IS NULL OR f.folder_id = p_folder_id)),
        0
    );
    
    RETURN QUERY
    SELECT 
        files.id,
        files.name,
        files.size,
        files.content_type,
        files.blob_url,
        files.blob_name,
        files.folder_id,
        files.created_at,
        v_total
    FROM files
    WHERE (p_folder_id IS NULL OR files.folder_id = p_folder_id)
    ORDER BY files.created_at DESC
    LIMIT p_page_size OFFSET v_offset;
END;
$$ LANGUAGE plpgsql;

-- Function: Rename file
CREATE OR REPLACE FUNCTION public.fn_file_rename(p_file_id UUID, p_new_name VARCHAR)
RETURNS TABLE(
    id UUID,
    name VARCHAR,
    size BIGINT,
    content_type VARCHAR,
    blob_url TEXT,
    blob_name VARCHAR,
    folder_id UUID,
    created_at TIMESTAMP
) AS $$
BEGIN
    UPDATE files
    SET name = p_new_name
    WHERE files.id = p_file_id;
    
    RETURN QUERY
    SELECT 
        files.id,
        files.name,
        files.size,
        files.content_type,
        files.blob_url,
        files.blob_name,
        files.folder_id,
        files.created_at
    FROM files
    WHERE files.id = p_file_id;
END;
$$ LANGUAGE plpgsql;

-- Function: Delete file and return blob name
CREATE OR REPLACE FUNCTION public.fn_file_delete(p_file_id UUID)
RETURNS TABLE(
    success BOOLEAN,
    blob_name VARCHAR
) AS $$
DECLARE
    v_blob_name VARCHAR;
BEGIN
    SELECT files.blob_name INTO v_blob_name FROM public.files WHERE id = p_file_id;
    
    DELETE FROM public.files WHERE id = p_file_id;
    
    RETURN QUERY SELECT (FOUND), v_blob_name;
END;
$$ LANGUAGE plpgsql;

-- Function: Search files with pagination
CREATE OR REPLACE FUNCTION public.fn_file_search(
    p_search_term TEXT,
    p_folder_id UUID,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE(
    id UUID,
    name VARCHAR,
    size BIGINT,
    content_type VARCHAR,
    blob_url TEXT,
    blob_name VARCHAR,
    folder_id UUID,
    created_at TIMESTAMP,
    total_count BIGINT
) AS $$
DECLARE
    v_offset INT;
    v_total BIGINT;
BEGIN
    v_offset := (p_page_number - 1) * p_page_size;
    
    v_total := COALESCE(
         (SELECT COUNT(*) 
         FROM public.files 
         WHERE (p_folder_id IS NULL OR public.files.folder_id = p_folder_id)
         AND LOWER(public.files.name) LIKE LOWER(CONCAT('%', p_search_term, '%'))),
        0
    );
    
    RETURN QUERY
    SELECT 
        files.id,
        files.name,
        files.size,
        files.content_type,
        files.blob_url,
        files.blob_name,
        files.folder_id,
        files.created_at,
        v_total
    FROM public.files
    WHERE (p_folder_id IS NULL OR files.folder_id = p_folder_id)
    AND LOWER(files.name) LIKE LOWER(CONCAT('%', p_search_term, '%'))
    ORDER BY files.created_at DESC
    LIMIT p_page_size OFFSET v_offset;
END;
$$ LANGUAGE plpgsql;

-- Done! The database has been migrated from Cloudinary to Azure Blob Storage
