import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { parseAnimal } from '../animals/services/animalsContractParsers'
import { useAuth } from '../auth/hooks/useAuth'
import { buttonClass, Input, Notice, Pagination, Panel, primaryClass, ReferenceSelect, ScrollTable, Select } from '../inventory/InventoryControls'
import { inventoryError, parseItem, parseLocal, parseLote, parseResponsible, showTime } from '../inventory/inventoryService'
import { useRememberListState, useRestoredListState } from '../../shared/details/listNavigation'
import { useUnsavedChanges } from '../../shared/navigation/useUnsavedChanges'
import { EggBridgeFrame, EggCommandReceipt, EggRecovery, EggSourceLabel } from './EggBridgeControls'
import { eggApi, eggList, eggPreview, eggRoot, integerCount, parseEggCandidate, parseEggPreview, parseEggProof, parseEggScope } from './eggBridgeService'
import { useEggBridgeCommand } from './useEggBridgeCommand'
import type { EggCandidate, EggCommandResult, EggOperation, EggPage, EggPreview, EggPreviewCommand } from './types'

export type EggSelected = Record<string, EggCandidate>
export function EggCandidateSelection({ selected, onChange, disabled = false, initialAnimal = '' }: { selected: EggSelected; onChange: (value: EggSelected) => void; disabled?: boolean; initialAnimal?: string }) {
  const initial = useRestoredListState(eggRoot, { page: 1, estado: 'SemPonte', animalId: initialAnimal })
  const [filters, setFilters] = useState(initial)
  const navigation = useRememberListState(eggRoot, filters)
  const [data, setData] = useState<EggPage<EggCandidate> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true; setData(null); setError(null)
    void eggList('/candidatos', { ...filters, pageSize: 25 }, parseEggCandidate).then(result => { if (live) setData(result) }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [filters, retry])
  const toggle = (candidate: EggCandidate) => {
    const next = { ...selected }
    if (next[candidate.origem.id]) delete next[candidate.origem.id]
    else {
      if (Object.keys(next).length >= 100) { setError('Selecione até 100 lançamentos por conferência ou confirmação. Prepare outro grupo para os demais registros.'); return }
      next[candidate.origem.id] = candidate
    }
    onChange(next)
  }
  return <Panel title="Seleção explícita de lançamentos"><p>Escolha cada registro após conferir sua origem. Os filtros não selecionam lançamentos. Uma linha zootécnica ou seu peso não comprova um ovo físico.</p><fieldset disabled={disabled} className="grid min-w-0 gap-4 sm:grid-cols-2"><ReferenceSelect label="Animal para filtrar" path="/animais" value={filters.animalId || null} parse={parseAnimal} caption={animal => `${animal.codigoInterno}${animal.nome ? ` — ${animal.nome}` : ''}`} onChange={id => setFilters(v => ({ ...v, animalId: id ?? '', page: 1 }))} /><Select label="Vínculo" value={filters.estado} onChange={estado => setFilters(v => ({ ...v, estado, page: 1 }))} options={[[ '', 'Todos'], ['SemPonte', 'Sem ponte'], ['ComPonte', 'Com ponte vitalícia']]} /></fieldset><Notice error={error} retry={() => setRetry(v => v + 1)} />{!data && !error && <p role="status">Consultando lançamentos…</p>}{data && <><ScrollTable label="Lançamentos disponíveis para seleção"><thead><tr><th scope="col">Selecionar</th><th scope="col">Lançamento</th><th scope="col">Peso registrado</th><th scope="col">Estado</th><th scope="col">Revisão observada</th></tr></thead><tbody>{data.items.map(candidate => <tr key={candidate.origem.id}><td><input type="checkbox" aria-label={`Selecionar lançamento ${candidate.origem.id}`} checked={!!selected[candidate.origem.id]} disabled={disabled || !!candidate.ponteId} onChange={() => toggle(candidate)} /></td><td><EggSourceLabel source={candidate.origem} /></td><td>{candidate.origem.pesoGramas} g · evidência zootécnica</td><td>{candidate.grupoId ? <Link className="text-emerald-700 underline" to={`${eggRoot}/grupos/${candidate.grupoId}`} state={navigation}>Visualizar ponte vitalícia</Link> : 'Sem ponte · comprovação ainda necessária'}</td><td>{showTime(candidate.origem.updatedAtUtc)}</td></tr>)}</tbody></ScrollTable>{!data.items.length && <p>Nenhum lançamento neste filtro.</p>}<Pagination page={filters.page} totalPages={data.totalPages} onChange={page => setFilters(v => ({ ...v, page }))} /></>}<div className="space-y-3"><h3 className="font-semibold">Selecionados entre páginas: {Object.keys(selected).length}</h3>{Object.values(selected).map(candidate => <div key={candidate.origem.id} className="flex min-w-0 flex-wrap items-center gap-3 rounded-lg border p-3"><div className="min-w-0 flex-1"><EggSourceLabel source={candidate.origem} /><p className="text-sm text-slate-600">Observado em {showTime(candidate.origem.updatedAtUtc)}</p></div><button type="button" className={buttonClass} disabled={disabled} onClick={() => toggle(candidate)} aria-label={`Remover lançamento ${candidate.origem.id}`}>Remover seleção</button></div>)}</div></Panel>
}

