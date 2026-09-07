\getenv custodia_db_name CUSTODIA_DB_NAME
\if :{?custodia_db_name}
\else
  \set custodia_db_name 'custodia'
\endif
SELECT set_config('custodia.provision_expected_db', :'custodia_db_name', false);

DO $$
DECLARE
  v_expected_db text := current_setting('custodia.provision_expected_db');
BEGIN
  IF current_database() <> v_expected_db THEN
    RAISE EXCEPTION 'custodia-role.sql: current_database() = "%", esperado "%" (variável CUSTODIA_DB_NAME, default ''custodia''). Abortando ANTES de qualquer alteração — confira o -d/--dbname do comando psql. A role custodia é global ao cluster: rodar isto contra o database errado rotaciona a senha da role custodia de verdade.',
      current_database(), v_expected_db;
  END IF;
END
$$;

\getenv custodia_app_password CUSTODIA_APP_PASSWORD
\if :{?custodia_app_password}
\else
  \set custodia_app_password ''
\endif

\o /dev/null
SELECT set_config('custodia.provision_password', :'custodia_app_password', false);
\o

BEGIN;

DO $$
DECLARE
  v_password text := current_setting('custodia.provision_password');
BEGIN
  IF v_password = '' THEN
    RAISE EXCEPTION 'CUSTODIA_APP_PASSWORD não foi definida ou está vazia — obrigatória para provisionar a role custodia.';
  END IF;

  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'custodia') THEN
    EXECUTE format(
      'ALTER ROLE custodia WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD %L',
      v_password
    );
  ELSE
    EXECUTE format(
      'CREATE ROLE custodia WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD %L',
      v_password
    );
  END IF;
END
$$;

DO $$
BEGIN
  EXECUTE format('ALTER DATABASE %I OWNER TO custodia', current_database());
  EXECUTE format('REVOKE CONNECT ON DATABASE %I FROM PUBLIC', current_database());
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO custodia', current_database());
END
$$;

COMMIT;
