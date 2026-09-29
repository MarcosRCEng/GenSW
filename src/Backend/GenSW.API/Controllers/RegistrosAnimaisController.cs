using GenSW.API.Contracts.AnimalRegistrations;
using GenSW.Application.Animals;
using GenSW.Application.Animals.Registros;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GenSW.API.Controllers;

[ApiController, Authorize, Route("api/v1/animais/{animalId:guid}/registros")]
public sealed class RegistrosAnimaisController(IRegistroAnimalService registros) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RegistroAnimalResponse>> Create(Guid animalId, CreateRegistroAnimalRequest request, CancellationToken cancellationToken)
    { try { var item = await registros.CreateAsync(animalId, new(request.TipoRegistro, request.NumeroRegistro, request.DataInicio), cancellationToken); return CreatedAtAction(nameof(List), new { animalId }, ToResponse(item)); }
      catch (AnimalNotFoundException) { return NotFound(); } catch (RegistroAnimalDuplicateException) { return Conflict(Problem("Institutional registration already exists.", statusCode: 409)); }
      catch (RegistroAnimalActiveTypeConflictException) { return Conflict(Problem("Animal already has an active registration of this type.", statusCode: 409)); }
      catch (ArgumentException) { return BadRequest(Problem("The submitted registration data is invalid.", statusCode: 400)); } }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RegistroAnimalResponse>>> List(Guid animalId, CancellationToken cancellationToken)
    { try { return Ok((await registros.ListByAnimalAsync(animalId, cancellationToken)).Select(ToResponse).ToArray()); } catch (AnimalNotFoundException) { return NotFound(); } }
    [HttpPatch("{registroId:guid}/inativar")]
    public async Task<ActionResult<RegistroAnimalResponse>> Inactivate(Guid animalId, Guid registroId, InactivateRegistroAnimalRequest request, CancellationToken cancellationToken)
    { try { return Ok(ToResponse(await registros.InactivateAsync(animalId, registroId, new(request.DataFim), cancellationToken))); }
      catch (AnimalNotFoundException) { return NotFound(); } catch (RegistroAnimalNotFoundException) { return NotFound(); }
      catch (ArgumentException) { return BadRequest(Problem("The submitted registration data is invalid.", statusCode: 400)); } }
    private static RegistroAnimalResponse ToResponse(RegistroAnimalResult item) => new(item.Id, item.AnimalId, item.TipoRegistro, item.NumeroRegistro, item.Ativo, item.DataInicio, item.DataFim, item.CreatedAtUtc, item.UpdatedAtUtc);
}
