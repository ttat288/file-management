-- ============================================
-- FILE FUNCTIONS
-- ============================================

-- fn_file_create: Create a new file
DROP FUNCTION IF EXISTS public.fn_file_create(VARCHAR, BIGINT, VARCHAR, TEXT, VARCHAR, UUID);
DROP FUNCTION IF EXISTS public.fn_file_create(UUID, VARCHAR, BIGINT, VARCHAR, TEXT, VARCHAR, UUID);

CREATE OR REPLACE FUNCTION public.fn_file_create(
    p_owner_id UUID,
    p_name VARCHAR(255),
    p_size BIGINT,
    p_content_type VARCHAR(100),
    p_blob_url TEXT,
    p_blob_name VARCHAR(255),
    p_folder_id UUID
) RETURNS TABLE(
    id UUID,
    name VARCHAR(255),
    size BIGINT,
    content_type VARCHAR(100),
    blob_url TEXT,
    blob_name VARCHAR(255),
    folder_id UUID,
    created_at TIMESTAMP
) AS $$
BEGIN
    IF p_folder_id IS NOT NULL THEN
        IF NOT EXISTS (SELECT 1 FROM folders WHERE id = p_folder_id AND owner_id = p_owner_id) THEN
            RAISE EXCEPTION 'Folder not found or not owned by user';
        END IF;
    END IF;

    RETURN QUERY
    INSERT INTO files (owner_id, name, size, content_type, blob_url, blob_name, folder_id)
    VALUES (p_owner_id, p_name, p_size, p_content_type, p_blob_url, p_blob_name, p_folder_id)
    RETURNING id, name, size, content_type,
              blob_url, blob_name, folder_id, created_at;
END;
$$ LANGUAGE plpgsql;

-- fn_file_get_list: Get paginated file list with optional folder filter
DROP FUNCTION IF EXISTS public.fn_file_get_list(UUID, INT, INT);
DROP FUNCTION IF EXISTS public.fn_file_get_list(UUID, UUID, INT, INT);

CREATE OR REPLACE FUNCTION public.fn_file_get_list(
    p_owner_id UUID,
    p_folder_id UUID,
    p_page_number INT,
    p_page_size INT
) RETURNS TABLE(
    id UUID,
    name VARCHAR(255),
    size BIGINT,
    content_type VARCHAR(100),
    blob_url TEXT,
    blob_name VARCHAR(255),
    folder_id UUID,
    created_at TIMESTAMP,
    total_count BIGINT
) AS $$
DECLARE
    v_offset INT;
    v_total BIGINT;
BEGIN
    v_offset := (p_page_number - 1) * p_page_size;
    
    -- Get total count
    SELECT COUNT(*) INTO v_total FROM files 
    WHERE files.owner_id = p_owner_id
    AND (p_folder_id IS NULL OR files.folder_id = p_folder_id);
    
    -- Return paginated results
    RETURN QUERY
    SELECT 
        f.id, f.name, f.size, f.content_type, 
        f.blob_url, f.blob_name, f.folder_id, f.created_at,
        v_total
    FROM files f
    WHERE f.owner_id = p_owner_id
    AND (p_folder_id IS NULL OR f.folder_id = p_folder_id)
    ORDER BY f.created_at DESC
    LIMIT p_page_size OFFSET v_offset;
END;
$$ LANGUAGE plpgsql;

-- fn_file_get_by_id: Get single file by ID
DROP FUNCTION IF EXISTS public.fn_file_get_by_id(UUID);
DROP FUNCTION IF EXISTS public.fn_file_get_by_id(UUID, UUID);

