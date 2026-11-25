import { formReturnTarget, listReturnState, savedRecordState } from '../../../shared/details/listNavigation'
import { DetailStatus } from '../../../shared/details/DetailsPage'
import { useUnsavedChanges } from '../../../shared/navigation/useUnsavedChanges'
import { PageHeader } from '../../../shared/layout/PageHeader'
import { ImageGallery } from '../../animals/evolution/ImageGallery'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { isHttpError } from '../../../shared/http/httpErrors'
import { listEspecies } from '../../species/services/speciesService'
import { VarietyRecordTabs, type VarietyRecordTab } from '../components/VarietyRecordTabs'
import { readVarietyImageContext } from '../navigation/varietyImageNavigation'
import { createVariedade, getVariedadeById, updateVariedade } from '../services/varietiesService'
import type { Variedade } from '../types/varieties'

type LoadState = 'loading' | 'ready' | 'not-found' | 'error'
type FieldId = 'variety-species' | 'variety-name'

interface SpeciesOption {
  id: string
  nomeComum: string
  ativo: boolean
}

const SPECIES_PAGE_SIZE = 100
const FORM_ID = 'variety-form'

async function loadActiveSpecies(): Promise<SpeciesOption[]> {
  const loaded: SpeciesOption[] = []
  let page = 1
  let totalPages = 1
  do {
    const response = await listEspecies({ page, pageSize: SPECIES_PAGE_SIZE, ativo: true })
    loaded.push(...response.items.filter((item) => item.ativo).map(({ id, nomeComum, ativo }) => ({ id, nomeComum, ativo })))
    totalPages = response.totalPages
    page += 1
  } while (page <= totalPages)
  return loaded
}

export function VarietyFormPage() {
  const { id } = useParams<{ id: string }>()
  return <VarietyForm id={id} key={id} />
}

