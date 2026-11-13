using System.Globalization;
using GenSW.Application.Formulation;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Optimization;

namespace GenSW.Application.Optimization;

internal sealed class OptimizationInputResolver(IFormulationRepository catalog)
{
    internal static OptimizationCommand Canonicalize(OptimizationCommand command)
    {
        if (command.Candidatos is null || command.Metas is null || command.Candidatos.Count > 100 || command.Metas.Count > 50 ||
            command.Candidatos.Any(x => x is null) || command.Metas.Any(x => x is null))
            throw new ArgumentException("Informe até 100 linhas de candidatos e 50 metas.");
        if (command.Candidatos.Any(x => x.Id == Guid.Empty || x.ItemId == Guid.Empty || x.ItemRevisao < 1 ||
                x.PerfilId == Guid.Empty || x.PerfilId.HasValue != x.PerfilRevisao.HasValue || x.PerfilRevisao is < 1) ||
            command.Metas.Any(x => x.Id == Guid.Empty) ||
            command.Candidatos.DistinctBy(x => x.Id).Count() != command.Candidatos.Count ||
            command.Candidatos.DistinctBy(x => x.ItemId).Count() != command.Candidatos.Count ||
            command.Metas.DistinctBy(x => x.Id).Count() != command.Metas.Count)
            throw new ArgumentException("Use IDs de linha, item e meta únicos e informe as revisões selecionadas.");
        var receita = command.ReceitaId.HasValue;
        if (receita != command.ReceitaRevisao.HasValue || receita != command.ReceitaVersaoId.HasValue ||
            receita != command.ReceitaVersaoRevisao.HasValue || command.ReceitaId == Guid.Empty ||
            command.ReceitaVersaoId == Guid.Empty || command.ReceitaRevisao is < 1 || command.ReceitaVersaoRevisao is < 1 ||
            command.EspecieId == Guid.Empty || command.AnteriorId == Guid.Empty)
            throw new ArgumentException("Informe a receita, a versão e as revisões de referência juntas.");

        return command with
        {
            VersaoModelo = CatalogRules.Text(command.VersaoModelo, 40, "Versão do modelo"),
            Modo = CatalogRules.Text(command.Modo, 40, "Modo"),
            MassaKg = CatalogRules.Format(CatalogRules.Decimal(command.MassaKg, true)),
            ReferenciaDescricao = CatalogRules.Optional(command.ReferenciaDescricao),
            Fase = ExactText(command.Fase, 200, "Fase", required: false),
            Preparo = string.IsNullOrWhiteSpace(command.Preparo) ? null : ExactText(command.Preparo, 500, "Preparo", required: false),
            Candidatos = command.Candidatos.Select(x => x with
            {
                ReferenciaKg = CatalogRules.Format(CatalogRules.Decimal(x.ReferenciaKg)),
                InclusaoMinima = Number(x.InclusaoMinima),
                InclusaoMaxima = Number(x.InclusaoMaxima),
                Justificativa = CatalogRules.Optional(x.Justificativa)
            }).OrderBy(x => x.Id.ToString("D"), StringComparer.Ordinal).ToArray(),
            Metas = command.Metas.Select(x => x with
            {
                Componente = CatalogRules.Text(x.Componente, 40, "Componente"),
                Metodo = ExactText(x.Metodo, 500, "Método", required: true)!,
                Contexto = ExactText(x.Contexto, 500, "Contexto", required: false) ?? "",
                Base = CatalogRules.Text(x.Base, 10, "Base"),
                Unidade = CatalogRules.Text(x.Unidade, 20, "Unidade"),
                Minimo = Number(x.Minimo),
                Maximo = Number(x.Maximo),
                Origem = CatalogRules.Text(x.Origem, 40, "Origem da meta"),
                Fonte = CatalogRules.Optional(x.Fonte, 1000),
                Justificativa = CatalogRules.Optional(x.Justificativa)
            }).OrderBy(x => x.Id.ToString("D"), StringComparer.Ordinal).ToArray()
        };
    }

    private static string? Number(string? value) => value is null ? null : CatalogRules.Format(CatalogRules.Decimal(value));
    private static string? ExactText(string? value, int limit, string label, bool required)
    {
        if (value?.Length > limit || (required && string.IsNullOrWhiteSpace(value)))
            throw new ArgumentException($"{label} inválido.");
        return value;
    }

