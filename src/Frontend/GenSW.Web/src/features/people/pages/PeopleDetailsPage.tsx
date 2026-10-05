import { useParams } from 'react-router-dom'
import { DetailsPage, DetailField, DetailStatus } from '../../../shared/details/DetailsPage'
import { useRecordDetails } from '../../../shared/details/useRecordDetails'
import { getPessoaById } from '../services/peopleService'
import { TipoPessoa } from '../types/people'

export function PeopleDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record: pessoa, state, retry } = useRecordDetails(id, getPessoaById)
  return <DetailsPage title="Detalhes da pessoa" listPath="/pessoas" listLabel="Pessoas" state={state} onRetry={retry} editPath={pessoa?.ativo ? `/pessoas/${pessoa.id}/editar` : undefined}>
    {pessoa ? <dl className="grid gap-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:grid-cols-2">
      <DetailField label="Nome">{pessoa.nome}</DetailField>
      <DetailField label="Tipo de pessoa">{pessoa.tipoPessoa === TipoPessoa.Fisica ? 'Pessoa física' : 'Pessoa jurídica'}</DetailField>
      {pessoa.tipoPessoa === TipoPessoa.Juridica ? <DetailField label="Nome fantasia">{pessoa.nomeFantasia}</DetailField> : null}
      <DetailField label="Status"><DetailStatus active={pessoa.ativo} /></DetailField>
    </dl> : null}
  </DetailsPage>
}
