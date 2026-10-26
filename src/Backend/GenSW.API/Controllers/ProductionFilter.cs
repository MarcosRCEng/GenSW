using GenSW.Application.Production;
using GenSW.Application.Inventory;
using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Npgsql;

namespace GenSW.API.Controllers;

[AttributeUsage(AttributeTargets.Class)]public sealed class ProductionAttribute:Attribute;
public sealed class ProductionFilter(ILogger<ProductionFilter> logger):IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if(!context.ActionDescriptor.EndpointMetadata.OfType<ProductionAttribute>().Any())return;
        var(status,code,message)=context.Exception switch {
            ProductionException e=>(e.Status,e.Code,e.Message),InventoryException {Code:"invariante_estoque"}=>(500,"invariante_producao","Invariante persistida impediu a operação. Preserve a evidência e solicite reconciliação."),InventoryException e=>(e.Status,e.Code,e.Message),InventoryConflictException e=>(409,e.Code,e.Message),CatalogConflictException e=>(409,e.Code,e.Message),
            ArgumentException e=>(400,"dados_invalidos",e.Message),OverflowException=>(400,"limite_decimal","Quantidade/paginação excedeu o limite."),
            _=>(500,"falha_operacional","Operação não concluída. Consulte o comando com a mesma chave e informe a correlação ao responsável.")};
        var pg=context.Exception as PostgresException??context.Exception.InnerException as PostgresException;
        if(pg?.SqlState is PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure)(status,code,message)=(409,"conflito_transitorio","Operação concorrente em andamento; tente novamente com a mesma chave.");
        if(pg?.SqlState==PostgresErrorCodes.UniqueViolation)(status,code,message)=(409,"registro_duplicado","Código ou identidade já utilizado. Confira o registro e a chave.");
        if(pg?.SqlState==PostgresErrorCodes.ForeignKeyViolation)(status,code,message)=(409,"referencia_invalida","Uma referência mudou ou não está disponível. Atualize os dados.");
        if(pg?.SqlState==PostgresErrorCodes.QueryCanceled)(status,code,message)=(409,"consulta_nao_concluida","Consulta não concluída no prazo. Restrinja os filtros.");
        if(pg?.SqlState==PostgresErrorCodes.CheckViolation)(status,code,message)=(500,"invariante_producao","Invariante persistida impediu a operação. Preserve a evidência e solicite reconciliação.");
        if(status==500)logger.LogError("Production command failed. Correlation {Correlation}, exception type {ExceptionType}",context.HttpContext.TraceIdentifier,context.Exception.GetType().Name);
        var problem=new ProblemDetails{Status=status,Title=message};problem.Extensions["code"]=code;problem.Extensions["correlationId"]=context.HttpContext.TraceIdentifier;context.Result=new ObjectResult(problem){StatusCode=status};context.ExceptionHandled=true;
    }
}
