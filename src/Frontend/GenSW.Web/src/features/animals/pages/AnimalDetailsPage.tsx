import { useMemo, useState } from 'react'
import { parentReturnState, readParentReturnState, recordEditState } from '../../../shared/details/listNavigation'
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
import { AnimalRecordTabs, type AnimalRecordTabId } from '../components/AnimalRecordTabs'
import type { Animal } from '../types/animals'

export function AnimalDetailsPage() {
  const { id } = useParams<{ id: string }>()
  return <AnimalDetailsRecord id={id} key={id} />
}

function AnimalDetailsRecord({ id }: { id?: string }) {
  const { record: animal, state, retry } = useRecordDetails(id, getAnimalById)
  if (animal) return <AnimalDetailsContent animal={animal} retry={retry} />
  return <DetailsPage title="Visualizar animal" listPath="/animais" listLabel="Animais" state={state} onRetry={retry} />
}

function AnimalDetailsContent({ animal, retry }: { animal: Animal; retry: () => void }) {
  const speciesId = animal.sexo === 2 ? animal.especieId : undefined
  const { record: species, state: speciesState, retry: retrySpecies } = useRecordDetails(speciesId, getEspecieById)
  const location = useLocation()
  const path = `/animais/${animal.id}`
  const restored = readParentReturnState(location.state, path)?.parentReturn
  const [tab, setTab] = useState<AnimalRecordTabId>('ficha')
  const animals = useMemo(() => [animal], [animal])
  const returnState = parentReturnState(location.state, path, restored?.section === tab ? restored : { section: tab })
  const sex = animal.sexo === 1 ? 'Macho' : animal.sexo === 2 ? 'Fêmea' : 'Indeterminado'

  return <DetailsPage title="Visualizar animal" listPath="/animais" listLabel="Animais" state="ready" onRetry={retry}
    context={<div className="flex flex-wrap items-center gap-x-3 gap-y-1">
      <strong className="text-slate-900">{animal.nome || animal.codigoInterno}</strong>
      {animal.nome && <span>{animal.codigoInterno}</span>}
      <span>{animal.especie.nomeComum} · {sex}</span>
      <DetailStatus active={animal.ativo} />
    </div>}
    actions={<>
      <Link className="button-secondary" to={`/animais/comparar?animalId=${encodeURIComponent(animal.id)}`} state={returnState}>Comparar aves</Link>
      <Link className="button-primary" to={`${path}/editar`} state={recordEditState(returnState, '/animais', path)}>Editar</Link>
    </>}
  >
    {speciesId && speciesState !== 'ready' && speciesState !== 'loading' && <div className="flex flex-wrap items-center gap-3 text-sm">
      <p role="alert">Não foi possível verificar se a aba Postura se aplica a este animal.</p>
      <button className="button-secondary" onClick={retrySpecies} type="button">Consultar espécie novamente</button>
    </div>}
    <AnimalRecordTabs initialSection={restored?.section} onTabChange={setTab} panels={{
      ficha: <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <dl className="grid gap-5 sm:grid-cols-2">
          <DetailField label="Código interno">{animal.codigoInterno}</DetailField>
          <DetailField label="Nome">{animal.nome}</DetailField>
          <DetailField label="Status"><DetailStatus active={animal.ativo} /></DetailField>
          <DetailField label="Escopo">{animal.escopo === 1 ? 'Operacional' : 'Referência'}</DetailField>
          <DetailField label="Espécie"><Link className="font-semibold text-emerald-700 underline" to={`/especies/${animal.especieId}`}>{animal.especie.nomeComum}</Link>{!animal.especie.ativo && ' (inativa)'}</DetailField>
          <DetailField label="Raça">{animal.raca ? <><Link className="font-semibold text-emerald-700 underline" to={`/racas/${animal.raca.id}`}>{animal.raca.nome}</Link>{!animal.raca.ativo && ' (inativa)'}</> : null}</DetailField>
          <DetailField label="Variedade">{animal.variedade ? <><Link className="font-semibold text-emerald-700 underline" to={`/variedades/${animal.variedade.id}`}>{animal.variedade.nome}</Link>{!animal.variedade.ativo && ' (inativa)'}</> : null}</DetailField>
          <DetailField label="Sexo">{sex}</DetailField>
          <DetailField label="Data de nascimento">{animal.dataNascimento?.split('-').reverse().join('/')}</DetailField>
        </dl>
      </section>,
      'producoes-ovos': species?.ovipara ? <AnimalEggProductionPanel animalId={animal.id} readOnly /> : undefined,
      pesagens: <WeightsPanel animalId={animal.id} birth={animal.dataNascimento} readOnly />,
      genealogia: <AnimalFiliationsPanel animalId={animal.id} readOnly />,
      propriedade: <AnimalPropertiesPanel animalId={animal.id} readOnly />,
      identificacoes: <AnimalIdentificationsPanel animalId={animal.id} readOnly />,
      registros: <AnimalRegistrationsPanel animalId={animal.id} readOnly />,
      imagens: <ImageGallery ownerId={animal.id} kind="animais" readOnly />,
      evolucao: <AnimalAnalyticsPanel animals={animals} />,
    }} />
  </DetailsPage>
}
