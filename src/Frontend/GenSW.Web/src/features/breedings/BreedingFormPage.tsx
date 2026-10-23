import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { formReturnTarget, listReturnState, parentReturnState, savedRecordState } from '../../shared/details/listNavigation'
import { PageHeader } from '../../shared/layout/PageHeader'
import { useUnsavedChanges } from '../../shared/navigation/useUnsavedChanges'
import { BreedingAnimalSelector } from './BreedingAnimalSelector'
import { createBreeding, getBreeding, updateBreeding } from './breedingsService'
import type { BreedingRequest, BreedingStatus } from './types'

const initial: BreedingRequest = { machoId: '', femeaId: '', status: 1, dataInicio: null, dataFim: null, objetivo: null, observacao: null }
const control = 'mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm'

export function BreedingFormPage() {
  const { id } = useParams()
  const location = useLocation()
  // A reused route starts a new form and retires requests from its previous record.
  return <BreedingForm key={`${id ?? 'new'}:${location.key}`} id={id} />
}

function BreedingForm({ id }: { id?: string }) {
  const navigate = useNavigate()
  const location = useLocation()
  const [form, setForm] = useState<BreedingRequest>(initial)
  const [loading, setLoading] = useState(Boolean(id))
  const [loadError, setLoadError] = useState(false)
  const [reload, setReload] = useState(0)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [dirty, setDirty] = useState(false)
  const active = useRef(false)
  const { markSaved } = useUnsavedChanges(dirty)
  const returnTarget = formReturnTarget(location.state, '/cruzamentos', id ? `/cruzamentos/${id}` : undefined)
  const listState = listReturnState(location.state, '/cruzamentos')

  useEffect(() => {
    active.current = true
    return () => { active.current = false }
  }, [])

  useEffect(() => {
    if (!id) return
    let current = true
    void getBreeding(id).then(record => {
      if (!current) return
      setForm({ machoId: record.machoId, femeaId: record.femeaId, status: record.status, dataInicio: record.dataInicio, dataFim: record.dataFim, objetivo: record.objetivo, observacao: record.observacao })
    }).catch(() => { if (current) setLoadError(true) })
      .finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [id, reload])

  const change = (patch: Partial<BreedingRequest>) => {
    setForm(value => ({ ...value, ...patch }))
    setDirty(true)
  }
  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (saving) return
    if (!form.machoId || !form.femeaId) return setError('Selecione o macho e a fêmea.')
    if (form.machoId === form.femeaId) return setError('Os animais devem ser diferentes.')
    if (form.dataInicio && form.dataFim && form.dataFim < form.dataInicio) return setError('A data final não pode ser anterior à inicial.')
    setSaving(true)
    setError(null)
    const action = id ? updateBreeding(id, form) : createBreeding(form)
    void action.then(record => {
      if (!active.current) return
      markSaved()
      setDirty(false)
      const target = id ? returnTarget.to : `/cruzamentos/${record.id}`
      navigate(target, { state: savedRecordState(id ? returnTarget.state : listState, target) })
    }).catch(() => { if (active.current) setError('Não foi possível salvar o cruzamento.') })
      .finally(() => { if (active.current) setSaving(false) })
  }

  return <main className="min-h-screen bg-slate-50 px-4 py-8"><div className="mx-auto max-w-3xl">
    <PageHeader title={id ? 'Editar cruzamento' : 'Novo cruzamento'} back={{ to: returnTarget.to, state: returnTarget.state, label: id && returnTarget.to === `/cruzamentos/${id}` ? 'Voltar para cruzamento' : 'Voltar para Cruzamentos' }} actions={id ? <Link className="gensw-secondary-action" state={parentReturnState(location.state, `/cruzamentos/${id}`, { section: 'ciclos' })} to={`/ciclos-reprodutivos?cruzamentoId=${id}`}>Ciclos e Proles</Link> : undefined} />
    {loading ? <p className="mt-8" role="status">Carregando…</p> : loadError ? <div className="mt-8" role="alert"><p>Não foi possível carregar o cruzamento.</p><button type="button" className="gensw-secondary-action mt-4" onClick={() => { setLoading(true); setLoadError(false); setReload(value => value + 1) }}>Tentar novamente</button></div> : <form className="mt-8 rounded-2xl border bg-white p-6" onSubmit={submit}>
      {error && <p className="mb-4 text-red-700" role="alert">{error}</p>}
      <fieldset disabled={saving}>
        <legend className="sr-only">Dados do cruzamento</legend>
        <div className="grid gap-4 sm:grid-cols-2">
          <BreedingAnimalSelector id="male" label="Macho" sexo={1} value={form.machoId} onChange={value => change({ machoId: value })} contextKey={id ?? 'new'} disabled={saving} />
          <BreedingAnimalSelector id="female" label="Fêmea" sexo={2} value={form.femeaId} onChange={value => change({ femeaId: value })} contextKey={id ?? 'new'} disabled={saving} />
          <div><label htmlFor="status">Status</label><select className={control} id="status" value={form.status} onChange={event => change({ status: Number(event.target.value) as BreedingStatus })}><option value="1">Planejado</option><option value="2">Em andamento</option><option value="3">Concluído</option><option value="4">Cancelado</option></select></div>
          <div><label htmlFor="start">Data de início</label><input className={control} id="start" type="date" value={form.dataInicio ?? ''} onChange={event => change({ dataInicio: event.target.value || null })} /></div>
          <div><label htmlFor="end">Data de fim</label><input className={control} id="end" type="date" value={form.dataFim ?? ''} onChange={event => change({ dataFim: event.target.value || null })} /></div>
        </div>
        <div className="mt-4"><label htmlFor="goal">Objetivo</label><input className={control} id="goal" maxLength={500} value={form.objetivo ?? ''} onChange={event => change({ objetivo: event.target.value || null })} /></div>
        <div className="mt-4"><label htmlFor="notes">Observação</label><textarea className={control} id="notes" maxLength={2000} rows={4} value={form.observacao ?? ''} onChange={event => change({ observacao: event.target.value || null })} /></div>
      </fieldset>
      <div className="mt-6 flex gap-3"><button className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white" disabled={saving} type="submit">{saving ? 'Salvando…' : 'Salvar'}</button><Link className="rounded-lg border px-4 py-2 text-sm font-semibold" to={returnTarget.to} state={returnTarget.state}>Cancelar</Link></div>
    </form>}
  </div></main>
}
