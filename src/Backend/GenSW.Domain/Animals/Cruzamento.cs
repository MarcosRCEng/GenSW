namespace GenSW.Domain.Animals;

public sealed class Cruzamento
{
    private Cruzamento() { }
    private Cruzamento(Guid machoId, Guid femeaId, StatusCruzamento status, DateOnly? dataInicio, DateOnly? dataFim, string? objetivo, string? observacao, DateTimeOffset now)
    { Id = Guid.NewGuid(); MachoId = machoId; FemeaId = femeaId; Apply(status, dataInicio, dataFim, objetivo, observacao, now); CreatedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid MachoId { get; private set; }
    public Guid FemeaId { get; private set; }
    public StatusCruzamento Status { get; private set; }
    public DateOnly? DataInicio { get; private set; }
    public DateOnly? DataFim { get; private set; }
    public string? Objetivo { get; private set; }
    public string? Observacao { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public static Cruzamento Criar(Guid machoId, Guid femeaId, StatusCruzamento status, DateOnly? dataInicio, DateOnly? dataFim, string? objetivo, string? observacao, DateTimeOffset now)
    { ValidateAnimals(machoId, femeaId); return new(machoId, femeaId, status, dataInicio, dataFim, objetivo, observacao, now); }
    public void Atualizar(Guid machoId, Guid femeaId, StatusCruzamento status, DateOnly? dataInicio, DateOnly? dataFim, string? objetivo, string? observacao, DateTimeOffset now)
    { ValidateAnimals(machoId, femeaId); MachoId = machoId; FemeaId = femeaId; Apply(status, dataInicio, dataFim, objetivo, observacao, now); }
    public void AlterarStatus(StatusCruzamento status, DateTimeOffset now)
    { if (!Enum.IsDefined(status)) throw new ArgumentException("Breeding status is invalid."); Status = status; UpdatedAtUtc = now; }
    private void Apply(StatusCruzamento status, DateOnly? inicio, DateOnly? fim, string? objetivo, string? observacao, DateTimeOffset now)
    { if (!Enum.IsDefined(status)) throw new ArgumentException("Breeding status is invalid."); if (fim < inicio) throw new ArgumentException("End date cannot precede start date."); Status = status; DataInicio = inicio; DataFim = fim; Objetivo = Clean(objetivo, 500, nameof(objetivo)); Observacao = Clean(observacao, 2000, nameof(observacao)); UpdatedAtUtc = now; }
    private static void ValidateAnimals(Guid machoId, Guid femeaId) { if (machoId == Guid.Empty || femeaId == Guid.Empty) throw new ArgumentException("Male and female are required."); if (machoId == femeaId) throw new ArgumentException("The same animal cannot be both male and female."); }
    private static string? Clean(string? value, int maximum, string parameter) { if (string.IsNullOrWhiteSpace(value)) return null; var clean = value.Trim(); if (clean.Length > maximum) throw new ArgumentException($"{parameter} is too long."); return clean; }
}
