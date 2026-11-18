import { ModalDialog } from '../../../shared/layout/ModalDialog'
import { TableRegion } from '../../../shared/layout/TableRegion'
import { parentReturnState, useRestoredParentSection } from '../../../shared/details/listNavigation'
import { useUnsavedChanges } from '../../../shared/navigation/useUnsavedChanges'
import { Link, useLocation } from 'react-router-dom'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { httpRequest } from '../../../shared/http/httpClient'
import { isHttpError } from '../../../shared/http/httpErrors'

type Entry = { id: string; dataPostura: string; pesoGramas: number; observacao: string | null }
type Response = { items: Entry[]; page: number; totalPages: number; metricas: { totalLancamentos: number; pesoMedioGramas: number | null; pesoMinimoGramas: number | null; pesoMaximoGramas: number | null; pesoPadraoGramas: number | null; diasAtePesoPadrao: number | null } }

export function AnimalEggProductionPanel({ animalId, readOnly = false }: { animalId: string; readOnly?: boolean }) {
  const location = useLocation()
  const parentPath = `/animais/${animalId}`
  const initial = useRestoredParentSection(parentPath, 'producoes-ovos', { page: 1 })
  const [dirty, setDirty] = useState(false)
  const { markSaved, confirmDiscard } = useUnsavedChanges(dirty)
  const [result, setResult] = useState<Response | null>(null)
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10))
  const [weight, setWeight] = useState('')
  const [note, setNote] = useState('')
  const [editing, setEditing] = useState<Entry | null>(null)
  const [pendingDelete, setPendingDelete] = useState<Entry | null>(null)
  const [deleting, setDeleting] = useState(false)
  const cancelDeleteButton = useRef<HTMLButtonElement>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [page, setPage] = useState(initial.page)
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    setResult(null); setError(null)
    void httpRequest<Response>(`/animais/${animalId}/producoes-ovos?page=${page}&pageSize=100`, { authenticated: true, signal: controller.signal }).then(data => {
      if (controller.signal.aborted) return
      const lastPage = Math.max(1, data.totalPages)
      if (page > lastPage) setPage(lastPage)
      else setResult(data)
    }).catch(() => { if (!controller.signal.aborted) setError('Não foi possível carregar a produção de ovos.') })
    return () => controller.abort()
  }, [animalId, page, revision])
  const submit = async (event: FormEvent) => { event.preventDefault(); if (readOnly) return; const pesoGramas = Number(weight); if (!date || !Number.isFinite(pesoGramas) || pesoGramas <= 0) { setError('Informe uma data e um peso positivo.'); return } try { await httpRequest(`/animais/${animalId}/producoes-ovos${editing ? `/${editing.id}` : ''}`, { method: editing ? 'PUT' : 'POST', authenticated: true, body: { dataPostura: date, pesoGramas, observacao: note.trim() || null } }); markSaved(); setDirty(false); setDate(new Date().toISOString().slice(0, 10)); setWeight(''); setNote(''); setEditing(null); setError(null); setRevision(value => value + 1) } catch { setError('Não foi possível salvar o lançamento.') } }
  const beginEdit = (entry: Entry) => { setEditing(entry); setDate(entry.dataPostura); setWeight(String(entry.pesoGramas)); setNote(entry.observacao ?? '') }
  const beginDelete = (entry: Entry) => { setError(null); setNotice(null); setPendingDelete(entry) }
  const cancelDelete = () => { if (!deleting) setPendingDelete(null) }
  const deleteEntry = async () => {
    if (!pendingDelete || deleting) return
    setDeleting(true); setError(null)
    try {
      await httpRequest<void>(`/animais/${animalId}/producoes-ovos/${pendingDelete.id}`, { method: 'DELETE', authenticated: true })
      if (editing?.id === pendingDelete.id) {
        markSaved()
        setDirty(false)
        setEditing(null)
        setWeight('')
        setNote('')
      }
      setPendingDelete(null); setNotice('Lançamento de postura excluído.')
      setRevision(value => value + 1)
    } catch (failure) {
      setPendingDelete(null)
      if (isHttpError(failure) && failure.status === 409) setError(
        'Esta postura faz parte do histórico da ponte de ovos. Se houver grupo, localize a postura pelo filtro “Com ponte vitalícia” e peça ao administrador para revisar a prévia de tratamento. ' +
        'Para estornar uma entrada integral registrada por engano, avalie “Compensar entrada integral”. Para corrigir uma associação indevida, avalie “Invalidar associação documental”. ' +
        'Se houver apenas conferência, peça a revisão em “Conferências e provas”. A postura permanece no histórico após o tratamento.',
      )
      else if (isHttpError(failure) && failure.status === 404) setError('O lançamento não foi encontrado. Recarregue a produção de ovos.')
      else if (isHttpError(failure) && failure.status === 403) setError('Você não tem permissão para excluir este lançamento.')
      else setError('Não foi possível excluir o lançamento de postura.')
    } finally { setDeleting(false) }
  }
  const metrics = result?.metricas
  return <section id="record-producoes-ovos" tabIndex={-1} className="mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"><h2 className="text-xl font-bold text-slate-900">Produção de ovos</h2><p className="mt-1 text-sm text-slate-600">Lançamentos independentes dos ciclos reprodutivos.</p><Link className="mt-3 inline-flex min-h-11 items-center rounded-lg border border-slate-300 px-3 py-2 font-semibold text-emerald-700" to={`/producao/ponte-ovos?animalId=${encodeURIComponent(animalId)}`}>Conferir lançamentos na ponte de ovos</Link>
    {!readOnly && <form className="mt-4 grid gap-3 md:grid-cols-4" onChangeCapture={() => setDirty(true)} onSubmit={submit}><input aria-label="Data da postura" className="rounded-lg border border-slate-300 px-3 py-2" onChange={(e) => setDate(e.target.value)} type="date" value={date} /><input aria-label="Peso em gramas" className="rounded-lg border border-slate-300 px-3 py-2" min="0.01" onChange={(e) => setWeight(e.target.value)} placeholder="Peso (g)" step="0.01" type="number" value={weight} /><input aria-label="Observação" className="rounded-lg border border-slate-300 px-3 py-2" maxLength={2000} onChange={(e) => setNote(e.target.value)} placeholder="Observação" value={note} /><div className="flex gap-2"><button className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white" type="submit">{editing ? 'Atualizar' : 'Adicionar'}</button>{editing ? <button className="rounded-lg border px-3 py-2 text-sm" onClick={() => confirmDiscard(() => { setDirty(false); setEditing(null); setWeight(''); setNote('') })} type="button">Cancelar</button> : null}</div></form>}
    {pendingDelete && <ModalDialog open title="Excluir lançamento de postura?" onClose={cancelDelete} closeLabel="Fechar confirmação de exclusão" initialFocusRef={cancelDeleteButton} footer={<>
      <button ref={cancelDeleteButton} className="rounded border border-slate-300 bg-white px-3 py-2" disabled={deleting} onClick={cancelDelete} type="button">Cancelar</button>
      <button className="rounded bg-red-700 px-3 py-2 font-semibold text-white" disabled={deleting} onClick={() => void deleteEntry()} type="button">{deleting ? 'Excluindo…' : 'Confirmar exclusão'}</button>
    </>}><p>Postura de {pendingDelete.dataPostura}, {pendingDelete.pesoGramas} g. Esta ação não pode ser desfeita.</p></ModalDialog>}
    {notice && <p className="mt-3 text-sm text-emerald-700" role="status">{notice}</p>}
    {error ? <div><p className="mt-3 text-sm text-red-700" role="alert">{error}</p><button className="mt-2 rounded border px-3 py-2" type="button" onClick={() => setRevision(value => value + 1)}>Recarregar produção</button></div> : null}
    {!result && !error && <p className="mt-4" role="status">Carregando produção de ovos…</p>}
    {metrics ? <div className="mt-5 grid grid-cols-2 gap-3 text-sm md:grid-cols-4"><p><strong>{metrics.totalLancamentos}</strong><br />lançamentos</p><p><strong>{metrics.pesoMedioGramas ?? '—'} g</strong><br />peso médio</p><p><strong>{metrics.pesoMinimoGramas ?? '—'}–{metrics.pesoMaximoGramas ?? '—'} g</strong><br />evolução de peso</p><p><strong>{metrics.pesoPadraoGramas ? `${metrics.diasAtePesoPadrao ?? '—'} dias` : 'Não definido'}</strong><br />até peso padrão</p></div> : null}
    <TableRegion label="Produção de ovos" className="mt-5 overflow-x-auto"><table className="min-w-full text-sm"><thead><tr className="border-b text-left"><th className="p-2">Postura</th><th className="p-2">Peso</th><th className="p-2">Observação</th><th className="p-2">Ações</th></tr></thead><tbody>{result?.items.map((entry) => <tr className="border-b" key={entry.id}><td className="p-2">{entry.dataPostura}</td><td className="p-2">{entry.pesoGramas} g</td><td className="p-2">{entry.observacao ?? '—'}</td><td className="p-2"><div className="flex gap-3"><Link className="font-semibold text-emerald-700 underline" state={parentReturnState(location.state, parentPath, { section: 'producoes-ovos', page })} to={`/animais/${animalId}/producoes-ovos/${entry.id}`}>Visualizar</Link>{!readOnly && <><button className="font-semibold text-emerald-700" onClick={() => confirmDiscard(() => { setDirty(false); beginEdit(entry) })} type="button">Editar</button><button aria-label={`Excluir postura de ${entry.dataPostura}, ${entry.pesoGramas} g`} className="font-semibold text-red-700" onClick={() => beginDelete(entry)} type="button">Excluir</button></>}</div></td></tr>)}</tbody></table></TableRegion>
    {result?.items.length === 0 && <p className="mt-4">Nenhuma produção de ovos registrada.</p>}
    {result && result.totalPages > 1 && <nav className="mt-4 flex flex-wrap items-center gap-3" aria-label="Paginação da produção de ovos"><button type="button" disabled={page === 1} onClick={() => setPage(value => value - 1)}>Produções anteriores</button><span>Página {page} de {result.totalPages}</span><button type="button" disabled={page >= result.totalPages} onClick={() => setPage(value => value + 1)}>Próximas produções</button></nav>}
  </section>
}
