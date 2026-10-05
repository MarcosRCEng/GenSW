using System.ComponentModel.DataAnnotations;

namespace GenSW.API.Contracts.Properties;

public sealed record PropriedadeRequest([Required] string Nome, string? Localizacao = null, string? Observacao = null);
public sealed record PropriedadeStatusRequest([Required] bool? Ativo);
public sealed record TransferirAnimalRequest(Guid PropriedadeId, [Required] DateOnly? DataInicio,
    Guid? VinculoAtualIdEsperado, string? Observacao = null);
public sealed record DesvincularAnimalRequest([Required] DateOnly? DataFim, [Required] Guid? VinculoAtualIdEsperado);
