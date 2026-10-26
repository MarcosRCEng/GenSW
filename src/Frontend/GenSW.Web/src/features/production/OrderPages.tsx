import { actionNames } from './productionActions'
import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../auth/hooks/useAuth'
import { httpRequest } from '../../shared/http/httpClient'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { listReturnState, recordEditState, savedRecordState, useRememberListState, useRestoredListState } from '../../shared/details/listNavigation'
import { useUnsavedChanges } from '../../shared/navigation/useUnsavedChanges'
import { buttonClass, Input, Notice, Pagination, Panel, primaryClass, ReferenceSelect, ScrollTable, Select } from '../inventory/InventoryControls'
import { decimalInput, inventoryError, parseProperty, parseResponsible, showQuantity, showTime } from '../inventory/inventoryService'
import type { RecipeVersion } from '../formulation/types'
import { itemName, parseHistory, parseOrder, parseRecipe, parseReconciliation, parseVersion, productionList, readOrder, record } from './productionService'
import { PendingCommand, ProductionFrame, ReferenceLabel } from './ProductionControls'
import { useProductionCommand } from './useProductionCommand'
import type { OrderCommand, PlanSnapshot, ProductionHistory, ProductionOrder, ProductionPage, Reconciliation } from './types'

const states = ['Rascunho', 'Planejada', 'Iniciada', 'Confirmada', 'Cancelada', 'AbortadaSemConsumo', 'Revertida']
const stateName = (state: string) => state === 'AbortadaSemConsumo' ? 'Abortada sem consumo' : state
const root = '/producao/ordens'
const grid = 'grid min-w-0 gap-4 sm:grid-cols-2'
const blankCommand = (id: string, responsible: string): OrderCommand => ({ id, codigo: '', receitaVersaoId: '', tamanho: '', unidade: 'kg', responsavelId: responsible, propriedadeId: null, versaoEsperada: 0, motivo: 'Cadastro de ordem' })

