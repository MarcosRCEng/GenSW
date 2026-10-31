using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenSW.Application.Authentication;
using GenSW.Application.Inventory;
using GenSW.Domain.Animals;
using GenSW.Domain.Catalog;
using GenSW.Domain.EggBridge;
using GenSW.Domain.Inventory;

namespace GenSW.Application.EggBridge;

public sealed class EggBridgeService(IEggBridgeRepository repository, IInventoryRepository inventory,
    IAuthenticationSessionService authentication, IEggBridgePreviewProtector protector, TimeProvider clock)
{
    public const string Algorithm = "gensw-egg-bridge/1.0";
    public const int SelectionLimit = 100;
    private DateTimeOffset Now
    {
        get
        {
            var instant = clock.GetUtcNow().ToUniversalTime();
            return new DateTimeOffset(instant.Ticks - instant.Ticks % 10, TimeSpan.Zero);
        }
    }
    private DateOnly Today => InventoryRules.Today(clock);
    private static EggBridgeException Missing() => new(404, "registro_nao_encontrado", "Registro não encontrado.");
    private static void Conflict(bool condition, string code, string message)
    { if (condition) throw new EggBridgeException(409, code, message); }
    private static void Admin(bool allowed)
    { if (!allowed) throw new EggBridgeException(403, "admin_necessario", "Operação exige papel Admin atual."); }
    private static string Text(string? value, string label = "Motivo") => CatalogRules.Text(value, 2000, label);
    private async Task<bool> Authorize(Guid actor, bool admin, CancellationToken ct)
    {
        var user = await authentication.GetCurrentUserAsync(actor, ct)
            ?? throw new EggBridgeException(401, "sessao_invalida", "Usuário inativo. Entre novamente.");
        var allowed = user.Roles.Contains("Admin", StringComparer.Ordinal);
        if (admin) Admin(allowed);
        return allowed;
    }
    private async Task Responsible(Guid id, bool tracking, CancellationToken ct)
    {
        InventoryRules.Id(id);
        var user = await inventory.ResponsibleAsync(id, tracking, ct) ?? throw Missing();
        Conflict(!user.Ativo, "responsavel_inativo", "Escolha responsável ativo para o novo fato.");
    }
    private async Task<T> Read<T>(Guid actor, bool admin, Func<Task<T>> action, CancellationToken ct)
    {
        await using var scope = await repository.BeginReadAsync(ct);
        await Authorize(actor, admin, ct);
        var result = await action();
        await scope.CommitAsync(ct);
        return result;
    }
    public static string Hash<T>(T value)
    {
        using var doc = JsonDocument.Parse(InventoryJson.Write(value));
        static string Canonical(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(",", e.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal)
                .Select(x => JsonSerializer.Serialize(x.Name) + ":" + Canonical(x.Value))) + "}",
            JsonValueKind.Array => "[" + string.Join(",", e.EnumerateArray().Select(Canonical)) + "]",
            _ => e.GetRawText()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(doc.RootElement))));
    }
    public static string SourceSnapshot(ProducaoOvo source) => InventoryJson.Write(new
    {
        ProducaoOvoId = source.Id, source.AnimalId, source.DataPostura,
        PesoGramas = source.PesoGramas.ToString("0.00", CultureInfo.InvariantCulture), source.Observacao,
        UpdatedAtUtc = source.UpdatedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
        Algoritmo = Algorithm
    });
    public static string SourceHash(ProducaoOvo source)
    {
        // Hash the versioned field tuple, not a JSON renderer's whitespace/escaping choices.
        // Base64 preserves every observation byte and distinguishes null (N) from empty (S).
        var canonical = string.Join("\n", Algorithm, source.Id.ToString("D"), source.AnimalId.ToString("D"),
            source.DataPostura.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            source.PesoGramas.ToString("0.00", CultureInfo.InvariantCulture),
            source.UpdatedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
            source.Observacao is null ? "N" : "S" + Convert.ToBase64String(Encoding.UTF8.GetBytes(source.Observacao)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
    private sealed record Mutation(object Body, int Status = 201, string? Location = null, Guid? Group = null);
    private async Task<EggMutationResult> Mutate<T>(string operation, T command, string key, Guid actor, bool admin,
        Func<Task<Mutation>> action, CancellationToken ct, bool productionLock = false)
    {
        ArgumentNullException.ThrowIfNull(command);
        key = CatalogRules.Text(key, 100, "Idempotency-Key");
        var hash = Hash(command);
        await using var transaction = await repository.BeginMutationAsync(productionLock, ct);
        try
        {
            var isAdmin = await Authorize(actor, admin, ct);
            if (await repository.ReplayAsync(operation, key, ct) is { } replay)
            {
                if (replay.ExigeAdmin) Admin(isAdmin);
                Conflict(replay.HashPayload != hash, "idempotencia_divergente", "Chave já utilizada com conteúdo diferente.");
                await transaction.CommitAsync(ct);
                return new(replay.StatusHttp, replay.Location, replay.RespostaJson, true);
            }
            var date = Today;
            var result = await action();
            await repository.SaveAsync(ct);
            var body = InventoryJson.Write(result.Body);
            repository.Add(new ComandoPonteOvos(Guid.NewGuid(), operation, key, hash, actor, result.Group, admin,
                result.Status, result.Location, body, Now));
            await repository.SaveAsync(ct);
            Conflict(Today != date, "data_operacional_alterada", "Data operacional mudou. Confira nova prévia.");
            await transaction.CommitAsync(ct);
            return new(result.Status, result.Location, body, false);
        }
        catch { repository.ClearTracking(); throw; }
    }
    public Task<EggBridgePage<EggCandidate>> CandidatesAsync(EggBridgeQuery q, Guid actor, CancellationToken ct) =>
        Read(actor, false, () => { q.Validate(); return repository.CandidatesAsync(q, ct); }, ct);
    public Task<EggBridgePage<GrupoPonteOvos>> GroupsAsync(EggBridgeQuery q, Guid actor, CancellationToken ct) =>
        Read(actor, false, () => { q.Validate(); return repository.GroupsAsync(q, ct); }, ct);
    public Task<AncoraConciliacaoOvos> EnrollmentAsync(Guid item, Guid actor, CancellationToken ct) =>
        Read(actor, false, async () => await repository.AnchorAsync(item, false, ct) ?? throw Missing(), ct);
    public Task<EggBridgePage<RecorteConciliacaoOvos>> ScopesAsync(EggBridgeQuery q, Guid actor, CancellationToken ct) =>
        Read(actor, false, () => { q.Validate(); return repository.ScopesAsync(q, ct); }, ct);
    public Task<EggBridgePage<ProvaEntradaOvos>> ProofsAsync(EggBridgeQuery q, Guid actor, CancellationToken ct) =>
        Read(actor, false, () => { q.Validate(); return repository.ProofsAsync(q, ct); }, ct);
    public Task<RecorteConciliacaoOvos> ScopeAsync(Guid id, Guid actor, CancellationToken ct) =>
        Read(actor, false, async () => await repository.ScopeAsync(id, ct) ?? throw Missing(), ct);
    public Task<ProvaEntradaOvos> ProofAsync(Guid id, Guid actor, CancellationToken ct) =>
        Read(actor, false, async () => await repository.ProofAsync(id, false, ct) ?? throw Missing(), ct);
    public Task<EggBridgeDetail> GetAsync(Guid id, Guid actor, CancellationToken ct) =>
        Read(actor, false, () => Detail(id, ct), ct);
    private async Task<EggBridgeDetail> Detail(Guid id, CancellationToken ct)
    {
        var group = await repository.GroupAsync(id, ct) ?? throw Missing();
        var bridges = await repository.GroupBridgesAsync(id, ct);
        var sources = (await repository.SourcesAsync(bridges.Select(x => x.ProducaoOvoId).ToArray(), false, ct)).ToDictionary(x => x.Id);
        var current = bridges.Select(x =>
        {
            sources.TryGetValue(x.ProducaoOvoId, out var source);
            return new EggSourceExpectation(x.ProducaoOvoId, source is null ? "" : SourceHash(source), source?.UpdatedAtUtc ?? default,
                "Historico", source is null ? "Origem ausente: preserve a evidência." : SourceHash(source) != x.HashOrigem || source.UpdatedAtUtc != x.OrigemUpdatedAtUtc
                    ? "Origem zootécnica alterada depois da ponte; saldo e snapshot preservados." : null);
        }).ToArray();
        return new(group, bridges, await repository.TreatmentsAsync(id, ct), current, await repository.ReconcileAsync(id, ct),
            ["A associação histórica não comprova disponibilidade atual nem qual ovo foi consumido no lote.",
             "Compensação ou invalidação não libera identidade ou capacidade para reentrada."]);
    }
    public Task<EggMutationResult> EnrollAsync(EggEnrollmentCommand c, string key, Guid actor, CancellationToken ct) =>
        Mutate("Inscricao", c, key, actor, true, async () =>
        {
            InventoryRules.Id(c.ItemId); Text(c.Motivo);
            var item = await inventory.ItemAsync(c.ItemId, true, ct) ?? throw Missing();
            Conflict(item.Unidade != "un" || !item.Ativo || !item.PodeEntrar, "item_inelegivel", "Inscrição exige item ativo de contagem un com capacidade de entrada.");
            Conflict(await repository.AnchorAsync(item.Id, true, ct) is not null, "item_ja_gerido", "Item já está explicitamente gerido por F03.");
            var anchor = new AncoraConciliacaoOvos(Guid.NewGuid(), item.Id, 0, actor, Now);
            repository.Add(anchor);
            await repository.SaveAsync(ct);
            var persisted = await repository.AnchorAsync(item.Id, false, ct) ?? throw Missing();
            return new(persisted, Location: $"/api/v1/producao/ponte-ovos/itens/{item.Id}");
        }, ct);
    private static IReadOnlyList<Guid> SelectionIds(IReadOnlyList<EggSelection> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Count is < 1 or > SelectionLimit) throw new ArgumentException($"Escolha entre 1 e {SelectionLimit} registros explicitamente.");
        foreach (var row in selection) { ArgumentNullException.ThrowIfNull(row); InventoryRules.Id(row.ProducaoOvoId); }
        var ids = selection.Select(x => x.ProducaoOvoId).Order().ToArray();
        if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("Seleção contém IDs repetidos.");
        return ids;
    }
    public Task<EggMutationResult> ApproveScopeAsync(EggScopeCommand c, string key, Guid actor, CancellationToken ct) =>
        Mutate("Recorte", c, key, actor, true, async () =>
        {
            InventoryRules.Id(c.ItemId); Text(c.Fronteira, "Fronteira"); Text(c.FontesConsultadas, "Fontes consultadas");
            Text(c.Motivo); CatalogRules.Optional(c.Lacunas); await Responsible(c.ResponsavelId, true, ct);
            ArgumentNullException.ThrowIfNull(c.Decisoes);
            var ids = SelectionIds(c.Decisoes.Select(x => new EggSelection(x.ProducaoOvoId)).ToArray());
            await inventory.ItemAsync(c.ItemId, true, ct);
            var anchor = await repository.AnchorAsync(c.ItemId, true, ct) ?? throw Missing();
            var sources = (await repository.SourcesAsync(ids, true, ct)).ToDictionary(x => x.Id);
            if (sources.Count != ids.Count) throw Missing();
            var scope = new RecorteConciliacaoOvos(Guid.NewGuid(), c.ItemId, anchor.Revisao,
                await inventory.LastSequenceAsync(ct), c.Fronteira, c.FontesConsultadas, c.Lacunas, c.Motivo, actor, c.ResponsavelId, Now);
            repository.Add(scope);
            foreach (var row in c.Decisoes)
            {
                if (row.Decisao is not ("NaoRecebidoComprovado" or "RecebidoComEquivalencia" or "Pendente")) throw new ArgumentException("Decisão de conciliação inválida.");
                Text(row.Evidencia, "Evidência");
                if (row.Decisao != "Pendente" && !row.IndividualComprovado) throw new ArgumentException("A decisão exige comprovação humana de ovo individual.");
                var source = sources[row.ProducaoOvoId];
                repository.Add(new DecisaoConciliacaoOvo(Guid.NewGuid(), scope.Id, source.Id, row.Decisao,
                    row.IndividualComprovado, row.Evidencia, SourceHash(source), source.UpdatedAtUtc));
            }
            return new(new { Recorte = scope, Decisoes = c.Decisoes });
        }, ct);
    private static long Count(string value)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "^[1-9][0-9]{0,13}$") ||
            !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)) throw new ArgumentException("Contagem/ordinal exige inteiro positivo de até 14 dígitos.");
        return count;
    }
    private async Task<MovimentoEstoque> EligibleMovement(Guid id, CancellationToken ct)
    {
        var movement = await repository.MovementAsync(id, ct) ?? throw Missing();
        var ev = await repository.EventAsync(movement.EventoId, ct) ?? throw Missing();
        Conflict(ev.Tipo is not ("Entrada" or "Abertura") || movement.Sentido != "Entrada" ||
            movement.Unidade != "un" || movement.UnidadeDeclarada != "un" ||
            movement.Quantidade != CatalogRules.Decimal(movement.QuantidadeDeclarada, true) ||
            await repository.IsBridgeLotAsync(movement.LoteId, ct), "entrada_manual_inelegivel",
            "A prova exige Entrada/Abertura manual original contada em un, sem contagem derivada de massa e fora de lote raiz F03.");
        return movement;
    }
    public Task<EggMutationResult> ApproveProofAsync(EggProofCommand c, string key, Guid actor, CancellationToken ct) =>
        Mutate("Prova", c, key, actor, true, async () =>
        {
            Text(c.Motivo); Text(c.Evidencia, "Evidência"); await Responsible(c.ResponsavelId, true, ct);
            var count = Count(c.Contagem);
            var movement = await EligibleMovement(c.MovimentoEstoqueId, ct);
            await inventory.ItemAsync(movement.ItemId, true, ct);
            _ = await repository.AnchorAsync(movement.ItemId, true, ct) ?? throw Missing();
            Conflict(count > movement.Quantidade, "capacidade_excedida", "Contagem comprovada excede a quantidade original declarada em un.");
            Conflict(await repository.ProofByMovementAsync(movement.Id, ct) is not null, "prova_existente", "Movimento já possui prova vitalícia.");
            var proof = new ProvaEntradaOvos(Guid.NewGuid(), movement.Id, movement.EventoId, movement.ItemId, count,
                c.Evidencia, c.Motivo, actor, c.ResponsavelId, Now);
            repository.Add(proof);
            return new(proof);
        }, ct);

    private sealed record Prepared(EggPreviewCommand Command, RecorteConciliacaoOvos Scope, AncoraConciliacaoOvos Anchor,
        Item Item, LoteMaterial Lot, LocalEstoque Local, PosicaoEstoque Position, ProvaEntradaOvos? Proof,
        MovimentoEstoque? Original, IReadOnlyList<ProducaoOvo> Sources, IReadOnlyList<EggSourceExpectation> Expectations,
        long Remaining, long Cut, bool BlockLot, string Hash, IReadOnlyList<string> Warnings);
    private async Task<Prepared> Prepare(EggPreviewCommand c, bool tracking, CancellationToken ct)
    {
        var ids = SelectionIds(c.Selecao);
        if (c.Operacao is not ("Entrada" or "Associacao")) throw new ArgumentException("Operação deve ser Entrada ou Associacao.");
        Text(c.Motivo); Text(c.Evidencia, "Evidência"); await Responsible(c.ResponsavelId, tracking, ct);
        var scope = await repository.ScopeAsync(c.RecorteId, ct) ?? throw Missing();
        var item = await inventory.ItemAsync(scope.ItemId, tracking, ct) ?? throw Missing();
        var anchor = await repository.AnchorAsync(item.Id, tracking, ct) ?? throw Missing();
        Conflict(anchor.Revisao != scope.RevisaoAncora, "conciliacao_obsoleta", "Houve crédito após a conferência. Exija nova conciliação Admin.");
        Conflict(item.Unidade != "un", "unidade_incompativel", "A ponte individual exige item em un.");
        ProvaEntradaOvos? proof = null; MovimentoEstoque? original = null;
        Guid lotId, localId;
        if (c.Operacao == "Associacao")
        {
            InventoryRules.Id(c.ProvaId ?? Guid.Empty);
            proof = await repository.ProofAsync(c.ProvaId!.Value, false, ct) ?? throw Missing();
            original = await EligibleMovement(proof.MovimentoEstoqueId, ct);
            Conflict(proof.ItemId != item.Id || original.ItemId != item.Id, "item_incompativel", "Prova e conciliação devem pertencer ao mesmo item.");
            lotId = original.LoteId; localId = original.LocalId;
            if (c.LoteId is { } l && l != lotId || c.LocalId is { } p && p != localId) throw new ArgumentException("Destino difere da origem histórica comprovada.");
        }
        else
        {
            if (c.ProvaId is not null || c.Selecao.Any(x => x.Ordinal is not null)) throw new ArgumentException("Entrada nova não aceita prova/ordinal manual.");
            lotId = c.LoteId ?? Guid.Empty; localId = c.LocalId ?? Guid.Empty;
            InventoryRules.Id(lotId); InventoryRules.Id(localId);
        }
        var local = await inventory.LocalAsync(localId, tracking, ct) ?? throw Missing();
        var lot = await inventory.LoteAsync(lotId, tracking, ct) ?? throw Missing();
        var position = await inventory.PositionAsync(lotId, localId, tracking, ct) ?? PosicaoEstoque.Create(lotId, localId, lot.Unidade);
        Conflict(lot.ItemId != item.Id || lot.Unidade != "un", "destino_incompativel", "Lote deve pertencer ao item un da conciliação.");
        await repository.EnsureLotConsistentAsync(lotId, ct);
        bool block = false;
        if (c.Operacao == "Entrada")
        {
            Conflict(!item.Ativo || !item.PodeEntrar || !lot.Ativo || !local.Ativo || lot.Situacao == "Encerrado",
                "destino_inelegivel", "Entrada exige item/lote/local ativos e capacidade de entrada.");
            Conflict(await inventory.IsProductionLotAsync(lotId, ct) || await repository.IsBridgeLotAsync(lotId, ct) ||
                await repository.LotHasAnyHistoryAsync(lotId, ct), "lote_nao_vazio", "Use lote dedicado vazio, sem movimento ou reserva histórica.");
            block = lot.Situacao == "Bloqueado" || InventoryRules.Expired(lot.Validade, Today) || local.Finalidade == "Segregacao";
            Conflict(block && local.Finalidade != "Segregacao", "segregacao_necessaria", "Recebimento bloqueado/vencido exige Segregação.");
        }
        long remaining = 0;
        if (proof is not null)
        {
            proof = await repository.ProofAsync(proof.Id, tracking, ct) ?? throw Missing();
            var allocations = await repository.AllocationsAsync(proof.Id, ct);
            var ordinals = c.Selecao.Select(x => Count(x.Ordinal ?? "")).ToArray();
            if (ordinals.Distinct().Count() != ordinals.Length) throw new ArgumentException("Ordinais repetidos na seleção.");
            Conflict(ordinals.Any(x => x > proof.Contagem) || allocations.Any(x => ordinals.Contains(x.Ordinal)),
                "capacidade_ocupada", "Ordinal fora da capacidade ou já ocupado vitaliciamente.");
            remaining = proof.Contagem - allocations.Count;
            Conflict(remaining < ids.Count, "capacidade_insuficiente", "Capacidade histórica insuficiente.");
        }
        var sources = await repository.SourcesAsync(ids, tracking, ct);
        if (sources.Count != ids.Count) throw Missing();
        var decisions = (await repository.DecisionsAsync(scope.Id, ct)).ToDictionary(x => x.ProducaoOvoId);
        var superseded = (await repository.SupersededSourcesAsync(scope.Id, ids, ct)).ToHashSet();
        var existing = (await repository.BridgesAsync(ids, ct)).Select(x => x.ProducaoOvoId).ToHashSet();
        var expectations = sources.OrderBy(x => x.Id).Select(source =>
        {
            decisions.TryGetValue(source.Id, out var decision);
            var hash = SourceHash(source);
            var expectedDecision = c.Operacao == "Entrada" ? "NaoRecebidoComprovado" : "RecebidoComEquivalencia";
            var reason = existing.Contains(source.Id) ? "ID já vinculado vitaliciamente, inclusive após tratamento."
                : superseded.Contains(source.Id) ? "Uma conferência mais nova substituiu a decisão deste ID. Use a conferência atual."
                : decision is null ? "ID fora da conferência aprovada."
                : !decision.IndividualComprovado || decision.Decisao != expectedDecision ? "Unidade ou destino físico não comprovado para esta operação."
                : decision.HashOrigem != hash || decision.OrigemUpdatedAtUtc != source.UpdatedAtUtc ? "Origem alterada após conferência; exija nova conciliação." : null;
            return new EggSourceExpectation(source.Id, hash, source.UpdatedAtUtc, decision?.Decisao ?? "Pendente", reason);
        }).ToArray();
        var cut = await inventory.LastSequenceAsync(ct);
        var warnings = new List<string> { "Quantidade em un; peso zootécnico não comprova massa física ou conversão do lote." };
        if (proof is not null) warnings.Add("Associação documental com delta zero: não restaura saldo consumido ou identifica o ovo consumido.");
        if (lot.Validade is null) warnings.Add("Validade não informada.");
        if (block) warnings.Add("Lote permanece/será bloqueado em Segregação.");
        var stateHash = Hash(new { Command = Normalize(c), Scope = scope, Anchor = anchor, Item = item, Lot = lot, Local = local,
            Position = position, Proof = proof, Remaining = remaining, Cut = cut, Day = Today, Expectations = expectations });
        return new(c, scope, anchor, item, lot, local, position, proof, original, sources, expectations, remaining, cut, block, stateHash, warnings);
    }
    private static EggPreviewCommand Normalize(EggPreviewCommand c) => c with { Selecao = c.Selecao.OrderBy(x => x.ProducaoOvoId).ToArray() };
    private sealed record Seal(string Mode, EggPreviewCommand? Command, EggTreatmentPreviewCommand? Treatment,
        string StateHash, DateTimeOffset ExpiresAtUtc, string Algorithm);
    private Seal Unseal(string token, bool enforceExpiry)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200000) throw new ArgumentException("Token de prévia inválido.");
        Seal seal;
        try { seal = InventoryJson.Read<Seal>(protector.Unprotect(token)); }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException or ArgumentException)
        { throw new EggBridgeException(409, "previa_invalida", "Prévia inválida. Prepare nova prévia verificável."); }
        Conflict(seal is null || seal.Algorithm != Algorithm || enforceExpiry && seal.ExpiresAtUtc < Now,
            "previa_expirada", "Prévia inválida ou expirada. Prepare nova prévia.");
        return seal!;
    }
    public Task<EggPreview> PreviewAsync(EggPreviewCommand c, Guid actor, CancellationToken ct) =>
        Read(actor, c.Operacao == "Associacao", async () =>
        {
            var plan = await Prepare(c, false, ct);
            var expiry = Now.AddMinutes(30);
            var token = protector.Protect(InventoryJson.Write(new Seal("Confirmacao", Normalize(c), null, plan.Hash, expiry, Algorithm)));
            var delta = c.Operacao == "Entrada" ? plan.Sources.Count : 0;
            return new EggPreview(token, c.Operacao, plan.Expectations, plan.Sources.Count.ToString(CultureInfo.InvariantCulture),
                delta.ToString(CultureInfo.InvariantCulture), CatalogRules.Format(plan.Position.Quantidade),
                CatalogRules.Format(plan.Position.Quantidade + delta), plan.Proof is null ? null : plan.Remaining.ToString(CultureInfo.InvariantCulture),
                "un", plan.Anchor.Revisao.ToString(CultureInfo.InvariantCulture), plan.Cut.ToString(CultureInfo.InvariantCulture),
                plan.Warnings, plan.Expectations.All(x => x.Impedimento is null), expiry);
        }, ct);
    public Task<EggMutationResult> ConfirmAsync(EggConfirmCommand c, string key, Guid actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        // The protected envelope is decoded before the transaction only to choose the permission/operation.
        // Expiry and changing state are evaluated after authorization and replay lookup.
        var initial = Unseal(c.Token, false);
        if (initial.Mode != "Confirmacao" || initial.Command is null) throw new ArgumentException("Prévia não é de confirmação.");
        var operation = initial.Command.Operacao;
        return Mutate(operation, c, key, actor, operation == "Associacao", async () =>
        {
            var seal = Unseal(c.Token, true);
            var plan = await Prepare(seal.Command!, true, ct);
            Conflict(plan.Expectations.Any(x => x.Impedimento is not null), "origem_impedida", "Há origem impedida. Revise a seleção e faça nova prévia; nenhum ID foi omitido.");
            Conflict(plan.Hash != seal.StateHash, "previa_obsoleta", "Origem, destino, prova ou livro mudou. Exija nova prévia/conferência.");
            var groupId = Guid.NewGuid();
            var now = Now;
            var ev = plan.Original?.EventoId ?? Guid.NewGuid();
            var movementId = plan.Original?.Id ?? Guid.NewGuid();
            var beforeLot = InventoryJson.Write(plan.Lot);
            if (plan.BlockLot && plan.Lot.Situacao != "Bloqueado") plan.Lot.SetState("Bloqueado", plan.Lot.Revisao, now, Today);
            if (operation == "Entrada")
            {
                var count = plan.Sources.Count;
                var quantity = new PhysicalQuantity(count.ToString(CultureInfo.InvariantCulture), "un", count.ToString(CultureInfo.InvariantCulture),
                    count.ToString(CultureInfo.InvariantCulture), "0", "un", null, null, null, false);
                var snapshot = InventoryJson.Write(new { Pedido = plan.Command, Item = plan.Item, Lote = plan.Lot,
                    Locais = new[] { plan.Local }, Previa = new { Quantidade = quantity }, F03 = new { GrupoId = groupId } });
                inventory.Add(new EventoEstoque(ev, await inventory.NextSequenceAsync(ct), "Entrada", actor, plan.Command.ResponsavelId,
                    now, Today, Today, plan.Command.Motivo, plan.Command.Evidencia, null, Algorithm, snapshot));
                if (plan.Position.Revisao == 0) inventory.Add(plan.Position);
                var before = plan.Position.Quantidade;
                plan.Position.SetBalance(checked(before + count), plan.Position.Revisao);
                inventory.Add(new MovimentoEstoque(movementId, ev, 1, plan.Lot.Id, plan.Local.Id, plan.Item.Id, "un", "Entrada", count,
                    quantity.Declarada, "un", "0", before, plan.Position.Quantidade, plan.Lot.ConversaoItemId, plan.Lot.PerfilNutricionalId, snapshot, "Entrada"));
                repository.Add(new OrigemLoteOvos(Guid.NewGuid(), plan.Lot.Id, groupId));
                if (beforeLot != InventoryJson.Write(plan.Lot))
                    inventory.Add(new HistoricoEstoque(Guid.NewGuid(), plan.Lot.Id, "Lote", "BloqueioPorMovimento", await inventory.NextSequenceAsync(ct), actor,
                        now, plan.Command.Motivo, beforeLot, InventoryJson.Write(plan.Lot)));
            }
            var group = new GrupoPonteOvos(groupId, operation, plan.Item.Id, plan.Lot.Id, plan.Local.Id, plan.Scope.Id,
                plan.Proof?.Id, ev, movementId, plan.Sources.Count, plan.Lot.Revisao, plan.Lot.Situacao, plan.Lot.Validade,
                InventoryJson.Write(plan.Lot), actor, plan.Command.ResponsavelId, plan.Command.Motivo, plan.Command.Evidencia, now);
            repository.Add(group);
            foreach (var source in plan.Sources)
            {
                var bridge = new PonteProducaoOvo(Guid.NewGuid(), source.Id, groupId, SourceSnapshot(source), SourceHash(source), source.UpdatedAtUtc, Algorithm);
                repository.Add(bridge);
                if (plan.Proof is { } proof)
                    repository.Add(new AlocacaoProvaOvos(Guid.NewGuid(), bridge.Id, proof.Id, proof.MovimentoEstoqueId,
                        Count(plan.Command.Selecao.Single(x => x.ProducaoOvoId == source.Id).Ordinal!)));
            }
            await repository.SaveAsync(ct);
            return new(await Detail(groupId, ct), Location: $"/api/v1/producao/ponte-ovos/grupos/{groupId}", Group: groupId);
        }, ct);
    }

    private sealed record TreatmentPlan(GrupoPonteOvos Group, EggTreatmentCommand Command,
        LoteMaterial Lot, LocalEstoque Local, PosicaoEstoque Position, MovimentoEstoque Original,
        IReadOnlyList<string> Reasons, string Hash);
    private async Task<TreatmentPlan> PrepareTreatment(Guid groupId, EggTreatmentCommand c, bool tracking, CancellationToken ct)
    {
        if (c.Tipo is not ("Anotacao" or "Invalidacao" or "Compensacao")) throw new ArgumentException("Tipo de tratamento inválido.");
        Text(c.Motivo); Text(c.Evidencia, "Evidência"); await Responsible(c.ResponsavelId, tracking, ct);
        var group = await repository.GroupAsync(groupId, ct) ?? throw Missing();
        _ = await inventory.ItemAsync(group.ItemId, tracking, ct) ?? throw Missing();
        _ = await repository.AnchorAsync(group.ItemId, tracking, ct) ?? throw Missing();
        var local = await inventory.LocalAsync(group.LocalId, tracking, ct) ?? throw Missing();
        var lot = await inventory.LoteAsync(group.LoteId, tracking, ct) ?? throw Missing();
        var position = await inventory.PositionAsync(group.LoteId, group.LocalId, tracking, ct) ?? throw Missing();
        var original = await repository.MovementAsync(group.MovimentoEstoqueId, ct) ?? throw Missing();
        var treatments = await repository.TreatmentsAsync(groupId, ct);
        var reasons = new List<string>();
        if (c.Tipo == "Invalidacao")
        {
            if (group.Operacao != "Associacao") reasons.Add("Invalidação documental somente para associação sem saldo.");
            if (treatments.Any(x => x.Tipo == "Invalidacao")) reasons.Add("Associação já invalidada; identidade e capacidade permanecem ocupadas.");
        }
        if (c.Tipo == "Compensacao")
        {
            if (group.Operacao != "Entrada") reasons.Add("Associação não criou saldo e não pode ser debitada.");
            if (!c.CoerenciaFisicaConfirmada) reasons.Add("Confirme coerência física e evidência de erro de registro.");
            if (treatments.Any(x => x.Tipo == "Compensacao")) reasons.Add("Grupo já compensado, sem liberação de identidade.");
            if (await repository.WasUsedAfterEntryAsync(group, ct))
                reasons.Add("Movimento, reserva histórica ou revisão posterior impede compensação.");
            if (!lot.Ativo || !local.Ativo || lot.Situacao == "Encerrado" || position.Quantidade != group.Quantidade || await inventory.ReservedAsync(lot.Id, local.Id, ct) != 0)
                reasons.Add("Lote/posição não está integralmente intacto e compatível com o débito.");
            var reconciliation = await repository.ReconcileAsync(groupId, ct);
            if (!reconciliation.Coerente) reasons.Add("Divergência persistida exige conciliação antes de compensar.");
        }
        var hash = Hash(new { Group = group, Command = c, Lot = lot, Local = local, Position = position, Original = original,
            Treatments = treatments.OrderBy(x => x.Id), Reasons = reasons, Cut = await inventory.LastSequenceAsync(ct), Day = Today });
        return new(group, c, lot, local, position, original, reasons, hash);
    }
    public Task<EggTreatmentPreview> PreviewTreatmentAsync(Guid groupId, EggTreatmentCommand c, Guid actor, CancellationToken ct) =>
        Read(actor, true, async () =>
        {
            var plan = await PrepareTreatment(groupId, c, false, ct);
            var expiry = Now.AddMinutes(30);
            var token = protector.Protect(InventoryJson.Write(new Seal("Tratamento", null, new(groupId, c), plan.Hash, expiry, Algorithm)));
            return new EggTreatmentPreview(token, groupId, c.Tipo, c.Tipo == "Compensacao" ? (-plan.Group.Quantidade).ToString(CultureInfo.InvariantCulture) : "0",
                plan.Reasons, plan.Reasons.Count == 0, expiry);
        }, ct);
    public Task<EggMutationResult> TreatAsync(Guid groupId, EggConfirmCommand c, string key, Guid actor, CancellationToken ct)
    {
        var initial = Unseal(c.Token, false);
        if (initial.Mode != "Tratamento" || initial.Treatment is null || initial.Treatment.GrupoId != groupId) throw new ArgumentException("Prévia não pertence ao grupo/tratamento.");
        return Mutate(initial.Treatment.Comando.Tipo, c, key, actor, true, async () =>
        {
            var seal = Unseal(c.Token, true);
            var plan = await PrepareTreatment(groupId, seal.Treatment!.Comando, true, ct);
            Conflict(plan.Reasons.Count != 0, "tratamento_impedido", string.Join(" ", plan.Reasons));
            Conflict(plan.Hash != seal.StateHash, "previa_obsoleta", "Estado mudou após a prévia. Confira novo tratamento.");
            Guid? ev = null;
            if (plan.Command.Tipo == "Compensacao")
            {
                ev = Guid.NewGuid();
                var count = CatalogRules.Format(plan.Original.Quantidade);
                var quantity = new PhysicalQuantity(count, plan.Original.Unidade, count, count, "0", plan.Original.Unidade, null, null, null, false);
                var snapshot = InventoryJson.Write(new { Pedido = plan.Command, F03 = new { GrupoId = groupId },
                    Compensacao = new { MovimentoOriginalId = plan.Original.Id }, Previa = new { Quantidade = quantity } });
                inventory.Add(new EventoEstoque(ev.Value, await inventory.NextSequenceAsync(ct), "CompensacaoEntradaOvos", actor,
                    plan.Command.ResponsavelId, Now, Today, Today, plan.Command.Motivo, plan.Command.Evidencia, plan.Group.EventoId, Algorithm, snapshot));
                var before = plan.Position.Quantidade;
                plan.Position.SetBalance(before - plan.Original.Quantidade, plan.Position.Revisao);
                inventory.Add(new MovimentoEstoque(Guid.NewGuid(), ev.Value, 1, plan.Lot.Id, plan.Local.Id, plan.Group.ItemId, plan.Original.Unidade,
                    "Saida", plan.Original.Quantidade, count, plan.Original.Unidade, "0", before, plan.Position.Quantidade,
                    plan.Original.ConversaoItemId, plan.Original.PerfilNutricionalId, snapshot, "CompensacaoEntradaOvos"));
                // Persist the zero projection before the terminal lot state is checked by the physical guard.
                await repository.SaveAsync(ct);
                var beforeLot = InventoryJson.Write(plan.Lot);
                plan.Lot.SetState("Encerrado", plan.Lot.Revisao, Now, Today);
                inventory.Add(new HistoricoEstoque(Guid.NewGuid(), plan.Lot.Id, "Lote", "CompensacaoEntradaOvos", await inventory.NextSequenceAsync(ct),
                    actor, Now, plan.Command.Motivo, beforeLot, InventoryJson.Write(plan.Lot)));
            }
            repository.Add(new TratamentoPonteOvos(Guid.NewGuid(), groupId, plan.Command.Tipo, ev, actor, plan.Command.ResponsavelId,
                plan.Command.Motivo, plan.Command.Evidencia, Now));
            await repository.SaveAsync(ct);
            return new(await Detail(groupId, ct), Status: 200, Group: groupId);
        }, ct, productionLock: initial.Treatment.Comando.Tipo == "Compensacao");
    }
    public Task<EggMutationResult> CommandAsync(string operation, string key, Guid actor, CancellationToken ct) =>
        Read(actor, false, async () =>
        {
            var result = await repository.ReplayAsync(operation, CatalogRules.Text(key, 100, "Chave"), ct) ?? throw Missing();
            if (result.ExigeAdmin) await Authorize(actor, true, ct);
            return new EggMutationResult(result.StatusHttp, result.Location, result.RespostaJson, true);
        }, ct);
    public Task<EggBridgePage<EggGroupReconciliation>> ReconcileAsync(EggBridgeQuery q, Guid actor, CancellationToken ct) =>
        Read(actor, true, async () =>
        {
            q.Validate();
            var groups = await repository.GroupsAsync(q, ct);
            var rows = new List<EggGroupReconciliation>();
            foreach (var group in groups.Items) rows.Add(await repository.ReconcileAsync(group.Id, ct));
            return new EggBridgePage<EggGroupReconciliation>(rows, groups.Page, groups.PageSize, groups.TotalItems, groups.TotalPages);
        }, ct);
}
