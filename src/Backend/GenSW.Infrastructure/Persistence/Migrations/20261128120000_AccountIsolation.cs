using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GenSW.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GenSWDbContext))]
[Migration("20261128120000_AccountIsolation")]
public sealed class AccountIsolation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        -- Credentials remain central. The local row and its UUID still own the existing FKs.
        ALTER TABLE public."RefreshSessions" DROP CONSTRAINT "FK_RefreshSessions_RefreshSessions_ReplacedBySessionId";
        DELETE FROM public."RefreshSessions";
        ALTER TABLE public."RefreshSessions" ADD CONSTRAINT "FK_RefreshSessions_RefreshSessions_ReplacedBySessionId"
            FOREIGN KEY ("ReplacedBySessionId") REFERENCES public."RefreshSessions" ("Id") ON DELETE RESTRICT;
        ALTER TABLE public."RefreshSessions" ADD CONSTRAINT "CK_RefreshSessions_CentralOnly" CHECK (false);
        DELETE FROM public."AspNetUserTokens";
        ALTER TABLE public."AspNetUserTokens" ADD CONSTRAINT "CK_AspNetUserTokens_CentralOnly" CHECK (false);
        DELETE FROM public."AspNetUserLogins";
        ALTER TABLE public."AspNetUserLogins" ADD CONSTRAINT "CK_AspNetUserLogins_CentralOnly" CHECK (false);
        UPDATE public."AspNetUsers" SET "PasswordHash" = NULL, "SecurityStamp" = NULL,
            "AccessFailedCount" = 0, "LockoutEnd" = NULL, "TwoFactorEnabled" = false;
        ALTER TABLE public."AspNetUsers" ADD CONSTRAINT "CK_AspNetUsers_LocalProjection"
            CHECK ("PasswordHash" IS NULL AND "SecurityStamp" IS NULL AND
                   "AccessFailedCount" = 0 AND "LockoutEnd" IS NULL AND NOT "TwoFactorEnabled");

        CREATE SCHEMA gensw_scope;
        REVOKE ALL ON SCHEMA gensw_scope FROM PUBLIC;

        CREATE TABLE gensw_scope.destination (
            singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
            database_id uuid NOT NULL,
            account_id uuid NOT NULL,
            binding_id uuid NOT NULL,
            binding_revision bigint NOT NULL CHECK (binding_revision > 0),
            generation bigint NOT NULL CHECK (generation > 0),
            enabled boolean NOT NULL DEFAULT false
        );

        CREATE FUNCTION gensw_scope.identity()
            RETURNS TABLE(database_id uuid, account_id uuid, binding_id uuid,
                          binding_revision bigint, generation bigint, enabled boolean)
            LANGUAGE sql SECURITY DEFINER SET search_path = pg_catalog, pg_temp
            AS $function$
                SELECT d.database_id, d.account_id, d.binding_id, d.binding_revision,
                       d.generation, d.enabled
                FROM gensw_scope.destination AS d
            $function$;

        CREATE FUNCTION gensw_scope.verify_generation()
            RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp
            AS $function$
            DECLARE admitted gensw_scope.destination%ROWTYPE;
            BEGIN
                -- Hold this lock through commit. Fence changes require an exclusive tuple lock.
                SELECT * INTO admitted FROM gensw_scope.destination FOR SHARE;
                IF NOT FOUND OR NOT admitted.enabled
                   OR admitted.binding_id::text IS DISTINCT FROM current_setting('gensw.binding', true)
                   OR admitted.binding_revision::text IS DISTINCT FROM current_setting('gensw.revision', true)
                   OR admitted.generation::text IS DISTINCT FROM current_setting('gensw.generation', true)
                THEN
                    RAISE EXCEPTION USING ERRCODE = 'GS001', MESSAGE = 'ERP generation unavailable';
                END IF;
            END;
            $function$;

        CREATE FUNCTION gensw_scope.guard_write()
            RETURNS trigger LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp
            AS $function$
            BEGIN
                IF current_user <> pg_get_userbyid(
                    (SELECT nspowner FROM pg_namespace WHERE nspname = 'gensw_scope')) THEN
                    PERFORM gensw_scope.verify_generation();
                END IF;
                RETURN NULL;
            END;
            $function$;

        CREATE FUNCTION gensw_scope.advance_fence(
            expected_database uuid, expected_account uuid, expected_binding uuid,
            expected_revision bigint, next_generation bigint, quarantine boolean)
            RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp
            AS $function$
            BEGIN
                -- File responses and publication hold a shared advisory lease until completion.
                PERFORM pg_advisory_xact_lock(581, 1);
                UPDATE gensw_scope.destination
                SET generation = next_generation, enabled = false
                WHERE database_id = expected_database AND account_id = expected_account
                    AND binding_id = expected_binding AND binding_revision = expected_revision AND
                    generation <= next_generation;
                IF NOT FOUND THEN
                    RETURN false;
                END IF;
                IF quarantine AND to_regprocedure('public.gensw_quarantine_optimization_jobs(bigint)') IS NOT NULL THEN
                    PERFORM public.gensw_quarantine_optimization_jobs(next_generation);
                END IF;
                RETURN true;
            END;
            $function$;

        CREATE FUNCTION gensw_scope.ensure_write_guards()
            RETURNS void LANGUAGE plpgsql SET search_path = pg_catalog, pg_temp
            AS $function$
            DECLARE target text;
            BEGIN
                FOR target IN
                    SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p') AND c.relname <> '__EFMigrationsHistory'
                    AND NOT EXISTS (SELECT 1 FROM pg_trigger t WHERE t.tgrelid = c.oid AND t.tgname = 'gensw_generation_guard')
                LOOP
                    EXECUTE format(
                        'CREATE TRIGGER gensw_generation_guard BEFORE INSERT OR UPDATE OR DELETE OR TRUNCATE ON public.%I
                         FOR EACH STATEMENT EXECUTE FUNCTION gensw_scope.guard_write()', target);
                END LOOP;
            END;
            $function$;

        REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA gensw_scope FROM PUBLIC;

        DO $migration$
        BEGIN
            PERFORM gensw_scope.ensure_write_guards();
        END;
        $migration$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException(
        "Account isolation erased obsolete credentials. Restore an approved backup for rollback.");
}
