import { useEffect, useId, useState, type FormEvent } from 'react'
import { createFiliation, listFiliations, type Filiation, type FiliationType } from '../services/filiationsService'
import { api, message, type Page, type Candidate } from '../../evolution/api'
import { AnimalTree } from '../../evolution/AnimalTree'
import '../../evolution/evolution.css'

export function AnimalFiliationsPanel({ animalId, pendingClassification = false, readOnly = false }: { animalId: string; pendingClassification?: boolean; readOnly?: boolean }) {
  const [items, setItems] = useState<Filiation[]>([]), [type, setType] = useState<FiliationType>(1)
  const [search, setSearch] = useState(''), [selected, setSelected] = useState<Candidate | null>(null), [index, setIndex] = useState(-1)
  const [candidates, setCandidates] = useState<Page<Candidate> | null>(null), [page, setPage] = useState(1), [revision, setRevision] = useState(0)
  const [error, setError] = useState(''), [busy, setBusy] = useState(false), [showTree, setShowTree] = useState(false)
  const [loading, setLoading] = useState(true)
  const listId = useId()
  useEffect(() => { let current = true; setLoading(true); setError(''); setItems([]); void listFiliations(animalId).then(x => { if (current) setItems(x) }).catch(e => { if (current) setError(message(e)) }).finally(() => { if (current) setLoading(false) }); return () => { current = false } }, [animalId, revision])
  useEffect(() => {
    const controller = new AbortController(); setCandidates(null); setIndex(-1)
    if (readOnly || pendingClassification) return () => controller.abort()
    const timer = setTimeout(() => {
      const query = new URLSearchParams({ tipoFiliacao: String(type), search, page: String(page), pageSize: '10' })
      void api<Page<Candidate>>(`/animais/${animalId}/progenitores-elegiveis?${query}`, controller.signal).then(x => { if (!controller.signal.aborted) setCandidates(x) }).catch(e => { if (!controller.signal.aborted) setError(message(e)) })
    }, 300)
    return () => { clearTimeout(timer); controller.abort() }
  }, [animalId, type, search, page, revision, pendingClassification, readOnly])
  async function submit(e: FormEvent) {
    e.preventDefault(); if (readOnly || !selected || pendingClassification) return
    setBusy(true); setError('')
    try { await createFiliation(animalId, { progenitorId: selected.id, tipoFiliacao: type, dataRegistro: null }); setSelected(null); setSearch(''); setRevision(x => x + 1) }
    catch (e) { setError(message(e)); setSelected(null); setRevision(x => x + 1) } finally { setBusy(false) }
  }
  function choose(candidate: Candidate) { setSelected(candidate); setIndex(-1) }
  return <section className="animal-panel" aria-label="Genealogia"><h2>Genealogia</h2><p>A correção preserva a filiação anterior no histórico.</p>{pendingClassification && <p className="animal-notice">Salve as alterações de espécie, raça ou sexo antes de pesquisar e definir progenitores.</p>}{error && <div><p role="alert">{error}</p><button onClick={() => setRevision(value => value + 1)} type="button">Recarregar filiações</button></div>}
    {!readOnly && <form onSubmit={e => void submit(e)}><fieldset disabled={busy || pendingClassification}><div className="animal-fields"><label>Papel do progenitor<select value={type} onChange={e => { setType(Number(e.target.value) as FiliationType); setSelected(null); setPage(1) }}><option value="1">Pai</option><option value="2">Mãe</option></select></label><label>Pesquisar progenitor por nome ou código<input role="combobox" aria-autocomplete="list" aria-expanded={!!candidates && !selected} aria-controls={listId} aria-activedescendant={index >= 0 ? `${listId}-${index}` : undefined} value={search} onChange={e => { setSearch(e.target.value); setSelected(null); setPage(1) }} onKeyDown={e => {
      if (e.key === 'ArrowDown') { e.preventDefault(); setIndex(x => Math.min((candidates?.items.length ?? 0) - 1, x + 1)) }
      if (e.key === 'ArrowUp') { e.preventDefault(); setIndex(x => Math.max(0, x - 1)) }
      if (e.key === 'Enter' && index >= 0 && candidates?.items[index]) { e.preventDefault(); choose(candidates.items[index]) }
      if (e.key === 'Escape') { setSelected(null); setIndex(-1) }
    }} /></label></div>
    {!pendingClassification && !selected && (!candidates ? <p role="status">Buscando progenitores…</p> : <><ul id={listId} role="listbox" aria-label="Progenitores elegíveis" className="candidate-options">{candidates.items.map((candidate, i) => <li role="option" aria-selected={index === i} id={`${listId}-${i}`} key={candidate.id} onClick={() => choose(candidate)}>{candidate.codigoInterno} — {candidate.nome || 'Sem nome'}</li>)}</ul>{!candidates.items.length && <p>Nenhum progenitor elegível para esta busca.</p>}<div className="animal-actions"><button type="button" disabled={page === 1} onClick={() => setPage(x => x - 1)}>Candidatos anteriores</button><span>Página {page}</span><button type="button" disabled={page >= candidates.totalPages} onClick={() => setPage(x => x + 1)}>Próximos candidatos</button></div></>)}
    {selected && <p role="status">Selecionado: {selected.codigoInterno} — {selected.nome || 'Sem nome'} <button type="button" onClick={() => setSelected(null)}>Trocar</button></p>}<button type="submit" disabled={!selected}>Definir {type === 1 ? 'pai' : 'mãe'}</button></fieldset></form>}
    {loading && <p role="status">Carregando filiações…</p>}
    <ul className="mt-4 space-y-2">{items.map(x => <li key={x.id}>{x.tipoFiliacao === 1 ? 'Pai' : 'Mãe'}: <strong>{x.progenitor?.codigoInterno || 'Progenitor histórico'}</strong> {x.progenitor?.nome} · {x.ativa ? 'Atual' : 'Histórico'}{x.progenitor?.ativo === false && ' · Inativo'}{readOnly && <span className="block text-sm">Registro: {x.dataRegistro ?? 'Não informado'} · Fim: {x.dataFim ?? 'Em aberto'}</span>}</li>)}</ul>{!loading && !error && !items.length && <p>Sem filiações cadastradas.</p>}
    <button className="mt-4" type="button" onClick={() => setShowTree(x => !x)}>{showTree ? 'Ocultar árvore' : 'Árvore genealógica'}</button>{showTree && <AnimalTree key={revision} animalId={animalId} />}
  </section>
}
