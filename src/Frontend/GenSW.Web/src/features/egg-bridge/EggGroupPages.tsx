import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { listReturnState, useRememberListState, useRestoredListState } from '../../shared/details/listNavigation'
import { useAuth } from '../auth/hooks/useAuth'
import { buttonClass, Input, Notice, Pagination, Panel, primaryClass, ReferenceSelect, ScrollTable, Select } from '../inventory/InventoryControls'
import { inventoryError, parseResponsible, showTime } from '../inventory/inventoryService'
import { ReferenceLabel } from '../production/ProductionControls'
import { EggBridgeFrame, EggCommandReceipt, EggRecovery, EggSourceLabel } from './EggBridgeControls'
import { eggList, eggPreview, eggRead, eggRoot, parseEggDetail, parseEggGroup, parseEggReconciliation, parseEggTreatmentPreview } from './eggBridgeService'
import type { EggBridge, EggCommandResult, EggDetail, EggGroup, EggPage, EggReconciliation, EggSource, EggTreatment, EggTreatmentPreview } from './types'
import { useEggBridgeCommand } from './useEggBridgeCommand'

const groupRoot = `${eggRoot}/grupos`
const readDetail = (id: string) => eggRead(`/grupos/${id}`, parseEggDetail)
const treatmentLabels = { Anotacao: 'Anotar divergência', Invalidacao: 'Invalidar associação documental', Compensacao: 'Compensar entrada integral' }
export function EggGroupsPage() {
  const initial = useRestoredListState(groupRoot, { page: 1, pageSize: 25 })
  const [filters, setFilters] = useState(initial)
  const navigation = useRememberListState(groupRoot, filters)
  const [data, setData] = useState<EggPage<EggGroup> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true; setData(null); setError(null)
    void eggList('/grupos', filters, parseEggGroup).then(value => { if (live) setData(value) }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [filters, retry])
  return <EggBridgeFrame title="Grupos e histórico da ponte"><Notice error={error} retry={() => setRetry(v => v + 1)} /><Panel>{!data && !error && <p role="status">Consultando grupos…</p>}{data && <><ScrollTable label="Grupos da ponte de ovos"><thead><tr><th scope="col">Operação</th><th scope="col">Lote</th><th scope="col">Quantidade histórica</th><th scope="col">Registro</th><th scope="col">Ação</th></tr></thead><tbody>{data.items.map(group => <tr key={group.id}><td>{group.operacao === 'Associacao' ? 'Associação · delta zero' : 'Entrada nova'}</td><td><ReferenceLabel kind="lote" id={group.loteId} /></td><td>{group.quantidade} un</td><td>{showTime(group.createdAtUtc)}</td><td><Link className="text-emerald-700 underline" to={`${groupRoot}/${group.id}`} state={navigation}>Visualizar grupo</Link></td></tr>)}</tbody></ScrollTable>{!data.items.length && <p>Nenhum grupo registrado.</p>}<Pagination page={filters.page} totalPages={data.totalPages} onChange={page => setFilters(v => ({ ...v, page }))} /></>}</Panel></EggBridgeFrame>
}
function HistoricalSource({ bridge }: { bridge: EggBridge }) {
  try {
    const raw = JSON.parse(bridge.snapshotJson) as Record<string, unknown>
    const source = { id: String(raw.id ?? raw.producaoOvoId ?? raw.Id ?? bridge.producaoOvoId), animalId: String(raw.animalId ?? raw.AnimalId ?? ''), dataPostura: String(raw.dataPostura ?? raw.DataPostura ?? ''), pesoGramas: raw.pesoGramas ?? raw.PesoGramas, observacao: raw.observacao ?? raw.Observacao } as unknown as EggSource
    return <div className="space-y-2">{source.animalId && <EggSourceLabel source={source} />}<p>Peso no snapshot: {String(source.pesoGramas)} g · evidência zootécnica, sem conversão de lote.</p>{source.observacao && <p className="whitespace-pre-wrap break-words">Observação preservada: {source.observacao}</p>}</div>
  } catch { return <p>Snapshot histórico indisponível para apresentação. Preserve os fatos e solicite diagnóstico.</p> }
}
export function EggGroupDetailsPage() {
  const { id } = useParams()
  return <EggGroupDetails key={id} id={id} />
}
function EggGroupDetails({ id }: { id?: string }) {
  const location = useLocation()
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  const { record: detail, state, retry } = useRecordDetails(id, readDetail)
  const [result, setResult] = useState<EggCommandResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [treatment, setTreatment] = useState({ tipo: 'Anotacao' as EggTreatment, responsavelId: user?.userId ?? '', motivo: '', evidencia: '', coerenciaFisicaConfirmada: false })
  const [preview, setPreview] = useState<EggTreatmentPreview | null>(null)
  const [accepted, setAccepted] = useState(false)
  const [loading, setLoading] = useState(false)
  const preparing = useRef(false)
  const command = useEggBridgeCommand()
  const locked = command.busy || !!command.pending || loading
  const back = listReturnState(location.state, groupRoot) ?? listReturnState(location.state, eggRoot)
  const complete = (value: EggCommandResult) => { setResult(value); setPreview(null); setAccepted(false) }
  const change = (patch: Partial<typeof treatment>) => { setTreatment(v => ({ ...v, ...patch })); setPreview(null); setAccepted(false); setError(null) }
  const prepare = async () => {
    if (!id || locked || preparing.current) return
    setError(null); setPreview(null); setAccepted(false); preparing.current = true; setLoading(true)
    try {
      if (!treatment.responsavelId || !treatment.motivo.trim() || !treatment.evidencia.trim()) throw new Error('Informe responsável, motivo e evidência do tratamento.')
      setPreview(await eggPreview(`/grupos/${id}/previas-tratamento`, treatment, parseEggTreatmentPreview))
    } catch (e) { setError(inventoryError(e)) }
    finally { preparing.current = false; setLoading(false) }
  }
  const confirm = async () => {
    if (!id || !preview?.podeConfirmar || !accepted || locked) return
    setError(null)
    try { const value = await command.submit(preview.tipo, `/grupos/${id}/tratamentos`, { token: preview.token }); if (value) complete(value) } catch (e) { setError(inventoryError(e)) }
  }
  return <EggBridgeFrame title="Visualizar grupo da ponte"><Link className={buttonClass} to={back?.list.path ?? groupRoot} state={back}>Voltar à lista</Link><Notice error={error} /><EggRecovery control={command} complete={complete} setError={setError} /><EggCommandReceipt result={result} />{state === 'loading' && <p role="status">Consultando estado atual…</p>}{state !== 'ready' && state !== 'loading' && <Notice error={state === 'not-found' ? 'Grupo não encontrado.' : state === 'forbidden' ? 'Acesso não permitido.' : 'Não foi possível consultar o grupo.'} retry={retry} />}{detail && <><GroupRead detail={detail} /><button type="button" className={buttonClass} onClick={() => { retry(); setPreview(null); setAccepted(false) }}>Reconsultar estado atual do grupo</button>{admin && <Panel title="Preparar tratamento"><p>Compensar não libera IDs para reentrada. Invalidar uma associação não debita o recebimento manual nem libera capacidade. Perda física real exige saída ou descarte presente.</p><form onSubmit={e => e.preventDefault()} className="space-y-4"><fieldset disabled={locked} className="grid min-w-0 gap-4 sm:grid-cols-2"><Select label="Tratamento" value={treatment.tipo} options={Object.entries(treatmentLabels)} onChange={v => change({ tipo: v as EggTreatment })} /><ReferenceSelect label="Responsável pelo tratamento" path="/estoque/responsaveis" value={treatment.responsavelId || null} params={{ ativo: true }} parse={parseResponsible} caption={person => person.nome} onChange={id => change({ responsavelId: id ?? '' })} /><Input label="Motivo do tratamento" value={treatment.motivo} onChange={v => change({ motivo: v })} multiline required /><Input label="Evidência do tratamento" value={treatment.evidencia} onChange={v => change({ evidencia: v })} multiline required />{treatment.tipo === 'Compensacao' && <label className="flex items-start gap-3 sm:col-span-2"><input type="checkbox" className="mt-1" checked={treatment.coerenciaFisicaConfirmada} onChange={e => change({ coerenciaFisicaConfirmada: e.target.checked })} /><span>Conferi a coerência física e comprovei erro de registro da entrada integral; não se trata de perda real.</span></label>}</fieldset><button type="button" className={buttonClass} disabled={locked} onClick={() => void prepare()}>Preparar prévia do tratamento</button></form>{preview && <section aria-label="Prévia do tratamento" className="space-y-3 rounded-lg border border-emerald-300 p-4"><h3 className="font-semibold">Revise o tratamento</h3><p>{treatmentLabels[preview.tipo]} · delta de estoque {preview.deltaEstoque} un · validade até {showTime(preview.expiraEmUtc)}.</p>{preview.impedimentos.length > 0 && <ul className="list-inside list-disc">{preview.impedimentos.map((reason, i) => <li key={i}>{reason}</li>)}</ul>}<label className="flex items-start gap-3"><input type="checkbox" className="mt-1" checked={accepted} disabled={locked || !preview.podeConfirmar} onChange={e => setAccepted(e.target.checked)} /><span>Conferi o tratamento e seu efeito. O vínculo vitalício e os ordinais permanecerão ocupados.</span></label><button type="button" className={primaryClass} disabled={locked || !preview.podeConfirmar || !accepted} onClick={() => void confirm()}>Confirmar tratamento</button></section>}<Link className={buttonClass} to={`/estoque/operacoes/ajuste?grupoPonteOvos=${detail.grupo.id}&lote=${detail.grupo.loteId}`}>Registrar ajuste presente vinculado</Link><p>Ajuste exige contagem atual e evidência; não recria origem nem libera reentrada.</p></Panel>}</>}</EggBridgeFrame>
}
function GroupRead({ detail }: { detail: EggDetail }) {
  const group = detail.grupo
  return <><Panel title="Fato histórico e estado atual"><dl className="grid min-w-0 gap-4 break-words sm:grid-cols-2"><div><dt className="font-semibold">Operação original</dt><dd>{group.operacao === 'Associacao' ? 'Associação histórica · delta zero' : 'Entrada individual nova'} · {group.quantidade} un</dd></div><div><dt className="font-semibold">Registro</dt><dd>{showTime(group.createdAtUtc)}</dd></div><div><dt className="font-semibold">Lote histórico</dt><dd><ReferenceLabel kind="lote" id={group.loteId} /></dd></div><div><dt className="font-semibold">Local histórico</dt><dd><ReferenceLabel kind="local" id={group.localId} /></dd></div><div><dt className="font-semibold">Responsável</dt><dd><ReferenceLabel kind="responsavel" id={group.responsavelId} /></dd></div><div><dt className="font-semibold">Motivo</dt><dd>{group.motivo}</dd></div></dl><p className="whitespace-pre-wrap break-words">Evidência: {group.evidencia}</p><Link className="text-emerald-700 underline" to={`/estoque/movimentos/${group.eventoId}`}>Consultar evento físico original</Link>{group.operacao === 'Associacao' && <p>Esta associação prova apenas origem histórica. Não afirma disponibilidade atual nem qual ovo foi consumido. Nenhum movimento físico foi criado pela associação.</p>}{detail.avisos.map((warning, i) => <p key={i}>{warning}</p>)}<p>Conciliação atual: {detail.conciliacao.coerente ? 'coerente neste corte' : 'divergências identificadas'}</p>{detail.conciliacao.divergencias.length > 0 && <ul className="list-inside list-disc">{detail.conciliacao.divergencias.map((reason, i) => <li key={i}>{reason}</li>)}</ul>}</Panel><Panel title="Origens preservadas e revisão atual">{detail.pontes.map(bridge => {
    const current = detail.origensAtuais.find(row => row.producaoOvoId === bridge.producaoOvoId)
    const divergent = !current || current.hashOrigem !== bridge.hashOrigem || current.updatedAtUtc !== bridge.origemUpdatedAtUtc
    return <details className="min-w-0 space-y-3 rounded-lg border p-4" key={bridge.id}><summary className="cursor-pointer break-words font-semibold">Lançamento {bridge.producaoOvoId} · {divergent ? 'origem atual divergente' : 'origem atual coincide com snapshot'}</summary><HistoricalSource bridge={bridge} /><p>Snapshot observado em {showTime(bridge.origemUpdatedAtUtc)}; origem atual {current ? showTime(current.updatedAtUtc) : 'indisponível'}.</p>{current?.impedimento && <p>{current.impedimento}</p>}<p className="break-all text-sm">Hash preservado: {bridge.hashOrigem}</p><p>Edição zootécnica posterior não altera estoque, massa ou conversão.</p></details>
  })}</Panel><Panel title="Tratamentos imutáveis">{detail.tratamentos.length ? detail.tratamentos.map(fact => <article key={fact.id} className="min-w-0 space-y-2 rounded-lg border p-4"><h3 className="font-semibold">{treatmentLabels[fact.tipo]} · {showTime(fact.createdAtUtc)}</h3><p className="break-words">{fact.motivo}</p><p className="whitespace-pre-wrap break-words">{fact.evidencia}</p>{fact.eventoId && <Link className="text-emerald-700 underline" to={`/estoque/movimentos/${fact.eventoId}`}>Consultar evento compensatório</Link>}</article>) : <p>Nenhum tratamento registrado.</p>}<p>Vínculos e ordinais permanecem vitalícios, inclusive após compensação ou invalidação.</p></Panel></>
}
export function EggReconciliationPage() {
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  const [page, setPage] = useState(1)
  const [data, setData] = useState<EggPage<EggReconciliation> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    if (!admin) return
    let live = true; setData(null); setError(null)
    void eggList('/conciliacao', { page, pageSize: 25 }, parseEggReconciliation).then(value => { if (live) setData(value) }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [admin, page, retry])
  return <EggBridgeFrame title="Conciliação da ponte de ovos">{!admin ? <Notice error="Esta consulta exige o papel Admin atual." /> : <><Notice error={error} retry={() => setRetry(v => v + 1)} /><Panel><p>Consulta consistente de origem, prova, capacidade histórica, pontes, livro, projeção e reservas. Divergências exigem diagnóstico e conferência; não há reparo automático.</p>{!data && !error && <p role="status">Consultando conciliação…</p>}{data && <>{!data.items.length && <p>Nenhum grupo neste corte.</p>}{data.items.map(row => <article key={row.grupoId} className="min-w-0 space-y-3 rounded-lg border p-4"><Link className="break-all text-emerald-700 underline" to={`${groupRoot}/${row.grupoId}`}>Visualizar grupo {row.grupoId}</Link><p>{row.coerente ? 'Coerente neste corte' : 'Divergências identificadas'}</p>{row.divergencias.length > 0 && <ul className="list-inside list-disc">{row.divergencias.map((reason, i) => <li key={i}>{reason}</li>)}</ul>}</article>)}<Pagination page={page} totalPages={data.totalPages} onChange={setPage} /></>}</Panel></>}</EggBridgeFrame>
}
