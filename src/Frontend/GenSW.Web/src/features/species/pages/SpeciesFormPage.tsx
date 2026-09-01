import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { isHttpError } from '../../../shared/http/httpErrors'
import { createEspecie, getEspecieById, updateEspecie } from '../services/speciesService'

type LoadState = 'loading' | 'ready' | 'not-found' | 'error'

export function SpeciesFormPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const isEdit = id !== undefined
  const [loadState, setLoadState] = useState<LoadState>(isEdit ? 'loading' : 'ready')
  const [retryKey, setRetryKey] = useState(0)
  const [nomeComum, setNomeComum] = useState('')
  const [nomeCientifico, setNomeCientifico] = useState('')
  const [nomeComumError, setNomeComumError] = useState(false)
  const [nomeCientificoError, setNomeCientificoError] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    if (!id) return
    let isCurrent = true
    setLoadState('loading')
    setSaveError(null)
    void getEspecieById(id).then((especie) => {
      if (!isCurrent) return
      setNomeComum(especie.nomeComum)
      setNomeCientifico(especie.nomeCientifico ?? '')
      setLoadState('ready')
    }).catch((error: unknown) => {
      if (isCurrent) setLoadState(isHttpError(error) && error.status === 404 ? 'not-found' : 'error')
    })
    return () => { isCurrent = false }
  }, [id, retryKey])

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (isSaving || loadState !== 'ready') return
    const normalizedCommonName = nomeComum.trim().replace(/\s+/g, ' ')
    const normalizedScientificName = nomeCientifico.trim().replace(/\s+/g, ' ')
    const hasCommonNameError = normalizedCommonName.length < 1 || normalizedCommonName.length > 200
    const hasScientificNameError = normalizedScientificName.length > 200
    setNomeComumError(hasCommonNameError)
    setNomeCientificoError(hasScientificNameError)
    setSaveError(null)
    if (hasCommonNameError || hasScientificNameError) return

    const request = { nomeComum: normalizedCommonName, nomeCientifico: normalizedScientificName || null }
    setIsSaving(true)
    try {
      if (isEdit && id) await updateEspecie(id, request)
      else await createEspecie(request)
      navigate('/especies')
    } catch (error: unknown) {
      if (isHttpError(error) && error.status === 409) setSaveError('Já existe uma espécie com esse nome comum ou científico.')
      else if (isHttpError(error) && isEdit && error.status === 404) setSaveError('Espécie não encontrada.')
      else setSaveError('Não foi possível salvar a espécie.')
    } finally {
      setIsSaving(false)
    }
  }

  const controlClassName = 'mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-900 focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:cursor-not-allowed disabled:bg-slate-100'
  const returnLink = <Link className="inline-flex rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2" to="/especies">Voltar para Espécies</Link>

  if (isEdit && loadState === 'loading') return <main className="flex min-h-screen items-center justify-center bg-slate-50 px-6"><p className="text-slate-600" role="status">Carregando espécie…</p></main>
  if (isEdit && loadState === 'not-found') return <main className="min-h-screen bg-slate-50 px-4 py-8"><section className="mx-auto max-w-2xl rounded-2xl border border-slate-200 bg-white p-8 shadow-sm"><h1 className="text-2xl font-bold text-slate-900">Espécie não encontrada.</h1><div className="mt-6">{returnLink}</div></section></main>
  if (isEdit && loadState === 'error') return <main className="min-h-screen bg-slate-50 px-4 py-8"><section className="mx-auto max-w-2xl rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm"><p className="font-medium text-red-700" role="alert">Não foi possível carregar a espécie.</p><button className="mt-4 rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-semibold text-red-700 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-600 focus:ring-offset-2" onClick={() => setRetryKey((current) => current + 1)} type="button">Tentar novamente</button></section></main>

  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8"><div className="mx-auto max-w-2xl">
    <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p>
    <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-900">{isEdit ? 'Editar espécie' : 'Nova espécie'}</h1>
    <form className="mt-8 space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm" noValidate onSubmit={handleSubmit}>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="species-common-name">Nome comum</label><input aria-invalid={nomeComumError} className={controlClassName} id="species-common-name" maxLength={200} minLength={1} onChange={(event) => { setNomeComum(event.target.value); setNomeComumError(false) }} required type="text" value={nomeComum} />{nomeComumError ? <p className="mt-1 text-sm text-red-700">Informe um nome comum entre 1 e 200 caracteres.</p> : null}</div>
      <div><label className="text-sm font-medium text-slate-700" htmlFor="species-scientific-name">Nome científico</label><input aria-invalid={nomeCientificoError} className={controlClassName} id="species-scientific-name" maxLength={200} onChange={(event) => { setNomeCientifico(event.target.value); setNomeCientificoError(false) }} type="text" value={nomeCientifico} />{nomeCientificoError ? <p className="mt-1 text-sm text-red-700">Informe um nome científico com até 200 caracteres.</p> : null}</div>
      {saveError ? <p className="text-sm font-medium text-red-700" role="alert">{saveError}</p> : null}
      <div className="flex flex-wrap gap-3"><button className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60" disabled={isSaving} type="submit">{isSaving ? 'Salvando…' : 'Salvar'}</button><Link className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2" to="/especies">Cancelar</Link></div>
    </form>
  </div></main>
}
