DO $$
DECLARE
    r RECORD;
    c RECORD;
    tbl text;
BEGIN
    FOR r IN
        SELECT table_name
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_type = 'BASE TABLE'
          AND table_name <> lower(table_name)
        ORDER BY table_name
    LOOP
        EXECUTE format('ALTER TABLE public.%I RENAME TO %I;', r.table_name, lower(r.table_name));
    END LOOP;

    FOR c IN
        SELECT table_name, column_name
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND column_name <> lower(column_name)
        ORDER BY table_name, ordinal_position
    LOOP
        tbl := lower(c.table_name);
        EXECUTE format('ALTER TABLE public.%I RENAME COLUMN %I TO %I;', tbl, c.column_name, lower(c.column_name));
    END LOOP;
END $$;
