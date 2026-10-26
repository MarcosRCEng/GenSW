namespace GenSW.Infrastructure.Persistence;

// F02 extends the installed F01 functions; the historical F01 migration stays unchanged.
internal static class ProductionGuardSql
{
    private static string ExistingFunction(string name)
    {
        var start = InventoryGuardSql.Up.IndexOf("CREATE FUNCTION \"" + name + "\"", StringComparison.Ordinal);
        var end = InventoryGuardSql.Up.IndexOf("END $$;", start, StringComparison.Ordinal) + "END $$;".Length;
        return InventoryGuardSql.Up[start..end].Replace("CREATE FUNCTION", "CREATE OR REPLACE FUNCTION", StringComparison.Ordinal);
    }

    private static string MovementFunction() => ExistingFunction("InventoryMovementGuard")
        .Replace("even_value numeric;", "even_value numeric; original_row \"MovimentosEstoque\"%ROWTYPE;", StringComparison.Ordinal)
        .Replace("q:=NEW.\"SnapshotJson\"", """
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
            q:=NEW."SnapshotJson"
            """, StringComparison.Ordinal);

    private static string EventFunction() => ExistingFunction("InventoryEventGuard")
        .Replace("'RetornoSegregacao','Descarte') THEN", "'RetornoSegregacao','Descarte','ProducaoConsumo','ProducaoSaida','ReversaoProducaoSaida','ReversaoProducaoConsumo') THEN", StringComparison.Ordinal)
        .Replace("IF event_type IN ('Transferencia'", """
            IF event_type IN ('ProducaoConsumo','ProducaoSaida','ReversaoProducaoSaida','ReversaoProducaoConsumo') THEN
              IF n<>1 OR (event_type IN ('ProducaoConsumo','ReversaoProducaoSaida') AND debits<>1)
                 OR (event_type IN ('ProducaoSaida','ReversaoProducaoConsumo') AND credits<>1)
                 OR NOT EXISTS(SELECT 1 FROM "ExecucaoMovimentos" em JOIN "MovimentosEstoque" m ON m."Id"=em."MovimentoEstoqueId"
                   JOIN "ExecucoesProducao" ex ON ex."Id"=em."ExecucaoId"
                   WHERE m."EventoId"=event_id AND em."ExecucaoId"::text=m."SnapshotJson" #>> '{producao,execucaoId}'
                   AND em."LinhaId"::text=m."SnapshotJson" #>> '{producao,linhaId}'
                   AND em."Papel"=CASE WHEN event_type='ProducaoConsumo' THEN 'Entrada' WHEN event_type='ProducaoSaida' THEN 'Saida' ELSE 'Compensacao' END) THEN
                RAISE EXCEPTION 'Production event requires its execution and exact movement' USING ERRCODE='23514';
              END IF;
            ELSIF event_type IN ('Transferencia'
            """, StringComparison.Ordinal);

