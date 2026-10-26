import { actionNames } from './productionActions'
import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/hooks/useAuth'
import { httpRequest } from '../../shared/http/httpClient'
import { useUnsavedChanges } from '../../shared/navigation/useUnsavedChanges'
import { listReturnState, savedRecordState } from '../../shared/details/listNavigation'
import { buttonClass, Input, Notice, Panel, primaryClass, ReferenceSelect, ScrollTable, Select } from '../inventory/InventoryControls'
import { decimalInput, inventoryError, inventoryRead, parseConversion, parseLocal, parseLote, parsePhysicalQuantity, parseProfile, parseResponsible, parseSaldo, showQuantity, showTime, today } from '../inventory/inventoryService'
import type { PhysicalQuantity, Saldo } from '../inventory/types'
import { itemName, record } from './productionService'
import { PendingCommand } from './ProductionControls'
import { useProductionCommand } from './useProductionCommand'
import type { Allocation, Confirmation, PlanSnapshot, ProductionInput, ProductionLoss, ProductionOrder, ProductionOutput } from './types'

const units = [['kg', 'kg'], ['g', 'g'], ['L', 'L'], ['mL', 'mL'], ['un', 'un']] as const
const grid = 'grid min-w-0 gap-4 sm:grid-cols-2'
const zero = (value: string) => /^0+(?:[.,]0+)?$/.test(value.trim())
interface QuantityPreview { papel: string; linhaId: string; id: string; quantidade: PhysicalQuantity }
interface Preview { quantidades: QuantityPreview[]; avisos: string[] }
function parsePreview(value: unknown): Preview {
  const v = record(value)
  if (!Array.isArray(v.quantidades) || !Array.isArray(v.avisos) || !v.avisos.every(x => typeof x === 'string')) throw new Error('A API retornou uma prévia inválida.')
  return { avisos: v.avisos as string[], quantidades: v.quantidades.map(raw => {
    const r = record(raw)
    if (typeof r.id !== 'string' || typeof r.linhaId !== 'string' || !['Entrada', 'Saida'].includes(String(r.papel))) throw new Error('A API retornou uma linha de prévia inválida.')
    return { papel: String(r.papel), linhaId: r.linhaId, id: r.id, quantidade: parsePhysicalQuantity(r.quantidade) }
  }) }
}
const allocation = (unit: string): Allocation => ({ id: crypto.randomUUID(), loteId: '', localId: '', quantidade: '', unidade: unit, conversaoItemId: null, aceiteQuantizacao: null })
function initialInputs(order: ProductionOrder): ProductionInput[] {
  return order.planejamento!.escalonada.entradas.map(line => ({ linhaId: line.id, alocacoes: order.reservas.filter(r => r.linhaId === line.id && !zero(r.quantidadeAtiva)).map(r => ({ id: r.id, loteId: r.loteId, localId: r.localId, quantidade: r.quantidadeDeclarada ?? r.quantidadeAtiva, unidade: r.unidadeDeclarada ?? r.unidade, conversaoItemId: r.conversaoItemId ?? null, aceiteQuantizacao: null })) }))
}
function output(order: ProductionOrder, lineId: string, index: number): ProductionOutput {
  const line = order.planejamento!.escalonada.saidas.find(s => s.id === lineId)!
  return { id: crypto.randomUUID(), linhaId: lineId, quantidade: line.quantidade, unidade: line.unidade, novoCodigoLote: `${order.codigo}-${index + 1}`, localId: '', dataOrigem: today(), fabricacao: null, coleta: null, validade: null, fonteValidade: null, responsavelValidadeId: null, perfilNutricionalId: null, conversaoItemId: null, aplicabilidade: null, aceiteQuantizacao: null, bloqueado: false }
}

