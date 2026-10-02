namespace GenSW.Application.Species;

public sealed record UpdateEspecieCommand(string NomeComum, string? NomeCientifico, bool Ovipara = false, decimal? PesoPadraoOvoGramas = null);