export function EggCandidatesPage() {
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  const [search] = useSearchParams()
  const [selected, setSelected] = useState<EggSelected>({})
  const [form, setForm] = useState({ operacao: 'Entrada' as EggOperation, itemId: '', recorteId: '', provaId: '', loteId: '', localId: '', responsavelId: user?.userId ?? '', motivo: '', evidencia: '' })
  const [ordinals, setOrdinals] = useState<Record<string, string>>({})
  const [preview, setPreview] = useState<EggPreview | null>(null)
  const [accepted, setAccepted] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [result, setResult] = useState<EggCommandResult | null>(null)
  const preparing = useRef(false)
  const heading = useRef<HTMLElement>(null)
  const command = useEggBridgeCommand()
  const draft = useUnsavedChanges(Object.keys(selected).length > 0 || !!form.motivo)
  const locked = command.busy || !!command.pending || loading
  useEffect(() => { if (preview) heading.current?.focus() }, [preview])
  const invalidate = () => { setPreview(null); setAccepted(false); setError(null); setResult(null) }
  const set = (key: keyof typeof form, value: string) => {
    setForm(v => ({ ...v, [key]: value, ...(key === 'itemId' ? { recorteId: '', provaId: '', loteId: '', localId: '' } : {}) })); invalidate()
  }
  const complete = (value: EggCommandResult) => { setResult(value); setPreview(null); setAccepted(false); draft.markSaved(); if (value.kind === 'grupo') setSelected({}) }
  const prepare = async () => {
    if (preparing.current || locked) return
    preparing.current = true; setLoading(true); setPreview(null); setAccepted(false); setError(null); setResult(null)
    try {
      const sources = Object.values(selected)
      if (!sources.length) throw new Error('Selecione explicitamente ao menos um lançamento.')
      if (!form.itemId || !form.recorteId || !form.responsavelId || !form.motivo.trim() || !form.evidencia.trim()) throw new Error('Selecione item, conferência e responsável e informe motivo e evidência.')
      if (form.operacao === 'Associacao' && !admin) throw new Error('A associação de recebimento anterior exige Admin.')
      if (form.operacao === 'Entrada' && (!form.loteId || !form.localId)) throw new Error('Selecione lote dedicado vazio e local para a entrada.')
      if (form.operacao === 'Associacao' && !form.provaId) throw new Error('Selecione a prova da entrada manual original em un.')
      const body: EggPreviewCommand = { operacao: form.operacao, recorteId: form.recorteId, responsavelId: form.responsavelId, motivo: form.motivo.trim(), evidencia: form.evidencia.trim(), selecao: sources.map(row => ({ producaoOvoId: row.origem.id, ordinal: form.operacao === 'Associacao' ? integerCount(ordinals[row.origem.id] ?? '') : null })), loteId: form.operacao === 'Entrada' ? form.loteId : null, localId: form.operacao === 'Entrada' ? form.localId : null, provaId: form.operacao === 'Associacao' ? form.provaId : null }
      setPreview(await eggPreview('/previas', body, parseEggPreview))
    } catch (e) { setError(inventoryError(e)) }
    finally { preparing.current = false; setLoading(false) }
  }
  const confirm = async () => {
    if (!preview || !preview.podeConfirmar || !accepted || locked) return
    setError(null)
    try { const value = await command.submit(preview.operacao, '/confirmacoes', { token: preview.token }); if (value) complete(value) } catch (e) { setError(inventoryError(e)) }
  }
  const selectedForNavigation = Object.values(selected).map(row => ({ ...row, origem: { ...row.origem, observacao: null } }))
  return <EggBridgeFrame title="Ponte explícita de ovos"><Notice error={error} /><EggRecovery control={command} complete={complete} setError={setError} /><EggCommandReceipt result={result} /><EggCandidateSelection initialAnimal={search.get('animalId') ?? ''} selected={selected} disabled={locked} onChange={value => { setSelected(value); invalidate() }} />{admin && <Panel><Link className={buttonClass} to={`${eggRoot}/administracao`} state={{ selected: selectedForNavigation }}>Preparar conferência dos selecionados</Link></Panel>}<Panel title="Preparar operação"><form onSubmit={e => e.preventDefault()} className="space-y-4"><fieldset disabled={locked} className="grid min-w-0 gap-4 sm:grid-cols-2"><Select label="Operação da ponte" value={form.operacao} onChange={v => set('operacao', v)} options={admin ? [['Entrada', 'Nova entrada física comprovada'], ['Associacao', 'Associar recebimento anterior · delta zero']] : [['Entrada', 'Nova entrada física comprovada']]} /><ReferenceSelect label="Item de ovos" path="/itens" value={form.itemId || null} params={{ ativo: true }} parse={parseItem} caption={item => `${item.codigo} — ${item.nome} · ${item.unidade}`} onChange={id => set('itemId', id ?? '')} />{form.itemId && <ReferenceSelect label="Conferência aprovada" path={`${eggApi}/recortes`} value={form.recorteId || null} params={{ itemId: form.itemId }} parse={parseEggScope} caption={scope => `${scope.fronteira} · revisão ${scope.revisaoAncora} · ${showTime(scope.createdAtUtc)}`} onChange={id => set('recorteId', id ?? '')} />}{form.operacao === 'Entrada' ? <><ReferenceSelect label="Lote dedicado vazio" path="/estoque/lotes" value={form.loteId || null} params={{ itemId: form.itemId, ativo: true }} parse={parseLote} caption={lot => `${lot.codigo} — ${lot.itemNome} · ${lot.situacao}`} onChange={id => set('loteId', id ?? '')} /><ReferenceSelect label="Local da entrada" path="/estoque/locais" value={form.localId || null} params={{ ativo: true }} parse={parseLocal} caption={local => `${local.codigo} — ${local.nome} · ${local.finalidade}`} onChange={id => set('localId', id ?? '')} /></> : form.itemId && <ReferenceSelect label="Prova de recebimento manual" path={`${eggApi}/provas`} value={form.provaId || null} params={{ itemId: form.itemId }} parse={parseEggProof} caption={proof => `${proof.contagem} un · ${proof.evidencia} · ${showTime(proof.createdAtUtc)}`} onChange={id => set('provaId', id ?? '')} />}<ReferenceSelect label="Responsável pela operação" path="/estoque/responsaveis" value={form.responsavelId || null} params={{ ativo: true }} parse={parseResponsible} caption={person => person.nome} onChange={id => set('responsavelId', id ?? '')} /><Input label="Motivo da ponte" value={form.motivo} onChange={v => set('motivo', v)} multiline required /><Input label="Evidência da seleção e da origem física" value={form.evidencia} onChange={v => set('evidencia', v)} multiline required /></fieldset>{form.operacao === 'Associacao' && <fieldset disabled={locked} className="space-y-4"><legend className="font-semibold">Mapeamento explícito das unidades comprovadas</legend><p>Informe a posição de cada unidade na prova. Os ordinais são documentais; não identificam qual ovo foi consumido ou está disponível.</p>{Object.values(selected).map(row => <div key={row.origem.id} className="min-w-0 rounded-lg border p-3"><EggSourceLabel source={row.origem} /><Input label={`Ordinal na prova · ${row.origem.id}`} value={ordinals[row.origem.id] ?? ''} onChange={v => { setOrdinals(values => ({ ...values, [row.origem.id]: v })); invalidate() }} /></div>)}</fieldset>}<p>{form.operacao === 'Associacao' ? 'A associação preserva o recebimento histórico e não incrementa saldo. Entradas somente em kg não provam contagem.' : 'O lote deve estar vazio e sem histórico. Depois da entrada, não receberá novos créditos ordinários. Registros ambíguos permanecem pendentes.'}</p><button type="button" className={buttonClass} disabled={locked} onClick={() => void prepare()}>{loading ? 'Preparando…' : 'Preparar prévia da seleção'}</button></form></Panel>{preview && <section ref={heading} tabIndex={-1} className="min-w-0 space-y-4 rounded-2xl border border-emerald-300 bg-white p-4 focus:ring-2 focus:ring-emerald-600 sm:p-6" aria-label="Prévia da ponte"><h2 className="text-xl font-semibold">Revise a seleção antes de confirmar</h2><p>Quantidade comprovada selecionada: {preview.quantidade} un. Delta de estoque: {preview.deltaEstoque} un.</p>{preview.saldoAnterior !== null && <p>Saldo observado: {preview.saldoAnterior} un · saldo previsto: {preview.saldoPosterior} un.</p>}{preview.operacao === 'Associacao' && <p className="font-semibold">Nenhum incremento de estoque. Capacidade histórica restante apresentada pelo servidor: {preview.capacidadeRestante} un.</p>}<p>Conferência na revisão {preview.revisaoAncora} · corte do livro {preview.sequenciaAte} · prévia válida até {showTime(preview.expiraEmUtc)}. Esta prévia não reserva estoque, ovos ou capacidade.</p><ScrollTable label="Resultado por lançamento selecionado"><thead><tr><th scope="col">Lançamento selecionado</th><th scope="col">Decisão</th><th scope="col">Resultado da revisão</th></tr></thead><tbody>{preview.origens.map(row => <tr key={row.producaoOvoId}><td>{selected[row.producaoOvoId] ? <EggSourceLabel source={selected[row.producaoOvoId].origem} /> : row.producaoOvoId}</td><td>{row.decisao}</td><td>{row.impedimento ?? 'Elegível nesta prévia'}</td></tr>)}</tbody></ScrollTable>{preview.avisos.map((warning, i) => <p key={i}>{warning}</p>)}{!preview.podeConfirmar && <p role="alert">A seleção contém impedimentos. Revise conscientemente os registros e prepare outra prévia; nenhuma linha será omitida automaticamente.</p>}<label className="flex items-start gap-3"><input type="checkbox" className="mt-1" checked={accepted} disabled={locked || !preview.podeConfirmar} onChange={e => setAccepted(e.target.checked)} /><span>Conferi a seleção, a comprovação individual e o efeito de {preview.deltaEstoque} un apresentado.</span></label><button type="button" className={primaryClass} disabled={locked || !preview.podeConfirmar || !accepted} onClick={() => void confirm()}>Confirmar {preview.operacao === 'Associacao' ? 'associação sem saldo' : 'entrada individual'}</button></section>}</EggBridgeFrame>
}
