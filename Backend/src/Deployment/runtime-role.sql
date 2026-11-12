-- Run in the application database using the deployment identity.
-- This is a permission group, not a login and not a database owner.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'urbanshade_runtime') THEN
        CREATE ROLE urbanshade_runtime NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
    END IF;
END $$;

GRANT USAGE ON SCHEMA public TO urbanshade_runtime;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO urbanshade_runtime;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO urbanshade_runtime;
REVOKE ALL ON TABLE public."__EFMigrationsHistory" FROM urbanshade_runtime;
-- Create a dedicated LOGIN separately, then GRANT urbanshade_runtime to that login.
-- Never grant it the deployment/owner role. Do not use the POSTGRES_USER identity for the API.
