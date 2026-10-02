using System.Security.Claims;
using GenSW.Application.Financial;
using GenSW.Domain.Financial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController, Authorize, Route("api/v1/financeiro")]
public sealed class FinanceiroController(FinancialService service) : ControllerBase
{
    private ObjectResult Error(string title, int status, string code) { var problem = new ProblemDetails { Title = title, Status = status }; problem.Extensions["code"] = code; return new ObjectResult(problem) { StatusCode = status }; }
    private Guid Autor => Guid.Parse(User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Key => Request.Headers["Idempotency-Key"].ToString();
    private async Task<IActionResult> Run<T>(Func<Task<T>> action, bool created = false)
    {
        try { var result = await action(); return new JsonResult(result) { StatusCode = created ? 201 : 200 }; }
        catch (CaixaNotFoundException e) { return Error(e.Message, 404, "nao_encontrado"); }
        catch (CaixaConflictException e) { return Error(e.Message, 409, "conflito_caixa"); }
        catch (ArgumentException e) { return Error(e.Message, 400, "dados_invalidos"); }
        catch (OverflowException) { return Problem(title: "Limite financeiro ou de paginação excedido.", statusCode: 400); }
    }
    [HttpGet("configuracao")]
    public Task<IActionResult> Config(CancellationToken ct) => Run(() => service.ConfiguracaoAsync(ct));
    [HttpPost("configuracao"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Configure(ConfiguracaoCommand c, CancellationToken ct) => Run(() => service.ConfigureAsync(c, Autor, ct));
    [HttpGet("categorias")]
    public Task<IActionResult> Categories(CancellationToken ct) => Run(() => service.CategoriasAsync(ct));
    [HttpPost("categorias")]
    public Task<IActionResult> CreateCategory(CategoriaCommand c, CancellationToken ct) => Run(() => service.CreateCategoriaAsync(c, ct), true);
    [HttpPut("categorias/{id:guid}")]
    [HttpPatch("categorias/{id:guid}/ativo")]
    public Task<IActionResult> UpdateCategory(Guid id, CategoriaUpdateCommand c, CancellationToken ct) => Run(() => service.UpdateCategoriaAsync(id, c, ct));
    [HttpGet("lancamentos")]
    public Task<IActionResult> List([FromQuery] LancamentoQuery q, CancellationToken ct) => Run(() => service.ListAsync(q, ct));
    [HttpGet("lancamentos/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => Run(() => service.GetAsync(id, ct));
    [HttpPost("lancamentos")]
    public Task<IActionResult> Create(LancamentoCommand c, CancellationToken ct) => Run(() => service.CreateAsync(c, Autor, Key, ct), true);
    [HttpPut("lancamentos/{id:guid}")]
    public Task<IActionResult> Update(Guid id, CorrecaoCommand c, CancellationToken ct) => Run(() => service.UpdateAsync(id, c, Autor, ct));
    [HttpPost("lancamentos/{id:guid}/cancelamento")]
    public Task<IActionResult> Cancel(Guid id, CancelamentoCommand c, CancellationToken ct) => Run(() => service.CancelAsync(id, c, Autor, ct));
    [HttpGet("lancamentos/{id:guid}/historico")]
    public Task<IActionResult> History(Guid id, CancellationToken ct) => Run(() => service.HistoricoAsync(id, ct));
    [HttpPost("lancamentos/{id:guid}/ajuste")]
    public Task<IActionResult> Adjust(Guid id, AjusteCommand c, CancellationToken ct) => Run(() => service.AdjustAsync(id, c, Autor, Key, ct));
    [HttpGet("meses/{ano:int}/{mes:int}")]
    public Task<IActionResult> Summary(int ano, int mes, CancellationToken ct) => Run(() => service.ResumoAsync(new DateOnly(ano, mes, 1), ct));
    [HttpGet("meses/{ano:int}/{mes:int}/fechamento")]
    public Task<IActionResult> Snapshot(int ano, int mes, CancellationToken ct) => Run(() => service.FechamentoAsync(new DateOnly(ano, mes, 1), ct));
    [HttpGet("fechamentos")]
    public Task<IActionResult> Snapshots(CancellationToken ct) => Run(() => service.FechamentosAsync(ct));
    [HttpPost("meses/{ano:int}/{mes:int}/fechamento"), Authorize(Roles = "Admin")]
    public Task<IActionResult> Close(int ano, int mes, FechamentoCommand c, CancellationToken ct) => Run(() => service.CloseAsync(new DateOnly(ano, mes, 1), c, Autor, ct));
}
