using GenSW.Domain.Catalog;

namespace GenSW.Domain.Formulation;

public sealed record RecipeHeader(string Codigo, string Nome, string Finalidade);
public sealed class Recipe
{
    private Recipe() { }
    public Guid Id { get; private set; }
    public string Codigo { get; private set; } = "";
    public string CodigoNormalizado { get; private set; } = "";
    public string Nome { get; private set; } = "";
    public string Finalidade { get; private set; } = "";
    public bool Ativo { get; private set; } = true;
    public int Revisao { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public static Recipe Create(RecipeHeader data, DateTimeOffset now)
    { var r = new Recipe { Id = Guid.NewGuid(), CreatedAtUtc = now }; r.Update(data, 0); return r; }
    public void Update(RecipeHeader data, int expected)
    { CatalogRules.Expected(Revisao, expected); Codigo = CatalogRules.Text(data.Codigo, 50, "Código"); CodigoNormalizado = Codigo.ToUpperInvariant(); Nome = CatalogRules.Text(data.Nome, 200, "Nome"); Finalidade = CatalogRules.Text(data.Finalidade, 2000, "Finalidade"); Revisao++; }
    public void SetActive(bool active, int expected)
    { CatalogRules.Expected(Revisao, expected); Ativo = active; Revisao++; }
}

public sealed record RecipeInput(Guid Id, Guid ItemId, string Papel, string Quantidade, string Unidade, string Escala,
    Guid? PerfilId = null, Guid? ConversaoId = null, Guid? SubReceitaVersaoId = null, Guid? SubSaidaId = null,
    string? InclusaoMinima = null, string? InclusaoMaxima = null);
public sealed record RecipeOutput(Guid Id, Guid ItemId, string Nome, string Quantidade, string Unidade, string Escala,
    bool Principal, Guid? PerfilId = null, Guid? ConversaoId = null, string? MassaLiquidaKg = null,
    string? MassaDrenadaKg = null, string? MassaBrutaKg = null, string? MetodoMedicao = null);
public sealed record RecipeLoss(string Quantidade, string Unidade, string Motivo, string Escala = "Variavel");
public sealed record RecipeStep(int Ordem, string Descricao);
public sealed record RetentionParameter(string Componente, string Fator, string Hipotese);
public sealed record RecipeData(string Tipo, string Modo, string TamanhoReferencia, string UnidadeReferencia,
    IReadOnlyList<RecipeInput> Entradas, IReadOnlyList<RecipeOutput> Saidas, IReadOnlyList<RecipeLoss> Perdas,
    IReadOnlyList<RecipeStep> Etapas, string? Observacao, IReadOnlyList<RetentionParameter> Retencoes)
{
    public void Validate(bool publication)
    {
        if (Tipo is not ("MisturaSimples" or "Processamento") || Modo is not ("Quantidade" or "Percentual")) throw new ArgumentException("Tipo/modo de receita inválido.");
        var reference = CatalogRules.Quantity(TamanhoReferencia, UnidadeReferencia);
        if (Modo == "Percentual" && UnidadeReferencia != "kg") throw new ArgumentException("Percentuais exigem referência em kg BN.");
        if (Entradas is null || Saidas is null || Perdas is null || Etapas is null || Retencoes is null ||
            Entradas.Count > 500 || Saidas.Count > 100 || Etapas.Count > 100 || Perdas.Count > 100) throw new ArgumentException("Limites de linhas/etapas excedidos.");
        if (publication && (Entradas.Count == 0 || Saidas.Count == 0 || Saidas.Count(x => x.Principal) != 1)) throw new ArgumentException("Publicação exige entradas e uma saída principal.");
        if (Entradas.Any(x => x.Id == Guid.Empty || x.ItemId == Guid.Empty) || Saidas.Any(x => x.Id == Guid.Empty || x.ItemId == Guid.Empty) ||
            Entradas.Select(x => x.Id).Distinct().Count() != Entradas.Count || Saidas.Select(x => x.Id).Distinct().Count() != Saidas.Count)
            throw new ArgumentException("Identificadores de linha devem ser únicos e não vazios.");
        foreach (var line in Entradas)
        {
            if (line.Papel is not ("Alimentar" or "OutroMaterialIncorporado" or "Embalagem" or "Consumivel")) throw new ArgumentException("Papel da linha inválido.");
            if (line.Escala is not ("Variavel" or "Fixa")) throw new ArgumentException("Escala deve ser Variavel ou Fixa.");
            CatalogRules.Quantity(line.Quantidade, line.Unidade);
            if (Modo == "Percentual" && Incorporated(line))
            {
                if (line.Unidade != "kg" || line.Escala != "Variavel") throw new ArgumentException("Percentual alimentar exige kg e escala variável.");
            }
            if (line.SubReceitaVersaoId.HasValue != line.SubSaidaId.HasValue) throw new ArgumentException("Sub-receita exige versão e saída específicas.");
            var min = line.InclusaoMinima is null ? 0m : CatalogRules.Decimal(line.InclusaoMinima);
            var max = line.InclusaoMaxima is null ? 100m : CatalogRules.Decimal(line.InclusaoMaxima);
            if (min > max || max > 100m) throw new ArgumentException("Limites de inclusão inválidos.");
        }
        if (publication && Modo == "Percentual" && Entradas.Where(Incorporated).Sum(x => CatalogRules.Decimal(x.Quantidade)) != 100m)
            throw new ArgumentException("Percentuais incorporados devem totalizar exatamente 100; não há normalização automática.");
        foreach (var line in Saidas)
        {
            CatalogRules.Quantity(line.Quantidade, line.Unidade); CatalogRules.Text(line.Nome, 200, "Saída");
            if (line.Escala is not ("Variavel" or "Fixa")) throw new ArgumentException("Escala da saída inválida.");
            decimal? liquid = line.MassaLiquidaKg is null ? null : CatalogRules.Decimal(line.MassaLiquidaKg);
            decimal? drained = line.MassaDrenadaKg is null ? null : CatalogRules.Decimal(line.MassaDrenadaKg);
            decimal? gross = line.MassaBrutaKg is null ? null : CatalogRules.Decimal(line.MassaBrutaKg);
            if ((drained.HasValue && liquid.HasValue && drained > liquid) || (liquid.HasValue && gross.HasValue && liquid > gross)) throw new ArgumentException("Exige-se drenado ≤ líquido ≤ bruto no mesmo escopo.");
            if (liquid.HasValue || drained.HasValue || gross.HasValue) CatalogRules.Text(line.MetodoMedicao, 2000, "Método/escopo da medição esperada");
        }
        foreach (var loss in Perdas) { CatalogRules.Quantity(loss.Quantidade, loss.Unidade); CatalogRules.Text(loss.Motivo, 2000, "Motivo da perda"); if (loss.Escala is not ("Variavel" or "Fixa")) throw new ArgumentException("Escala da perda inválida."); }
        if (Etapas.Select(x => x.Ordem).Distinct().Count() != Etapas.Count) throw new ArgumentException("Ordem das etapas duplicada.");
        foreach (var step in Etapas) { if (step.Ordem < 1) throw new ArgumentException("Ordem da etapa positiva."); CatalogRules.Text(step.Descricao, 2000, "Etapa"); }
        CatalogRules.Optional(Observacao);
        if (Retencoes.Select(x => x.Componente).Distinct().Count() != Retencoes.Count) throw new ArgumentException("Retenção duplicada.");
        foreach (var retention in Retencoes) { NutritionComponents.Get(retention.Componente); if (CatalogRules.Decimal(retention.Fator) > 1m) throw new ArgumentException("Retenção entre 0 e 1."); CatalogRules.Text(retention.Hipotese, 2000, "Hipótese da retenção"); }
        if (publication && Tipo == "MisturaSimples")
        {
            if (Saidas.Count != 1 || Perdas.Count != 0 || Retencoes.Count != 0 || Saidas[0].Escala != "Variavel" ||
                CatalogRules.Dimension(UnidadeReferencia) != "massa" || CatalogRules.Dimension(Saidas[0].Unidade) != "massa" || Entradas.Where(Incorporated).Any(x => x.Escala != "Variavel"))
                throw new ArgumentException("Mistura simples exige saída única, massa conservada e ausência declarada de processamento/perdas.");
            // Contextual mass conversions and equality are checked with exact selected versions by Application.
            if (Modo == "Percentual" && CatalogRules.Canonical(CatalogRules.Decimal(Saidas[0].Quantidade), Saidas[0].Unidade) != reference)
                throw new ArgumentException("Saída da mistura deve igualar a massa incorporada de referência.");
        }
        if (Retencoes.Count > 0 && (Tipo != "Processamento" || Saidas.Count != 1)) throw new ArgumentException("Estimativa por retenção suporta somente processamento com saída única.");
    }
    public static bool Incorporated(RecipeInput line) => line.Papel is "Alimentar" or "OutroMaterialIncorporado";
    public RecipeData Scale(string size, string unit)
    {
        if (CatalogRules.Dimension(unit) != CatalogRules.Dimension(UnidadeReferencia)) throw new ArgumentException("Escalonamento exige mesma dimensão da referência.");
        var ratio = CatalogRules.Canonical(CatalogRules.Quantity(size, unit), unit) / CatalogRules.Canonical(CatalogRules.Quantity(TamanhoReferencia, UnidadeReferencia), UnidadeReferencia);
        string ScaleValue(string q, string u, string mode)
        {
            var result = mode == "Fixa" ? CatalogRules.Decimal(q) : checked(CatalogRules.Decimal(q) * ratio);
            if (result > CatalogRules.Maximum || decimal.Round(result, 6, MidpointRounding.ToEven) != result) throw new ArgumentException("Escala excede precisão; ajuste explicitamente as quantidades.");
            if (u == "un" && decimal.Truncate(result) != result) throw new ArgumentException("Escala exige fração de unidade; ajuste explicitamente, sem arredondamento automático.");
            return CatalogRules.Format(result);
        }
        return this with
        {
            TamanhoReferencia = size,
            UnidadeReferencia = unit,
            Modo = "Quantidade",
            Entradas = Entradas.Select(x => x with { Quantidade = ScaleValue(Modo == "Percentual" && Incorporated(x) ? CatalogRules.Format(CatalogRules.Decimal(x.Quantidade) * CatalogRules.Decimal(TamanhoReferencia) / 100m) : x.Quantidade, x.Unidade, x.Escala) }).ToArray(),
            Saidas = Saidas.Select(x => x with
            {
                Quantidade = ScaleValue(x.Quantidade, x.Unidade, x.Escala),
                MassaLiquidaKg = x.MassaLiquidaKg is null ? null : ScaleValue(x.MassaLiquidaKg, "kg", x.Escala),
                MassaDrenadaKg = x.MassaDrenadaKg is null ? null : ScaleValue(x.MassaDrenadaKg, "kg", x.Escala),
                MassaBrutaKg = x.MassaBrutaKg is null ? null : ScaleValue(x.MassaBrutaKg, "kg", x.Escala)
            }).ToArray(),
            Perdas = Perdas.Select(x => x with { Quantidade = ScaleValue(x.Quantidade, x.Unidade, x.Escala) }).ToArray()
        };
    }
}

public sealed class RecipeVersion
{
    private RecipeVersion() { }
    public Guid Id { get; private set; }
    public Guid ReceitaId { get; private set; }
    public int Numero { get; private set; }
    public int Revisao { get; private set; }
    public string Estado { get; private set; } = "Rascunho";
    public string ConteudoJson { get; private set; } = "";
    public Guid AutorId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public RecipeData Data() => FormulationJson.Read<RecipeData>(ConteudoJson);
    public static RecipeVersion Create(Guid recipe, int number, RecipeData data, Guid author, DateTimeOffset now)
    { var version = new RecipeVersion { Id = Guid.NewGuid(), ReceitaId = recipe, Numero = number, AutorId = author, CreatedAtUtc = now }; version.Update(data, 0); return version; }
    public void Update(RecipeData data, int expected)
    { CatalogRules.Expected(Revisao, expected); Editable(); data.Validate(false); ConteudoJson = FormulationJson.Write(data); Revisao++; }
    public void Publish(int expected, DateTimeOffset now)
    { CatalogRules.Expected(Revisao, expected); Editable(); Data().Validate(true); Estado = "Publicado"; PublishedAtUtc = now; Revisao++; }
    public void Inactivate(int expected)
    { CatalogRules.Expected(Revisao, expected); if (Estado != "Publicado") throw new CatalogConflictException("estado_invalido", "Somente publicado pode ser inativado."); Estado = "Inativo"; Revisao++; }
    private void Editable()
    { if (Estado != "Rascunho") throw new CatalogConflictException("versao_imutavel", "Versão publicada é imutável. Crie nova revisão."); }
}

// Relational references protect published documents against deletion of their exact dependencies.
public sealed record RecipeReference(Guid Id, Guid VersaoId, Guid ItemId, Guid? PerfilId, Guid? ConversaoId, Guid? SubVersaoId);
