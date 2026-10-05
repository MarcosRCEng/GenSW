import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { listReturnState } from '../../shared/details/listNavigation'
import { isHttpError } from '../../shared/http/httpErrors'
import { createPropriedade, getPropriedadeById, updatePropriedade } from './propertiesService'

export function PropertyFormPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const location = useLocation()
  const returnState = listReturnState(location.state, '/propriedades')
  const [state, setState] = useState<'loading' | 'ready' | 'not-found' | 'error'>(id ? 'loading' : 'ready')
  const [retryKey, setRetryKey] = useState(0)
  const [nome, setNome] = useState('')
  const [localizacao, setLocalizacao] = useState('')
  const [observacao, setObservacao] = useState('')
  const [nameError, setNameError] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!id) return
    let current = true
    setState('loading')
    void getPropriedadeById(id).then((property) => {
      if (!current) return
      setNome(property.nome); setLocalizacao(property.localizacao ?? ''); setObservacao(property.observacao ?? ''); setState('ready')
    }).catch((failure: unknown) => { if (current) setState(isHttpError(failure) && failure.status === 404 ? 'not-found' : 'error') })
    return () => { current = false }
  }, [id, retryKey])

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (saving || state !== 'ready') return
    const normalizedName = nome.trim().replace(/\s+/g, ' ')
    const invalidName = normalizedName.length === 0 || normalizedName.length > 200
    setNameError(invalidName); setError(null)
    if (invalidName) return
    if (localizacao.trim().length > 500 || observacao.trim().length > 2000) { setError('Informe localização com até 500 caracteres e observação com até 2000.'); return }
    setSaving(true)
    const request = { nome: normalizedName, localizacao: localizacao.trim() || null, observacao: observacao.trim() || null }
    try {
      if (id) await updatePropriedade(id, request)
      else await createPropriedade(request)
      navigate('/propriedades', { state: returnState })
    } catch (failure: unknown) {
      setError(isHttpError(failure) && failure.code === 'propriedade_duplicada' ? 'Já existe uma propriedade com esse nome.' : isHttpError(failure) && failure.status === 409 ? 'Outra operação está em andamento. Atualize e tente novamente.' : isHttpError(failure) && failure.status === 404 ? 'Propriedade não encontrada.' : 'Não foi possível salvar a propriedade.')
    } finally { setSaving(false) }
  }

  const control = 'mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-900 focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:bg-slate-100'
  const backLink = <Link className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100" to="/propriedades" state={returnState}>Voltar para Propriedades</Link>
  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6"><div className="mx-auto max-w-2xl">
    <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p>
    <h1 className="mt-2 text-3xl font-bold text-slate-900">{id ? 'Editar propriedade' : 'Nova propriedade'}</h1>
    <p className="mt-2 text-slate-600">Cadastre uma unidade operacional física.</p>
    {state === 'loading' ? <p className="mt-8" role="status">Carregando propriedade…</p> : state === 'error' || state === 'not-found' ? <section className="mt-8 space-y-4 rounded-2xl border bg-white p-6"><p role="alert">{state === 'not-found' ? 'Propriedade não encontrada.' : 'Não foi possível carregar a propriedade.'}</p><div className="flex flex-wrap gap-3">{state === 'error' && <button className="rounded-lg border px-4 py-2" onClick={() => setRetryKey((value) => value + 1)} type="button">Tentar novamente</button>}{backLink}</div></section> : <form className="mt-8 space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm" noValidate onSubmit={submit}>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="property-name">Nome</label><input aria-describedby={nameError ? 'property-name-error' : undefined} aria-invalid={nameError} className={control} disabled={saving} id="property-name" maxLength={200} onChange={(event) => { setNome(event.target.value); setNameError(false) }} required value={nome} />{nameError && <p className="mt-1 text-sm text-red-700" id="property-name-error">Informe um nome entre 1 e 200 caracteres.</p>}</div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="property-location">Localização</label><input className={control} disabled={saving} id="property-location" maxLength={500} onChange={(event) => setLocalizacao(event.target.value)} value={localizacao} /><p className="mt-1 text-sm text-slate-500">Opcional: endereço ou referência de localização da unidade.</p></div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="property-notes">Observação</label><textarea className={control} disabled={saving} id="property-notes" maxLength={2000} onChange={(event) => setObservacao(event.target.value)} rows={4} value={observacao} /></div>
      {error && <p className="text-sm font-medium text-red-700" role="alert">{error}</p>}
      <div className="flex flex-wrap gap-3"><button className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white disabled:opacity-60" disabled={saving} type="submit">{saving ? 'Salvando…' : 'Salvar'}</button>{backLink}</div>
    </form>}
  </div></main>
}
