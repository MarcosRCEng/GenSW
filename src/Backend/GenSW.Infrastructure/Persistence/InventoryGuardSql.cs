namespace GenSW.Infrastructure.Persistence;

internal static class InventoryGuardSql
{
    internal const string Up = """
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
        """;
    internal const string Down = """
        DROP TRIGGER "TR_PosicoesEstoque_Reconcile" ON "PosicoesEstoque";
        DROP TRIGGER "TR_MovimentosEstoque_Reconcile" ON "MovimentosEstoque";
        DROP TRIGGER "TR_EventosEstoque_Cardinality" ON "EventosEstoque";
        DROP TRIGGER "TR_MovimentosEstoque_Cardinality" ON "MovimentosEstoque";
        DROP TRIGGER "TR_MovimentosEstoque_Guard" ON "MovimentosEstoque";
        DROP TRIGGER "TR_PosicoesEstoque_Guard" ON "PosicoesEstoque";
        DROP TRIGGER "TR_LotesMateriais_Guard" ON "LotesMateriais";
        DROP TRIGGER "TR_LotesMateriais_FixedUnit" ON "LotesMateriais";
        DROP TRIGGER "TR_EventosEstoque_Immutable" ON "EventosEstoque";
        DROP TRIGGER "TR_MovimentosEstoque_Immutable" ON "MovimentosEstoque";
        DROP TRIGGER "TR_HistoricoEstoque_Immutable" ON "HistoricoEstoque";
        DROP TRIGGER "TR_ComandosEstoque_Immutable" ON "ComandosEstoque";
        DROP FUNCTION "InventoryReconcileGuard"(); DROP FUNCTION "InventoryEventGuard"(); DROP FUNCTION "InventoryMovementGuard"(); DROP FUNCTION "InventoryPositionGuard"(); DROP FUNCTION "InventoryLotGuard"(); DROP FUNCTION "InventoryFixedItemGuard"(); DROP FUNCTION "InventoryImmutable"();
        """;
}
