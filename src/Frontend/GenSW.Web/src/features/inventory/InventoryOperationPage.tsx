import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/hooks/useAuth'
import { isHttpError } from '../../shared/http/httpErrors'
import { InventoryFrame, Panel, Input, Select, Notice, ReferenceSelect, ScrollTable, buttonClass, primaryClass } from './InventoryControls'
import { inventoryError, inventoryRead, inventorySend, parseCommittedEvent, parseEvent, parseLocal, parseLote, parsePreview, parseResponsible, parseSaldo, decimalInput, showQuantity, showTime, today } from './inventoryService'
import { useInventoryMutation } from './useInventoryMutation'
import { operationInfo, operationLabel, type Lote, type MovementCommand, type Operation, type Preview, type Versions } from './types'

const emptyVersions: Versions = { item: 0, lote: 0, local: 0, posicao: 0, origemLocal: 0, destinoLocal: 0, origemPosicao: 0, destinoPosicao: 0 }
const optional = (v: string) => v.trim() || null

export function InventoryOperationPage() {
  const { type } = useParams()
  const [params] = useSearchParams()
  return <OperationForm key={`${type}:${params.get('lote') ?? ''}`} />
}

function OperationForm() {
  const { type } = useParams()
  const [params] = useSearchParams()
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  const operation = type && Object.prototype.hasOwnProperty.call(operationInfo, type) ? type as Operation : null
  const info = operation ? operationInfo[operation] : null
  const allowed = Boolean(info && (!info.admin || admin))
  const navigate = useNavigate()
  const [form, setForm] = useState({ loteId: params.get('lote') ?? '', localId: '', origemLocalId: '', destinoLocalId: '', quantidade: '', unidade: 'kg', responsavelId: user?.userId ?? '', motivo: '', dataObservada: today(), origem: '', fonte: '', documento: '', destino: '', evidencia: '', eventoReferenciaId: '' })
  const [lot, setLot] = useState<Lote | null>(null)
  const [preview, setPreview] = useState<Preview | null>(null)
  const [command, setCommand] = useState<MovementCommand | null>(null)
  const [accepted, setAccepted] = useState(false)
  const [acceptReason, setAcceptReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [conflicted, setConflicted] = useState(false)
  const preparing = useRef(false)
  const heading = useRef<HTMLElement>(null)
  const { submit, busy, resetAttempt } = useInventoryMutation()
  useEffect(() => {
    let live = true; setLot(null)
    if (!form.loteId || !allowed) return
    void inventoryRead(`lotes/${form.loteId}`, parseLote).then(x => { if (live) { setLot(x); setForm(f => ({ ...f, unidade: x.unidade, origem: x.origem, fonte: x.fonte })) } }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [form.loteId, allowed])
  useEffect(() => { if (preview) heading.current?.focus() }, [preview])
  const set = (key: keyof typeof form, v: string) => {
    resetAttempt()
    setForm(x => ({ ...x, [key]: v })); setPreview(null); setCommand(null); setAccepted(false); setAcceptReason(''); setError(null)
  }
  const prepare = async (event?: FormEvent) => {
    event?.preventDefault()
    if (!operation || !info || !allowed || preparing.current) return
    resetAttempt()
    preparing.current = true; setLoading(true); setError(null); setPreview(null); setCommand(null); setAccepted(false); setConflicted(false)
    try {
      if (!form.loteId || !form.responsavelId || !form.motivo.trim()) throw new Error('Selecione lote, responsável e informe o motivo.')
      const lote = await inventoryRead(`lotes/${form.loteId}`, parseLote)
      setLot(lote)
      const unit = info.transfer || operation === 'ajuste' ? lote.unidade : form.unidade
      const quantity = decimalInput(form.quantidade, operation === 'ajuste')
      const originId = info.transfer ? form.origemLocalId : form.localId
      if (!originId || (info.transfer && !form.destinoLocalId)) throw new Error('Selecione os locais da operação.')
      const origin = await inventoryRead(`saldos/${lote.id}/${originId}`, parseSaldo)
      const destination = info.transfer ? await inventoryRead(`saldos/${lote.id}/${form.destinoLocalId}`, parseSaldo) : null
      const versions: Versions = { ...emptyVersions, item: origin.itemRevisao, lote: origin.loteRevisao, local: info.transfer ? 0 : origin.localRevisao, posicao: info.transfer ? 0 : origin.revisao, origemLocal: info.transfer ? origin.localRevisao : 0, origemPosicao: info.transfer ? origin.revisao : 0, destinoLocal: destination?.localRevisao ?? 0, destinoPosicao: destination?.revisao ?? 0 }
      const body: MovementCommand = { loteId: lote.id, quantidade: quantity, unidade: unit, responsavelId: form.responsavelId, motivo: form.motivo.trim(), versoesEsperadas: versions, localId: info.transfer ? null : form.localId, origemLocalId: info.transfer ? form.origemLocalId : null, destinoLocalId: info.transfer ? form.destinoLocalId : null, conversaoItemId: info.transfer || operation === 'ajuste' ? null : lote.conversaoItemId, quantidadeContada: operation === 'ajuste' ? quantity : null, aceiteQuantizacao: null, dataObservada: optional(form.dataObservada), origem: optional(form.origem), fonte: optional(form.fonte), documento: optional(form.documento), destino: optional(form.destino), evidencia: optional(form.evidencia), eventoReferenciaId: optional(form.eventoReferenciaId) }
      const result = await inventorySend('previas', { operacao: operation, comando: body }, parsePreview)
      setCommand({ ...body, versoesEsperadas: result.versoesEsperadas }); setPreview(result)
    } catch (e) { setError(inventoryError(e)) }
    finally { preparing.current = false; setLoading(false) }
  }
  const confirm = async () => {
    if (!operation || !info || !command || !preview || conflicted) return
    setError(null)
    try {
      if (preview.quantidade.exigeAceite && (!accepted || !acceptReason.trim())) throw new Error('Confira e aceite o valor canônico e o resíduo, informando o motivo.')
      const body = { ...command, aceiteQuantizacao: preview.quantidade.exigeAceite ? { calculado: preview.quantidade.calculada, normalizado: preview.quantidade.normalizada, residuo: preview.quantidade.residuo, motivo: acceptReason.trim() } : null }
      const result = await submit(info.endpoint, body, parseCommittedEvent)
      if (result) void navigate(`/estoque/movimentos/${result.id}`)
    } catch (e) { setError(inventoryError(e)); if (isHttpError(e) && (e.status === 409 || e.status === 403)) setConflicted(true) }
  }
  return <InventoryFrame title={info?.label ?? 'Operação de estoque'}>{!info ? <Notice error="Operação não encontrada." /> : !allowed ? <Notice error="Esta operação exige o papel Admin. Nenhuma prévia ou comando foi enviado." /> : <>
    <Notice error={error} />{error && <p className="text-sm text-slate-600">Se a resposta da confirmação se perdeu, tente Confirmar novamente com os mesmos dados. Em conflito, recarregue os dados e revise uma nova prévia.</p>}
    <Panel title="Apontamento físico"><form className="space-y-4" onSubmit={prepare}><fieldset disabled={busy || loading} className="grid min-w-0 gap-4 sm:grid-cols-2">
      <ReferenceSelect label="Lote" path="/estoque/lotes" value={form.loteId || null} params={{ ...(operation === 'entrada' ? { ativo: true } : {}), ...(operation === 'consumo-interno' || operation === 'saida-manual' || operation === 'transferencia' ? { ativo: true, situacao: 'Liberado' } : {}) }} parse={parseLote} caption={x => `${x.codigo} — ${x.itemNome} · ${x.situacao}${x.ativo ? '' : ' · Inativo'}`} onChange={v => set('loteId', v ?? '')} />
      {info.transfer ? <><ReferenceSelect label="Local de origem" path="/estoque/locais" value={form.origemLocalId || null} parse={parseLocal} caption={x => `${x.codigo} — ${x.nome}${x.ativo ? '' : ' · Inativo'}`} onChange={v => set('origemLocalId', v ?? '')} /><ReferenceSelect label="Local de destino" path="/estoque/locais" value={form.destinoLocalId || null} params={{ ativo: true, finalidade: operation === 'segregacao' ? 'Segregacao' : 'Ordinario' }} parse={parseLocal} caption={x => `${x.codigo} — ${x.nome}`} onChange={v => set('destinoLocalId', v ?? '')} /></> : <ReferenceSelect label="Local" path="/estoque/locais" value={form.localId || null} params={operation === 'ajuste' || operation === 'descarte' ? {} : { ativo: true }} parse={parseLocal} caption={x => `${x.codigo} — ${x.nome}${x.ativo ? '' : ' · Inativo'}`} onChange={v => set('localId', v ?? '')} />}
      <Input label={operation === 'ajuste' ? 'Quantidade contada (alvo)' : 'Quantidade declarada'} value={form.quantidade} onChange={v => set('quantidade', v)} required hint="Até seis casas; nenhum saldo ou arredondamento é calculado no navegador." />
      <Select label="Unidade declarada" value={info.transfer || operation === 'ajuste' ? lot?.unidade ?? form.unidade : form.unidade} disabled={info.transfer || operation === 'ajuste'} onChange={v => set('unidade', v)} options={[[ 'kg', 'kg'], ['g', 'g'], ['L', 'L'], ['mL', 'mL'], ['un', 'un']]} />
      <ReferenceSelect label="Responsável" path="/estoque/responsaveis" value={form.responsavelId || null} params={{ ativo: true }} parse={parseResponsible} caption={x => `${x.nome}${x.ativo ? '' : ' · Inativo'}`} onChange={v => set('responsavelId', v ?? '')} />
      <Input label="Motivo" value={form.motivo} onChange={v => set('motivo', v)} required multiline /><Input label="Data observada ou da conferência" value={form.dataObservada} type="date" onChange={v => set('dataObservada', v)} required={['entrada', 'abertura', 'ajuste'].includes(operation ?? '')} />
      {operation === 'entrada' && <><Input label="Origem da entrada" value={form.origem} onChange={v => set('origem', v)} required maxLength={1000} /><Input label="Fonte da entrada" value={form.fonte} onChange={v => set('fonte', v)} required maxLength={1000} /></>}
      {operation === 'saida-manual' && <Input label="Destino e finalidade da saída" value={form.destino} onChange={v => set('destino', v)} required maxLength={1000} />}
      {info.admin && <Input label="Evidência e declaração da conferência física" value={form.evidencia} onChange={v => set('evidencia', v)} required multiline />}
      <Input label="Documento ou referência textual" value={form.documento} onChange={v => set('documento', v)} /><ReferenceSelect label="Evento anterior referenciado (opcional)" searchPlaceholder="Motivo ou documento" path="/estoque/movimentos" value={form.eventoReferenciaId || null} params={{ sortBy: 'sequencia', sortDirection: 'desc' }} parse={parseEvent} caption={x => `Sequência ${x.sequencia} · ${operationLabel(x.tipo)} · ${x.dataOperacional}`} onChange={v => set('eventoReferenciaId', v ?? '')} />
    </fieldset>{lot && <p className="text-sm text-slate-600">Lote {lot.codigo} · {lot.situacao} · unidade canônica {lot.unidade} · validade {lot.validade ?? 'não informada'}. {lot.conversaoItemId ? 'Será usada somente a conversão documental vinculada ao lote quando aplicável.' : 'Não há conversão entre grandezas vinculada.'}</p>}{operation === 'ajuste' && <p>A contagem alvo será comparada ao saldo atual pelo servidor. Este ajuste não representa produção.</p>}{operation === 'segregacao' && <p>A operação transfere a quantidade e bloqueia o lote inteiro, inclusive suas outras posições.</p>}{operation === 'descarte' && <p>Saída excepcional limitada ao saldo real, sem venda ou lançamento financeiro.</p>}<button className={buttonClass} type="submit" disabled={busy || loading}>{loading ? 'Calculando…' : preview ? 'Recarregar dados e nova prévia' : 'Calcular prévia'}</button></form></Panel>
    {preview && <section ref={heading} tabIndex={-1} className="min-w-0 rounded-2xl border border-emerald-300 bg-white p-4 focus:ring-2 focus:ring-emerald-600 sm:p-6" aria-label="Prévia da operação"><h2 className="text-xl font-semibold">Revise antes de confirmar</h2><p className="mt-2 text-sm text-slate-600">Observado em {showTime(preview.observadoEmUtc)} · sequência {preview.sequenciaAte}. A prévia não reserva nem garante disponibilidade futura.</p><dl className="my-5 grid min-w-0 gap-3 break-words sm:grid-cols-2 [&_div]:min-w-0"><div><dt className="font-medium">Declarado</dt><dd>{showQuantity(preview.quantidade.declarada)} {preview.quantidade.unidadeDeclarada}</dd></div><div><dt className="font-medium">Valor canônico calculado</dt><dd>{showQuantity(preview.quantidade.calculada)} {preview.unidade}</dd></div><div><dt className="font-medium">Quantidade que será registrada{operation === 'ajuste' ? ' (delta absoluto)' : ''}</dt><dd>{showQuantity(preview.quantidade.normalizada)} {preview.unidade}</dd></div><div><dt className="font-medium">Resíduo (calculado − registrado)</dt><dd>{showQuantity(preview.quantidade.residuo)} {preview.unidade}</dd></div>{preview.quantidade.fator && <div><dt className="font-medium">Conversão / fonte</dt><dd>{preview.quantidade.sentido} · fator {showQuantity(preview.quantidade.fator)} · {preview.quantidade.proveniencia}</dd></div>}</dl><ScrollTable label="Saldos previstos"><thead><tr><th scope="col">Local</th><th scope="col">Saldo observado</th><th scope="col">Saldo após ação</th></tr></thead><tbody>{preview.posicoes.map(position => <tr key={position.localId}><td>{position.localId === form.destinoLocalId ? 'Destino' : info.transfer ? 'Origem' : 'Local selecionado'}</td><td>{showQuantity(position.saldoAnterior)} {preview.unidade}</td><td>{showQuantity(position.saldoPosterior)} {preview.unidade}</td></tr>)}</tbody></ScrollTable>{preview.avisos.length > 0 && <ul className="my-4 list-inside list-disc text-amber-900">{preview.avisos.map((warning, i) => <li key={i}>{warning}</li>)}</ul>}{preview.quantidade.exigeAceite && <div className="my-4 space-y-3 rounded-lg border border-amber-300 bg-amber-50 p-3"><label className="flex items-start gap-2"><input className="mt-1" type="checkbox" checked={accepted} onChange={e => { resetAttempt(); setAccepted(e.target.checked) }} disabled={busy} />Aceito registrar {showQuantity(preview.quantidade.normalizada)} {preview.unidade} e o resíduo {showQuantity(preview.quantidade.residuo)} {preview.unidade} apresentados.</label><Input label="Motivo do aceite do resíduo" value={acceptReason} onChange={v => { resetAttempt(); setAcceptReason(v) }} required disabled={busy} /></div>}<div className="mt-4 flex flex-wrap gap-3"><button className={primaryClass} type="button" disabled={busy || conflicted || (preview.quantidade.exigeAceite && (!accepted || !acceptReason.trim()))} onClick={confirm}>{busy ? 'Confirmando…' : 'Confirmar movimento'}</button><Link className={buttonClass} to="/estoque/saldos">Voltar para Saldos</Link></div></section>}
  </>}</InventoryFrame>
}
