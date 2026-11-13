using GenSW.Application.Formulation;
using GenSW.Domain.Catalog;
using GenSW.Domain.Formulation;
using GenSW.Domain.Optimization;

namespace GenSW.Application.Optimization;

public sealed partial class OptimizationService
{
    private const string DietWarning = "Ótimo matemático não certifica uma dieta. Confira fontes, aplicabilidade e avaliação do responsável.";

    public async Task<OptimizationDraftPreview> PreviewDraftAsync(Guid executionId,
        OptimizationDraftDestination destination, Guid actor, CancellationToken ct)
    {
        destination = ValidateDestination(destination);
        await using var transaction = await catalog.BeginAsync(ct);
        await AuthorizeAsync(actor, ct);
        var (preview, _) = await BuildDraftPreviewAsync(executionId, destination, ct);
        await transaction.CommitAsync(ct);
        return preview;
    }

    public async Task<OptimizationDraftReceipt> CreateDraftAsync(Guid executionId, OptimizationDraftCommand command,
        string key, Guid actor, CancellationToken ct)
    {
        var destination = ValidateDestination(command.Destino);
        var previewHash = CatalogRules.Text(command.HashPrevia, 64, "Hash da prévia");
        if (previewHash.Length != 64 || !previewHash.All(Uri.IsHexDigit)) throw new ArgumentException("Informe o hash da prévia.");
        key = Key(key);
        var hash = Hash(new { ExecutionId = executionId, Destination = destination, PreviewHash = previewHash,
            command.AceitaResiduos, command.AceitaAvisos });

        await using var transaction = await catalog.BeginAsync(ct);
        await AuthorizeAsync(actor, ct);
        if (await repository.FindDerivationAsync(actor, key, ct) is { } existing)
        {
            if (existing.ExecutionId != executionId || existing.Hash != hash) throw DivergentKey();
            var replay = FormulationJson.Read<DraftDerivationContent>(existing.ConteudoJson);
            await transaction.CommitAsync(ct);
            return replay.Recibo;
        }

        var (preview, recipe) = await BuildDraftPreviewAsync(executionId, destination, ct);
        if (preview.Hash != previewHash)
            throw new FormulationException(409, "previa_desatualizada", "A prévia não corresponde ao destino e à solução atuais.");
        if (!command.AceitaAvisos || (preview.ExigeAceiteResiduos && !command.AceitaResiduos))
            throw new ArgumentException("Confirme os avisos e, quando houver, os resíduos da quantização.");
        recipe ??= formulation.CreateRecipeInMutation(destination.NovaReceita!, actor);
        var version = await formulation.CreateVersionInMutationAsync(recipe, preview.Conteudo, actor, ct);
        var receipt = new OptimizationDraftReceipt(Guid.NewGuid(), executionId, recipe.Id, version.Id, Now());
        var content = new DraftDerivationContent(receipt, destination, preview, version, command.AceitaResiduos, command.AceitaAvisos);
        repository.Add(new OptimizationDerivation
        {
            Id = receipt.Id, ExecutionId = executionId, AutorId = actor, Chave = key, Hash = hash,
            ReceitaId = recipe.Id, VersaoId = version.Id, CreatedAtUtc = receipt.CreatedAtUtc, ConteudoJson = FormulationJson.Write(content)
        });
        await catalog.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return receipt;
    }

    public async Task<OptimizationDraftReceipt> GetDraftByKeyAsync(Guid executionId, string key, Guid actor, CancellationToken ct)
    {
        await AuthorizeAsync(actor, ct);
        var row = await repository.FindDerivationAsync(actor, Key(key), ct);
        if (row?.ExecutionId != executionId) throw Missing();
        return FormulationJson.Read<DraftDerivationContent>(row.ConteudoJson).Recibo;
    }

    public async Task<OptimizationDraftReceipt> GetDraftByVersionAsync(Guid versionId, Guid actor, CancellationToken ct)
    {
        await AuthorizeAsync(actor, ct);
        var row = await repository.FindDerivationByVersionAsync(versionId, ct) ?? throw Missing();
        return FormulationJson.Read<DraftDerivationContent>(row.ConteudoJson).Recibo;
    }

