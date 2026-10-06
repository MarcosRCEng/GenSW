START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE TABLE "CategoriasItens" (
        "Id" uuid NOT NULL,
        "Nome" character varying(200) NOT NULL,
        "NomeNormalizado" character varying(200) NOT NULL,
        "Ativo" boolean NOT NULL,
        "Revisao" integer NOT NULL,
        CONSTRAINT "PK_CategoriasItens" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_CategoriasItens_Revisao" CHECK ("Revisao" > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE TABLE "HistoricoFormulacao" (
        "Id" uuid NOT NULL,
        "RegistroId" uuid NOT NULL,
        "Tipo" character varying(30) NOT NULL,
        "Operacao" character varying(30) NOT NULL,
        "AutorId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "AntesJson" jsonb NOT NULL,
        "DepoisJson" jsonb NOT NULL,
        CONSTRAINT "PK_HistoricoFormulacao" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE TABLE "Receitas" (
        "Id" uuid NOT NULL,
        "Codigo" character varying(50) NOT NULL,
        "CodigoNormalizado" character varying(50) NOT NULL,
        "Nome" character varying(200) NOT NULL,
        "Finalidade" character varying(2000) NOT NULL,
        "Ativo" boolean NOT NULL,
        "Revisao" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Receitas" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Receitas_Revisao" CHECK ("Revisao" > 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE TABLE "SnapshotsFormulacao" (
        "Id" uuid NOT NULL,
        "Tipo" character varying(20) NOT NULL,
        "AutorId" uuid NOT NULL,
        "Chave" character varying(100) NOT NULL,
        "Hash" character varying(64) NOT NULL,
        "ConteudoJson" jsonb NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_SnapshotsFormulacao" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_SnapshotsFormulacao_Tipo" CHECK ("Tipo" IN ('Simulacao','Comparacao'))
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE TABLE "Itens" (
        "Id" uuid NOT NULL,
        "Codigo" character varying(50) NOT NULL,
        "CodigoNormalizado" character varying(50) NOT NULL,
        "Nome" character varying(200) NOT NULL,
        "Descricao" character varying(2000),
        "CategoriaId" uuid,
        "Classe" character varying(30) NOT NULL,
        "Unidade" character varying(3) NOT NULL,
        "PodeEntrar" boolean NOT NULL,
        "PodeProduzir" boolean NOT NULL,
        "UsoInterno" boolean NOT NULL,
        "Venda" boolean NOT NULL,
        "Ativo" boolean NOT NULL,
        "UnidadeFixada" boolean NOT NULL,
        "Revisao" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Itens" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Itens_Classe" CHECK ("Classe" IN ('Alimentar', 'OutroMaterialIncorporado', 'Embalagem', 'Consumivel')),
        CONSTRAINT "CK_Itens_Revisao" CHECK ("Revisao" > 0),
        CONSTRAINT "CK_Itens_Unidade" CHECK ("Unidade" IN ('kg', 'L', 'un')),
        CONSTRAINT "FK_Itens_CategoriasItens_CategoriaId" FOREIGN KEY ("CategoriaId") REFERENCES "CategoriasItens" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE TABLE "ReceitasVersoes" (
        "Id" uuid NOT NULL,
        "ReceitaId" uuid NOT NULL,
        "Numero" integer NOT NULL,
        "Revisao" integer NOT NULL,
        "Estado" character varying(20) NOT NULL,
        "ConteudoJson" jsonb NOT NULL,
        "AutorId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "PublishedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_ReceitasVersoes" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_ReceitasVersoes_Estado" CHECK ("Estado" IN ('Rascunho','Publicado','Inativo')),
        CONSTRAINT "CK_ReceitasVersoes_Versao" CHECK ("Numero" > 0 AND "Revisao" > 0),
        CONSTRAINT "FK_ReceitasVersoes_Receitas_ReceitaId" FOREIGN KEY ("ReceitaId") REFERENCES "Receitas" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE TABLE "ConversoesItens" (
        "Id" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Numero" integer NOT NULL,
        "Origem" character varying(3) NOT NULL,
        "Destino" character varying(3) NOT NULL,
        "Fator" numeric(20,6) NOT NULL,
        "Fonte" character varying(1000) NOT NULL,
        "Metodo" character varying(500) NOT NULL,
        "DataFonte" date NOT NULL,
        "Contexto" character varying(2000) NOT NULL,
        "ReferenciaAmostra" character varying(200) NOT NULL,
        "Proveniencia" character varying(20) NOT NULL,
        "AutorId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ConversoesItens" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_ConversoesItens_Fator" CHECK ("Fator" > 0),
        CONSTRAINT "CK_ConversoesItens_Numero" CHECK ("Numero" > 0),
        CONSTRAINT "CK_ConversoesItens_Unidades" CHECK ("Origem" IN ('kg','g','L','mL','un') AND "Destino" IN ('kg','g','L','mL','un') AND "Origem" <> "Destino"),
        CONSTRAINT "FK_ConversoesItens_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE TABLE "PerfisNutricionais" (
        "Id" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Numero" integer NOT NULL,
        "Revisao" integer NOT NULL,
        "Estado" character varying(20) NOT NULL,
        "ConteudoJson" jsonb NOT NULL,
        "AutorId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "PublishedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_PerfisNutricionais" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_PerfisNutricionais_Estado" CHECK ("Estado" IN ('Rascunho','Publicado','Inativo')),
        CONSTRAINT "CK_PerfisNutricionais_Versao" CHECK ("Numero" > 0 AND "Revisao" > 0),
        CONSTRAINT "FK_PerfisNutricionais_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE TABLE "ReceitasReferencias" (
        "Id" uuid NOT NULL,
        "VersaoId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "PerfilId" uuid,
        "ConversaoId" uuid,
        "SubVersaoId" uuid,
        CONSTRAINT "PK_ReceitasReferencias" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ReceitasReferencias_ConversoesItens_ConversaoId" FOREIGN KEY ("ConversaoId") REFERENCES "ConversoesItens" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ReceitasReferencias_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ReceitasReferencias_PerfisNutricionais_PerfilId" FOREIGN KEY ("PerfilId") REFERENCES "PerfisNutricionais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ReceitasReferencias_ReceitasVersoes_SubVersaoId" FOREIGN KEY ("SubVersaoId") REFERENCES "ReceitasVersoes" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ReceitasReferencias_ReceitasVersoes_VersaoId" FOREIGN KEY ("VersaoId") REFERENCES "ReceitasVersoes" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE UNIQUE INDEX "IX_CategoriasItens_NomeNormalizado" ON "CategoriasItens" ("NomeNormalizado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE UNIQUE INDEX "IX_ConversoesItens_ItemId_Numero" ON "ConversoesItens" ("ItemId", "Numero");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE INDEX "IX_HistoricoFormulacao_RegistroId_CreatedAtUtc_Id" ON "HistoricoFormulacao" ("RegistroId", "CreatedAtUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE INDEX "IX_Itens_Ativo_Nome_Id" ON "Itens" ("Ativo", "Nome", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE INDEX "IX_Itens_CategoriaId" ON "Itens" ("CategoriaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE UNIQUE INDEX "IX_Itens_CodigoNormalizado" ON "Itens" ("CodigoNormalizado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE UNIQUE INDEX "IX_PerfisNutricionais_ItemId_Numero" ON "PerfisNutricionais" ("ItemId", "Numero");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE INDEX "IX_Receitas_Ativo_Nome_Id" ON "Receitas" ("Ativo", "Nome", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE UNIQUE INDEX "IX_Receitas_CodigoNormalizado" ON "Receitas" ("CodigoNormalizado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE INDEX "IX_ReceitasReferencias_ConversaoId" ON "ReceitasReferencias" ("ConversaoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE INDEX "IX_ReceitasReferencias_ItemId" ON "ReceitasReferencias" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE INDEX "IX_ReceitasReferencias_PerfilId" ON "ReceitasReferencias" ("PerfilId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE INDEX "IX_ReceitasReferencias_SubVersaoId" ON "ReceitasReferencias" ("SubVersaoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE INDEX "IX_ReceitasReferencias_VersaoId" ON "ReceitasReferencias" ("VersaoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE UNIQUE INDEX "IX_ReceitasVersoes_ReceitaId_Numero" ON "ReceitasVersoes" ("ReceitaId", "Numero");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE UNIQUE INDEX "IX_SnapshotsFormulacao_AutorId_Tipo_Chave" ON "SnapshotsFormulacao" ("AutorId", "Tipo", "Chave");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE INDEX "IX_SnapshotsFormulacao_Tipo_CreatedAtUtc_Id" ON "SnapshotsFormulacao" ("Tipo", "CreatedAtUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    CREATE FUNCTION "FormulationImmutable"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
        RAISE EXCEPTION 'Published formulation record is immutable' USING ERRCODE = '23514';
    END; $$;
    CREATE TRIGGER "TR_SnapshotsFormulacao_Immutable" BEFORE UPDATE OR DELETE ON "SnapshotsFormulacao"
        FOR EACH ROW EXECUTE FUNCTION "FormulationImmutable"();
    CREATE TRIGGER "TR_HistoricoFormulacao_Immutable" BEFORE UPDATE OR DELETE ON "HistoricoFormulacao"
        FOR EACH ROW EXECUTE FUNCTION "FormulationImmutable"();
    CREATE TRIGGER "TR_ConversoesItens_Immutable" BEFORE UPDATE OR DELETE ON "ConversoesItens"
        FOR EACH ROW EXECUTE FUNCTION "FormulationImmutable"();
    CREATE TRIGGER "TR_ReceitasReferencias_Immutable" BEFORE UPDATE OR DELETE ON "ReceitasReferencias"
        FOR EACH ROW EXECUTE FUNCTION "FormulationImmutable"();
    CREATE FUNCTION "FormulationVersionGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
        IF OLD."Estado" <> 'Rascunho' THEN
            IF TG_OP = 'DELETE' THEN
                RAISE EXCEPTION 'Published version cannot be deleted' USING ERRCODE = '23514';
            END IF;
            IF OLD."Estado" <> 'Publicado' OR NEW."Estado" <> 'Inativo'
                OR (to_jsonb(NEW) - 'Estado' - 'Revisao') <> (to_jsonb(OLD) - 'Estado' - 'Revisao')
                OR NEW."Revisao" <> OLD."Revisao" + 1 THEN
                RAISE EXCEPTION 'Published version is immutable' USING ERRCODE = '23514';
            END IF;
        END IF;
        IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
        RETURN NEW;
    END; $$;
    CREATE TRIGGER "TR_PerfisNutricionais_VersionGuard" BEFORE UPDATE OR DELETE ON "PerfisNutricionais"
        FOR EACH ROW EXECUTE FUNCTION "FormulationVersionGuard"();
    CREATE TRIGGER "TR_ReceitasVersoes_VersionGuard" BEFORE UPDATE OR DELETE ON "ReceitasVersoes"
        FOR EACH ROW EXECUTE FUNCTION "FormulationVersionGuard"();
    CREATE FUNCTION "FormulationItemUnitGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
        IF OLD."UnidadeFixada" AND (NOT NEW."UnidadeFixada" OR NEW."Unidade" <> OLD."Unidade") THEN
            RAISE EXCEPTION 'Published canonical unit is immutable' USING ERRCODE = '23514';
        END IF;
        RETURN NEW;
    END; $$;
    CREATE TRIGGER "TR_Itens_UnitGuard" BEFORE UPDATE ON "Itens"
        FOR EACH ROW EXECUTE FUNCTION "FormulationItemUnitGuard"();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006220939_AddCatalogAndFormulation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261006220939_AddCatalogAndFormulation', '8.0.10');
    END IF;
END $EF$;
COMMIT;
