import { Link, useParams } from 'react-router-dom'
import { DetailField, DetailsPage } from '../../shared/details/DetailsPage'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { getReproductiveCycle } from './reproductiveCyclesService'

const date = (value: string | null) => value?.split('-').reverse().join('/')
export function ReproductiveCycleDetailsPage() {
  const { id } = useParams<{ id: string }>()
  const { record: item, state, retry } = useRecordDetails(id, getReproductiveCycle)
  return <DetailsPage title="Visualizar ciclo reprodutivo" listPath="/ciclos-reprodutivos" listLabel="Ciclos reprodutivos" state={state} onRetry={retry} editPath={id ? `/ciclos-reprodutivos/${id}/editar` : undefined}>
    {item && <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <dl className="grid gap-5 sm:grid-cols-2">
        <DetailField label="Fluxo">{item.tipo === 1 ? 'Ovíparo' : 'Gestacional'}</DetailField>
        <DetailField label="Status">{['', 'Em andamento', 'Concluído', 'Cancelado'][item.status]}</DetailField>
        <DetailField label="Cruzamento"><Link className="font-semibold text-emerald-700 underline" to={`/cruzamentos/${item.cruzamentoId}`}>Visualizar cruzamento</Link></DetailField>
        {item.tipo === 1 ? <>
          <DetailField label="Data da postura">{date(item.dataPostura)}</DetailField>
          <DetailField label="Início da incubação">{date(item.dataInicioIncubacao)}</DetailField>
          <DetailField label="Data da eclosão">{date(item.dataEclosao)}</DetailField>
          <DetailField label="Duração da incubação (dias)">{item.duracaoIncubacaoDias}</DetailField>
          <DetailField label="Ovos postos">{item.ovosPostos}</DetailField>
          <DetailField label="Ovos férteis">{item.ovosFerteis}</DetailField>
          <DetailField label="Ovos incubados">{item.ovosIncubados}</DetailField>
          <DetailField label="Ovos eclodidos">{item.ovosEclodidos}</DetailField>
          <DetailField label="Ovos inviáveis">{item.ovosInviaveis}</DetailField>
          <DetailField label="Peso médio do ovo (g)">{item.pesoMedioOvoGramas}</DetailField>
          <DetailField label="Taxa de fertilidade (%)">{item.taxaFertilidade}</DetailField>
          <DetailField label="Taxa de eclosão (%)">{item.taxaEclosao}</DetailField>
        </> : <>
          <DetailField label="Início da gestação">{date(item.dataInicioGestacao)}</DetailField>
          <DetailField label="Previsão de parto">{date(item.dataPrevistaParto)}</DetailField>
          <DetailField label="Data do parto">{date(item.dataParto)}</DetailField>
          <DetailField label="Duração da gestação (dias)">{item.duracaoGestacaoDias}</DetailField>
          <DetailField label="Nascidos">{item.nascidos}</DetailField>
          <DetailField label="Nascidos vivos">{item.nascidosVivos}</DetailField>
          <DetailField label="Nascidos mortos">{item.nascidosMortos}</DetailField>
          <DetailField label="Peso ao nascer (g)">{item.pesoAoNascerGramas}</DetailField>
        </>}
        <DetailField label="Observação">{item.observacao}</DetailField>
      </dl>
      <Link className="mt-6 inline-flex rounded-lg border px-4 py-2 font-semibold text-emerald-700" to={`/proles?cicloReprodutivoId=${item.id}`}>Visualizar proles</Link>
      <p className="mt-4 text-sm text-slate-600">A filiação permanece a fonte de verdade do pedigree; este ciclo não confirma descendentes.</p>
    </section>}
  </DetailsPage>
}
