using GenSW.API.Contracts.AnimalFiliations;
using GenSW.Application.Animals;
using GenSW.Application.Animals.Filiacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GenSW.API.Controllers;
[ApiController,Authorize,Route("api/v1/animais/{animalId:guid}")]
public sealed class FiliacoesAnimaisController(IFiliacaoAnimalService service):ControllerBase
{
 [HttpPost("filiacoes")] public async Task<ActionResult<FiliacaoAnimalResponse>> Create(Guid animalId,CreateFiliacaoAnimalRequest request,CancellationToken ct){try{var x=await service.CreateOrReplaceAsync(animalId,new(request.ProgenitorId,request.TipoFiliacao,request.DataRegistro),ct);return CreatedAtAction(nameof(List),new{animalId},To(x));}catch(AnimalNotFoundException){return NotFound();}catch(FiliacaoAnimalConflictException e){return Conflict(new ProblemDetails { Title=e.Message, Status=409, Extensions={ ["code"]="progenitor_inelegivel" } });}catch(ArgumentException){return BadRequest(new ProblemDetails { Title="Dados de filiação inválidos.", Status=400, Extensions={ ["code"]="dados_invalidos" } });}}
 [HttpGet("filiacoes")] public async Task<ActionResult<IReadOnlyList<FiliacaoAnimalResponse>>> List(Guid animalId,CancellationToken ct){try{return Ok((await service.ListAsync(animalId,ct)).Select(To).ToArray());}catch(AnimalNotFoundException){return NotFound();}}
 [HttpGet("pedigree")] public async Task<ActionResult<PedigreeAnimalResponse>> Pedigree(Guid animalId,[FromQuery]int generations=3,CancellationToken ct=default){try{return Ok(To(await service.GetPedigreeAsync(animalId,generations,ct)));}catch(AnimalNotFoundException){return NotFound();}catch(ArgumentOutOfRangeException){return BadRequest(Problem("Generations must be between 1 and 8.",statusCode:400));}}
 private static FiliacaoAnimalResponse To(FiliacaoAnimalResult x)=>new(x.Id,x.AnimalId,x.ProgenitorId,x.TipoFiliacao,x.Ativa,x.DataRegistro,x.DataFim,x.CreatedAtUtc,x.UpdatedAtUtc,x.Progenitor);
 private static PedigreeAnimalResponse To(PedigreeAnimalResult x)=>new(x.AnimalId,x.CodigoInterno,x.Nome,x.Progenitores.Select(To).ToArray());
}
