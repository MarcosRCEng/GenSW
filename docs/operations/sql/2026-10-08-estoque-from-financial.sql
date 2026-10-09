START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005132012_AddOperationalProperties') THEN
    CREATE TABLE "Propriedades" (
        "Id" uuid NOT NULL,
        "Nome" character varying(200) NOT NULL,
        "NomeNormalizado" character varying(200) NOT NULL,
        "Localizacao" character varying(500),
        "Observacao" character varying(2000),
        "Ativo" boolean NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Propriedades" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Propriedades_Nome_Canonical" CHECK ("Nome" <> '' AND "Nome" !~ U&'[\0009-\000D\0085\00A0\1680\2000-\200A\2028\2029\202F\205F\3000]' AND "Nome" !~ '(^ | $|  )')
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005132012_AddOperationalProperties') THEN
    CREATE TABLE "VinculosAnimalPropriedade" (
        "Id" uuid NOT NULL,
        "AnimalId" uuid NOT NULL,
        "PropriedadeId" uuid NOT NULL,
        "DataInicio" date NOT NULL,
        "DataFim" date,
        "Observacao" character varying(2000),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_VinculosAnimalPropriedade" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_VinculosAnimalPropriedade_Periodo" CHECK ("DataFim" IS NULL OR "DataFim" >= "DataInicio"),
        CONSTRAINT "FK_VinculosAnimalPropriedade_Animais_AnimalId" FOREIGN KEY ("AnimalId") REFERENCES "Animais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_VinculosAnimalPropriedade_Propriedades_PropriedadeId" FOREIGN KEY ("PropriedadeId") REFERENCES "Propriedades" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005132012_AddOperationalProperties') THEN
    CREATE INDEX "IX_Propriedades_Ativo_Nome_Id" ON "Propriedades" ("Ativo", "Nome", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005132012_AddOperationalProperties') THEN
    CREATE INDEX "IX_VinculosAnimalPropriedade_AnimalId_DataInicio_CreatedAtUtc_~" ON "VinculosAnimalPropriedade" ("AnimalId", "DataInicio", "CreatedAtUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005132012_AddOperationalProperties') THEN
    CREATE INDEX "IX_VinculosAnimalPropriedade_PropriedadeId_DataFim" ON "VinculosAnimalPropriedade" ("PropriedadeId", "DataFim");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005132012_AddOperationalProperties') THEN
    CREATE UNIQUE INDEX "UX_VinculosAnimalPropriedade_Animal_Atual" ON "VinculosAnimalPropriedade" ("AnimalId") WHERE "DataFim" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005132012_AddOperationalProperties') THEN
    CREATE UNIQUE INDEX "UX_Propriedades_Nome_CaseInsensitive" ON "Propriedades" ("NomeNormalizado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005132012_AddOperationalProperties') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261005132012_AddOperationalProperties', '8.0.10');
    END IF;
END $EF$;
COMMIT;

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

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE SEQUENCE "SequenciaEstoque" START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE TABLE "EventosEstoque" (
        "Id" uuid NOT NULL,
        "Sequencia" bigint NOT NULL,
        "Tipo" character varying(30) NOT NULL,
        "AutorId" uuid NOT NULL,
        "ResponsavelId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "DataOperacional" date NOT NULL,
        "DataObservada" date,
        "Motivo" character varying(2000) NOT NULL,
        "Documento" character varying(1000),
        "EventoReferenciaId" uuid,
        "Algoritmo" character varying(50) NOT NULL,
        "SnapshotJson" jsonb NOT NULL,
        CONSTRAINT "PK_EventosEstoque" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_EventosEstoque_Data" CHECK ("DataObservada" IS NULL OR "DataObservada" <= "DataOperacional"),
        CONSTRAINT "CK_EventosEstoque_Sequencia" CHECK ("Sequencia" > 0),
        CONSTRAINT "FK_EventosEstoque_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_EventosEstoque_AspNetUsers_ResponsavelId" FOREIGN KEY ("ResponsavelId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_EventosEstoque_EventosEstoque_EventoReferenciaId" FOREIGN KEY ("EventoReferenciaId") REFERENCES "EventosEstoque" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE TABLE "HistoricoEstoque" (
        "Id" uuid NOT NULL,
        "RegistroId" uuid NOT NULL,
        "Tipo" character varying(30) NOT NULL,
        "Operacao" character varying(40) NOT NULL,
        "Sequencia" bigint NOT NULL,
        "AutorId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "Motivo" character varying(2000) NOT NULL,
        "AntesJson" jsonb NOT NULL,
        "DepoisJson" jsonb NOT NULL,
        CONSTRAINT "PK_HistoricoEstoque" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_HistoricoEstoque_Sequencia" CHECK ("Sequencia" > 0),
        CONSTRAINT "FK_HistoricoEstoque_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE TABLE "LocaisEstoque" (
        "Id" uuid NOT NULL,
        "Codigo" character varying(50) NOT NULL,
        "CodigoNormalizado" character varying(50) NOT NULL,
        "Nome" character varying(200) NOT NULL,
        "Descricao" character varying(2000),
        "PropriedadeId" uuid,
        "Finalidade" character varying(20) NOT NULL,
        "Ativo" boolean NOT NULL,
        "Revisao" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_LocaisEstoque" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_LocaisEstoque_Finalidade" CHECK ("Finalidade" IN ('Ordinario','Segregacao')),
        CONSTRAINT "CK_LocaisEstoque_Revisao" CHECK ("Revisao" > 0),
        CONSTRAINT "FK_LocaisEstoque_Propriedades_PropriedadeId" FOREIGN KEY ("PropriedadeId") REFERENCES "Propriedades" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE TABLE "LotesMateriais" (
        "Id" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Codigo" character varying(50) NOT NULL,
        "CodigoNormalizado" character varying(50) NOT NULL,
        "CodigoExterno" character varying(100),
        "Origem" character varying(1000) NOT NULL,
        "Fonte" character varying(1000) NOT NULL,
        "ResponsavelId" uuid NOT NULL,
        "DataOrigem" date,
        "Fabricacao" date,
        "Coleta" date,
        "Validade" date,
        "FonteValidade" character varying(1000),
        "ResponsavelValidadeId" uuid,
        "Situacao" character varying(20) NOT NULL,
        "Ativo" boolean NOT NULL,
        "Unidade" character varying(3) NOT NULL,
        "PerfilNutricionalId" uuid,
        "ConversaoItemId" uuid,
        "Aplicabilidade" character varying(2000),
        "Revisao" integer NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_LotesMateriais" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_LotesMateriais_Revisao" CHECK ("Revisao" > 0),
        CONSTRAINT "CK_LotesMateriais_Situacao" CHECK ("Situacao" IN ('Liberado','Bloqueado','Encerrado')),
        CONSTRAINT "CK_LotesMateriais_Unidade" CHECK ("Unidade" IN ('kg','L','un')),
        CONSTRAINT "CK_LotesMateriais_Validade" CHECK ("Validade" IS NULL OR ("FonteValidade" IS NOT NULL AND length(trim("FonteValidade")) > 0 AND "ResponsavelValidadeId" IS NOT NULL AND ("DataOrigem" IS NULL OR "Validade" >= "DataOrigem") AND ("Fabricacao" IS NULL OR "Validade" >= "Fabricacao") AND ("Coleta" IS NULL OR "Validade" >= "Coleta"))),
        CONSTRAINT "FK_LotesMateriais_AspNetUsers_ResponsavelId" FOREIGN KEY ("ResponsavelId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_LotesMateriais_AspNetUsers_ResponsavelValidadeId" FOREIGN KEY ("ResponsavelValidadeId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_LotesMateriais_ConversoesItens_ConversaoItemId" FOREIGN KEY ("ConversaoItemId") REFERENCES "ConversoesItens" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_LotesMateriais_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_LotesMateriais_PerfisNutricionais_PerfilNutricionalId" FOREIGN KEY ("PerfilNutricionalId") REFERENCES "PerfisNutricionais" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE TABLE "ComandosEstoque" (
        "Id" uuid NOT NULL,
        "AutorId" uuid NOT NULL,
        "Operacao" character varying(40) NOT NULL,
        "RecursoId" uuid NOT NULL,
        "Chave" character varying(100) NOT NULL,
        "HashPayload" character varying(64) NOT NULL,
        "StatusHttp" integer NOT NULL,
        "ExigeAdmin" boolean NOT NULL,
        "Location" character varying(300),
        "RespostaJson" text NOT NULL,
        "EventoId" uuid,
        "RegistroId" uuid,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ComandosEstoque" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_ComandosEstoque_Sucesso" CHECK ("StatusHttp" IN (200,201) AND length("Chave") BETWEEN 1 AND 100 AND length("HashPayload") = 64),
        CONSTRAINT "FK_ComandosEstoque_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ComandosEstoque_EventosEstoque_EventoId" FOREIGN KEY ("EventoId") REFERENCES "EventosEstoque" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE TABLE "PosicoesEstoque" (
        "LoteId" uuid NOT NULL,
        "LocalId" uuid NOT NULL,
        "Unidade" character varying(3) NOT NULL,
        "Quantidade" numeric(20,6) NOT NULL,
        "Revisao" integer NOT NULL,
        CONSTRAINT "PK_PosicoesEstoque" PRIMARY KEY ("LoteId", "LocalId"),
        CONSTRAINT "CK_PosicoesEstoque_Quantidade" CHECK ("Quantidade" >= 0),
        CONSTRAINT "CK_PosicoesEstoque_Revisao" CHECK ("Revisao" > 0),
        CONSTRAINT "CK_PosicoesEstoque_Unidade" CHECK ("Unidade" IN ('kg','L','un') AND ("Unidade" <> 'un' OR "Quantidade" = round("Quantidade",0))),
        CONSTRAINT "FK_PosicoesEstoque_LocaisEstoque_LocalId" FOREIGN KEY ("LocalId") REFERENCES "LocaisEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PosicoesEstoque_LotesMateriais_LoteId" FOREIGN KEY ("LoteId") REFERENCES "LotesMateriais" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE TABLE "MovimentosEstoque" (
        "Id" uuid NOT NULL,
        "EventoId" uuid NOT NULL,
        "Ordinal" integer NOT NULL,
        "LoteId" uuid NOT NULL,
        "LocalId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Unidade" character varying(3) NOT NULL,
        "Sentido" character varying(10) NOT NULL,
        "Quantidade" numeric(20,6) NOT NULL,
        "QuantidadeDeclarada" character varying(21) NOT NULL,
        "UnidadeDeclarada" character varying(3) NOT NULL,
        "Residuo" character varying(60) NOT NULL,
        "SaldoAnterior" numeric(20,6) NOT NULL,
        "SaldoPosterior" numeric(20,6) NOT NULL,
        "ConversaoItemId" uuid,
        "PerfilNutricionalId" uuid,
        "SnapshotJson" jsonb NOT NULL,
        "TipoEvento" character varying(30) NOT NULL,
        CONSTRAINT "PK_MovimentosEstoque" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_MovimentosEstoque_Declarado" CHECK ("QuantidadeDeclarada" ~ '^[0-9]{1,14}(\.[0-9]{1,6})?$' AND ("QuantidadeDeclarada"::numeric > 0 OR ("TipoEvento" = 'Ajuste' AND "QuantidadeDeclarada"::numeric = 0)) AND ("UnidadeDeclarada" <> 'un' OR "QuantidadeDeclarada"::numeric = trunc("QuantidadeDeclarada"::numeric)) AND "Residuo" ~ '^-?[0-9]+(\.[0-9]+)?$'),
        CONSTRAINT "CK_MovimentosEstoque_Ordinal" CHECK ("Ordinal" > 0),
        CONSTRAINT "CK_MovimentosEstoque_Quantidade" CHECK ("Quantidade" > 0 AND "SaldoAnterior" >= 0 AND "SaldoPosterior" >= 0),
        CONSTRAINT "CK_MovimentosEstoque_Sentido" CHECK ("Sentido" IN ('Entrada','Saida') AND "SaldoPosterior" = "SaldoAnterior" + CASE WHEN "Sentido" = 'Entrada' THEN "Quantidade" ELSE -"Quantidade" END),
        CONSTRAINT "CK_MovimentosEstoque_Unidade" CHECK ("Unidade" IN ('kg','L','un') AND "UnidadeDeclarada" IN ('kg','g','L','mL','un') AND ("Unidade" <> 'un' OR ("Quantidade" = round("Quantidade",0) AND "SaldoAnterior" = round("SaldoAnterior",0) AND "SaldoPosterior" = round("SaldoPosterior",0)))),
        CONSTRAINT "FK_MovimentosEstoque_ConversoesItens_ConversaoItemId" FOREIGN KEY ("ConversaoItemId") REFERENCES "ConversoesItens" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MovimentosEstoque_EventosEstoque_EventoId" FOREIGN KEY ("EventoId") REFERENCES "EventosEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MovimentosEstoque_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MovimentosEstoque_PerfisNutricionais_PerfilNutricionalId" FOREIGN KEY ("PerfilNutricionalId") REFERENCES "PerfisNutricionais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MovimentosEstoque_PosicoesEstoque_LoteId_LocalId" FOREIGN KEY ("LoteId", "LocalId") REFERENCES "PosicoesEstoque" ("LoteId", "LocalId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE UNIQUE INDEX "IX_ComandosEstoque_AutorId_Operacao_RecursoId_Chave" ON "ComandosEstoque" ("AutorId", "Operacao", "RecursoId", "Chave");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_ComandosEstoque_EventoId" ON "ComandosEstoque" ("EventoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_EventosEstoque_AutorId" ON "EventosEstoque" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_EventosEstoque_EventoReferenciaId" ON "EventosEstoque" ("EventoReferenciaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_EventosEstoque_ResponsavelId" ON "EventosEstoque" ("ResponsavelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE UNIQUE INDEX "IX_EventosEstoque_Sequencia" ON "EventosEstoque" ("Sequencia");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_EventosEstoque_Tipo_DataOperacional_Id" ON "EventosEstoque" ("Tipo", "DataOperacional", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_HistoricoEstoque_AutorId" ON "HistoricoEstoque" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_HistoricoEstoque_RegistroId_Sequencia_Id" ON "HistoricoEstoque" ("RegistroId", "Sequencia", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE UNIQUE INDEX "IX_HistoricoEstoque_Sequencia" ON "HistoricoEstoque" ("Sequencia");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_LocaisEstoque_Ativo_Nome_Id" ON "LocaisEstoque" ("Ativo", "Nome", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE UNIQUE INDEX "IX_LocaisEstoque_CodigoNormalizado" ON "LocaisEstoque" ("CodigoNormalizado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_LocaisEstoque_PropriedadeId" ON "LocaisEstoque" ("PropriedadeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE UNIQUE INDEX "IX_LotesMateriais_CodigoNormalizado" ON "LotesMateriais" ("CodigoNormalizado");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_LotesMateriais_ConversaoItemId" ON "LotesMateriais" ("ConversaoItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_LotesMateriais_ItemId_Situacao_Validade_Id" ON "LotesMateriais" ("ItemId", "Situacao", "Validade", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_LotesMateriais_PerfilNutricionalId" ON "LotesMateriais" ("PerfilNutricionalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_LotesMateriais_ResponsavelId" ON "LotesMateriais" ("ResponsavelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_LotesMateriais_ResponsavelValidadeId" ON "LotesMateriais" ("ResponsavelValidadeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_MovimentosEstoque_ConversaoItemId" ON "MovimentosEstoque" ("ConversaoItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE UNIQUE INDEX "IX_MovimentosEstoque_EventoId_Ordinal" ON "MovimentosEstoque" ("EventoId", "Ordinal");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_MovimentosEstoque_ItemId" ON "MovimentosEstoque" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_MovimentosEstoque_LoteId_LocalId_EventoId" ON "MovimentosEstoque" ("LoteId", "LocalId", "EventoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_MovimentosEstoque_PerfilNutricionalId" ON "MovimentosEstoque" ("PerfilNutricionalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE UNIQUE INDEX "UX_MovimentosEstoque_Abertura" ON "MovimentosEstoque" ("LoteId", "LocalId") WHERE "TipoEvento" = 'Abertura';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE INDEX "IX_PosicoesEstoque_LocalId" ON "PosicoesEstoque" ("LocalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    CREATE FUNCTION "InventoryImmutable"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN RAISE EXCEPTION 'Stock facts are immutable' USING ERRCODE='23514'; END $$;
    CREATE TRIGGER "TR_EventosEstoque_Immutable" BEFORE UPDATE OR DELETE ON "EventosEstoque" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_MovimentosEstoque_Immutable" BEFORE UPDATE OR DELETE ON "MovimentosEstoque" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_HistoricoEstoque_Immutable" BEFORE UPDATE OR DELETE ON "HistoricoEstoque" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_ComandosEstoque_Immutable" BEFORE UPDATE OR DELETE ON "ComandosEstoque" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();

    CREATE FUNCTION "InventoryLotGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE item_unit text;
    BEGIN
      IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Lots cannot be deleted' USING ERRCODE='23514'; END IF;
      IF TG_OP='UPDATE' AND (NEW."ItemId"<>OLD."ItemId" OR NEW."Unidade"<>OLD."Unidade" OR NEW."Codigo"<>OLD."Codigo" OR NEW."CodigoNormalizado"<>OLD."CodigoNormalizado" OR (OLD."Situacao"='Encerrado' AND NEW."Situacao"<>'Encerrado')) THEN
        RAISE EXCEPTION 'Lot identity and terminal state are immutable' USING ERRCODE='23514';
      END IF;
      SELECT "Unidade" INTO item_unit FROM "Itens" WHERE "Id"=NEW."ItemId";
      IF item_unit IS DISTINCT FROM NEW."Unidade" THEN RAISE EXCEPTION 'Lot unit differs from item' USING ERRCODE='23514'; END IF;
      IF NEW."Situacao"='Encerrado' AND EXISTS(SELECT 1 FROM "PosicoesEstoque" WHERE "LoteId"=NEW."Id" AND "Quantidade"<>0) THEN RAISE EXCEPTION 'Closing requires zero stock' USING ERRCODE='23514'; END IF;
      IF NEW."PerfilNutricionalId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "PerfisNutricionais" WHERE "Id"=NEW."PerfilNutricionalId" AND "ItemId"=NEW."ItemId") THEN
        RAISE EXCEPTION 'Profile belongs to another item' USING ERRCODE='23514'; END IF;
      IF NEW."ConversaoItemId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "ConversoesItens" WHERE "Id"=NEW."ConversaoItemId" AND "ItemId"=NEW."ItemId") THEN
        RAISE EXCEPTION 'Conversion belongs to another item' USING ERRCODE='23514'; END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_LotesMateriais_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "LotesMateriais" FOR EACH ROW EXECUTE FUNCTION "InventoryLotGuard"();

    CREATE FUNCTION "InventoryFixedItemGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
      IF NOT EXISTS(SELECT 1 FROM "Itens" WHERE "Id"=NEW."ItemId" AND "UnidadeFixada" AND "Unidade"=NEW."Unidade") THEN
        RAISE EXCEPTION 'Physical stock requires fixed item unit' USING ERRCODE='23514'; END IF;
      RETURN NULL;
    END $$;
    CREATE CONSTRAINT TRIGGER "TR_LotesMateriais_FixedUnit" AFTER INSERT OR UPDATE ON "LotesMateriais" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "InventoryFixedItemGuard"();

    CREATE FUNCTION "InventoryPositionGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
      IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Zero positions are retained' USING ERRCODE='23514'; END IF;
      IF TG_OP='UPDATE' AND (NEW."LoteId"<>OLD."LoteId" OR NEW."LocalId"<>OLD."LocalId" OR NEW."Unidade"<>OLD."Unidade") THEN RAISE EXCEPTION 'Position identity is immutable' USING ERRCODE='23514'; END IF;
      IF NOT EXISTS(SELECT 1 FROM "LotesMateriais" WHERE "Id"=NEW."LoteId" AND "Unidade"=NEW."Unidade" AND ("Situacao"<>'Encerrado' OR NEW."Quantidade"=0)) THEN
        RAISE EXCEPTION 'Position unit or terminal lot is invalid' USING ERRCODE='23514'; END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_PosicoesEstoque_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "PosicoesEstoque" FOR EACH ROW EXECUTE FUNCTION "InventoryPositionGuard"();

    CREATE FUNCTION "InventoryMovementGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE q jsonb; acceptance jsonb; calculated numeric; normalized numeric; residue numeric; scaled numeric; integer_part numeric; even_value numeric;
    BEGIN
      IF NOT EXISTS(SELECT 1 FROM "LotesMateriais" WHERE "Id"=NEW."LoteId" AND "ItemId"=NEW."ItemId" AND "Unidade"=NEW."Unidade") THEN RAISE EXCEPTION 'Movement lot identity mismatch' USING ERRCODE='23514'; END IF;
      IF NOT EXISTS(SELECT 1 FROM "EventosEstoque" WHERE "Id"=NEW."EventoId" AND "Tipo"=NEW."TipoEvento") THEN RAISE EXCEPTION 'Movement event type mismatch' USING ERRCODE='23514'; END IF;
      IF NEW."ConversaoItemId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "ConversoesItens" WHERE "Id"=NEW."ConversaoItemId" AND "ItemId"=NEW."ItemId") THEN RAISE EXCEPTION 'Movement conversion item mismatch' USING ERRCODE='23514'; END IF;
      IF NEW."PerfilNutricionalId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "PerfisNutricionais" WHERE "Id"=NEW."PerfilNutricionalId" AND "ItemId"=NEW."ItemId") THEN RAISE EXCEPTION 'Movement profile item mismatch' USING ERRCODE='23514'; END IF;
      q:=NEW."SnapshotJson" #> '{previa,quantidade}'; acceptance:=NEW."SnapshotJson" #> '{pedido,aceiteQuantizacao}';
      IF q IS NULL OR q->>'declarada' IS NULL OR q->>'unidadeDeclarada' IS NULL OR q->>'calculada' IS NULL OR q->>'normalizada' IS NULL OR q->>'residuo' IS NULL OR q->>'unidade' IS NULL OR q->>'exigeAceite' IS NULL THEN RAISE EXCEPTION 'Missing physical quantity snapshot' USING ERRCODE='23514'; END IF;
      calculated:=(q->>'calculada')::numeric; normalized:=(q->>'normalizada')::numeric; residue:=(q->>'residuo')::numeric;
      IF normalized<>NEW."Quantidade" OR residue<>NEW."Residuo"::numeric OR calculated<>normalized+residue OR q->>'unidade'<>NEW."Unidade" OR q->>'unidadeDeclarada'<>NEW."UnidadeDeclarada" OR q->>'declarada'<>NEW."QuantidadeDeclarada" THEN
        RAISE EXCEPTION 'Physical snapshot differs from movement' USING ERRCODE='23514'; END IF;
      scaled:=calculated*1000000; integer_part:=trunc(scaled);
      even_value:=CASE WHEN scaled-integer_part=0.5 THEN (integer_part+mod(integer_part,2))/1000000 ELSE round(scaled)/1000000 END;
      IF normalized<>even_value OR calculated<=0 THEN RAISE EXCEPTION 'Physical quantization mismatch' USING ERRCODE='23514'; END IF;
      IF (q->>'exigeAceite')::boolean THEN
        IF acceptance IS NULL OR acceptance='null'::jsonb OR acceptance->>'calculado' IS NULL OR acceptance->>'normalizado' IS NULL OR acceptance->>'residuo' IS NULL OR acceptance->>'motivo' IS NULL THEN RAISE EXCEPTION 'Missing quantization acceptance' USING ERRCODE='23514'; END IF;
        IF (acceptance->>'calculado')::numeric<>calculated OR (acceptance->>'normalizado')::numeric<>normalized OR (acceptance->>'residuo')::numeric<>residue OR length(trim(acceptance->>'motivo'))=0 OR residue=0 THEN RAISE EXCEPTION 'Quantization acceptance mismatch' USING ERRCODE='23514'; END IF;
      ELSIF residue<>0 OR (acceptance IS NOT NULL AND acceptance<>'null'::jsonb) THEN RAISE EXCEPTION 'Unaccepted physical residue' USING ERRCODE='23514'; END IF;
      IF NEW."TipoEvento"='Ajuste' AND NEW."QuantidadeDeclarada"::numeric<>NEW."SaldoPosterior" THEN RAISE EXCEPTION 'Counted target differs from resulting balance' USING ERRCODE='23514'; END IF;
      IF NEW."TipoEvento"='Abertura' AND EXISTS(SELECT 1 FROM "MovimentosEstoque" m JOIN "EventosEstoque" e ON e."Id"=m."EventoId" JOIN "EventosEstoque" current_event ON current_event."Id"=NEW."EventoId" WHERE m."LoteId"=NEW."LoteId" AND m."LocalId"=NEW."LocalId" AND e."Sequencia"<current_event."Sequencia") THEN RAISE EXCEPTION 'Opening requires untouched position' USING ERRCODE='23514'; END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_MovimentosEstoque_Guard" BEFORE INSERT ON "MovimentosEstoque" FOR EACH ROW EXECUTE FUNCTION "InventoryMovementGuard"();

    CREATE FUNCTION "InventoryEventGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE event_id uuid; event_type text; n integer; credits integer; debits integer; lots integer; places integer; net numeric;
    BEGIN
      IF TG_TABLE_NAME='EventosEstoque' THEN event_id:=NEW."Id"; ELSE event_id:=NEW."EventoId"; END IF;
      SELECT "Tipo" INTO event_type FROM "EventosEstoque" WHERE "Id"=event_id;
      IF event_type NOT IN ('Abertura','Entrada','Transferencia','SaidaManual','ConsumoInterno','Ajuste','Segregacao','RetornoSegregacao','Descarte') THEN RAISE EXCEPTION 'Unknown stock event type' USING ERRCODE='23514'; END IF;
      SELECT count(*),count(*) FILTER(WHERE "Sentido"='Entrada'),count(*) FILTER(WHERE "Sentido"='Saida'),count(DISTINCT "LoteId"),count(DISTINCT "LocalId"),sum(CASE WHEN "Sentido"='Entrada' THEN "Quantidade" ELSE -"Quantidade" END) INTO n,credits,debits,lots,places,net FROM "MovimentosEstoque" WHERE "EventoId"=event_id;
      IF event_type IN ('Transferencia','Segregacao','RetornoSegregacao') THEN
        IF n<>2 OR credits<>1 OR debits<>1 OR lots<>1 OR places<>2 OR net<>0 THEN RAISE EXCEPTION 'Transfer requires equal debit and credit' USING ERRCODE='23514'; END IF;
      ELSIF n<>1 OR (event_type IN ('Entrada','Abertura') AND credits<>1) OR (event_type IN ('SaidaManual','ConsumoInterno','Descarte') AND debits<>1) THEN RAISE EXCEPTION 'Invalid stock event cardinality' USING ERRCODE='23514'; END IF;
      RETURN NULL;
    END $$;
    CREATE CONSTRAINT TRIGGER "TR_EventosEstoque_Cardinality" AFTER INSERT ON "EventosEstoque" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "InventoryEventGuard"();
    CREATE CONSTRAINT TRIGGER "TR_MovimentosEstoque_Cardinality" AFTER INSERT ON "MovimentosEstoque" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "InventoryEventGuard"();

    CREATE FUNCTION "InventoryReconcileGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE projection numeric; ledger numeric;
    BEGIN
      SELECT "Quantidade" INTO projection FROM "PosicoesEstoque" WHERE "LoteId"=NEW."LoteId" AND "LocalId"=NEW."LocalId";
      SELECT coalesce(sum(CASE WHEN "Sentido"='Entrada' THEN "Quantidade" ELSE -"Quantidade" END),0) INTO ledger FROM "MovimentosEstoque" WHERE "LoteId"=NEW."LoteId" AND "LocalId"=NEW."LocalId";
      IF projection IS NULL OR projection<>ledger THEN RAISE EXCEPTION 'Stock projection differs from ledger' USING ERRCODE='23514'; END IF;
      RETURN NULL;
    END $$;
    CREATE CONSTRAINT TRIGGER "TR_PosicoesEstoque_Reconcile" AFTER INSERT OR UPDATE ON "PosicoesEstoque" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "InventoryReconcileGuard"();
    CREATE CONSTRAINT TRIGGER "TR_MovimentosEstoque_Reconcile" AFTER INSERT ON "MovimentosEstoque" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "InventoryReconcileGuard"();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007225228_AddManualInventory') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261007225228_AddManualInventory', '8.0.10');
    END IF;
END $EF$;
COMMIT;
