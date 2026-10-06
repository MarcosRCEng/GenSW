using GenSW.Domain.Catalog;

namespace GenSW.Domain.Formulation;

public sealed record ResolvedIngredient(Guid LinhaId, Guid ItemId, string Nome, string Papel, string Quantidade,
    string Unidade, string? MassaKg, Guid? PerfilId, int? PerfilNumero, ProfileData? Perfil,
    Guid? ConversaoId, string? OrigemConversao, string? Caminho, string? Problema);
public sealed record NutritionGoal(string Componente, string Metodo, string Contexto, string Base, string Unidade,
    string? Minimo, string? Maximo, string Origem, string? Fonte, bool AceitarEstimados = true);
public sealed record NutrientContribution(Guid LinhaId, string Item, string? MassaKg, string? QuantidadeComponente,
    string? Origem, string? Fonte, string? Motivo);
public sealed record NutrientResult(string Componente, string Metodo, string Contexto, Guid? EspecieId, string? Fase,
    string Unidade, string Estado, string? ValorBN, string? ValorMS, string? ContribuicaoConhecidaBN,
    string? CoberturaPercentual, bool Estimado, IReadOnlyList<NutrientContribution> Contribuicoes, string? KcalBN = null, bool EstimadoMS = false);
public sealed record GoalResult(NutritionGoal Meta, string Estado, string Motivo, bool ImpossibilidadeLocal);
public sealed record NutritionResult(string Motor, string? MassaKg, string? MateriaSecaKg, string? UmidadePercentual,
    IReadOnlyList<NutrientResult> Componentes, IReadOnlyList<GoalResult> Metas, IReadOnlyList<string> Avisos);

