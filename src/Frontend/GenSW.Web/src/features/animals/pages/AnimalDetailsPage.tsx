import { Link, useParams } from 'react-router-dom'
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

function EggProduction({ animalId, speciesId }: { animalId: string; speciesId: string }) {
  const { record, state, retry } = useRecordDetails(speciesId, getEspecieById)
  if (state === 'loading') return <p role="status">Consultando espécie para produção de ovos…</p>
  if (state !== 'ready') return <section className="animal-panel"><p role="alert">Não foi possível consultar a espécie para verificar a produção de ovos.</p><button onClick={retry} type="button">Tentar novamente</button></section>
  return record?.ovipara ? <AnimalEggProductionPanel animalId={animalId} readOnly /> : null
}

export function AnimalDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record: animal, state, retry } = useRecordDetails(id, getAnimalById)
  return <DetailsPage title="Visualizar animal" listPath="/animais" listLabel="Animais" state={state} onRetry={retry} editPath={id ? `/animais/${id}/editar` : undefined}>
    {animal && <>
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
      <AnimalIdentificationsPanel key={`identifications-${animal.id}`} animalId={animal.id} readOnly />
      <AnimalRegistrationsPanel key={`registrations-${animal.id}`} animalId={animal.id} readOnly />
      <WeightsPanel key={`weights-${animal.id}`} animalId={animal.id} birth={animal.dataNascimento} readOnly />
      <ImageGallery key={`images-${animal.id}`} ownerId={animal.id} kind="animais" readOnly />
      <AnimalFiliationsPanel key={`filiations-${animal.id}`} animalId={animal.id} readOnly />
      {animal.sexo === 2 && <EggProduction key={`eggs-${animal.id}`} animalId={animal.id} speciesId={animal.especieId} />}
    </>}
  </DetailsPage>
}
