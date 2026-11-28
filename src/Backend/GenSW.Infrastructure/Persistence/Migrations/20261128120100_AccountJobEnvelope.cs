using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GenSW.Infrastructure.Persistence.Migrations;

[DbContext(typeof(GenSWDbContext))]
[Migration("20261128120100_AccountJobEnvelope")]
public sealed class AccountJobEnvelope : Migration
{
    private const string Executions = "ExecucoesOtimizacao";
    private const string Derivations = "DerivacoesOtimizacao";
    private const string StateConstraint = "CK_ExecucoesOtimizacao_Estado";

    internal const string ExecutionState = """
        ("Estado" = 'EmCalculo' AND "Revisao" = 1 AND "QuarantinedAtUtc" IS NULL) OR
        ("Estado" = 'Finalizada' AND "Revisao" = 2 AND "QuarantinedAtUtc" IS NULL) OR
        ("Estado" = 'Quarentenada' AND "Revisao" = 2 AND "QuarantinedAtUtc" IS NOT NULL AND "QuarantinedAtUtc" >= "CreatedAtUtc")
        """;

    internal const string Scope = """
        ("AccountId" IS NULL AND "BindingId" IS NULL AND "BindingRevision" IS NULL AND "AccessGeneration" IS NULL) OR
        ("AccountId" IS NOT NULL AND "BindingId" IS NOT NULL AND "BindingRevision" IS NOT NULL AND "AccessGeneration" IS NOT NULL
            AND "AccountId" <> '00000000-0000-0000-0000-000000000000'
            AND "BindingId" <> '00000000-0000-0000-0000-000000000000'
            AND "BindingRevision" > 0 AND "AccessGeneration" > 0)
        """;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { Executions, Derivations })
        {
            migrationBuilder.AddColumn<Guid>("AccountId", table, "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>("BindingId", table, "uuid", nullable: true);
            migrationBuilder.AddColumn<long>("BindingRevision", table, "bigint", nullable: true);
            migrationBuilder.AddColumn<long>("AccessGeneration", table, "bigint", nullable: true);
            migrationBuilder.AddCheckConstraint($"CK_{table}_Escopo", table, Scope);
            migrationBuilder.CreateIndex($"UX_{table}_Escopo_Chave", table,
                ["AutorId", "Chave", "AccountId", "BindingId", "BindingRevision", "AccessGeneration"], unique: true);
            migrationBuilder.DropIndex($"IX_{table}_AutorId_Chave", table);
        }

        migrationBuilder.AddColumn<DateTimeOffset>("QuarantinedAtUtc", Executions, "timestamp with time zone", nullable: true);
        migrationBuilder.DropCheckConstraint(StateConstraint, Executions);
        migrationBuilder.AddCheckConstraint(StateConstraint, Executions, ExecutionState);
        migrationBuilder.Sql("""
            CREATE FUNCTION public.gensw_require_optimization_envelope() RETURNS trigger LANGUAGE plpgsql AS $job$
            BEGIN
                IF num_nonnulls(NEW."AccountId", NEW."BindingId", NEW."BindingRevision", NEW."AccessGeneration") = 4
                    THEN RETURN NEW;
                END IF;
                RAISE EXCEPTION 'A operação de otimização exige escopo da conta' USING ERRCODE = '23514';
            END;
            $job$;

            CREATE TRIGGER "TR_ExecucoesOtimizacao_Escopo"
                BEFORE INSERT ON public."ExecucoesOtimizacao"
                FOR EACH ROW EXECUTE FUNCTION public.gensw_require_optimization_envelope();
            CREATE TRIGGER "TR_DerivacoesOtimizacao_Escopo"
                BEFORE INSERT ON public."DerivacoesOtimizacao"
                FOR EACH ROW EXECUTE FUNCTION public.gensw_require_optimization_envelope();

            CREATE OR REPLACE FUNCTION public.gensw_guard_optimization_execution() RETURNS trigger LANGUAGE plpgsql AS $job$
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    IF NEW."Estado" = 'EmCalculo' AND NEW."Revisao" = 1 THEN RETURN NEW; END IF;
                ELSIF TG_OP = 'UPDATE' THEN
                    IF OLD."Estado" = 'EmCalculo' AND NEW."Revisao" = OLD."Revisao" + 1
                        AND (
                            (NEW."Estado" = 'Finalizada' AND NEW."QuarantinedAtUtc" IS NULL) OR
                            (NEW."Estado" = 'Quarentenada' AND current_user =
                                (SELECT pg_catalog.pg_get_userbyid(nspowner)
                                 FROM pg_catalog.pg_namespace WHERE nspname = 'gensw_scope'))
                        )
                        AND (to_jsonb(NEW) - 'Estado' - 'Revisao' - 'QuarantinedAtUtc') =
                            (to_jsonb(OLD) - 'Estado' - 'Revisao' - 'QuarantinedAtUtc')
                        THEN RETURN NEW;
                    END IF;
                END IF;
                RAISE EXCEPTION 'Transição inválida para execução de otimização' USING ERRCODE = '23514';
            END;
            $job$;

            -- Deferred checks can run after advance_fence has returned to the restricted caller.
            CREATE OR REPLACE FUNCTION public.gensw_check_optimization_execution_result()
                RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $job$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM public."ExecucoesOtimizacao" e WHERE e."Id" = NEW."Id"
                      AND (e."Estado" = 'Finalizada') <> EXISTS (
                          SELECT 1 FROM public."ResultadosOtimizacao" r WHERE r."ExecutionId" = e."Id"
                      )
                ) THEN
                    RAISE EXCEPTION 'Finalização da otimização exige um resultado na mesma transação' USING ERRCODE = '23514';
                END IF;
                RETURN NULL;
            END;
            $job$;
            REVOKE EXECUTE ON FUNCTION public.gensw_check_optimization_execution_result() FROM PUBLIC;

            CREATE FUNCTION public.gensw_quarantine_optimization_jobs(next_generation bigint) RETURNS integer
                LANGUAGE plpgsql SET search_path = pg_catalog, public AS $job$
            DECLARE affected integer;
            BEGIN
                IF next_generation IS NULL OR next_generation < 1 THEN
                    RAISE EXCEPTION 'Geração de revogação inválida' USING ERRCODE = '22023';
                END IF;
                UPDATE public."ExecucoesOtimizacao"
                SET "Estado" = 'Quarentenada', "Revisao" = 2,
                    "QuarantinedAtUtc" = GREATEST(clock_timestamp(), "CreatedAtUtc")
                WHERE "Estado" = 'EmCalculo'
                    AND ("AccessGeneration" IS NULL OR "AccessGeneration" < next_generation);
                GET DIAGNOSTICS affected = ROW_COUNT;
                RETURN affected;
            END;
            $job$;
            REVOKE EXECUTE ON FUNCTION public.gensw_quarantine_optimization_jobs(bigint) FROM PUBLIC;

            DO $migration$
            BEGIN
                PERFORM public.gensw_quarantine_optimization_jobs(1);
            END;
            $migration$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            LOCK TABLE public."ExecucoesOtimizacao", public."DerivacoesOtimizacao" IN ACCESS EXCLUSIVE MODE;
            DO $job$
            BEGIN
                IF EXISTS (SELECT 1 FROM public."ExecucoesOtimizacao" WHERE "AccountId" IS NOT NULL OR "Estado" = 'Quarentenada')
                    OR EXISTS (SELECT 1 FROM public."DerivacoesOtimizacao" WHERE "AccountId" IS NOT NULL) THEN
                    RAISE EXCEPTION 'Downgrade exige preservar a autoria e a quarentena do histórico de otimizações' USING ERRCODE = '23514';
                END IF;
            END;
            $job$;
            DROP FUNCTION public.gensw_quarantine_optimization_jobs(bigint);
            ALTER FUNCTION public.gensw_check_optimization_execution_result() SECURITY INVOKER;
            ALTER FUNCTION public.gensw_check_optimization_execution_result() RESET search_path;
            GRANT EXECUTE ON FUNCTION public.gensw_check_optimization_execution_result() TO PUBLIC;
            DROP TRIGGER "TR_DerivacoesOtimizacao_Escopo" ON public."DerivacoesOtimizacao";
            DROP TRIGGER "TR_ExecucoesOtimizacao_Escopo" ON public."ExecucoesOtimizacao";
            DROP FUNCTION public.gensw_require_optimization_envelope();

            CREATE OR REPLACE FUNCTION public.gensw_guard_optimization_execution() RETURNS trigger LANGUAGE plpgsql AS $job$
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    IF NEW."Estado" = 'EmCalculo' AND NEW."Revisao" = 1 THEN RETURN NEW; END IF;
                ELSIF TG_OP = 'UPDATE' THEN
                    IF OLD."Estado" = 'EmCalculo' AND NEW."Estado" = 'Finalizada'
                        AND NEW."Revisao" = OLD."Revisao" + 1
                        AND (to_jsonb(NEW) - 'Estado' - 'Revisao') = (to_jsonb(OLD) - 'Estado' - 'Revisao')
                        THEN RETURN NEW;
                    END IF;
                END IF;
                RAISE EXCEPTION 'Transição inválida para execução de otimização' USING ERRCODE = '23514';
            END;
            $job$;
            """);
        migrationBuilder.DropCheckConstraint(StateConstraint, Executions);
        migrationBuilder.DropColumn("QuarantinedAtUtc", Executions);
        migrationBuilder.AddCheckConstraint(StateConstraint, Executions,
            """("Estado" = 'EmCalculo' AND "Revisao" = 1) OR ("Estado" = 'Finalizada' AND "Revisao" = 2)""");

        foreach (var table in new[] { Derivations, Executions })
        {
            migrationBuilder.CreateIndex($"IX_{table}_AutorId_Chave", table, ["AutorId", "Chave"], unique: true);
            migrationBuilder.DropIndex($"UX_{table}_Escopo_Chave", table);
            migrationBuilder.DropCheckConstraint($"CK_{table}_Escopo", table);
            migrationBuilder.DropColumn("AccessGeneration", table);
            migrationBuilder.DropColumn("BindingRevision", table);
            migrationBuilder.DropColumn("BindingId", table);
            migrationBuilder.DropColumn("AccountId", table);
        }
    }
}
