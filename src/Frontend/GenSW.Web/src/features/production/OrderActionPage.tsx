import { actionNames } from './productionActions'
import { useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../auth/hooks/useAuth'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { listReturnState, savedRecordState } from '../../shared/details/listNavigation'
import { useUnsavedChanges } from '../../shared/navigation/useUnsavedChanges'
import { buttonClass, Input, Notice, Panel, primaryClass, Select } from '../inventory/InventoryControls'
import { inventoryError, today } from '../inventory/inventoryService'
import { PendingCommand, ProductionFrame } from './ProductionControls'
import { readOrder } from './productionService'
import { ProductionExecutionForm } from './ProductionExecutionForm'
import { hasProductionRetryContext, useProductionCommand } from './useProductionCommand'
import type { ProductionOrder } from './types'

const adminActions = ['confirmacao-ocorrencia', 'aborto-sem-consumo', 'reversao']
const executable = ['planejamento', 'revisao-planejamento', 'cancelamento', 'inicio', 'confirmacao', 'confirmacao-ocorrencia', 'aborto-sem-consumo', 'reversao', 'ocorrencias']
export function OrderActionPage() {
  const { id, action = '' } = useParams()
  return <OrderAction key={`${id}/${action}`} id={id} action={action} />
}
function OrderAction({ id, action }: { id?: string; action: string }) {
  const { user } = useAuth()
  const denied = adminActions.includes(action) && !user?.roles.includes('Admin')
  const { record: order, state, retry } = useRecordDetails(!denied && executable.includes(action) ? id : undefined, readOrder)
  const location = useLocation()
  return <ProductionFrame title={actionNames[action] ?? 'Ação da ordem'}>{denied ? <Notice error="Esta operação exige o papel Admin atual." /> : !executable.includes(action) ? <Notice error="Ação de produção não encontrada." /> : <>{state === 'loading' && <p role="status">Carregando ordem…</p>}{state !== 'ready' && state !== 'loading' && <Notice error={state === 'not-found' ? 'Ordem não encontrada.' : state === 'forbidden' ? 'Acesso não permitido.' : 'Não foi possível consultar a ordem.'} retry={retry} />}{order && (!order.acoesPermitidas.includes(action) && !hasProductionRetryContext(order.id) ? <Panel><Notice error="A ação está impedida no estado atual. Confira a ordem e os motivos antes de prosseguir." />{order.impedimentos.length > 0 && <ul className="list-inside list-disc">{order.impedimentos.map((reason, i) => <li key={i}>{reason}</li>)}</ul>}<Link className={buttonClass} to={`/producao/ordens/${order.id}`} state={location.state}>Voltar à ordem</Link></Panel> : ['inicio', 'confirmacao', 'confirmacao-ocorrencia'].includes(action) ? order.planejamento ? <ProductionExecutionForm order={order} action={action as 'inicio' | 'confirmacao' | 'confirmacao-ocorrencia'} /> : <Notice error="O snapshot do planejamento não está disponível. Reconsulte a ordem." /> : <StateForm order={order} action={action} />)}</>}</ProductionFrame>
}

function StateForm({ order, action }: { order: ProductionOrder; action: string }) {
  const command = useProductionCommand(order.id)
  const pendingBody = command.pending?.operation === action ? command.pending.body as Record<string, unknown> : null
  const [reason, setReason] = useState(String(pendingBody?.motivo ?? ''))
  const [evidence, setEvidence] = useState(String(pendingBody?.evidencia ?? ''))
  const [confirmed, setConfirmed] = useState(pendingBody?.nenhumConsumoFisico === true || pendingBody?.coerenciaFisicaConfirmada === true)
  const [consumed, setConsumed] = useState(pendingBody?.houveConsumoFisico === true)
  const [kind, setKind] = useState(String(pendingBody?.tipo ?? 'Anotacao'))
  const [observed, setObserved] = useState(String(pendingBody?.dataObservada ?? today()))
  const [error, setError] = useState<string | null>(null)
  const [dirty, setDirty] = useState(false)
  const location = useLocation()
  const navigate = useNavigate()
  const { markSaved, confirmDiscard } = useUnsavedChanges(dirty || !!command.pending)
  const back = listReturnState(location.state, '/producao/ordens')
  const complete = (result: ProductionOrder) => { markSaved(); setDirty(false); navigate(`/producao/ordens/${result.id}`, { state: savedRecordState(back, `/producao/ordens/${result.id}`) }) }
  const send = async () => {
    setError(null)
    try {
      if (!reason.trim()) throw new Error('Informe o motivo da operação.')
      if (['aborto-sem-consumo', 'reversao'].includes(action) && (!confirmed || !evidence.trim())) throw new Error('Confirme a declaração e informe a evidência da conferência física.')
      if (action === 'ocorrencias' && !evidence.trim()) throw new Error('Informe a evidência ou referência observada da ocorrência.')
      const base = { versaoEsperada: order.revisao, motivo: reason.trim() }
      const body = action === 'aborto-sem-consumo' ? { ...base, nenhumConsumoFisico: confirmed, evidencia: evidence.trim() } : action === 'reversao' ? { ...base, coerenciaFisicaConfirmada: confirmed, evidencia: evidence.trim() } : action === 'ocorrencias' ? { ...base, tipo: kind, evidencia: evidence.trim(), dataObservada: observed || null, houveConsumoFisico: consumed } : base
      const result = await command.submit(order.id, action, body)
      if (result) complete(result)
    } catch (e) { setError(inventoryError(e)) }
  }
  return <><Notice error={error} /><PendingCommand control={command} complete={complete} setError={setError} /><Panel><p>Ordem {order.codigo} · {order.estado} · revisão {order.revisao}</p>{action === 'planejamento' && <p>O planejamento congela a versão publicada exata, escala, rótulos, perfis e conversões. Nenhum saldo será movimentado ou reservado.</p>}{action === 'revisao-planejamento' && <p>Esta ação preserva o snapshot anterior e devolve a ordem ao rascunho. Um novo planejamento explícito será necessário antes de iniciar.</p>}{action === 'cancelamento' && <p>O cancelamento é terminal e somente está disponível antes do início. Não apaga o histórico.</p>}{action === 'aborto-sem-consumo' && <p className="rounded-lg border border-amber-300 bg-amber-50 p-3">Declare somente quando nenhum material tiver sido fisicamente consumido. Havendo consumo, informe os reais/perdas na confirmação. O aborto libera reservas uma única vez e encerra a ordem.</p>}{action === 'reversao' && <div className="space-y-3 rounded-lg border border-amber-300 bg-amber-50 p-3"><p>Reversão corrige um lançamento confirmado incorreto. Exige saídas integralmente no local original, contagem coerente e nenhuma reserva, movimento ou uso posterior, mesmo devolvido ou liberado. Mudança posterior de status/validade também impede; fonte Encerrada não será reaberta.</p><p>A compensação é integral. Se houver impedimento, registre anotação e ajuste presente vinculado. Nunca use a reversão para recriar material realmente transformado ou perdido.</p></div>}{action === 'ocorrencias' && <p>A ocorrência preserva fatos e evidências. Interrupção não libera reservas nem cancela material consumido.</p>}<form onSubmit={e => e.preventDefault()}><fieldset disabled={command.busy || !!command.pending} className="space-y-4">{action === 'ocorrencias' && <><Select label="Tipo de ocorrência" value={kind} onChange={v => { setDirty(true); setKind(v) }} options={[[ 'Anotacao', 'Anotação'], ['Interrupcao', 'Interrupção'], ['Desvio', 'Desvio'], ['Falta', 'Falta'], ['Perda', 'Perda']]} /><Input label="Data observada da ocorrência" value={observed} type="date" onChange={v => { setDirty(true); setObserved(v) }} /><label className="flex items-start gap-2"><input className="mt-1" type="checkbox" checked={consumed} onChange={e => { setDirty(true); setConsumed(e.target.checked) }} />Houve consumo físico nesta ocorrência; o aborto sem consumo ficará impedido.</label></>}<Input label="Motivo" value={reason} onChange={v => { setDirty(true); setReason(v) }} required multiline />{['ocorrencias', 'aborto-sem-consumo', 'reversao'].includes(action) && <Input label="Evidência e conferência física" value={evidence} onChange={v => { setDirty(true); setEvidence(v) }} required multiline />}{['aborto-sem-consumo', 'reversao'].includes(action) && <label className="flex items-start gap-2"><input type="checkbox" className="mt-1" checked={confirmed} onChange={e => { setDirty(true); setConfirmed(e.target.checked) }} />{action === 'aborto-sem-consumo' ? 'Declaro que não houve nenhum consumo físico e assumo a responsabilidade por esta evidência.' : 'Confirmo que o lançamento original está incorreto e a compensação proposta corresponde à contagem física das entradas e saídas.'}</label>}<button className={primaryClass} type="button" onClick={() => void send()}>{actionNames[action]}</button></fieldset></form></Panel><button className={buttonClass} type="button" onClick={() => confirmDiscard(() => navigate(`/producao/ordens/${order.id}`, { state: back }))}>Voltar à ordem</button></>
}
