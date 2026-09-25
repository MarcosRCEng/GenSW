using System.Text.Json.Serialization;

namespace GenSW.API.Contracts.AnimalIdentifications;

public sealed class UpdateIdentificacaoAnimalMetadataRequest
{
    private DateOnly? _dataAplicacao;
    private string? _observacao;

    public DateOnly? DataAplicacao
    {
        get => _dataAplicacao;
        set
        {
            _dataAplicacao = value;
            HasDataAplicacao = true;
        }
    }

    public string? Observacao
    {
        get => _observacao;
        set
        {
            _observacao = value;
            HasObservacao = true;
        }
    }

    [JsonIgnore]
    public bool HasDataAplicacao { get; private set; }

    [JsonIgnore]
    public bool HasObservacao { get; private set; }
}
