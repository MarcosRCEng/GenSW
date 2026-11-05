using GenSW.Application.Inventory;
using GenSW.Application.MaterialCosts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Npgsql;

namespace GenSW.API.Controllers;

[AttributeUsage(AttributeTargets.Class)]public sealed class MaterialCostsAttribute:Attribute;
public sealed class MaterialCostsFilter:IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if(context.ExceptionHandled)return;
        if(context.Exception is not MaterialCostException&&!context.ActionDescriptor.EndpointMetadata.OfType<MaterialCostsAttribute>().Any())return;
        var(status,code,message)=context.Exception switch{
            MaterialCostException e=>(e.Status,e.Code,e.Message),InventoryException e=>(e.Status,e.Code,e.Message),
            ArgumentException e=>(400,"dados_invalidos",e.Message),OverflowException=>(400,"limite_monetario","Cálculo excede os limites monetários permitidos."),
            _=>(500,"pedido_nao_confirmado","Não foi possível confirmar o pedido. Preserve a chave e consulte o resultado antes de repetir.")};
        var pg=context.Exception as PostgresException??context.Exception.InnerException as PostgresException;
        if(pg?.SqlState is PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)(status,code,message)=(409,"conflito_transitorio","Operação concorrente; mantenha a chave e consulte ou repita o pedido original.");
        if(pg?.SqlState==PostgresErrorCodes.QueryCanceled)(status,code,message)=(409,"consulta_nao_concluida","A consulta não foi concluída; não equivale à ausência de custo.");
        if(pg?.SqlState==PostgresErrorCodes.CheckViolation)(status,code,message)=(400,"invariante_custo","Pedido não preserva as regras monetárias.");
        if(pg?.SqlState==PostgresErrorCodes.UniqueViolation)(status,code,message)=(409,"origem_duplicada","Origem, comando ou compensação já registrado.");
        if(pg?.SqlState==PostgresErrorCodes.ForeignKeyViolation)(status,code,message)=(409,"referencia_invalida","Referência monetária indisponível.");
        if(status==0)return;var problem=new ProblemDetails{Status=status,Title=message};problem.Extensions["code"]=code;
        context.Result=new ObjectResult(problem){StatusCode=status};context.ExceptionHandled=true;
    }
}
