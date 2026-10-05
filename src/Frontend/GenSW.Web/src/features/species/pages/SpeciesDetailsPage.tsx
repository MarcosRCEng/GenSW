import { useParams } from 'react-router-dom'
import { DetailsPage, DetailField, DetailStatus } from '../../../shared/details/DetailsPage'
import { useRecordDetails } from '../../../shared/details/useRecordDetails'
import { getEspecieById } from '../services/speciesService'

export function SpeciesDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record: especie, state, retry } = useRecordDetails(id, getEspecieById)
  return <DetailsPage title="Detalhes da espécie" listPath="/especies" listLabel="Espécies" state={state} onRetry={retry} editPath={especie ? `/especies/${especie.id}/editar` : undefined}>
    {especie ? <dl className="grid gap-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:grid-cols-2">
      <DetailField label="Nome comum">{especie.nomeComum}</DetailField>
      <DetailField label="Nome científico">{especie.nomeCientifico}</DetailField>
      <DetailField label="Status"><DetailStatus active={especie.ativo} /></DetailField>
      <DetailField label="Ovípara">{especie.ovipara === undefined ? null : especie.ovipara ? 'Sim' : 'Não'}</DetailField>
      {especie.ovipara ? <DetailField label="Peso padrão do ovo">{especie.pesoPadraoOvoGramas == null ? null : `${especie.pesoPadraoOvoGramas.toLocaleString('pt-BR')} g`}</DetailField> : null}
    </dl> : null}
  </DetailsPage>
}
