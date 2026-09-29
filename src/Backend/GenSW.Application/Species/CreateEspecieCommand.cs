namespace GenSW.Application.Species;

public sealed record CreateEspecieCommand(string NomeComum, string? NomeCientifico, bool Ovipara = false, decimal? PesoPadraoOvoGramas = null);
