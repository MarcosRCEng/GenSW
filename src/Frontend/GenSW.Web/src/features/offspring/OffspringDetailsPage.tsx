import { Link, useParams } from 'react-router-dom'
import { DetailField, DetailsPage } from '../../shared/details/DetailsPage'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { getOffspring } from './offspringService'

export function OffspringDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record: item, state, retry } = useRecordDetails(id, getOffspring)
  return <DetailsPage title="Visualizar prole" listPath="/proles" listLabel="Proles" state={state} onRetry={retry} editPath={id ? `/proles/${id}/editar` : undefined}>
    {item && <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"><dl className="grid gap-5 sm:grid-cols-2">
      <DetailField label="Tipo de registro">{item.tipoRegistro === 1 ? 'Individual' : 'Lote'}</DetailField>
      <DetailField label="Origem">{item.origem === 1 ? 'Nascimento' : 'Eclosão'}</DetailField>
      <DetailField label="Data">{item.data.split('-').reverse().join('/')}</DetailField>
      <DetailField label="Quantidade">{item.quantidade}</DetailField>
      {item.tipoRegistro === 2 && <DetailField label="Quantidade desdobrada">{item.quantidadeDesdobrada}</DetailField>}
      <DetailField label="Peso (g)">{item.pesoGramas}</DetailField>
      <DetailField label="Sexo">{item.sexo === 1 ? 'Macho' : item.sexo === 2 ? 'Fêmea' : 'Indeterminado'}</DetailField>
      <DetailField label="Condição">{item.condicao}</DetailField>
      <DetailField label="Observação">{item.observacao}</DetailField>
      <DetailField label="Ciclo reprodutivo"><Link className="font-semibold text-emerald-700 underline" to={`/ciclos-reprodutivos/${item.cicloReprodutivoId}`}>Visualizar ciclo reprodutivo</Link></DetailField>
      {item.loteOrigemId && <DetailField label="Lote de origem"><Link className="font-semibold text-emerald-700 underline" to={`/proles/${item.loteOrigemId}`}>Visualizar lote</Link></DetailField>}
      <DetailField label="Animal resultante">{item.animalId ? <Link className="font-semibold text-emerald-700 underline" to={`/animais/${item.animalId}`}>Visualizar animal</Link> : 'Ainda não convertido'}</DetailField>
    </dl></section>}
  </DetailsPage>
}