function VarietyForm({ id }: { id?: string }) {
  const navigate = useNavigate()
  const location = useLocation()
  const isEdit = id !== undefined
  const imageContext = readVarietyImageContext(location.state, location.pathname)
  const [tab, setTab] = useState<VarietyRecordTab>(imageContext ? 'imagens' : 'ficha')
  const returnTarget = formReturnTarget(location.state, '/variedades', id ? `/variedades/${id}` : undefined)
  const listState = listReturnState(location.state, '/variedades')
  const [dirty, setDirty] = useState(false)
  const { markSaved } = useUnsavedChanges(dirty)
  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [retryKey, setRetryKey] = useState(0)
  const [currentVariety, setCurrentVariety] = useState<Variedade | null>(null)
  const [speciesOptions, setSpeciesOptions] = useState<SpeciesOption[]>([])
  const [especieId, setEspecieId] = useState('')
  const [nome, setNome] = useState('')
  const [especieIdError, setEspecieIdError] = useState(false)
  const [nomeError, setNomeError] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [focusField, setFocusField] = useState<FieldId | null>(null)
  const form = useRef<HTMLFormElement>(null)
  const mounted = useRef(false)

  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false }
  }, [])

  useEffect(() => {
    if (!focusField) return
    const field = form.current?.elements.namedItem(focusField)
    if (field instanceof HTMLElement) field.focus()
    setFocusField(null)
  }, [focusField])

  useEffect(() => {
    let isCurrent = true
    const load = async () => {
      setLoadState('loading')
      setSaveError(null)
      let variety: Variedade | null = null
      if (id) {
        try {
          variety = await getVariedadeById(id)
        } catch (error: unknown) {
          if (isCurrent) setLoadState(isHttpError(error) && error.status === 404 ? 'not-found' : 'error')
          return
        }
      }

      if (!isCurrent) return
      try {
        const activeSpecies = await loadActiveSpecies()
        if (!isCurrent) return
        const options = [...activeSpecies]
        if (variety && !variety.especie.ativo && !options.some((option) => option.id === variety.especie.id)) {
          options.push({ ...variety.especie })
        }
        setSpeciesOptions(options)
        setCurrentVariety(variety)
        setEspecieId(variety?.especieId ?? '')
        setNome(variety?.nome ?? '')
        setLoadState('ready')
      } catch {
        if (isCurrent) setLoadState('error')
      }
    }
    void load()
    return () => { isCurrent = false }
  }, [id, retryKey])

  function focusGeneralField(field: FieldId) {
    setTab('ficha')
    setFocusField(field)
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (isSaving || loadState !== 'ready') return
    const normalizedName = nome.trim().replace(/\s+/g, ' ')
    const selectedSpecies = speciesOptions.find((option) => option.id === especieId)
    const canKeepCurrentInactive = isEdit && currentVariety?.especieId === especieId
    const hasSpeciesError = !selectedSpecies || (!selectedSpecies.ativo && !canKeepCurrentInactive)
    const hasNameError = normalizedName.length < 1 || normalizedName.length > 200
    setEspecieIdError(hasSpeciesError)
    setNomeError(hasNameError)
    setSaveError(null)
    if (hasSpeciesError || hasNameError) {
      focusGeneralField(hasSpeciesError ? 'variety-species' : 'variety-name')
      return
    }

    const request = { especieId, nome: normalizedName }
    setIsSaving(true)
    try {
      if (isEdit && id) await updateVariedade(id, request)
      else await createVariedade(request)
      if (!mounted.current) return
      markSaved(); setDirty(false); navigate(returnTarget.to, { state: savedRecordState(returnTarget.state, returnTarget.to) })
    } catch (error: unknown) {
      if (!mounted.current) return
      if (isHttpError(error) && error.status === 409) {
        const speciesInUse = error.detail === 'Variety is referenced by an animal'
        setSaveError(speciesInUse
          ? 'A espécie não pode ser alterada enquanto a variedade estiver vinculada a um animal.'
          : 'Já existe uma variedade com esse nome para a espécie selecionada.')
        focusGeneralField(speciesInUse ? 'variety-species' : 'variety-name')
      } else if (isHttpError(error) && isEdit && error.status === 404) setSaveError('Variedade não encontrada.')
      else if (isHttpError(error) && error.status === 400) setSaveError('Os dados informados para a variedade são inválidos.')
      else setSaveError('Não foi possível salvar a variedade.')
    } finally {
      if (mounted.current) setIsSaving(false)
    }
  }

  const controlClassName = 'mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-900 focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:cursor-not-allowed disabled:bg-slate-100'
  const returnLink = <Link className="inline-flex rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2" to={listState?.list.path ?? '/variedades'} state={listState}>Voltar para Variedades</Link>

  if (loadState === 'loading') return <main className="flex min-h-screen items-center justify-center bg-slate-50 px-6"><p className="text-slate-600" role="status">{isEdit ? 'Carregando variedade…' : 'Carregando espécies…'}</p></main>
  if (isEdit && loadState === 'not-found') return <main className="min-h-screen bg-slate-50 px-4 py-8"><section className="mx-auto max-w-2xl rounded-2xl border border-slate-200 bg-white p-8 shadow-sm"><h1 className="text-2xl font-bold text-slate-900">Variedade não encontrada.</h1><div className="mt-6">{returnLink}</div></section></main>
  if (loadState === 'error') return <main className="min-h-screen bg-slate-50 px-4 py-8"><section className="mx-auto max-w-2xl rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm"><p className="font-medium text-red-700" role="alert">Não foi possível carregar {isEdit ? 'a variedade' : 'as espécies'}.</p><button className="mt-4 rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-semibold text-red-700 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-600 focus:ring-offset-2" onClick={() => setRetryKey((current) => current + 1)} type="button">Tentar novamente</button></section></main>

  const formActions = <>
    <button className="button-primary disabled:opacity-60" disabled={isSaving} form={FORM_ID} type="submit">{isSaving ? 'Salvando…' : 'Salvar'}</button>
    <Link className="button-secondary" to={returnTarget.to} state={returnTarget.state}>Cancelar</Link>
  </>
  const saveFeedback = saveError && <p className="text-sm font-medium text-red-700" role="alert">{saveError}</p>
  const generalForm = <form className="space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
    id={FORM_ID} noValidate onChangeCapture={() => setDirty(true)} onSubmit={handleSubmit} ref={form}>
    <div>
      <label className="text-sm font-medium text-slate-700" htmlFor="variety-species">Espécie</label>
      <select aria-describedby={especieIdError ? 'variety-species-error' : undefined} aria-invalid={especieIdError} className={controlClassName}
        id="variety-species" onChange={(event) => { setEspecieId(event.target.value); setEspecieIdError(false) }} required value={especieId}>
        <option value="">Selecione uma espécie</option>
        {speciesOptions.map((option) => <option key={option.id} value={option.id}>{option.nomeComum}{option.ativo ? '' : ' (inativa)'}</option>)}
      </select>
      {especieIdError && <p className="mt-1 text-sm text-red-700" id="variety-species-error">Selecione uma espécie ativa.</p>}
    </div>
    <div>
      <label className="text-sm font-medium text-slate-700" htmlFor="variety-name">Nome</label>
      <input aria-describedby={nomeError ? 'variety-name-error' : undefined} aria-invalid={nomeError} className={controlClassName}
        id="variety-name" maxLength={200} minLength={1} onChange={(event) => { setNome(event.target.value); setNomeError(false) }} required type="text" value={nome} />
      {nomeError && <p className="mt-1 text-sm text-red-700" id="variety-name-error">Informe um nome entre 1 e 200 caracteres.</p>}
    </div>
    {!isEdit && <>{saveFeedback}<div className="flex flex-wrap gap-3">{formActions}</div></>}
  </form>

  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8"><div className={isEdit ? 'mx-auto max-w-5xl' : 'mx-auto max-w-2xl'}>
    <PageHeader title={isEdit ? 'Editar variedade' : 'Nova variedade'}
      context={currentVariety && <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <strong className="text-slate-900 [overflow-wrap:anywhere]">{currentVariety.nome}</strong>
        <DetailStatus active={currentVariety.ativo} />
      </div>}
      back={{ to: returnTarget.to, state: returnTarget.state, label: id && returnTarget.to === `/variedades/${id}` ? 'Voltar para variedade' : 'Voltar para Variedades' }}
      actions={isEdit ? formActions : undefined} />
    <div className="mt-8 space-y-4">
      {isEdit && saveFeedback}
      {currentVariety ? <VarietyRecordTabs activeTab={tab} onSelect={setTab} general={generalForm}
        images={<ImageGallery ownerId={currentVariety.id} kind="variedades" imageContext={imageContext} panelActive={tab === 'imagens'} />}
      /> : generalForm}
    </div>
  </div></main>
}