public static class NutritionEngine
{
    public const string Version = "gensw-formulation/1.0";
    private static decimal Read(string? value) => decimal.Parse(value!, System.Globalization.CultureInfo.InvariantCulture);
    public static void ValidateGoals(IReadOnlyList<NutritionGoal> goals)
    {
        if (goals.Count > 100) throw new ArgumentException("Até 100 metas.");
        foreach (var goal in goals)
        {
            NutritionComponents.Get(goal.Componente);
            if (goal.Base is not ("BN" or "MS")) throw new ArgumentException("Meta exige base BN/MS.");
            if (goal.Origem is not ("InformadaUsuario" or "ReferenciaTecnica")) throw new ArgumentException("Origem da meta inválida.");
            if (goal.Origem == "ReferenciaTecnica") CatalogRules.Text(goal.Fonte, 1000, "Fonte da meta técnica");
            CatalogRules.Text(goal.Metodo, 500, "Método da meta");
            if (NutritionComponents.Get(goal.Componente).Grandeza == "energia") CatalogRules.Text(goal.Contexto, 500, "Contexto energético da meta");
            var min = goal.Minimo is null ? (decimal?)null : NutritionComponents.Normalize(goal.Componente, CatalogRules.Decimal(goal.Minimo), goal.Unidade);
            var max = goal.Maximo is null ? (decimal?)null : NutritionComponents.Normalize(goal.Componente, CatalogRules.Decimal(goal.Maximo), goal.Unidade);
            if ((!min.HasValue && !max.HasValue) || min > max) throw new ArgumentException("Meta exige mínimo/máximo coerentes; mínimo não pode exceder máximo.");
        }
    }
    public static NutritionResult Calculate(IReadOnlyList<ResolvedIngredient> all, IReadOnlyList<NutritionGoal> goals,
        Guid? species = null, string? phase = null)
    {
        ValidateGoals(goals);
        try { return CalculateChecked(all, goals, species, phase); }
        catch (OverflowException) { throw new ArgumentException("Cálculo excedeu o limite decimal. Reduza a escala explicitamente."); }
    }
    private static NutritionResult CalculateChecked(IReadOnlyList<ResolvedIngredient> all, IReadOnlyList<NutritionGoal> goals, Guid? species, string? phase)
    {
        var inputs = all.Where(x => x.Papel is "Alimentar" or "OutroMaterialIncorporado").ToArray();
        if (inputs.Length == 0) throw new ArgumentException("Simulação exige ao menos uma entrada incorporada.");
        decimal? mass = inputs.All(x => x.MassaKg is not null) ? inputs.Sum(x => Read(x.MassaKg)) : null;
        if (mass == 0) throw new ArgumentException("Massa incorporada deve ser positiva.");
        decimal? dry = mass.HasValue && inputs.All(x => x.Perfil?.DryFraction() is not null &&
            (x.Perfil.EspecieId is null || x.Perfil.EspecieId == species) && (string.IsNullOrWhiteSpace(x.Perfil.Fase) || x.Perfil.Fase == phase))
            ? inputs.Sum(x => checked(Read(x.MassaKg) * x.Perfil!.DryFraction()!.Value)) : null;
        var keys = inputs.SelectMany(x => x.Perfil?.Valores ?? []).Select(v => (v.Componente, v.Metodo, v.Contexto))
            .Concat(goals.Select(x => (x.Componente, x.Metodo, x.Contexto))).Distinct().ToArray();
        var results = new List<NutrientResult>();
        foreach (var key in keys)
        {
            var contributions = new List<NutrientContribution>();
            decimal known = 0m, covered = 0m; bool complete = mass.HasValue, estimated = false;
            foreach (var input in inputs)
            {
                var profile = input.Perfil;
                var value = profile?.Valores.SingleOrDefault(v => (v.Componente, v.Metodo, v.Contexto) == key);
                var compatible = profile is not null && (profile.EspecieId is null || profile.EspecieId == species) &&
                    (string.IsNullOrWhiteSpace(profile.Fase) || profile.Fase == phase);
                // Energy without an explicit matching context is never treated as universally applicable.
                if (NutritionComponents.Get(key.Componente).Grandeza == "energia") compatible = profile is not null && profile.EspecieId == species && profile.Fase == phase && !string.IsNullOrWhiteSpace(key.Contexto);
                string? reason = input.Problema;
                decimal? concentration = null;
                if (input.MassaKg is null) reason ??= "Massa desconhecida: selecione conversão explícita aplicável.";
                else if (value is null) reason = "Componente/método/contexto ausente.";
                else if (!compatible) reason = "Aplicabilidade de espécie/fase incompatível.";
                else if (value.Estado != "Conhecido" || value.Qualificador != "Pontual") reason = value.Motivo ?? $"{value.Estado}/{value.Qualificador}: total indeterminado.";
                else
                {
                    concentration = NutritionComponents.Normalize(value.Componente, Read(value.Valor), value.Unidade);
                    if (value.Base == "MS")
                    {
                        if (profile!.DryFraction() is { } fraction && fraction > 0) concentration *= fraction;
                        else { concentration = null; reason = "MS da amostra desconhecida ou nula; conversão bloqueada."; }
                    }
                }
                decimal? quantity = concentration.HasValue && input.MassaKg is not null ? checked(concentration.Value * Read(input.MassaKg)) : null;
                if (quantity is { } q)
                {
                    known = checked(known + q); covered += Read(input.MassaKg);
                    estimated |= value!.Origem == "Estimado" || input.OrigemConversao == "Estimado" ||
                        (value.Base == "MS" && profile!.Valores.Any(v => (v.Componente is "MS" or "UMIDADE") && v.Origem == "Estimado"));
                }
                else complete = false;
                contributions.Add(new(input.LinhaId, input.Nome, input.MassaKg, quantity.HasValue ? CatalogRules.Format(quantity.Value) : null,
                    value?.Origem, value?.Fonte ?? profile?.Fonte, quantity.HasValue ? null : reason));
            }
            var state = complete ? "Completo" : contributions.Any(x => x.QuantidadeComponente is not null) ? "Parcial" : "Indeterminado";
            string? bn = complete && mass > 0 ? CatalogRules.Format(known / mass.Value) : null;
            string? ms = complete && dry > 0 ? CatalogRules.Format(known / dry.Value) : null;
            var energy = NutritionComponents.Get(key.Componente).Grandeza == "energia";
            results.Add(new(key.Componente, key.Metodo, key.Contexto, species, phase, energy ? "MJ/kg" : "g/kg", state,
                bn, ms, mass > 0 ? CatalogRules.Format(known / mass.Value) : null,
                mass > 0 ? CatalogRules.Format(covered / mass.Value * 100m) : null, estimated, contributions,
                energy && bn is not null ? CatalogRules.Format(decimal.Parse(bn, System.Globalization.CultureInfo.InvariantCulture) / 0.004184m) : null,
                estimated || inputs.Any(x => x.Perfil?.Valores.Any(v => (v.Componente is "MS" or "UMIDADE") && v.Origem == "Estimado") == true)));
        }
        var goalResults = goals.Select(goal =>
        {
            var result = results.Single(x => (x.Componente, x.Metodo, x.Contexto) == (goal.Componente, goal.Metodo, goal.Contexto));
            var raw = goal.Base == "BN" ? result.ValorBN : result.ValorMS;
            if (raw is null || (!goal.AceitarEstimados && (goal.Base == "MS" ? result.EstimadoMS : result.Estimado))) return new GoalResult(goal, "Indeterminada", "Dados compatíveis incompletos ou estimativas excluídas pela política.", false);
            var actual = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
            var minimum = goal.Minimo is null ? (decimal?)null : NutritionComponents.Normalize(goal.Componente, CatalogRules.Decimal(goal.Minimo), goal.Unidade);
            var maximum = goal.Maximo is null ? (decimal?)null : NutritionComponents.Normalize(goal.Componente, CatalogRules.Decimal(goal.Maximo), goal.Unidade);
            bool passes = (!minimum.HasValue || actual >= minimum) && (!maximum.HasValue || actual <= maximum);
            // Convex bound applies only to a complete simple BN mixture with these selected ingredients.
            bool impossible = false;
            if (goal.Base == "BN" && minimum.HasValue && result.Contribuicoes.All(x => x.QuantidadeComponente is not null && decimal.Parse(x.MassaKg!, System.Globalization.CultureInfo.InvariantCulture) > 0))
                impossible = minimum > result.Contribuicoes.Max(x => decimal.Parse(x.QuantidadeComponente!, System.Globalization.CultureInfo.InvariantCulture) / decimal.Parse(x.MassaKg!, System.Globalization.CultureInfo.InvariantCulture));
            return new GoalResult(goal, passes ? "AtendidaNosDadosDisponiveis" : "NaoAtendida",
                impossible ? "Mínimo excede o máximo dos ingredientes selecionados nesta mistura; prova local, sem solver." : passes ? "Atendida nos dados e hipóteses selecionados." : "Esta formulação não atende à meta.", impossible);
        }).ToArray();
        var warnings = new List<string>();
        if (mass is null) warnings.Add("Massa total e cobertura indeterminadas: grandezas sem conversões explícitas.");
        if (dry is null || dry == 0) warnings.Add("Matéria seca desconhecida/nula: resultados MS indeterminados; BN preservada quando calculável.");
        if (inputs.Any(x => x.Perfil is { EspecieId: null } && species.HasValue)) warnings.Add("Perfil genérico selecionado explicitamente: aplicabilidade à espécie não comprovada pela ausência de restrição.");
        warnings.Add("Simulação manual; metas não certificam adequação de dieta ou segurança de processamento.");
        if (mass > CatalogRules.Maximum || dry > CatalogRules.Maximum) throw new ArgumentException("Massa calculada excede precisão de armazenamento.");
        return new(Version, mass.HasValue ? CatalogRules.Format(mass.Value) : null, dry.HasValue ? CatalogRules.Format(dry.Value) : null,
            dry.HasValue && mass > 0 ? CatalogRules.Format((1m - dry.Value / mass.Value) * 100m) : null, results, goalResults, warnings);
    }
}

public sealed class FormulationSnapshot
{
    private FormulationSnapshot() { }
    public Guid Id { get; private set; }
    public string Tipo { get; private set; } = "";
    public Guid AutorId { get; private set; }
    public string Chave { get; private set; } = "";
    public string Hash { get; private set; } = "";
    public string ConteudoJson { get; private set; } = "";
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public static FormulationSnapshot Create(string type, Guid author, string key, string hash, string json, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), Tipo = type, AutorId = author, Chave = CatalogRules.Text(key, 100, "Chave de idempotência"), Hash = hash, ConteudoJson = json, CreatedAtUtc = now };
}