    internal static string Up => MovementFunction() + "\n" + EventFunction() + "\n" + """
        CREATE FUNCTION "ProductionReservationGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Reservation history is retained' USING ERRCODE='23514'; END IF;
          IF TG_OP='INSERT' AND NOT EXISTS(SELECT 1 FROM "OrdensProducao" WHERE "Id"=NEW."OrdemId" AND "Estado" IN ('Planejada','Iniciada')) THEN
            RAISE EXCEPTION 'New reservations require an order awaiting physical confirmation' USING ERRCODE='23514';
          END IF;
          IF TG_OP='UPDATE' AND (NEW."Id"<>OLD."Id" OR NEW."OrdemId"<>OLD."OrdemId" OR NEW."LinhaId"<>OLD."LinhaId"
             OR NEW."LoteId"<>OLD."LoteId" OR NEW."LocalId"<>OLD."LocalId" OR NEW."Unidade"<>OLD."Unidade"
             OR NEW."QuantidadeOriginal"<>OLD."QuantidadeOriginal" OR NEW."SnapshotJson"<>OLD."SnapshotJson"
             OR NEW."CreatedAtUtc"<>OLD."CreatedAtUtc" OR NEW."QuantidadeAtiva">OLD."QuantidadeAtiva"
             OR NEW."QuantidadeConsumida"<OLD."QuantidadeConsumida" OR NEW."QuantidadeLiberada"<OLD."QuantidadeLiberada"
             OR NEW."Revisao"<>OLD."Revisao"+1) THEN
            RAISE EXCEPTION 'Reservation identity and settlement history are immutable' USING ERRCODE='23514';
          END IF;
          -- A tuple write serializes even non-cooperating SQL and forces stale RR writers to fail.
          UPDATE "PosicoesEstoque" SET "Quantidade"="Quantidade"
            WHERE "LoteId"=NEW."LoteId" AND "LocalId"=NEW."LocalId" AND "Unidade"=NEW."Unidade";
          IF NOT FOUND THEN RAISE EXCEPTION 'Reservation position or unit is invalid' USING ERRCODE='23514'; END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_ReservasProducao_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "ReservasProducao"
          FOR EACH ROW EXECUTE FUNCTION "ProductionReservationGuard"();

        CREATE FUNCTION "ProductionLotRevisionGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF EXISTS(SELECT 1 FROM "SaidasProducao" WHERE "LoteId"=OLD."Id") AND
            (NEW."Revisao"<>OLD."Revisao"+1 OR NEW."Origem"<>OLD."Origem" OR NEW."Fonte"<>OLD."Fonte") THEN
            RAISE EXCEPTION 'Produced lot changes require a new revision and preserve production identity' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_LotesMateriais_ProductionRevision" BEFORE UPDATE ON "LotesMateriais"
          FOR EACH ROW EXECUTE FUNCTION "ProductionLotRevisionGuard"();

        CREATE FUNCTION "ProductionReservationBalance"() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE physical numeric; reserved numeric;
        BEGIN
          SELECT "Quantidade" INTO physical FROM "PosicoesEstoque" WHERE "LoteId"=NEW."LoteId" AND "LocalId"=NEW."LocalId";
          SELECT coalesce(sum("QuantidadeAtiva"),0) INTO reserved FROM "ReservasProducao" WHERE "LoteId"=NEW."LoteId" AND "LocalId"=NEW."LocalId";
          IF physical IS NULL OR reserved>physical THEN RAISE EXCEPTION 'Reserved stock exceeds physical stock' USING ERRCODE='23514'; END IF;
          IF EXISTS(SELECT 1 FROM "ReservasProducao" r JOIN "OrdensProducao" o ON o."Id"=r."OrdemId"
              WHERE r."LoteId"=NEW."LoteId" AND r."LocalId"=NEW."LocalId" AND r."QuantidadeAtiva">0 AND o."Estado"<>'Iniciada') THEN
            RAISE EXCEPTION 'Only started orders retain active reservations' USING ERRCODE='23514';
          END IF;
          RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER "TR_ReservasProducao_Balance" AFTER INSERT OR UPDATE ON "ReservasProducao"
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionReservationBalance"();
        CREATE CONSTRAINT TRIGGER "TR_PosicoesEstoque_Reservations" AFTER INSERT OR UPDATE ON "PosicoesEstoque"
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionReservationBalance"();

        CREATE FUNCTION "ProductionOrderGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Order history is retained' USING ERRCODE='23514'; END IF;
          IF TG_OP='UPDATE' THEN
            IF NEW."Id"<>OLD."Id" OR NEW."AutorId"<>OLD."AutorId" OR NEW."CreatedAtUtc"<>OLD."CreatedAtUtc" OR NEW."Revisao"<>OLD."Revisao"+1 THEN
              RAISE EXCEPTION 'Order identity or revision is invalid' USING ERRCODE='23514';
            END IF;
            IF OLD."Estado"<>'Rascunho' AND (NEW."Codigo"<>OLD."Codigo" OR NEW."ReceitaVersaoId"<>OLD."ReceitaVersaoId"
              OR NEW."Tamanho"<>OLD."Tamanho" OR NEW."Unidade"<>OLD."Unidade" OR NEW."ResponsavelId"<>OLD."ResponsavelId"
              OR NEW."PropriedadeId" IS DISTINCT FROM OLD."PropriedadeId" OR NEW."PropriedadeNome" IS DISTINCT FROM OLD."PropriedadeNome") THEN
              RAISE EXCEPTION 'A planned or started order cannot change its recipe or identity' USING ERRCODE='23514';
            END IF;
            IF OLD."Estado" IN ('Iniciada','Confirmada','Revertida','AbortadaSemConsumo','Cancelada') AND
              (NEW."SnapshotPlanejadoJson" IS DISTINCT FROM OLD."SnapshotPlanejadoJson" OR NEW."PlanoHash" IS DISTINCT FROM OLD."PlanoHash") THEN
              RAISE EXCEPTION 'Execution planning snapshot is immutable' USING ERRCODE='23514';
            END IF;
            IF OLD."Estado"='Planejada' AND NEW."Estado"<>'Rascunho' AND
              (NEW."SnapshotPlanejadoJson" IS DISTINCT FROM OLD."SnapshotPlanejadoJson" OR NEW."PlanoHash" IS DISTINCT FROM OLD."PlanoHash") THEN
              RAISE EXCEPTION 'A published plan requires an explicit new planning revision' USING ERRCODE='23514';
            END IF;
            IF NEW."Estado"<>OLD."Estado" AND NOT (
              (OLD."Estado"='Rascunho' AND NEW."Estado" IN ('Planejada','Cancelada')) OR
              (OLD."Estado"='Planejada' AND NEW."Estado" IN ('Rascunho','Iniciada','Cancelada')) OR
              (OLD."Estado"='Iniciada' AND NEW."Estado" IN ('Confirmada','AbortadaSemConsumo')) OR
              (OLD."Estado"='Confirmada' AND NEW."Estado"='Revertida')) THEN
              RAISE EXCEPTION 'Invalid order transition' USING ERRCODE='23514';
            END IF;
          END IF;
          IF NEW."Estado" IN ('Planejada','Iniciada','Confirmada','Revertida','AbortadaSemConsumo') AND
             (NEW."SnapshotPlanejadoJson" IS NULL OR NEW."PlanoHash" IS NULL OR length(NEW."PlanoHash")<>64) THEN
            RAISE EXCEPTION 'A planned order needs its immutable snapshot' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_OrdensProducao_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "OrdensProducao"
          FOR EACH ROW EXECUTE FUNCTION "ProductionOrderGuard"();

        CREATE FUNCTION "ProductionOrderConsistency"() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE order_id uuid; state text; execution_count integer; active_count integer; reversal_count integer;
        BEGIN
          IF TG_TABLE_NAME='OrdensProducao' THEN order_id:=NEW."Id";
          ELSIF TG_TABLE_NAME IN ('ExecucaoMovimentos','ReversoesProducao') THEN
            SELECT "OrdemId" INTO order_id FROM "ExecucoesProducao" WHERE "Id"=NEW."ExecucaoId";
          ELSE order_id:=NEW."OrdemId"; END IF;
          SELECT "Estado" INTO state FROM "OrdensProducao" WHERE "Id"=order_id;
          SELECT count(*) INTO active_count FROM "ReservasProducao" WHERE "OrdemId"=order_id AND "QuantidadeAtiva">0;
          SELECT count(*) INTO execution_count FROM "ExecucoesProducao" WHERE "OrdemId"=order_id;
          SELECT count(*) INTO reversal_count FROM "ReversoesProducao" r JOIN "ExecucoesProducao" e ON e."Id"=r."ExecucaoId" WHERE e."OrdemId"=order_id;
          IF (state='Iniciada' AND active_count=0) OR (state<>'Iniciada' AND active_count<>0)
             OR (state IN ('Confirmada','Revertida') AND execution_count<>1)
             OR (state NOT IN ('Confirmada','Revertida') AND execution_count<>0)
             OR (state='Revertida' AND reversal_count<>1) OR (state<>'Revertida' AND reversal_count<>0) THEN
            RAISE EXCEPTION 'Order, reservations, execution and reversal disagree' USING ERRCODE='23514';
          END IF;
          IF execution_count=0 AND EXISTS(SELECT 1 FROM "ReservasProducao" WHERE "OrdemId"=order_id AND "QuantidadeConsumida"<>0) THEN
            RAISE EXCEPTION 'Consumed reservations require a confirmed physical execution' USING ERRCODE='23514';
          END IF;
          IF execution_count=1 AND EXISTS(
            WITH reserved AS (
              SELECT "LinhaId","LoteId","LocalId",sum("QuantidadeConsumida") AS quantity
              FROM "ReservasProducao" WHERE "OrdemId"=order_id GROUP BY "LinhaId","LoteId","LocalId"),
            consumed AS (
              SELECT em."LinhaId",m."LoteId",m."LocalId",sum(m."Quantidade") AS quantity
              FROM "ExecucaoMovimentos" em JOIN "ExecucoesProducao" e ON e."Id"=em."ExecucaoId"
              JOIN "MovimentosEstoque" m ON m."Id"=em."MovimentoEstoqueId"
              WHERE e."OrdemId"=order_id AND em."Papel"='Entrada' GROUP BY em."LinhaId",m."LoteId",m."LocalId")
            SELECT 1 FROM reserved r FULL JOIN consumed c USING ("LinhaId","LoteId","LocalId")
            WHERE coalesce(r.quantity,0)<>coalesce(c.quantity,0)) THEN
            RAISE EXCEPTION 'Reservation settlement must equal every physical consumption line' USING ERRCODE='23514';
          END IF;
          RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER "TR_OrdensProducao_Consistency" AFTER INSERT OR UPDATE ON "OrdensProducao"
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionOrderConsistency"();
        CREATE CONSTRAINT TRIGGER "TR_ExecucoesProducao_Order" AFTER INSERT ON "ExecucoesProducao"
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionOrderConsistency"();
        CREATE CONSTRAINT TRIGGER "TR_ReservasProducao_Order" AFTER INSERT OR UPDATE ON "ReservasProducao"
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionOrderConsistency"();
        CREATE CONSTRAINT TRIGGER "TR_ExecucaoMovimentos_Order" AFTER INSERT ON "ExecucaoMovimentos"
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionOrderConsistency"();
        CREATE CONSTRAINT TRIGGER "TR_ReversoesProducao_Order" AFTER INSERT ON "ReversoesProducao"
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionOrderConsistency"();

        CREATE FUNCTION "ProductionMovementMembershipGuard"() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE state text;
        BEGIN
          SELECT o."Estado" INTO state FROM "ExecucoesProducao" e JOIN "OrdensProducao" o ON o."Id"=e."OrdemId"
            WHERE e."Id"=NEW."ExecucaoId";
          IF (NEW."Papel" IN ('Entrada','Saida') AND state IS DISTINCT FROM 'Iniciada')
             OR (NEW."Papel"='Compensacao' AND state IS DISTINCT FROM 'Confirmada') THEN
            RAISE EXCEPTION 'A committed execution cannot acquire additional physical facts' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_ExecucaoMovimentos_Membership" BEFORE INSERT ON "ExecucaoMovimentos"
          FOR EACH ROW EXECUTE FUNCTION "ProductionMovementMembershipGuard"();

        CREATE FUNCTION "ProductionExecutionConsistency"() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE execution_id uuid; result text;
        BEGIN
          IF TG_TABLE_NAME='ExecucoesProducao' THEN execution_id:=NEW."Id"; ELSE execution_id:=NEW."ExecucaoId"; END IF;
          SELECT "Resultado" INTO result FROM "ExecucoesProducao" WHERE "Id"=execution_id;
          IF NOT EXISTS(SELECT 1 FROM "ExecucaoMovimentos" WHERE "ExecucaoId"=execution_id AND "Papel"='Entrada') THEN
            RAISE EXCEPTION 'An execution requires physical consumption' USING ERRCODE='23514';
          END IF;
          IF EXISTS(SELECT 1 FROM "ExecucaoMovimentos" em JOIN "MovimentosEstoque" m ON m."Id"=em."MovimentoEstoqueId"
              WHERE em."ExecucaoId"=execution_id AND (
                (em."Papel"='Entrada' AND m."TipoEvento"<>'ProducaoConsumo') OR
                (em."Papel"='Saida' AND m."TipoEvento"<>'ProducaoSaida') OR
                (em."Papel"='Compensacao' AND m."TipoEvento" NOT IN ('ReversaoProducaoSaida','ReversaoProducaoConsumo')))) THEN
            RAISE EXCEPTION 'Execution movement role is invalid' USING ERRCODE='23514';
          END IF;
          IF EXISTS(SELECT 1 FROM "SaidasProducao" s WHERE s."ExecucaoId"=execution_id AND NOT EXISTS(
              SELECT 1 FROM "ExecucaoMovimentos" em JOIN "MovimentosEstoque" m ON m."Id"=em."MovimentoEstoqueId"
              WHERE em."ExecucaoId"=execution_id AND em."Papel"='Saida' AND em."LinhaId"=s."LinhaId"
                AND m."EventoId"=s."EventoId" AND m."LoteId"=s."LoteId" AND m."LocalId"=s."LocalId" AND m."Quantidade"=s."Quantidade"))
             OR EXISTS(SELECT 1 FROM "ExecucaoMovimentos" em JOIN "MovimentosEstoque" m ON m."Id"=em."MovimentoEstoqueId"
              WHERE em."ExecucaoId"=execution_id AND em."Papel"='Saida' AND NOT EXISTS(
                SELECT 1 FROM "SaidasProducao" s WHERE s."ExecucaoId"=execution_id AND s."LoteId"=m."LoteId" AND s."EventoId"=m."EventoId")) THEN
            RAISE EXCEPTION 'Production outputs disagree with physical credits' USING ERRCODE='23514';
          END IF;
          IF result='PerdaTotal' AND EXISTS(SELECT 1 FROM "SaidasProducao" WHERE "ExecucaoId"=execution_id) THEN
            RAISE EXCEPTION 'Total loss cannot create an output lot' USING ERRCODE='23514';
          END IF;
          IF NOT EXISTS(SELECT 1 FROM "ReversoesProducao" WHERE "ExecucaoId"=execution_id) AND EXISTS(
            SELECT 1 FROM "SaidasProducao" s JOIN "LotesMateriais" l ON l."Id"=s."LoteId"
            WHERE s."ExecucaoId"=execution_id AND (s."RevisaoLoteOriginal"<>l."Revisao"
              OR s."SituacaoOriginal"<>l."Situacao" OR s."ValidadeOriginal" IS DISTINCT FROM l."Validade")) THEN
            RAISE EXCEPTION 'Output snapshot must retain the real lot identity at confirmation' USING ERRCODE='23514';
          END IF;
          IF result IN ('FatoApto','ConsumoIrregular') AND EXISTS(
            SELECT 1 FROM "SaidasProducao" s JOIN "LocaisEstoque" l ON l."Id"=s."LocalId"
            WHERE s."ExecucaoId"=execution_id AND (s."SituacaoOriginal"<>'Bloqueado' OR l."Finalidade"<>'Segregacao')) THEN
            RAISE EXCEPTION 'Exceptional outputs must be blocked and segregated' USING ERRCODE='23514';
          END IF;
          IF EXISTS(SELECT 1 FROM "SaidasProducao" s JOIN "MovimentosEstoque" m ON m."EventoId"=s."EventoId" AND m."LoteId"=s."LoteId"
            JOIN "EventosEstoque" e ON e."Id"=m."EventoId" WHERE s."ExecucaoId"=execution_id AND (m."SaldoAnterior"<>0 OR EXISTS(
              SELECT 1 FROM "MovimentosEstoque" prior JOIN "EventosEstoque" pe ON pe."Id"=prior."EventoId"
              WHERE prior."LoteId"=s."LoteId" AND pe."Sequencia"<e."Sequencia"))) THEN
            RAISE EXCEPTION 'Production output requires a new untouched lot' USING ERRCODE='23514';
          END IF;
          IF EXISTS(SELECT 1 FROM "GenealogiaMateriais" g WHERE g."ExecucaoId"=execution_id AND
            (g."LoteEntradaId"=g."LoteSaidaId" OR NOT EXISTS(
              SELECT 1 FROM "ExecucaoMovimentos" em JOIN "MovimentosEstoque" m ON m."Id"=em."MovimentoEstoqueId"
              WHERE em."ExecucaoId"=execution_id AND em."Papel"='Entrada' AND m."LoteId"=g."LoteEntradaId") OR NOT EXISTS(
              SELECT 1 FROM "SaidasProducao" s WHERE s."ExecucaoId"=execution_id AND s."LoteId"=g."LoteSaidaId"))) THEN
            RAISE EXCEPTION 'Genealogy must link the actual physical inputs and outputs' USING ERRCODE='23514';
          END IF;
          IF EXISTS(SELECT 1 FROM "ExecucaoMovimentos" em JOIN "MovimentosEstoque" m ON m."Id"=em."MovimentoEstoqueId"
            CROSS JOIN "SaidasProducao" s WHERE em."ExecucaoId"=execution_id AND em."Papel"='Entrada' AND s."ExecucaoId"=execution_id
            AND NOT EXISTS(SELECT 1 FROM "GenealogiaMateriais" g WHERE g."ExecucaoId"=execution_id AND g."LoteEntradaId"=m."LoteId" AND g."LoteSaidaId"=s."LoteId")) THEN
            RAISE EXCEPTION 'Genealogy must retain every physical input to output link' USING ERRCODE='23514';
          END IF;
          IF EXISTS(WITH RECURSIVE reach(origin,current_lot) AS (
            SELECT "LoteEntradaId","LoteSaidaId" FROM "GenealogiaMateriais" WHERE "ExecucaoId"=execution_id
            UNION
            SELECT r.origin,g."LoteSaidaId" FROM reach r JOIN "GenealogiaMateriais" g ON g."LoteEntradaId"=r.current_lot)
            SELECT 1 FROM reach WHERE origin=current_lot) THEN
            RAISE EXCEPTION 'Physical genealogy cannot contain a cycle' USING ERRCODE='23514';
          END IF;
          IF NOT EXISTS(SELECT 1 FROM "ReversoesProducao" WHERE "ExecucaoId"=execution_id) AND
            EXISTS(SELECT 1 FROM "ExecucaoMovimentos" WHERE "ExecucaoId"=execution_id AND "Papel"='Compensacao') THEN
            RAISE EXCEPTION 'Compensation requires an explicit complete reversal' USING ERRCODE='23514';
          END IF;
          IF EXISTS(SELECT 1 FROM "ReversoesProducao" WHERE "ExecucaoId"=execution_id) AND EXISTS(
            SELECT 1 FROM "ExecucaoMovimentos" em WHERE em."ExecucaoId"=execution_id AND em."Papel" IN ('Entrada','Saida')
              AND (SELECT count(*) FROM "ExecucaoMovimentos" c JOIN "MovimentosEstoque" m ON m."Id"=c."MovimentoEstoqueId"
                WHERE c."ExecucaoId"=execution_id AND c."Papel"='Compensacao'
                  AND m."SnapshotJson" #>> '{compensacao,movimentoOriginalId}'=em."MovimentoEstoqueId"::text)<>1) THEN
            RAISE EXCEPTION 'Reversal must compensate every original movement exactly once' USING ERRCODE='23514';
          END IF;
          IF EXISTS(SELECT 1 FROM "ReversoesProducao" WHERE "ExecucaoId"=execution_id) AND EXISTS(
            SELECT 1 FROM "SaidasProducao" s JOIN "LotesMateriais" l ON l."Id"=s."LoteId" WHERE s."ExecucaoId"=execution_id AND (
              l."Situacao"<>'Encerrado' OR l."Revisao"<>s."RevisaoLoteOriginal"+1 OR EXISTS(
                SELECT 1 FROM "ReservasProducao" r WHERE r."LoteId"=s."LoteId") OR EXISTS(
                SELECT 1 FROM "MovimentosEstoque" m WHERE m."LoteId"=s."LoteId" AND m."EventoId"<>s."EventoId" AND NOT EXISTS(
                  SELECT 1 FROM "ExecucaoMovimentos" em WHERE em."ExecucaoId"=execution_id AND em."Papel"='Compensacao' AND em."MovimentoEstoqueId"=m."Id")))) THEN
            RAISE EXCEPTION 'Reversal cannot cross any later output use or alteration' USING ERRCODE='23514';
          END IF;
          RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER "TR_ExecucoesProducao_Consistency" AFTER INSERT ON "ExecucoesProducao" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionExecutionConsistency"();
        CREATE CONSTRAINT TRIGGER "TR_ExecucaoMovimentos_Consistency" AFTER INSERT ON "ExecucaoMovimentos" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionExecutionConsistency"();
        CREATE CONSTRAINT TRIGGER "TR_SaidasProducao_Consistency" AFTER INSERT ON "SaidasProducao" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionExecutionConsistency"();
        CREATE CONSTRAINT TRIGGER "TR_GenealogiaMateriais_Consistency" AFTER INSERT ON "GenealogiaMateriais" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionExecutionConsistency"();
        CREATE CONSTRAINT TRIGGER "TR_ReversoesProducao_Consistency" AFTER INSERT ON "ReversoesProducao" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ProductionExecutionConsistency"();

        CREATE TRIGGER "TR_ExecucoesProducao_Immutable" BEFORE UPDATE OR DELETE ON "ExecucoesProducao" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
        CREATE TRIGGER "TR_ExecucaoMovimentos_Immutable" BEFORE UPDATE OR DELETE ON "ExecucaoMovimentos" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
        CREATE TRIGGER "TR_SaidasProducao_Immutable" BEFORE UPDATE OR DELETE ON "SaidasProducao" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
        CREATE TRIGGER "TR_GenealogiaMateriais_Immutable" BEFORE UPDATE OR DELETE ON "GenealogiaMateriais" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
        CREATE TRIGGER "TR_HistoricoProducao_Immutable" BEFORE UPDATE OR DELETE ON "HistoricoProducao" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
        CREATE TRIGGER "TR_OcorrenciasProducao_Immutable" BEFORE UPDATE OR DELETE ON "OcorrenciasProducao" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
        CREATE TRIGGER "TR_ComandosProducao_Immutable" BEFORE UPDATE OR DELETE ON "ComandosProducao" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
        CREATE TRIGGER "TR_ReversoesProducao_Immutable" BEFORE UPDATE OR DELETE ON "ReversoesProducao" FOR EACH ROW EXECUTE FUNCTION "InventoryImmutable"();
        """;
}
