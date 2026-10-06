using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;

namespace GenSW.Application.Formulation;

public sealed class FormulationService(IFormulationRepository repository, TimeProvider clock)
{
    private static FormulationException Missing() => new(404, "registro_nao_encontrado", "Registro não encontrado.");
    private static void Active(bool active) { if (!active) throw new FormulationException(409, "registro_inativo", "Referência inativa impede novo uso; histórico continua disponível."); }
    public static ProfileView View(NutritionProfile p) => new(p.Id, p.ItemId, p.Numero, p.Revisao, p.Estado, p.AutorId, p.CreatedAtUtc, p.PublishedAtUtc, p.Data());
    public static RecipeVersionView View(RecipeVersion v) => new(v.Id, v.ReceitaId, v.Numero, v.Revisao, v.Estado, v.AutorId, v.CreatedAtUtc, v.PublishedAtUtc, v.Data());
    private async Task<T> Mutate<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var transaction = await repository.BeginAsync(ct);
        var value = await work(); await repository.SaveAsync(ct); await transaction.CommitAsync(ct); return value;
    }
    private void Audit(Guid id, string type, string operation, Guid actor, object? before, object after) => repository.Add(
        new CatalogAudit(Guid.NewGuid(), id, type, operation, actor, clock.GetUtcNow(), FormulationJson.Write(before), FormulationJson.Write(after)));
    public async Task<Item> GetItemAsync(Guid id, CancellationToken ct) => await repository.ItemAsync(id, false, ct) ?? throw Missing();
    public Task<CatalogPage<Item>> ItemsAsync(CatalogQuery q, CancellationToken ct) { q.Validate(); return repository.ItemsAsync(q, ct); }
    public Task<CatalogPage<CategoriaItem>> CategoriesAsync(CatalogQuery q, CancellationToken ct) { q.Validate(); return repository.CategoriesAsync(q, ct); }
    public async Task<CatalogPage<ConversaoItem>> ConversionsAsync(Guid item, CatalogQuery q, CancellationToken ct) { await GetItemAsync(item, ct); q.Validate(); return await repository.ConversionsAsync(item, q, ct); }
    public async Task<CatalogPage<CatalogAudit>> HistoryAsync(Guid id, CatalogQuery q, CancellationToken ct) { q.Validate(); return await repository.HistoryAsync(id, q, ct); }
    private async Task CategoryForAsync(ItemData data, CancellationToken ct)
    { if (data.CategoriaId is { } id) Active((await repository.CategoryAsync(id, false, ct) ?? throw Missing()).Ativo); }
    public Task<Item> CreateItemAsync(ItemData data, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        await CategoryForAsync(data, ct); var item = Item.Create(data, clock.GetUtcNow()); repository.Add(item); Audit(item.Id, "Item", "Criacao", actor, null, item); return item;
    }, ct);
    public Task<Item> UpdateItemAsync(Guid id, ItemCommand command, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        var item = await repository.ItemAsync(id, true, ct) ?? throw Missing(); var before = FormulationJson.Write(item);
        if (item.CategoriaId != command.Conteudo.CategoriaId) await CategoryForAsync(command.Conteudo, ct);
        item.Update(command.Conteudo, command.VersaoEsperada, clock.GetUtcNow()); Audit(id, "Item", "Edicao", actor, JsonDocument.Parse(before).RootElement, item); return item;
    }, ct);
    public Task<Item> ItemStatusAsync(Guid id, StatusCommand command, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        var item = await repository.ItemAsync(id, true, ct) ?? throw Missing(); var before = FormulationJson.Write(item);
        item.SetActive(command.Ativo, command.VersaoEsperada, clock.GetUtcNow()); Audit(id, "Item", "Status", actor, JsonDocument.Parse(before).RootElement, item); return item;
    }, ct);
    public Task<CategoriaItem> CreateCategoryAsync(CategoryCommand command, Guid actor, CancellationToken ct) => Mutate(() =>
    {
        var item = CategoriaItem.Create(command.Nome); repository.Add(item); Audit(item.Id, "Categoria", "Criacao", actor, null, item); return Task.FromResult(item);
    }, ct);
    public Task<CategoriaItem> UpdateCategoryAsync(Guid id, CategoryCommand command, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        var item = await repository.CategoryAsync(id, true, ct) ?? throw Missing(); var before = FormulationJson.Write(item);
        item.Update(command.Nome, command.Ativo, command.VersaoEsperada); Audit(id, "Categoria", "EdicaoStatus", actor, JsonDocument.Parse(before).RootElement, item); return item;
    }, ct);
    public Task<ConversaoItem> CreateConversionAsync(Guid id, ConversionData data, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        var item = await repository.ItemAsync(id, true, ct) ?? throw Missing(); Active(item.Ativo);
        var conversion = ConversaoItem.Create(id, await repository.NextConversionAsync(id, ct), data, actor, clock.GetUtcNow());
        repository.Add(conversion); item.FixUnit(); Audit(conversion.Id, "Conversao", "Publicacao", actor, null, conversion); return conversion;
    }, ct);
    private async Task SpeciesAsync(Guid? species, CancellationToken ct)
    { if (species is { } id && !await repository.SpeciesExistsAsync(id, ct)) throw Missing(); }
    public async Task<ProfileView> GetProfileAsync(Guid id, CancellationToken ct) => View(await repository.ProfileAsync(id, false, ct) ?? throw Missing());
    public async Task<CatalogPage<ProfileView>> ProfilesAsync(Guid item, CatalogQuery q, CancellationToken ct)
    { await GetItemAsync(item, ct); q.Validate(); var page = await repository.ProfilesAsync(item, q, ct); return new(page.Items.Select(View).ToArray(), page.Page, page.PageSize, page.TotalItems, page.TotalPages); }
    public Task<ProfileView> CreateProfileAsync(Guid itemId, ProfileData data, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        Active((await GetItemAsync(itemId, ct)).Ativo); await SpeciesAsync(data.EspecieId, ct);
        var profile = NutritionProfile.Create(itemId, await repository.NextProfileAsync(itemId, ct), data, actor, clock.GetUtcNow());
        repository.Add(profile); Audit(profile.Id, "Perfil", "Criacao", actor, null, View(profile)); return View(profile);
    }, ct);
    public Task<ProfileView> UpdateProfileAsync(Guid id, ProfileCommand command, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        var p = await repository.ProfileAsync(id, true, ct) ?? throw Missing(); var before = View(p);
        Active((await GetItemAsync(p.ItemId, ct)).Ativo); await SpeciesAsync(command.Conteudo.EspecieId, ct);
        p.Update(command.Conteudo, command.VersaoEsperada); Audit(id, "Perfil", "Edicao", actor, before, View(p)); return View(p);
    }, ct);
    public Task<ProfileView> ProfileStateAsync(Guid id, int expected, bool publish, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        var p = await repository.ProfileAsync(id, true, ct) ?? throw Missing(); var before = View(p);
        if (publish) { var item = await repository.ItemAsync(p.ItemId, true, ct) ?? throw Missing(); Active(item.Ativo); p.Publish(expected, clock.GetUtcNow()); item.FixUnit(); }
        else p.Inactivate(expected);
        Audit(id, "Perfil", publish ? "Publicacao" : "Inativacao", actor, before, View(p)); return View(p);
    }, ct);
    public async Task<Recipe> GetRecipeAsync(Guid id, CancellationToken ct) => await repository.RecipeAsync(id, false, ct) ?? throw Missing();
    public Task<CatalogPage<Recipe>> RecipesAsync(CatalogQuery q, CancellationToken ct) { q.Validate(); return repository.RecipesAsync(q, ct); }
    public Task<Recipe> CreateRecipeAsync(RecipeHeader data, Guid actor, CancellationToken ct) => Mutate(() =>
    { var r = Recipe.Create(data, clock.GetUtcNow()); repository.Add(r); Audit(r.Id, "Receita", "Criacao", actor, null, r); return Task.FromResult(r); }, ct);
    public Task<Recipe> UpdateRecipeAsync(Guid id, RecipeCommand command, Guid actor, CancellationToken ct) => Mutate(async () =>
    { var r = await repository.RecipeAsync(id, true, ct) ?? throw Missing(); var before = FormulationJson.Write(r); r.Update(command.Conteudo, command.VersaoEsperada); Audit(id, "Receita", "Edicao", actor, JsonDocument.Parse(before).RootElement, r); return r; }, ct);
    public Task<Recipe> RecipeStatusAsync(Guid id, StatusCommand command, Guid actor, CancellationToken ct) => Mutate(async () =>
    { var r = await repository.RecipeAsync(id, true, ct) ?? throw Missing(); var before = FormulationJson.Write(r); r.SetActive(command.Ativo, command.VersaoEsperada); Audit(id, "Receita", "Status", actor, JsonDocument.Parse(before).RootElement, r); return r; }, ct);
    public async Task<RecipeVersionView> GetVersionAsync(Guid id, CancellationToken ct) => View(await repository.VersionAsync(id, false, ct) ?? throw Missing());
    public async Task<CatalogPage<RecipeVersionView>> VersionsAsync(Guid? recipe, CatalogQuery q, CancellationToken ct)
    { q.Validate(); if (recipe is { } id) await GetRecipeAsync(id, ct); var p = await repository.VersionsAsync(recipe, q, ct); return new(p.Items.Select(View).ToArray(), p.Page, p.PageSize, p.TotalItems, p.TotalPages); }
    public Task<RecipeVersionView> CreateVersionAsync(Guid recipe, RecipeData data, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        Active((await GetRecipeAsync(recipe, ct)).Ativo); data.Validate(false); await ReferencesAsync(data, ct);
        var v = RecipeVersion.Create(recipe, await repository.NextVersionAsync(recipe, ct), data, actor, clock.GetUtcNow());
        repository.Add(v); Audit(v.Id, "ReceitaVersao", "Criacao", actor, null, View(v)); return View(v);
    }, ct);
    public Task<RecipeVersionView> UpdateVersionAsync(Guid id, RecipeVersionCommand command, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        var v = await repository.VersionAsync(id, true, ct) ?? throw Missing(); var before = View(v);
        Active((await GetRecipeAsync(v.ReceitaId, ct)).Ativo); command.Conteudo.Validate(false); await ReferencesAsync(command.Conteudo, ct);
        v.Update(command.Conteudo, command.VersaoEsperada); Audit(id, "ReceitaVersao", "Edicao", actor, before, View(v)); return View(v);
    }, ct);
    public Task<RecipeVersionView> VersionStateAsync(Guid id, int expected, bool publish, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        var v = await repository.VersionAsync(id, true, ct) ?? throw Missing(); var before = View(v);
        if (publish)
        {
            CatalogRules.Expected(v.Revisao, expected); Active((await GetRecipeAsync(v.ReceitaId, ct)).Ativo);
            var data = v.Data(); data.Validate(true); await ReferencesAsync(data, ct);
            await GraphAsync(v.ReceitaId, data, [], new Traversal(), ct); await SimpleMassAsync(data, ct);
            v.Publish(expected, clock.GetUtcNow());
            foreach (var line in data.Entradas) repository.Add(new RecipeReference(Guid.NewGuid(), v.Id, line.ItemId, line.PerfilId, line.ConversaoId, line.SubReceitaVersaoId));
            foreach (var line in data.Saidas) repository.Add(new RecipeReference(Guid.NewGuid(), v.Id, line.ItemId, line.PerfilId, line.ConversaoId, null));
            foreach (var itemId in data.Entradas.Select(x => x.ItemId).Concat(data.Saidas.Select(x => x.ItemId)).Distinct()) (await repository.ItemAsync(itemId, true, ct))!.FixUnit();
        }
        else v.Inactivate(expected);
        Audit(id, "ReceitaVersao", publish ? "Publicacao" : "Inativacao", actor, before, View(v)); return View(v);
    }, ct);
    private async Task SelectedAsync(Guid item, Guid? profile, Guid? conversion, CancellationToken ct)
    {
        if (profile is { } p)
        { var value = await repository.ProfileAsync(p, false, ct) ?? throw Missing(); if (value.ItemId != item) throw new ArgumentException("Perfil pertence a outro item."); Active(value.Estado == "Publicado"); }
        if (conversion is { } c && (await repository.ConversionAsync(c, ct) ?? throw Missing()).ItemId != item) throw new ArgumentException("Conversão pertence a outro item.");
    }
    private async Task ReferencesAsync(RecipeData data, CancellationToken ct)
    {
        foreach (var line in data.Entradas)
        {
            var item = await GetItemAsync(line.ItemId, ct); Active(item.Ativo); Active(item.PodeEntrar);
            if (item.Classe != line.Papel) throw new ArgumentException("Papel da linha deve corresponder à classe do material.");
            await SelectedAsync(item.Id, line.PerfilId, line.ConversaoId, ct);
            if (line.SubReceitaVersaoId is { } sub)
            {
                var version = await GetVersionAsync(sub, ct); Active(version.Estado == "Publicado"); Active((await GetRecipeAsync(version.ReceitaId, ct)).Ativo);
                var output = version.Conteudo.Saidas.SingleOrDefault(x => x.Id == line.SubSaidaId) ?? throw new ArgumentException("Saída da sub-receita não encontrada.");
                if (output.ItemId != line.ItemId) throw new ArgumentException("Sub-receita deve produzir o mesmo ItemId selecionado.");
                if (CatalogRules.Dimension(output.Unidade) != CatalogRules.Dimension(line.Unidade)) throw new ArgumentException("Quantidade da sub-receita exige mesma dimensão da saída escolhida.");
            }
        }
        foreach (var line in data.Saidas)
        { var item = await GetItemAsync(line.ItemId, ct); Active(item.Ativo); Active(item.PodeProduzir); await SelectedAsync(item.Id, line.PerfilId, line.ConversaoId, ct); }
    }
    private sealed class Traversal { public int Lines; }
    private async Task GraphAsync(Guid recipe, RecipeData data, IReadOnlyList<Guid> path, Traversal traversal, CancellationToken ct)
    {
        if (path.Contains(recipe)) throw new ArgumentException("Ciclo de receitas: dependência transitiva da mesma ReceitaId.");
        if (path.Count >= 10) throw new ArgumentException("Profundidade máxima de sub-receitas: 10.");
        traversal.Lines += data.Entradas.Count;
        if (traversal.Lines > 500) throw new ArgumentException("Limite de 500 linhas expandidas excedido.");
        foreach (var sub in data.Entradas.Where(x => x.SubReceitaVersaoId.HasValue))
        { var v = await GetVersionAsync(sub.SubReceitaVersaoId!.Value, ct); await ReferencesAsync(v.Conteudo, ct); await GraphAsync(v.ReceitaId, v.Conteudo, path.Append(recipe).ToArray(), traversal, ct); }
    }
    private async Task<decimal?> MassAsync(Guid item, string quantity, string unit, Guid? conversion, CancellationToken ct)
    {
        var q = CatalogRules.Quantity(quantity, unit);
        if (CatalogRules.Dimension(unit) == "massa") return CatalogRules.Canonical(q, unit);
        if (conversion is null) return null;
        var c = await repository.ConversionAsync(conversion.Value, ct) ?? throw Missing();
        if (c.ItemId != item) throw new ArgumentException("Conversão pertence a outro item.");
        return c.ToKg(q, unit);
    }
    private async Task SimpleMassAsync(RecipeData data, CancellationToken ct)
    {
        if (data.Tipo != "MisturaSimples") return;
        var scaled = data.Scale(data.TamanhoReferencia, data.UnidadeReferencia);
        decimal sum = 0m;
        foreach (var line in scaled.Entradas.Where(RecipeData.Incorporated))
            sum += await MassAsync(line.ItemId, line.Quantidade, line.Unidade, line.ConversaoId, ct) ?? throw new ArgumentException("Mistura simples exige massa conhecida de todas as entradas.");
        var output = scaled.Saidas.Single();
        var outputMass = await MassAsync(output.ItemId, output.Quantidade, output.Unidade, output.ConversaoId, ct);
        if (sum != outputMass) throw new ArgumentException("Mistura simples exige saída igual à massa incorporada; use Processamento para perdas/rendimento diferente.");
    }
    public async Task<RecipeData> ScaleAsync(Guid id, string size, string unit, CancellationToken ct) => (await GetVersionAsync(id, ct)).Conteudo.Scale(size, unit);
    public async Task<SimulationView> GetSimulationAsync(Guid id, CancellationToken ct)
    { var s = await repository.SnapshotAsync(id, ct) ?? throw Missing(); if (s.Tipo != "Simulacao") throw Missing(); return new(s.Id, s.AutorId, s.CreatedAtUtc, FormulationJson.Read<SimulationData>(s.ConteudoJson)); }
    public Task<CatalogPage<SnapshotSummary>> SnapshotsAsync(string type, CatalogQuery q, CancellationToken ct) { q.Validate(); return repository.SnapshotsAsync(type, q, ct); }
    private static string CanonicalHash<T>(T value)
    {
        // Normalize only numeric contract fields: numeric-looking source identifiers remain distinct.
        using var doc = JsonDocument.Parse(FormulationJson.Write(value));
        string Canonical(JsonElement e, string? field = null) => e.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(",", e.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value, p.Name))) + "}",
            JsonValueKind.Array => "[" + string.Join(",", e.EnumerateArray().Select(x => Canonical(x))) + "]",
            JsonValueKind.String when field is "tamanho" or "tamanhoReferencia" or "quantidade" or "inclusaoMinima" or "inclusaoMaxima" or "massaLiquidaKg" or "massaDrenadaKg" or "massaBrutaKg" or "minimo" or "maximo" or "fator"
                && System.Text.RegularExpressions.Regex.IsMatch(e.GetString()!, @"^(0|[1-9][0-9]{0,13})(\.[0-9]{1,6})?$") => JsonSerializer.Serialize(CatalogRules.Format(CatalogRules.Decimal(e.GetString()))),
            _ => e.GetRawText()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(doc.RootElement))));
    }
    public Task<SimulationView> SimulateAsync(SimulationCommand command, string key, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        key = CatalogRules.Text(key, 100, "Chave de idempotência"); var hash = CanonicalHash(command);
        if (await repository.ReplayAsync(actor, "Simulacao", key, ct) is { } replay)
        { if (replay.Hash != hash) throw new FormulationException(409, "idempotencia_divergente", "Chave já usada com conteúdo diferente."); return new SimulationView(replay.Id, replay.AutorId, replay.CreatedAtUtc, FormulationJson.Read<SimulationData>(replay.ConteudoJson)); }
        var version = await GetVersionAsync(command.ReceitaVersaoId, ct); Active(version.Estado == "Publicado"); var recipe = await GetRecipeAsync(version.ReceitaId, ct); Active(recipe.Ativo);
        await SpeciesAsync(command.EspecieId, ct); NutritionEngine.ValidateGoals(command.Metas);
        if (command.AnteriorId is { } prior) await GetSimulationAsync(prior, ct);
        var data = command.Variacao ?? version.Conteudo; data.Validate(true); await ReferencesAsync(data, ct);
        await GraphAsync(recipe.Id, data, [], new Traversal(), ct); await SimpleMassAsync(data, ct);
        var scaled = data.Scale(command.Tamanho, command.Unidade);
        var dependencies = new List<RecipeVersionView>(); var conversions = new Dictionary<Guid, ConversaoItem>();
        var inputs = await ResolveAsync(scaled, dependencies, conversions, command.EspecieId, command.Fase, recipe.Nome, ct);
        var problems = new List<string>();
        var calculationInputs = inputs;
        if (scaled.Tipo == "Processamento")
        {
            var main = scaled.Saidas.Single(x => x.Principal);
            calculationInputs = [await ProcessOutputAsync(scaled, main, inputs, conversions, command.EspecieId, command.Fase, ct)];
            if (scaled.Saidas.Count > 1) problems.Add("Nutrição refere-se apenas à saída principal com perfil explícito; distribuição de retenção multissaída não modelada.");
        }
        var result = NutritionEngine.Calculate(calculationInputs, command.Metas, command.EspecieId, command.Fase);
        if (scaled.Tipo == "Processamento") result = result with { Metas = result.Metas.Select(x => x with { ImpossibilidadeLocal = false }).ToArray() };
        else if (scaled.Entradas.All(x => x.SubReceitaVersaoId is null)) result = InclusionBounds(result, scaled);
        foreach (var line in scaled.Entradas.Where(RecipeData.Incorporated))
        {
            var m = await MassAsync(line.ItemId, line.Quantidade, line.Unidade, line.ConversaoId, ct);
            var total = inputs.All(x => x.Papel is "Embalagem" or "Consumivel" || x.MassaKg is not null) ? inputs.Where(x => x.Papel is "Alimentar" or "OutroMaterialIncorporado").Sum(x => Read(x.MassaKg!)) : (decimal?)null;
            if (m.HasValue && total > 0)
            {
                var inclusion = m / total * 100m;
                if (line.InclusaoMinima is { } minimum && inclusion < CatalogRules.Decimal(minimum) || line.InclusaoMaxima is { } maximum && inclusion > CatalogRules.Decimal(maximum))
                    problems.Add($"Linha {line.Id}: inclusão {CatalogRules.Format(inclusion!.Value)}% fora dos limites explícitos.");
            }
        }
        decimal? inputMass = inputs.Where(x => x.Papel is "Alimentar" or "OutroMaterialIncorporado").All(x => x.MassaKg is not null) ? inputs.Where(x => x.Papel is "Alimentar" or "OutroMaterialIncorporado").Sum(x => Read(x.MassaKg!)) : null;
        decimal outputMass = 0, lossMass = 0; bool balanceKnown = inputMass.HasValue;
        foreach (var output in scaled.Saidas) { var m = await MassAsync(output.ItemId, output.Quantidade, output.Unidade, output.ConversaoId, ct); if (m is null) balanceKnown = false; else outputMass += m.Value; }
        foreach (var loss in scaled.Perdas) { if (CatalogRules.Dimension(loss.Unidade) != "massa") balanceKnown = false; else lossMass += CatalogRules.Canonical(CatalogRules.Decimal(loss.Quantidade), loss.Unidade); }
        var mainOutput = scaled.Saidas.Single(x => x.Principal); var mainMass = await MassAsync(mainOutput.ItemId, mainOutput.Quantidade, mainOutput.Unidade, mainOutput.ConversaoId, ct);
        var content = new SimulationData(command, version, new(recipe.Codigo, recipe.Nome, recipe.Finalidade), scaled, inputs, dependencies.DistinctBy(x => x.Id).ToArray(), conversions.Values.Select(c => new ConversionView(c.Id, c.ItemId, c.Numero, c.Origem, c.Destino, CatalogRules.Format(c.Fator), c.Fonte, c.Metodo, c.DataFonte, c.Contexto, c.ReferenciaAmostra, c.Proveniencia)).ToArray(), result,
            mainMass.HasValue && inputMass > 0 ? CatalogRules.Format(mainMass.Value / inputMass.Value * 100m) : null,
            balanceKnown ? CatalogRules.Format(inputMass!.Value - outputMass - lossMass) : null, problems);
        var snapshot = FormulationSnapshot.Create("Simulacao", actor, key, hash, FormulationJson.Write(content), clock.GetUtcNow());
        repository.Add(snapshot); Audit(snapshot.Id, "Simulacao", "Criacao", actor, null, new { snapshot.Id, snapshot.Hash, Motor = NutritionEngine.Version });
        return new SimulationView(snapshot.Id, actor, snapshot.CreatedAtUtc, content);
    }, ct);
    private static decimal Read(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    private static NutritionResult InclusionBounds(NutritionResult result, RecipeData data)
    {
        var goals = result.Metas.Select(goal =>
        {
            if (goal.Meta.Base != "BN" || goal.Meta.Minimo is null || goal.Estado == "Indeterminada") return goal;
            var nutrient = result.Componentes.Single(x => (x.Componente, x.Metodo, x.Contexto) == (goal.Meta.Componente, goal.Meta.Metodo, goal.Meta.Contexto));
            if (nutrient.Contribuicoes.Any(x => x.QuantidadeComponente is null || x.MassaKg is null || Read(x.MassaKg) <= 0)) return goal;
            var choices = nutrient.Contribuicoes.Select(c =>
            {
                var line = data.Entradas.Single(x => x.Id == c.LinhaId);
                return (Concentration: Read(c.QuantidadeComponente!) / Read(c.MassaKg!),
                    Min: line.InclusaoMinima is null ? 0m : CatalogRules.Decimal(line.InclusaoMinima),
                    Max: line.InclusaoMaxima is null ? 100m : CatalogRules.Decimal(line.InclusaoMaxima));
            }).ToArray();
            if (choices.Sum(x => x.Min) > 100m || choices.Sum(x => x.Max) < 100m) return goal;
            decimal left = 100m - choices.Sum(x => x.Min), bound = choices.Sum(x => x.Concentration * x.Min);
            foreach (var choice in choices.OrderByDescending(x => x.Concentration)) { var take = Math.Min(left, choice.Max - choice.Min); bound += take * choice.Concentration; left -= take; }
            bound /= 100m;
            if (NutritionComponents.Normalize(goal.Meta.Componente, CatalogRules.Decimal(goal.Meta.Minimo), goal.Meta.Unidade) > bound)
                return goal with { ImpossibilidadeLocal = true, Motivo = $"Mínimo excede limite convexo local {CatalogRules.Format(bound)} com as inclusões declaradas; sem solver." };
            return goal;
        }).ToArray();
        return result with { Metas = goals };
    }
    private async Task<List<ResolvedIngredient>> ResolveAsync(RecipeData data, List<RecipeVersionView> dependencies,
        Dictionary<Guid, ConversaoItem> conversions, Guid? species, string? phase, string path, CancellationToken ct)
    {
        var inputs = new List<ResolvedIngredient>();
        foreach (var line in data.Entradas)
        {
            var item = await GetItemAsync(line.ItemId, ct); Active(item.Ativo);
            var profile = line.PerfilId is { } p ? await GetProfileAsync(p, ct) : null;
            if (line.ConversaoId is { } c) conversions[c] = await repository.ConversionAsync(c, ct) ?? throw Missing();
            var mass = await MassAsync(line.ItemId, line.Quantidade, line.Unidade, line.ConversaoId, ct);
            if (line.SubReceitaVersaoId is { } subId && profile is null && RecipeData.Incorporated(line))
            {
                var sub = await GetVersionAsync(subId, ct); dependencies.Add(sub);
                var output = sub.Conteudo.Saidas.Single(x => x.Id == line.SubSaidaId);
                var ratio = CatalogRules.Canonical(CatalogRules.Decimal(line.Quantidade), line.Unidade) / CatalogRules.Canonical(CatalogRules.Decimal(output.Quantidade), output.Unidade);
                var size = checked(CatalogRules.Decimal(sub.Conteudo.TamanhoReferencia) * ratio);
                var expanded = sub.Conteudo.Scale(CatalogRules.Format(size), sub.Conteudo.UnidadeReferencia);
                var child = await ResolveAsync(expanded, dependencies, conversions, species, phase, path + " → " + subId, ct);
                if (expanded.Tipo == "MisturaSimples") inputs.AddRange(child.Where(x => x.Papel is "Alimentar" or "OutroMaterialIncorporado"));
                else inputs.Add(await ProcessOutputAsync(expanded, expanded.Saidas.Single(x => x.Id == line.SubSaidaId), child, conversions, species, phase, ct));
                continue;
            }
            inputs.Add(new(line.Id, item.Id, item.Nome, line.Papel, line.Quantidade, line.Unidade, mass.HasValue ? CatalogRules.Format(mass.Value) : null,
                profile?.Id, profile?.Numero, profile?.Conteudo, line.ConversaoId, line.ConversaoId is { } cv ? conversions[cv].Proveniencia : null, path, mass is null ? "Sem conversão aplicável para massa." : null));
        }
        return inputs;
    }
    private async Task<ResolvedIngredient> ProcessOutputAsync(RecipeData data, RecipeOutput output, IReadOnlyList<ResolvedIngredient> inputs,
        Dictionary<Guid, ConversaoItem> conversions, Guid? species, string? phase, CancellationToken ct)
    {
        var item = await GetItemAsync(output.ItemId, ct);
        if (output.ConversaoId is { } c) conversions[c] = await repository.ConversionAsync(c, ct) ?? throw Missing();
        var mass = await MassAsync(output.ItemId, output.Quantidade, output.Unidade, output.ConversaoId, ct);
        var profile = output.PerfilId is { } p ? await GetProfileAsync(p, ct) : null;
        ProfileData? content = profile?.Conteudo;
        if (content is null && data.Saidas.Count == 1 && data.Retencoes.Count > 0 && mass > 0)
        {
            var source = NutritionEngine.Calculate(inputs, [], species, phase); var values = new List<NutritionValue>();
            foreach (var result in source.Componentes)
            {
                var retention = data.Retencoes.SingleOrDefault(x => x.Componente == result.Componente);
                if (retention is null || result.ValorBN is null || source.MassaKg is null || result.Componente == "UMIDADE") continue;
                var value = checked(Read(result.ValorBN) * Read(source.MassaKg) * CatalogRules.Decimal(retention.Fator) / mass.Value);
                if (NutritionComponents.Get(result.Componente).Grandeza == "massa" && value > 1000m) throw new ArgumentException("Estimativa de saída excede concentração física; reveja massa/retensão.");
                values.Add(new(result.Componente, "Conhecido", CatalogRules.Format(value), "Estimado", "BN", result.Unidade, result.Metodo, result.Contexto, Hipotese: retention.Hipotese + "; " + NutritionEngine.Version));
            }
            // Dry matter is an explicit retained component, never inferred from output yield alone.
            if (data.Retencoes.SingleOrDefault(x => x.Componente == "MS") is { } dryRetention && source.MateriaSecaKg is not null)
            {
                values.RemoveAll(x => x.Componente == "MS");
                var dry = Read(source.MateriaSecaKg) * CatalogRules.Decimal(dryRetention.Fator) / mass.Value * 1000m;
                if (dry > 1000m) throw new ArgumentException("MS estimada excede massa da saída.");
                values.Add(new("MS", "Conhecido", CatalogRules.Format(dry), "Estimado", "BN", "g/kg", "RetencaoMS", "", Hipotese: dryRetention.Hipotese));
            }
            content = new("Estimativa explícita da saída", "Snapshot das entradas e parâmetros", "Retenção por componente", "Saída única", "Simulação", "Hipóteses da receita", species, phase, null, null, values);
        }
        return new(output.Id, item.Id, item.Nome, item.Classe, output.Quantidade, output.Unidade, mass.HasValue ? CatalogRules.Format(mass.Value) : null,
            profile?.Id, profile?.Numero, content, output.ConversaoId, output.ConversaoId is { } cv ? conversions[cv].Proveniencia : null, "Saída processada", content is null ? "Processamento sem perfil de saída ou retenções explícitas: nutrição não calculável." : null);
    }
    public Task<ComparisonView> CompareAsync(ComparisonCommand command, string key, Guid actor, CancellationToken ct) => Mutate(async () =>
    {
        key = CatalogRules.Text(key, 100, "Chave de idempotência");
        if (command.Base is not ("BN" or "MS") || command.SimulacaoIds.Count is < 2 or > 10 || command.SimulacaoIds.Distinct().Count() != command.SimulacaoIds.Count) throw new ArgumentException("Comparação exige 2–10 simulações distintas e base BN/MS.");
        var hash = CanonicalHash(command);
        if (await repository.ReplayAsync(actor, "Comparacao", key, ct) is { } replay)
        { if (replay.Hash != hash) throw new FormulationException(409, "idempotencia_divergente", "Chave já usada com conteúdo diferente."); return new ComparisonView(replay.Id, replay.CreatedAtUtc, FormulationJson.Read<ComparisonData>(replay.ConteudoJson)); }
        var simulations = new List<SimulationView>(); foreach (var id in command.SimulacaoIds) simulations.Add(await GetSimulationAsync(id, ct));
        var keys = simulations.SelectMany(x => x.Conteudo.Resultado.Componentes).Select(x => (x.Componente, x.Metodo, x.Contexto, x.EspecieId, x.Fase)).Distinct();
        var rows = keys.Select(k =>
        {
            var cells = simulations.Select(s =>
            {
                var r = s.Conteudo.Resultado.Componentes.SingleOrDefault(x => (x.Componente, x.Metodo, x.Contexto, x.EspecieId, x.Fase) == k);
                return new ComparisonCell(s.Id, command.Base == "BN" ? r?.ValorBN : r?.ValorMS, r?.Estado ?? "NaoComparavel", (command.Base == "BN" ? r?.Estimado : r?.EstimadoMS) ?? false);
            }).ToArray();
            return new ComparisonRow(k.Componente, k.Metodo, k.Contexto, k.EspecieId, k.Fase, NutritionComponents.Get(k.Componente).Grandeza == "energia" ? "MJ/kg" : "g/kg", cells.All(x => x.Valor is not null), cells);
        }).ToArray();
        var content = new ComparisonData(command, simulations, rows); var snapshot = FormulationSnapshot.Create("Comparacao", actor, key, hash, FormulationJson.Write(content), clock.GetUtcNow());
        repository.Add(snapshot); Audit(snapshot.Id, "Comparacao", "Criacao", actor, null, new { snapshot.Id, snapshot.Hash }); return new ComparisonView(snapshot.Id, snapshot.CreatedAtUtc, content);
    }, ct);
    public async Task<ComparisonView> GetComparisonAsync(Guid id, CancellationToken ct)
    { var s = await repository.SnapshotAsync(id, ct) ?? throw Missing(); if (s.Tipo != "Comparacao") throw Missing(); return new(s.Id, s.CreatedAtUtc, FormulationJson.Read<ComparisonData>(s.ConteudoJson)); }
}
