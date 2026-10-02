namespace GenSW.API.Contracts.Species;

public sealed record UpdateEspecieRequest(string NomeComum, string? NomeCientifico, bool Ovipara = false, decimal? PesoPadraoOvoGramas = null);
