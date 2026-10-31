import { useEffect, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { useAuth } from '../auth/hooks/useAuth'
import { buttonClass, Input, Notice, Panel, primaryClass, ReferenceSelect, Select } from '../inventory/InventoryControls'
import { inventoryError, parseEvent, parseItem, parseResponsible, showTime } from '../inventory/inventoryService'
import type { Event } from '../inventory/types'
import { EggBridgeFrame, EggCommandReceipt, EggRecovery, EggSourceLabel } from './EggBridgeControls'
import { EggCandidateSelection, type EggSelected } from './EggCandidatesPage'
import { eggRead, integerCount, parseEggAnchor, parseEggCandidate } from './eggBridgeService'
import { useEggBridgeCommand } from './useEggBridgeCommand'
import type { EggAnchor, EggCommandResult, EggDecision } from './types'

const grid = 'grid min-w-0 gap-4 sm:grid-cols-2'
const decisions: readonly (readonly [string, string])[] = [['', 'Escolha após conferir'], ['NaoRecebidoComprovado', 'Não recebido · comprovado'], ['RecebidoComEquivalencia', 'Recebido · equivalência comprovada'], ['Pendente', 'Pendente · sem autorização de ingresso']]
function initialSelection(state: unknown): EggSelected {
  if (!state || typeof state !== 'object' || !('selected' in state) || !Array.isArray(state.selected)) return {}
  try { return Object.fromEntries(state.selected.map(value => { const candidate = parseEggCandidate(value); return [candidate.origem.id, candidate] })) } catch { return {} }
}
export function EggAdministrationPage() {
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  const location = useLocation()
  const [selected, setSelected] = useState<EggSelected>(() => initialSelection(location.state))
  const [itemId, setItemId] = useState('')
  const [anchor, setAnchor] = useState<EggAnchor | null>(null)
  const [anchorChecked, setAnchorChecked] = useState(false)
  const [reload, setReload] = useState(0)
  const [tab, setTab] = useState('recorte')
  const [enrollmentReason, setEnrollmentReason] = useState('')
  const [scope, setScope] = useState({ fronteira: '', fontesConsultadas: '', lacunas: '', responsavelId: user?.userId ?? '', motivo: '' })
  const [rows, setRows] = useState<Record<string, { decisao: EggDecision | ''; individualComprovado: boolean; evidencia: string }>>({})
  const [proof, setProof] = useState({ movimentoEstoqueId: '', contagem: '', responsavelId: user?.userId ?? '', motivo: '', evidencia: '' })
  const [event, setEvent] = useState<Event | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<EggCommandResult | null>(null)
  const command = useEggBridgeCommand()
  const locked = command.busy || !!command.pending
  useEffect(() => {
    if (!admin || !itemId) { setAnchor(null); setAnchorChecked(false); return }
    let live = true; setAnchor(null); setAnchorChecked(false)
    void eggRead(`/itens/${itemId}`, parseEggAnchor).then(value => { if (live) { setAnchor(value); setAnchorChecked(true) } }).catch(e => {
      if (!live) return
      if (e && typeof e === 'object' && 'status' in e && e.status === 404) setAnchorChecked(true)
      else setError(inventoryError(e))
    })
    return () => { live = false }
  }, [admin, itemId, reload])
  const complete = (value: EggCommandResult) => { setResult(value); setReload(v => v + 1) }
  const enroll = async () => {
    setError(null); setResult(null)
    try {
      if (!itemId || !enrollmentReason.trim()) throw new Error('Selecione o item e informe o motivo da inscrição.')
      const value = await command.submit('Inscricao', '/itens', { itemId, motivo: enrollmentReason.trim() }); if (value) complete(value)
    } catch (e) { setError(inventoryError(e)) }
  }
  const saveScope = async () => {
    setError(null); setResult(null)
    try {
      if (!anchor || !scope.fronteira.trim() || !scope.fontesConsultadas.trim() || !scope.motivo.trim() || !scope.responsavelId) throw new Error('Selecione item inscrito e responsável e descreva fronteira, fontes consultadas e motivo.')
      const decisionRows = Object.keys(selected).map(id => {
        const row = rows[id]
        if (!row?.decisao || !row.evidencia.trim()) throw new Error('Cada lançamento selecionado exige decisão explícita e evidência. Não há decisão padrão.')
        if (row.decisao !== 'Pendente' && !row.individualComprovado) throw new Error('Só um ovo individual comprovado pode receber decisão de ingresso ou associação.')
        return { producaoOvoId: id, decisao: row.decisao, individualComprovado: row.individualComprovado, evidencia: row.evidencia.trim() }
      })
      const value = await command.submit('Recorte', '/recortes', { itemId, ...scope, decisoes: decisionRows }); if (value) complete(value)
    } catch (e) { setError(inventoryError(e)) }
  }
  const saveProof = async () => {
    setError(null); setResult(null)
    try {
      const movement = event?.movimentos.find(row => row.id === proof.movimentoEstoqueId)
      if (!event || !['Entrada', 'Abertura'].includes(event.tipo) || !movement || movement.unidade !== 'un' || movement.unidadeDeclarada !== 'un' || movement.sentido !== 'Entrada') throw new Error('Escolha Entrada/Abertura manual original declarada em un. Peso convertido não comprova contagem.')
      if (!proof.responsavelId || !proof.motivo.trim() || !proof.evidencia.trim()) throw new Error('Informe responsável, motivo e prova da contagem histórica.')
      const value = await command.submit('Prova', '/provas', { ...proof, contagem: integerCount(proof.contagem) }); if (value) complete(value)
    } catch (e) { setError(inventoryError(e)) }
  }
  const rowChange = (id: string, patch: Partial<{ decisao: EggDecision | ''; individualComprovado: boolean; evidencia: string }>) => setRows(values => ({ ...values, [id]: { ...(values[id] ?? { decisao: '', individualComprovado: false, evidencia: '' }), ...patch } }))
  return <EggBridgeFrame title="Conferências e provas de ovos">{!admin ? <Notice error="Esta operação exige o papel Admin atual." /> : <><Notice error={error} retry={() => setReload(v => v + 1)} /><EggRecovery control={command} complete={complete} setError={setError} /><EggCommandReceipt result={result} /><Panel><nav aria-label="Preparação F03" className="flex flex-wrap gap-3"><button type="button" className={buttonClass} aria-pressed={tab === 'recorte'} onClick={() => setTab('recorte')}>Inscrição e conferência</button><button type="button" className={buttonClass} aria-pressed={tab === 'prova'} onClick={() => setTab('prova')}>Prova de entrada manual</button></nav></Panel>{tab === 'recorte' ? <><Panel title="Item explicitamente gerido"><fieldset disabled={locked} className="space-y-4"><ReferenceSelect label="Item a conferir" path="/itens" value={itemId || null} parse={parseItem} caption={item => `${item.codigo} — ${item.nome} · ${item.unidade}`} onChange={id => { setItemId(id ?? ''); setResult(null) }} />{anchor ? <p>Item inscrito · revisão atual {anchor.revisao}. Cada crédito relevante invalida a conferência anterior.</p> : itemId && anchorChecked ? <><p>Este item ainda não está inscrito. A inscrição passa a exigir origem comprovada nos recebimentos F01 geridos.</p><Input label="Motivo da inscrição F03" value={enrollmentReason} onChange={setEnrollmentReason} multiline required /><button type="button" className={primaryClass} onClick={() => void enroll()}>Inscrever item na gestão F03</button></> : itemId ? <p role="status">Consultando inscrição…</p> : null}</fieldset></Panel><EggCandidateSelection selected={selected} onChange={setSelected} disabled={locked} /><Panel title="Conferência humana do recorte"><p>Descreva o acervo físico e externo consultado. Uma base F01 vazia não comprova ausência de estoque. Cadastros coletivos e amostras ficam pendentes nesta ponte individual.</p><form onSubmit={e => e.preventDefault()} className="space-y-4"><fieldset disabled={locked} className={grid}><Input label="Fronteira física, período e controle documental" value={scope.fronteira} onChange={v => setScope(s => ({ ...s, fronteira: v }))} multiline required /><Input label="Fontes e registros efetivamente consultados" value={scope.fontesConsultadas} onChange={v => setScope(s => ({ ...s, fontesConsultadas: v }))} multiline required /><Input label="Lacunas e limites da conferência" value={scope.lacunas} onChange={v => setScope(s => ({ ...s, lacunas: v }))} multiline /><ReferenceSelect label="Responsável pela conferência" path="/estoque/responsaveis" value={scope.responsavelId || null} params={{ ativo: true }} parse={parseResponsible} caption={person => person.nome} onChange={id => setScope(s => ({ ...s, responsavelId: id ?? '' }))} /><Input label="Motivo da aprovação do recorte" value={scope.motivo} onChange={v => setScope(s => ({ ...s, motivo: v }))} multiline required /></fieldset>{Object.values(selected).map(candidate => <fieldset disabled={locked} key={candidate.origem.id} className="min-w-0 space-y-3 rounded-lg border p-4"><legend className="font-semibold">Decisão do lançamento</legend><EggSourceLabel source={candidate.origem} /><Select label={`Decisão · ${candidate.origem.id}`} value={rows[candidate.origem.id]?.decisao ?? ''} options={decisions} onChange={v => rowChange(candidate.origem.id, { decisao: v as EggDecision | '' })} /><label className="flex items-start gap-3"><input type="checkbox" className="mt-1" checked={rows[candidate.origem.id]?.individualComprovado ?? false} onChange={e => rowChange(candidate.origem.id, { individualComprovado: e.target.checked })} /><span>Comprovei que este lançamento representa um ovo individual.</span></label><Input label={`Evidência da decisão · ${candidate.origem.id}`} value={rows[candidate.origem.id]?.evidencia ?? ''} onChange={v => rowChange(candidate.origem.id, { evidencia: v })} multiline required /></fieldset>)}<button type="button" className={primaryClass} disabled={locked || !anchor} onClick={() => void saveScope()}>Aprovar conferência e decisões explícitas</button></form></Panel></> : <Panel title="Prova da entrada manual original"><form onSubmit={e => e.preventDefault()} className="space-y-4"><fieldset disabled={locked} className={grid}><ReferenceSelect label="Entrada ou abertura original" path="/estoque/movimentos" value={event?.id ?? null} parse={parseEvent} caption={row => `Sequência ${row.sequencia} · ${row.tipo} · ${row.dataOperacional} · ${row.motivo}`} onChange={(_, row) => { setEvent(row ?? null); setProof(p => ({ ...p, movimentoEstoqueId: '' })) }} /><Select label="Movimento original comprovado" value={proof.movimentoEstoqueId} options={[[ '', 'Selecione o movimento em un'], ...(event?.movimentos ?? []).map(movement => [movement.id, `${movement.quantidadeDeclarada} ${movement.unidadeDeclarada} · ${movement.sentido} · ${movement.quantidade} ${movement.unidade}`] as const)]} onChange={v => setProof(p => ({ ...p, movimentoEstoqueId: v }))} /><Input label="Contagem histórica comprovada em un" value={proof.contagem} onChange={v => setProof(p => ({ ...p, contagem: v }))} required hint="Inteiro positivo até a contagem original, sem conversão de kg. A capacidade é histórica, não o saldo atual." /><ReferenceSelect label="Responsável pela prova" path="/estoque/responsaveis" value={proof.responsavelId || null} params={{ ativo: true }} parse={parseResponsible} caption={person => person.nome} onChange={id => setProof(p => ({ ...p, responsavelId: id ?? '' }))} /><Input label="Motivo da prova manual" value={proof.motivo} onChange={v => setProof(p => ({ ...p, motivo: v }))} multiline required /><Input label="Documento, contagem e evidência da equivalência" value={proof.evidencia} onChange={v => setProof(p => ({ ...p, evidencia: v }))} multiline required /></fieldset>{event && <p>Fato original em {showTime(event.createdAtUtc)}. Mesmo item, data, peso ou saldo não provam equivalência. Créditos raiz e lotes F03 não são recebimentos manuais elegíveis.</p>}<button type="button" className={primaryClass} disabled={locked} onClick={() => void saveProof()}>Registrar prova histórica sem movimentar saldo</button></form></Panel>}</>}</EggBridgeFrame>
}
