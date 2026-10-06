using System.Text.Json;
using GenSW.Domain.Catalog;

namespace GenSW.Domain.Formulation;

public static class FormulationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw new ArgumentException("Documento vazio.");
}

public sealed record ComponentDefinition(string Codigo, string Nome, string Grandeza, string Semantica, int Versao = 1);
public static class NutritionComponents
{
    public static readonly IReadOnlyList<ComponentDefinition> All = [
        new("UMIDADE", "Umidade", "massa", "Água na mesma amostra, BN; complementar à MS."),
        new("MS", "Matéria seca", "massa", "Fração seca na mesma amostra, BN; complementar à umidade."),
        new("PB", "Proteína bruta", "massa", "Proteína bruta pelo método/fator de nitrogênio declarado; não proteína digestível."),
        new("GORDURA", "Gordura", "massa", "Lipídios declarados pelo método informado; não equivalente automático a EE."),
        new("EE", "Extrato etéreo", "massa", "Fração extraída pelo método informado."),
        new("FB", "Fibra bruta", "massa", "Fibra bruta pelo método informado; distinta de FDN/FDA/fibra alimentar."),
        new("EB", "Energia bruta", "energia", "Energia bruta; contexto obrigatório."),
        new("ED", "Energia digestível", "energia", "Energia digestível para espécie/fase/contexto informados."),
        new("EM", "Energia metabolizável", "energia", "Energia metabolizável para espécie/fase/contexto informados."),
        new("EMAn", "EM aparente corrigida por nitrogênio", "energia", "Correção por nitrogênio explicitada; não equivalente a EM."),
        new("EL", "Energia líquida", "energia", "Finalidade e contexto de energia líquida informados."),
        new("CINZAS", "Cinzas", "massa", "Resíduo do método; não minerais disponíveis."),
        new("AMIDO", "Amido", "massa", "Amido analisado; não carboidratos por diferença."),
        new("FDN", "Fibra em detergente neutro", "massa", "Método deve identificar correções/amilase; não somar frações sobrepostas."),
        new("FDA", "Fibra em detergente ácido", "massa", "Método específico; não equivalente a FB/FDN."),
        new("LIGNINA", "Lignina", "massa", "Fração pelo método declarado."),
        new("CA", "Cálcio total", "massa", "Cálcio total; não disponível."),
        new("P_TOTAL", "Fósforo total", "massa", "Fósforo total; não disponível/digestível."),
        new("NA", "Sódio", "massa", "Sódio pelo método/base informados."),
        new("LISINA", "Lisina total", "massa", "Aminoácido total; não digestível."),
        new("METIONINA", "Metionina total", "massa", "Aminoácido total; não metionina+cistina.")
    ];
    public static ComponentDefinition Get(string code) => All.SingleOrDefault(x => x.Codigo == code) ?? throw new ArgumentException("Componente sem definição semântica.");
    public static decimal Normalize(string code, decimal value, string unit)
    {
        var energy = Get(code).Grandeza == "energia";
        return (energy, unit) switch
        {
            (true, "MJ/kg") => value,
            (true, "kcal/kg") => checked(value * 0.004184m),
            (false, "g/kg") => value,
            (false, "g/100g") or (false, "%") => checked(value * 10m),
            (false, "mg/kg") => value / 1000m,
            _ => throw new ArgumentException("Unidade incompatível com a definição do componente.")
        };
    }
}

public sealed record NutritionValue(string Componente, string Estado, string? Valor, string? Origem,
    string Base, string Unidade, string Metodo, string Contexto, string Qualificador = "Pontual",
    string? Motivo = null, string? Fonte = null, string? Hipotese = null);

