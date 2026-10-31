namespace GenSW.Infrastructure.Persistence;

// Extends the installed F01/F02 functions from their canonical sources. Historical migrations stay intact.
internal static class EggBridgeGuardSql
{
    private static string ProductionFunction(string name)
    {
        var sql = ProductionGuardSql.Up;
        var start = sql.IndexOf("CREATE OR REPLACE FUNCTION \"" + name + "\"", StringComparison.Ordinal);
        var end = sql.IndexOf("END $$;", start, StringComparison.Ordinal) + "END $$;".Length;
        return sql[start..end];
    }
    private static string MovementFunction() => ProductionFunction("InventoryMovementGuard")
        .Replace("q:=NEW.\"SnapshotJson\"", """
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
            q:=NEW."SnapshotJson"
            """, StringComparison.Ordinal);
    private static string EventFunction() => ProductionFunction("InventoryEventGuard")
        .Replace("'ReversaoProducaoConsumo') THEN RAISE EXCEPTION 'Unknown", "'ReversaoProducaoConsumo','CompensacaoEntradaOvos') THEN RAISE EXCEPTION 'Unknown", StringComparison.Ordinal)
        .Replace("IF event_type IN ('ProducaoConsumo'", """
            IF event_type='CompensacaoEntradaOvos' THEN
              IF n<>1 OR debits<>1 OR NOT EXISTS(SELECT 1 FROM "TratamentosPonteOvos" t JOIN "GruposPonteOvos" g ON g."Id"=t."GrupoId"
                JOIN "MovimentosEstoque" m ON m."EventoId"=event_id WHERE t."EventoId"=event_id AND t."Tipo"='Compensacao'
                  AND g."Id"::text=m."SnapshotJson" #>> '{f03,grupoId}') THEN
                RAISE EXCEPTION 'Egg compensation requires the exact treatment' USING ERRCODE='23514';
              END IF;
            ELSIF event_type IN ('ProducaoConsumo'
            """, StringComparison.Ordinal);

    internal static string Up => MovementFunction() + "\n" + EventFunction() + "\n" + """
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
        """;
}
