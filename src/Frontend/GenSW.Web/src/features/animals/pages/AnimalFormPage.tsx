import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { isHttpError } from '../../../shared/http/httpErrors'
import { listRacas } from '../../breeds/services/breedsService'
import type { Raca } from '../../breeds/types/breeds'
import { listEspecies } from '../../species/services/speciesService'
import type { Especie } from '../../species/types/species'
import { listVariedades } from '../../varieties/services/varietiesService'
import type { Variedade } from '../../varieties/types/varieties'
import { createAnimal, getAnimalById, updateAnimal } from '../services/animalsService'
import type { Animal, EscopoAnimal, SexoAnimal } from '../types/animals'

type LoadState = 'loading' | 'ready' | 'not-found' | 'error'
const CATALOG_PAGE_SIZE = 100

async function loadAllActive<T>(load: (params: { page: number; pageSize: number; ativo: boolean }) => Promise<{ items: T[]; totalPages: number }>): Promise<T[]> {
  const loaded: T[] = []
  let page = 1
  let totalPages = 1
  do {
    const response = await load({ page, pageSize: CATALOG_PAGE_SIZE, ativo: true })
    loaded.push(...response.items)
    totalPages = response.totalPages
    page += 1
  } while (page <= totalPages)
  return loaded
}

function normalizeText(value: string) { return value.trim().replace(/\s+/g, ' ') }
function label(name: string, ativo: boolean) { return ativo ? name : `${name} (inativa)` }
function appendCurrent<T extends { id: string; ativo: boolean }>(items: T[], current: T | null): T[] {
  return current && !current.ativo ? [...items.filter((item) => item.id !== current.id), current] : items
}