public sealed record ProfileData(string Nome, string Fonte, string Metodo, string Preparacao, string ReferenciaAmostra,
    string Contexto, Guid? EspecieId, string? Fase, DateOnly? DataFonte, DateOnly? DataColeta,
    IReadOnlyList<NutritionValue> Valores)
{
    public void Validate(bool publication)
    {
        CatalogRules.Text(Nome, 200, "Nome do perfil");
        if (Valores is null || Valores.Count > 100) throw new ArgumentException("Até 100 observações por perfil.");
        if (publication)
        {
            CatalogRules.Text(Fonte, 1000, "Fonte"); CatalogRules.Text(Metodo, 500, "Método");
            CatalogRules.Text(Preparacao, 500, "Preparação/parte"); CatalogRules.Text(ReferenciaAmostra, 200, "Amostra");
            CatalogRules.Text(Contexto, 2000, "Aplicabilidade/contexto");
            if (Valores.Count == 0) throw new ArgumentException("Perfil publicado exige observações.");
        }
        foreach (var value in Valores)
        {
            var definition = NutritionComponents.Get(value.Componente);
            if (value.Base is not ("BN" or "MS")) throw new ArgumentException("Base suportada: BN ou MS; porções exigem normalização explícita na fonte.");
            NutritionComponents.Normalize(value.Componente, 0m, value.Unidade);
            if (value.Qualificador is not ("Pontual" or "Minimo" or "Maximo" or "Faixa" or "AbaixoDeteccao")) throw new ArgumentException("Qualificador inválido.");
            if (value.Estado == "Conhecido")
            {
                var number = NutritionComponents.Normalize(value.Componente, CatalogRules.Decimal(value.Valor), value.Unidade);
                if (definition.Grandeza == "massa" && number > 1000m) throw new ArgumentException("Composição em massa não pode exceder 1000 g/kg.");
                if (value.Origem is not ("Medido" or "Declarado" or "Estimado")) throw new ArgumentException("Valor conhecido exige origem explícita.");
                if (value.Origem == "Estimado") CatalogRules.Text(value.Hipotese, 2000, "Hipótese/equação/fontes da estimativa");
                if (publication) { CatalogRules.Text(value.Fonte ?? Fonte, 1000, "Fonte do valor"); CatalogRules.Text(value.Metodo, 500, "Método do valor"); }
            }
            else if (value.Estado is "Desconhecido" or "NaoAplicavel")
            {
                if (value.Valor is not null) throw new ArgumentException("Desconhecido/não aplicável exige valor nulo, nunca zero.");
                CatalogRules.Text(value.Motivo, 2000, "Motivo da lacuna");
            }
            else throw new ArgumentException("Estado do valor inválido.");
            if (definition.Grandeza == "energia" && publication) CatalogRules.Text(value.Contexto, 500, "Contexto energético");
            if (value.Componente is "UMIDADE" or "MS" && value.Base != "BN") throw new ArgumentException("Umidade/MS são frações complementares na BN.");
        }
        if (Valores.Select(x => x.Componente).Distinct().Count() != Valores.Count) throw new ArgumentException("Selecione um único valor por componente no perfil; fontes alternativas exigem outro perfil.");
        var moisture = Fraction("UMIDADE"); var dry = Fraction("MS");
        if (moisture.HasValue && dry.HasValue && Math.Abs(moisture.Value + dry.Value - 1m) > 0.000001m)
            throw new ArgumentException("Umidade e matéria seca da amostra divergem; corrija a fonte.");
    }
    public decimal? Fraction(string code)
    {
        var v = Valores.SingleOrDefault(x => x.Componente == code && x.Estado == "Conhecido" && x.Qualificador == "Pontual");
        return v is null ? null : NutritionComponents.Normalize(code, decimal.Parse(v.Valor!, System.Globalization.CultureInfo.InvariantCulture), v.Unidade) / 1000m;
    }
    public decimal? DryFraction() => Fraction("MS") ?? (Fraction("UMIDADE") is { } u ? 1m - u : null);
}

public sealed class NutritionProfile
{
    private NutritionProfile() { }
    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public int Numero { get; private set; }
    public int Revisao { get; private set; }
    public string Estado { get; private set; } = "Rascunho";
    public string ConteudoJson { get; private set; } = "";
    public Guid AutorId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public ProfileData Data() => FormulationJson.Read<ProfileData>(ConteudoJson);
    public static NutritionProfile Create(Guid item, int number, ProfileData data, Guid author, DateTimeOffset now)
    {
        var profile = new NutritionProfile { Id = Guid.NewGuid(), ItemId = item, Numero = number, AutorId = author, CreatedAtUtc = now };
        profile.Update(data, 0); return profile;
    }
    public void Update(ProfileData data, int expected)
    {
        CatalogRules.Expected(Revisao, expected); Editable(); data.Validate(false);
        ConteudoJson = FormulationJson.Write(data); Revisao++;
    }
    public void Publish(int expected, DateTimeOffset now)
    { CatalogRules.Expected(Revisao, expected); Editable(); Data().Validate(true); Estado = "Publicado"; PublishedAtUtc = now; Revisao++; }
    public void Inactivate(int expected)
    { CatalogRules.Expected(Revisao, expected); if (Estado != "Publicado") throw new CatalogConflictException("estado_invalido", "Somente publicado pode ser inativado."); Estado = "Inativo"; Revisao++; }
    private void Editable()
    { if (Estado != "Rascunho") throw new CatalogConflictException("versao_imutavel", "Versão publicada é imutável. Crie nova revisão."); }
}
