import { useCostDetails } from './useCostDetails'
import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../auth/hooks/useAuth'
import { useUnsavedChanges } from '../../shared/navigation/useUnsavedChanges'
import { listReturnState, savedRecordState, useRememberListState, useRestoredListState } from '../../shared/details/listNavigation'
import { Input, Notice, Pagination, Panel, ReferenceSelect, ScrollTable, Select, buttonClass, primaryClass } from '../inventory/InventoryControls'
import { inventoryError, parseItem, showTime, today } from '../inventory/inventoryService'
import { CostFrame, CostPending } from './MaterialCostControls'
import { costList, costRead, costRecord, parseCostReference } from './materialCostService'
import { displayDecimal, quantityInput, unitPriceInput } from './decimals'
import { useCostCommand } from './useCostCommand'
import type { CostPage, CostReference, ReferenceCommand } from './types'

const root = '/custos-materiais/referencias'
const grid = 'grid min-w-0 gap-4 sm:grid-cols-2'
export function CostReferencesPage() {
  const { user } = useAuth()
  const initial = useRestoredListState(root, { page: 1, pageSize: 25, search: '', estado: '', itemId: '', data: '' })
  const [filters, setFilters] = useState(initial)
  const navigation = useRememberListState(root, filters)
  const [data, setData] = useState<CostPage<CostReference> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [revision, setRevision] = useState(0)
  const encoded = JSON.stringify(filters)
  useEffect(() => {
    let live = true; setData(null); setError(null)
    const query = JSON.parse(encoded)
    for (const key of Object.keys(query)) if (query[key] === '') delete query[key]
    void costList('/referencias', query, parseCostReference).then(value => { if (live) setData(value) }).catch(error => { if (live) setError(inventoryError(error)) })
    return () => { live = false }
  }, [encoded, revision])
  const set = (field: keyof typeof filters, value: string) => setFilters(current => ({ ...current, [field]: value, page: 1 }))
  return <CostFrame title="Referências documentais de materiais"><Panel><p>Cotação é uma referência para estimar. Sua publicação ou substituição não altera custo comprovado, estoque, apuração ou Caixa.</p>{user?.roles.includes('Admin') && <Link className={primaryClass} to={`${root}/nova`} state={navigation}>Publicar referência</Link>}</Panel><Panel title="Filtros"><div className={grid}><Input label="Buscar referência" value={filters.search} onChange={value => set('search', value)} /><Select label="Estado documental" value={filters.estado} onChange={value => set('estado', value)} options={[[ '', 'Todos'], ['Publicada', 'Publicada'], ['Substituida', 'Substituída'], ['Revogada', 'Revogada']]} /><ReferenceSelect label="Item da referência" path="/itens" value={filters.itemId || null} onChange={value => set('itemId', value ?? '')} parse={parseItem} caption={item => `${item.codigo} — ${item.nome}`} /><Input label="Vigente na data" value={filters.data} type="date" onChange={value => set('data', value)} /></div></Panel><Notice error={error} retry={() => setRevision(value => value + 1)} />{!data && !error && <p role="status">Consultando referências…</p>}{data && <Panel>{data.items.length === 0 ? <p>Nenhuma referência encontrada. Ausência de referência não significa custo zero.</p> : <ScrollTable label="Referências documentais"><thead><tr>{['Item', 'Fonte e data', 'Preço de referência', 'Vigência', 'Estado', 'Ação'].map(label => <th scope="col" key={label}>{label}</th>)}</tr></thead><tbody>{data.items.map(reference => <tr key={reference.id}><td>{reference.itemCodigo} — {reference.itemNome}</td><td className="break-words">{reference.fonte}<br />{reference.dataFonte}</td><td>BRL {displayDecimal(reference.valorUnitario)} por {displayDecimal(reference.quantidadeBase)} {reference.unidade}</td><td>{reference.vigenteDesde} até {reference.vigenteAte ?? 'sem fim declarado'} (fim exclusivo)</td><td>{reference.estado}</td><td><Link className="text-emerald-700 underline" to={`${root}/${reference.id}`} state={navigation}>Visualizar</Link></td></tr>)}</tbody></ScrollTable>}<Pagination page={filters.page} totalPages={data.totalPages} onChange={page => setFilters(value => ({ ...value, page }))} /></Panel>}</CostFrame>
}
export function CostReferenceDetailsPage() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const { value, error, retry } = useCostDetails(`/referencias/${id}`, parseCostReference)
  const [history, setHistory] = useState<CostPage<CostReference> | null>(null)
  const [historyError, setHistoryError] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [revision, setRevision] = useState(0)
  const location = useLocation()
  const back = listReturnState(location.state, root)
  useEffect(() => {
    let live = true; setHistory(null); setHistoryError(null)
    void costList(`/referencias/${id}/historico`, { page, pageSize: 10 }, parseCostReference).then(value => { if (live) setHistory(value) }).catch(error => { if (live) setHistoryError(inventoryError(error)) })
    return () => { live = false }
  }, [id, page, revision])
  return <CostFrame title="Visualizar referência documental"><Link className={buttonClass} to={back?.list.path ?? root} state={back}>Voltar para Referências</Link><Notice error={error} retry={retry} />{!value && !error && <p role="status">Consultando referência…</p>}{value && <Panel title={`${value.itemCodigo} — ${value.itemNome}`}><p className="font-semibold">Referência documental · {value.estado}</p><dl className={grid}>{[['Fonte', value.fonte], ['Referência protegida', value.referenciaDocumento], ['Data da fonte', value.dataFonte], ['Preço por quantidade-base', `BRL ${displayDecimal(value.valorUnitario)} por ${displayDecimal(value.quantidadeBase)} ${value.unidade}`], ['Vigência', `${value.vigenteDesde} a ${value.vigenteAte ?? 'sem fim declarado'} · fim exclusivo`], ['Aplicabilidade', value.aplicabilidade], ['Publicação', showTime(value.createdAtUtc)]].map(([label, text]) => <div key={label}><dt className="font-semibold">{label}</dt><dd className="break-words">{text}</dd></div>)}</dl><p>Referências concorrentes não são escolhidas automaticamente. Selecione a versão exata em cada estimativa; uma substituição preserva os snapshots anteriores.</p>{value.substituiId && <Link className="text-emerald-700 underline" to={`${root}/${value.substituiId}`} state={location.state}>Consultar versão anterior</Link>}{user?.roles.includes('Admin') && value.estado === 'Publicada' && <Link className={buttonClass} to={`${root}/${value.id}/substituir`} state={location.state}>Substituir ou revogar por novo fato</Link>}</Panel>}<Notice error={historyError} retry={() => setRevision(value => value + 1)} />{history && <Panel title="Histórico documental"><ul className="space-y-3">{history.items.map(reference => <li className="break-words" key={reference.id}><Link className="text-emerald-700 underline" to={`${root}/${reference.id}`} state={location.state}>{reference.dataFonte} · BRL {displayDecimal(reference.valorUnitario)} por {displayDecimal(reference.quantidadeBase)} {reference.unidade} · {reference.estado}</Link><p>{reference.fonte}</p></li>)}</ul><Pagination page={page} totalPages={history.totalPages} onChange={setPage} /></Panel>}</CostFrame>
}
export function CostReferenceFormPage() {
  const { id } = useParams()
  return <ReferenceForm key={id ?? 'nova'} original={id} />
}
function ReferenceForm({ original }: { original?: string }) {
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  const command = useCostCommand(`reference:${original ?? 'new'}`)
  const [form, setForm] = useState<ReferenceCommand>(() => command.pending ? command.pending.body as unknown as ReferenceCommand : { id: crypto.randomUUID(), itemId: '', fonte: '', referenciaDocumento: '', dataFonte: today(), moeda: 'BRL', unidade: 'kg', quantidadeBase: '1', valorUnitario: '', vigenteDesde: today(), vigenteAte: null, aplicabilidade: '', motivo: '', revogada: false })
  const [ready, setReady] = useState(!original)
  const [error, setError] = useState<string | null>(null)
  const [dirty, setDirty] = useState(false)
  const [revision, setRevision] = useState(0)
  const location = useLocation()
  const navigate = useNavigate()
  const back = listReturnState(location.state, root)
  const draft = useUnsavedChanges(dirty || !!command.pending)
  useEffect(() => {
    let live = true
    if (!original || !admin) return
    setReady(false)
    void costRead(`/referencias/${original}`, parseCostReference).then(value => {
      if (!live) return
      if (!command.pending) setForm(current => ({ ...current, itemId: value.itemId, unidade: value.unidade, quantidadeBase: value.quantidadeBase, valorUnitario: value.valorUnitario, fonte: value.fonte, referenciaDocumento: value.referenciaDocumento, aplicabilidade: value.aplicabilidade }))
      setReady(true)
    }).catch(error => { if (live) setError(inventoryError(error)) })
    return () => { live = false }
  // The saved body must retain the original identity and expected source after reload.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [original, admin, revision])
  const change = (key: keyof ReferenceCommand, value: string | boolean | null) => { setDirty(true); setForm(current => ({ ...current, [key]: value })) }
  const complete = (value: unknown) => { const result = costRecord(value); draft.markSaved(); setDirty(false); navigate(`${root}/${String(result.id)}`, { state: savedRecordState(back, `${root}/${String(result.id)}`) }) }
  const send = async () => {
    setError(null)
    try {
      if (!form.itemId || !form.fonte.trim() || !form.referenciaDocumento.trim() || !form.dataFonte || !form.vigenteDesde || !form.aplicabilidade.trim() || !form.motivo.trim()) throw new Error('Informe item, fonte, documento, datas, aplicabilidade e motivo.')
      const body = { ...form, quantidadeBase: quantityInput(form.quantidadeBase), valorUnitario: unitPriceInput(form.valorUnitario), fonte: form.fonte.trim(), referenciaDocumento: form.referenciaDocumento.trim(), aplicabilidade: form.aplicabilidade.trim(), motivo: form.motivo.trim() }
      const value = await command.submit(form.id, original ? 'substituicao' : 'referencia', original ? `/custos-materiais/referencias/${original}/substituicoes` : '/custos-materiais/referencias', body)
      if (value) complete(value)
    } catch (error) { setError(inventoryError(error)) }
  }
  return <CostFrame title={original ? 'Substituir ou revogar referência' : 'Publicar referência documental'}>{!admin ? <Notice error="Publicação e substituição exigem papel Admin atual." /> : <><Notice error={error} retry={!ready ? () => setRevision(value => value + 1) : undefined} /><CostPending control={command} complete={complete} setError={setError} />{!ready && !error && <p role="status">Consultando versão original…</p>}{ready && <Panel><form onSubmit={event => event.preventDefault()}><fieldset disabled={command.busy || !!command.pending || !!command.recoveryError} className="space-y-4"><p>Esta ação grava um fato documental novo. Não preenche custo comprovado e não altera snapshots anteriores.</p><div className={grid}><ReferenceSelect label="Item da referência" path="/itens" value={form.itemId || null} onChange={value => change('itemId', value ?? '')} parse={parseItem} caption={item => `${item.codigo} — ${item.nome}`} /><Select label="Unidade da quantidade-base" value={form.unidade} onChange={value => change('unidade', value)} options={[[ 'kg', 'kg'], ['g', 'g'], ['L', 'L'], ['mL', 'mL'], ['un', 'un']]} /><Input label="Quantidade-base da cotação" value={form.quantidadeBase} onChange={value => change('quantidadeBase', value)} required /><Input label="Preço de referência BRL por quantidade-base" value={form.valorUnitario} onChange={value => change('valorUnitario', value)} required hint="Até seis casas. Este campo é uma cotação, separado do total comprovado de um recebimento." /><Input label="Fonte da referência" value={form.fonte} onChange={value => change('fonte', value)} required /><Input label="Referência documental protegida" value={form.referenciaDocumento} onChange={value => change('referenciaDocumento', value)} required /><Input label="Data da fonte" value={form.dataFonte} type="date" onChange={value => change('dataFonte', value)} required /><Input label="Vigência inicial (inclusiva)" value={form.vigenteDesde} type="date" onChange={value => change('vigenteDesde', value)} required /><Input label="Vigência final (exclusiva, opcional)" value={form.vigenteAte ?? ''} type="date" onChange={value => change('vigenteAte', value || null)} /><Input label="Aplicabilidade da referência" value={form.aplicabilidade} onChange={value => change('aplicabilidade', value)} multiline required /></div><Input label="Motivo da publicação ou substituição" value={form.motivo} onChange={value => change('motivo', value)} multiline required />{original && <label className="flex items-start gap-2"><input type="checkbox" checked={form.revogada} onChange={event => change('revogada', event.target.checked)} />Registrar revogação documental, conservando a versão anterior.</label>}<button className={primaryClass} type="button" onClick={() => void send()}>{original ? 'Registrar substituição ou revogação' : 'Publicar referência'}</button></fieldset></form></Panel>}<button className={buttonClass} type="button" onClick={() => draft.confirmDiscard(() => navigate(original ? `${root}/${original}` : root, { state: back }))}>Voltar sem publicar</button></>}</CostFrame>
}
