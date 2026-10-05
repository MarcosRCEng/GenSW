import { useParams } from 'react-router-dom'
import { DetailField, DetailStatus, DetailsPage } from '../../shared/details/DetailsPage'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { getPropriedadeById } from './propertiesService'

export function PropertyDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record: propriedade, state, retry } = useRecordDetails(id, getPropriedadeById)
  const formatDate = (value: string) => new Date(value).toLocaleString('pt-BR')
  return <DetailsPage title="Visualizar propriedade" listPath="/propriedades" listLabel="Propriedades" state={state} onRetry={retry} editPath={propriedade ? `/propriedades/${propriedade.id}/editar` : undefined}>
    {propriedade && <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <p className="mb-6 text-sm text-slate-600">Unidade operacional física</p>
      <dl className="grid gap-6 sm:grid-cols-2">
        <DetailField label="Nome">{propriedade.nome}</DetailField>
        <DetailField label="Status"><DetailStatus active={propriedade.ativo} /></DetailField>
        <DetailField label="Localização">{propriedade.localizacao}</DetailField>
        <DetailField label="Observação">{propriedade.observacao}</DetailField>
        <DetailField label="Cadastro">{formatDate(propriedade.createdAtUtc)}</DetailField>
        <DetailField label="Última atualização">{formatDate(propriedade.updatedAtUtc)}</DetailField>
      </dl>
      {!propriedade.ativo && <p className="mt-6 text-sm text-slate-600">Esta propriedade está inativa. Os vínculos existentes e o histórico dos animais são preservados.</p>}
    </section>}
  </DetailsPage>
}