export function OrdersPage() {
  const { user } = useAuth()
  const initial = useRestoredListState<Record<string, string | number>>(root, { page: 1, pageSize: 25, search: '', estado: '', sortBy: 'codigo', sortDirection: 'asc' })
  const [filters, setFilters] = useState(initial)
  const navigation = useRememberListState(root, filters)
  const [data, setData] = useState<ProductionPage<ProductionOrder> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  const encoded = JSON.stringify(filters)
  useEffect(() => {
    let live = true; setData(null); setError(null)
    const query = JSON.parse(encoded)
    delete query.receitaId
    void productionList('', query, parseOrder).then(result => {
      if (!live) return
      if (result.totalPages && result.page > result.totalPages) setFilters(v => ({ ...v, page: result.totalPages }))
      else setData(result)
    }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [encoded, retry])
  const set = (key: string, value: string) => setFilters(v => ({ ...v, [key]: value, page: 1 }))
  return <ProductionFrame title="Ordens de produção"><Panel><div className="flex flex-wrap gap-3"><Link className={primaryClass} to={`${root}/nova`} state={navigation}>Nova ordem</Link>{user?.roles.includes('Admin') && <Link className={buttonClass} to={`${root}/reconciliacao`}>Reconciliação de produção</Link>}</div></Panel><Panel title="Filtros"><div className={`${grid} lg:grid-cols-3`}><Input label="Buscar código da ordem" value={String(filters.search ?? '')} onChange={v => set('search', v)} maxLength={200} /><Select label="Estado" value={String(filters.estado ?? '')} onChange={v => set('estado', v)} options={[[ '', 'Todos'], ...states.map(s => [s, stateName(s)] as const)]} /><ReferenceSelect label="Receita para filtrar" path="/receitas" value={String(filters.receitaId ?? '') || null} parse={parseRecipe} caption={r => `${r.codigo} — ${r.nome}`} onChange={v => setFilters(f => ({ ...f, receitaId: v ?? '', receitaVersaoId: '', page: 1 }))} />{filters.receitaId && <ReferenceSelect label="Versão da receita para filtrar" path={`/receitas/${filters.receitaId}/versoes`} detailPath={id => `/receitas/versoes/${id}`} value={String(filters.receitaVersaoId ?? '') || null} parse={parseVersion} caption={v => `Versão ${v.numero} · ${v.estado}`} onChange={v => set('receitaVersaoId', v ?? '')} />}{filters.receitaId && !filters.receitaVersaoId && <p>Selecione uma versão para aplicar o filtro de receita.</p>}<ReferenceSelect label="Responsável" path="/estoque/responsaveis" value={String(filters.responsavelId ?? '') || null} onChange={v => set('responsavelId', v ?? '')} parse={parseResponsible} caption={r => `${r.nome}${r.ativo ? '' : ' · Inativo'}`} /><ReferenceSelect label="Propriedade" path="/propriedades" value={String(filters.propriedadeId ?? '') || null} onChange={v => set('propriedadeId', v ?? '')} parse={parseProperty} caption={r => `${r.nome}${r.ativo ? '' : ' · Inativa'}`} /><Input label="Data operacional inicial" type="date" value={String(filters.dataDe ?? '')} onChange={v => set('dataDe', v)} /><Input label="Data operacional final" type="date" value={String(filters.dataAte ?? '')} onChange={v => set('dataAte', v)} /><Select label="Ordenar por" value={String(filters.sortBy)} onChange={v => set('sortBy', v)} options={[[ 'codigo', 'Código'], ['createdAtUtc', 'Cadastro']]} /><Select label="Direção" value={String(filters.sortDirection)} onChange={v => set('sortDirection', v)} options={[[ 'asc', 'Crescente'], ['desc', 'Decrescente']]} /><Select label="Registros por página" value={String(filters.pageSize)} onChange={v => set('pageSize', v)} options={[[ '10', '10'], ['25', '25'], ['50', '50'], ['100', '100']]} /></div></Panel><Notice error={error} retry={() => setRetry(v => v + 1)} />{!data && !error && <p role="status">Carregando ordens…</p>}{data && <Panel><p>{data.totalItems} ordens</p>{!data.items.length ? <p>Nenhuma ordem encontrada.</p> : <ScrollTable label="Ordens de produção"><thead><tr>{['Código', 'Estado', 'Planejamento', 'Responsável', 'Ações'].map(h => <th scope="col" key={h}>{h}</th>)}</tr></thead><tbody>{data.items.map(order => <tr key={order.id}><td>{order.codigo}</td><td>{stateName(order.estado)}</td><td>{order.planejamento ? `${order.planejamento.cabecalho.codigo} — ${order.planejamento.cabecalho.nome} · versão ${order.planejamento.receita.numero}` : 'Rascunho sem snapshot'}<br />{showQuantity(order.tamanho)} {order.unidade}</td><td><ReferenceLabel kind="responsavel" id={order.responsavelId} /></td><td><Link className="text-emerald-700 underline" to={`${root}/${order.id}`} state={navigation}>Visualizar</Link></td></tr>)}</tbody></ScrollTable>}<Pagination page={Number(filters.page)} totalPages={data.totalPages} onChange={page => setFilters(v => ({ ...v, page }))} /></Panel>}</ProductionFrame>
}

export function OrderFormPage() {
  const { id } = useParams()
  return <OrderForm key={id ?? 'nova'} id={id} />
}
function OrderForm({ id }: { id?: string }) {
  const { user } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const command = useProductionCommand(id ?? 'nova')
  const [form, setForm] = useState<OrderCommand>(() => command.pending ? command.pending.body as OrderCommand : blankCommand(id ?? crypto.randomUUID(), user?.userId ?? ''))
  const [recipeId, setRecipeId] = useState('')
  const [version, setVersion] = useState<RecipeVersion | null>(null)
  const [ready, setReady] = useState(!id)
  const [error, setError] = useState<string | null>(null)
  const [dirty, setDirty] = useState(false)
  const [retry, setRetry] = useState(0)
  const { markSaved, confirmDiscard } = useUnsavedChanges(dirty || !!command.pending)
  const back = listReturnState(location.state, root)
  useEffect(() => {
    let live = true
    if (!id) return
    setReady(false)
    void readOrder(id).then(order => {
      if (!live) return
      if (!order.acoesPermitidas.includes('edicao')) throw new Error('Esta ordem não permite editar o rascunho. Revise explicitamente o planejamento antes de alterá-lo.')
      if (!command.pending) setForm({ id: order.id, codigo: order.codigo, receitaVersaoId: order.receitaVersaoId, tamanho: order.tamanho, unidade: order.unidade, responsavelId: order.responsavelId, propriedadeId: order.propriedadeId, versaoEsperada: order.revisao, motivo: 'Revisão do rascunho' })
      setReady(true)
    }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  // Pending body must retain the exact original expected revision after reload.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, retry])
  useEffect(() => {
    let live = true; setVersion(null)
    if (!form.receitaVersaoId) return
    void httpRequest<unknown>(`/receitas/versoes/${form.receitaVersaoId}`, { authenticated: true }).then(parseVersion).then(v => { if (live) { setVersion(v); setRecipeId(v.receitaId) } }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [form.receitaVersaoId])
  const set = <K extends keyof OrderCommand>(key: K, value: OrderCommand[K]) => { setDirty(true); setForm(v => ({ ...v, [key]: value })) }
  const complete = (order: ProductionOrder) => { markSaved(); setDirty(false); navigate(`${root}/${order.id}`, { state: savedRecordState(back, `${root}/${order.id}`) }) }
  const save = async () => {
    setError(null)
    try {
      if (!form.codigo.trim() || !form.receitaVersaoId || !form.responsavelId) throw new Error('Informe o código, uma versão publicada e o responsável ativo.')
      if (version?.estado !== 'Publicado') throw new Error('Selecione uma versão publicada da receita.')
      const result = await command.submit(form.id, id ? 'edicao' : 'criacao', { ...form, codigo: form.codigo.trim(), tamanho: decimalInput(form.tamanho), motivo: form.motivo.trim() }, id ? 'PUT' : 'POST')
      if (result) complete(result)
    } catch (e) { setError(inventoryError(e)) }
  }
  const leave = () => confirmDiscard(() => navigate(id ? `${root}/${id}` : root, { state: back }))
  return <ProductionFrame title={id ? 'Editar ordem' : 'Nova ordem'}><Notice error={error} retry={!ready ? () => setRetry(v => v + 1) : undefined} /><PendingCommand control={command} complete={complete} setError={setError} />{!ready && !error && <p role="status">Carregando ordem…</p>}{ready && <Panel><form onSubmit={e => e.preventDefault()}><fieldset disabled={command.busy || !!command.pending} className="space-y-4"><div className={grid}><Input label="Código da ordem" value={form.codigo} onChange={v => set('codigo', v)} required maxLength={100} /><ReferenceSelect label="Receita" path="/receitas" value={recipeId || null} params={{ ativo: true }} parse={parseRecipe} caption={r => `${r.codigo} — ${r.nome}${r.ativo ? '' : ' · Inativa'}`} onChange={v => { setRecipeId(v ?? ''); set('receitaVersaoId', '') }} />{recipeId && <ReferenceSelect label="Versão publicada" path={`/receitas/${recipeId}/versoes`} detailPath={versionId => `/receitas/versoes/${versionId}`} value={form.receitaVersaoId || null} params={{ estado: 'Publicado' }} parse={parseVersion} caption={v => `Versão ${v.numero} · ${v.estado} · ${showQuantity(v.conteudo.tamanhoReferencia)} ${v.conteudo.unidadeReferencia}`} onChange={(v, selected) => { set('receitaVersaoId', v ?? ''); if (selected) set('unidade', selected.conteudo.unidadeReferencia) }} />}<Input label="Tamanho solicitado" value={form.tamanho} onChange={v => set('tamanho', v)} required hint="Quantidade decimal; mantenha a unidade de referência da versão escolhida." /><Select label="Unidade do tamanho" value={form.unidade} onChange={v => set('unidade', v)} options={[[ 'kg', 'kg'], ['g', 'g'], ['L', 'L'], ['mL', 'mL'], ['un', 'un']]} /><ReferenceSelect label="Responsável operacional" path="/estoque/responsaveis" value={form.responsavelId || null} params={{ ativo: true }} parse={parseResponsible} caption={r => `${r.nome}${r.ativo ? '' : ' · Inativo'}`} onChange={v => set('responsavelId', v ?? '')} /><ReferenceSelect label="Propriedade (contexto opcional)" path="/propriedades" value={form.propriedadeId} params={{ ativo: true }} parse={parseProperty} caption={r => `${r.nome}${r.ativo ? '' : ' · Inativa'}`} onChange={v => set('propriedadeId', v)} /></div><Input label="Motivo do cadastro ou revisão" value={form.motivo} onChange={v => set('motivo', v)} required multiline /><p>Salvar cria ou altera somente o rascunho. O snapshot da versão publicada será congelado na ação explícita Planejar. A propriedade indica contexto e não seleciona estoque.</p><button className={primaryClass} type="button" onClick={() => void save()}>Salvar rascunho</button></fieldset><button className={`${buttonClass} mt-4`} type="button" onClick={leave}>Cancelar edição</button></form></Panel>}</ProductionFrame>
}

export function PlanRead({ plan }: { plan: PlanSnapshot }) {
  return <Panel title="Planejamento congelado"><p>{plan.cabecalho.codigo} — {plan.cabecalho.nome} · versão publicada {plan.receita.numero} · revisão {plan.receita.revisao}</p><p>{plan.escalonada.tipo} · {showQuantity(plan.escalonada.tamanhoReferencia)} {plan.escalonada.unidadeReferencia} · cálculo {plan.algoritmo}</p>{plan.responsavel && <p>Responsável no planejamento: {plan.responsavel.nome}</p>}<ScrollTable label="Entradas planejadas"><thead><tr>{['Item', 'Papel', 'Quantidade planejada', 'Escala', 'Referências documentais'].map(h => <th scope="col" key={h}>{h}</th>)}</tr></thead><tbody>{plan.escalonada.entradas.map(line => <tr key={line.id}><td>{itemName(plan, line.itemId)}</td><td>{line.papel}</td><td>{showQuantity(line.quantidade)} {line.unidade}</td><td>{line.escala}</td><td>{line.subReceitaVersaoId ? 'Intermediário real em estoque; ancestrais não serão debitados.' : ''}{line.perfilId ? ` Perfil ${plan.perfis.find(p => p.id === line.perfilId)?.numero ?? 'histórico'}.` : ' Sem perfil.'}{line.conversaoId ? ` Conversão ${plan.conversoes.find(c => c.id === line.conversaoId)?.numero ?? 'histórica'}.` : ' Sem conversão entre grandezas.'}</td></tr>)}</tbody></ScrollTable><ScrollTable label="Saídas planejadas"><thead><tr><th scope="col">Item / saída</th><th scope="col">Quantidade planejada</th><th scope="col">Escala</th></tr></thead><tbody>{plan.escalonada.saidas.map(line => <tr key={line.id}><td>{itemName(plan, line.itemId)} · {line.nome}</td><td>{showQuantity(line.quantidade)} {line.unidade}</td><td>{line.escala}</td></tr>)}</tbody></ScrollTable>{plan.escalonada.perdas.length > 0 && <div><h3 className="font-semibold">Perdas previstas</h3><ul className="list-inside list-disc">{plan.escalonada.perdas.map((loss, i) => <li key={i}>{showQuantity(loss.quantidade)} {loss.unidade} · {loss.motivo}</li>)}</ul></div>}{plan.escalonada.etapas.length > 0 && <ol className="list-inside list-decimal">{plan.escalonada.etapas.map(step => <li key={step.ordem}>{step.descricao}</li>)}</ol>}<p className="text-sm text-slate-600">Plano preservado com identidades, versões e conversões históricas. Os reais e desvios são registrados separadamente.</p></Panel>
}

function History({ id }: { id: string }) {
  const [page, setPage] = useState(1)
  const [data, setData] = useState<ProductionPage<ProductionHistory> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true; setData(null); setError(null)
    void productionList(`/${id}/historico`, { page, pageSize: 25 }, parseHistory).then(v => { if (live) setData(v) }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [id, page, retry])
  return <Panel title="Histórico e revisões"><Notice error={error} retry={() => setRetry(v => v + 1)} />{!data && !error && <p role="status">Carregando histórico…</p>}{data && <>{!data.items.length && <p>Nenhum histórico registrado.</p>}{data.items.map(row => <details key={row.id} className="rounded-lg border p-3"><summary className="cursor-pointer font-semibold">{actionNames[row.operacao] ?? row.operacao} · {showTime(row.createdAtUtc)}</summary><p className="my-2">{row.motivo}</p><p>Autor: <ReferenceLabel kind="responsavel" id={row.autorId} /></p><HistoricalState value={row.antesJson} label="Antes" /><HistoricalState value={row.depoisJson} label="Depois" /></details>)}<Pagination page={page} totalPages={data.totalPages} onChange={setPage} /></>}</Panel>
}
function HistoricalState({ value, label }: { value: string; label: string }) {
  try {
    const decoded: unknown = JSON.parse(value)
    const data = record(typeof decoded === 'string' ? JSON.parse(decoded) : decoded)
    const estado = data.estado ?? data.Estado
    const revision = data.revisao ?? data.Revisao
    const snapshot = data.snapshotPlanejadoJson ?? data.SnapshotPlanejadoJson
    const plan = typeof snapshot === 'string' ? record(JSON.parse(snapshot)) as unknown as PlanSnapshot : null
    return <div className="mt-2"><p>{label}: {typeof estado === 'string' ? stateName(estado) : 'registro preservado'}{typeof revision === 'number' ? ` · revisão ${revision}` : ''}</p>{plan && <details className="mt-2"><summary className="cursor-pointer">Consultar snapshot histórico desta revisão</summary><PlanRead plan={plan} /></details>}</div>
  } catch { return null }
}

export function OrderDetailsPage() {
  const { id } = useParams()
  return <OrderDetails key={id} id={id} />
}
function OrderDetails({ id }: { id?: string }) {
  const location = useLocation()
  const navigate = useNavigate()
  const { user } = useAuth()
  const { record: order, state, retry } = useRecordDetails(id, readOrder)
  const command = useProductionCommand(id ?? '')
  const [error, setError] = useState<string | null>(null)
  const back = listReturnState(location.state, root)
  const complete = (result: ProductionOrder) => { navigate(`${root}/${result.id}`, { replace: true, state: savedRecordState(back, `${root}/${result.id}`) }); retry() }
  return <ProductionFrame title="Visualizar ordem de produção"><Link className={buttonClass} to={root} state={back}>Voltar para Ordens</Link><Notice error={error} /><PendingCommand control={command} complete={complete} setError={setError} />{state === 'loading' && <p role="status">Carregando ordem…</p>}{state !== 'ready' && state !== 'loading' && <Notice error={state === 'not-found' ? 'Ordem não encontrada.' : state === 'forbidden' ? 'Acesso não permitido.' : 'Não foi possível consultar a ordem.'} retry={retry} />}{order && <><Panel title={order.codigo}><dl className={`${grid} break-words`}><div><dt className="font-semibold">Estado</dt><dd>{stateName(order.estado)} · revisão {order.revisao}</dd></div><div><dt className="font-semibold">Tamanho solicitado</dt><dd>{showQuantity(order.tamanho)} {order.unidade}</dd></div><div><dt className="font-semibold">Responsável</dt><dd><ReferenceLabel kind="responsavel" id={order.responsavelId} /></dd></div><div><dt className="font-semibold">Propriedade</dt><dd>{order.propriedadeNome ?? 'Não vinculada'}</dd></div><div><dt className="font-semibold">Cadastro</dt><dd>{showTime(order.createdAtUtc)}</dd></div>{order.iniciadaEmUtc && <div><dt className="font-semibold">Início</dt><dd>{showTime(order.iniciadaEmUtc)}</dd></div>}</dl>{order.estado === 'Iniciada' && <p className="rounded-lg border border-amber-300 bg-amber-50 p-3">Durante a transformação, o físico contábil permanece registrado e o material fica reservado. A baixa física será registrada na confirmação integral. Reservas não expiram automaticamente.</p>}{order.impedimentos.length > 0 && <div><h3 className="font-semibold">Impedimentos</h3><ul className="list-inside list-disc">{order.impedimentos.map((reason, i) => <li key={i}>{reason}</li>)}</ul></div>}<nav aria-label="Ações da ordem" className="flex flex-wrap gap-3">{!command.pending && order.acoesPermitidas.filter(action => user?.roles.includes('Admin') || !['aborto-sem-consumo', 'confirmacao-ocorrencia', 'reversao'].includes(action)).map(action => action === 'edicao' ? <Link className={buttonClass} key={action} to={`${root}/${order.id}/editar`} state={recordEditState(location.state, root, `${root}/${order.id}`)}>{actionNames[action]}</Link> : <Link className={buttonClass} key={action} to={`${root}/${order.id}/acoes/${action}`} state={back}>{actionNames[action] ?? action}</Link>)}</nav></Panel>{order.planejamento && <PlanRead plan={order.planejamento} />}{order.reservas.length > 0 && <Panel title="Reservas da ordem"><ScrollTable label="Reservas e resolução"><thead><tr>{['Lote / local', 'Original', 'Ativa', 'Consumida', 'Liberada'].map(h => <th scope="col" key={h}>{h}</th>)}</tr></thead><tbody>{order.reservas.map(r => <tr key={r.id}><td><ReferenceLabel kind="lote" id={r.loteId} /><br /><ReferenceLabel kind="local" id={r.localId} /></td><td>{showQuantity(r.quantidadeOriginal)} {r.unidade}</td><td>{showQuantity(r.quantidadeAtiva)} {r.unidade}</td><td>{showQuantity(r.quantidadeConsumida)} {r.unidade}</td><td>{showQuantity(r.quantidadeLiberada)} {r.unidade}</td></tr>)}</tbody></ScrollTable></Panel>}{order.execucao && <Panel title="Execução confirmada"><p>Resultado: {order.execucao.resultado} · {showTime(order.execucao.createdAtUtc)}</p><p>Data operacional: {order.execucao.dataOperacional}{order.execucao.dataObservada ? ` · fato observado em ${order.execucao.dataObservada}` : ''}</p><ExecutionRead order={order} />{order.execucao.ajustesPresentes && order.execucao.ajustesPresentes.length > 0 && <div className="space-y-3"><h3 className="font-semibold">Ajustes presentes vinculados</h3><p>Correções de contagem atual; não são quantidade produzida nem reversão de ancestrais.</p>{order.execucao.ajustesPresentes.map(event => <div key={event.id} className="rounded-lg border p-3"><Link className="text-emerald-700 underline" to={`/estoque/movimentos/${event.id}`}>Ajuste · sequência {event.sequencia}</Link><p>{event.motivo}</p>{event.movimentos.map(movement => <p key={movement.id}>{movement.sentido === 'Entrada' ? 'Correção positiva' : 'Correção negativa'}: {showQuantity(movement.quantidade)} {movement.unidade}</p>)}</div>)}</div>}<h3 className="font-semibold">Lotes produzidos</h3>{order.execucao.lotesSaida.length ? <ul className="space-y-3">{order.execucao.lotesSaida.map(output => <li key={output.id}><ReferenceLabel kind="lote" id={output.loteId} /> · {showQuantity(output.quantidade)} {output.unidade} · <ReferenceLabel kind="local" id={output.localId} /><br /><Link className="text-emerald-700 underline" to={`/producao/genealogia/${output.loteId}`}>Consultar genealogia deste lote</Link></li>)}</ul> : <p>Perda total: nenhum lote vazio foi criado.</p>}{user?.roles.includes('Admin') && <Link className={buttonClass} to={`/estoque/operacoes/ajuste?execucao=${order.execucao.id}&ordem=${order.id}`}>Ajuste presente vinculado à execução</Link>}<p>O ajuste presente registra a situação atual; não desfaz a execução nem restaura ancestrais.</p></Panel>}<History id={order.id} /></>}</ProductionFrame>
}

function ExecutionRead({ order }: { order: ProductionOrder }) {
  if (!order.execucao || !order.planejamento) return null
  try {
    const snapshot = record(JSON.parse(order.execucao.snapshotJson))
    const actual = record(snapshot.realizado ?? snapshot.comando ?? snapshot.pedido ?? snapshot.reais ?? snapshot)
    const inputs = Array.isArray(actual.entradas) ? actual.entradas : []
    const outputs = Array.isArray(actual.saidas) ? actual.saidas : []
    const losses = Array.isArray(actual.perdas) ? actual.perdas : []
    const plan = order.planejamento
    return <div className="space-y-4"><h3 className="font-semibold">Planejado e realizado</h3><ScrollTable label="Consumo real e plano"><thead><tr><th scope="col">Entrada</th><th scope="col">Planejado</th><th scope="col">Real declarado por lote/local</th></tr></thead><tbody>{plan.escalonada.entradas.map(line => {
      const input = inputs.find(v => record(v).linhaId === line.id)
      const allocations = input && Array.isArray(record(input).alocacoes) ? record(input).alocacoes as unknown[] : []
      return <tr key={line.id}><td>{itemName(plan, line.itemId)}</td><td>{showQuantity(line.quantidade)} {line.unidade}</td><td>{allocations.length ? allocations.map((v, i) => { const a = record(v); return <p key={i}>{typeof a.quantidade === 'string' ? showQuantity(a.quantidade) : '—'} {String(a.unidade ?? '')} · <ReferenceLabel kind="lote" id={String(a.loteId)} /> · <ReferenceLabel kind="local" id={String(a.localId)} /></p> }) : 'Zero consumo nesta linha'}</td></tr>
    })}</tbody></ScrollTable><ScrollTable label="Saída real e plano"><thead><tr><th scope="col">Saída</th><th scope="col">Planejado</th><th scope="col">Real declarado</th></tr></thead><tbody>{plan.escalonada.saidas.map(line => <tr key={line.id}><td>{itemName(plan, line.itemId)} · {line.nome}</td><td>{showQuantity(line.quantidade)} {line.unidade}</td><td>{outputs.filter(v => record(v).linhaId === line.id).map((v, i) => { const o = record(v); return <p key={i}>{typeof o.quantidade === 'string' ? showQuantity(o.quantidade) : '—'} {String(o.unidade ?? '')} · {String(o.novoCodigoLote ?? 'Sem lote')}</p> })}</td></tr>)}</tbody></ScrollTable>{losses.length > 0 && <div><h3 className="font-semibold">Perdas declaradas</h3><ul className="list-inside list-disc">{losses.map((v, i) => { const loss = record(v); return <li key={i}>{typeof loss.quantidade === 'string' ? showQuantity(loss.quantidade) : '—'} {String(loss.unidade ?? '')} · {String(loss.motivo ?? '')}</li> })}</ul></div>}{typeof actual.observacao === 'string' && <p>{actual.observacao}</p>}</div>
  } catch { return <p>Não foi possível apresentar os reais preservados. Reconsulte a ordem antes de qualquer correção.</p> }
}

export function ProductionReconciliationPage() {
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  const [data, setData] = useState<ProductionPage<Reconciliation> | null>(null)
  const [page, setPage] = useState(1)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    if (!admin) return
    let live = true; setData(null); setError(null)
    void productionList('/reconciliacao', { page, pageSize: 25 }, parseReconciliation).then(rows => { if (live) setData(rows) }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [admin, page, retry])
  return <ProductionFrame title="Reconciliação de produção">{!admin ? <Notice error="Esta consulta exige o papel Admin atual." /> : <><Notice error={error} retry={() => setRetry(v => v + 1)} /><Panel><p>Consulta de reservas, ordens, execuções e movimentos no mesmo corte consistente. Divergências exigem diagnóstico; nenhum reparo automático será feito.</p>{!data && !error && <p role="status">Consultando reconciliação…</p>}{data && <>{data.items.every(row => row.coerente) && <p>Sem divergências nesta página do corte consultado.</p>}{data.items.map(row => <div key={row.ordemId} className="rounded-lg border p-3"><Link className="text-emerald-700 underline" to={`${root}/${row.ordemId}`}>{row.codigo}</Link> · {row.estado} · {row.coerente ? 'Coerente' : 'Divergência'}{!row.coerente && <ul className="list-inside list-disc">{row.divergencias.map((reason, i) => <li key={i}>{reason}</li>)}</ul>}</div>)}<Pagination page={page} totalPages={data.totalPages} onChange={setPage} /></>}</Panel></>}</ProductionFrame>
}
