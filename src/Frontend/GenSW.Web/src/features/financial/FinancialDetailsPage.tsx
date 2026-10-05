import { Link, useParams } from 'react-router-dom'
import { DetailField, DetailStatus, DetailsPage } from '../../shared/details/DetailsPage'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { brl, dateLabel, finance, type Category, type EntryView } from './financial'

const loadCategory = (id: string) => finance<Category>(`categorias/${id}`)
const loadEntry = (id: string) => finance<EntryView>(`lancamentos/${id}`)
const linkClass = 'font-semibold text-emerald-700 underline focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2'
const fieldsClass = 'grid gap-5 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:grid-cols-2'

export function CategoryDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record, state, retry } = useRecordDetails(id, loadCategory)
  return <DetailsPage title="Visualizar categoria financeira" listPath="/financeiro" listLabel="Financeiro" state={state} onRetry={retry}>
    {record && <dl className={fieldsClass}>
      <DetailField label="Nome">{record.nome}</DetailField>
      <DetailField label="Natureza">{record.natureza === 1 ? 'Receita' : 'Despesa'}</DetailField>
      <DetailField label="Status"><DetailStatus active={record.ativa} /></DetailField>
      <DetailField label="Código">{record.codigo}</DetailField>
    </dl>}
  </DetailsPage>
}

export function EntryDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record, state, retry } = useRecordDetails(id, loadEntry)
  const entry = record?.lancamento
  return <DetailsPage title="Visualizar lançamento financeiro" listPath="/financeiro" listLabel="Financeiro" state={state} onRetry={retry}>
    {record && entry && <dl className={fieldsClass}>
      <DetailField label="Descrição">{entry.descricao}</DetailField>
      <DetailField label="Status">{entry.cancelado ? 'Cancelado' : record.revertido ? 'Efetivo · revertido por ajuste' : 'Efetivo'}</DetailField>
      <DetailField label="Tipo">{entry.tipo === 1 ? 'Receita' : 'Despesa'}</DetailField>
      <DetailField label="Valor">{brl(entry.valor)}</DetailField>
      <DetailField label="Data do movimento">{dateLabel(entry.dataMovimento)}</DetailField>
      <DetailField label="Forma de pagamento">{['', 'Dinheiro', 'Pix', 'Transferência', 'Cartão', 'Outro'][entry.formaPagamento]}</DetailField>
      <DetailField label="Categoria"><Link className={linkClass} to={`/financeiro/categorias/${entry.categoriaId}`}>{record.categoriaNome}</Link></DetailField>
      <DetailField label="Pessoa">{entry.pessoaId && record.pessoaNome ? <Link className={linkClass} to={`/pessoas/${entry.pessoaId}`}>{record.pessoaNome}</Link> : null}</DetailField>
      <DetailField label="Animal">{entry.animalId && record.animalNome ? <Link className={linkClass} to={`/animais/${entry.animalId}`}>{record.animalNome}</Link> : null}</DetailField>
      <DetailField label="Origem">{entry.origem === 1 ? 'Lançamento manual' : entry.origem === 2 ? 'Ajuste de reversão' : 'Ajuste substituto'}</DetailField>
      <DetailField label="Observação">{entry.observacao}</DetailField>
      <DetailField label="Motivo do ajuste">{entry.motivoAjuste}</DetailField>
      {entry.lancamentoOriginalId && <DetailField label="Lançamento original"><Link className={linkClass} to={`/financeiro/lancamentos/${entry.lancamentoOriginalId}`}>Visualizar lançamento original</Link></DetailField>}
    </dl>}
  </DetailsPage>
}