function PositionBalance({ loteId, localId }: { loteId: string; localId: string }) {
  const [stock, setStock] = useState<Saldo | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true; setStock(null); setError(null)
    if (!loteId || !localId) return
    void inventoryRead(`saldos/${loteId}/${localId}`, parseSaldo).then(s => { if (live) setStock(s) }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [loteId, localId, retry])
  if (!loteId || !localId) return null
  return <div className="space-y-2 rounded-lg bg-slate-50 p-3"><Notice error={error} retry={() => setRetry(v => v + 1)} />{!stock && !error && <p role="status">Consultando saldo da posição…</p>}{stock && <><dl className="grid gap-2 sm:grid-cols-3"><div><dt className="font-semibold">Físico</dt><dd>{showQuantity(stock.fisico)} {stock.unidade}</dd></div><div><dt className="font-semibold">Reservado</dt><dd>{showQuantity(stock.reservado)} {stock.unidade}</dd></div><div><dt className="font-semibold">Disponível</dt><dd>{showQuantity(stock.disponivel)} {stock.unidade}</dd></div></dl><p>{stock.usoOrdinarioPermitido ? `Utilizável: ${showQuantity(stock.quantidadeElegivel)} ${stock.unidade}` : `Uso ordinário impedido: ${stock.motivosIndisponibilidade.join(' · ')}`}</p>{stock.avisos.map((warning, i) => <p key={i} className="text-amber-900">{warning}</p>)}<p className="text-xs text-slate-600">Observado em {showTime(stock.observadoEmUtc)}. O comando revalida disponibilidade; a própria reserva continua incluída no reservado exibido.</p></>}</div>
}

function InputsEditor({ plan, inputs, onChange, start, exception }: { plan: PlanSnapshot; inputs: ProductionInput[]; onChange: (value: ProductionInput[]) => void; start: boolean; exception: boolean }) {
  const update = (lineId: string, allocations: Allocation[]) => onChange(inputs.map(line => line.linhaId === lineId ? { ...line, alocacoes: allocations } : line))
  return <div className="space-y-5"><h2 className="text-xl font-semibold">{start ? 'Alocações para reserva integral' : 'Entradas reais'}</h2>{plan.escalonada.entradas.map((line, index) => {
    const row = inputs.find(r => r.linhaId === line.id)!
    return <fieldset key={line.id} className="min-w-0 space-y-4 rounded-xl border p-4"><legend className="max-w-full break-words px-1 font-semibold">{index + 1}. {itemName(plan, line.itemId)}</legend><p>Planejado: {showQuantity(line.quantidade)} {line.unidade} · {line.papel} · {line.escala}</p>{line.subReceitaVersaoId && <p>Selecione somente o lote do intermediário em estoque. Seus ancestrais não serão debitados.</p>}{!row.alocacoes.length && <p>{start ? 'Adicione lotes e locais suficientes para reservar toda esta linha.' : 'Zero consumo declarado nesta linha; a reserva remanescente será liberada na confirmação.'}</p>}{row.alocacoes.map((a, i) => <div key={a.id} className="min-w-0 space-y-4 rounded-lg border border-slate-200 bg-slate-50/50 p-3"><h3 className="font-semibold">Alocação {i + 1}</h3><div className={grid}><ReferenceSelect label={`Lote da entrada ${index + 1}, alocação ${i + 1}`} path="/estoque/lotes" value={a.loteId || null} params={{ itemId: line.itemId, ...(exception ? {} : { ativo: true, situacao: 'Liberado' }) }} parse={parseLote} caption={lot => `${lot.codigo} — ${lot.itemNome} · ${lot.situacao}${lot.ativo ? '' : ' · Inativo'} · validade ${lot.validade ?? 'não informada'}`} onChange={id => update(line.id, row.alocacoes.map(x => x.id === a.id ? { ...x, id: !start && x.loteId !== (id ?? '') ? crypto.randomUUID() : x.id, loteId: id ?? '', conversaoItemId: null } : x))} /><ReferenceSelect label={`Local da entrada ${index + 1}, alocação ${i + 1}`} path="/estoque/locais" value={a.localId || null} params={exception ? {} : { ativo: true, finalidade: 'Ordinario' }} parse={parseLocal} caption={local => `${local.codigo} — ${local.nome} · ${local.finalidade}${local.ativo ? '' : ' · Inativo'}`} onChange={id => update(line.id, row.alocacoes.map(x => x.id === a.id ? { ...x, id: !start && x.localId !== (id ?? '') ? crypto.randomUUID() : x.id, localId: id ?? '' } : x))} /><Input label={`${start ? 'Quantidade a reservar' : 'Quantidade realmente retirada'} · entrada ${index + 1}, alocação ${i + 1}`} value={a.quantidade} onChange={v => update(line.id, row.alocacoes.map(x => x.id === a.id ? { ...x, quantidade: v } : x))} required /><Select label={`Unidade · entrada ${index + 1}, alocação ${i + 1}`} value={a.unidade} options={units} onChange={v => update(line.id, row.alocacoes.map(x => x.id === a.id ? { ...x, unidade: v } : x))} /><ReferenceSelect label={`Conversão documental · entrada ${index + 1}, alocação ${i + 1}`} path={`/itens/${line.itemId}/conversoes`} detailPath={id => `/estoque/conversoes/${id}`} value={a.conversaoItemId} parse={parseConversion} caption={c => `Versão ${c.numero} · ${c.origem} → ${c.destino} · fator ${showQuantity(c.fator)} · ${c.fonte}`} onChange={id => update(line.id, row.alocacoes.map(x => x.id === a.id ? { ...x, conversaoItemId: id } : x))} /></div><PositionBalance loteId={a.loteId} localId={a.localId} /><button className={buttonClass} type="button" onClick={() => update(line.id, row.alocacoes.filter(x => x.id !== a.id))}>{start ? 'Remover alocação' : 'Declarar esta alocação não utilizada'}</button></div>)}<button className={buttonClass} type="button" onClick={() => update(line.id, [...row.alocacoes, allocation(line.unidade)])}>Adicionar alocação à entrada {index + 1}</button></fieldset>
  })}</div>
}

