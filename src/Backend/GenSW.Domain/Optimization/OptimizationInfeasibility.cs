using static GenSW.Domain.Optimization.OptimizationModel;

namespace GenSW.Domain.Optimization;

internal static class OptimizationInfeasibility
{
    internal static OptimizationProof? FindAnalyticProof(OptimizationLinearModel model, OptimizationIngredient[] ingredients, OptimizationGoal[] goals)
    {
        var mass = model.MassKg;
        var lower = Sum(ingredients.Select(x => Percent(x.MinimumPercent)));
        var upper = Sum(ingredients.Select(x => Percent(x.MaximumPercent)));
        if (lower > 1 || upper < 1)
        {
            var minimumConflict = lower > 1;
            var bound = minimumConflict ? "min" : "max";
            var ids = ingredients.Select(x => $"Inclusao:{x.Id:D}:{bound}")
                .Prepend(minimumConflict ? "Massa:max" : "Massa:min").ToArray();
            return new("SomaLimitesInclusao", ids, [
                $"massaKg={mass}", $"somaMinimaKg={lower * mass}", $"somaMaximaKg={upper * mass}"
            ]);
        }

        foreach (var goal in goals)
        {
            var attainableMin = BoundedConcentration(ingredients, goal, lower, maximize: false);
            var attainableMax = BoundedConcentration(ingredients, goal, lower, maximize: true);
            var minimumConflict = goal.Minimum is { } min && attainableMax < min;
            var maximumConflict = goal.Maximum is { } max && attainableMin > max;
            if (!minimumConflict && !maximumConflict)
                continue;

            var goalId = $"Meta:{goal.Id:D}:{(minimumConflict ? "min" : "max")}";
            var ids = model.Inequalities.Select(x => x.Id).Where(id => id.StartsWith("Inclusao:", StringComparison.Ordinal)
                || id.StartsWith("Massa:", StringComparison.Ordinal)).Append(goalId).ToArray();
            return new("LimiteComponente", ids, [
                $"unidade={goal.Unit}", $"minimoAtingivel={attainableMin}", $"maximoAtingivel={attainableMax}",
                $"minimoMeta={goal.Minimum}", $"maximoMeta={goal.Maximum}"
            ]);
        }

        // For two ingredients, substitute x₁=M−x₂ into each constraint that does not involve deviation.
        // Every surviving row is an exact upper or lower bound on x₂.
        if (ingredients.Length != 2)
            return null;

        Rational minMass = 0;
        var maxMass = mass;
        var minId = $"Inclusao:{ingredients[1].Id:D}:min";
        var maxId = $"Inclusao:{ingredients[0].Id:D}:min";
        foreach (var row in model.Inequalities.Where(x => !x.Id.StartsWith("Desvio:", StringComparison.Ordinal)))
        {
            var coefficient = row.Coefficients[1] - row.Coefficients[0];
            var limit = row.UpperBound - row.Coefficients[0] * mass;
            if (coefficient.IsZero)
            {
                if (limit < 0)
                    return new("ContradicaoLinear", ["Massa:min", "Massa:max", row.Id], [$"zeroMaiorQueLimite={limit}"]);
                continue;
            }

            var bound = limit / coefficient;
            if (coefficient < 0 && bound > minMass)
                (minMass, minId) = (bound, row.Id);
            if (coefficient > 0 && bound < maxMass)
                (maxMass, maxId) = (bound, row.Id);
        }

        return minMass > maxMass
            ? new("IntervaloContraditorio", ["Massa:min", "Massa:max", minId, maxId], [
                $"ingredienteId={ingredients[1].Id:D}", $"minimoKg={minMass}", $"maximoKg={maxMass}"
            ])
            : null;
    }

    internal static OptimizationProof? VerifyDualRay(OptimizationLinearModel model, double[]? ray)
    {
        if (ray is null || ray.Length != model.Inequalities.Length || ray.Any(x => !double.IsFinite(x)))
            return null;

        // HiGHS supplies signed row multipliers. The scaled matrix has A/s per row,
        // so undo that row scaling before checking the original rational inequalities.
        var sign = ray.Any(x => x > 0) ? 1 : -1;
        var weights = ray.Select((x, i) => Rational.FromDouble(sign * x) / model.Inequalities[i].Scale).ToArray();
        if (weights.Any(x => x < 0))
            return null;

        var sum = Sum(weights.Select((x, i) => x * model.Inequalities[i].UpperBound));
        if (sum >= 0)
            return null;

        for (var column = 0; column < model.Native.Costs.Length; column++)
        {
            if (!Sum(weights.Select((x, i) => x * model.Inequalities[i].Coefficients[column])).IsZero)
                return null;
        }

        var certificate = model.Inequalities.Select((row, i) => (row.Id, Weight: weights[i]))
            .Where(x => !x.Weight.IsZero).ToDictionary(x => x.Id, x => x.Weight, StringComparer.Ordinal);
        return new("FarkasVerificado", certificate.Keys.ToArray(), [$"produtoEscalarLimites={sum}"], certificate);
    }

    private static Rational BoundedConcentration(OptimizationIngredient[] ingredients, OptimizationGoal goal, Rational lower, bool maximize)
    {
        var remaining = 1 - lower;
        var result = Sum(ingredients.Select(x => Percent(x.MinimumPercent) * goal.Coefficients[x.Id]));
        var ordered = maximize
            ? ingredients.OrderByDescending(x => goal.Coefficients[x.Id])
            : ingredients.OrderBy(x => goal.Coefficients[x.Id]);

        foreach (var ingredient in ordered)
        {
            var room = Percent(ingredient.MaximumPercent - ingredient.MinimumPercent);
            var allocation = room < remaining ? room : remaining;
            result += allocation * goal.Coefficients[ingredient.Id];
            remaining -= allocation;
            if (remaining.IsZero)
                break;
        }

        return result;
    }
}
