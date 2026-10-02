using GenSW.Application.Animals;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GenSW.API.Controllers;

public sealed class AnimalEvolutionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var error = context.Exception as AnimalEvolutionException;
        var postgres = context.Exception as Npgsql.PostgresException ?? context.Exception.InnerException as Npgsql.PostgresException;
        if (postgres?.SqlState == Npgsql.PostgresErrorCodes.LockNotAvailable)
            error = new(409, "conflito_transitorio", "Outra operação está em andamento. Atualize e tente novamente.");
        if (postgres?.SqlState == Npgsql.PostgresErrorCodes.QueryCanceled)
            error = new(503, "consulta_timeout", "A consulta excedeu o tempo permitido. Tente novamente.");
        if (error is null && context.Exception is ArgumentException &&
            context.ActionDescriptor.EndpointMetadata.OfType<AnimalEvolutionAttribute>().Any())
            error = new(400, "dados_invalidos", context.Exception.Message);
        if (error is null) return;
        var problem = new ProblemDetails { Status = error.Status, Title = error.Message };
        problem.Extensions["code"] = error.Code;
        context.Result = new ObjectResult(problem) { StatusCode = error.Status };
        context.ExceptionHandled = true;
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class AnimalEvolutionAttribute : Attribute;
