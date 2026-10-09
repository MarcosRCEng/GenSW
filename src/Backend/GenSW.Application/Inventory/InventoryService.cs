using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenSW.Application.Authentication;
using GenSW.Application.Formulation;
using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;

namespace GenSW.Application.Inventory;

public sealed class InventoryService(IInventoryRepository repository, IAuthenticationSessionService authentication, TimeProvider clock)
{
    private static InventoryException Missing() => new(404, "registro_nao_encontrado", "Registro não encontrado.");
    private static void Conflict(bool condition, string code, string text) { if (condition) throw new InventoryException(409, code, text); }
    private static void Admin(bool isAdmin) { if (!isAdmin) throw new InventoryException(403, "admin_necessario", "Operação exige papel Admin atual."); }
    private DateTimeOffset Now => clock.GetUtcNow();
    private DateOnly Today => InventoryRules.Today(clock);
    private static T Required<T>(T? value) where T : class => value ?? throw new ArgumentNullException(nameof(value));
    private async Task<bool> AuthorizeAsync(Guid actor, bool admin, CancellationToken ct)
    {
        var user = await authentication.GetCurrentUserAsync(actor, ct) ?? throw new InventoryException(401, "sessao_invalida", "Usuário não está mais ativo. Entre novamente.");
        var isAdmin = user.Roles.Contains("Admin", StringComparer.Ordinal); if (admin) Admin(isAdmin); return isAdmin;
    }
    private async Task<T> Read<T>(Guid actor, Func<Task<T>> action, CancellationToken ct)
    {
        await using var scope = await repository.BeginReadAsync(ct); await AuthorizeAsync(actor, false, ct);
        var result = await action(); await scope.CommitAsync(ct); return result;
    }
    public static string CanonicalHash<T>(T value)
    {
        using var doc = JsonDocument.Parse(InventoryJson.Write(value));
        string Canonical(JsonElement e, string? field = null) => e.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(",", e.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value, p.Name))) + "}",
            JsonValueKind.Array => "[" + string.Join(",", e.EnumerateArray().Select(x => Canonical(x))) + "]",
            JsonValueKind.String when field is "quantidade" or "quantidadeContada" or "calculado" or "normalizado" or "residuo" => NormalizeNumber(e),
            _ => e.GetRawText()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(doc.RootElement))));
    }
    private static string NormalizeNumber(JsonElement e) => decimal.TryParse(e.GetString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
        ? JsonSerializer.Serialize(CatalogRules.Format(value)) : e.GetRawText();
    private sealed record Payload(object Body, int Status, string? Location, Guid? RegistroId = null, Guid? EventoId = null, bool ExigeAdmin = false);
    private async Task<InventoryMutationResult> Mutate<T>(string operation, Guid resource, T command, string key, Guid actor, bool admin, Func<bool, Task<Payload>> action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command); ValidateShape(command);
        key = CatalogRules.Text(key, 100, "Idempotency-Key"); var hash = CanonicalHash(command);
        await using var scope = await repository.BeginMutationAsync(ct);
        try
        {
            var isAdmin = await AuthorizeAsync(actor, admin, ct);
            if (await repository.ReplayAsync(actor, operation, resource, key, ct) is { } replay)
            {
                if (replay.ExigeAdmin) Admin(isAdmin);
                Conflict(replay.HashPayload != hash, "idempotencia_divergente", "Chave já utilizada com conteúdo diferente.");
                await scope.CommitAsync(ct); return new(replay.StatusHttp, replay.Location, replay.RespostaJson, true);
            }
            var result = await action(isAdmin); var json = InventoryJson.Write(result.Body);
            repository.Add(new ComandoEstoque(Guid.NewGuid(), actor, operation, resource, key, hash, result.Status, result.Location, json, result.EventoId, result.RegistroId, Now, admin || result.ExigeAdmin));
            await repository.SaveAsync(ct); await scope.CommitAsync(ct); return new(result.Status, result.Location, json, false);
        }
        catch { repository.ClearTracking(); throw; }
    }
    private async Task Audit(Guid id, string type, string operation, Guid actor, string reason, object? before, object after, CancellationToken ct)
    {
        repository.Add(new HistoricoEstoque(Guid.NewGuid(), id, type, operation, await repository.NextSequenceAsync(ct), actor, Now,
            CatalogRules.Text(reason, 2000, "Motivo"), InventoryJson.Write(before), InventoryJson.Write(after)));
    }
    private async Task Responsible(Guid id, CancellationToken ct, bool locking = true)
    {
        InventoryRules.Id(id);
        var value = await repository.ResponsibleAsync(id, locking, ct) ?? throw Missing();
        Conflict(!value.Ativo, "responsavel_inativo", "Escolha responsável ativo para o novo fato.");
    }
    private async Task References(Guid item, Guid? profile, Guid? conversion, string? applicability, CancellationToken ct)
    {
        if (profile is { } p)
        {
            var value = await repository.ProfileAsync(p, ct) ?? throw Missing();
            if (value.ItemId != item) throw new ArgumentException("Perfil pertence a outro item.");
            Conflict(value.Estado != "Publicado", "perfil_inativo", "Selecione perfil publicado para o novo vínculo.");
        }
        if (conversion is { } c && (await repository.ConversionAsync(c, ct) ?? throw Missing()).ItemId != item) throw new ArgumentException("Conversão pertence a outro item.");
        if (profile.HasValue || conversion.HasValue) CatalogRules.Text(applicability, 2000, "Aplicabilidade das referências");
    }
    private static LoteData LotData(LoteCommand c) => new(c.CodigoExterno ?? "", c.Origem, c.Fonte, c.ResponsavelId, c.DataOrigem, c.Fabricacao, c.Coleta);
    public Task<InventoryMutationResult> CreateLocalAsync(LocalCommand c, string key, Guid actor, CancellationToken ct) => Mutate("LocalCriacao", Guid.Empty, c, key, actor, !Required(c).Ativo, async _ =>
    {
        InventoryRules.Expected(0, c.VersaoEsperada);
        if (c.PropriedadeId is { } p) Conflict(!await repository.PropertyActiveAsync(p, ct), "propriedade_inativa", "Propriedade inexistente ou inativa para o novo vínculo.");
        var local = LocalEstoque.Create(c.Codigo, c.Nome, c.Descricao, c.PropriedadeId, c.Finalidade, c.Ativo, Now);
        repository.Add(local); await Audit(local.Id, "Local", "Criacao", actor, c.Motivo, null, local, ct);
        await repository.SaveAsync(ct); return new(await repository.LocalViewAsync(local.Id, ct) ?? throw Missing(), 201, $"/api/v1/estoque/locais/{local.Id}", local.Id);
    }, ct);
    public Task<InventoryMutationResult> UpdateLocalAsync(Guid id, LocalCommand c, string key, Guid actor, CancellationToken ct) => Mutate("LocalEdicao", id, c, key, actor, false, async _ =>
    {
        var local = await repository.LocalAsync(id, true, ct) ?? throw Missing(); var before = InventoryJson.Write(local);
        if (c.Ativo != local.Ativo || c.Finalidade != local.Finalidade) throw new ArgumentException("Status/finalidade usam comandos próprios.");
        if (c.PropriedadeId is { } p && p != local.PropriedadeId) Conflict(!await repository.PropertyActiveAsync(p, ct), "propriedade_inativa", "Propriedade inexistente ou inativa para o novo vínculo.");
        local.Update(c.Codigo, c.Nome, c.Descricao, c.PropriedadeId, c.VersaoEsperada, Now);
        await Audit(id, "Local", "Edicao", actor, c.Motivo, JsonDocument.Parse(before).RootElement, local, ct);
        await repository.SaveAsync(ct); return new(await repository.LocalViewAsync(id, ct) ?? throw Missing(), 200, null, id);
    }, ct);
    public Task<InventoryMutationResult> LocalStateAsync(Guid id, string operation, InventoryStateCommand c, string key, Guid actor, CancellationToken ct)
    {
        operation = StateOperation(operation); return Mutate("Local" + operation, id, c, key, actor, true, async _ =>
        {
            Evidence(c.Motivo, c.Evidencia); var local = await repository.LocalAsync(id, true, ct) ?? throw Missing(); var before = InventoryJson.Write(local);
            if (operation == "Ativo") local.SetActive(c.Ativo ?? throw new ArgumentException("Informe ativo."), c.VersaoEsperada, Now);
            else if (operation == "Finalidade")
            { Conflict(await repository.HasBalanceAsync(null, id, ct), "local_com_saldo", "Troca de finalidade exige todas as posições zeradas."); local.SetPurpose(c.Finalidade ?? "", c.VersaoEsperada, Now); }
            else throw new ArgumentException("Comando de local inválido.");
            await Audit(id, "Local", operation, actor, c.Motivo, JsonDocument.Parse(before).RootElement, new { Local = local, c.Evidencia }, ct);
            await repository.SaveAsync(ct); return new(await repository.LocalViewAsync(id, ct) ?? throw Missing(), 200, null, id);
        }, ct);
    }
    public Task<InventoryMutationResult> CreateLoteAsync(LoteCommand c, string key, Guid actor, CancellationToken ct) => Mutate("LoteCriacao", Guid.Empty, c, key, actor, false, async admin =>
    {
        InventoryRules.Expected(0, c.VersaoEsperada); var item = await repository.ItemAsync(c.ItemId, true, ct) ?? throw Missing();
        InventoryRules.Expected(item.Revisao, c.ItemVersaoEsperada); if (!item.Ativo || !item.PodeEntrar) Admin(admin);
        await Responsible(c.ResponsavelId, ct); if (c.Validade.HasValue) await Responsible(c.ResponsavelValidadeId ?? Guid.Empty, ct);
        await References(item.Id, c.PerfilNutricionalId, c.ConversaoItemId, c.Aplicabilidade, ct);
        var lot = LoteMaterial.Create(item.Id, c.Codigo, item.Unidade, LotData(c), c.Validade, c.FonteValidade, c.ResponsavelValidadeId,
            c.PerfilNutricionalId, c.ConversaoItemId, c.Aplicabilidade, c.Pendente || !item.Ativo || !item.PodeEntrar, Now, Today);
        var beforeItem = InventoryJson.Write(item); item.FixUnit(); repository.Add(lot);
        if (beforeItem != InventoryJson.Write(item))
        {
            repository.Add(new CatalogAudit(Guid.NewGuid(), item.Id, "Item", "UsoFisico", actor, Now, beforeItem, InventoryJson.Write(item)));
            await Audit(item.Id, "Item", "FixacaoUnidade", actor, c.Motivo, JsonDocument.Parse(beforeItem).RootElement, item, ct);
        }
        await Audit(lot.Id, "Lote", "Criacao", actor, c.Motivo, null, lot, ct); await repository.SaveAsync(ct);
        return new(await repository.LoteViewAsync(lot.Id, ct) ?? throw Missing(), 201, $"/api/v1/estoque/lotes/{lot.Id}", lot.Id, ExigeAdmin: !item.Ativo || !item.PodeEntrar);
    }, ct);
    public Task<InventoryMutationResult> UpdateLoteAsync(Guid id, LoteCommand c, string key, Guid actor, CancellationToken ct) => Mutate("LoteEdicao", id, c, key, actor, false, async _ =>
    {
        var lot = await repository.LoteAsync(id, true, ct) ?? throw Missing(); var before = InventoryJson.Write(lot);
        if (lot.ItemId != c.ItemId || lot.Codigo != c.Codigo || lot.Validade != c.Validade || lot.FonteValidade != c.FonteValidade || lot.ResponsavelValidadeId != c.ResponsavelValidadeId ||
            lot.PerfilNutricionalId != c.PerfilNutricionalId || lot.ConversaoItemId != c.ConversaoItemId || lot.Aplicabilidade != c.Aplicabilidade || c.Pendente) throw new ArgumentException("Identidade, validade, situação e referências não são editáveis por metadados.");
        if (c.ResponsavelId != lot.ResponsavelId) await Responsible(c.ResponsavelId, ct);
        lot.Update(LotData(c), c.VersaoEsperada, Now, Today); await Audit(id, "Lote", "Edicao", actor, c.Motivo, JsonDocument.Parse(before).RootElement, lot, ct);
        await repository.SaveAsync(ct); return new(await repository.LoteViewAsync(id, ct) ?? throw Missing(), 200, null, id);
    }, ct);
    public Task<InventoryMutationResult> LoteStateAsync(Guid id, string operation, InventoryStateCommand c, string key, Guid actor, CancellationToken ct)
    {
        operation = StateOperation(operation); return Mutate("Lote" + operation, id, c, key, actor, true, async _ =>
        {
            Evidence(c.Motivo, c.Evidencia); var lot = await repository.LoteAsync(id, true, ct) ?? throw Missing(); var before = InventoryJson.Write(lot);
            if (operation == "Ativo") lot.SetActive(c.Ativo ?? throw new ArgumentException("Informe ativo."), c.VersaoEsperada, Now);
            else if (operation is "Bloqueio" or "Liberacao" or "Encerramento")
            {
                if (operation == "Encerramento") Conflict(await repository.HasBalanceAsync(id, null, ct), "lote_com_saldo", "Encerrar exige saldo zero em todos os locais.");
                if (operation == "Liberacao") Conflict(lot.Situacao != "Bloqueado", "situacao_invalida", "Somente lote Bloqueado pode ser liberado.");
                if (operation == "Bloqueio") Conflict(lot.Situacao != "Liberado", "situacao_invalida", "Somente lote Liberado pode ser bloqueado.");
                lot.SetState(operation == "Bloqueio" ? "Bloqueado" : operation == "Liberacao" ? "Liberado" : "Encerrado", c.VersaoEsperada, Now, Today);
            }
            else if (operation == "Validade")
            { if (c.Validade.HasValue) await Responsible(c.ResponsavelId ?? Guid.Empty, ct); lot.SetExpiry(c.Validade, c.FonteValidade, c.ResponsavelId, c.VersaoEsperada, Now, Today); }
            else if (operation == "Referencias")
            { await References(lot.ItemId, c.PerfilNutricionalId, c.ConversaoItemId, c.Aplicabilidade, ct); lot.SetReferences(c.PerfilNutricionalId, c.ConversaoItemId, c.Aplicabilidade, c.VersaoEsperada, Now); }
            else throw new ArgumentException("Comando de lote inválido.");
            await Audit(id, "Lote", operation, actor, c.Motivo, JsonDocument.Parse(before).RootElement, new { Lote = lot, c.Evidencia }, ct);
            await repository.SaveAsync(ct); return new(await repository.LoteViewAsync(id, ct) ?? throw Missing(), 200, null, id);
        }, ct);
    }
    public static string MovementOperation(string value) => value switch
    {
        "Abertura" or "abertura" => "Abertura", "Entrada" or "entrada" => "Entrada", "Transferencia" or "transferencia" => "Transferencia",
        "SaidaManual" or "saida-manual" => "SaidaManual", "ConsumoInterno" or "consumo-interno" => "ConsumoInterno", "Ajuste" or "ajuste" => "Ajuste",
        "Segregacao" or "segregacao" => "Segregacao", "RetornoSegregacao" or "retorno-segregacao" => "RetornoSegregacao", "Descarte" or "descarte" => "Descarte",
        _ => throw new ArgumentException("Operação de estoque inválida.")
    };
    private static string StateOperation(string value) => value switch
    {
        "Ativo" or "ativo" => "Ativo", "Finalidade" or "finalidade" => "Finalidade", "Bloqueio" or "bloqueio" => "Bloqueio", "Liberacao" or "liberacao" => "Liberacao",
        "Encerramento" or "encerramento" => "Encerramento", "Validade" or "validade" => "Validade", "Referencias" or "referencias" => "Referencias", _ => throw new ArgumentException("Transição inválida.")
    };
    private static bool AdminOperation(string operation) => operation is "Abertura" or "Ajuste" or "Segregacao" or "RetornoSegregacao" or "Descarte";
    private static void Evidence(string reason, string? evidence)
    { CatalogRules.Text(reason, 2000, "Motivo"); CatalogRules.Text(evidence, 2000, "Evidência/declaração de conferência"); }
    private static void ValidateShape<T>(T value)
    {
        switch (value)
        {
            case LocalCommand c:
                CatalogRules.Text(c.Codigo, 50, "Código"); CatalogRules.Text(c.Nome, 200, "Nome"); CatalogRules.Text(c.Motivo, 2000, "Motivo"); break;
            case LoteCommand c:
                InventoryRules.Id(c.ItemId); InventoryRules.Id(c.ResponsavelId); CatalogRules.Text(c.Codigo, 50, "Código"); CatalogRules.Text(c.Origem, 1000, "Origem"); CatalogRules.Text(c.Fonte, 1000, "Fonte"); CatalogRules.Text(c.Motivo, 2000, "Motivo"); break;
            case InventoryStateCommand c:
                Evidence(c.Motivo, c.Evidencia); break;
            case InventoryMovementCommand c:
                InventoryRules.Id(c.LoteId); InventoryRules.Id(c.ResponsavelId); CatalogRules.Decimal(c.Quantidade); CatalogRules.Unit(c.Unidade); CatalogRules.Text(c.Motivo, 2000, "Motivo");
                ArgumentNullException.ThrowIfNull(c.VersoesEsperadas);
                if (c.QuantidadeContada is not null) CatalogRules.Decimal(c.QuantidadeContada);
                if (c.AceiteQuantizacao is { } a) { InventoryRules.PreciseDecimal(a.Calculado); InventoryRules.PreciseDecimal(a.Normalizado); InventoryRules.PreciseDecimal(a.Residuo); CatalogRules.Text(a.Motivo, 2000, "Motivo do aceite"); }
                break;
        }
    }
    private sealed record Plan(Item Item, LoteMaterial Lot, IReadOnlyList<(LocalEstoque Local, PosicaoEstoque Position, decimal Before, decimal After)> Legs,
        InventoryPreview Preview, bool BlockLot, ConversaoItem? Conversion);
    private async Task<Plan> Prepare(string operation, InventoryMovementCommand c, bool tracking, CancellationToken ct, int clockRetry = 0)
    {
        ArgumentNullException.ThrowIfNull(c); ValidateShape(c);
        if (c.VersoesEsperadas is null) throw new ArgumentException("Versões esperadas obrigatórias.");
        var preliminary = await repository.LoteAsync(c.LoteId, false, ct) ?? throw Missing(); var item = await repository.ItemAsync(preliminary.ItemId, tracking, ct) ?? throw Missing();
        bool transfer = operation is "Transferencia" or "Segregacao" or "RetornoSegregacao";
        var localIds = transfer ? new[] { c.OrigemLocalId ?? Guid.Empty, c.DestinoLocalId ?? Guid.Empty } : new[] { c.LocalId ?? Guid.Empty };
        foreach (var id in localIds) InventoryRules.Id(id);
        if (localIds.Distinct().Count() != localIds.Length) throw new ArgumentException("Origem e destino precisam ser diferentes.");
        var locals = new Dictionary<Guid, LocalEstoque>(); foreach (var id in localIds.Order()) locals[id] = await repository.LocalAsync(id, tracking, ct) ?? throw Missing();
        var lot = await repository.LoteAsync(c.LoteId, tracking, ct) ?? throw Missing();
        var positions = new Dictionary<Guid, PosicaoEstoque>(); foreach (var id in localIds.Order()) positions[id] = await repository.PositionAsync(lot.Id, id, tracking, ct) ?? PosicaoEstoque.Create(lot.Id, id, lot.Unidade);
        InventoryRules.Expected(item.Revisao, c.VersoesEsperadas.Item); InventoryRules.Expected(lot.Revisao, c.VersoesEsperadas.Lote);
        foreach (var id in localIds)
        {
            bool first = id == localIds[0]; InventoryRules.Expected(locals[id].Revisao, transfer ? first ? c.VersoesEsperadas.OrigemLocal : c.VersoesEsperadas.DestinoLocal : c.VersoesEsperadas.Local);
            InventoryRules.Expected(positions[id].Revisao, transfer ? first ? c.VersoesEsperadas.OrigemPosicao : c.VersoesEsperadas.DestinoPosicao : c.VersoesEsperadas.Posicao);
        }
        await Responsible(c.ResponsavelId, ct, tracking); CatalogRules.Text(c.Motivo, 2000, "Motivo");
        var today = Today; InventoryRules.Date(c.DataObservada, today); Conflict(lot.Situacao == "Encerrado", "lote_encerrado", "Lote encerrado não admite novos movimentos.");
        if (lot.Unidade != item.Unidade) throw new InventoryException(409, "unidade_incompativel", "Unidade do lote não corresponde ao item.");
        var origin = locals[localIds[0]]; var target = locals[localIds[^1]]; bool block = false;
        if (AdminOperation(operation)) Evidence(c.Motivo, c.Evidencia);
        if ((operation is "Abertura" or "Entrada" or "Ajuste") && c.DataObservada is null) throw new ArgumentException("Informe data observada/conferência.");
        if (operation == "Abertura")
        {
            Conflict(await repository.HasMovementsAsync(lot.Id, origin.Id, ct), "posicao_ja_movimentada", "Abertura somente antes de qualquer movimento da posição.");
            Conflict(!origin.Ativo, "local_inativo", "Local de abertura deve estar ativo.");
            block = !item.Ativo || !item.PodeEntrar || !lot.Ativo || lot.Situacao == "Bloqueado" || InventoryRules.Expired(lot.Validade, today) || origin.Finalidade == "Segregacao";
            Conflict(block && origin.Finalidade != "Segregacao", "segregacao_necessaria", "Inventário de material inelegível exige segregação.");
        }
        else if (operation == "Entrada")
        {
            Conflict(!item.Ativo || !item.PodeEntrar || !lot.Ativo || !origin.Ativo, "referencia_inativa", "Recebimento exige item/lote/local ativos e capacidade de entrada.");
            if (CatalogRules.Text(c.Origem, 1000, "Origem") != lot.Origem || CatalogRules.Text(c.Fonte, 1000, "Fonte") != lot.Fonte) throw new ArgumentException("Origem/fonte distintas exigem outro lote.");
            block = lot.Situacao == "Bloqueado" || InventoryRules.Expired(lot.Validade, today) || origin.Finalidade == "Segregacao";
            Conflict(block && origin.Finalidade != "Segregacao", "segregacao_necessaria", "Recebimento Bloqueado/vencido exige segregação.");
        }
        else if (operation == "Segregacao")
        { Conflict(!target.Ativo || target.Finalidade != "Segregacao", "destino_invalido", "Destino deve ser Segregação ativa."); block = true; }
        else if (operation == "RetornoSegregacao")
        { Conflict(!origin.Ativo || origin.Finalidade != "Segregacao" || !target.Ativo || target.Finalidade != "Ordinario" || !item.Ativo || !lot.Ativo || lot.Situacao != "Liberado" || InventoryRules.Expired(lot.Validade, today), "retorno_invalido", "Retorno exige lote liberado/ativo/não vencido e locais ativos compatíveis."); }
        else if (operation is "Transferencia" or "SaidaManual" or "ConsumoInterno")
        {
            var reasons = InventoryRules.Unavailable(item, lot, origin, today, operation == "ConsumoInterno"); Conflict(reasons.Count > 0, "estoque_inelegivel", string.Join("; ", reasons));
            if (transfer) Conflict(!target.Ativo || target.Finalidade != "Ordinario", "destino_invalido", "Transferência ordinária exige destino Ordinário ativo.");
            if (operation == "SaidaManual") CatalogRules.Text(c.Destino, 1000, "Destino/finalidade da saída");
        }
        if (c.EventoReferenciaId is { } eventId && await repository.EventoAsync(eventId, ct) is null) throw Missing();
        ConversaoItem? conversion = null;
        if (c.ConversaoItemId is { } conversionId)
        { if (conversionId != lot.ConversaoItemId) throw new ArgumentException("Conversão deve estar explicitamente vinculada ao lote."); conversion = await repository.ConversionAsync(conversionId, ct) ?? throw Missing(); }
        PhysicalQuantity quantity; decimal amount; bool addition = operation is "Abertura" or "Entrada";
        if (operation == "Ajuste")
        {
            if (c.Unidade != lot.Unidade || c.AceiteQuantizacao is not null || c.ConversaoItemId is not null) throw new ArgumentException("Ajuste aponta contagem na unidade canônica, sem conversão.");
            var counted = CatalogRules.Decimal(c.QuantidadeContada); InventoryRules.Balance(counted, lot.Unidade); var delta = counted - positions[origin.Id].Quantidade;
            if (delta == 0) throw new ArgumentException("Contagem igual ao saldo não gera movimento.");
            addition = delta > 0; amount = Math.Abs(delta); var formatted = CatalogRules.Format(amount);
            quantity = new(c.QuantidadeContada!, lot.Unidade, formatted, formatted, "0", lot.Unidade, null, null, null, false);
            block = addition && (!item.Ativo || !lot.Ativo || lot.Situacao == "Bloqueado" || !origin.Ativo || origin.Finalidade == "Segregacao" || InventoryRules.Expired(lot.Validade, today));
        }
        else
        {
            if (transfer && (c.Unidade != lot.Unidade || c.ConversaoItemId.HasValue || c.AceiteQuantizacao is not null)) throw new ArgumentException("Movimentação interna exige quantidade canônica, sem conversão ou quantização.");
            quantity = InventoryRules.Normalize(c.Quantidade, c.Unidade, lot.Unidade, conversion, lot.ItemId); amount = CatalogRules.Decimal(quantity.Normalizada, true);
        }
        var legs = new List<(LocalEstoque Local, PosicaoEstoque Position, decimal Before, decimal After)>();
        foreach (var id in localIds)
        {
            var position = positions[id]; var isCredit = transfer ? id == localIds[1] : addition;
            var after = checked(position.Quantidade + (isCredit ? amount : -amount)); InventoryRules.Balance(after, lot.Unidade);
            legs.Add((locals[id], position, position.Quantidade, after));
        }
        var warnings = new List<string>(); if (lot.Validade is null) warnings.Add("Validade não informada."); if (quantity.Proveniencia is "Estimado" or "Declarado") warnings.Add("Conversão " + quantity.Proveniencia.ToLowerInvariant() + "."); if (block) warnings.Add("Lote permanece/será Bloqueado globalmente.");
        var cut = await repository.LastSequenceAsync(ct); var observed = Now;
        if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(observed, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime) != today)
        { if (clockRetry >= 2) throw new InventoryException(409, "conflito_transitorio", "Data operacional mudou; confira nova prévia."); return await Prepare(operation, c, tracking, ct, clockRetry + 1); }
        var preview = new InventoryPreview(operation, lot.Id, lot.Unidade, quantity, legs.Select(x => new PreviewPosition(lot.Id, x.Local.Id, CatalogRules.Format(x.Before), CatalogRules.Format(x.After), x.Position.Revisao, x.Position.Revisao + 1)).ToArray(), c.VersoesEsperadas, warnings, observed, cut.ToString(CultureInfo.InvariantCulture));
        return new(item, lot, legs, preview, block, conversion);
    }
    public Task<InventoryPreview> PreviewAsync(InventoryPreviewCommand c, Guid actor, CancellationToken ct) => Read(actor, async () =>
    {
        var operation = MovementOperation(Required(c).Operacao); await AuthorizeAsync(actor, AdminOperation(operation), ct); return (await Prepare(operation, c.Comando, false, ct)).Preview;
    }, ct);
    public Task<InventoryMutationResult> MoveAsync(string operation, InventoryMovementCommand c, string key, Guid actor, CancellationToken ct)
    {
        operation = MovementOperation(operation); return Mutate(operation, Required(c).LoteId, c, key, actor, AdminOperation(operation), async _ =>
        {
            var plan = await Prepare(operation, c, true, ct); InventoryRules.Accept(plan.Preview.Quantidade, c.AceiteQuantizacao);
            var today = Today;
            // A date change while references were being checked must never retain yesterday's eligibility.
            if (today != DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(plan.Preview.ObservadoEmUtc, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime)) plan = await Prepare(operation, c, true, ct);
            var sequence = await repository.NextSequenceAsync(ct); var id = Guid.NewGuid();
            var profile = plan.Lot.PerfilNutricionalId is { } p ? await repository.ProfileAsync(p, ct) : null;
            var referenceConversion = plan.Conversion ?? (plan.Lot.ConversaoItemId is { } conversionId ? await repository.ConversionAsync(conversionId, ct) : null);
            var registeredAt = Now; var registeredDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(registeredAt, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
            var previewDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(plan.Preview.ObservadoEmUtc, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
            if (registeredDate != previewDate)
            {
                plan = await Prepare(operation, c, true, ct); InventoryRules.Accept(plan.Preview.Quantidade, c.AceiteQuantizacao);
                registeredAt = Now; registeredDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(registeredAt, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
                if (registeredDate != DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(plan.Preview.ObservadoEmUtc, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime)) throw new InventoryException(409, "conflito_transitorio", "Data operacional mudou; confira nova prévia.");
            }
            var beforeLot = InventoryJson.Write(plan.Lot);
            if (plan.BlockLot && plan.Lot.Situacao != "Bloqueado") plan.Lot.SetState("Bloqueado", plan.Lot.Revisao, registeredAt, registeredDate);
            var snapshot = InventoryJson.Write(new { Pedido = c, Item = plan.Item, Lote = plan.Lot, Locais = plan.Legs.Select(x => x.Local).ToArray(), Conversao = referenceConversion, Perfil = profile, Previa = plan.Preview });
            var entity = new EventoEstoque(id, sequence, operation, actor, c.ResponsavelId, registeredAt, registeredDate, c.DataObservada, CatalogRules.Text(c.Motivo, 2000, "Motivo"), CatalogRules.Optional(c.Documento, 1000), c.EventoReferenciaId, InventoryRules.Algorithm, snapshot);
            repository.Add(entity); var movements = new List<MovimentoView>();
            int ordinal = 0;
            foreach (var leg in plan.Legs)
            {
                var revision = leg.Position.Revisao; if (revision == 0) repository.Add(leg.Position);
                leg.Position.SetBalance(leg.After, revision);
                var movement = new MovimentoEstoque(Guid.NewGuid(), id, ++ordinal, plan.Lot.Id, leg.Local.Id, plan.Item.Id, plan.Lot.Unidade, leg.After > leg.Before ? "Entrada" : "Saida", Math.Abs(leg.After - leg.Before), plan.Preview.Quantidade.Declarada, plan.Preview.Quantidade.UnidadeDeclarada, plan.Preview.Quantidade.Residuo, leg.Before, leg.After, plan.Lot.ConversaoItemId, plan.Lot.PerfilNutricionalId, snapshot, operation);
                repository.Add(movement); movements.Add(View(movement));
                // Separate saves intentionally permit a failure barrier between debit and credit. The outer transaction keeps them atomic.
                await repository.SaveAsync(ct);
            }
            if (beforeLot != InventoryJson.Write(plan.Lot)) await Audit(plan.Lot.Id, "Lote", "BloqueioPorMovimento", actor, c.Motivo, JsonDocument.Parse(beforeLot).RootElement, plan.Lot, ct);
            await Audit(id, "Evento", operation, actor, c.Motivo, null, new { Evento = entity, Movimentos = movements }, ct);
            var view = new EventoView(id, sequence.ToString(CultureInfo.InvariantCulture), operation, actor, c.ResponsavelId, entity.CreatedAtUtc, entity.DataOperacional, c.DataObservada, entity.Motivo, entity.Documento, c.EventoReferenciaId, entity.Algoritmo, snapshot, movements);
            return new(new { Evento = view, Previa = plan.Preview }, 201, $"/api/v1/estoque/movimentos/{id}", null, id);
        }, ct);
    }
    private static MovimentoView View(MovimentoEstoque m) => new(m.Id, m.Ordinal, m.LoteId, m.LocalId, m.ItemId, m.Unidade, m.Sentido, CatalogRules.Format(m.Quantidade), m.QuantidadeDeclarada, m.UnidadeDeclarada, m.Residuo, CatalogRules.Format(m.SaldoAnterior), CatalogRules.Format(m.SaldoPosterior), m.ConversaoItemId, m.PerfilNutricionalId, m.SnapshotJson);
    public Task<InventoryPage<LocalView>> LocaisAsync(InventoryQuery q, Guid actor, CancellationToken ct) => Read(actor, () => { q.Validate(); return repository.LocaisAsync(q, ct); }, ct);
    public Task<InventoryPage<LoteView>> LotesAsync(InventoryQuery q, Guid actor, CancellationToken ct) => Read(actor, () => { q.Validate(); return repository.LotesAsync(q, ct); }, ct);
    public Task<InventoryPage<SaldoView>> SaldosAsync(InventoryQuery q, Guid actor, CancellationToken ct) => Read(actor, () => { q.Validate(); return repository.SaldosAsync(q, Today, ct); }, ct);
    public Task<InventoryPage<EventoView>> EventosAsync(InventoryQuery q, Guid actor, CancellationToken ct) => Read(actor, () => { q.Validate(); return repository.EventosAsync(q, ct); }, ct);
    public Task<InventoryPage<ReconciliacaoView>> ReconcileAsync(InventoryQuery q, Guid actor, CancellationToken ct) => Read(actor, () => { q.Validate(); return repository.ReconcileAsync(q, ct); }, ct);
    public Task<InventoryPage<ResponsavelView>> ResponsaveisAsync(InventoryQuery q, Guid actor, CancellationToken ct) => Read(actor, () => { q.Validate(); return repository.ResponsaveisAsync(q, ct); }, ct);
    public Task<InventoryPage<HistoricoView>> HistoryAsync(Guid id, InventoryQuery q, Guid actor, CancellationToken ct) => Read(actor, async () => { q.Validate(); if (await repository.LocalAsync(id, false, ct) is null && await repository.LoteAsync(id, false, ct) is null) throw Missing(); return await repository.HistoryAsync(id, q, ct); }, ct);
    public Task<LocalView> GetLocalAsync(Guid id, Guid actor, CancellationToken ct) => Read(actor, async () => await repository.LocalViewAsync(id, ct) ?? throw Missing(), ct);
    public Task<LoteView> GetLoteAsync(Guid id, Guid actor, CancellationToken ct) => Read(actor, async () => await repository.LoteViewAsync(id, ct) ?? throw Missing(), ct);
    public Task<SaldoView> GetSaldoAsync(Guid lot, Guid local, Guid actor, CancellationToken ct) => Read(actor, async () => await repository.SaldoAsync(lot, local, Today, ct) ?? throw Missing(), ct);
    public Task<EventoView> GetEventoAsync(Guid id, Guid actor, CancellationToken ct) => Read(actor, async () => await repository.EventoAsync(id, ct) ?? throw Missing(), ct);
    public Task<ResponsavelView> GetResponsavelAsync(Guid id, Guid actor, CancellationToken ct) => Read(actor, async () => await repository.ResponsibleAsync(id, false, ct) ?? throw Missing(), ct);
    public Task<ConversionView> GetConversionAsync(Guid id, Guid actor, CancellationToken ct) => Read(actor, async () =>
    { var c = await repository.ConversionAsync(id, ct) ?? throw Missing(); return new ConversionView(c.Id, c.ItemId, c.Numero, c.Origem, c.Destino, CatalogRules.Format(c.Fator), c.Fonte, c.Metodo, c.DataFonte, c.Contexto, c.ReferenciaAmostra, c.Proveniencia); }, ct);
}
