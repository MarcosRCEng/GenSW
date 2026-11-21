import { useEffect, useState } from 'react'
import { listRacas } from '../../breeds/services/breedsService'
import type { Raca } from '../../breeds/types/breeds'
import { listEspecies } from '../../species/services/speciesService'
import type { Especie } from '../../species/types/species'
import { listVariedades } from '../../varieties/services/varietiesService'
import type { Variedade } from '../../varieties/types/varieties'
import type { ListAnimalsParams, SexoAnimal } from '../types/animals'

export type AnimalTaxonomyFilterValues = Pick<ListAnimalsParams, 'especieId' | 'racaId' | 'variedadeId' | 'sexo'>

const CATALOG_PAGE_SIZE = 100
const control = 'mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 disabled:cursor-not-allowed disabled:bg-slate-100'

async function loadAllPages<T>(load: (params: { page: number; pageSize: number }) => Promise<{ items: T[]; totalPages: number }>): Promise<T[]> {
  const loaded: T[] = []
  let page = 1
  let totalPages = 1
  do {
    const response = await load({ page, pageSize: CATALOG_PAGE_SIZE })
    loaded.push(...response.items)
    totalPages = response.totalPages
    page += 1
  } while (page <= totalPages)
  return loaded
}

function optionLabel(name: string, active: boolean) {
  return active ? name : `${name} (inativa)`
}

interface Props {
  idPrefix: string
  value: AnimalTaxonomyFilterValues
  onChange: (value: AnimalTaxonomyFilterValues) => void
  disabled?: boolean
}

export function AnimalTaxonomyFilters({ idPrefix, value, onChange, disabled }: Props) {
  const [species, setSpecies] = useState<Especie[]>([])
  const [breeds, setBreeds] = useState<Raca[]>([])
  const [varieties, setVarieties] = useState<Variedade[]>([])
  const [catalogError, setCatalogError] = useState(false)

  useEffect(() => {
    let current = true
    void Promise.all([loadAllPages(listEspecies), loadAllPages(listRacas), loadAllPages(listVariedades)]).then(([loadedSpecies, loadedBreeds, loadedVarieties]) => {
      if (!current) return
      setSpecies(loadedSpecies)
      setBreeds(loadedBreeds)
      setVarieties(loadedVarieties)
    }).catch(() => { if (current) setCatalogError(true) })
    return () => { current = false }
  }, [])

  return <>
    <div>
      <label className="text-sm font-medium text-slate-700" htmlFor={`${idPrefix}-species`}>Espécie</label>
      <select className={control} disabled={disabled || catalogError} id={`${idPrefix}-species`}
        onChange={event => onChange({ ...value, especieId: event.target.value || undefined })} value={value.especieId ?? ''}>
        <option value="">Todas</option>
        {species.map(item => <option key={item.id} value={item.id}>{optionLabel(item.nomeComum, item.ativo)}</option>)}
      </select>
    </div>
    <div>
      <label className="text-sm font-medium text-slate-700" htmlFor={`${idPrefix}-breed`}>Raça</label>
      <select className={control} disabled={disabled || catalogError} id={`${idPrefix}-breed`}
        onChange={event => onChange({ ...value, racaId: event.target.value || undefined })} value={value.racaId ?? ''}>
        <option value="">Todas</option>
        {breeds.map(item => <option key={item.id} value={item.id}>{optionLabel(item.nome, item.ativo)}</option>)}
      </select>
    </div>
    <div>
      <label className="text-sm font-medium text-slate-700" htmlFor={`${idPrefix}-variety`}>Variedade</label>
      <select className={control} disabled={disabled || catalogError} id={`${idPrefix}-variety`}
        onChange={event => onChange({ ...value, variedadeId: event.target.value || undefined })} value={value.variedadeId ?? ''}>
        <option value="">Todas</option>
        {varieties.map(item => <option key={item.id} value={item.id}>{optionLabel(item.nome, item.ativo)}</option>)}
      </select>
    </div>
    <div>
      <label className="text-sm font-medium text-slate-700" htmlFor={`${idPrefix}-sex`}>Sexo</label>
      <select className={control} disabled={disabled} id={`${idPrefix}-sex`}
        onChange={event => onChange({ ...value, sexo: event.target.value ? Number(event.target.value) as SexoAnimal : undefined })} value={value.sexo ?? ''}>
        <option value="">Todos</option>
        <option value="1">Macho</option>
        <option value="2">Fêmea</option>
        <option value="3">Indeterminado</option>
      </select>
    </div>
    {catalogError && <p className="col-span-full rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700" role="alert">
      Não foi possível carregar os catálogos para os filtros.
    </p>}
  </>
}
