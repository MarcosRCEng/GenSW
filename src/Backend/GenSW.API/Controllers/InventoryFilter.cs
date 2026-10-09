using GenSW.Application.Inventory;
using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Npgsql;

namespace GenSW.API.Controllers;

public sealed class InventoryFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (!context.ActionDescriptor.EndpointMetadata.OfType<InventoryAttribute>().Any()) return;
        var (status, code, message) = context.Exception switch
        {
            InventoryException e => (e.Status, e.Code, e.Message),
            InventoryConflictException e => (409, e.Code, e.Message),
            CatalogConflictException e => (409, e.Code, e.Message),
            ArgumentException e => (400, "dados_invalidos", e.Message),
            OverflowException => (400, "limite_decimal", "Quantidade ou paginação excedeu o limite permitido."),
            _ => (0, "", "")
        };
        var pg = context.Exception as PostgresException ?? context.Exception.InnerException as PostgresException;
        if (pg?.SqlState == PostgresErrorCodes.LockNotAvailable)
            (status, code, message) = (409, "conflito_transitorio", "Outra operação está em andamento. Tente novamente com a mesma chave.");
        if (pg?.SqlState == PostgresErrorCodes.QueryCanceled)
            (status, code, message) = (409, "consulta_nao_concluida", "A consulta não foi concluída no prazo. Restrinja os filtros e tente novamente.");
        if (pg?.SqlState == PostgresErrorCodes.UniqueViolation)
            (status, code, message) = (409, "registro_duplicado", "Código ou posição já registrado. Atualize os dados antes de tentar novamente.");
        if (pg?.SqlState == PostgresErrorCodes.CheckViolation)
            (status, code, message) = (400, "dados_invalidos", "Dados incompatíveis com as regras de estoque.");
        if (pg?.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            (status, code, message) = (409, "referencia_invalida", "Uma referência mudou ou não está disponível. Atualize os dados.");
        if (status == 0) return;
        var problem = new ProblemDetails { Status = status, Title = message };
        problem.Extensions["code"] = code;
        context.Result = new ObjectResult(problem) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class InventoryAttribute : Attribute;
