import { Link, useParams } from 'react-router-dom'
import { DetailField, DetailsPage } from '../../shared/details/DetailsPage'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { getBreeding } from './breedingsService'
import type { BreedingStatus } from './types'

const labels: Record<BreedingStatus, string> = { 1: 'Planejado', 2: 'Em andamento', 3: 'Concluído', 4: 'Cancelado' }
const date = (value: string | null) => value?.split('-').reverse().join('/')
export function BreedingDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record: item, state, retry } = useRecordDetails(id, getBreeding)
  return <DetailsPage title="Visualizar cruzamento" listPath="/cruzamentos" listLabel="Cruzamentos" state={state} onRetry={retry} editPath={id ? `/cruzamentos/${id}/editar` : undefined}>
    {item && <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <dl className="grid gap-5 sm:grid-cols-2">
        <DetailField label="Macho"><Link className="font-semibold text-emerald-700 underline" to={`/animais/${item.machoId}`}>{item.macho.codigoInterno}{item.macho.nome ? ` — ${item.macho.nome}` : ''}</Link></DetailField>
        <DetailField label="Fêmea"><Link className="font-semibold text-emerald-700 underline" to={`/animais/${item.femeaId}`}>{item.femea.codigoInterno}{item.femea.nome ? ` — ${item.femea.nome}` : ''}</Link></DetailField>
        <DetailField label="Status">{labels[item.status]}</DetailField>
        <DetailField label="Data de início">{date(item.dataInicio)}</DetailField>
        <DetailField label="Data de fim">{date(item.dataFim)}</DetailField>
        <DetailField label="Objetivo">{item.objetivo}</DetailField>
        <DetailField label="Observação">{item.observacao}</DetailField>
      </dl>
      <Link className="mt-6 inline-flex rounded-lg border px-4 py-2 font-semibold text-emerald-700" to={`/ciclos-reprodutivos?cruzamentoId=${item.id}`}>Visualizar ciclos reprodutivos</Link>
      <p className="mt-4 text-sm text-slate-600">A filiação continua sendo a fonte de verdade do pedigree.</p>
    </section>}
  </DetailsPage>
}