function OutputsEditor({ order, outputs, onChange, exception }: { order: ProductionOrder; outputs: ProductionOutput[]; onChange: (value: ProductionOutput[]) => void; exception: boolean }) {
  const plan = order.planejamento!
  const change = (id: string, patch: Partial<ProductionOutput>) => onChange(outputs.map(o => o.id === id ? { ...o, ...patch } : o))
  return <div className="space-y-5"><h2 className="text-xl font-semibold">Saídas reais e novos lotes</h2><p>Cada parcela positiva cria um lote novo no destino escolhido. Saída zero registra a linha sem criar lote vazio. Perfil e conversão da saída são escolhas explícitas.</p>{outputs.map((o, index) => {
    const line = plan.escalonada.saidas.find(s => s.id === o.linhaId)!
    const empty = zero(o.quantidade)
    return <fieldset key={o.id} className="min-w-0 space-y-4 rounded-xl border p-4"><legend className="max-w-full break-words px-1 font-semibold">Saída {index + 1}: {itemName(plan, line.itemId)} · {line.nome}</legend><p>Planejado na linha: {showQuantity(line.quantidade)} {line.unidade}</p><div className={grid}><Input label={`Quantidade real da saída ${index + 1}`} value={o.quantidade} onChange={v => change(o.id, { quantidade: v })} required /><Select label={`Unidade da saída ${index + 1}`} value={o.unidade} options={units} onChange={v => change(o.id, { unidade: v })} /></div>{!empty && <><div className={grid}><Input label={`Código do novo lote ${index + 1}`} value={o.novoCodigoLote} onChange={v => change(o.id, { novoCodigoLote: v })} required maxLength={100} /><ReferenceSelect label={`Destino da saída ${index + 1}`} path="/estoque/locais" value={o.localId || null} params={{ ativo: true, finalidade: exception || o.bloqueado ? 'Segregacao' : 'Ordinario' }} parse={parseLocal} caption={local => `${local.codigo} — ${local.nome} · ${local.finalidade}`} onChange={id => change(o.id, { localId: id ?? '' })} /><Input label={`Data de origem do lote ${index + 1}`} type="date" value={o.dataOrigem ?? ''} onChange={v => change(o.id, { dataOrigem: v || null })} /><Input label={`Fabricação do lote ${index + 1}`} type="date" value={o.fabricacao ?? ''} onChange={v => change(o.id, { fabricacao: v || null })} /><Input label={`Coleta do lote ${index + 1}`} type="date" value={o.coleta ?? ''} onChange={v => change(o.id, { coleta: v || null })} /><Input label={`Validade do lote ${index + 1}`} type="date" value={o.validade ?? ''} onChange={v => change(o.id, { validade: v || null })} />{o.validade && <><Input label={`Fonte da validade do lote ${index + 1}`} value={o.fonteValidade ?? ''} onChange={v => change(o.id, { fonteValidade: v || null })} required /><ReferenceSelect label={`Responsável pela validade do lote ${index + 1}`} path="/estoque/responsaveis" value={o.responsavelValidadeId} params={{ ativo: true }} parse={parseResponsible} caption={r => r.nome} onChange={id => change(o.id, { responsavelValidadeId: id })} /></>}<ReferenceSelect label={`Perfil publicado da saída ${index + 1} (opcional)`} path={`/itens/${line.itemId}/perfis-nutricionais`} detailPath={id => `/perfis-nutricionais/${id}`} value={o.perfilNutricionalId} params={{ estado: 'Publicado' }} parse={parseProfile} caption={p => `Versão ${p.numero} · ${p.conteudo.nome} · ${p.estado}`} onChange={id => change(o.id, { perfilNutricionalId: id })} /><ReferenceSelect label={`Conversão documental da saída ${index + 1} (opcional)`} path={`/itens/${line.itemId}/conversoes`} detailPath={id => `/estoque/conversoes/${id}`} value={o.conversaoItemId} parse={parseConversion} caption={c => `Versão ${c.numero} · ${c.origem} → ${c.destino} · fator ${showQuantity(c.fator)} · ${c.fonte}`} onChange={id => change(o.id, { conversaoItemId: id })} /></div><Input label={`Aplicabilidade do perfil/conversão da saída ${index + 1}`} value={o.aplicabilidade ?? ''} onChange={v => change(o.id, { aplicabilidade: v || null })} multiline />{!o.validade && <p className="text-amber-900">Validade não informada; nenhum prazo será inferido da receita.</p>}<label className="flex items-start gap-2"><input className="mt-1" type="checkbox" checked={exception || o.bloqueado} disabled={exception} onChange={e => change(o.id, { bloqueado: e.target.checked, localId: '' })} />Saída bloqueada, com destino de segregação ativo</label></>}{empty && <p>Esta linha tem saída zero; os campos de lote e destino não geram cadastro.</p>}<button className={buttonClass} type="button" onClick={() => onChange(outputs.filter(x => x.id !== o.id))}>Remover parcela da saída {index + 1}</button></fieldset>
  })}<div className="flex flex-wrap gap-3">{plan.escalonada.saidas.map((line, index) => <button className={buttonClass} type="button" key={line.id} onClick={() => onChange([...outputs, { ...output(order, line.id, outputs.length), quantidade: '' }])}>Adicionar parcela da saída planejada {index + 1}</button>)}</div></div>
}

