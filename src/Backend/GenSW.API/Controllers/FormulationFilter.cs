using GenSW.Application.Formulation;
using GenSW.Domain.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GenSW.API.Controllers;

public sealed class FormulationFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (!context.ActionDescriptor.EndpointMetadata.OfType<FormulationAttribute>().Any()) return;
        var exception = context.Exception;
        var (status, code, message) = exception switch
        {
            FormulationException e => (e.Status, e.Code, e.Message),
            CatalogConflictException e => (409, e.Code, e.Message),
            ArgumentException e => (400, "dados_invalidos", e.Message),
            OverflowException => (400, "limite_decimal", "Cálculo excedeu o limite decimal."),
            _ => (0, "", "")
        };
        var pg = exception as Npgsql.PostgresException ?? exception.InnerException as Npgsql.PostgresException;
        if (pg?.SqlState == Npgsql.PostgresErrorCodes.LockNotAvailable) (status, code, message) = (409, "conflito_transitorio", "Outra operação está em andamento. Tente novamente com a mesma chave.");
        if (pg?.SqlState == Npgsql.PostgresErrorCodes.CheckViolation) (status, code, message) = (400, "dados_invalidos", "Dados incompatíveis com as regras de persistência.");
        if (status == 0) return;
        var problem = new ProblemDetails { Status = status, Title = message }; problem.Extensions["code"] = code;
        context.Result = new ObjectResult(problem) { StatusCode = status }; context.ExceptionHandled = true;
    }
}
[AttributeUsage(AttributeTargets.Class)] public sealed class FormulationAttribute : Attribute;