    internal static decimal Read(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    internal async Task<(OptimizationInputs Inputs, OptimizationProblem? Problem)> ResolveAsync(
        OptimizationCommand command, DateTimeOffset cut, CancellationToken ct)
    {
        var diagnostics = new List<OptimizationDiagnostic>();
        var candidates = new List<OptimizationCandidateSnapshot>();
        var ingredients = new List<OptimizationIngredient>();
        var goalDefinitions = new List<GoalDefinition>();
        var mass = Read(command.MassaKg);
        if (command.VersaoModelo != OptimizationEngine.ModelVersion || command.Modo != "N1" || command.Disponibilidade)
            diagnostics.Add(Incompatible("ModoForaRecorte", "O recorte N1 usa metas BN e menor desvio, sem disponibilidade ou custo."));
        if (command.Candidatos.Count == 0 || command.Metas.Count == 0)
            diagnostics.Add(Incompatible("ProblemaVazio", "Selecione candidatos e pelo menos uma meta."));
        if (command.Candidatos.Sum(x => Read(x.ReferenciaKg)) != mass)
            diagnostics.Add(Incompatible("MassaReferenciaDivergente", "As massas da referência devem somar exatamente a massa do lote."));
        if (command.EspecieId is { } species && !await catalog.SpeciesExistsAsync(species, ct))
            throw Missing("Espécie selecionada não encontrada.");

        var (recipe, version, scaled, output) = await ResolveReferenceAsync(command, diagnostics, ct);
        foreach (var goal in command.Metas)
        {
            var definition = ValidateGoal(goal, diagnostics);
            if (definition is not null) goalDefinitions.Add(definition);
        }

        var coefficients = goalDefinitions.ToDictionary(x => x.Goal.Id, _ => new Dictionary<Guid, Rational>());
        foreach (var line in command.Candidatos)
        {
            var item = await catalog.ItemAsync(line.ItemId, false, ct) ?? throw Missing("Item selecionado não encontrado.");
            RequireRevision(item.Revisao, line.ItemRevisao);
            var min = line.InclusaoMinima is null ? 0m : Read(line.InclusaoMinima);
            var max = line.InclusaoMaxima is null ? 100m : Read(line.InclusaoMaxima);
            if (min > max || max > 100m)
                diagnostics.Add(Incompatible("IntervaloInclusaoInvalido", "A inclusão exige 0 ≤ mínimo ≤ máximo ≤ 100.", line.Id));
            if (max == 0 && line.Justificativa is null)
                diagnostics.Add(Incompatible("ExclusaoSemJustificativa", "Confirme e justifique a exclusão do candidato.", line.Id));
            var excluded = max == 0 && min == 0;
            ingredients.Add(new(line.Id, Read(line.ReferenciaKg), min, max));
            if (!excluded && (item.Classe != "Alimentar" || item.Unidade != "kg"))
                diagnostics.Add(Incompatible("ModoForaRecorte", "N1 exige candidato alimentar com unidade canônica kg.", line.Id));
            if (!excluded && (!item.Ativo || !item.PodeEntrar))
                diagnostics.Add(Incompatible("ItemInelegivel", "Selecione um candidato ativo e apto para entrada.", line.Id));

            NutritionProfile? profile = null;
            if (line.PerfilId is { } profileId)
            {
                profile = await catalog.ProfileAsync(profileId, false, ct) ?? throw Missing("Perfil selecionado não encontrado.");
                RequireRevision(profile.Revisao, line.PerfilRevisao!.Value);
                if (!excluded && (profile.ItemId != item.Id || profile.Estado != "Publicado"))
                    diagnostics.Add(Incompatible("PerfilIncompativel", "Selecione um perfil publicado do próprio item.", line.Id));
            }
            var view = profile is null ? null : FormulationService.View(profile);
            var cells = new List<OptimizationCoefficient>();
            if (!excluded)
            {
                foreach (var definition in goalDefinitions)
                {
                    var (cell, coefficient, diagnostic) = Coefficient(command, line, definition, view);
                    cells.Add(cell);
                    if (diagnostic is not null) diagnostics.Add(diagnostic);
                    if (coefficient is { } value) coefficients[definition.Goal.Id].Add(line.Id, value);
                }
            }
            candidates.Add(new(line.Id, item.Id, item.Nome, excluded, item.Revisao, item.Ativo, item.Data(), view, cells));
        }

        if (ingredients.Count(x => x.MaximumPercent > 0) > 50)
            diagnostics.Add(Incompatible("LimiteCandidatos", "N1 permite até 50 candidatos incluídos."));
        var input = new OptimizationInputs(cut, candidates, command.Metas, diagnostics,
            recipe, version, scaled, goalDefinitions.Select(x => x.Component).DistinctBy(x => x.Codigo).ToArray(),
            SummarizeComposition(command, candidates, command.Candidatos.ToDictionary(x => x.Id, x => x.ReferenciaKg)), output);
        if (diagnostics.Count != 0) return (input, null);
        var goals = goalDefinitions.Select(x => new OptimizationGoal(x.Goal.Id, x.Min, x.Max, x.Unit, coefficients[x.Goal.Id])).ToArray();
        return (input, new OptimizationProblem(mass, ingredients, goals));
    }

    private async Task<(OptimizationRecipeSnapshot?, RecipeVersionView?, RecipeData?, OptimizationReferenceOutputSnapshot?)> ResolveReferenceAsync(
        OptimizationCommand command, List<OptimizationDiagnostic> diagnostics, CancellationToken ct)
    {
        if (command.ReceitaId is not { } recipeId)
        {
            if (command.ReferenciaDescricao is null)
                diagnostics.Add(Incomplete("ReferenciaManualSemIdentificacao", "Identifique a mistura de referência manual."));
            return (null, null, null, null);
        }

        var recipe = await catalog.RecipeAsync(recipeId, false, ct) ?? throw Missing("Receita de referência não encontrada.");
        var version = await catalog.VersionAsync(command.ReceitaVersaoId!.Value, false, ct) ?? throw Missing("Versão da receita de referência não encontrada.");
        RequireRevision(recipe.Revisao, command.ReceitaRevisao!.Value);
        RequireRevision(version.Revisao, command.ReceitaVersaoRevisao!.Value);
        var snapshot = new OptimizationRecipeSnapshot(recipe.Id, recipe.Revisao, recipe.Ativo,
            new RecipeHeader(recipe.Codigo, recipe.Nome, recipe.Finalidade));
        var versionView = FormulationService.View(version);
        var data = versionView.Conteudo;
        if (!recipe.Ativo || version.ReceitaId != recipe.Id || version.Estado != "Publicado")
        {
            diagnostics.Add(Incompatible("ReferenciaIncompativel", "A referência deve ser uma versão publicada da receita ativa selecionada."));
            return (snapshot, versionView, null, null);
        }
        if (data.Tipo != "MisturaSimples" || data.Perdas.Count != 0 || data.Retencoes.Count != 0 ||
            CatalogRules.Dimension(data.UnidadeReferencia) != "massa" ||
            data.Entradas.Any(x => x.Papel != "Alimentar" || x.Unidade is not ("kg" or "g") || x.SubReceitaVersaoId.HasValue) ||
            data.Entradas.DistinctBy(x => x.ItemId).Count() != data.Entradas.Count || data.Saidas.Count != 1)
        {
            diagnostics.Add(Incompatible("ModoForaRecorte", "A referência N1 deve ser mistura simples alimentar em massa, com uma linha por item."));
            return (snapshot, versionView, null, null);
        }
        var output = await catalog.ItemAsync(data.Saidas[0].ItemId, false, ct) ?? throw Missing("Saída da receita de referência não encontrada.");
        var outputSnapshot = new OptimizationReferenceOutputSnapshot(output.Id, output.Revisao, output.Ativo, output.Data());
        if (!output.Ativo || !output.PodeProduzir || output.Classe != "Alimentar" || output.Unidade != "kg")
            diagnostics.Add(Incompatible("SaidaReferenciaIncompativel", "A saída da referência deve ser um item alimentar ativo produzido em kg."));

        RecipeData scaled;
        try
        {
            scaled = data.Scale(command.MassaKg, "kg");
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            diagnostics.Add(Incompatible("EscalaReferenciaIncompativel", "A escala solicitada não é representável na referência publicada."));
            return (snapshot, versionView, null, outputSnapshot);
        }

        var rows = command.Candidatos.ToDictionary(x => x.ItemId);
        var original = scaled.Entradas.ToDictionary(x => x.ItemId);
        foreach (var input in scaled.Entradas)
        {
            if (!rows.TryGetValue(input.ItemId, out var row) ||
                Read(row.ReferenciaKg) != CatalogRules.Canonical(Read(input.Quantidade), input.Unidade))
                diagnostics.Add(Incompatible("LinhaReferenciaDivergente", "Inclua todas as massas da referência publicada na escala selecionada.", row?.Id));
            else if ((Read(row.InclusaoMinima ?? "0") != Read(input.InclusaoMinima ?? "0") ||
                    Read(row.InclusaoMaxima ?? "100") != Read(input.InclusaoMaxima ?? "100")) && row.Justificativa is null)
                diagnostics.Add(Incompatible("LimiteImportadoAlterado", "Confirme e justifique a mudança de limites da receita.", row.Id));
        }
        foreach (var row in command.Candidatos.Where(x => !original.ContainsKey(x.ItemId) && Read(x.ReferenciaKg) != 0))
            diagnostics.Add(Incompatible("CandidatoNovoComReferencia", "Candidato novo exige massa de referência zero.", row.Id));
        return (snapshot, versionView, scaled, outputSnapshot);
    }

    private sealed record GoalDefinition(OptimizationGoalCommand Goal, ComponentDefinition Component,
        string Unit, Rational? Min, Rational? Max);

    private static GoalDefinition? ValidateGoal(OptimizationGoalCommand goal, List<OptimizationDiagnostic> diagnostics)
    {
        if (goal.Base != "BN")
        {
            diagnostics.Add(Incompatible("ModoForaRecorte", "N1 aceita somente metas em base natural.", goal: goal.Id));
            return null;
        }
        var component = NutritionComponents.All.SingleOrDefault(x => x.Codigo == goal.Componente);
        if (component is null)
        {
            diagnostics.Add(Incompatible("ComponenteInvalido", "Selecione uma definição nutricional existente.", goal: goal.Id));
            return null;
        }
        var energy = component.Grandeza == "energia";
        if (energy && string.IsNullOrWhiteSpace(goal.Contexto))
            diagnostics.Add(Incompatible("ContextoEnergeticoAusente", "Informe o contexto da modalidade energética.", goal: goal.Id));
        if (goal.Origem is not ("InformadaUsuario" or "ReferenciaTecnica"))
            diagnostics.Add(Incompatible("OrigemMetaInvalida", "Selecione uma origem de meta reconhecida.", goal: goal.Id));
        if (goal.Origem == "ReferenciaTecnica" && goal.Fonte is null)
            diagnostics.Add(Incomplete("FonteMetaAusente", "Identifique fonte, edição, seção e aplicabilidade da meta técnica.", goal: goal.Id));
        if ((goal.Origem == "InformadaUsuario" || goal.AceitarEstimados) && goal.Justificativa is null)
            diagnostics.Add(Incomplete("JustificativaMetaAusente", "Identifique a justificativa da meta ou da aceitação de estimativas.", goal: goal.Id));
        if ((goal.Minimo is null && goal.Maximo is null) ||
            (goal.Minimo is { } minimum && goal.Maximo is { } maximum && Read(minimum) > Read(maximum)))
            diagnostics.Add(Incompatible("IntervaloMetaInvalido", "Informe mínimo e/ou máximo coerentes para a meta.", goal: goal.Id));
        try
        {
            NutritionComponents.Normalize(component.Codigo, 0m, goal.Unidade);
            return new(goal, component, energy ? "MJ/kg BN" : "g/kg BN",
                goal.Minimo is null ? null : Normalize(component.Codigo, goal.Minimo, goal.Unidade),
                goal.Maximo is null ? null : Normalize(component.Codigo, goal.Maximo, goal.Unidade));
        }
        catch (ArgumentException)
        {
            diagnostics.Add(Incompatible("UnidadeMetaIncompativel", "A unidade não é compatível com o componente da meta.", goal: goal.Id));
            return null;
        }
    }

    private static (OptimizationCoefficient, Rational?, OptimizationDiagnostic?) Coefficient(
        OptimizationCommand command, OptimizationCandidateCommand line, GoalDefinition goal, ProfileView? view)
    {
        var target = goal.Goal;
        var profile = view?.Conteudo;
        NutritionValue? value = null, dry = null;
        Rational? fraction = null;
        (OptimizationCoefficient, Rational?, OptimizationDiagnostic?) Failed(OptimizationOutcome outcome, string code, string message)
            => (new(target.Id, null, goal.Unit, outcome.ToString(), code, value?.Fonte ?? profile?.Fonte,
                    value?.Origem, value?.Hipotese, value, dry, fraction?.ToDecimalString()),
                null, new(outcome, code, message, line.Id, target.Id));

        if (profile is null) return Failed(OptimizationOutcome.DadosIncompletos, "PerfilAusente", "Selecione um perfil publicado para o ingrediente.");
        if (view!.ItemId != line.ItemId || view.Estado != "Publicado")
            return Failed(OptimizationOutcome.DadosIncompativeis, "PerfilIncompativel", "O perfil não é publicado para este ingrediente.");
        if (command.Preparo is { } preparation && profile.Preparacao != preparation)
            return Failed(OptimizationOutcome.DadosIncompativeis, "PreparoIncompativel", "O perfil não corresponde ao preparo comum exigido no pedido.");

        var energy = goal.Component.Grandeza == "energia";
        var compatible = energy
            ? profile.EspecieId == command.EspecieId && profile.Fase == command.Fase && !string.IsNullOrWhiteSpace(target.Contexto)
            : (profile.EspecieId is null || profile.EspecieId == command.EspecieId) &&
                (string.IsNullOrWhiteSpace(profile.Fase) || profile.Fase == command.Fase);
        value = profile.Valores.SingleOrDefault(x => x.Componente == target.Componente);
        if (!compatible)
            return Failed(OptimizationOutcome.DadosIncompativeis, "AplicabilidadeIncompativel", "A espécie ou fase não corresponde à aplicabilidade do perfil.");
        if (value is null)
        {
            if (energy && profile.Valores.Any(x => NutritionComponents.Get(x.Componente).Grandeza == "energia"))
                return Failed(OptimizationOutcome.DadosIncompativeis, "ModalidadeEnergeticaIncompativel", "O perfil não fornece a modalidade energética solicitada.");
            return Failed(OptimizationOutcome.DadosIncompletos, "CoeficienteAusente", "Informe a observação do componente no perfil escolhido.");
        }
        if (value.Metodo != target.Metodo || value.Contexto != target.Contexto)
            return Failed(OptimizationOutcome.DadosIncompativeis, "MetodoContextoIncompativel", "Método e contexto da observação devem coincidir com a meta.");
        if (value.Estado == "NaoAplicavel")
            return Failed(OptimizationOutcome.DadosIncompativeis, "CoeficienteNaoAplicavel", "O componente foi declarado não aplicável neste perfil.");
        if (value.Estado != "Conhecido" || value.Valor is null)
            return Failed(OptimizationOutcome.DadosIncompletos, "CoeficienteDesconhecido", "O componente não tem valor conhecido neste perfil.");
        if (value.Qualificador != "Pontual")
            return Failed(OptimizationOutcome.DadosIncompletos, "CoeficienteNaoPontual", "Limites, faixas e detecção não são teores pontuais.");

        var coefficient = Normalize(value.Componente, value.Valor, value.Unidade);
        if (value.Base == "MS")
        {
            dry = profile.Valores.SingleOrDefault(x => IsPoint(x, "MS")) ??
                profile.Valores.SingleOrDefault(x => IsPoint(x, "UMIDADE"));
            if (dry is null)
                return Failed(OptimizationOutcome.DadosIncompletos, "MateriaSecaAusente", "Informe matéria seca ou umidade pontual da mesma amostra.");
            var d = NutritionComponents.Normalize(dry.Componente, Read(dry.Valor!), dry.Unidade) / 1000m;
            if (dry.Componente == "UMIDADE") d = 1m - d;
            if (d <= 0m)
                return Failed(OptimizationOutcome.DadosIncompativeis, "MateriaSecaNula", "A observação em MS exige fração seca positiva.");
            fraction = Rational.FromDecimal(d);
            coefficient *= fraction.Value;
        }
        var estimated = value.Origem == "Estimado" || dry?.Origem == "Estimado";
        if (estimated && !target.AceitarEstimados)
            return Failed(OptimizationOutcome.DadosIncompletos, "EstimativaExcluida", "A política da meta exclui a estimativa ou sua derivação.");
        var hypothesis = string.Join(" | ", new[] { value.Hipotese, dry?.Hipotese }.Where(x => x is not null));
        return (new(target.Id, coefficient.ToDecimalString(24), goal.Unit, "Conhecido", null, value.Fonte ?? profile.Fonte,
            estimated ? "Estimado" : value.Origem, hypothesis.Length == 0 ? null : hypothesis, value, dry, fraction?.ToDecimalString(),
            coefficient.ToString()), coefficient, null);
    }

    internal static OptimizationComposition? SummarizeComposition(OptimizationCommand command,
        IReadOnlyList<OptimizationCandidateSnapshot> candidates, IReadOnlyDictionary<Guid, string> masses)
    {
        var lines = new List<ResolvedIngredient>();
        foreach (var candidate in candidates)
        {
            if (!masses.TryGetValue(candidate.Id, out var quantity) || Read(quantity) <= 0) continue;
            if (candidate.Item.Classe != "Alimentar" || candidate.Item.Unidade != "kg") return null;
            var profile = candidate.Perfil is { Estado: "Publicado" } published && published.ItemId == candidate.ItemId
                ? published : null;
            lines.Add(new(candidate.Id, candidate.ItemId, candidate.Nome, "Alimentar", quantity, "kg", quantity,
                profile?.Id, profile?.Numero, profile?.Conteudo, null, null, null, null));
        }
        if (lines.Count == 0 || lines.Sum(x => Read(x.Quantidade)) != Read(command.MassaKg)) return null;
        try
        {
            // This read-only diagnostic uses the existing calculation semantics without creating a simulation.
            // Model feasibility and transfers are independently checked by the rational verifier.
            var result = NutritionEngine.Calculate(lines, [], command.EspecieId, command.Fase);
            var components = result.Componentes.Select(x =>
            {
                var known = x.Contribuicoes.Where(c => c.QuantidadeComponente is not null).ToArray();
                return new OptimizationComponentSummary(x.Componente, x.Metodo, x.Contexto, x.Unidade,
                    x.Estado, x.ValorBN, x.ValorMS, known.Length == 0 ? null : CatalogRules.Format(known.Sum(c => Read(c.QuantidadeComponente!))),
                    x.CoberturaPercentual, x.Estimado, x.EstimadoMS);
            }).ToArray();
            return new(result.MassaKg, result.MateriaSecaKg, result.UmidadePercentual, components,
                result.Avisos.Where(x => !x.StartsWith("Simulação manual;", StringComparison.Ordinal)).ToArray());
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            return new(command.MassaKg, null, null, [], ["O resumo de composição excede a precisão do cálculo descritivo."]);
        }
    }

    private static bool IsPoint(NutritionValue value, string component) =>
        value.Componente == component && value.Estado == "Conhecido" && value.Qualificador == "Pontual" && value.Valor is not null;

    private static Rational Normalize(string component, string value, string unit) =>
        Rational.FromDecimal(NutritionComponents.Normalize(component, Read(value), unit));

    internal static FormulationException Missing(string message) => new(404, "registro_nao_encontrado", message);
    internal static void RequireRevision(int actual, int expected) => CatalogRules.Expected(actual, expected);
    private static OptimizationDiagnostic Incompatible(string code, string message, Guid? candidate = null, Guid? goal = null) =>
        new(OptimizationOutcome.DadosIncompativeis, code, message, candidate, goal);
    private static OptimizationDiagnostic Incomplete(string code, string message, Guid? candidate = null, Guid? goal = null) =>
        new(OptimizationOutcome.DadosIncompletos, code, message, candidate, goal);
}