function LossesEditor({ plan, losses, onChange }: { plan: PlanSnapshot; losses: ProductionLoss[]; onChange: (value: ProductionLoss[]) => void }) {
  const change = (index: number, patch: Partial<ProductionLoss>) => onChange(losses.map((loss, i) => i === index ? { ...loss, ...patch } : loss))
  return <div className="space-y-4"><h2 className="text-xl font-semibold">Perdas e desvios medidos</h2><p>A perda de processo discrimina o material já debitado nas entradas e não gera outra baixa. Embalagem não compõe rendimento alimentar.</p>{losses.map((loss, index) => <fieldset className="space-y-4 rounded-lg border p-3" key={index}><legend className="px-1 font-semibold">Perda {index + 1}</legend><div className={grid}><Input label={`Quantidade da perda ${index + 1}`} value={loss.quantidade} onChange={v => change(index, { quantidade: v })} /><Select label={`Unidade da perda ${index + 1}`} value={loss.unidade} onChange={v => change(index, { unidade: v })} options={units} /><Select label={`Linha da perda ${index + 1}`} value={loss.linhaId ?? ''} onChange={v => change(index, { linhaId: v || null })} options={[[ '', 'Processo'], ...plan.escalonada.entradas.map((line, i) => [line.id, `Entrada ${i + 1}: ${itemName(plan, line.itemId)}`] as const)]} /></div><Input label={`Motivo da perda ${index + 1}`} value={loss.motivo} onChange={v => change(index, { motivo: v })} required multiline /><button className={buttonClass} type="button" onClick={() => onChange(losses.filter((_, i) => i !== index))}>Remover perda {index + 1}</button></fieldset>)}<button className={buttonClass} type="button" onClick={() => onChange([...losses, { quantidade: '', unidade: 'kg', motivo: '', linhaId: null }])}>Adicionar perda</button></div>
}