export function AnimalFormPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const isEdit = id !== undefined
  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [retryKey, setRetryKey] = useState(0)
  const [currentAnimal, setCurrentAnimal] = useState<Animal | null>(null)
  const [species, setSpecies] = useState<Especie[]>([])
  const [breeds, setBreeds] = useState<Raca[]>([])
  const [varieties, setVarieties] = useState<Variedade[]>([])
  const [codigoInterno, setCodigoInterno] = useState('')
  const [codigoTouched, setCodigoTouched] = useState(false)
  const [nome, setNome] = useState('')
  const [especieId, setEspecieId] = useState('')
  const [racaId, setRacaId] = useState('')
  const [variedadeId, setVariedadeId] = useState('')
  const [sexo, setSexo] = useState<SexoAnimal>(3)
  const [dataNascimento, setDataNascimento] = useState('')
  const [escopo, setEscopo] = useState<EscopoAnimal>(1)
  const [codigoError, setCodigoError] = useState(false)
  const [especieError, setEspecieError] = useState(false)
  const [classificationError, setClassificationError] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    let current = true
    const load = async () => {
      setLoadState('loading'); setSaveError(null)
      let animal: Animal | null = null
      if (id) {
        try { animal = await getAnimalById(id) }
        catch (error: unknown) { if (current) setLoadState(isHttpError(error) && error.status === 404 ? 'not-found' : 'error'); return }
      }
      try {
        const [activeSpecies, activeBreeds, activeVarieties] = await Promise.all([loadAllActive(listEspecies), loadAllActive(listRacas), loadAllActive(listVariedades)])
        if (!current) return
        const historicalSpecies = animal && !animal.especie.ativo ? { id: animal.especie.id, nomeComum: animal.especie.nomeComum, nomeCientifico: null, ativo: false, createdAtUtc: animal.createdAtUtc, updatedAtUtc: animal.updatedAtUtc } : null
        const historicalBreed = animal?.raca && !animal.raca.ativo ? { id: animal.raca.id, especieId: animal.especieId, nome: animal.raca.nome, ativo: false, createdAtUtc: animal.createdAtUtc, updatedAtUtc: animal.updatedAtUtc, especie: { id: animal.especieId, nomeComum: animal.especie.nomeComum, ativo: animal.especie.ativo } } : null
        const historicalVariety = animal?.variedade && !animal.variedade.ativo ? { id: animal.variedade.id, especieId: animal.especieId, nome: animal.variedade.nome, ativo: false, createdAtUtc: animal.createdAtUtc, updatedAtUtc: animal.updatedAtUtc, especie: { id: animal.especieId, nomeComum: animal.especie.nomeComum, ativo: animal.especie.ativo } } : null
        setSpecies(appendCurrent(activeSpecies, historicalSpecies)); setBreeds(appendCurrent(activeBreeds, historicalBreed)); setVarieties(appendCurrent(activeVarieties, historicalVariety))
        setCurrentAnimal(animal); setCodigoInterno(animal?.codigoInterno ?? ''); setNome(animal?.nome ?? ''); setEspecieId(animal?.especieId ?? ''); setRacaId(animal?.racaId ?? ''); setVariedadeId(animal?.variedadeId ?? ''); setSexo(animal?.sexo ?? 3); setDataNascimento(animal?.dataNascimento ?? ''); setEscopo(animal?.escopo ?? 1)
        setLoadState('ready')
      } catch { if (current) setLoadState('error') }
    }
    void load()
    return () => { current = false }
  }, [id, retryKey])

  const selectedBreed = breeds.find((item) => item.id === racaId)
  const selectedVariety = varieties.find((item) => item.id === variedadeId)
  const breedIncompatible = racaId !== '' && (!selectedBreed || selectedBreed.especieId !== especieId)
  const varietyIncompatible = variedadeId !== '' && (!selectedVariety || selectedVariety.especieId !== especieId)
  const visibleBreeds = breeds.filter((item) => item.especieId === especieId || item.id === racaId)
  const visibleVarieties = varieties.filter((item) => item.especieId === especieId || item.id === variedadeId)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (isSaving || loadState !== 'ready') return
    const normalizedCode = normalizeText(codigoInterno)
    const selectedSpecies = species.find((item) => item.id === especieId)
    const currentInactiveSpecies = isEdit && currentAnimal?.especieId === especieId && currentAnimal.especie.ativo === false
    const invalidCode = (isEdit && normalizedCode.length === 0) || (!isEdit && codigoTouched && normalizedCode.length === 0)
    const invalidSpecies = !selectedSpecies || (!selectedSpecies.ativo && !currentInactiveSpecies)
    const invalidClassifications = breedIncompatible || varietyIncompatible || (selectedBreed !== undefined && !selectedBreed.ativo && currentAnimal?.racaId !== selectedBreed.id) || (selectedVariety !== undefined && !selectedVariety.ativo && currentAnimal?.variedadeId !== selectedVariety.id)
    setCodigoError(invalidCode); setEspecieError(invalidSpecies); setClassificationError(invalidClassifications); setSaveError(null)
    if (invalidCode || invalidSpecies || invalidClassifications) return

    const common = { nome: normalizeText(nome) || null, especieId, racaId: racaId || null, variedadeId: variedadeId || null, sexo, dataNascimento: dataNascimento || null, escopo }
    const request = isEdit ? { codigoInterno: normalizedCode, ...common } : normalizedCode ? { codigoInterno: normalizedCode, ...common } : common
    setIsSaving(true)
    try {
      if (isEdit && id) await updateAnimal(id, request as Parameters<typeof updateAnimal>[1])
      else await createAnimal(request)
      navigate('/animais')
    } catch (error: unknown) {
      if (isHttpError(error) && error.status === 409) setSaveError('Já existe um animal com esse código interno.')
      else if (isHttpError(error) && error.status === 404) setSaveError('Animal ou classificação não encontrada.')
      else if (isHttpError(error) && error.status === 400) setSaveError('Os dados informados para o animal são inválidos.')
      else setSaveError('Não foi possível salvar o animal.')
    } finally { setIsSaving(false) }
  }

  const control = 'mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-900 focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:cursor-not-allowed disabled:bg-slate-100'
  const backLink = <Link className="inline-flex rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700" to="/animais">Voltar para Animais</Link>
  if (loadState === 'loading') return <main className="flex min-h-screen items-center justify-center bg-slate-50 px-6"><p role="status">Carregando {isEdit ? 'animal' : 'catálogos'}…</p></main>
  if (isEdit && loadState === 'not-found') return <main className="min-h-screen bg-slate-50 px-4 py-8"><section className="mx-auto max-w-2xl rounded-2xl border border-slate-200 bg-white p-8 shadow-sm"><h1>Animal não encontrado.</h1>{backLink}</section></main>
  if (loadState === 'error') return <main className="min-h-screen bg-slate-50 px-4 py-8"><section className="mx-auto max-w-2xl rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm"><p role="alert">Não foi possível carregar {isEdit ? 'o animal' : 'os catálogos'}.</p><button onClick={() => setRetryKey((key) => key + 1)} type="button">Tentar novamente</button></section></main>

  return <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8"><div className="mx-auto max-w-2xl"><p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p><h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-900">{isEdit ? 'Editar animal' : 'Novo animal'}</h1><form className="mt-8 space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm" noValidate onSubmit={handleSubmit}>
    <div><label className="text-sm font-medium text-slate-700" htmlFor="animal-code">Código interno</label><input aria-invalid={codigoError} className={control} id="animal-code" onChange={(event) => { setCodigoInterno(event.target.value); setCodigoTouched(true); setCodigoError(false) }} onFocus={() => setCodigoTouched(true)} type="text" value={codigoInterno} />{!isEdit && !codigoTouched && codigoInterno === '' ? <p className="mt-1 text-sm text-slate-600">O código será gerado automaticamente se permanecer vazio.</p> : null}{codigoError ? <p className="mt-1 text-sm text-red-700">Informe um código interno não vazio.</p> : null}</div>
    <div><label className="text-sm font-medium text-slate-700" htmlFor="animal-name">Nome</label><input className={control} id="animal-name" maxLength={200} onChange={(event) => setNome(event.target.value)} type="text" value={nome} /></div>
    <div><label className="text-sm font-medium text-slate-700" htmlFor="animal-species">Espécie</label><select aria-invalid={especieError} className={control} id="animal-species" onChange={(event) => { setEspecieId(event.target.value); setEspecieError(false); setClassificationError(false) }} required value={especieId}><option value="">Selecione uma espécie</option>{species.map((item) => <option key={item.id} value={item.id}>{label(item.nomeComum, item.ativo)}</option>)}</select>{especieError ? <p className="mt-1 text-sm text-red-700">Selecione uma espécie ativa.</p> : null}</div>
    <div><label className="text-sm font-medium text-slate-700" htmlFor="animal-breed">Raça</label><select className={control} id="animal-breed" onChange={(event) => { setRacaId(event.target.value); setClassificationError(false) }} value={racaId}><option value="">Nenhuma</option>{visibleBreeds.map((item) => <option key={item.id} value={item.id}>{label(item.nome, item.ativo)}</option>)}</select>{breedIncompatible ? <p className="mt-1 text-sm text-amber-700">A raça selecionada não pertence à espécie selecionada.</p> : null}</div>
    <div><label className="text-sm font-medium text-slate-700" htmlFor="animal-variety">Variedade</label><select className={control} id="animal-variety" onChange={(event) => { setVariedadeId(event.target.value); setClassificationError(false) }} value={variedadeId}><option value="">Nenhuma</option>{visibleVarieties.map((item) => <option key={item.id} value={item.id}>{label(item.nome, item.ativo)}</option>)}</select>{varietyIncompatible ? <p className="mt-1 text-sm text-amber-700">A variedade selecionada não pertence à espécie selecionada.</p> : null}{classificationError ? <p className="mt-1 text-sm text-red-700">Corrija ou limpe as classificações incompatíveis.</p> : null}</div>
    <div><label className="text-sm font-medium text-slate-700" htmlFor="animal-sex">Sexo</label><select className={control} id="animal-sex" onChange={(event) => setSexo(Number(event.target.value) as SexoAnimal)} value={sexo}><option value="1">Macho</option><option value="2">Fêmea</option><option value="3">Indeterminado</option></select></div>
    <div><label className="text-sm font-medium text-slate-700" htmlFor="animal-scope">Escopo</label><select className={control} id="animal-scope" onChange={(event) => setEscopo(Number(event.target.value) as EscopoAnimal)} value={escopo}><option value="1">Operacional</option><option value="2">Referência</option></select></div>
    <div><label className="text-sm font-medium text-slate-700" htmlFor="animal-birth-date">Data de nascimento</label><input className={control} id="animal-birth-date" onChange={(event) => setDataNascimento(event.target.value)} type="date" value={dataNascimento} /></div>
    {saveError ? <p className="text-sm font-medium text-red-700" role="alert">{saveError}</p> : null}<div className="flex flex-wrap gap-3"><button className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white disabled:opacity-60" disabled={isSaving} type="submit">{isSaving ? 'Salvando…' : 'Salvar'}</button>{backLink}</div>
  </form></div></main>
}
