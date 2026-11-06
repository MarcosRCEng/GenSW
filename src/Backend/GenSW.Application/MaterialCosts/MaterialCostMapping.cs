using System.Globalization;
using System.Numerics;
using GenSW.Domain.Catalog;
using GenSW.Domain.Inventory;
using GenSW.Domain.MaterialCosts;

namespace GenSW.Application.MaterialCosts;

public static class MaterialCostMapping
{
    public static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    public static string Number(decimal value) => CatalogRules.Format(value);
    public static string Sequence(long value) => value.ToString(CultureInfo.InvariantCulture);
    public static IReadOnlyList<CostComponent> Components(string json) => InventoryJson.Read<CostComponent[]>(json);
    public static IReadOnlyList<string> Gaps(string json) => InventoryJson.Read<string[]>(json);
    public static MaterialComponentView Component(CostComponent value) => new(value.Id,value.CamadaId,value.ItemId,value.Unidade,
        Number(value.Quantidade),value.ValorConhecido is {} known?Money(known):null,value.Lacunas,value.EvidenciaId,value.FracaoNumerador,value.FracaoDenominador);
    public static decimal Known(IReadOnlyList<CostComponent> values) => MaterialCostRules.FromCents(values.Aggregate(BigInteger.Zero,(sum,x)=>sum+(x.ValorConhecido is {} value?MaterialCostRules.Cents(value):0)));
    public static MaterialCostSummary Summary(IReadOnlyList<CostComponent> components,decimal quantity,long sequence,int revision,IReadOnlyList<string>? additionalGaps=null)
    {
        var gaps=components.SelectMany(x=>x.Lacunas).Concat(additionalGaps??[]).Distinct(StringComparer.Ordinal).ToArray();
        var state=MaterialCostRules.Coverage(components); if(gaps.Length>0&&state=="Completo")state="Parcial";
        var known=Known(components);
        var unit=state=="Completo"&&quantity>0?MaterialCostRules.UnitCost(known,quantity):null;
        return new(state,Money(known),state=="Completo"?Money(known):null,unit is null?null:unit.Valor.ToString("0.000000",CultureInfo.InvariantCulture),
            components.Select(Component).ToArray(),gaps,Sequence(sequence),revision);
    }
    public static IReadOnlyList<CostComponent> Merge(IEnumerable<CostComponent> values)
    {
        return values.GroupBy(x=>x.Id).Select(group=>
        {
            var first=group.First();var numerator=BigInteger.Zero;var denominator=BigInteger.One;
            foreach(var component in group)
            {
                if(component.CamadaId!=first.CamadaId||component.ItemId!=first.ItemId||component.Unidade!=first.Unidade||component.Quantidade!=first.Quantidade||component.EvidenciaId!=first.EvidenciaId)
                    throw new MaterialCostException(409,"proveniencia_divergente","Identidade do componente monetário diverge.");
                var n=BigInteger.Parse(component.FracaoNumerador,CultureInfo.InvariantCulture);var d=BigInteger.Parse(component.FracaoDenominador,CultureInfo.InvariantCulture);
                numerator=numerator*d+n*denominator;denominator*=d;var gcd=BigInteger.GreatestCommonDivisor(numerator,denominator);numerator/=gcd;denominator/=gcd;
            }
            if(numerator>denominator)throw new MaterialCostException(409,"cobertura_sobreposta","Componentes sobrepõem a cobertura original.");
            return first with{ValorConhecido=group.All(x=>x.ValorConhecido is null)?null:Known(group.ToArray()),
                Lacunas=group.SelectMany(x=>x.Lacunas).Distinct(StringComparer.Ordinal).ToArray(),FracaoNumerador=numerator.ToString(CultureInfo.InvariantCulture),FracaoDenominador=denominator.ToString(CultureInfo.InvariantCulture)};
        }).OrderBy(x=>x.Id.ToString("D"),StringComparer.Ordinal).ToArray();
    }
    public static (IReadOnlyList<CostComponent> Taken,IReadOnlyList<CostComponent> Remaining) Split(IReadOnlyList<CostComponent> components,decimal quantity,decimal take)
    {
        var q=MaterialCostRules.ScaledQuantity(quantity,"kg");var t=MaterialCostRules.ScaledQuantity(take,"kg");
        if(q<=0||t<0||t>q)throw new ArgumentException("Quantidade monetária insuficiente.");
        var taken=new List<CostComponent>();var remaining=new List<CostComponent>();
        foreach(var component in components)
        {
            var split=component.ValorConhecido is {} value?MaterialCostRules.Split(quantity,value,take):null;
            if(t>0)taken.Add(MaterialCostRules.Fraction(component,t,q,split?.ValorRetirado));
            if(t<q)remaining.Add(MaterialCostRules.Fraction(component,q-t,q,split?.ValorRestante));
        }
        return(taken,remaining);
    }
    public static MaterialDestinationView Destination(AllocatedCostDestination value,decimal quantity,string unit,string reason,long sequence,Guid? output=null)
    {
        var summary = Summary(value.Componentes, value.Tipo == "Saida" ? quantity : 0, sequence, 0, value.Lacunas) with
        {
            Estado = value.Cobertura,
            ValorTotal = value.ValorTotal is { } total ? Money(total) : null,
            CustoUnitarioCompleto = value.UnitarioCompleto?.Valor.ToString("0.000000", CultureInfo.InvariantCulture),
        };
        return new(Guid.Empty,value.Id,value.Tipo,output,value.Tipo=="Perda"?value.Id:null,Number(quantity),unit,Number(value.Percentual),summary,
            value.UnitarioConhecido?.Valor.ToString("0.000000",CultureInfo.InvariantCulture),value.UnitarioConhecido?.Arredondado??false,
            value.UnitarioConhecido?.ResiduoNumerador,value.UnitarioConhecido?.ResiduoDenominador,reason);
    }
}
