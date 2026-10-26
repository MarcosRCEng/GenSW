using GenSW.Application.Inventory;
using GenSW.Application.Production;
using GenSW.Domain.Inventory;
using GenSW.Domain.Production;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GenSW.Infrastructure.Production;

public sealed class ProductionRepository(GenSWDbContext db,IInventoryRepository inventory) : IProductionRepository
{
    private bool Postgres => db.Database.ProviderName=="Npgsql.EntityFrameworkCore.PostgreSQL";
    public async Task<IInventoryScope> BeginMutationAsync(CancellationToken ct)
    {
        var scope=await inventory.BeginMutationAsync(ct);
        try { if(Postgres) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(430,2)",ct); return scope; }
        catch { await scope.DisposeAsync(); throw; }
    }
    public void Add<T>(T value) where T:class => db.Add(value);
    public Task SaveAsync(CancellationToken ct)=>inventory.SaveAsync(ct);
    public Task<OrdemProducao?> OrderAsync(Guid id,bool tracking,CancellationToken ct) =>
        (tracking&&Postgres?db.Set<OrdemProducao>().FromSqlInterpolated($"SELECT * FROM \"OrdensProducao\" WHERE \"Id\"={id} FOR UPDATE"):tracking?db.Set<OrdemProducao>():db.Set<OrdemProducao>().AsNoTracking()).SingleOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<ProductionPage<OrdemProducao>> OrdersAsync(ProductionQuery q,CancellationToken ct)
    {
        q.Validate(); var rows=db.Set<OrdemProducao>().AsNoTracking().AsQueryable();
        if(q.Search is {} term) { var literal="%"+term.Trim().ToUpperInvariant().Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%"; rows=rows.Where(x=>EF.Functions.Like(x.CodigoNormalizado,literal,"\\")); }
        if(q.Estado is {} state) rows=rows.Where(x=>x.Estado==state);
        if(q.ReceitaVersaoId is {} recipe) rows=rows.Where(x=>x.ReceitaVersaoId==recipe);
        if(q.ResponsavelId is {} responsible) rows=rows.Where(x=>x.ResponsavelId==responsible);
        if(q.PropriedadeId is {} property) rows=rows.Where(x=>x.PropriedadeId==property);
        // Resolve the calendar through the same time zone used by inventory, including historical offsets.
        var zone=TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        if(q.DataDe is {} from) { var instant=new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue),zone)); rows=rows.Where(x=>x.CreatedAtUtc>=instant); }
        if(q.DataAte is {} until) { var instant=new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(until.AddDays(1).ToDateTime(TimeOnly.MinValue),zone)); rows=rows.Where(x=>x.CreatedAtUtc<instant); }
        rows=(q.SortBy,q.SortDirection) switch { ("createdAtUtc","desc")=>rows.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id),("createdAtUtc",_)=>rows.OrderBy(x=>x.CreatedAtUtc).ThenBy(x=>x.Id),(_,"desc")=>rows.OrderByDescending(x=>x.CodigoNormalizado).ThenBy(x=>x.Id),_=>rows.OrderBy(x=>x.CodigoNormalizado).ThenBy(x=>x.Id)};
        return await Page(rows,q.Page,q.PageSize,ct);
    }
    public Task<ComandoProducao?> ReplayAsync(Guid order,string operation,string key,CancellationToken ct)=>db.Set<ComandoProducao>().AsNoTracking().SingleOrDefaultAsync(x=>x.OrdemId==order&&x.Operacao==operation&&x.Chave==key,ct);
    public async Task<IReadOnlyList<ReservaProducao>> ReservationsAsync(Guid order,bool tracking,CancellationToken ct)=>await (tracking?db.Set<ReservaProducao>():db.Set<ReservaProducao>().AsNoTracking()).Where(x=>x.OrdemId==order).OrderBy(x=>x.Id).ToArrayAsync(ct);
    public Task<decimal> ReservedAsync(Guid lot,Guid local,Guid? excludingOrder,CancellationToken ct)=>db.Set<ReservaProducao>().Where(x=>x.LoteId==lot&&x.LocalId==local&&(!excludingOrder.HasValue||x.OrdemId!=excludingOrder)).SumAsync(x=>x.QuantidadeAtiva,ct);
    public Task<ExecucaoProducao?> ExecutionAsync(Guid order,CancellationToken ct)=>db.Set<ExecucaoProducao>().AsNoTracking().SingleOrDefaultAsync(x=>x.OrdemId==order,ct);
    public async Task<IReadOnlyList<ExecucaoMovimento>> MovementsAsync(Guid execution,CancellationToken ct)=>await db.Set<ExecucaoMovimento>().AsNoTracking().Where(x=>x.ExecucaoId==execution).OrderBy(x=>x.Id).ToArrayAsync(ct);
    public async Task<IReadOnlyList<SaidaProducao>> OutputsAsync(Guid execution,CancellationToken ct)=>await db.Set<SaidaProducao>().AsNoTracking().Where(x=>x.ExecucaoId==execution).OrderBy(x=>x.Id).ToArrayAsync(ct);
    public async Task<IReadOnlyList<MovimentoEstoque>> StockMovementsAsync(Guid execution,CancellationToken ct)=>await (from link in db.Set<ExecucaoMovimento>().AsNoTracking() join movement in db.MovimentosEstoque.AsNoTracking() on link.MovimentoEstoqueId equals movement.Id where link.ExecucaoId==execution&&link.Papel!="Compensacao" orderby movement.EventoId,movement.Ordinal select movement).ToArrayAsync(ct);
    public async Task<bool> OutputWasUsedAsync(SaidaProducao output,CancellationToken ct)
    {
        if(await db.Set<ReservaProducao>().AnyAsync(x=>x.LoteId==output.LoteId,ct)) return true;
        if(await db.MovimentosEstoque.AnyAsync(x=>x.LoteId==output.LoteId&&x.EventoId!=output.EventoId,ct)) return true;
        var lot=await db.LotesMateriais.AsNoTracking().SingleAsync(x=>x.Id==output.LoteId,ct);
        return lot.Revisao!=output.RevisaoLoteOriginal||lot.Situacao!=output.SituacaoOriginal||lot.Validade!=output.ValidadeOriginal||
            await db.PosicoesEstoque.AnyAsync(x=>x.LoteId==output.LoteId&&(x.LocalId!=output.LocalId&&x.Quantidade!=0||x.LocalId==output.LocalId&&x.Quantidade!=output.Quantidade),ct);
    }
    public Task<bool> HasDeclaredConsumptionAsync(Guid order,CancellationToken ct)=>db.Set<OcorrenciaProducao>().AnyAsync(x=>x.OrdemId==order&&x.HouveConsumoFisico,ct);
    public Task<ProductionPage<HistoricoProducao>> HistoryAsync(Guid order,int page,int size,CancellationToken ct)=>Page(db.Set<HistoricoProducao>().AsNoTracking().Where(x=>x.OrdemId==order).OrderBy(x=>x.CreatedAtUtc).ThenBy(x=>x.Id),page,size,ct);
    public Task<ProductionPage<OcorrenciaProducao>> OccurrencesAsync(Guid order,int page,int size,CancellationToken ct)=>Page(db.Set<OcorrenciaProducao>().AsNoTracking().Where(x=>x.OrdemId==order).OrderBy(x=>x.CreatedAtUtc).ThenBy(x=>x.Id),page,size,ct);
    private static async Task<ProductionPage<T>> Page<T>(IQueryable<T> rows,int page,int size,CancellationToken ct)
    { if(page<1||size is <1 or >100||(long)(page-1)*size>int.MaxValue) throw new ArgumentException("Paginação inválida."); var count=await rows.CountAsync(ct); return new(await rows.Skip((page-1)*size).Take(size).ToArrayAsync(ct),page,size,count,(int)Math.Ceiling(count/(double)size)); }
    public async Task<ProductionPage<GenealogyEdge>> GenealogyAsync(Guid lot,GenealogyQuery q,CancellationToken ct)
    {
        q=q with{Direcao=q.Direcao switch{"ancestrais" or "origem"=>"origens","descendentes" or "destino"=>"destinos",_=>q.Direcao}};
        if(q.Direcao is not ("ambas" or "origens" or "destinos")||q.Profundidade is <1 or >10||q.Page<1||q.PageSize is <1 or >100||(long)(q.Page-1)*q.PageSize>int.MaxValue) throw new ArgumentException("Direção/profundidade/paginação inválidas.");
        var visited=new HashSet<Guid>{lot}; var frontier=new HashSet<Guid>{lot}; var edges=new Dictionary<Guid,GenealogyEdge>();
        for(int depth=0;depth<q.Profundidade&&frontier.Count>0;depth++)
        {
            var ids=frontier.ToArray();
            var batch=await (from g in db.Set<GenealogiaMaterial>().AsNoTracking() join e in db.Set<ExecucaoProducao>() on g.ExecucaoId equals e.Id where (q.Direcao!="destinos"&&ids.Contains(g.LoteSaidaId))||(q.Direcao!="origens"&&ids.Contains(g.LoteEntradaId)) orderby g.Id select new {g.Id,Edge=new GenealogyEdge(e.Id,e.OrdemId,g.LoteEntradaId,g.LoteSaidaId)}).Take(5001).ToArrayAsync(ct);
            if(batch.Length>5000||edges.Count+batch.Length>10000) throw new ProductionException(409,"genealogia_limite","Reduza a profundidade da consulta de genealogia.");
            frontier=[]; foreach(var row in batch) { edges[row.Id]=row.Edge; if(q.Direcao!="destinos"&&visited.Add(row.Edge.LoteEntradaId)) frontier.Add(row.Edge.LoteEntradaId); if(q.Direcao!="origens"&&visited.Add(row.Edge.LoteSaidaId)) frontier.Add(row.Edge.LoteSaidaId); }
        }
        var values=edges.OrderBy(x=>x.Key).Select(x=>x.Value).ToArray(); return new(values.Skip((q.Page-1)*q.PageSize).Take(q.PageSize).ToArray(),q.Page,q.PageSize,values.Length,(int)Math.Ceiling(values.Length/(double)q.PageSize));
    }
    public Task<string?> PropertyLabelAsync(Guid id,CancellationToken ct)=>db.Propriedades.Where(x=>x.Id==id).Select(x=>(string?)x.Nome).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<Guid>> AdjustmentEventsAsync(Guid execution,CancellationToken ct)
    {
        if(Postgres)return await db.EventosEstoque.FromSqlInterpolated($"SELECT * FROM \"EventosEstoque\" WHERE \"Tipo\"='Ajuste' AND \"SnapshotJson\" #>> '{{pedido,execucaoReferenciaId}}'={execution.ToString()}").AsNoTracking().OrderBy(x=>x.Sequencia).Select(x=>x.Id).ToArrayAsync(ct);
        return (await db.EventosEstoque.AsNoTracking().Where(x=>x.Tipo=="Ajuste").ToArrayAsync(ct)).Where(x=>x.SnapshotJson.Contains(execution.ToString(),StringComparison.Ordinal)).Select(x=>x.Id).ToArray();
    }
    private async Task<IReadOnlyList<string>> PositionErrors(Guid lot,Guid local,CancellationToken ct)
    {
        var errors=new List<string>();var position=await db.PosicoesEstoque.AsNoTracking().SingleOrDefaultAsync(x=>x.LoteId==lot&&x.LocalId==local,ct);
        var ledger=await db.MovimentosEstoque.Where(x=>x.LoteId==lot&&x.LocalId==local).SumAsync(x=>x.Sentido=="Entrada"?x.Quantidade:-x.Quantidade,ct);
        var reserves=await db.Set<ReservaProducao>().AsNoTracking().Where(x=>x.LoteId==lot&&x.LocalId==local).ToArrayAsync(ct);
        if((position?.Quantidade??0)!=ledger)errors.Add("Livro e projeção física divergentes.");
        if(reserves.Sum(x=>x.QuantidadeAtiva)>(position?.Quantidade??0))errors.Add("Reservado excede físico.");
        if(reserves.Any(x=>x.QuantidadeOriginal!=x.QuantidadeAtiva+x.QuantidadeConsumida+x.QuantidadeLiberada||x.QuantidadeAtiva<0||x.QuantidadeConsumida<0||x.QuantidadeLiberada<0))errors.Add("Conservação das reservas divergente.");
        var activeOrders=reserves.Where(x=>x.QuantidadeAtiva>0).Select(x=>x.OrdemId).Distinct().ToArray();if(await db.Set<OrdemProducao>().AnyAsync(x=>activeOrders.Contains(x.Id)&&x.Estado!="Iniciada",ct))errors.Add("Reserva ativa fora de ordem iniciada.");
        return errors;
    }
    private async Task<IReadOnlyList<string>> OrderErrors(OrdemProducao order,CancellationToken ct)
    {
        var errors=new List<string>();var reservations=await ReservationsAsync(order.Id,false,ct);var execution=await ExecutionAsync(order.Id,ct);var positions=reservations.Select(x=>(x.LoteId,x.LocalId)).ToHashSet();
        if(reservations.Any(x=>x.QuantidadeOriginal!=x.QuantidadeAtiva+x.QuantidadeConsumida+x.QuantidadeLiberada))errors.Add("Conservação das reservas divergente.");
        if(order.Estado!="Iniciada"&&reservations.Any(x=>x.QuantidadeAtiva>0))errors.Add("Reserva ativa fora de ordem iniciada.");
        if((order.Estado is "Confirmada" or "Revertida")!=(execution is not null))errors.Add("Estado e confirmação divergentes.");
        if(execution is not null)
        {
            var links=await MovementsAsync(execution.Id,ct);var originals=await StockMovementsAsync(execution.Id,ct);var outputs=await OutputsAsync(execution.Id,ct);
            if(Postgres)
            {
                var physicalIds=await db.MovimentosEstoque.FromSqlInterpolated($"SELECT * FROM \"MovimentosEstoque\" WHERE \"SnapshotJson\" #>> '{{producao,execucaoId}}'={execution.Id.ToString()}").AsNoTracking().Select(x=>x.Id).ToArrayAsync(ct);
                if(!physicalIds.ToHashSet().SetEquals(links.Select(x=>x.MovimentoEstoqueId)))errors.Add("Movimento físico sem ligação correspondente à execução.");
            }
            if(!originals.Any(x=>x.TipoEvento=="ProducaoConsumo")||originals.Count!=links.Count(x=>x.Papel!="Compensacao"))errors.Add("Conjunto de movimentos da execução divergente.");
            foreach(var allocation in reservations.GroupBy(x=>(x.LinhaId,x.LoteId,x.LocalId)))
            {
                var ids=links.Where(x=>x.LinhaId==allocation.Key.LinhaId&&x.Papel=="Entrada").Select(x=>x.MovimentoEstoqueId).ToArray();
                if(allocation.Sum(x=>x.QuantidadeConsumida)!=originals.Where(x=>ids.Contains(x.Id)&&x.LoteId==allocation.Key.LoteId&&x.LocalId==allocation.Key.LocalId).Sum(x=>x.Quantidade))errors.Add("Reservas consumidas e débitos físicos divergentes.");
            }
            if(originals.Count(x=>x.TipoEvento=="ProducaoSaida")!=outputs.Count)errors.Add("Lotes de saída e créditos divergentes.");
            foreach(var output in outputs)
            {
                var credit=originals.SingleOrDefault(x=>x.EventoId==output.EventoId);if(credit is null||credit.LoteId!=output.LoteId||credit.LocalId!=output.LocalId||credit.Quantidade!=output.Quantidade||credit.TipoEvento!="ProducaoSaida")errors.Add("Vínculo de saída não corresponde ao crédito original.");
                positions.Add((output.LoteId,output.LocalId));foreach(var local in await db.PosicoesEstoque.Where(x=>x.LoteId==output.LoteId).Select(x=>x.LocalId).ToArrayAsync(ct))positions.Add((output.LoteId,local));
            }
            var sourceLots=originals.Where(x=>x.TipoEvento=="ProducaoConsumo").Select(x=>x.LoteId).Distinct().ToArray();var expected=sourceLots.SelectMany(input=>outputs.Select(output=>(input,output.LoteId))).ToHashSet();
            var actual=(await db.Set<GenealogiaMaterial>().AsNoTracking().Where(x=>x.ExecucaoId==execution.Id).ToArrayAsync(ct)).Select(x=>(x.LoteEntradaId,x.LoteSaidaId)).ToArray();if(actual.Length!=expected.Count||!expected.SetEquals(actual))errors.Add("Genealogia não corresponde às entradas/saídas físicas.");
            var reversals=await db.Set<ReversaoProducao>().CountAsync(x=>x.ExecucaoId==execution.Id,ct);if(reversals!=(order.Estado=="Revertida"?1:0))errors.Add("Estado e reversão divergentes.");
            if(order.Estado=="Revertida"&&links.Count(x=>x.Papel=="Compensacao")!=originals.Count)errors.Add("Compensação incompleta.");
            if(order.Estado!="Revertida"&&links.Any(x=>x.Papel=="Compensacao"))errors.Add("Compensação fora de reversão.");
        }
        foreach(var pair in positions)errors.AddRange(await PositionErrors(pair.LoteId,pair.LocalId,ct));
        return errors.Distinct().ToArray();
    }
    public async Task EnsureOrderConsistentAsync(Guid order,CancellationToken ct)
    {var value=await OrderAsync(order,false,ct);if(value is not null&&(await OrderErrors(value,ct)).Count>0)throw new ProductionException(500,"invariante_producao","Divergência persistida na ordem. Preserve evidências e solicite reconciliação antes de operar.");}
    public async Task EnsurePositionConsistentAsync(Guid lot,Guid local,CancellationToken ct)
    {if((await PositionErrors(lot,local,ct)).Count>0)throw new ProductionException(500,"invariante_producao","Divergência persistida na posição. Preserve evidências e solicite reconciliação antes de operar.");}
    public async Task<ProductionPage<ProductionReconciliation>> ReconcileAsync(ProductionQuery query,CancellationToken ct)
    {
        var page=await OrdersAsync(query,ct);var result=new List<ProductionReconciliation>();
        foreach(var order in page.Items)
        {
            var errors=await OrderErrors(order,ct);result.Add(new(order.Id,order.Codigo,order.Estado,errors.Count==0,errors));
        }
        return new(result,page.Page,page.PageSize,page.TotalItems,page.TotalPages);
    }
}
