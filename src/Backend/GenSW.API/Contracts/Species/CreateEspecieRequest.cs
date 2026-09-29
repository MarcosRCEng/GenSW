namespace GenSW.API.Contracts.Species;

public sealed record CreateEspecieRequest(string NomeComum, string? NomeCientifico, bool Ovipara = false, decimal? PesoPadraoOvoGramas = null);
