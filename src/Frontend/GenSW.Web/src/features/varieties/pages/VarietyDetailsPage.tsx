import { useParams } from 'react-router-dom'
import { DetailsPage, DetailField, DetailStatus } from '../../../shared/details/DetailsPage'
import { useRecordDetails } from '../../../shared/details/useRecordDetails'
import { ImageGallery } from '../../animals/evolution/ImageGallery'
import { getVariedadeById } from '../services/varietiesService'

export function VarietyDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record: variedade, state, retry } = useRecordDetails(id, getVariedadeById)
  return <DetailsPage title="Detalhes da variedade" listPath="/variedades" listLabel="Variedades" state={state} onRetry={retry} editPath={variedade ? `/variedades/${variedade.id}/editar` : undefined}>
    {variedade ? <>
      <dl className="grid gap-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:grid-cols-2">
        <DetailField label="Nome">{variedade.nome}</DetailField>
        <DetailField label="Status"><DetailStatus active={variedade.ativo} /></DetailField>
        <DetailField label="Espécie">{variedade.especie.nomeComum}{variedade.especie.ativo ? '' : ' (inativa)'}</DetailField>
      </dl>
      <ImageGallery ownerId={variedade.id} kind="variedades" readOnly />
    </> : null}
  </DetailsPage>
}
