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
