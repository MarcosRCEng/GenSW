namespace GenSW.Infrastructure.Persistence;

internal static class OptimizationGuardSql
{
    internal const string Install = """
        CREATE FUNCTION gensw_guard_optimization_execution() RETURNS trigger LANGUAGE plpgsql AS $$
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
        $$;

        CREATE FUNCTION gensw_guard_optimization_result() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF TG_OP = 'INSERT' THEN
                IF EXISTS (
                    SELECT 1 FROM "ExecucoesOtimizacao" e WHERE e."Id" = NEW."ExecutionId"
                      AND e."Estado" = 'Finalizada' AND NEW."CreatedAtUtc" >= e."CreatedAtUtc"
                ) THEN RETURN NEW; END IF;
            END IF;
            RAISE EXCEPTION 'Resultado de otimização exige execução finalizada e é imutável' USING ERRCODE = '23514';
        END;
        $$;

        CREATE FUNCTION gensw_check_optimization_execution_result() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF EXISTS (
                SELECT 1 FROM "ExecucoesOtimizacao" e WHERE e."Id" = NEW."Id"
                  AND (e."Estado" = 'Finalizada') <> EXISTS (
                      SELECT 1 FROM "ResultadosOtimizacao" r WHERE r."ExecutionId" = e."Id"
                  )
            ) THEN
                RAISE EXCEPTION 'Finalização da otimização exige um resultado na mesma transação' USING ERRCODE = '23514';
            END IF;
            RETURN NULL;
        END;
        $$;

        CREATE FUNCTION gensw_guard_optimization_derivation() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF TG_OP = 'INSERT' THEN
                IF EXISTS (
                    SELECT 1 FROM "ResultadosOtimizacao" r
                      JOIN "ReceitasVersoes" v ON v."Id" = NEW."VersaoId"
                    WHERE r."ExecutionId" = NEW."ExecutionId" AND r."Desfecho" = 'SolucaoViavel'
                      AND v."ReceitaId" = NEW."ReceitaId" AND v."Estado" = 'Rascunho'
                      AND v."Revisao" = 1 AND v."AutorId" = NEW."AutorId"
                      AND NEW."CreatedAtUtc" >= r."CreatedAtUtc"
                ) THEN RETURN NEW; END IF;
            END IF;
            RAISE EXCEPTION 'Derivação exige solução viável e nova versão rascunho' USING ERRCODE = '23514';
        END;
        $$;

        CREATE TRIGGER "TR_ExecucoesOtimizacao_Transicao"
            BEFORE INSERT OR UPDATE OR DELETE ON "ExecucoesOtimizacao"
            FOR EACH ROW EXECUTE FUNCTION gensw_guard_optimization_execution();
        CREATE CONSTRAINT TRIGGER "TR_ExecucoesOtimizacao_Resultado"
            AFTER INSERT OR UPDATE ON "ExecucoesOtimizacao" DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION gensw_check_optimization_execution_result();
        CREATE TRIGGER "TR_ResultadosOtimizacao_Imutavel"
            BEFORE INSERT OR UPDATE OR DELETE ON "ResultadosOtimizacao"
            FOR EACH ROW EXECUTE FUNCTION gensw_guard_optimization_result();
        CREATE TRIGGER "TR_DerivacoesOtimizacao_Imutavel"
            BEFORE INSERT OR UPDATE OR DELETE ON "DerivacoesOtimizacao"
            FOR EACH ROW EXECUTE FUNCTION gensw_guard_optimization_derivation();
        """;

    internal const string PrepareDown = """
        LOCK TABLE "ExecucoesOtimizacao", "ResultadosOtimizacao", "DerivacoesOtimizacao" IN ACCESS EXCLUSIVE MODE;
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM "ExecucoesOtimizacao") THEN
                RAISE EXCEPTION 'Downgrade de F05 preserva execuções e resultados já registrados' USING ERRCODE = '23514';
            END IF;
        END $$;
        DROP TRIGGER "TR_DerivacoesOtimizacao_Imutavel" ON "DerivacoesOtimizacao";
        DROP TRIGGER "TR_ResultadosOtimizacao_Imutavel" ON "ResultadosOtimizacao";
        DROP TRIGGER "TR_ExecucoesOtimizacao_Resultado" ON "ExecucoesOtimizacao";
        DROP TRIGGER "TR_ExecucoesOtimizacao_Transicao" ON "ExecucoesOtimizacao";
        DROP FUNCTION gensw_guard_optimization_derivation();
        DROP FUNCTION gensw_guard_optimization_result();
        DROP FUNCTION gensw_check_optimization_execution_result();
        DROP FUNCTION gensw_guard_optimization_execution();
        """;
}
