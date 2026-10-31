START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE TABLE "AncorasConciliacaoOvos" (
        "Id" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Revisao" bigint NOT NULL,
        "AutorId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "SequenciaInicial" bigint NOT NULL,
        CONSTRAINT "PK_AncorasConciliacaoOvos" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_AncorasConciliacaoOvos_Revisao" CHECK ("Revisao">=0),
        CONSTRAINT "FK_AncorasConciliacaoOvos_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AncorasConciliacaoOvos_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE TABLE "ProvasEntradaOvos" (
        "Id" uuid NOT NULL,
        "MovimentoEstoqueId" uuid NOT NULL,
        "EventoId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Contagem" bigint NOT NULL,
        "Evidencia" character varying(2000) NOT NULL,
        "Motivo" character varying(2000) NOT NULL,
        "AutorId" uuid NOT NULL,
        "ResponsavelId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ProvasEntradaOvos" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_ProvasEntradaOvos_Id_MovimentoEstoqueId" UNIQUE ("Id", "MovimentoEstoqueId"),
        CONSTRAINT "CK_ProvasEntradaOvos_Contagem" CHECK ("Contagem" BETWEEN 1 AND 99999999999999 AND length(trim("Evidencia"))>0),
        CONSTRAINT "FK_ProvasEntradaOvos_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ProvasEntradaOvos_AspNetUsers_ResponsavelId" FOREIGN KEY ("ResponsavelId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ProvasEntradaOvos_EventosEstoque_EventoId" FOREIGN KEY ("EventoId") REFERENCES "EventosEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ProvasEntradaOvos_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ProvasEntradaOvos_MovimentosEstoque_MovimentoEstoqueId" FOREIGN KEY ("MovimentoEstoqueId") REFERENCES "MovimentosEstoque" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE TABLE "RecortesConciliacaoOvos" (
        "Id" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "RevisaoAncora" bigint NOT NULL,
        "SequenciaAte" bigint NOT NULL,
        "Fronteira" character varying(2000) NOT NULL,
        "FontesConsultadas" character varying(2000) NOT NULL,
        "Lacunas" character varying(2000) NOT NULL,
        "Motivo" character varying(2000) NOT NULL,
        "AutorId" uuid NOT NULL,
        "ResponsavelId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "OrdemAprovacao" bigint NOT NULL DEFAULT 0,
        CONSTRAINT "PK_RecortesConciliacaoOvos" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_RecortesConciliacaoOvos_Ordem" CHECK ("OrdemAprovacao">0),
        CONSTRAINT "CK_RecortesConciliacaoOvos_Conferencia" CHECK ("RevisaoAncora">=0 AND "SequenciaAte">=0 AND length(trim("Fronteira"))>0 AND length(trim("FontesConsultadas"))>0 AND length(trim("Motivo"))>0),
        CONSTRAINT "FK_RecortesConciliacaoOvos_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RecortesConciliacaoOvos_AspNetUsers_ResponsavelId" FOREIGN KEY ("ResponsavelId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RecortesConciliacaoOvos_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE TABLE "DecisoesConciliacaoOvos" (
        "Id" uuid NOT NULL,
        "RecorteId" uuid NOT NULL,
        "ProducaoOvoId" uuid NOT NULL,
        "Decisao" character varying(40) NOT NULL,
        "IndividualComprovado" boolean NOT NULL,
        "Evidencia" character varying(2000) NOT NULL,
        "HashOrigem" character varying(64) NOT NULL,
        "OrigemUpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_DecisoesConciliacaoOvos" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_DecisoesConciliacaoOvos_Decisao" CHECK ("Decisao" IN ('NaoRecebidoComprovado','RecebidoComEquivalencia','Pendente') AND ("Decisao"='Pendente' OR "IndividualComprovado")),
        CONSTRAINT "CK_DecisoesConciliacaoOvos_Hash" CHECK (length("HashOrigem")=64),
        CONSTRAINT "FK_DecisoesConciliacaoOvos_ProducoesOvos_ProducaoOvoId" FOREIGN KEY ("ProducaoOvoId") REFERENCES "ProducoesOvos" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DecisoesConciliacaoOvos_RecortesConciliacaoOvos_RecorteId" FOREIGN KEY ("RecorteId") REFERENCES "RecortesConciliacaoOvos" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE TABLE "GruposPonteOvos" (
        "Id" uuid NOT NULL,
        "Operacao" character varying(20) NOT NULL,
        "ItemId" uuid NOT NULL,
        "LoteId" uuid NOT NULL,
        "LocalId" uuid NOT NULL,
        "RecorteId" uuid NOT NULL,
        "ProvaId" uuid,
        "EventoId" uuid NOT NULL,
        "MovimentoEstoqueId" uuid NOT NULL,
        "Quantidade" bigint NOT NULL,
        "RevisaoLoteOriginal" integer NOT NULL,
        "SituacaoOriginal" character varying(20) NOT NULL,
        "ValidadeOriginal" date,
        "SnapshotLoteJson" jsonb NOT NULL,
        "AutorId" uuid NOT NULL,
        "ResponsavelId" uuid NOT NULL,
        "Motivo" character varying(2000) NOT NULL,
        "Evidencia" character varying(2000) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_GruposPonteOvos" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_GruposPonteOvos_Operacao" CHECK (("Operacao"='Entrada' AND "ProvaId" IS NULL) OR ("Operacao"='Associacao' AND "ProvaId" IS NOT NULL)),
        CONSTRAINT "CK_GruposPonteOvos_Quantidade" CHECK ("Quantidade" BETWEEN 1 AND 99999999999999),
        CONSTRAINT "FK_GruposPonteOvos_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GruposPonteOvos_AspNetUsers_ResponsavelId" FOREIGN KEY ("ResponsavelId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GruposPonteOvos_EventosEstoque_EventoId" FOREIGN KEY ("EventoId") REFERENCES "EventosEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GruposPonteOvos_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GruposPonteOvos_LocaisEstoque_LocalId" FOREIGN KEY ("LocalId") REFERENCES "LocaisEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GruposPonteOvos_LotesMateriais_LoteId" FOREIGN KEY ("LoteId") REFERENCES "LotesMateriais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GruposPonteOvos_MovimentosEstoque_MovimentoEstoqueId" FOREIGN KEY ("MovimentoEstoqueId") REFERENCES "MovimentosEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GruposPonteOvos_ProvasEntradaOvos_ProvaId" FOREIGN KEY ("ProvaId") REFERENCES "ProvasEntradaOvos" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GruposPonteOvos_RecortesConciliacaoOvos_RecorteId" FOREIGN KEY ("RecorteId") REFERENCES "RecortesConciliacaoOvos" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE TABLE "OrigensRecebimentoOvos" (
        "Id" uuid NOT NULL,
        "EventoId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "RecorteId" uuid NOT NULL,
        "Evidencia" character varying(2000) NOT NULL,
        "AutorId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_OrigensRecebimentoOvos" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_OrigensRecebimentoOvos_Evidencia" CHECK (length(trim("Evidencia"))>0),
        CONSTRAINT "FK_OrigensRecebimentoOvos_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_OrigensRecebimentoOvos_EventosEstoque_EventoId" FOREIGN KEY ("EventoId") REFERENCES "EventosEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_OrigensRecebimentoOvos_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_OrigensRecebimentoOvos_RecortesConciliacaoOvos_RecorteId" FOREIGN KEY ("RecorteId") REFERENCES "RecortesConciliacaoOvos" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE TABLE "ComandosPonteOvos" (
        "Id" uuid NOT NULL,
        "Operacao" character varying(50) NOT NULL,
        "Chave" character varying(100) NOT NULL,
        "HashPayload" character varying(64) NOT NULL,
        "AutorId" uuid NOT NULL,
        "GrupoId" uuid,
        "ExigeAdmin" boolean NOT NULL,
        "StatusHttp" integer NOT NULL,
        "Location" character varying(300),
        "RespostaJson" text NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ComandosPonteOvos" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_ComandosPonteOvos_Chave" CHECK (length("Chave") BETWEEN 1 AND 100 AND length("HashPayload")=64 AND "StatusHttp" IN (200,201)),
        CONSTRAINT "FK_ComandosPonteOvos_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ComandosPonteOvos_GruposPonteOvos_GrupoId" FOREIGN KEY ("GrupoId") REFERENCES "GruposPonteOvos" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE TABLE "OrigensLoteOvos" (
        "Id" uuid NOT NULL,
        "LoteId" uuid NOT NULL,
        "GrupoId" uuid NOT NULL,
        CONSTRAINT "PK_OrigensLoteOvos" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_OrigensLoteOvos_GruposPonteOvos_GrupoId" FOREIGN KEY ("GrupoId") REFERENCES "GruposPonteOvos" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_OrigensLoteOvos_LotesMateriais_LoteId" FOREIGN KEY ("LoteId") REFERENCES "LotesMateriais" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE TABLE "PontesProducaoOvos" (
        "Id" uuid NOT NULL,
        "ProducaoOvoId" uuid NOT NULL,
        "GrupoId" uuid NOT NULL,
        "SnapshotJson" text NOT NULL,
        "HashOrigem" character varying(64) NOT NULL,
        "OrigemUpdatedAtUtc" timestamp with time zone NOT NULL,
        "Algoritmo" character varying(50) NOT NULL,
        CONSTRAINT "PK_PontesProducaoOvos" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_PontesProducaoOvos_Snapshot" CHECK (length("HashOrigem")=64 AND "Algoritmo"='gensw-egg-bridge/1.0'),
        CONSTRAINT "FK_PontesProducaoOvos_GruposPonteOvos_GrupoId" FOREIGN KEY ("GrupoId") REFERENCES "GruposPonteOvos" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PontesProducaoOvos_ProducoesOvos_ProducaoOvoId" FOREIGN KEY ("ProducaoOvoId") REFERENCES "ProducoesOvos" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE TABLE "TratamentosPonteOvos" (
        "Id" uuid NOT NULL,
        "GrupoId" uuid NOT NULL,
        "Tipo" character varying(20) NOT NULL,
        "EventoId" uuid,
        "AutorId" uuid NOT NULL,
        "ResponsavelId" uuid NOT NULL,
        "Motivo" character varying(2000) NOT NULL,
        "Evidencia" character varying(2000) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_TratamentosPonteOvos" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_TratamentosPonteOvos_Tipo" CHECK ("Tipo" IN ('Anotacao','Invalidacao','Compensacao') AND (("Tipo"='Compensacao')=("EventoId" IS NOT NULL)) AND length(trim("Evidencia"))>0),
        CONSTRAINT "FK_TratamentosPonteOvos_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_TratamentosPonteOvos_AspNetUsers_ResponsavelId" FOREIGN KEY ("ResponsavelId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_TratamentosPonteOvos_EventosEstoque_EventoId" FOREIGN KEY ("EventoId") REFERENCES "EventosEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_TratamentosPonteOvos_GruposPonteOvos_GrupoId" FOREIGN KEY ("GrupoId") REFERENCES "GruposPonteOvos" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE TABLE "AlocacoesProvaOvos" (
        "Id" uuid NOT NULL,
        "PonteId" uuid NOT NULL,
        "ProvaId" uuid NOT NULL,
        "MovimentoEstoqueId" uuid NOT NULL,
        "Ordinal" bigint NOT NULL,
        CONSTRAINT "PK_AlocacoesProvaOvos" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_AlocacoesProvaOvos_Ordinal" CHECK ("Ordinal" BETWEEN 1 AND 99999999999999),
        CONSTRAINT "FK_AlocacoesProvaOvos_PontesProducaoOvos_PonteId" FOREIGN KEY ("PonteId") REFERENCES "PontesProducaoOvos" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_AlocacoesProvaOvos_ProvasEntradaOvos_ProvaId_MovimentoEstoq~" FOREIGN KEY ("ProvaId", "MovimentoEstoqueId") REFERENCES "ProvasEntradaOvos" ("Id", "MovimentoEstoqueId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_AlocacoesProvaOvos_MovimentoEstoqueId_Ordinal" ON "AlocacoesProvaOvos" ("MovimentoEstoqueId", "Ordinal");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_AlocacoesProvaOvos_PonteId" ON "AlocacoesProvaOvos" ("PonteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_AlocacoesProvaOvos_ProvaId_MovimentoEstoqueId" ON "AlocacoesProvaOvos" ("ProvaId", "MovimentoEstoqueId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_AncorasConciliacaoOvos_AutorId" ON "AncorasConciliacaoOvos" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_AncorasConciliacaoOvos_ItemId" ON "AncorasConciliacaoOvos" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_ComandosPonteOvos_AutorId" ON "ComandosPonteOvos" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_ComandosPonteOvos_GrupoId" ON "ComandosPonteOvos" ("GrupoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_ComandosPonteOvos_Operacao_Chave" ON "ComandosPonteOvos" ("Operacao", "Chave");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_DecisoesConciliacaoOvos_ProducaoOvoId" ON "DecisoesConciliacaoOvos" ("ProducaoOvoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_DecisoesConciliacaoOvos_RecorteId_ProducaoOvoId" ON "DecisoesConciliacaoOvos" ("RecorteId", "ProducaoOvoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_GruposPonteOvos_AutorId" ON "GruposPonteOvos" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_GruposPonteOvos_EventoId" ON "GruposPonteOvos" ("EventoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_GruposPonteOvos_ItemId_CreatedAtUtc_Id" ON "GruposPonteOvos" ("ItemId", "CreatedAtUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_GruposPonteOvos_LocalId" ON "GruposPonteOvos" ("LocalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_GruposPonteOvos_LoteId" ON "GruposPonteOvos" ("LoteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_GruposPonteOvos_MovimentoEstoqueId" ON "GruposPonteOvos" ("MovimentoEstoqueId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_GruposPonteOvos_ProvaId" ON "GruposPonteOvos" ("ProvaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_GruposPonteOvos_RecorteId" ON "GruposPonteOvos" ("RecorteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_GruposPonteOvos_ResponsavelId" ON "GruposPonteOvos" ("ResponsavelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_OrigensLoteOvos_GrupoId" ON "OrigensLoteOvos" ("GrupoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_OrigensLoteOvos_LoteId" ON "OrigensLoteOvos" ("LoteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_OrigensRecebimentoOvos_AutorId" ON "OrigensRecebimentoOvos" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_OrigensRecebimentoOvos_EventoId" ON "OrigensRecebimentoOvos" ("EventoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_OrigensRecebimentoOvos_ItemId" ON "OrigensRecebimentoOvos" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_OrigensRecebimentoOvos_RecorteId" ON "OrigensRecebimentoOvos" ("RecorteId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_PontesProducaoOvos_GrupoId" ON "PontesProducaoOvos" ("GrupoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_PontesProducaoOvos_ProducaoOvoId" ON "PontesProducaoOvos" ("ProducaoOvoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_ProvasEntradaOvos_AutorId" ON "ProvasEntradaOvos" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_ProvasEntradaOvos_EventoId" ON "ProvasEntradaOvos" ("EventoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_ProvasEntradaOvos_ItemId" ON "ProvasEntradaOvos" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_ProvasEntradaOvos_MovimentoEstoqueId" ON "ProvasEntradaOvos" ("MovimentoEstoqueId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_ProvasEntradaOvos_ResponsavelId" ON "ProvasEntradaOvos" ("ResponsavelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_RecortesConciliacaoOvos_AutorId" ON "RecortesConciliacaoOvos" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_RecortesConciliacaoOvos_ItemId_CreatedAtUtc_Id" ON "RecortesConciliacaoOvos" ("ItemId", "CreatedAtUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_RecortesConciliacaoOvos_ResponsavelId" ON "RecortesConciliacaoOvos" ("ResponsavelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_TratamentosPonteOvos_AutorId" ON "TratamentosPonteOvos" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_TratamentosPonteOvos_EventoId" ON "TratamentosPonteOvos" ("EventoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_TratamentosPonteOvos_GrupoId_Tipo" ON "TratamentosPonteOvos" ("GrupoId", "Tipo") WHERE "Tipo" IN ('Invalidacao','Compensacao');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE INDEX "IX_TratamentosPonteOvos_ResponsavelId" ON "TratamentosPonteOvos" ("ResponsavelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE SEQUENCE "SequenciaConciliacaoOvos" START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE UNIQUE INDEX "IX_RecortesConciliacaoOvos_OrdemAprovacao" ON "RecortesConciliacaoOvos" ("OrdemAprovacao");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    CREATE OR REPLACE FUNCTION "InventoryMovementGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE q jsonb; acceptance jsonb; calculated numeric; normalized numeric; residue numeric; scaled numeric; integer_part numeric; even_value numeric; original_row "MovimentosEstoque"%ROWTYPE;
    BEGIN
      IF NOT EXISTS(SELECT 1 FROM "LotesMateriais" WHERE "Id"=NEW."LoteId" AND "ItemId"=NEW."ItemId" AND "Unidade"=NEW."Unidade") THEN RAISE EXCEPTION 'Movement lot identity mismatch' USING ERRCODE='23514'; END IF;
      IF NOT EXISTS(SELECT 1 FROM "EventosEstoque" WHERE "Id"=NEW."EventoId" AND "Tipo"=NEW."TipoEvento") THEN RAISE EXCEPTION 'Movement event type mismatch' USING ERRCODE='23514'; END IF;
      IF NEW."ConversaoItemId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "ConversoesItens" WHERE "Id"=NEW."ConversaoItemId" AND "ItemId"=NEW."ItemId") THEN RAISE EXCEPTION 'Movement conversion item mismatch' USING ERRCODE='23514'; END IF;
      IF NEW."PerfilNutricionalId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM "PerfisNutricionais" WHERE "Id"=NEW."PerfilNutricionalId" AND "ItemId"=NEW."ItemId") THEN RAISE EXCEPTION 'Movement profile item mismatch' USING ERRCODE='23514'; END IF;
      IF NEW."TipoEvento" IN ('Entrada','Abertura') AND EXISTS(SELECT 1 FROM "SaidasProducao" WHERE "LoteId"=NEW."LoteId") THEN
      RAISE EXCEPTION 'Produced lots cannot receive another receipt' USING ERRCODE='23514';
    END IF;
    IF NEW."TipoEvento"='Ajuste' AND EXISTS(SELECT 1 FROM "SaidasProducao" WHERE "LoteId"=NEW."LoteId") AND NOT EXISTS(
      SELECT 1 FROM "ExecucoesProducao" WHERE "Id"::text=NEW."SnapshotJson" #>> '{pedido,execucaoReferenciaId}') THEN
      RAISE EXCEPTION 'A production correction requires an execution reference' USING ERRCODE='23514';
    END IF;
    IF NEW."TipoEvento" IN ('ReversaoProducaoSaida','ReversaoProducaoConsumo') THEN
      SELECT m.* INTO original_row FROM "MovimentosEstoque" m
      JOIN "ExecucaoMovimentos" em ON em."MovimentoEstoqueId"=m."Id"
      WHERE m."Id"::text=NEW."SnapshotJson" #>> '{compensacao,movimentoOriginalId}'
        AND em."ExecucaoId"::text=NEW."SnapshotJson" #>> '{producao,execucaoId}'
        AND em."Papel" IN ('Entrada','Saida');
      IF NOT FOUND OR original_row."LoteId"<>NEW."LoteId" OR original_row."LocalId"<>NEW."LocalId"
         OR original_row."ItemId"<>NEW."ItemId" OR original_row."Unidade"<>NEW."Unidade"
         OR original_row."Quantidade"<>NEW."Quantidade" OR original_row."Sentido"=NEW."Sentido"
         OR NEW."ConversaoItemId" IS DISTINCT FROM original_row."ConversaoItemId"
         OR NEW."PerfilNutricionalId" IS DISTINCT FROM original_row."PerfilNutricionalId"
         OR NEW."QuantidadeDeclarada"::numeric<>original_row."Quantidade"
         OR NEW."UnidadeDeclarada"<>NEW."Unidade" OR NEW."Residuo"::numeric<>0
         OR (NEW."TipoEvento"='ReversaoProducaoSaida' AND original_row."TipoEvento"<>'ProducaoSaida')
         OR (NEW."TipoEvento"='ReversaoProducaoConsumo' AND original_row."TipoEvento"<>'ProducaoConsumo')
         OR NOT EXISTS(SELECT 1 FROM "EventosEstoque" WHERE "Id"=NEW."EventoId" AND "EventoReferenciaId"=original_row."EventoId") THEN
        RAISE EXCEPTION 'Compensation must exactly invert the original normalized fact' USING ERRCODE='23514';
      END IF;
      RETURN NEW;
    END IF;
    IF NEW."TipoEvento"='CompensacaoEntradaOvos' THEN
      SELECT m.* INTO original_row FROM "MovimentosEstoque" m JOIN "GruposPonteOvos" g ON g."MovimentoEstoqueId"=m."Id"
        WHERE g."Id"::text=NEW."SnapshotJson" #>> '{f03,grupoId}' AND g."Operacao"='Entrada'
          AND m."Id"::text=NEW."SnapshotJson" #>> '{compensacao,movimentoOriginalId}';
      IF NOT FOUND OR NEW."LoteId"<>original_row."LoteId" OR NEW."LocalId"<>original_row."LocalId"
        OR NEW."ItemId"<>original_row."ItemId" OR NEW."Unidade"<>'un' OR NEW."Sentido"<>'Saida'
        OR NEW."Quantidade"<>original_row."Quantidade" OR NEW."QuantidadeDeclarada"::numeric<>original_row."Quantidade"
        OR NEW."UnidadeDeclarada"<>'un' OR NEW."Residuo"::numeric<>0
        OR NEW."ConversaoItemId" IS DISTINCT FROM original_row."ConversaoItemId"
        OR NEW."PerfilNutricionalId" IS DISTINCT FROM original_row."PerfilNutricionalId"
        OR NOT EXISTS(SELECT 1 FROM "EventosEstoque" e WHERE e."Id"=NEW."EventoId" AND e."EventoReferenciaId"=original_row."EventoId") THEN
        RAISE EXCEPTION 'Egg compensation must exactly invert its original receipt' USING ERRCODE='23514';
      END IF;
      q:=NEW."SnapshotJson" #> '{previa,quantidade}';
      IF q IS NULL OR q->>'declarada' IS NULL OR q->>'unidadeDeclarada' IS NULL OR q->>'calculada' IS NULL
         OR q->>'normalizada' IS NULL OR q->>'residuo' IS NULL OR q->>'unidade' IS NULL OR q->>'exigeAceite' IS NULL
         OR q->>'declarada' IS DISTINCT FROM NEW."QuantidadeDeclarada"
         OR q->>'unidadeDeclarada' IS DISTINCT FROM 'un' OR q->>'unidade' IS DISTINCT FROM 'un'
         OR (q->>'calculada')::numeric IS DISTINCT FROM NEW."Quantidade"
         OR (q->>'normalizada')::numeric IS DISTINCT FROM NEW."Quantidade"
         OR (q->>'residuo')::numeric IS DISTINCT FROM 0 OR (q->>'exigeAceite')::boolean IS DISTINCT FROM false THEN
        RAISE EXCEPTION 'Egg compensation quantity snapshot must match its exact physical debit' USING ERRCODE='23514';
      END IF;
      RETURN NEW;
    END IF;
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
    CREATE OR REPLACE FUNCTION "InventoryEventGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE event_id uuid; event_type text; n integer; credits integer; debits integer; lots integer; places integer; net numeric;
    BEGIN
      IF TG_TABLE_NAME='EventosEstoque' THEN event_id:=NEW."Id"; ELSE event_id:=NEW."EventoId"; END IF;
      SELECT "Tipo" INTO event_type FROM "EventosEstoque" WHERE "Id"=event_id;
      IF event_type NOT IN ('Abertura','Entrada','Transferencia','SaidaManual','ConsumoInterno','Ajuste','Segregacao','RetornoSegregacao','Descarte','ProducaoConsumo','ProducaoSaida','ReversaoProducaoSaida','ReversaoProducaoConsumo','CompensacaoEntradaOvos') THEN RAISE EXCEPTION 'Unknown stock event type' USING ERRCODE='23514'; END IF;
      SELECT count(*),count(*) FILTER(WHERE "Sentido"='Entrada'),count(*) FILTER(WHERE "Sentido"='Saida'),count(DISTINCT "LoteId"),count(DISTINCT "LocalId"),sum(CASE WHEN "Sentido"='Entrada' THEN "Quantidade" ELSE -"Quantidade" END) INTO n,credits,debits,lots,places,net FROM "MovimentosEstoque" WHERE "EventoId"=event_id;
      IF event_type='CompensacaoEntradaOvos' THEN
      IF n<>1 OR debits<>1 OR NOT EXISTS(SELECT 1 FROM "TratamentosPonteOvos" t JOIN "GruposPonteOvos" g ON g."Id"=t."GrupoId"
        JOIN "MovimentosEstoque" m ON m."EventoId"=event_id WHERE t."EventoId"=event_id AND t."Tipo"='Compensacao'
          AND g."Id"::text=m."SnapshotJson" #>> '{f03,grupoId}') THEN
        RAISE EXCEPTION 'Egg compensation requires the exact treatment' USING ERRCODE='23514';
      END IF;
    ELSIF event_type IN ('ProducaoConsumo','ProducaoSaida','ReversaoProducaoSaida','ReversaoProducaoConsumo') THEN
      IF n<>1 OR (event_type IN ('ProducaoConsumo','ReversaoProducaoSaida') AND debits<>1)
         OR (event_type IN ('ProducaoSaida','ReversaoProducaoConsumo') AND credits<>1)
         OR NOT EXISTS(SELECT 1 FROM "ExecucaoMovimentos" em JOIN "MovimentosEstoque" m ON m."Id"=em."MovimentoEstoqueId"
           JOIN "ExecucoesProducao" ex ON ex."Id"=em."ExecucaoId"
           WHERE m."EventoId"=event_id AND em."ExecucaoId"::text=m."SnapshotJson" #>> '{producao,execucaoId}'
           AND em."LinhaId"::text=m."SnapshotJson" #>> '{producao,linhaId}'
           AND em."Papel"=CASE WHEN event_type='ProducaoConsumo' THEN 'Entrada' WHEN event_type='ProducaoSaida' THEN 'Saida' ELSE 'Compensacao' END) THEN
        RAISE EXCEPTION 'Production event requires its execution and exact movement' USING ERRCODE='23514';
      END IF;
    ELSIF event_type IN ('Transferencia','Segregacao','RetornoSegregacao') THEN
        IF n<>2 OR credits<>1 OR debits<>1 OR lots<>1 OR places<>2 OR net<>0 THEN RAISE EXCEPTION 'Transfer requires equal debit and credit' USING ERRCODE='23514'; END IF;
      ELSIF n<>1 OR (event_type IN ('Entrada','Abertura') AND credits<>1) OR (event_type IN ('SaidaManual','ConsumoInterno','Descarte') AND debits<>1) THEN RAISE EXCEPTION 'Invalid stock event cardinality' USING ERRCODE='23514'; END IF;
      RETURN NULL;
    END $$;
    -- Versioned field tuple, independent of JSON whitespace, property order or escaping.
    CREATE FUNCTION "EggCanonicalSourceHash"(origin_row "ProducoesOvos") RETURNS text LANGUAGE sql IMMUTABLE AS $$
      SELECT upper(encode(sha256(convert_to(concat_ws(E'\n',
        'gensw-egg-bridge/1.0', (origin_row)."Id"::text, (origin_row)."AnimalId"::text,
        to_char((origin_row)."DataPostura",'YYYY-MM-DD'), ((origin_row)."PesoGramas"::numeric(10,2))::text,
        to_char((origin_row)."UpdatedAtUtc" AT TIME ZONE 'UTC','YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),
        CASE WHEN (origin_row)."Observacao" IS NULL THEN 'N'
          ELSE 'S'||replace(replace(encode(convert_to((origin_row)."Observacao",'UTF8'),'base64'),chr(10),''),chr(13),'') END),
        'UTF8')),'hex'));
    $$;

    CREATE FUNCTION "EggAnchorGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
      IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Egg management enrollment is permanent' USING ERRCODE='23514'; END IF;
      IF TG_OP='INSERT' THEN
        -- Enrollment and all receipt writers share a physical item tuple version, even before an anchor exists.
        UPDATE "Itens" SET "Revisao"="Revisao" WHERE "Id"=NEW."ItemId";
        IF NOT EXISTS(SELECT 1 FROM "Itens" WHERE "Id"=NEW."ItemId" AND "Unidade"='un') THEN RAISE EXCEPTION 'Egg management requires units' USING ERRCODE='23514'; END IF;
        NEW."Revisao":=0;
        SELECT coalesce(max("Sequencia"),0) INTO NEW."SequenciaInicial" FROM "EventosEstoque";
      ELSIF NEW."Id"<>OLD."Id" OR NEW."ItemId"<>OLD."ItemId" OR NEW."AutorId"<>OLD."AutorId" OR NEW."CreatedAtUtc"<>OLD."CreatedAtUtc"
          OR NEW."SequenciaInicial"<>OLD."SequenciaInicial" OR (NEW."Revisao"<>OLD."Revisao" AND (pg_trigger_depth()<2 OR NEW."Revisao"<>OLD."Revisao"+1)) THEN
        RAISE EXCEPTION 'Egg anchor identity and revision protocol are immutable' USING ERRCODE='23514';
      END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_AncorasConciliacaoOvos_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "AncorasConciliacaoOvos" FOR EACH ROW EXECUTE FUNCTION "EggAnchorGuard"();

    CREATE FUNCTION "EggManagedItemGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
      IF NEW."Unidade"<>'un' AND EXISTS(SELECT 1 FROM "AncorasConciliacaoOvos" WHERE "ItemId"=NEW."Id") THEN
        RAISE EXCEPTION 'An explicitly managed egg item retains canonical unit un' USING ERRCODE='23514';
      END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_Itens_EggUnit" BEFORE UPDATE ON "Itens" FOR EACH ROW EXECUTE FUNCTION "EggManagedItemGuard"();

    CREATE FUNCTION "EggCreditGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
      IF NEW."Sentido"='Entrada' AND NEW."TipoEvento" IN ('Entrada','Abertura','Ajuste','ProducaoSaida','ReversaoProducaoConsumo') THEN
        -- A tuple version on Item closes enrollment races for SQL writers with an older RR snapshot.
        UPDATE "Itens" SET "Revisao"="Revisao" WHERE "Id"=NEW."ItemId";
        -- A real tuple write serializes all lots of this item, including SQL writers in RC/RR.
        UPDATE "AncorasConciliacaoOvos" SET "Revisao"="Revisao"+1 WHERE "ItemId"=NEW."ItemId";
      END IF;
      IF EXISTS(SELECT 1 FROM "OrigensLoteOvos" WHERE "LoteId"=NEW."LoteId") AND NEW."TipoEvento" IN ('Entrada','Abertura') THEN
        RAISE EXCEPTION 'An egg bridge lot cannot receive another receipt' USING ERRCODE='23514';
      END IF;
      IF NEW."TipoEvento"='Ajuste' AND (EXISTS(SELECT 1 FROM "GruposPonteOvos" WHERE "LoteId"=NEW."LoteId") OR NEW."SnapshotJson" #>> '{pedido,grupoPonteOvosReferenciaId}' IS NOT NULL) AND NOT EXISTS(
        SELECT 1 FROM "GruposPonteOvos" WHERE "LoteId"=NEW."LoteId" AND "ItemId"=NEW."ItemId" AND "Id"::text=NEW."SnapshotJson" #>> '{pedido,grupoPonteOvosReferenciaId}') THEN
        RAISE EXCEPTION 'A present egg adjustment requires its original group reference' USING ERRCODE='23514';
      END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_MovimentosEstoque_EggCredit" BEFORE INSERT ON "MovimentosEstoque" FOR EACH ROW EXECUTE FUNCTION "EggCreditGuard"();

    CREATE FUNCTION "EggCreditConsistency"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE anchor_row "AncorasConciliacaoOvos"%ROWTYPE; linked "OrigensRecebimentoOvos"%ROWTYPE; scope_row "RecortesConciliacaoOvos"%ROWTYPE; origin_kind text;
    BEGIN
      IF NEW."Sentido"<>'Entrada' OR NEW."TipoEvento" NOT IN ('Entrada','Abertura','Ajuste') THEN RETURN NULL; END IF;
      SELECT * INTO anchor_row FROM "AncorasConciliacaoOvos" WHERE "ItemId"=NEW."ItemId";
      IF NOT FOUND THEN RETURN NULL; END IF;
      IF EXISTS(SELECT 1 FROM "GruposPonteOvos" g JOIN "OrigensLoteOvos" o ON o."GrupoId"=g."Id" AND o."LoteId"=g."LoteId"
        WHERE g."MovimentoEstoqueId"=NEW."Id" AND g."Operacao"='Entrada' AND g."Id"::text=NEW."SnapshotJson" #>> '{f03,grupoId}') THEN RETURN NULL; END IF;
      -- The dedicated-lot marker may be inserted after this movement in the same transaction.
      -- Recheck at commit, when the complete origin is visible, while allowing the exact F03 root above.
      IF NEW."TipoEvento" IN ('Entrada','Abertura') AND EXISTS(SELECT 1 FROM "OrigensLoteOvos" WHERE "LoteId"=NEW."LoteId") THEN
        RAISE EXCEPTION 'An egg bridge lot cannot receive another receipt' USING ERRCODE='23514';
      END IF;
      SELECT * INTO linked FROM "OrigensRecebimentoOvos" WHERE "EventoId"=NEW."EventoId" AND "ItemId"=NEW."ItemId";
      IF NOT FOUND THEN RAISE EXCEPTION 'Managed egg receipt requires proven structured origin' USING ERRCODE='23514'; END IF;
      SELECT * INTO scope_row FROM "RecortesConciliacaoOvos" WHERE "Id"=linked."RecorteId";
      origin_kind:=NEW."SnapshotJson" #>> '{pedido,origemOvosComprovada}';
      IF scope_row."ItemId"<>NEW."ItemId" OR scope_row."RevisaoAncora"<>anchor_row."Revisao"-1
        OR NEW."SnapshotJson" #>> '{pedido,conciliacaoOvosId}' IS DISTINCT FROM linked."RecorteId"::text
        OR NEW."SnapshotJson" #>> '{pedido,evidencia}' IS DISTINCT FROM linked."Evidencia"
        OR (CASE WHEN NEW."SnapshotJson" #>> '{pedido,grupoPonteOvosReferenciaId}' IS NOT NULL THEN origin_kind IS DISTINCT FROM 'AjustePresenteVinculado' OR NEW."TipoEvento"<>'Ajuste' ELSE origin_kind IS DISTINCT FROM 'ColetivaDistinta' END)
        OR (origin_kind='AjustePresenteVinculado' AND NOT EXISTS(
          SELECT 1 FROM "GruposPonteOvos" WHERE "LoteId"=NEW."LoteId" AND "ItemId"=NEW."ItemId" AND "Id"::text=NEW."SnapshotJson" #>> '{pedido,grupoPonteOvosReferenciaId}')) THEN
        RAISE EXCEPTION 'Managed egg receipt conference or distinct origin is invalid' USING ERRCODE='23514';
      END IF;
      RETURN NULL;
    END $$;
    CREATE CONSTRAINT TRIGGER "TR_MovimentosEstoque_EggOrigin" AFTER INSERT ON "MovimentosEstoque" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "EggCreditConsistency"();

    CREATE FUNCTION "EggAnchorConsistency"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE revision bigint; initial_cut bigint; credits bigint;
    BEGIN
      SELECT "Revisao","SequenciaInicial" INTO revision,initial_cut FROM "AncorasConciliacaoOvos" WHERE "ItemId"=NEW."ItemId";
      SELECT count(*) INTO credits FROM "MovimentosEstoque" m JOIN "EventosEstoque" e ON e."Id"=m."EventoId"
        WHERE m."ItemId"=NEW."ItemId" AND e."Sequencia">initial_cut AND m."Sentido"='Entrada' AND m."TipoEvento" IN ('Entrada','Abertura','Ajuste','ProducaoSaida','ReversaoProducaoConsumo');
      IF revision<>credits THEN RAISE EXCEPTION 'Egg anchor revision differs from immutable credit facts' USING ERRCODE='23514'; END IF;
      RETURN NULL;
    END $$;
    CREATE CONSTRAINT TRIGGER "TR_AncorasConciliacaoOvos_Consistency" AFTER INSERT OR UPDATE ON "AncorasConciliacaoOvos" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "EggAnchorConsistency"();

    CREATE FUNCTION "EggScopeGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE current_revision bigint;
    BEGIN
      UPDATE "Itens" SET "Revisao"="Revisao" WHERE "Id"=NEW."ItemId";
      UPDATE "AncorasConciliacaoOvos" SET "Revisao"="Revisao" WHERE "ItemId"=NEW."ItemId" RETURNING "Revisao" INTO current_revision;
      IF NOT FOUND OR current_revision<>NEW."RevisaoAncora" THEN RAISE EXCEPTION 'Conference must identify the current item revision' USING ERRCODE='23514'; END IF;
      NEW."OrdemAprovacao":=nextval('"SequenciaConciliacaoOvos"');
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_RecortesConciliacaoOvos_Guard" BEFORE INSERT ON "RecortesConciliacaoOvos" FOR EACH ROW EXECUTE FUNCTION "EggScopeGuard"();

    CREATE FUNCTION "EggDecisionGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE approval_order bigint; observed_source "ProducoesOvos"%ROWTYPE;
    BEGIN
      -- The physical ID is global: decisions for two different target items must also serialize with bridge confirmation.
      -- This no-op tuple version changes neither zootecnical content nor its observed timestamp, but defeats stale RR snapshots.
      UPDATE "ProducoesOvos" SET "UpdatedAtUtc"="UpdatedAtUtc" WHERE "Id"=NEW."ProducaoOvoId" RETURNING * INTO observed_source;
      IF NOT FOUND OR NEW."HashOrigem" IS DISTINCT FROM "EggCanonicalSourceHash"(observed_source)
        OR NEW."OrigemUpdatedAtUtc" IS DISTINCT FROM observed_source."UpdatedAtUtc" THEN
        RAISE EXCEPTION 'An immutable egg decision requires the current canonical observed source' USING ERRCODE='23514';
      END IF;
      SELECT "OrdemAprovacao" INTO approval_order FROM "RecortesConciliacaoOvos" WHERE "Id"=NEW."RecorteId";
      IF EXISTS(SELECT 1 FROM "DecisoesConciliacaoOvos" d JOIN "RecortesConciliacaoOvos" r ON r."Id"=d."RecorteId"
        WHERE d."ProducaoOvoId"=NEW."ProducaoOvoId" AND r."OrdemAprovacao">approval_order) THEN
        RAISE EXCEPTION 'A newer global decision already superseded this physical origin' USING ERRCODE='23514';
      END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_DecisoesConciliacaoOvos_Guard" BEFORE INSERT ON "DecisoesConciliacaoOvos" FOR EACH ROW EXECUTE FUNCTION "EggDecisionGuard"();

    CREATE FUNCTION "EggProofGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE original_row "MovimentosEstoque"%ROWTYPE;
    BEGIN
      UPDATE "AncorasConciliacaoOvos" SET "Revisao"="Revisao" WHERE "ItemId"=NEW."ItemId";
      IF NOT FOUND THEN RAISE EXCEPTION 'Proof requires an explicitly managed item' USING ERRCODE='23514'; END IF;
      SELECT * INTO original_row FROM "MovimentosEstoque" WHERE "Id"=NEW."MovimentoEstoqueId" FOR SHARE;
      IF NOT FOUND OR original_row."EventoId"<>NEW."EventoId" OR original_row."ItemId"<>NEW."ItemId"
        OR original_row."TipoEvento" NOT IN ('Entrada','Abertura') OR original_row."Sentido"<>'Entrada'
        OR original_row."Unidade"<>'un' OR original_row."UnidadeDeclarada"<>'un'
        OR original_row."QuantidadeDeclarada"::numeric<>original_row."Quantidade" OR NEW."Contagem">original_row."Quantidade"
        OR original_row."SnapshotJson" #>> '{f03,grupoId}' IS NOT NULL
        OR EXISTS(SELECT 1 FROM "OrigensLoteOvos" WHERE "LoteId"=original_row."LoteId")
        OR EXISTS(SELECT 1 FROM "GruposPonteOvos" WHERE "MovimentoEstoqueId"=original_row."Id" AND "Operacao"='Entrada') THEN
        RAISE EXCEPTION 'Proof requires the original counted manual receipt, excluding all F03 roots' USING ERRCODE='23514';
      END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_ProvasEntradaOvos_Guard" BEFORE INSERT ON "ProvasEntradaOvos" FOR EACH ROW EXECUTE FUNCTION "EggProofGuard"();

    CREATE FUNCTION "EggGroupGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE current_revision bigint; scope_row "RecortesConciliacaoOvos"%ROWTYPE;
    BEGIN
      UPDATE "AncorasConciliacaoOvos" SET "Revisao"="Revisao" WHERE "ItemId"=NEW."ItemId" RETURNING "Revisao" INTO current_revision;
      IF NOT FOUND THEN RAISE EXCEPTION 'Egg group requires explicit managed item' USING ERRCODE='23514'; END IF;
      SELECT * INTO scope_row FROM "RecortesConciliacaoOvos" WHERE "Id"=NEW."RecorteId";
      IF scope_row."ItemId"<>NEW."ItemId" OR scope_row."RevisaoAncora"<>current_revision-(CASE WHEN NEW."Operacao"='Entrada' THEN 1 ELSE 0 END) THEN
        RAISE EXCEPTION 'Egg conference is stale at confirmation' USING ERRCODE='23514';
      END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_GruposPonteOvos_Guard" BEFORE INSERT ON "GruposPonteOvos" FOR EACH ROW EXECUTE FUNCTION "EggGroupGuard"();

    CREATE FUNCTION "EggSourceGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE source_row "ProducoesOvos"%ROWTYPE; decision_row "DecisoesConciliacaoOvos"%ROWTYPE; group_row "GruposPonteOvos"%ROWTYPE; j jsonb;
    BEGIN
      SELECT * INTO group_row FROM "GruposPonteOvos" WHERE "Id"=NEW."GrupoId";
      SELECT * INTO source_row FROM "ProducoesOvos" WHERE "Id"=NEW."ProducaoOvoId" FOR SHARE;
      SELECT * INTO decision_row FROM "DecisoesConciliacaoOvos" WHERE "RecorteId"=group_row."RecorteId" AND "ProducaoOvoId"=NEW."ProducaoOvoId";
      j:=NEW."SnapshotJson"::jsonb;
      IF NOT (j ?& ARRAY['producaoOvoId','animalId','dataPostura','pesoGramas','observacao','updatedAtUtc','algoritmo']) OR NEW."HashOrigem" IS DISTINCT FROM "EggCanonicalSourceHash"(source_row) OR NEW."Algoritmo"<>'gensw-egg-bridge/1.0' OR decision_row."Id" IS NULL OR NOT decision_row."IndividualComprovado" OR decision_row."Decisao"<>(CASE WHEN group_row."Operacao"='Entrada' THEN 'NaoRecebidoComprovado' ELSE 'RecebidoComEquivalencia' END)
         OR EXISTS(SELECT 1 FROM "DecisoesConciliacaoOvos" newer_decision JOIN "RecortesConciliacaoOvos" newer_scope ON newer_scope."Id"=newer_decision."RecorteId" JOIN "RecortesConciliacaoOvos" old_scope ON old_scope."Id"=group_row."RecorteId"
           WHERE newer_decision."ProducaoOvoId"=NEW."ProducaoOvoId" AND newer_scope."OrdemAprovacao">old_scope."OrdemAprovacao")
         OR decision_row."HashOrigem"<>NEW."HashOrigem" OR decision_row."OrigemUpdatedAtUtc"<>NEW."OrigemUpdatedAtUtc"
         OR source_row."UpdatedAtUtc"<>NEW."OrigemUpdatedAtUtc" OR j->>'producaoOvoId' IS DISTINCT FROM source_row."Id"::text
         OR j->>'animalId' IS DISTINCT FROM source_row."AnimalId"::text OR (j->>'dataPostura')::date IS DISTINCT FROM source_row."DataPostura"
         OR (j->>'pesoGramas')::numeric IS DISTINCT FROM source_row."PesoGramas" OR j->>'observacao' IS DISTINCT FROM source_row."Observacao"
         OR (j->>'updatedAtUtc')::timestamptz IS DISTINCT FROM source_row."UpdatedAtUtc" OR j->>'algoritmo' IS DISTINCT FROM NEW."Algoritmo" THEN
        RAISE EXCEPTION 'Egg source snapshot or individual evidence changed' USING ERRCODE='23514';
      END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_PontesProducaoOvos_Source" BEFORE INSERT ON "PontesProducaoOvos" FOR EACH ROW EXECUTE FUNCTION "EggSourceGuard"();

    CREATE FUNCTION "EggAllocationGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE proof_row "ProvasEntradaOvos"%ROWTYPE; group_row "GruposPonteOvos"%ROWTYPE;
    BEGIN
      SELECT * INTO proof_row FROM "ProvasEntradaOvos" WHERE "Id"=NEW."ProvaId";
      -- Serializes competing slot writers even when they do not take application advisory locks.
      UPDATE "AncorasConciliacaoOvos" SET "Revisao"="Revisao" WHERE "ItemId"=proof_row."ItemId";
      IF NOT FOUND THEN RAISE EXCEPTION 'Proof anchor missing' USING ERRCODE='23514'; END IF;
      SELECT g.* INTO group_row FROM "GruposPonteOvos" g JOIN "PontesProducaoOvos" p ON p."GrupoId"=g."Id" WHERE p."Id"=NEW."PonteId";
      IF NEW."Ordinal">proof_row."Contagem" OR NEW."MovimentoEstoqueId"<>proof_row."MovimentoEstoqueId" OR group_row."ProvaId" IS DISTINCT FROM proof_row."Id"
        OR group_row."ItemId"<>proof_row."ItemId" OR group_row."MovimentoEstoqueId"<>proof_row."MovimentoEstoqueId" THEN
        RAISE EXCEPTION 'Egg allocation exceeds its immutable historical capacity' USING ERRCODE='23514';
      END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_AlocacoesProvaOvos_Guard" BEFORE INSERT ON "AlocacoesProvaOvos" FOR EACH ROW EXECUTE FUNCTION "EggAllocationGuard"();

    CREATE FUNCTION "EggLotSnapshotMatches"(snapshot jsonb, lot_id uuid, terminal boolean) RETURNS boolean LANGUAGE sql STABLE AS $$
      SELECT snapshot ?& ARRAY['id','itemId','codigo','codigoNormalizado','codigoExterno','origem','fonte','responsavelId','dataOrigem','fabricacao','coleta','validade','fonteValidade','responsavelValidadeId','situacao','ativo','unidade','perfilNutricionalId','conversaoItemId','aplicabilidade','revisao','createdAtUtc','updatedAtUtc']
        AND (snapshot->>'id')::uuid IS NOT DISTINCT FROM l."Id"
        AND (snapshot->>'itemId')::uuid IS NOT DISTINCT FROM l."ItemId"
        AND snapshot->>'codigo' IS NOT DISTINCT FROM l."Codigo"
        AND snapshot->>'codigoNormalizado' IS NOT DISTINCT FROM l."CodigoNormalizado"
        AND snapshot->>'codigoExterno' IS NOT DISTINCT FROM l."CodigoExterno"
        AND snapshot->>'origem' IS NOT DISTINCT FROM l."Origem"
        AND snapshot->>'fonte' IS NOT DISTINCT FROM l."Fonte"
        AND (snapshot->>'responsavelId')::uuid IS NOT DISTINCT FROM l."ResponsavelId"
        AND (snapshot->>'dataOrigem')::date IS NOT DISTINCT FROM l."DataOrigem"
        AND (snapshot->>'fabricacao')::date IS NOT DISTINCT FROM l."Fabricacao"
        AND (snapshot->>'coleta')::date IS NOT DISTINCT FROM l."Coleta"
        AND (snapshot->>'validade')::date IS NOT DISTINCT FROM l."Validade"
        AND snapshot->>'fonteValidade' IS NOT DISTINCT FROM l."FonteValidade"
        AND (snapshot->>'responsavelValidadeId')::uuid IS NOT DISTINCT FROM l."ResponsavelValidadeId"
        AND (terminal OR snapshot->>'situacao' IS NOT DISTINCT FROM l."Situacao")
        AND (snapshot->>'ativo')::boolean IS NOT DISTINCT FROM l."Ativo"
        AND snapshot->>'unidade' IS NOT DISTINCT FROM l."Unidade"
        AND (snapshot->>'perfilNutricionalId')::uuid IS NOT DISTINCT FROM l."PerfilNutricionalId"
        AND (snapshot->>'conversaoItemId')::uuid IS NOT DISTINCT FROM l."ConversaoItemId"
        AND snapshot->>'aplicabilidade' IS NOT DISTINCT FROM l."Aplicabilidade"
        AND (terminal OR (snapshot->>'revisao')::integer IS NOT DISTINCT FROM l."Revisao")
        AND (snapshot->>'createdAtUtc')::timestamptz IS NOT DISTINCT FROM l."CreatedAtUtc"
        AND (terminal OR (snapshot->>'updatedAtUtc')::timestamptz IS NOT DISTINCT FROM l."UpdatedAtUtc")
      FROM "LotesMateriais" l WHERE l."Id"=lot_id;
    $$;

    CREATE FUNCTION "EggGroupConsistency"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE group_id uuid; g "GruposPonteOvos"%ROWTYPE; m "MovimentosEstoque"%ROWTYPE; bridge_count bigint;
    BEGIN
      IF TG_TABLE_NAME='GruposPonteOvos' THEN group_id:=NEW."Id";
      ELSIF TG_TABLE_NAME='AlocacoesProvaOvos' THEN SELECT "GrupoId" INTO group_id FROM "PontesProducaoOvos" WHERE "Id"=NEW."PonteId";
      ELSE group_id:=NEW."GrupoId"; END IF;
      SELECT * INTO g FROM "GruposPonteOvos" WHERE "Id"=group_id;
      SELECT * INTO m FROM "MovimentosEstoque" WHERE "Id"=g."MovimentoEstoqueId";
      SELECT count(*) INTO bridge_count FROM "PontesProducaoOvos" WHERE "GrupoId"=g."Id";
      IF NOT EXISTS(SELECT 1 FROM "ComandosPonteOvos" c WHERE c."GrupoId"=g."Id" AND c."Operacao"=g."Operacao" AND c."ExigeAdmin"=(g."Operacao"='Associacao') AND c."RespostaJson"::jsonb #>> '{grupo,id}'=g."Id"::text) THEN
        RAISE EXCEPTION 'Egg group requires its persistent original command result' USING ERRCODE='23514'; END IF;
      IF "EggLotSnapshotMatches"(g."SnapshotLoteJson",g."LoteId",false) IS DISTINCT FROM true
        OR NOT EXISTS(SELECT 1 FROM "LotesMateriais" l WHERE l."Id"=g."LoteId" AND l."Revisao"=g."RevisaoLoteOriginal" AND l."Situacao"=g."SituacaoOriginal" AND l."Validade" IS NOT DISTINCT FROM g."ValidadeOriginal") THEN
        RAISE EXCEPTION 'Egg group original lot snapshot differs from the persisted fact' USING ERRCODE='23514'; END IF;
      IF bridge_count<>g."Quantidade" OR m."EventoId"<>g."EventoId" OR m."ItemId"<>g."ItemId" OR m."LoteId"<>g."LoteId" OR m."LocalId"<>g."LocalId" OR m."Unidade"<>'un' OR m."Sentido"<>'Entrada' THEN
        RAISE EXCEPTION 'Egg group is incomplete or its physical identity differs' USING ERRCODE='23514';
      END IF;
      IF g."Operacao"='Entrada' THEN
        IF m."TipoEvento"<>'Entrada' OR m."Quantidade"<>g."Quantidade" OR m."UnidadeDeclarada"<>'un' OR m."QuantidadeDeclarada"::numeric<>g."Quantidade" OR m."SaldoAnterior"<>0
          OR m."SnapshotJson" #>> '{f03,grupoId}' IS DISTINCT FROM g."Id"::text
          OR NOT EXISTS(SELECT 1 FROM "OrigensLoteOvos" WHERE "GrupoId"=g."Id" AND "LoteId"=g."LoteId")
          OR EXISTS(SELECT 1 FROM "ReservasProducao" WHERE "LoteId"=g."LoteId")
          OR (SELECT count(*) FROM "PosicoesEstoque" WHERE "LoteId"=g."LoteId")<>1
          OR NOT EXISTS(SELECT 1 FROM "PosicoesEstoque" WHERE "LoteId"=g."LoteId" AND "LocalId"=g."LocalId" AND "Revisao"=1 AND "Quantidade"=g."Quantidade")
          OR EXISTS(SELECT 1 FROM "MovimentosEstoque" older JOIN "EventosEstoque" e ON e."Id"=older."EventoId" JOIN "EventosEstoque" current_event ON current_event."Id"=g."EventoId"
            WHERE older."LoteId"=g."LoteId" AND e."Sequencia"<current_event."Sequencia")
          OR EXISTS(SELECT 1 FROM "AlocacoesProvaOvos" a JOIN "PontesProducaoOvos" p ON p."Id"=a."PonteId" WHERE p."GrupoId"=g."Id") THEN
          RAISE EXCEPTION 'New egg receipt requires its dedicated untouched lot and exact bridge count' USING ERRCODE='23514';
        END IF;
      ELSE
        IF NOT EXISTS(SELECT 1 FROM "ProvasEntradaOvos" WHERE "Id"=g."ProvaId" AND "MovimentoEstoqueId"=m."Id" AND "ItemId"=g."ItemId")
          OR (SELECT count(*) FROM "AlocacoesProvaOvos" a JOIN "PontesProducaoOvos" p ON p."Id"=a."PonteId" WHERE p."GrupoId"=g."Id")<>g."Quantidade"
          OR EXISTS(SELECT 1 FROM "OrigensLoteOvos" WHERE "GrupoId"=g."Id") THEN
          RAISE EXCEPTION 'Historical association requires one permanent proof slot per bridge and no new receipt' USING ERRCODE='23514';
        END IF;
      END IF;
      RETURN NULL;
    END $$;
    CREATE CONSTRAINT TRIGGER "TR_GruposPonteOvos_Consistency" AFTER INSERT ON "GruposPonteOvos" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "EggGroupConsistency"();
    CREATE CONSTRAINT TRIGGER "TR_PontesProducaoOvos_Consistency" AFTER INSERT ON "PontesProducaoOvos" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "EggGroupConsistency"();
    CREATE CONSTRAINT TRIGGER "TR_AlocacoesProvaOvos_Consistency" AFTER INSERT ON "AlocacoesProvaOvos" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "EggGroupConsistency"();
    CREATE CONSTRAINT TRIGGER "TR_OrigensLoteOvos_Consistency" AFTER INSERT ON "OrigensLoteOvos" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "EggGroupConsistency"();

    CREATE FUNCTION "EggLotRevisionGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
      IF EXISTS(SELECT 1 FROM "OrigensLoteOvos" WHERE "LoteId"=OLD."Id") AND NEW."Revisao"<>OLD."Revisao"+1 THEN
        RAISE EXCEPTION 'An egg bridge lot alteration requires its next revision' USING ERRCODE='23514';
      END IF;
      RETURN NEW;
    END $$;
    CREATE TRIGGER "TR_LotesMateriais_EggRevision" BEFORE UPDATE ON "LotesMateriais" FOR EACH ROW EXECUTE FUNCTION "EggLotRevisionGuard"();

    CREATE FUNCTION "EggTreatmentGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE g "GruposPonteOvos"%ROWTYPE; l "LotesMateriais"%ROWTYPE; compensation "MovimentosEstoque"%ROWTYPE;
    BEGIN
      SELECT * INTO g FROM "GruposPonteOvos" WHERE "Id"=NEW."GrupoId" FOR SHARE;
      UPDATE "AncorasConciliacaoOvos" SET "Revisao"="Revisao" WHERE "ItemId"=g."ItemId";
      IF NEW."Tipo"='Invalidacao' AND g."Operacao"<>'Associacao' THEN RAISE EXCEPTION 'Only documentary associations can be invalidated' USING ERRCODE='23514'; END IF;
      IF NOT EXISTS(SELECT 1 FROM "ComandosPonteOvos" c, jsonb_array_elements(c."RespostaJson"::jsonb->'tratamentos') t WHERE c."GrupoId"=g."Id" AND c."Operacao"=NEW."Tipo" AND c."ExigeAdmin" AND t->>'id'=NEW."Id"::text) THEN
        RAISE EXCEPTION 'Egg treatment requires its persistent command result' USING ERRCODE='23514'; END IF;
      IF NEW."Tipo"='Compensacao' THEN
        SELECT * INTO l FROM "LotesMateriais" WHERE "Id"=g."LoteId" FOR UPDATE;
        SELECT * INTO compensation FROM "MovimentosEstoque" WHERE "EventoId"=NEW."EventoId";
        IF "EggLotSnapshotMatches"(g."SnapshotLoteJson",g."LoteId",true) IS DISTINCT FROM true OR g."Operacao"<>'Entrada' OR compensation."TipoEvento" IS DISTINCT FROM 'CompensacaoEntradaOvos'
          OR compensation."Quantidade"<>g."Quantidade" OR compensation."LoteId"<>g."LoteId" OR compensation."LocalId"<>g."LocalId"
          OR compensation."SaldoAnterior"<>g."Quantidade" OR compensation."SaldoPosterior"<>0
          OR l."Situacao"<>'Encerrado' OR l."Revisao"<>g."RevisaoLoteOriginal"+1
          OR l."Origem" IS DISTINCT FROM g."SnapshotLoteJson"->>'origem' OR l."Fonte" IS DISTINCT FROM g."SnapshotLoteJson"->>'fonte'
          OR l."CodigoExterno" IS DISTINCT FROM g."SnapshotLoteJson"->>'codigoExterno'
          OR l."ResponsavelId"::text IS DISTINCT FROM g."SnapshotLoteJson"->>'responsavelId'
          OR l."DataOrigem"::text IS DISTINCT FROM g."SnapshotLoteJson"->>'dataOrigem'
          OR l."Fabricacao"::text IS DISTINCT FROM g."SnapshotLoteJson"->>'fabricacao'
          OR l."Coleta"::text IS DISTINCT FROM g."SnapshotLoteJson"->>'coleta'
          OR l."FonteValidade" IS DISTINCT FROM g."SnapshotLoteJson"->>'fonteValidade'
          OR l."ResponsavelValidadeId"::text IS DISTINCT FROM g."SnapshotLoteJson"->>'responsavelValidadeId'
          OR l."PerfilNutricionalId"::text IS DISTINCT FROM g."SnapshotLoteJson"->>'perfilNutricionalId'
          OR l."ConversaoItemId"::text IS DISTINCT FROM g."SnapshotLoteJson"->>'conversaoItemId'
          OR l."Aplicabilidade" IS DISTINCT FROM g."SnapshotLoteJson"->>'aplicabilidade'
          OR l."Ativo" IS DISTINCT FROM (g."SnapshotLoteJson"->>'ativo')::boolean
          OR l."Validade" IS DISTINCT FROM g."ValidadeOriginal"
          OR EXISTS(SELECT 1 FROM "ReservasProducao" WHERE "LoteId"=g."LoteId")
          OR EXISTS(SELECT 1 FROM "MovimentosEstoque" WHERE "LoteId"=g."LoteId" AND "EventoId" NOT IN (g."EventoId",NEW."EventoId"))
          OR NOT EXISTS(SELECT 1 FROM "PosicoesEstoque" WHERE "LoteId"=g."LoteId" AND "LocalId"=g."LocalId" AND "Quantidade"=0) THEN
          RAISE EXCEPTION 'Egg compensation cannot cross any historical use or alteration' USING ERRCODE='23514';
        END IF;
      END IF;
      RETURN NULL;
    END $$;
    CREATE CONSTRAINT TRIGGER "TR_TratamentosPonteOvos_Guard" AFTER INSERT ON "TratamentosPonteOvos" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "EggTreatmentGuard"();

    CREATE TRIGGER "TR_RecortesConciliacaoOvos_Immutable" BEFORE UPDATE OR DELETE ON "RecortesConciliacaoOvos" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_DecisoesConciliacaoOvos_Immutable" BEFORE UPDATE OR DELETE ON "DecisoesConciliacaoOvos" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_ProvasEntradaOvos_Immutable" BEFORE UPDATE OR DELETE ON "ProvasEntradaOvos" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_GruposPonteOvos_Immutable" BEFORE UPDATE OR DELETE ON "GruposPonteOvos" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_PontesProducaoOvos_Immutable" BEFORE UPDATE OR DELETE ON "PontesProducaoOvos" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_AlocacoesProvaOvos_Immutable" BEFORE UPDATE OR DELETE ON "AlocacoesProvaOvos" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_OrigensLoteOvos_Immutable" BEFORE UPDATE OR DELETE ON "OrigensLoteOvos" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_TratamentosPonteOvos_Immutable" BEFORE UPDATE OR DELETE ON "TratamentosPonteOvos" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_ComandosPonteOvos_Immutable" BEFORE UPDATE OR DELETE ON "ComandosPonteOvos" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    CREATE TRIGGER "TR_OrigensRecebimentoOvos_Immutable" BEFORE UPDATE OR DELETE ON "OrigensRecebimentoOvos" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029000136_AddEggBridge') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261029000136_AddEggBridge', '8.0.10');
    END IF;
END $EF$;
COMMIT;
