import { useMemo, useState, type KeyboardEvent } from 'react'
import { parentReturnState, readParentReturnState, useRestoreParentSection } from '../../../shared/details/listNavigation'
import { Link, useLocation, useParams } from 'react-router-dom'
import { DetailField, DetailStatus, DetailsPage } from '../../../shared/details/DetailsPage'
import { useRecordDetails } from '../../../shared/details/useRecordDetails'
import { getEspecieById } from '../../species/services/speciesService'
import { getAnimalById } from '../services/animalsService'
import { AnimalIdentificationsPanel } from '../identifications/components/AnimalIdentificationsPanel'
import { AnimalRegistrationsPanel } from '../registrations/components/AnimalRegistrationsPanel'
import { AnimalFiliationsPanel } from '../filiations/components/AnimalFiliationsPanel'
import { AnimalEggProductionPanel } from '../egg-production/AnimalEggProductionPanel'
import { WeightsPanel } from '../evolution/WeightsPanel'
import { ImageGallery } from '../evolution/ImageGallery'
import { AnimalPropertiesPanel } from '../../properties/AnimalPropertiesPanel'
import { AnimalAnalyticsPanel } from '../analytics/AnimalAnalyticsPanel'

function EggProduction({ animalId, speciesId }: { animalId: string; speciesId: string }) {
  const { record, state, retry } = useRecordDetails(speciesId, getEspecieById)
  if (state === 'loading') return <p role="status">Consultando espécie para produção de ovos…</p>
  if (state !== 'ready') return <section className="animal-panel"><p role="alert">Não foi possível consultar a espécie para verificar a produção de ovos.</p><button onClick={retry} type="button">Tentar novamente</button></section>
  return record?.ovipara ? <AnimalEggProductionPanel animalId={animalId} readOnly /> : null
}

export function AnimalDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record: animal, state, retry } = useRecordDetails(id, getAnimalById)
  const path = `/animais/${id}`
  const location = useLocation()
  const restoredSection = readParentReturnState(location.state, path)?.parentReturn.section
  const [tab, setTab] = useState(restoredSection === 'evolucao' ? 'evolucao' : 'ficha')
  const [openedEvolution, setOpenedEvolution] = useState(restoredSection === 'evolucao')
  const animals = useMemo(() => animal ? [animal] : [], [animal])
  useRestoreParentSection(path, state === 'ready')

  function selectTab(next: 'ficha' | 'evolucao') {
    setTab(next)
    if (next === 'evolucao') setOpenedEvolution(true)
  }

  function onTabKey(event: KeyboardEvent<HTMLButtonElement>) {
    let next: 'ficha' | 'evolucao'
    if (event.key === 'Home') next = 'ficha'
    else if (event.key === 'End') next = 'evolucao'
    else if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') next = tab === 'ficha' ? 'evolucao' : 'ficha'
    else return
    event.preventDefault()
    selectTab(next)
    document.getElementById(`animal-tab-${next}`)?.focus()
  }

  return <DetailsPage title="Visualizar animal" listPath="/animais" listLabel="Animais" state={state} onRetry={retry} editPath={id ? `/animais/${id}/editar` : undefined}>
    {animal && <>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex gap-2" role="tablist" aria-label="Ficha da ave">
          <button className="analytics-button" id="animal-tab-ficha" type="button" role="tab" aria-controls="record-ficha" aria-selected={tab === 'ficha'}
            tabIndex={tab === 'ficha' ? 0 : -1} onClick={() => selectTab('ficha')} onKeyDown={onTabKey}>Ficha</button>
          <button className="analytics-button" id="animal-tab-evolucao" type="button" role="tab" aria-controls="record-evolucao" aria-selected={tab === 'evolucao'}
            tabIndex={tab === 'evolucao' ? 0 : -1} onClick={() => selectTab('evolucao')} onKeyDown={onTabKey}>Evolução</button>
        </div>
        <Link className="analytics-button inline-flex items-center font-semibold text-emerald-700" to={`/animais/comparar?animalId=${encodeURIComponent(animal.id)}`}
          state={parentReturnState(location.state, path, { section: tab })}>Comparar aves</Link>
      </div>
      <div className="space-y-6" id="record-ficha" role="tabpanel" aria-labelledby="animal-tab-ficha" tabIndex={-1} hidden={tab !== 'ficha'}>
        <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <dl className="grid gap-5 sm:grid-cols-2">
            <DetailField label="Código interno">{animal.codigoInterno}</DetailField>
            <DetailField label="Nome">{animal.nome}</DetailField>
            <DetailField label="Status"><DetailStatus active={animal.ativo} /></DetailField>
            <DetailField label="Escopo">{animal.escopo === 1 ? 'Operacional' : 'Referência'}</DetailField>
            <DetailField label="Espécie"><Link className="font-semibold text-emerald-700 underline" to={`/especies/${animal.especieId}`}>{animal.especie.nomeComum}</Link>{!animal.especie.ativo && ' (inativa)'}</DetailField>
            <DetailField label="Raça">{animal.raca ? <><Link className="font-semibold text-emerald-700 underline" to={`/racas/${animal.raca.id}`}>{animal.raca.nome}</Link>{!animal.raca.ativo && ' (inativa)'}</> : null}</DetailField>
            <DetailField label="Variedade">{animal.variedade ? <><Link className="font-semibold text-emerald-700 underline" to={`/variedades/${animal.variedade.id}`}>{animal.variedade.nome}</Link>{!animal.variedade.ativo && ' (inativa)'}</> : null}</DetailField>
            <DetailField label="Sexo">{animal.sexo === 1 ? 'Macho' : animal.sexo === 2 ? 'Fêmea' : 'Indeterminado'}</DetailField>
            <DetailField label="Data de nascimento">{animal.dataNascimento?.split('-').reverse().join('/')}</DetailField>
          </dl>
        </section>
        <AnimalPropertiesPanel key={`properties-${animal.id}`} animalId={animal.id} readOnly />
        <AnimalIdentificationsPanel key={`identifications-${animal.id}`} animalId={animal.id} readOnly />
        <AnimalRegistrationsPanel key={`registrations-${animal.id}`} animalId={animal.id} readOnly />
        <WeightsPanel key={`weights-${animal.id}`} animalId={animal.id} birth={animal.dataNascimento} readOnly />
        <ImageGallery key={`images-${animal.id}`} ownerId={animal.id} kind="animais" readOnly />
        <AnimalFiliationsPanel key={`filiations-${animal.id}`} animalId={animal.id} readOnly />
        {animal.sexo === 2 && <EggProduction key={`eggs-${animal.id}`} animalId={animal.id} speciesId={animal.especieId} />}
      </div>
      <div id="record-evolucao" role="tabpanel" aria-labelledby="animal-tab-evolucao" tabIndex={-1} hidden={tab !== 'evolucao'}>
        {openedEvolution && <AnimalAnalyticsPanel key={animal.id} animals={animals} />}
      </div>
    </>}
  </DetailsPage>
}