CREATE OR REPLACE FUNCTION public.fn_file_get_by_id(p_owner_id UUID, p_file_id UUID) 
RETURNS TABLE(
    id UUID,
    name VARCHAR(255),
    size BIGINT,
    content_type VARCHAR(100),
    blob_url TEXT,
    blob_name VARCHAR(255),
    folder_id UUID,
    created_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT files.id, files.name, files.size, files.content_type,
           files.blob_url, files.blob_name, files.folder_id, files.created_at
    FROM files
    WHERE files.owner_id = p_owner_id
    AND files.id = p_file_id;
END;
$$ LANGUAGE plpgsql;

-- fn_file_rename: Rename a file
DROP FUNCTION IF EXISTS public.fn_file_rename(UUID, VARCHAR);
DROP FUNCTION IF EXISTS public.fn_file_rename(UUID, UUID, VARCHAR);

CREATE OR REPLACE FUNCTION public.fn_file_rename(
    p_owner_id UUID,
    p_file_id UUID,
    p_new_name VARCHAR(255)
) RETURNS TABLE(
    id UUID,
    name VARCHAR(255),
    size BIGINT,
    content_type VARCHAR(100),
    blob_url TEXT,
    blob_name VARCHAR(255),
    folder_id UUID,
    created_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    UPDATE files 
    SET name = p_new_name
    WHERE files.owner_id = p_owner_id
    AND files.id = p_file_id
    RETURNING files.id, files.name, files.size, files.content_type,
              files.blob_url, files.blob_name, files.folder_id, files.created_at;
END;
$$ LANGUAGE plpgsql;

-- fn_file_delete: Delete a file by ID
DROP FUNCTION IF EXISTS public.fn_file_delete(UUID);
DROP FUNCTION IF EXISTS public.fn_file_delete(UUID, UUID);

CREATE OR REPLACE FUNCTION public.fn_file_delete(p_owner_id UUID, p_file_id UUID)
RETURNS TABLE(
    success BOOLEAN,
    blob_name VARCHAR(255)
) AS $$
DECLARE
    v_count INT;
    v_blob_name VARCHAR(255);
BEGIN
    SELECT files.blob_name INTO v_blob_name
    FROM files
    WHERE files.owner_id = p_owner_id AND files.id = p_file_id;
    
    DELETE FROM files
    WHERE files.owner_id = p_owner_id AND files.id = p_file_id;
    
    GET DIAGNOSTICS v_count = ROW_COUNT;
    
    RETURN QUERY
    SELECT v_count > 0, v_blob_name;
END;
$$ LANGUAGE plpgsql;

-- fn_file_search: Search files by name (case-insensitive)
DROP FUNCTION IF EXISTS public.fn_file_search(TEXT, UUID, INT, INT);
DROP FUNCTION IF EXISTS public.fn_file_search(UUID, TEXT, UUID, INT, INT);

CREATE OR REPLACE FUNCTION public.fn_file_search(
    p_owner_id UUID,
    p_search_term TEXT,
    p_folder_id UUID,
    p_page_number INT,
    p_page_size INT
) RETURNS TABLE(
    id UUID,
    name VARCHAR(255),
    size BIGINT,
    content_type VARCHAR(100),
    blob_url TEXT,
    blob_name VARCHAR(255),
    folder_id UUID,
    created_at TIMESTAMP,
    total_count BIGINT
) AS $$
DECLARE
    v_offset INT;
    v_total BIGINT;
    v_search_pattern VARCHAR;
BEGIN
    v_offset := (p_page_number - 1) * p_page_size;
    v_search_pattern := '%' || p_search_term || '%';
    
    SELECT COUNT(*) INTO v_total FROM files 
    WHERE files.owner_id = p_owner_id
    AND (LOWER(files.name) LIKE LOWER(v_search_pattern)) 
    AND (p_folder_id IS NULL OR files.folder_id = p_folder_id);
    
    RETURN QUERY
    SELECT 
        f.id, f.name, f.size, f.content_type, 
        f.blob_url, f.blob_name, f.folder_id, f.created_at,
        v_total
    FROM files f
    WHERE f.owner_id = p_owner_id
    AND (LOWER(f.name) LIKE LOWER(v_search_pattern))
    AND (p_folder_id IS NULL OR f.folder_id = p_folder_id)
    ORDER BY f.created_at DESC
    LIMIT p_page_size OFFSET v_offset;
END;
$$ LANGUAGE plpgsql;

-- ============================================
-- FOLDER FUNCTIONS
-- ============================================

-- fn_folder_create: Create a new folder
DROP FUNCTION IF EXISTS public.fn_folder_create(VARCHAR, UUID);
DROP FUNCTION IF EXISTS public.fn_folder_create(UUID, VARCHAR, UUID);

CREATE OR REPLACE FUNCTION public.fn_folder_create(
    p_owner_id UUID,
    p_name VARCHAR(255),
    p_parent_id UUID DEFAULT NULL
) RETURNS TABLE(
    id UUID,
    name VARCHAR(255),
    parent_id UUID,
    created_at TIMESTAMP
) AS $$
BEGIN
    IF p_parent_id IS NOT NULL THEN
        IF NOT EXISTS (SELECT 1 FROM folders WHERE id = p_parent_id AND owner_id = p_owner_id) THEN
            RAISE EXCEPTION 'Parent folder not found or not owned by user';
        END IF;
    END IF;

    RETURN QUERY
    INSERT INTO folders (owner_id, name, parent_id)
    VALUES (p_owner_id, p_name, p_parent_id)
    RETURNING folders.id, folders.name, folders.parent_id, folders.created_at;
END;
$$ LANGUAGE plpgsql;

-- fn_folder_get_list: Get paginated folder list with optional parent filter
DROP FUNCTION IF EXISTS public.fn_folder_get_list(UUID, INT, INT);
DROP FUNCTION IF EXISTS public.fn_folder_get_list(UUID, UUID, INT, INT);

CREATE OR REPLACE FUNCTION public.fn_folder_get_list(
    p_owner_id UUID,
    p_parent_id UUID,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE(
    id UUID,
    name VARCHAR(255),
    parent_id UUID,
    created_at TIMESTAMP,
    total_count BIGINT
) AS $$
DECLARE
    v_offset INT;
    v_total BIGINT;
BEGIN
    v_offset := (p_page_number - 1) * p_page_size;

    -- Qualify column names to avoid ambiguity with RETURNS TABLE output parameters.
    SELECT COUNT(*) INTO v_total
    FROM public.folders f
    WHERE f.owner_id = p_owner_id
      AND (p_parent_id IS NULL OR f.parent_id = p_parent_id);

    RETURN QUERY
    SELECT f.id, f.name, f.parent_id, f.created_at, v_total
    FROM folders f
    WHERE f.owner_id = p_owner_id
    AND (p_parent_id IS NULL OR f.parent_id = p_parent_id)
    ORDER BY f.created_at DESC
    LIMIT p_page_size OFFSET v_offset;
END;
$$ LANGUAGE plpgsql;

-- fn_folder_get_by_id: Get single folder by ID
DROP FUNCTION IF EXISTS public.fn_folder_get_by_id(UUID);
DROP FUNCTION IF EXISTS public.fn_folder_get_by_id(UUID, UUID);

CREATE OR REPLACE FUNCTION public.fn_folder_get_by_id(p_owner_id UUID, p_folder_id UUID)
RETURNS TABLE(
    id UUID,
    name VARCHAR(255),
    parent_id UUID,
    created_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    SELECT folders.id, folders.name, folders.parent_id, folders.created_at
    FROM folders
    WHERE folders.owner_id = p_owner_id
    AND folders.id = p_folder_id;
END;
$$ LANGUAGE plpgsql;

-- fn_folder_rename: Rename a folder
DROP FUNCTION IF EXISTS public.fn_folder_rename(UUID, VARCHAR);
DROP FUNCTION IF EXISTS public.fn_folder_rename(UUID, UUID, VARCHAR);

CREATE OR REPLACE FUNCTION public.fn_folder_rename(
    p_owner_id UUID,
    p_folder_id UUID,
    p_new_name VARCHAR(255)
) RETURNS TABLE(
    id UUID,
    name VARCHAR(255),
    parent_id UUID,
    created_at TIMESTAMP
) AS $$
BEGIN
    RETURN QUERY
    UPDATE folders
    SET name = p_new_name
    WHERE folders.owner_id = p_owner_id
    AND folders.id = p_folder_id
    RETURNING folders.id, folders.name, folders.parent_id, folders.created_at;
END;
$$ LANGUAGE plpgsql;

-- fn_folder_delete: Delete a folder and its contents (cascade handled by DB)
DROP FUNCTION IF EXISTS public.fn_folder_delete(UUID);
DROP FUNCTION IF EXISTS public.fn_folder_delete(UUID, UUID);

CREATE OR REPLACE FUNCTION public.fn_folder_delete(p_owner_id UUID, p_folder_id UUID)
RETURNS TABLE(
    id UUID,
    success BOOLEAN
) AS $$
DECLARE
    v_count INT;
BEGIN
    DELETE FROM folders WHERE owner_id = p_owner_id AND id = p_folder_id;
    
    GET DIAGNOSTICS v_count = ROW_COUNT;
    
    RETURN QUERY
    SELECT p_folder_id AS id, (v_count > 0) AS success;
END;
$$ LANGUAGE plpgsql;