    private async Task<(OptimizationDraftPreview, Recipe?)> BuildDraftPreviewAsync(Guid executionId,
        OptimizationDraftDestination destination, CancellationToken ct)
    {
        var execution = await FindAsync(executionId, ct);
        var resultRow = await repository.FindResultAsync(executionId, ct);
        var result = resultRow is null ? null : FormulationJson.Read<OptimizationResultContent>(resultRow.ConteudoJson);
        var candidate = result?.Motor?.Candidate;
        if (result?.Desfecho != OptimizationOutcome.SolucaoViavel || candidate?.QuantizedMasses is null)
            throw new FormulationException(409, "resultado_sem_rascunho", "Apenas uma solução viável revalidada permite preparar rascunho.");
        var command = FormulationJson.Read<OptimizationCommand>(execution.PedidoJson);
        var inputs = FormulationJson.Read<OptimizationInputs>(execution.EntradasJson);
        await CheckReferenceAsync(inputs, ct);

        Recipe? recipe = null;
        if (destination.ReceitaId is { } recipeId)
        {
            recipe = await catalog.RecipeAsync(recipeId, false, ct) ?? throw OptimizationInputResolver.Missing("Receita de destino não encontrada.");
            EnsureCurrent(recipe.Ativo && recipe.Revisao == destination.ReceitaRevisao);
        }

        var output = await catalog.ItemAsync(destination.SaidaItemId, false, ct) ??
            throw OptimizationInputResolver.Missing("Item de saída não encontrado.");
        EnsureCurrent(output.Revisao == destination.SaidaItemRevisao);
        if (!output.Ativo || !output.PodeProduzir || output.Classe != "Alimentar" || output.Unidade != "kg")
            throw new FormulationException(409, "saida_incompativel", "Selecione uma saída alimentar ativa, produzível e canônica em kg.");

        var rows = command.Candidatos.ToDictionary(x => x.Id);
        var lines = new List<RecipeInput>();
        var warnings = new List<string> { DietWarning, "O cálculo não verifica saldo disponível nem custo." };
        var estimated = false;
        foreach (var input in inputs.Candidatos.Where(x => !x.Excluido))
        {
            var item = await catalog.ItemAsync(input.ItemId, false, ct);
            EnsureCurrent(item is not null && item.Revisao == input.ItemRevisao && item.Ativo && item.PodeEntrar &&
                item.Classe == "Alimentar" && item.Unidade == "kg");
            var frozen = input.Perfil!;
            var current = await catalog.ProfileAsync(frozen.Id, false, ct);
            EnsureCurrent(current is not null && current.Revisao == frozen.Revisao && current.Estado == "Publicado" && current.ItemId == input.ItemId);
            var kg = candidate.QuantizedMasses[input.Id];
            if (CatalogRules.Decimal(kg) == 0m) continue;
            var requestLine = rows[input.Id];
            lines.Add(new(input.Id, input.ItemId, "Alimentar", kg, "kg", "Variavel", frozen.Id,
                InclusaoMinima: requestLine.InclusaoMinima, InclusaoMaxima: requestLine.InclusaoMaxima));
            estimated |= input.Coeficientes.Any(x => x.Origem == "Estimado");
        }
        if (estimated) warnings.Add("A solução usa estimativas aceitas nas metas indicadas no resultado.");
        var residuals = candidate.ResidualsKg?.Values.Any(x => !Rational.Parse(x).IsZero) == true;
        if (residuals) warnings.Add("Confira as diferenças entre massas calculadas e quantizadas antes de criar o rascunho.");
        var data = new RecipeData("MisturaSimples", "Quantidade", command.MassaKg, "kg", lines,
            [new RecipeOutput(execution.Id, output.Id, output.Nome, command.MassaKg, "kg", "Variavel", true)],
            [], [], null, []);
        data.Validate(true);
        if (lines.Sum(x => CatalogRules.Decimal(x.Quantidade)) != CatalogRules.Decimal(command.MassaKg))
            throw new FormulationException(409, "resultado_sem_rascunho", "As massas transferíveis não fecham o lote da solução.");

        var hash = Hash(new { ExecutionId = execution.Id, Resultado = HashJson(resultRow!.ConteudoJson), Destination = destination, Data = data,
            Avisos = warnings, ExigeAceiteResiduos = residuals });
        return (new(hash, data, warnings, residuals), recipe);
    }

    private async Task CheckReferenceAsync(OptimizationInputs inputs, CancellationToken ct)
    {
        if (inputs.Receita is not { } frozen || inputs.ReceitaVersao is not { } version) return;
        var recipe = await catalog.RecipeAsync(frozen.Id, false, ct);
        var current = await catalog.VersionAsync(version.Id, false, ct);
        EnsureCurrent(recipe is not null && recipe.Revisao == frozen.Revisao && recipe.Ativo &&
            current is not null && current.Revisao == version.Revisao && current.Estado == "Publicado" && current.ReceitaId == frozen.Id);
        if (inputs.SaidaReferencia is { } frozenOutput)
        {
            var output = await catalog.ItemAsync(frozenOutput.Id, false, ct);
            EnsureCurrent(output is not null && output.Revisao == frozenOutput.Revisao && output.Ativo &&
                output.PodeProduzir && output.Classe == "Alimentar" && output.Unidade == "kg");
        }
    }

    private static void EnsureCurrent(bool current)
    {
        if (!current)
            throw new FormulationException(409, "referencia_desatualizada", "Uma referência mudou ou foi inativada. Confira o destino e a solução antes de continuar.");
    }

    private static OptimizationDraftDestination ValidateDestination(OptimizationDraftDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var existing = destination.ReceitaId.HasValue;
        if (destination.SaidaItemId == Guid.Empty || destination.SaidaItemRevisao < 1 || destination.ReceitaId == Guid.Empty ||
            existing != destination.ReceitaRevisao.HasValue || destination.ReceitaRevisao is < 1 || existing == (destination.NovaReceita is not null))
            throw new ArgumentException("Informe a saída com revisão e escolha uma receita existente ou um novo cabeçalho.");
        if (destination.NovaReceita is not { } header) return destination;
        return destination with { NovaReceita = new(
            CatalogRules.Text(header.Codigo, 50, "Código"), CatalogRules.Text(header.Nome, 200, "Nome"),
            CatalogRules.Text(header.Finalidade, 2000, "Finalidade")) };
    }

    private sealed record DraftDerivationContent(OptimizationDraftReceipt Recibo, OptimizationDraftDestination Destino,
        OptimizationDraftPreview Previa, RecipeVersionView VersaoInicial, bool AceitouResiduos, bool AceitouAvisos);
}
