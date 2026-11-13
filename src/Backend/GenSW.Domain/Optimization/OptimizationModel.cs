namespace GenSW.Domain.Optimization;

internal static class OptimizationModel
{
    internal const double SmallMatrixValue = 1e-12;
    internal const double SolverTolerance = 1e-8;

    internal static OptimizationLinearModel Create(Rational mass, OptimizationIngredient[] ingredients, OptimizationGoal[] goals,
        Rational excludedDeviation)
    {
        var count = ingredients.Length;
        var inequalities = new List<OptimizationInequality>();
        var ones = new Rational[2 * count];
        Array.Fill(ones, (Rational)1, 0, count);
        inequalities.Add(new("Massa:max", ones, mass, 1));
        inequalities.Add(new("Massa:min", Negate(ones), -mass, 1));

        for (var i = 0; i < count; i++)
        {
            var ingredient = ingredients[i];
            var id = $"Inclusao:{ingredient.Id:D}";
            var unit = Vector(count, i, 1);
            inequalities.Add(new($"{id}:max", unit, mass * Percent(ingredient.MaximumPercent), 1));
            inequalities.Add(new($"{id}:min", Negate(unit), -mass * Percent(ingredient.MinimumPercent), 1));

            var reference = Rational.FromDecimal(ingredient.ReferenceKg);
            var deviationPositive = Vector(count, i, 1);
            deviationPositive[count + i] = -1;
            inequalities.Add(new($"Desvio:{ingredient.Id:D}:positivo", deviationPositive, reference, 1));
            var deviationNegative = Vector(count, i, -1);
            deviationNegative[count + i] = -1;
            inequalities.Add(new($"Desvio:{ingredient.Id:D}:negativo", deviationNegative, -reference, 1));
            inequalities.Add(new($"Desvio:{ingredient.Id:D}:min", Vector(count, count + i, -1), 0, 1));
        }

        foreach (var goal in goals)
        {
            var coefficients = new Rational[2 * count];
            for (var i = 0; i < count; i++)
                coefficients[i] = goal.Coefficients[ingredients[i].Id];

            var scale = coefficients.Select(x => x.Abs()).Append(goal.Minimum?.Abs() ?? 0)
                .Append(goal.Maximum?.Abs() ?? 0).Append(1).Max();

            if (goal.Maximum is { } maximum)
                inequalities.Add(new($"Meta:{goal.Id:D}:max", coefficients, maximum * mass, scale));
            if (goal.Minimum is { } minimum)
                inequalities.Add(new($"Meta:{goal.Id:D}:min", Negate(coefficients), -minimum * mass, scale));
        }

        var costs = new double[2 * count];
        Array.Fill(costs, 1.0, count, count);
        var native = new OptimizationNativeModel(
            costs,
            inequalities.Select(row => row.Coefficients.Select(value => (value / row.Scale).ToDouble()).ToArray()).ToArray(),
            inequalities.Select(row => (row.UpperBound / (mass * row.Scale)).ToDouble()).ToArray(),
            SmallMatrixValue,
            (excludedDeviation / mass).ToDouble());
        return new OptimizationLinearModel(OptimizationEngine.ModelVersion, mass,
            ingredients.Select(x => x.Id).ToArray(), goals.Select(x => x.Id).ToArray(), inequalities.ToArray(), native, excludedDeviation);
    }

    internal static bool NativeModelPreservesCoefficients(OptimizationLinearModel model)
    {
        for (var rowIndex = 0; rowIndex < model.Inequalities.Length; rowIndex++)
        {
            var row = model.Inequalities[rowIndex];
            var upperBound = model.Native.UpperBounds[rowIndex];
            if (!double.IsFinite(upperBound) || (!row.UpperBound.IsZero && upperBound == 0))
                return false;

            var nativeRow = model.Native.Coefficients[rowIndex];
            for (var column = 0; column < row.Coefficients.Length; column++)
            {
                if (!double.IsFinite(nativeRow[column]) ||
                    (!row.Coefficients[column].IsZero && Math.Abs(nativeRow[column]) <= SmallMatrixValue))
                    return false;
            }
        }
        return true;
    }

    internal static Rational Percent(decimal value) => Rational.FromDecimal(value) / 100;

    internal static Rational Sum(IEnumerable<Rational> values) => values.Aggregate(default(Rational), (sum, value) => sum + value);

    private static Rational[] Vector(int count, int index, Rational value)
    {
        var vector = new Rational[2 * count];
        vector[index] = value;
        return vector;
    }

    private static Rational[] Negate(Rational[] vector) => vector.Select(x => -x).ToArray();
}
