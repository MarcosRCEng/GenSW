using System.Text.Json;
using GenSW.Domain.Optimization;
using GenSW.OptimizationWorker;

OptimizationSolverResult result;
try
{
    var request = await JsonSerializer.DeserializeAsync<OptimizationSolverRequest>(Console.OpenStandardInput());
    result = request is null
        ? new(OptimizationNativeStatus.Error, "RequisicaoNativaInvalida")
        : HighsWorker.Solve(request);
}
catch (JsonException)
{
    result = new(OptimizationNativeStatus.Error, "RequisicaoNativaInvalida");
}
catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
{
    result = new(OptimizationNativeStatus.Error, "BibliotecaNativaIndisponivel");
}
catch (IOException)
{
    result = new(OptimizationNativeStatus.Error, "CanalNativoInvalido");
}

await JsonSerializer.SerializeAsync(Console.OpenStandardOutput(), result);
