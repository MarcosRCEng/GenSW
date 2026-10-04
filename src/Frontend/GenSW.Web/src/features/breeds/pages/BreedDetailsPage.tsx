import { useParams } from 'react-router-dom'
import { DetailsPage, DetailField, DetailStatus } from '../../../shared/details/DetailsPage'
import { useRecordDetails } from '../../../shared/details/useRecordDetails'
import { getRacaById } from '../services/breedsService'

export function BreedDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record: raca, state, retry } = useRecordDetails(id, getRacaById)
  return <DetailsPage title="Detalhes da raça" listPath="/racas" listLabel="Raças" state={state} onRetry={retry} editPath={raca ? `/racas/${raca.id}/editar` : undefined}>
    {raca ? <dl className="grid gap-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:grid-cols-2">
      <DetailField label="Nome">{raca.nome}</DetailField>
      <DetailField label="Status"><DetailStatus active={raca.ativo} /></DetailField>
      <DetailField label="Espécie">{raca.especie.nomeComum}{raca.especie.ativo ? '' : ' (inativa)'}</DetailField>
    </dl> : null}
  </DetailsPage>
}
