import { useEffect, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../../shared/layout/PageHeader'
import { useAuth } from '../auth/hooks/useAuth'
import { getAnimalById } from '../animals/services/animalsService'
import { buttonClass, Panel } from '../inventory/InventoryControls'
import { inventoryError, showTime } from '../inventory/inventoryService'
import { eggRoot } from './eggBridgeService'
import type { EggCommandOperation, EggCommandResult, EggSource } from './types'
import type { EggCommandControl } from './useEggBridgeCommand'

export function EggBridgeFrame({ title, children }: { title: string; children: ReactNode }) {
  const { user } = useAuth()
  return <main className="gensw-page min-w-0 px-4 py-6 sm:px-6"><div className="mx-auto max-w-6xl space-y-6"><PageHeader title={title} context="Produção animal · ponte de ovos" /><nav aria-label="Ponte de ovos" className="flex flex-wrap gap-2"><Link className={buttonClass} to={eggRoot}>Selecionar lançamentos</Link><Link className={buttonClass} to={`${eggRoot}/grupos`}>Grupos e histórico</Link>{user?.roles.includes('Admin') && <><Link className={buttonClass} to={`${eggRoot}/administracao`}>Conferências e provas</Link><Link className={buttonClass} to={`${eggRoot}/conciliacao`}>Conciliação</Link></>}</nav>{children}</div></main>
}
export function EggSourceLabel({ source }: { source: Pick<EggSource, 'animalId' | 'dataPostura' | 'id'> }) {
  const [label, setLabel] = useState<string | null>(null)
  useEffect(() => {
    let live = true
    void getAnimalById(source.animalId).then(animal => { if (live) setLabel(`${animal.codigoInterno}${animal.nome ? ` — ${animal.nome}` : ''}`) }).catch(() => { if (live) setLabel(null) })
    return () => { live = false }
  }, [source.animalId])
  return <span className="break-words"><Link className="text-emerald-700 underline" to={`/animais/${source.animalId}/producoes-ovos/${source.id}`}>{label ?? 'Consultar animal e lançamento'} · postura {source.dataPostura}</Link></span>
}
export function EggRecovery({ control, complete, setError }: { control: EggCommandControl; complete: (result: EggCommandResult) => void; setError: (error: string | null) => void }) {
  if (control.recoveryError) return <Panel title="Pedido pendente indisponível"><p role="alert">{control.recoveryError}</p></Panel>
  if (!control.pending) return null
  const run = async (action: 'retry' | 'consult') => {
    setError(null)
    try { const result = await control[action](); if (result) complete(result) } catch (error) { setError(inventoryError(error)) }
  }
  return <Panel title="Pedido F03 aguardando confirmação"><p role="status">O pedido original de {control.pending.operation} foi preservado. Consulte ou repita os mesmos dados antes de editar.</p><div className="flex flex-wrap gap-3"><button type="button" className={buttonClass} disabled={control.busy} onClick={() => void run('consult')}>Consultar resultado original</button><button type="button" className={buttonClass} disabled={control.busy} onClick={() => void run('retry')}>Repetir pedido original</button></div>{control.absent && <><p>{control.releaseAllowed ? 'O pedido foi recusado e a consulta não encontrou resultado registrado. É possível liberar a edição conscientemente para obter nova prévia.' : 'Resultado ainda não encontrado. O pedido pode continuar em processamento; mantenha a chave e os dados originais.'}</p>{control.releaseAllowed && <button type="button" className={buttonClass} onClick={control.release}>Liberar edição após consulta</button>}</>}</Panel>
}
export function EggCommandReceipt({ result }: { result: EggCommandResult | null }) {
  if (!result) return null
  return <Panel title="Resultado original do comando"><p role="status">Pedido confirmado. Este resultado preserva o fato original.</p>{result.kind === 'grupo' ? <GroupCommandReceipt result={result} /> : result.kind === 'inscricao' ? <p>Item inscrito para gestão pela F03 · revisão {result.anchor.revisao}.</p> : result.kind === 'recorte' ? <p>Conferência registrada · revisão {result.scope.revisaoAncora} · corte do livro {result.scope.sequenciaAte}.</p> : <p>Prova manual registrada · contagem histórica comprovada de {result.proof.contagem} un.</p>}</Panel>
}
const commandLabels: Record<EggCommandOperation, string> = { Entrada: 'Entrada individual confirmada', Associacao: 'Associação histórica confirmada', Compensacao: 'Compensação integral confirmada', Invalidacao: 'Invalidação documental confirmada', Anotacao: 'Anotação documental registrada', Inscricao: 'Inscrição confirmada', Recorte: 'Conferência aprovada', Prova: 'Prova registrada' }
function GroupCommandReceipt({ result }: { result: Extract<EggCommandResult, { kind: 'grupo' }> }) {
  const group = result.detail.grupo
  const delta = result.operation === 'Entrada' ? `+${group.quantidade}` : result.operation === 'Compensacao' ? `-${group.quantidade}` : '0'
  const treatments = result.detail.tratamentos.filter(fact => fact.tipo === result.operation)
  const treatment = treatments[treatments.length - 1]
  return <><p className="font-semibold">{commandLabels[result.operation]} · {showTime(treatment?.createdAtUtc ?? group.createdAtUtc)}</p><p>Delta de estoque do comando: {delta} un.</p><p>Grupo original preservado: {group.quantidade} un · registro de {showTime(group.createdAtUtc)}.</p>{result.operation === 'Compensacao' && <p>O débito compensatório não libera os IDs para reentrada.</p>}{result.operation === 'Invalidacao' && <p>A invalidação documental não debita o recebimento manual nem libera os ordinais.</p>}{result.operation === 'Associacao' && <p>Nenhum incremento de estoque.</p>}<Link className={buttonClass} to={`${eggRoot}/grupos/${group.id}`}>Consultar estado atual do grupo</Link></>
}
