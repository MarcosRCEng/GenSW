START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    CREATE TABLE "ExecucoesOtimizacao" (
        "Id" uuid NOT NULL,
        "AutorId" uuid NOT NULL,
        "Chave" character varying(100) NOT NULL,
        "Hash" character varying(64) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "DeadlineUtc" timestamp with time zone NOT NULL,
        "OwnerToken" uuid NOT NULL,
        "Estado" character varying(16) NOT NULL,
        "Revisao" integer NOT NULL,
        "EspecieId" uuid,
        "Fase" character varying(200),
        "PedidoJson" jsonb NOT NULL,
        "EntradasJson" jsonb NOT NULL,
        "ModeloJson" jsonb,
        CONSTRAINT "PK_ExecucoesOtimizacao" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_ExecucoesOtimizacao_Estado" CHECK (("Estado" = 'EmCalculo' AND "Revisao" = 1) OR ("Estado" = 'Finalizada' AND "Revisao" = 2)),
        CONSTRAINT "CK_ExecucoesOtimizacao_Hash" CHECK (length("Hash") = 64 AND length("Chave") > 0),
        CONSTRAINT "CK_ExecucoesOtimizacao_Prazo" CHECK ("DeadlineUtc" > "CreatedAtUtc"),
        CONSTRAINT "FK_ExecucoesOtimizacao_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    CREATE TABLE "ResultadosOtimizacao" (
        "ExecutionId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "Desfecho" character varying(32) NOT NULL,
        "ConteudoJson" jsonb NOT NULL,
        CONSTRAINT "PK_ResultadosOtimizacao" PRIMARY KEY ("ExecutionId"),
        CONSTRAINT "CK_ResultadosOtimizacao_Desfecho" CHECK ("Desfecho" IN ('SolucaoViavel','InviabilidadeDemonstrada','DadosIncompletos','DadosIncompativeis','LimiteTempo','ErroAlgoritmo','Inconclusivo')),
        CONSTRAINT "FK_ResultadosOtimizacao_ExecucoesOtimizacao_ExecutionId" FOREIGN KEY ("ExecutionId") REFERENCES "ExecucoesOtimizacao" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    CREATE TABLE "DerivacoesOtimizacao" (
        "Id" uuid NOT NULL,
        "ExecutionId" uuid NOT NULL,
        "AutorId" uuid NOT NULL,
        "Chave" character varying(100) NOT NULL,
        "Hash" character varying(64) NOT NULL,
        "ReceitaId" uuid NOT NULL,
        "VersaoId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "ConteudoJson" jsonb NOT NULL,
        CONSTRAINT "PK_DerivacoesOtimizacao" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_DerivacoesOtimizacao_Hash" CHECK (length("Hash") = 64 AND length("Chave") > 0),
        CONSTRAINT "FK_DerivacoesOtimizacao_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DerivacoesOtimizacao_ReceitasVersoes_VersaoId" FOREIGN KEY ("VersaoId") REFERENCES "ReceitasVersoes" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DerivacoesOtimizacao_Receitas_ReceitaId" FOREIGN KEY ("ReceitaId") REFERENCES "Receitas" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DerivacoesOtimizacao_ResultadosOtimizacao_ExecutionId" FOREIGN KEY ("ExecutionId") REFERENCES "ResultadosOtimizacao" ("ExecutionId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    CREATE UNIQUE INDEX "IX_DerivacoesOtimizacao_AutorId_Chave" ON "DerivacoesOtimizacao" ("AutorId", "Chave");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    CREATE INDEX "IX_DerivacoesOtimizacao_ExecutionId" ON "DerivacoesOtimizacao" ("ExecutionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    CREATE INDEX "IX_DerivacoesOtimizacao_ReceitaId" ON "DerivacoesOtimizacao" ("ReceitaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    CREATE UNIQUE INDEX "IX_DerivacoesOtimizacao_VersaoId" ON "DerivacoesOtimizacao" ("VersaoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    CREATE UNIQUE INDEX "IX_ExecucoesOtimizacao_AutorId_Chave" ON "ExecucoesOtimizacao" ("AutorId", "Chave");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    CREATE INDEX "IX_ExecucoesOtimizacao_CreatedAtUtc_Id" ON "ExecucoesOtimizacao" ("CreatedAtUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    CREATE INDEX "IX_ExecucoesOtimizacao_Estado_DeadlineUtc_Id" ON "ExecucoesOtimizacao" ("Estado", "DeadlineUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    CREATE INDEX "IX_ResultadosOtimizacao_Desfecho" ON "ResultadosOtimizacao" ("Desfecho");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112205407_AddFormulationOptimization') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112205407_AddFormulationOptimization', '8.0.10');
    END IF;
END $EF$;
COMMIT;
