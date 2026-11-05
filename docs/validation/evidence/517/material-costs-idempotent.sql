START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE SEQUENCE "SequenciaCustoMaterial" START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE TABLE "ApuracoesCustoExecucoes" (
        "Id" uuid NOT NULL,
        "ExecucaoId" uuid NOT NULL,
        "Estado" character varying(100) NOT NULL,
        "ValorConhecido" numeric(18,2) NOT NULL,
        "ValorTotal" numeric(18,2),
        "Cobertura" character varying(100) NOT NULL,
        "ComponentesJson" jsonb NOT NULL,
        "LacunasJson" jsonb NOT NULL,
        "CorteFisico" bigint NOT NULL,
        "CorteMonetario" bigint NOT NULL,
        "SnapshotJson" jsonb NOT NULL,
        "AutorId" uuid NOT NULL,
        "ResponsavelId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "DataOperacional" date NOT NULL,
        "Motivo" character varying(4000) NOT NULL,
        "Correlacao" character varying(4000) NOT NULL,
        "Algoritmo" character varying(100) NOT NULL,
        "ApuracaoOriginalId" uuid,
        CONSTRAINT "PK_ApuracoesCustoExecucoes" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_ApuracaoMaterial_Dados" CHECK ("ValorConhecido">=0 AND ("ValorTotal" IS NULL OR ("Cobertura"='Completo' AND "ValorTotal"="ValorConhecido")) AND ("Cobertura"='Completo' OR "ValorTotal" IS NULL)),
        CONSTRAINT "FK_ApuracoesCustoExecucoes_ApuracoesCustoExecucoes_ApuracaoOri~" FOREIGN KEY ("ApuracaoOriginalId") REFERENCES "ApuracoesCustoExecucoes" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ApuracoesCustoExecucoes_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ApuracoesCustoExecucoes_AspNetUsers_ResponsavelId" FOREIGN KEY ("ResponsavelId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ApuracoesCustoExecucoes_ExecucoesProducao_ExecucaoId" FOREIGN KEY ("ExecucaoId") REFERENCES "ExecucoesProducao" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE TABLE "EventosCustoMateriais" (
        "Id" uuid NOT NULL,
        "Sequencia" bigint NOT NULL,
        "Tipo" character varying(100) NOT NULL,
        "AutorId" uuid NOT NULL,
        "ResponsavelId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "DataOperacional" date NOT NULL,
        "Motivo" character varying(4000) NOT NULL,
        "Correlacao" character varying(4000) NOT NULL,
        "Algoritmo" character varying(100) NOT NULL,
        "SnapshotJson" jsonb NOT NULL,
        "EventoEstoqueId" uuid,
        "ExecucaoId" uuid,
        "EventoOriginalId" uuid,
        CONSTRAINT "PK_EventosCustoMateriais" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_EventosCustoMateriais_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_EventosCustoMateriais_AspNetUsers_ResponsavelId" FOREIGN KEY ("ResponsavelId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_EventosCustoMateriais_EventosCustoMateriais_EventoOriginalId" FOREIGN KEY ("EventoOriginalId") REFERENCES "EventosCustoMateriais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_EventosCustoMateriais_EventosEstoque_EventoEstoqueId" FOREIGN KEY ("EventoEstoqueId") REFERENCES "EventosEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_EventosCustoMateriais_ExecucoesProducao_ExecucaoId" FOREIGN KEY ("ExecucaoId") REFERENCES "ExecucoesProducao" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE TABLE "EvidenciasCustoMateriais" (
        "Id" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Fonte" character varying(4000) NOT NULL,
        "ReferenciaProtegida" character varying(4000) NOT NULL,
        "DataFonte" date NOT NULL,
        "Moeda" character varying(4000) NOT NULL,
        "Unidade" character varying(4000) NOT NULL,
        "Quantidade" numeric(20,6) NOT NULL,
        "Total" numeric(18,2) NOT NULL,
        "ComponentesJson" jsonb NOT NULL,
        "LacunasJson" jsonb NOT NULL,
        "Metodo" character varying(4000) NOT NULL,
        "AutorId" uuid NOT NULL,
        "ResponsavelId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "DataOperacional" date NOT NULL,
        "Motivo" character varying(4000) NOT NULL,
        "Correlacao" character varying(4000) NOT NULL,
        "Algoritmo" character varying(100) NOT NULL,
        "HashDocumento" character varying(128),
        CONSTRAINT "PK_EvidenciasCustoMateriais" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_EvidenciaMaterial_Dados" CHECK ("Moeda"='BRL' AND "Quantidade">0 AND "Total">=0),
        CONSTRAINT "FK_EvidenciasCustoMateriais_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_EvidenciasCustoMateriais_AspNetUsers_ResponsavelId" FOREIGN KEY ("ResponsavelId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_EvidenciasCustoMateriais_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE TABLE "PrecosReferenciaMateriais" (
        "Id" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Fonte" character varying(4000) NOT NULL,
        "ReferenciaProtegida" character varying(4000) NOT NULL,
        "DataFonte" date NOT NULL,
        "Moeda" character varying(4000) NOT NULL,
        "Unidade" character varying(4000) NOT NULL,
        "QuantidadeBase" numeric(20,6) NOT NULL,
        "ValorUnitario" numeric(20,6) NOT NULL,
        "VigenciaInicio" date NOT NULL,
        "VigenciaFim" date,
        "Aplicabilidade" character varying(4000) NOT NULL,
        "Metodo" character varying(4000) NOT NULL,
        "AutorId" uuid NOT NULL,
        "ResponsavelId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "DataOperacional" date NOT NULL,
        "Motivo" character varying(4000) NOT NULL,
        "Correlacao" character varying(4000) NOT NULL,
        "Algoritmo" character varying(100) NOT NULL,
        "SubstituiId" uuid,
        "Revogada" boolean NOT NULL,
        CONSTRAINT "PK_PrecosReferenciaMateriais" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_PrecoMaterial_Dados" CHECK ("Moeda"='BRL' AND "QuantidadeBase">0 AND "ValorUnitario">=0 AND ("VigenciaFim" IS NULL OR "VigenciaFim">"VigenciaInicio")),
        CONSTRAINT "FK_PrecosReferenciaMateriais_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PrecosReferenciaMateriais_AspNetUsers_ResponsavelId" FOREIGN KEY ("ResponsavelId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PrecosReferenciaMateriais_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PrecosReferenciaMateriais_PrecosReferenciaMateriais_Substit~" FOREIGN KEY ("SubstituiId") REFERENCES "PrecosReferenciaMateriais" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE TABLE "ComandosCustoMateriais" (
        "Id" uuid NOT NULL,
        "Operacao" character varying(100) NOT NULL,
        "RecursoId" uuid NOT NULL,
        "Chave" character varying(100) NOT NULL,
        "HashPayload" character varying(128) NOT NULL,
        "AutorId" uuid NOT NULL,
        "ExigeAdmin" boolean NOT NULL,
        "StatusHttp" integer NOT NULL,
        "Location" character varying(4000),
        "RespostaJson" TEXT NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "EventoId" uuid,
        "RegistroId" uuid,
        CONSTRAINT "PK_ComandosCustoMateriais" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ComandosCustoMateriais_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ComandosCustoMateriais_EventosCustoMateriais_EventoId" FOREIGN KEY ("EventoId") REFERENCES "EventosCustoMateriais" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE TABLE "CamadasCustoMateriais" (
        "Id" uuid NOT NULL,
        "LoteId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Unidade" character varying(4000) NOT NULL,
        "QuantidadeOriginal" numeric(20,6) NOT NULL,
        "ValorConhecido" numeric(18,2) NOT NULL,
        "Estado" character varying(100) NOT NULL,
        "TipoOrigem" character varying(4000) NOT NULL,
        "SequenciaOrigem" bigint NOT NULL,
        "ComponentesJson" jsonb NOT NULL,
        "LacunasJson" jsonb NOT NULL,
        "AutorId" uuid NOT NULL,
        "ResponsavelId" uuid NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        "DataOperacional" date NOT NULL,
        "Motivo" character varying(4000) NOT NULL,
        "Correlacao" character varying(4000) NOT NULL,
        "Algoritmo" character varying(100) NOT NULL,
        "MovimentoOrigemId" uuid,
        "SaidaProducaoId" uuid,
        "EvidenciaId" uuid,
        "CamadaReferenciaId" uuid,
        CONSTRAINT "PK_CamadasCustoMateriais" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_CamadaMaterial_Dados" CHECK ("QuantidadeOriginal">0 AND "ValorConhecido">=0 AND "Estado" IN ('Completo','Parcial','Desconhecido') AND "Unidade" IN ('kg','L','un') AND ("Unidade"<>'un' OR trunc("QuantidadeOriginal")="QuantidadeOriginal")),
        CONSTRAINT "FK_CamadasCustoMateriais_AspNetUsers_AutorId" FOREIGN KEY ("AutorId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CamadasCustoMateriais_AspNetUsers_ResponsavelId" FOREIGN KEY ("ResponsavelId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CamadasCustoMateriais_CamadasCustoMateriais_CamadaReferenci~" FOREIGN KEY ("CamadaReferenciaId") REFERENCES "CamadasCustoMateriais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CamadasCustoMateriais_EvidenciasCustoMateriais_EvidenciaId" FOREIGN KEY ("EvidenciaId") REFERENCES "EvidenciasCustoMateriais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CamadasCustoMateriais_Itens_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Itens" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CamadasCustoMateriais_LotesMateriais_LoteId" FOREIGN KEY ("LoteId") REFERENCES "LotesMateriais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CamadasCustoMateriais_MovimentosEstoque_MovimentoOrigemId" FOREIGN KEY ("MovimentoOrigemId") REFERENCES "MovimentosEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CamadasCustoMateriais_SaidasProducao_SaidaProducaoId" FOREIGN KEY ("SaidaProducaoId") REFERENCES "SaidasProducao" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE TABLE "DestinosCustoExecucoes" (
        "Id" uuid NOT NULL,
        "ApuracaoId" uuid NOT NULL,
        "DestinoId" uuid NOT NULL,
        "Tipo" character varying(100) NOT NULL,
        "Quantidade" numeric(20,6) NOT NULL,
        "Unidade" character varying(4000) NOT NULL,
        "Percentual" numeric(9,6) NOT NULL,
        "ValorConhecido" numeric(18,2) NOT NULL,
        "ValorTotal" numeric(18,2),
        "CustoUnitarioCompleto" numeric(20,6),
        "Cobertura" character varying(100) NOT NULL,
        "ComponentesJson" jsonb NOT NULL,
        "LacunasJson" jsonb NOT NULL,
        "OrdemRateio" integer NOT NULL,
        "RestoRateio" jsonb NOT NULL,
        "Motivo" character varying(4000) NOT NULL,
        "SaidaProducaoId" uuid,
        "PerdaId" uuid,
        "CamadaSaidaId" uuid,
        CONSTRAINT "PK_DestinosCustoExecucoes" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_DestinoMaterial_Dados" CHECK ("Percentual" BETWEEN 0 AND 100 AND "ValorConhecido">=0 AND "Tipo" IN ('Saida','Perda') AND ("ValorTotal" IS NULL OR "Cobertura"='Completo') AND ("Tipo"<>'Perda' OR ("CustoUnitarioCompleto" IS NULL AND "SaidaProducaoId" IS NULL)) AND ("Quantidade">0 OR ("Percentual"=0 AND "CustoUnitarioCompleto" IS NULL))),
        CONSTRAINT "FK_DestinosCustoExecucoes_ApuracoesCustoExecucoes_ApuracaoId" FOREIGN KEY ("ApuracaoId") REFERENCES "ApuracoesCustoExecucoes" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DestinosCustoExecucoes_CamadasCustoMateriais_CamadaSaidaId" FOREIGN KEY ("CamadaSaidaId") REFERENCES "CamadasCustoMateriais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DestinosCustoExecucoes_SaidasProducao_SaidaProducaoId" FOREIGN KEY ("SaidaProducaoId") REFERENCES "SaidasProducao" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE TABLE "ParcelasCustoMateriais" (
        "Id" uuid NOT NULL,
        "EventoCustoId" uuid NOT NULL,
        "CamadaId" uuid NOT NULL,
        "LocalId" uuid NOT NULL,
        "Sentido" character varying(4000) NOT NULL,
        "Quantidade" numeric(20,6) NOT NULL,
        "ValorConhecido" numeric(18,2) NOT NULL,
        "ComponentesJson" jsonb NOT NULL,
        "LacunasJson" jsonb NOT NULL,
        "MovimentoEstoqueId" uuid,
        "ParcelaOriginalId" uuid,
        CONSTRAINT "PK_ParcelasCustoMateriais" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_ParcelaMaterial_Dados" CHECK ("Quantidade">0 AND "ValorConhecido">=0 AND "Sentido" IN ('Entrada','Saida')),
        CONSTRAINT "FK_ParcelasCustoMateriais_CamadasCustoMateriais_CamadaId" FOREIGN KEY ("CamadaId") REFERENCES "CamadasCustoMateriais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ParcelasCustoMateriais_EventosCustoMateriais_EventoCustoId" FOREIGN KEY ("EventoCustoId") REFERENCES "EventosCustoMateriais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ParcelasCustoMateriais_LocaisEstoque_LocalId" FOREIGN KEY ("LocalId") REFERENCES "LocaisEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ParcelasCustoMateriais_MovimentosEstoque_MovimentoEstoqueId" FOREIGN KEY ("MovimentoEstoqueId") REFERENCES "MovimentosEstoque" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ParcelasCustoMateriais_ParcelasCustoMateriais_ParcelaOrigin~" FOREIGN KEY ("ParcelaOriginalId") REFERENCES "ParcelasCustoMateriais" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE TABLE "PosicoesCustoMateriais" (
        "CamadaId" uuid NOT NULL,
        "LocalId" uuid NOT NULL,
        "Unidade" text NOT NULL,
        "Quantidade" numeric(20,6) NOT NULL,
        "ValorConhecido" numeric(18,2) NOT NULL,
        "ComponentesJson" jsonb NOT NULL,
        "LacunasJson" jsonb NOT NULL,
        "Estado" text NOT NULL,
        "Revisao" integer NOT NULL,
        CONSTRAINT "PK_PosicoesCustoMateriais" PRIMARY KEY ("CamadaId", "LocalId"),
        CONSTRAINT "CK_PosicaoMaterial_Dados" CHECK ("Quantidade">=0 AND "ValorConhecido">=0 AND ("Quantidade">0 OR "ValorConhecido"=0) AND "Estado" IN ('Completo','Parcial','Desconhecido')),
        CONSTRAINT "FK_PosicoesCustoMateriais_CamadasCustoMateriais_CamadaId" FOREIGN KEY ("CamadaId") REFERENCES "CamadasCustoMateriais" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PosicoesCustoMateriais_LocaisEstoque_LocalId" FOREIGN KEY ("LocalId") REFERENCES "LocaisEstoque" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE UNIQUE INDEX "IX_ApuracoesCustoExecucoes_ApuracaoOriginalId" ON "ApuracoesCustoExecucoes" ("ApuracaoOriginalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_ApuracoesCustoExecucoes_AutorId" ON "ApuracoesCustoExecucoes" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE UNIQUE INDEX "IX_ApuracoesCustoExecucoes_ExecucaoId" ON "ApuracoesCustoExecucoes" ("ExecucaoId") WHERE "ApuracaoOriginalId" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_ApuracoesCustoExecucoes_ResponsavelId" ON "ApuracoesCustoExecucoes" ("ResponsavelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_CamadasCustoMateriais_AutorId" ON "CamadasCustoMateriais" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_CamadasCustoMateriais_CamadaReferenciaId" ON "CamadasCustoMateriais" ("CamadaReferenciaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_CamadasCustoMateriais_EvidenciaId" ON "CamadasCustoMateriais" ("EvidenciaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_CamadasCustoMateriais_ItemId" ON "CamadasCustoMateriais" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_CamadasCustoMateriais_LoteId_SequenciaOrigem_Id" ON "CamadasCustoMateriais" ("LoteId", "SequenciaOrigem", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE UNIQUE INDEX "IX_CamadasCustoMateriais_MovimentoOrigemId" ON "CamadasCustoMateriais" ("MovimentoOrigemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_CamadasCustoMateriais_ResponsavelId" ON "CamadasCustoMateriais" ("ResponsavelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE UNIQUE INDEX "IX_CamadasCustoMateriais_SaidaProducaoId" ON "CamadasCustoMateriais" ("SaidaProducaoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_ComandosCustoMateriais_AutorId" ON "ComandosCustoMateriais" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_ComandosCustoMateriais_EventoId" ON "ComandosCustoMateriais" ("EventoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE UNIQUE INDEX "IX_ComandosCustoMateriais_RecursoId_Operacao_Chave" ON "ComandosCustoMateriais" ("RecursoId", "Operacao", "Chave");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE UNIQUE INDEX "IX_DestinosCustoExecucoes_ApuracaoId_DestinoId" ON "DestinosCustoExecucoes" ("ApuracaoId", "DestinoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_DestinosCustoExecucoes_CamadaSaidaId" ON "DestinosCustoExecucoes" ("CamadaSaidaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_DestinosCustoExecucoes_SaidaProducaoId" ON "DestinosCustoExecucoes" ("SaidaProducaoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_EventosCustoMateriais_AutorId" ON "EventosCustoMateriais" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_EventosCustoMateriais_EventoEstoqueId" ON "EventosCustoMateriais" ("EventoEstoqueId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE UNIQUE INDEX "IX_EventosCustoMateriais_EventoOriginalId" ON "EventosCustoMateriais" ("EventoOriginalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_EventosCustoMateriais_ExecucaoId" ON "EventosCustoMateriais" ("ExecucaoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_EventosCustoMateriais_ResponsavelId" ON "EventosCustoMateriais" ("ResponsavelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE UNIQUE INDEX "IX_EventosCustoMateriais_Sequencia" ON "EventosCustoMateriais" ("Sequencia");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_EvidenciasCustoMateriais_AutorId" ON "EvidenciasCustoMateriais" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_EvidenciasCustoMateriais_ItemId" ON "EvidenciasCustoMateriais" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_EvidenciasCustoMateriais_ResponsavelId" ON "EvidenciasCustoMateriais" ("ResponsavelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_ParcelasCustoMateriais_CamadaId_LocalId_EventoCustoId" ON "ParcelasCustoMateriais" ("CamadaId", "LocalId", "EventoCustoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_ParcelasCustoMateriais_EventoCustoId" ON "ParcelasCustoMateriais" ("EventoCustoId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_ParcelasCustoMateriais_LocalId" ON "ParcelasCustoMateriais" ("LocalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_ParcelasCustoMateriais_MovimentoEstoqueId" ON "ParcelasCustoMateriais" ("MovimentoEstoqueId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE UNIQUE INDEX "IX_ParcelasCustoMateriais_ParcelaOriginalId" ON "ParcelasCustoMateriais" ("ParcelaOriginalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_PosicoesCustoMateriais_LocalId" ON "PosicoesCustoMateriais" ("LocalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_PrecosReferenciaMateriais_AutorId" ON "PrecosReferenciaMateriais" ("AutorId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_PrecosReferenciaMateriais_ItemId_VigenciaInicio_Id" ON "PrecosReferenciaMateriais" ("ItemId", "VigenciaInicio", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE INDEX "IX_PrecosReferenciaMateriais_ResponsavelId" ON "PrecosReferenciaMateriais" ("ResponsavelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE UNIQUE INDEX "IX_PrecosReferenciaMateriais_SubstituiId" ON "PrecosReferenciaMateriais" ("SubstituiId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    CREATE TABLE "TravaLivroCustoMaterial" (
      "Unica" boolean PRIMARY KEY CHECK ("Unica"),
      "Versao" bigint NOT NULL
    );
    CREATE FUNCTION "MaterialCostLock"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
      -- Same physical serialization resource; application acquires catalog first.
      PERFORM pg_advisory_xact_lock(430,1);
      -- The advisory lock alone cannot advance an older REPEATABLE READ snapshot.
      -- A concurrent writer must also see the last committed monetary transaction.
      INSERT INTO "TravaLivroCustoMaterial" ("Unica", "Versao") VALUES (true, 1)
      ON CONFLICT ("Unica") DO UPDATE SET "Versao"="TravaLivroCustoMaterial"."Versao"+1;
      RETURN CASE WHEN TG_OP='DELETE' THEN OLD ELSE NEW END;
    END $$;
    CREATE FUNCTION "MaterialCostImmutable"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN RAISE EXCEPTION 'Material cost facts are append-only' USING ERRCODE='23514'; END $$;
    CREATE FUNCTION "MaterialCostPositionRevision"() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
     IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Cost projections cannot be deleted' USING ERRCODE='23514'; END IF;
     IF NEW."CamadaId"<>OLD."CamadaId" OR NEW."LocalId"<>OLD."LocalId" OR NEW."Unidade"<>OLD."Unidade" OR NEW."Revisao"<>OLD."Revisao"+1 THEN
       RAISE EXCEPTION 'Invalid cost projection revision or identity' USING ERRCODE='23514';
     END IF;
     RETURN NEW;
    END $$;
    CREATE FUNCTION "MaterialComponentsValid"(components jsonb) RETURNS boolean LANGUAGE plpgsql IMMUTABLE AS $$
    DECLARE c jsonb; n numeric; d numeric;
    BEGIN
     IF jsonb_typeof(components)<>'array' OR jsonb_array_length(components)>1000 THEN RETURN false; END IF;
     IF (SELECT count(*) FROM jsonb_array_elements(components))<>(SELECT count(DISTINCT x->>'id') FROM jsonb_array_elements(components) x) THEN RETURN false; END IF;
     FOR c IN SELECT * FROM jsonb_array_elements(components) LOOP
       IF coalesce(c->>'id','')!~'^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' OR
          coalesce(c->>'camadaId','')!~'^[0-9a-f-]{36}$' OR coalesce(c->>'itemId','')!~'^[0-9a-f-]{36}$' OR
          coalesce(c->>'unidade','') NOT IN ('kg','L','un') OR coalesce(c->>'quantidade','')!~'^(0|[1-9][0-9]{0,13})(\.[0-9]{1,6})?$' OR
          coalesce(c->>'fracaoNumerador','')!~'^[0-9]+$' OR coalesce(c->>'fracaoDenominador','')!~'^[0-9]+$' OR
          coalesce(jsonb_typeof(c->'lacunas'),'')<>'array' OR coalesce(jsonb_typeof(c->'valorConhecido'),'') NOT IN ('string','null') THEN RETURN false; END IF;
       n=(c->>'fracaoNumerador')::numeric;d=(c->>'fracaoDenominador')::numeric;
       IF d<=0 OR n<=0 OR n>d OR (c->>'quantidade')::numeric<=0 OR (c->>'unidade'='un' AND mod((c->>'quantidade')::numeric,1)<>0) THEN RETURN false; END IF;
       IF c->>'valorConhecido' IS NOT NULL AND (c->>'valorConhecido')!~'^(0|[1-9][0-9]{0,15})(\.[0-9]{1,2})?$' THEN RETURN false; END IF;
       IF EXISTS(SELECT 1 FROM jsonb_array_elements(c->'lacunas') gap WHERE jsonb_typeof(gap)<>'string' OR length(trim(gap#>>'{}'))=0) THEN RETURN false; END IF;
     END LOOP;
     RETURN true;
    EXCEPTION WHEN invalid_text_representation OR numeric_value_out_of_range THEN RETURN false;
    END $$;
    CREATE FUNCTION "MaterialCostPhysicalRequired"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE amount numeric;
    BEGIN
     -- Only inserted physical facts are checked. Historical rows receive no backfill.
     SELECT coalesce(sum(p."Quantidade"),0) INTO amount FROM "ParcelasCustoMateriais" p
     JOIN "CamadasCustoMateriais" l ON l."Id"=p."CamadaId"
     WHERE p."MovimentoEstoqueId"=NEW."Id" AND p."Sentido"=NEW."Sentido" AND p."LocalId"=NEW."LocalId" AND l."LoteId"=NEW."LoteId";
     IF amount<>NEW."Quantidade" THEN RAISE EXCEPTION 'Physical movement requires exact monetary coverage, including unknown cost' USING ERRCODE='23514'; END IF;
     RETURN NULL;
    END $$;
    CREATE FUNCTION "MaterialCostConsistency"() RETURNS trigger LANGUAGE plpgsql AS $$
    DECLARE rowdata record; component_value jsonb; component_source record; fraction_n numeric; fraction_d numeric; total_n numeric; total_d numeric; gcd_a numeric; gcd_b numeric; gcd_r numeric; current_id text;
            position_layer uuid; position_location uuid; total_known numeric;
    BEGIN
     -- Projections are a consequence of the immutable ledger, never its authority.
     IF EXISTS(
       SELECT 1 FROM (
         SELECT "CamadaId","LocalId",sum(CASE WHEN "Sentido"='Entrada' THEN "Quantidade" ELSE -"Quantidade" END) q,
           sum(CASE WHEN "Sentido"='Entrada' THEN "ValorConhecido" ELSE -"ValorConhecido" END) k
         FROM "ParcelasCustoMateriais" GROUP BY "CamadaId","LocalId"
       ) book FULL JOIN "PosicoesCustoMateriais" p ON p."CamadaId"=book."CamadaId" AND p."LocalId"=book."LocalId"
       WHERE book."CamadaId" IS NULL OR p."CamadaId" IS NULL OR book.q<>p."Quantidade" OR book.k<>p."ValorConhecido"
     ) THEN RAISE EXCEPTION 'Cost ledger and projection disagree' USING ERRCODE='23514'; END IF;
     IF EXISTS(SELECT 1 FROM "PosicoesCustoMateriais" p JOIN "CamadasCustoMateriais" l ON l."Id"=p."CamadaId" WHERE p."Unidade"<>l."Unidade" OR p."Revisao"<1) THEN RAISE EXCEPTION 'Invalid cost position identity' USING ERRCODE='23514';END IF;
     IF EXISTS(
       SELECT 1 FROM "PosicoesCustoMateriais" p JOIN "CamadasCustoMateriais" l ON l."Id"=p."CamadaId"
       GROUP BY l."LoteId",p."LocalId" HAVING sum(p."Quantidade")>coalesce((SELECT s."Quantidade" FROM "PosicoesEstoque" s WHERE s."LoteId"=l."LoteId" AND s."LocalId"=p."LocalId"),0)
     ) THEN RAISE EXCEPTION 'Monetary coverage exceeds physical quantity' USING ERRCODE='23514'; END IF;
     IF EXISTS(
       SELECT 1 FROM "ParcelasCustoMateriais" p JOIN "CamadasCustoMateriais" l ON l."Id"=p."CamadaId"
       JOIN "MovimentosEstoque" m ON m."Id"=p."MovimentoEstoqueId"
       WHERE m."LoteId"<>l."LoteId" OR m."ItemId"<>l."ItemId" OR m."Unidade"<>l."Unidade" OR m."LocalId"<>p."LocalId" OR m."Sentido"<>p."Sentido"
     ) THEN RAISE EXCEPTION 'Cost parcel has incompatible physical provenance' USING ERRCODE='23514'; END IF;
     IF TG_TABLE_NAME='ParcelasCustoMateriais' AND TG_OP='INSERT' THEN
       IF NEW."MovimentoEstoqueId" IS NOT NULL AND EXISTS(
         SELECT 1 FROM "MovimentosEstoque" m WHERE m."Id"=NEW."MovimentoEstoqueId" AND m."Quantidade"<>(
           SELECT sum(p."Quantidade") FROM "ParcelasCustoMateriais" p WHERE p."MovimentoEstoqueId"=m."Id")
       ) THEN RAISE EXCEPTION 'Linked parcels must sum to the physical movement' USING ERRCODE='23514'; END IF;
     END IF;
     IF EXISTS(
       SELECT 1 FROM "ParcelasCustoMateriais" p JOIN "EventosCustoMateriais" e ON e."Id"=p."EventoCustoId"
       LEFT JOIN "MovimentosEstoque" m ON m."Id"=p."MovimentoEstoqueId"
       WHERE (m."Id" IS NULL AND e."Tipo" NOT IN ('ReconhecimentoLegado','AvaliacaoPresente','CompensacaoAvaliacao','ResolucaoRateioPresente'))
          OR (m."Id" IS NOT NULL AND e."Tipo"<>'Producao' AND e."EventoEstoqueId" IS DISTINCT FROM m."EventoId")
     ) THEN RAISE EXCEPTION 'Cost parcel requires the corresponding physical or explicit present event' USING ERRCODE='23514'; END IF;
     IF EXISTS(
       SELECT 1 FROM "CamadasCustoMateriais" l LEFT JOIN "MovimentosEstoque" m ON m."Id"=l."MovimentoOrigemId"
       LEFT JOIN "SaidasProducao" s ON s."Id"=l."SaidaProducaoId"
       WHERE (m."Id" IS NOT NULL AND (m."Sentido"<>'Entrada' OR m."LoteId"<>l."LoteId" OR m."ItemId"<>l."ItemId" OR m."Unidade"<>l."Unidade" OR m."Quantidade"<>l."QuantidadeOriginal"))
         OR (s."Id" IS NOT NULL AND (s."LoteId"<>l."LoteId" OR s."Quantidade"<>l."QuantidadeOriginal" OR m."EventoId"<>s."EventoId"))
         OR (l."MovimentoOrigemId" IS NULL AND l."TipoOrigem" NOT IN ('Legado','Avaliacao','ApuracaoPresente'))
         OR (l."TipoOrigem"='ApuracaoPresente' AND l."CamadaReferenciaId" IS NULL)
         OR (l."TipoOrigem" IN ('Recebimento','Avaliacao') AND l."Estado"<>'Desconhecido' AND l."EvidenciaId" IS NULL)
         OR (l."Estado"='Desconhecido' AND l."ValorConhecido"<>0)
         OR (l."TipoOrigem"='Legado' AND (l."Estado"<>'Desconhecido' OR l."SequenciaOrigem"<>0 OR l."EvidenciaId" IS NOT NULL))
     ) THEN RAISE EXCEPTION 'Cost layer has incompatible origin or evidence' USING ERRCODE='23514'; END IF;
     IF EXISTS(
       SELECT 1 FROM "CamadasCustoMateriais" l JOIN "EvidenciasCustoMateriais" e ON e."Id"=l."EvidenciaId"
       WHERE e."ItemId"<>l."ItemId" OR e."Unidade"<>l."Unidade" OR e."Quantidade"<>l."QuantidadeOriginal" OR e."Total"<>l."ValorConhecido"
         OR jsonb_array_length(e."ComponentesJson")=0 OR length(trim(e."Fonte"))=0 OR length(trim(e."ReferenciaProtegida"))=0
     ) THEN RAISE EXCEPTION 'Documentary evidence does not cover its layer' USING ERRCODE='23514'; END IF;
     FOR rowdata IN
       SELECT "ComponentesJson" components,"ValorConhecido" known,"Estado" coverage,"LacunasJson" gaps,false zero_policy FROM "CamadasCustoMateriais"
       UNION ALL SELECT "ComponentesJson","ValorConhecido",NULL,"LacunasJson",false FROM "ParcelasCustoMateriais"
       UNION ALL SELECT "ComponentesJson","ValorConhecido","Estado","LacunasJson",false FROM "PosicoesCustoMateriais"
       UNION ALL SELECT "ComponentesJson","ValorConhecido","Cobertura","LacunasJson",false FROM "ApuracoesCustoExecucoes"
       UNION ALL SELECT "ComponentesJson","ValorConhecido","Cobertura","LacunasJson","Percentual"=0 FROM "DestinosCustoExecucoes"
     LOOP
       IF NOT "MaterialComponentsValid"(rowdata.components) OR coalesce((SELECT sum((x->>'valorConhecido')::numeric) FROM jsonb_array_elements(rowdata.components) x),0)<>rowdata.known THEN
         RAISE EXCEPTION 'Invalid component schema or cent sum' USING ERRCODE='23514';
       END IF;
       IF rowdata.coverage IS NOT NULL AND NOT rowdata.zero_policy AND (
           (rowdata.coverage='Completo' AND (jsonb_array_length(rowdata.components)=0 OR jsonb_array_length(rowdata.gaps)>0 OR EXISTS(SELECT 1 FROM jsonb_array_elements(rowdata.components) x WHERE x->>'valorConhecido' IS NULL OR jsonb_array_length(x->'lacunas')>0)))
           OR (rowdata.coverage='Desconhecido' AND EXISTS(SELECT 1 FROM jsonb_array_elements(rowdata.components) x WHERE x->>'valorConhecido' IS NOT NULL))
           OR (rowdata.coverage='Parcial' AND NOT EXISTS(SELECT 1 FROM jsonb_array_elements(rowdata.components) x WHERE x->>'valorConhecido' IS NOT NULL))) THEN
           RAISE EXCEPTION 'Coverage state contradicts documentary components' USING ERRCODE='23514';
       END IF;
       FOR component_value IN SELECT * FROM jsonb_array_elements(rowdata.components) LOOP
         SELECT * INTO component_source FROM "CamadasCustoMateriais" WHERE "Id"=(component_value->>'camadaId')::uuid;
         IF NOT FOUND OR component_source."ItemId"<>(component_value->>'itemId')::uuid OR component_source."Unidade"<>component_value->>'unidade' OR component_source."QuantidadeOriginal"<>(component_value->>'quantidade')::numeric THEN
           RAISE EXCEPTION 'Component source identity is invalid' USING ERRCODE='23514';
         END IF;
         IF component_source."EvidenciaId" IS DISTINCT FROM (component_value->>'evidenciaId')::uuid THEN
           RAISE EXCEPTION 'Component evidence must match its immutable source' USING ERRCODE='23514';
         END IF;
         IF NOT EXISTS(SELECT 1 FROM jsonb_array_elements(component_source."ComponentesJson") source_component
           WHERE source_component->>'id'=component_value->>'id' AND source_component->>'camadaId'=component_value->>'camadaId'
           AND source_component->'lacunas'=component_value->'lacunas'
           AND ((source_component->>'valorConhecido' IS NULL AND component_value->>'valorConhecido' IS NULL)
             OR (source_component->>'valorConhecido' IS NOT NULL AND component_value->>'valorConhecido' IS NOT NULL AND (component_value->>'valorConhecido')::numeric<=(source_component->>'valorConhecido')::numeric))) THEN
           RAISE EXCEPTION 'Component identity, documentary gaps or value diverges from its origin' USING ERRCODE='23514';
         END IF;
         IF component_value->>'valorConhecido' IS NOT NULL AND component_value->>'evidenciaId' IS NULL AND NOT ((component_value->>'valorConhecido')::numeric=0 AND component_source."TipoOrigem" IN ('Producao','ApuracaoPresente') AND EXISTS(SELECT 1 FROM "DestinosCustoExecucoes" z WHERE z."CamadaSaidaId"=component_source."Id" AND z."Percentual"=0)) THEN
           RAISE EXCEPTION 'Known component requires documentary evidence or explicit zero allocation' USING ERRCODE='23514';
         END IF;
       END LOOP;
     END LOOP;
     -- Compare the fractions and cents of the affected projection to the signed ledger.
     IF TG_TABLE_NAME IN ('ParcelasCustoMateriais','PosicoesCustoMateriais') AND TG_OP IN ('INSERT','UPDATE') THEN
       position_layer=NEW."CamadaId"; position_location=NEW."LocalId";
       current_id=NULL; total_n=0; total_d=1; total_known=0;
       FOR rowdata IN
         SELECT entry->>'id' AS id, (entry->>'fracaoNumerador')::numeric AS numerator,
                (entry->>'fracaoDenominador')::numeric AS denominator,
                coalesce((entry->>'valorConhecido')::numeric,0) AS known, direction
         FROM (
           SELECT "ComponentesJson" AS components, CASE WHEN "Sentido"='Entrada' THEN 1 ELSE -1 END AS direction
           FROM "ParcelasCustoMateriais" WHERE "CamadaId"=position_layer AND "LocalId"=position_location
           UNION ALL
           SELECT "ComponentesJson", -1 FROM "PosicoesCustoMateriais" WHERE "CamadaId"=position_layer AND "LocalId"=position_location
         ) parts CROSS JOIN LATERAL jsonb_array_elements(parts.components) entry
         ORDER BY (entry->>'id') COLLATE "C"
       LOOP
         IF current_id IS DISTINCT FROM rowdata.id THEN
           IF total_n<>0 OR total_known<>0 THEN RAISE EXCEPTION 'Component projection differs from ledger' USING ERRCODE='23514'; END IF;
           current_id=rowdata.id; total_n=0; total_d=1; total_known=0;
         END IF;
         total_n=total_n*rowdata.denominator+rowdata.direction*rowdata.numerator*total_d;
         total_d=total_d*rowdata.denominator;
         total_known=total_known+rowdata.direction*rowdata.known;
         gcd_a=abs(total_n); gcd_b=total_d;
         WHILE gcd_b<>0 LOOP gcd_r=mod(gcd_a,gcd_b); gcd_a=gcd_b; gcd_b=gcd_r; END LOOP;
         total_n=total_n/gcd_a; total_d=total_d/gcd_a;
       END LOOP;
       IF total_n<>0 OR total_known<>0 THEN RAISE EXCEPTION 'Component projection differs from ledger' USING ERRCODE='23514'; END IF;
     END IF;
     -- Sum rational coverage exactly, without rounded percentages or physical unit mixing.
     current_id=NULL;total_n=0;total_d=1;
     FOR rowdata IN SELECT x->>'id' id,x->>'fracaoNumerador' n,x->>'fracaoDenominador' d FROM "PosicoesCustoMateriais" p CROSS JOIN LATERAL jsonb_array_elements(p."ComponentesJson") x WHERE p."Quantidade">0 ORDER BY (x->>'id') COLLATE "C" LOOP
       IF current_id IS DISTINCT FROM rowdata.id THEN current_id=rowdata.id;total_n=0;total_d=1;END IF;
       fraction_n=rowdata.n::numeric;fraction_d=rowdata.d::numeric;total_n=total_n*fraction_d+fraction_n*total_d;total_d=total_d*fraction_d;
       gcd_a=total_n;gcd_b=total_d;WHILE gcd_b<>0 LOOP gcd_r=mod(gcd_a,gcd_b);gcd_a=gcd_b;gcd_b=gcd_r;END LOOP;
       IF gcd_a<>0 THEN total_n=total_n/gcd_a;total_d=total_d/gcd_a;END IF;
       IF total_n>total_d THEN RAISE EXCEPTION 'Component coverage overlaps' USING ERRCODE='23514';END IF;
     END LOOP;
     IF EXISTS(
       SELECT 1 FROM "ParcelasCustoMateriais" p JOIN "ParcelasCustoMateriais" original_part ON original_part."Id"=p."ParcelaOriginalId"
       WHERE p."CamadaId"<>original_part."CamadaId" OR p."LocalId"<>original_part."LocalId" OR p."Sentido"=original_part."Sentido" OR p."Quantidade"<>original_part."Quantidade" OR p."ValorConhecido"<>original_part."ValorConhecido" OR p."ComponentesJson"<>original_part."ComponentesJson"
     ) THEN RAISE EXCEPTION 'Compensation must use the exact original parcels' USING ERRCODE='23514'; END IF;
     -- Each new physical execution has one frozen calculation, including pending allocation.
     IF EXISTS(
       SELECT 1 FROM "ApuracoesCustoExecucoes" a WHERE
         (a."Estado" NOT IN ('PendenteRateio','Apurada')
          OR (a."ApuracaoOriginalId" IS NOT NULL AND a."Estado"<>'Apurada')
          OR (a."ApuracaoOriginalId" IS NULL AND a."ValorConhecido"<>coalesce((SELECT sum(p."ValorConhecido") FROM "ParcelasCustoMateriais" p JOIN "EventosCustoMateriais" e ON e."Id"=p."EventoCustoId" WHERE e."ExecucaoId"=a."ExecucaoId" AND e."Tipo"='Producao' AND p."Sentido"='Saida'),0))
          OR (a."Estado"='PendenteRateio' AND (a."ValorTotal" IS NOT NULL OR EXISTS(SELECT 1 FROM "DestinosCustoExecucoes" d WHERE d."ApuracaoId"=a."Id")))
          OR (a."Estado"='Apurada' AND ((SELECT coalesce(sum(d."Percentual"),0) FROM "DestinosCustoExecucoes" d WHERE d."ApuracaoId"=a."Id")<>100 OR (SELECT coalesce(sum(d."ValorConhecido"),0) FROM "DestinosCustoExecucoes" d WHERE d."ApuracaoId"=a."Id")<>a."ValorConhecido")))
     ) THEN RAISE EXCEPTION 'Execution allocation does not conserve consumed cents' USING ERRCODE='23514'; END IF;
     IF EXISTS(
       SELECT 1 FROM "DestinosCustoExecucoes" d JOIN "ApuracoesCustoExecucoes" a ON a."Id"=d."ApuracaoId" JOIN "ExecucoesProducao" e ON e."Id"=a."ExecucaoId"
       LEFT JOIN "SaidasProducao" s ON s."Id"=d."SaidaProducaoId"
       WHERE (d."Tipo"='Saida' AND d."Quantidade">0 AND (s."Id" IS NULL OR s."ExecucaoId"<>a."ExecucaoId" OR s."Id"<>d."DestinoId" OR s."Quantidade"<>d."Quantidade" OR d."CamadaSaidaId" IS NULL))
         OR (d."Tipo"='Perda' AND (d."PerdaId" IS DISTINCT FROM d."DestinoId" OR NOT EXISTS(SELECT 1 FROM jsonb_array_elements(coalesce(a."SnapshotJson"->'perdas',e."SnapshotJson"->'realizado'->'perdas')) loss WHERE loss->>'id'=d."DestinoId"::text)))
         OR (d."Tipo"='Saida' AND d."Quantidade"=0 AND (d."ValorTotal" IS NOT NULL OR d."Cobertura"<>'NaoAplicavel' OR d."SaidaProducaoId" IS NOT NULL OR d."CamadaSaidaId" IS NOT NULL OR NOT EXISTS(SELECT 1 FROM jsonb_array_elements(e."SnapshotJson"->'realizado'->'saidas') output WHERE output->>'id'=d."DestinoId"::text AND (output->>'quantidade')::numeric=0)))
         OR (d."Percentual"=0 AND (d."ValorConhecido"<>0 OR jsonb_array_length(d."ComponentesJson")<>0))
         OR (d."Percentual">0 AND (jsonb_array_length(d."ComponentesJson")<>jsonb_array_length(a."ComponentesJson") OR EXISTS(
           SELECT 1 FROM jsonb_array_elements(a."ComponentesJson") input WHERE NOT EXISTS(SELECT 1 FROM jsonb_array_elements(d."ComponentesJson") output WHERE output->>'id'=input->>'id'
           AND output->'lacunas'=input->'lacunas' AND (output->>'fracaoNumerador')::numeric*(input->>'fracaoDenominador')::numeric*100=(input->>'fracaoNumerador')::numeric*(output->>'fracaoDenominador')::numeric*d."Percentual"))))
     ) THEN RAISE EXCEPTION 'Invalid allocation destination or inherited coverage' USING ERRCODE='23514'; END IF;
     IF EXISTS(
       SELECT 1 FROM "ApuracoesCustoExecucoes" a JOIN "SaidasProducao" s ON s."ExecucaoId"=a."ExecucaoId"
       WHERE a."Estado"='Apurada' AND NOT EXISTS(
         SELECT 1 FROM "DestinosCustoExecucoes" d WHERE d."ApuracaoId"=a."Id" AND d."SaidaProducaoId"=s."Id")
     ) THEN RAISE EXCEPTION 'Every positive output requires an allocation destination' USING ERRCODE='23514'; END IF;
     IF EXISTS(SELECT 1 FROM "ApuracoesCustoExecucoes" a JOIN "ApuracoesCustoExecucoes" original ON original."Id"=a."ApuracaoOriginalId" WHERE original."Estado"<>'PendenteRateio' OR original."ExecucaoId"<>a."ExecucaoId" OR original."ValorConhecido"<>a."ValorConhecido" OR original."ComponentesJson"<>a."ComponentesJson") THEN
       RAISE EXCEPTION 'Present resolution must preserve original consumed components' USING ERRCODE='23514';END IF;
     IF EXISTS(SELECT 1 FROM "PrecosReferenciaMateriais" WHERE "SubstituiId"="Id") THEN RAISE EXCEPTION 'Reference cannot supersede itself' USING ERRCODE='23514';END IF;
     IF EXISTS(
       WITH shares AS (
        SELECT a."Id" ap,d."Id" destination,d."DestinoId",d."Percentual",input_component->>'id' component,(input_component->>'valorConhecido')::numeric*100 cents,
          trunc((input_component->>'valorConhecido')::numeric*d."Percentual") floor_cents,mod((input_component->>'valorConhecido')::numeric*100*d."Percentual"*1000000,100000000) remainder,
          d."ComponentesJson" components
        FROM "ApuracoesCustoExecucoes" a JOIN "DestinosCustoExecucoes" d ON d."ApuracaoId"=a."Id" CROSS JOIN LATERAL jsonb_array_elements(a."ComponentesJson") input_component
        WHERE a."Estado"='Apurada' AND input_component->>'valorConhecido' IS NOT NULL
       ), ranked AS (
        SELECT *,sum(floor_cents) OVER(PARTITION BY ap,component) floors,row_number() OVER(PARTITION BY ap,component ORDER BY remainder DESC,"DestinoId"::text COLLATE "C") rank FROM shares
       ) SELECT 1 FROM ranked WHERE "Percentual">0 AND NOT EXISTS(SELECT 1 FROM jsonb_array_elements(components) x WHERE x->>'id'=component AND (x->>'valorConhecido')::numeric*100=floor_cents+CASE WHEN rank<=cents-floors THEN 1 ELSE 0 END)
     ) THEN RAISE EXCEPTION 'Largest-remainder allocation or stable tie-break is invalid' USING ERRCODE='23514'; END IF;
     RETURN NULL;
    END $$;CREATE TRIGGER "MaterialCostLock_PrecosReferenciaMateriais" BEFORE INSERT OR UPDATE OR DELETE ON "PrecosReferenciaMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostLock"();
    CREATE CONSTRAINT TRIGGER "MaterialCostConsistency_PrecosReferenciaMateriais" AFTER INSERT OR UPDATE OR DELETE ON "PrecosReferenciaMateriais" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "MaterialCostConsistency"();
    CREATE TRIGGER "MaterialCostLock_EvidenciasCustoMateriais" BEFORE INSERT OR UPDATE OR DELETE ON "EvidenciasCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostLock"();
    CREATE CONSTRAINT TRIGGER "MaterialCostConsistency_EvidenciasCustoMateriais" AFTER INSERT OR UPDATE OR DELETE ON "EvidenciasCustoMateriais" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "MaterialCostConsistency"();
    CREATE TRIGGER "MaterialCostLock_CamadasCustoMateriais" BEFORE INSERT OR UPDATE OR DELETE ON "CamadasCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostLock"();
    CREATE CONSTRAINT TRIGGER "MaterialCostConsistency_CamadasCustoMateriais" AFTER INSERT OR UPDATE OR DELETE ON "CamadasCustoMateriais" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "MaterialCostConsistency"();
    CREATE TRIGGER "MaterialCostLock_EventosCustoMateriais" BEFORE INSERT OR UPDATE OR DELETE ON "EventosCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostLock"();
    CREATE CONSTRAINT TRIGGER "MaterialCostConsistency_EventosCustoMateriais" AFTER INSERT OR UPDATE OR DELETE ON "EventosCustoMateriais" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "MaterialCostConsistency"();
    CREATE TRIGGER "MaterialCostLock_ParcelasCustoMateriais" BEFORE INSERT OR UPDATE OR DELETE ON "ParcelasCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostLock"();
    CREATE CONSTRAINT TRIGGER "MaterialCostConsistency_ParcelasCustoMateriais" AFTER INSERT OR UPDATE OR DELETE ON "ParcelasCustoMateriais" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "MaterialCostConsistency"();
    CREATE TRIGGER "MaterialCostLock_ApuracoesCustoExecucoes" BEFORE INSERT OR UPDATE OR DELETE ON "ApuracoesCustoExecucoes" FOR EACH ROW EXECUTE FUNCTION "MaterialCostLock"();
    CREATE CONSTRAINT TRIGGER "MaterialCostConsistency_ApuracoesCustoExecucoes" AFTER INSERT OR UPDATE OR DELETE ON "ApuracoesCustoExecucoes" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "MaterialCostConsistency"();
    CREATE TRIGGER "MaterialCostLock_DestinosCustoExecucoes" BEFORE INSERT OR UPDATE OR DELETE ON "DestinosCustoExecucoes" FOR EACH ROW EXECUTE FUNCTION "MaterialCostLock"();
    CREATE CONSTRAINT TRIGGER "MaterialCostConsistency_DestinosCustoExecucoes" AFTER INSERT OR UPDATE OR DELETE ON "DestinosCustoExecucoes" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "MaterialCostConsistency"();
    CREATE TRIGGER "MaterialCostLock_ComandosCustoMateriais" BEFORE INSERT OR UPDATE OR DELETE ON "ComandosCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostLock"();
    CREATE CONSTRAINT TRIGGER "MaterialCostConsistency_ComandosCustoMateriais" AFTER INSERT OR UPDATE OR DELETE ON "ComandosCustoMateriais" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "MaterialCostConsistency"();
    CREATE TRIGGER "MaterialCostLock_PosicoesCustoMateriais" BEFORE INSERT OR UPDATE OR DELETE ON "PosicoesCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostLock"();
    CREATE CONSTRAINT TRIGGER "MaterialCostConsistency_PosicoesCustoMateriais" AFTER INSERT OR UPDATE OR DELETE ON "PosicoesCustoMateriais" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "MaterialCostConsistency"();
    CREATE TRIGGER "MaterialCostImmutable_PrecosReferenciaMateriais" BEFORE UPDATE OR DELETE ON "PrecosReferenciaMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostImmutable"();
    CREATE TRIGGER "MaterialCostImmutable_EvidenciasCustoMateriais" BEFORE UPDATE OR DELETE ON "EvidenciasCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostImmutable"();
    CREATE TRIGGER "MaterialCostImmutable_CamadasCustoMateriais" BEFORE UPDATE OR DELETE ON "CamadasCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostImmutable"();
    CREATE TRIGGER "MaterialCostImmutable_EventosCustoMateriais" BEFORE UPDATE OR DELETE ON "EventosCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostImmutable"();
    CREATE TRIGGER "MaterialCostImmutable_ParcelasCustoMateriais" BEFORE UPDATE OR DELETE ON "ParcelasCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostImmutable"();
    CREATE TRIGGER "MaterialCostImmutable_ApuracoesCustoExecucoes" BEFORE UPDATE OR DELETE ON "ApuracoesCustoExecucoes" FOR EACH ROW EXECUTE FUNCTION "MaterialCostImmutable"();
    CREATE TRIGGER "MaterialCostImmutable_DestinosCustoExecucoes" BEFORE UPDATE OR DELETE ON "DestinosCustoExecucoes" FOR EACH ROW EXECUTE FUNCTION "MaterialCostImmutable"();
    CREATE TRIGGER "MaterialCostImmutable_ComandosCustoMateriais" BEFORE UPDATE OR DELETE ON "ComandosCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostImmutable"();
    CREATE TRIGGER "MaterialCostPositionRevision" BEFORE UPDATE OR DELETE ON "PosicoesCustoMateriais" FOR EACH ROW EXECUTE FUNCTION "MaterialCostPositionRevision"();
    CREATE CONSTRAINT TRIGGER "MaterialCostPhysicalRequired" AFTER INSERT ON "MovimentosEstoque" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "MaterialCostPhysicalRequired"();

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103233403_AddMaterialCosts') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261103233403_AddMaterialCosts', '8.0.10');
    END IF;
END $EF$;
COMMIT;

