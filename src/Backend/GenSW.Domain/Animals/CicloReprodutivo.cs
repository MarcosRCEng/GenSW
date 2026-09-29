namespace GenSW.Domain.Animals;

public sealed class CicloReprodutivo
{
    private CicloReprodutivo() { }
    public Guid Id { get; private set; }
    public Guid CruzamentoId { get; private set; }
    public TipoCicloReprodutivo Tipo { get; private set; }
    public StatusCicloReprodutivo Status { get; private set; }
    public DateOnly? DataPostura { get; private set; }
    public DateOnly? DataInicioIncubacao { get; private set; }
    public DateOnly? DataEclosao { get; private set; }
    public int? OvosPostos { get; private set; }
    public int? OvosFerteis { get; private set; }
    public int? OvosIncubados { get; private set; }
    public int? OvosEclodidos { get; private set; }
    public int? OvosInviaveis { get; private set; }
    public decimal? PesoMedioOvoGramas { get; private set; }
    public DateOnly? DataInicioGestacao { get; private set; }
    public DateOnly? DataPrevistaParto { get; private set; }
    public DateOnly? DataParto { get; private set; }
    public int? Nascidos { get; private set; }
    public int? NascidosVivos { get; private set; }
    public int? NascidosMortos { get; private set; }
    public decimal? PesoAoNascerGramas { get; private set; }
    public string? Observacao { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static CicloReprodutivo Criar(Guid cruzamentoId, TipoCicloReprodutivo tipo, StatusCicloReprodutivo status, DateOnly? dataPostura, DateOnly? dataInicioIncubacao, DateOnly? dataEclosao, int? ovosPostos, int? ovosFerteis, int? ovosIncubados, int? ovosEclodidos, int? ovosInviaveis, decimal? pesoMedioOvoGramas, DateOnly? dataInicioGestacao, DateOnly? dataPrevistaParto, DateOnly? dataParto, int? nascidos, int? nascidosVivos, int? nascidosMortos, decimal? pesoAoNascerGramas, string? observacao, DateTimeOffset now)
    { if (cruzamentoId == Guid.Empty) throw new ArgumentException("Breeding is required."); var x = new CicloReprodutivo { Id = Guid.NewGuid(), CruzamentoId = cruzamentoId, CreatedAtUtc = now }; x.Apply(tipo, status, dataPostura, dataInicioIncubacao, dataEclosao, ovosPostos, ovosFerteis, ovosIncubados, ovosEclodidos, ovosInviaveis, pesoMedioOvoGramas, dataInicioGestacao, dataPrevistaParto, dataParto, nascidos, nascidosVivos, nascidosMortos, pesoAoNascerGramas, observacao, now); return x; }
    public void Atualizar(TipoCicloReprodutivo tipo, StatusCicloReprodutivo status, DateOnly? dataPostura, DateOnly? dataInicioIncubacao, DateOnly? dataEclosao, int? ovosPostos, int? ovosFerteis, int? ovosIncubados, int? ovosEclodidos, int? ovosInviaveis, decimal? pesoMedioOvoGramas, DateOnly? dataInicioGestacao, DateOnly? dataPrevistaParto, DateOnly? dataParto, int? nascidos, int? nascidosVivos, int? nascidosMortos, decimal? pesoAoNascerGramas, string? observacao, DateTimeOffset now)
        => Apply(tipo, status, dataPostura, dataInicioIncubacao, dataEclosao, ovosPostos, ovosFerteis, ovosIncubados, ovosEclodidos, ovosInviaveis, pesoMedioOvoGramas, dataInicioGestacao, dataPrevistaParto, dataParto, nascidos, nascidosVivos, nascidosMortos, pesoAoNascerGramas, observacao, now);
    public void AlterarStatus(StatusCicloReprodutivo status, DateTimeOffset now) { if (!Enum.IsDefined(status)) throw new ArgumentException("Cycle status is invalid."); Status = status; UpdatedAtUtc = now; }
    private void Apply(TipoCicloReprodutivo tipo, StatusCicloReprodutivo status, DateOnly? postura, DateOnly? inicioIncubacao, DateOnly? eclosao, int? postos, int? ferteis, int? incubados, int? eclodidos, int? inviaveis, decimal? pesoOvo, DateOnly? inicioGestacao, DateOnly? previstaParto, DateOnly? parto, int? nascidos, int? vivos, int? mortos, decimal? pesoNascer, string? observacao, DateTimeOffset now)
    {
        if (!Enum.IsDefined(tipo) || !Enum.IsDefined(status)) throw new ArgumentException("Cycle type or status is invalid.");
        ValidateNonNegative(postos, ferteis, incubados, eclodidos, inviaveis, nascidos, vivos, mortos); ValidateWeight(pesoOvo); ValidateWeight(pesoNascer);
        if (tipo == TipoCicloReprodutivo.Oviparo) { if (inicioIncubacao < postura || eclosao < inicioIncubacao || (ferteis.HasValue && postos.HasValue && ferteis > postos) || (incubados.HasValue && ferteis.HasValue && incubados > ferteis) || (eclodidos.HasValue && incubados.HasValue && eclodidos > incubados) || (inviaveis.HasValue && incubados.HasValue && inviaveis > incubados) || (eclodidos ?? 0) + (inviaveis ?? 0) > (incubados ?? int.MaxValue)) throw new ArgumentException("Oviparous cycle data is inconsistent."); if (inicioGestacao is not null || previstaParto is not null || parto is not null || nascidos is not null || vivos is not null || mortos is not null || pesoNascer is not null) throw new ArgumentException("Gestational data is not allowed for an oviparous cycle."); }
        else { if (previstaParto < inicioGestacao || parto < inicioGestacao || (vivos ?? 0) + (mortos ?? 0) > (nascidos ?? int.MaxValue)) throw new ArgumentException("Gestational cycle data is inconsistent."); if (postura is not null || inicioIncubacao is not null || eclosao is not null || postos is not null || ferteis is not null || incubados is not null || eclodidos is not null || inviaveis is not null || pesoOvo is not null) throw new ArgumentException("Oviparous data is not allowed for a gestational cycle."); }
        Tipo = tipo; Status = status; DataPostura = postura; DataInicioIncubacao = inicioIncubacao; DataEclosao = eclosao; OvosPostos = postos; OvosFerteis = ferteis; OvosIncubados = incubados; OvosEclodidos = eclodidos; OvosInviaveis = inviaveis; PesoMedioOvoGramas = pesoOvo; DataInicioGestacao = inicioGestacao; DataPrevistaParto = previstaParto; DataParto = parto; Nascidos = nascidos; NascidosVivos = vivos; NascidosMortos = mortos; PesoAoNascerGramas = pesoNascer; Observacao = Clean(observacao); UpdatedAtUtc = now;
    }
    private static void ValidateNonNegative(params int?[] values) { if (values.Any(x => x < 0)) throw new ArgumentException("Quantities cannot be negative."); }
    private static void ValidateWeight(decimal? value) { if (value is <= 0 or > 100000) throw new ArgumentException("Weight must be greater than zero and realistic."); }
    private static string? Clean(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; var clean = value.Trim(); if (clean.Length > 2000) throw new ArgumentException("Observacao is too long."); return clean; }
}