export function ProductionExecutionForm({ order, action }: { order: ProductionOrder; action: 'inicio' | 'confirmacao' | 'confirmacao-ocorrencia' }) {
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  const start = action === 'inicio'
  const exception = action === 'confirmacao-ocorrencia'
  const location = useLocation()
  const navigate = useNavigate()
  const command = useProductionCommand(order.id)
  const pendingBody = command.pending?.operation === action ? command.pending.body as Partial<Confirmation> : null
  const plan = order.planejamento!
  const [inputs, setInputs] = useState<ProductionInput[]>(pendingBody?.entradas ?? initialInputs(order))
  const [outputs, setOutputs] = useState<ProductionOutput[]>(pendingBody?.saidas ?? plan.escalonada.saidas.map((line, i) => output(order, line.id, i)))
  const [losses, setLosses] = useState<ProductionLoss[]>(pendingBody?.perdas ?? plan.escalonada.perdas.map(loss => ({ quantidade: loss.quantidade, unidade: loss.unidade, motivo: loss.motivo, linhaId: null })))
  const [responsible, setResponsible] = useState(pendingBody?.responsavelId ?? order.responsavelId)
  const [reason, setReason] = useState(pendingBody?.motivo ?? '')
  const [note, setNote] = useState(pendingBody?.observacao ?? '')
  const [observed, setObserved] = useState(pendingBody?.dataObservada ?? today())
  const [evidence, setEvidence] = useState(pendingBody?.evidencia ?? '')
  const [fitness, setFitness] = useState(pendingBody?.aptidaoNoFato ?? 'Irregular')
  const [preview, setPreview] = useState<Preview | null>(null)
  const [previewing, setPreviewing] = useState(false)
  const [acceptance, setAcceptance] = useState<Record<string, { checked: boolean; reason: string }>>({})
  const [error, setError] = useState<string | null>(null)
  const [dirty, setDirty] = useState(false)
  const heading = useRef<HTMLElement>(null)
  const { markSaved, confirmDiscard } = useUnsavedChanges(dirty || !!command.pending)
  const back = listReturnState(location.state, '/producao/ordens')
  const changed = () => { setDirty(true); setPreview(null); setAcceptance({}) }
  const changeInputs = (value: ProductionInput[]) => { changed(); setInputs(value) }
  const changeOutputs = (value: ProductionOutput[]) => { changed(); setOutputs(value) }
  const changeLosses = (value: ProductionLoss[]) => { changed(); setLosses(value) }
  const complete = (result: ProductionOrder) => { markSaved(); setDirty(false); navigate(`/producao/ordens/${result.id}`, { state: savedRecordState(back, `/producao/ordens/${result.id}`) }) }
  const acceptanceFor = (id: string) => {
    const p = preview?.quantidades.find(q => q.id === id)
    const a = acceptance[id]
    return p?.quantidade.exigeAceite && a?.checked && a.reason.trim() ? { calculado: p.quantidade.calculada, normalizado: p.quantidade.normalizada, residuo: p.quantidade.residuo, motivo: a.reason.trim() } : null
  }
  const body = (withAcceptance: boolean) => {
    if (!responsible || !reason.trim()) throw new Error('Selecione o responsável e informe um motivo explícito para a operação.')
    const normalizedInputs = inputs.map(line => ({ ...line, alocacoes: line.alocacoes.map(a => {
      if (!a.loteId || !a.localId) throw new Error('Selecione o lote e o local de cada alocação.')
      return { ...a, quantidade: decimalInput(a.quantidade, !start), aceiteQuantizacao: withAcceptance ? acceptanceFor(a.id) : null }
    }) }))
    if (start && normalizedInputs.some(line => !line.alocacoes.length)) throw new Error('O início exige alocação integral de todas as entradas, inclusive embalagens e consumíveis.')
    if (start) return { versaoEsperada: order.revisao, responsavelId: responsible, entradas: normalizedInputs, motivo: reason.trim() }
    if (exception && (!evidence.trim() || !observed)) throw new Error('Informe a data observada e evidência do fato já ocorrido. Esta via não autoriza novo uso irregular.')
    const normalizedOutputs = outputs.map(o => {
      const quantity = decimalInput(o.quantidade, true)
      if (!zero(quantity) && (!o.novoCodigoLote.trim() || !o.localId)) throw new Error('Toda saída positiva exige código de lote novo e destino explícito.')
      if (!zero(quantity) && o.validade && (!o.fonteValidade?.trim() || !o.responsavelValidadeId)) throw new Error('Informe fonte e responsável para cada validade declarada.')
      return { ...o, quantidade: quantity, novoCodigoLote: o.novoCodigoLote.trim(), bloqueado: exception || o.bloqueado, aceiteQuantizacao: withAcceptance ? acceptanceFor(o.id) : null }
    })
    const normalizedLosses = losses.map(loss => {
      if (!loss.motivo.trim()) throw new Error('Cada perda exige motivo explícito.')
      return { ...loss, quantidade: decimalInput(loss.quantidade), motivo: loss.motivo.trim() }
    })
    return { versaoEsperada: order.revisao, responsavelId: responsible, entradas: normalizedInputs, saidas: normalizedOutputs, perdas: normalizedLosses, motivo: reason.trim(), observacao: note.trim() || null, dataObservada: observed || null, evidencia: evidence.trim() || null, aptidaoNoFato: exception ? fitness : null }
  }
  const calculate = async () => {
    setError(null); setPreview(null); setAcceptance({}); setPreviewing(true)
    try {
      if (!start && (!outputs.length || outputs.every(o => zero(o.quantidade))) && !admin) throw new Error('A perda total exige confirmação por Admin. Registre a ocorrência e solicite a resolução pelo responsável autorizado.')
      const request = body(false)
      const p = parsePreview(await httpRequest<unknown>(`/ordens-producao/${order.id}/previas`, { authenticated: true, method: 'POST', body: { operacao: action, inicio: start ? request : null, confirmacao: start ? null : request } }))
      setPreview(p); requestAnimationFrame(() => heading.current?.focus())
    } catch (e) { setError(inventoryError(e)) } finally { setPreviewing(false) }
  }
  const submit = async () => {
    if (!preview) return
    setError(null)
    try {
      const result = await command.submit(order.id, action, body(true))
      if (result) complete(result)
    } catch (e) { setError(inventoryError(e)) }
  }
  const needsAcceptance = preview?.quantidades.some(q => q.quantidade.exigeAceite && (!acceptance[q.id]?.checked || !acceptance[q.id]?.reason.trim()))
  const allZeroOutput = !outputs.length || outputs.every(o => zero(o.quantidade))
  return <><Notice error={error} /><PendingCommand control={command} complete={complete} setError={setError} /><Panel><p>Ordem {order.codigo} · revisão {order.revisao} · {plan.cabecalho.codigo} — {plan.cabecalho.nome} · versão publicada {plan.receita.numero}</p>{exception && <p className="rounded-lg border border-amber-300 bg-amber-50 p-3">Registro excepcional Admin de fato já ocorrido. Todas as saídas serão bloqueadas em segregação. Status, validade e evidências serão preservados; esta ação não autoriza consumo futuro irregular.</p>}{!start && <p>Informe todos os reais. O servidor libera a sobra de cada reserva e valida atomicamente lotes substitutos ou quantidades adicionais. Saldo insuficiente preserva a ordem e suas reservas, sem execução parcial.</p>}<form onSubmit={e => e.preventDefault()}><fieldset className="space-y-6" disabled={command.busy || !!command.pending || previewing}><InputsEditor plan={plan} inputs={inputs} onChange={changeInputs} start={start} exception={exception} />{!start && <><OutputsEditor order={order} outputs={outputs} onChange={changeOutputs} exception={exception} /><LossesEditor plan={plan} losses={losses} onChange={changeLosses} />{allZeroOutput && <p className="rounded-lg border border-amber-300 bg-amber-50 p-3">Saída total zero representa perda total e exige Admin, motivo e evidência. Sem nenhum consumo físico, use o aborto sem consumo.</p>}</>}<div className={grid}><ReferenceSelect label="Responsável operacional" path="/estoque/responsaveis" value={responsible || null} params={{ ativo: true }} parse={parseResponsible} caption={r => `${r.nome}${r.ativo ? '' : ' · Inativo'}`} onChange={id => { changed(); setResponsible(id ?? '') }} />{!start && <Input label="Data observada do fato" type="date" value={observed} onChange={v => { changed(); setObserved(v) }} />}{exception && <Select label="Aptidão comprovada no fato" value={fitness} onChange={v => { changed(); setFitness(v) }} options={[[ 'Irregular', 'Consumo irregular ou sem prova de aptidão'], ['Apto', 'Consumo comprovadamente apto na data observada']]} />}</div><Input label="Motivo da operação e dos desvios" value={reason} onChange={v => { changed(); setReason(v) }} required multiline />{!start && <><Input label="Observação e diferenças não explicadas" value={note} onChange={v => { changed(); setNote(v) }} multiline hint="Não presuma equivalência nutricional; justifique diferenças de processo. Nenhuma perda de ajuste será inventada." /><Input label="Evidência do fato e da conferência física" value={evidence} onChange={v => { changed(); setEvidence(v) }} required={exception || allZeroOutput} multiline /></>}<button className={buttonClass} type="button" onClick={() => void calculate()}>{previewing ? 'Consultando prévia…' : 'Revisar quantidades e disponibilidade'}</button></fieldset></form></Panel>{preview && !command.pending && <section ref={heading} tabIndex={-1} className="min-w-0 space-y-4 rounded-xl border border-emerald-300 bg-white p-4 focus:ring-2 focus:ring-emerald-600 sm:p-6" aria-label="Prévia de produção"><h2 className="text-xl font-semibold">Revise antes de {start ? 'reservar' : 'confirmar'}</h2><p>A prévia não altera estoque nem reserva. O comando revalida os dados atuais.</p><ScrollTable label="Quantidades canônicas da produção"><thead><tr>{['Linha', 'Declarada', 'Calculada', 'Será registrada', 'Resíduo'].map(h => <th scope="col" key={h}>{h}</th>)}</tr></thead><tbody>{preview.quantidades.map(q => {
    const line = q.papel === 'Entrada' ? plan.escalonada.entradas.find(l => l.id === q.linhaId) : plan.escalonada.saidas.find(l => l.id === q.linhaId)
    return <tr key={`${q.papel}/${q.id}`}><td>{q.papel === 'Entrada' ? 'Entrada' : 'Saída'} · {line ? itemName(plan, line.itemId) : 'Linha histórica'}</td><td>{showQuantity(q.quantidade.declarada)} {q.quantidade.unidadeDeclarada}</td><td>{showQuantity(q.quantidade.calculada)} {q.quantidade.unidade}</td><td>{showQuantity(q.quantidade.normalizada)} {q.quantidade.unidade}</td><td>{showQuantity(q.quantidade.residuo)} {q.quantidade.unidade}</td></tr>
  })}</tbody></ScrollTable>{preview.avisos.length > 0 && <ul className="list-inside list-disc text-amber-900">{preview.avisos.map((warning, i) => <li key={i}>{warning}</li>)}</ul>}{preview.quantidades.filter(q => q.quantidade.exigeAceite).map((q, index) => <fieldset key={q.id} className="space-y-3 rounded-lg border border-amber-300 bg-amber-50 p-3"><legend className="px-1 font-semibold">Aceite individual de resíduo {index + 1}</legend><label className="flex items-start gap-2"><input type="checkbox" className="mt-1" checked={acceptance[q.id]?.checked ?? false} onChange={e => setAcceptance(v => ({ ...v, [q.id]: { checked: e.target.checked, reason: v[q.id]?.reason ?? '' } }))} />Aceito registrar {showQuantity(q.quantidade.normalizada)} {q.quantidade.unidade}, com resíduo {showQuantity(q.quantidade.residuo)} {q.quantidade.unidade} nesta {q.papel === 'Entrada' ? 'alocação de entrada' : 'parcela de saída'}.</label><Input label={`Motivo do aceite do resíduo ${index + 1}`} value={acceptance[q.id]?.reason ?? ''} onChange={reason => setAcceptance(v => ({ ...v, [q.id]: { checked: v[q.id]?.checked ?? false, reason } }))} required /></fieldset>)}<button className={primaryClass} type="button" disabled={command.busy || !!needsAcceptance} onClick={() => void submit()}>{actionNames[action]}</button></section>}<button className={buttonClass} type="button" onClick={() => confirmDiscard(() => navigate(`/producao/ordens/${order.id}`, { state: back }))}>Voltar à ordem</button></>
}
