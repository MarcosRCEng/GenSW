import { Link } from 'react-router-dom'
import { useEffect, useState, type FormEvent } from 'react'
import { api, save, message, type Page, type Weight } from './api'
import './evolution.css'

const milestones = ['Livre', 'Nascimento', 'Idade em dias', 'Primeira postura', 'Abate', 'Outro']
export function WeightsPanel({ animalId, birth, readOnly = false }: { animalId: string; birth: string | null; readOnly?: boolean }) {
  const [items, setItems] = useState<Page<Weight> | null>(null)
  const [page, setPage] = useState(1), [revision, setRevision] = useState(0)
  const [editing, setEditing] = useState<string | null>(null)
  const [date, setDate] = useState(''), [weight, setWeight] = useState(''), [milestone, setMilestone] = useState(1)
  const [description, setDescription] = useState(''), [target, setTarget] = useState(''), [note, setNote] = useState('')
  const [error, setError] = useState(''), [busy, setBusy] = useState(false)
  const [from, setFrom] = useState(''), [to, setTo] = useState(''), [filter, setFilter] = useState('')
  const root = `/animais/${animalId}/pesagens`
  useEffect(() => {
    const controller = new AbortController(); setItems(null); setError('')
    const query = new URLSearchParams({ page: String(page), pageSize: '10' })
    if (from) query.set('dataInicial', from); if (to) query.set('dataFinal', to); if (filter) query.set('tipoMarco', filter)
    void api<Page<Weight>>(`${root}?${query}`, controller.signal).then(setItems).catch(e => { if (!controller.signal.aborted) setError(message(e)) })
    return () => controller.abort()
  }, [root, page, revision, from, to, filter])
  function edit(item?: Weight) {
    setEditing(item?.id ?? null); setDate(item?.dataMedicao ?? ''); setWeight(String(item?.pesoGramas ?? ''))
    setMilestone(item?.tipoMarco ?? 1); setDescription(item?.descricaoMarco ?? ''); setTarget(String(item?.idadeReferenciaDias ?? '')); setNote(item?.observacao ?? '')
  }
  async function submit(e: FormEvent) {
    e.preventDefault(); if (readOnly) return; setBusy(true); setError('')
    try {
      await save(`${root}${editing ? `/${editing}` : ''}`, { dataMedicao: date, pesoGramas: Number(weight), tipoMarco: milestone, descricaoMarco: description || null, idadeReferenciaDias: milestone === 3 ? Number(target) : null, observacao: note || null }, editing ? 'PUT' : 'POST')
      edit(); setRevision(x => x + 1)
    } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }
  return <section className="animal-panel" aria-label="Pesagens"><h2>Histórico de peso</h2><p>Data da medição, idade real e idade alvo são informações distintas.</p>
    {!birth && <p className="animal-notice">Nascimento desconhecido: a idade real não pode ser calculada, inclusive no marco Nascimento.</p>}
    {error && <><p role="alert">{error}</p><button onClick={() => { setError(''); setRevision(x => x + 1) }}>Recarregar pesagens</button></>}
    {!readOnly && <form onSubmit={e => void submit(e)} className="animal-fields">
      <label>Data da medição<input type="date" required value={date} max={new Date().toISOString().slice(0, 10)} onChange={e => setDate(e.target.value)} /></label>
      <label>Peso (g)<input type="number" required min="0.01" max="99999999.99" step="0.01" value={weight} onChange={e => setWeight(e.target.value)} /></label>
      <label>Marco<select value={milestone} onChange={e => setMilestone(Number(e.target.value))}>{milestones.map((x, i) => <option value={i + 1} key={x}>{x}</option>)}</select></label>
      {milestone === 3 && <label>Idade alvo (dias)<input type="number" min="0" step="1" required value={target} onChange={e => setTarget(e.target.value)} /></label>}
      <label>Descrição do marco<input maxLength={100} required={milestone === 6} value={description} onChange={e => setDescription(e.target.value)} /></label>
      <label>Observação<textarea maxLength={2000} value={note} onChange={e => setNote(e.target.value)} /></label>
      <div className="animal-actions"><button disabled={busy} type="submit">{editing ? 'Salvar correção' : 'Registrar pesagem'}</button>{editing && <button type="button" onClick={() => edit()}>Cancelar correção</button>}</div>
    </form>}
    <div className="animal-fields"><label>De<input type="date" value={from} onChange={e => { setFrom(e.target.value); setPage(1) }} /></label><label>Até<input type="date" value={to} onChange={e => { setTo(e.target.value); setPage(1) }} /></label><label>Filtrar marco<select value={filter} onChange={e => { setFilter(e.target.value); setPage(1) }}><option value="">Todos</option>{milestones.map((x, i) => <option value={i + 1} key={x}>{x}</option>)}</select></label></div>
    {!items ? !error && <p role="status">Carregando pesagens…</p> : <><div className="animal-table"><table><caption>{items.totalItems} medições</caption><thead><tr><th>Data</th><th>Gramas</th><th>Marco</th><th>Idade alvo / real</th><th>Observação</th><th>Ações</th></tr></thead><tbody>{items.items.map(x => <tr key={x.id}><td>{x.dataMedicao}</td><td>{x.pesoGramas.toLocaleString('pt-BR')}</td><td>{milestones[x.tipoMarco - 1]}{x.descricaoMarco && ` · ${x.descricaoMarco}`}</td><td>{x.idadeReferenciaDias ?? '—'} / {x.idadeDiasNaMedicao ?? 'desconhecida'}</td><td>{x.observacao ?? '—'}</td><td><div className="animal-actions"><Link className="font-semibold text-emerald-700 underline" to={`/animais/${animalId}/pesagens/${x.id}`}>Visualizar</Link>{!readOnly && <button type="button" onClick={() => edit(x)}>Corrigir</button>}</div></td></tr>)}</tbody></table></div>{items.totalItems === 0 && <p>Nenhuma pesagem registrada.</p>}<div className="animal-actions"><button disabled={page === 1} onClick={() => setPage(x => x - 1)}>Pesagens anteriores</button><span>Página {page} de {Math.max(1, items.totalPages)}</span><button disabled={page >= items.totalPages} onClick={() => setPage(x => x + 1)}>Próximas pesagens</button></div></>}
  </section>
}
