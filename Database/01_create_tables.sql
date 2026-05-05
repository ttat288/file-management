-- Enable extension
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ========================
-- USERS + REFRESH TOKENS
-- ========================
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(320) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    display_name VARCHAR(120),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash TEXT NOT NULL UNIQUE,
    expires_at TIMESTAMP NOT NULL,
    revoked_at TIMESTAMP NULL,
    last_used_at TIMESTAMP NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- ========================
-- FOLDERS TABLE
-- ========================
CREATE TABLE IF NOT EXISTS folders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    parent_id UUID REFERENCES folders(id) ON DELETE CASCADE,
    owner_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_folder_name_per_parent 
        UNIQUE (owner_id, name, parent_id)
);

-- If the table already existed (older schema), CREATE TABLE IF NOT EXISTS will not add new columns/constraints.
DO $$
BEGIN
    -- Ensure owner_id column exists
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'folders'
          AND column_name = 'owner_id'
    ) THEN
        ALTER TABLE public.folders ADD COLUMN owner_id UUID;
    END IF;

    -- Ensure FK exists (only if column exists)
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'folders'
          AND column_name = 'owner_id'
    ) AND NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_folders_owner_id'
    ) THEN
        ALTER TABLE public.folders
            ADD CONSTRAINT fk_folders_owner_id
            FOREIGN KEY (owner_id) REFERENCES public.users(id) ON DELETE CASCADE;
    END IF;

    -- Ensure unique constraint exists (only if column exists)
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'folders'
          AND column_name = 'owner_id'
    ) AND NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'unique_folder_name_per_parent'
    ) THEN
        ALTER TABLE public.folders
            ADD CONSTRAINT unique_folder_name_per_parent
            UNIQUE (owner_id, name, parent_id);
    END IF;
END;
$$;

-- ========================
-- FILES TABLE
-- ========================
CREATE TABLE IF NOT EXISTS files (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    size BIGINT NOT NULL CHECK (size > 0),
    content_type VARCHAR(100),
    blob_url TEXT,
    blob_name VARCHAR(255) UNIQUE,
    folder_id UUID REFERENCES folders(id) ON DELETE SET NULL,
    owner_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Same note as folders: ensure new column/constraints exist when table pre-exists.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'files'
          AND column_name = 'owner_id'
    ) THEN
        ALTER TABLE public.files ADD COLUMN owner_id UUID;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'files'
          AND column_name = 'owner_id'
    ) AND NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_files_owner_id'
    ) THEN
        ALTER TABLE public.files
            ADD CONSTRAINT fk_files_owner_id
            FOREIGN KEY (owner_id) REFERENCES public.users(id) ON DELETE CASCADE;
    END IF;
END;
$$;

-- ========================
-- INDEXES
-- ========================
CREATE INDEX IF NOT EXISTS idx_files_folder_id ON files(folder_id);
CREATE INDEX IF NOT EXISTS idx_files_created_at ON files(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_files_blob_name ON files(blob_name);
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'files'
          AND column_name = 'owner_id'
    ) THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_files_owner_id ON public.files(owner_id)';
    END IF;
END;
$$;

CREATE INDEX IF NOT EXISTS idx_folders_parent_id ON folders(parent_id);
CREATE INDEX IF NOT EXISTS idx_folders_created_at ON folders(created_at DESC);
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'folders'
          AND column_name = 'owner_id'
    ) THEN
        EXECUTE 'CREATE INDEX IF NOT EXISTS idx_folders_owner_id ON public.folders(owner_id)';
    END IF;
END;
$$;
