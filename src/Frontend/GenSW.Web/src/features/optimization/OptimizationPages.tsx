import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { DetailField, DetailsPage } from '../../shared/details/DetailsPage'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { useRememberListState, useRestoredListState } from '../../shared/details/listNavigation'
import { TableRegion } from '../../shared/layout/TableRegion'
import { display, message, page } from '../formulation/api'
import { button, Choice, ErrorNotice, Field, Frame, Pager, primary, Section, ServerSelect } from '../formulation/FormControls'
import type { Page } from '../formulation/types'
import { getOptimization, optimizationApi, optimizationPath, outcomeLabel, outcomeLabels } from './api'
import { CoefficientMatrix } from './CoefficientMatrix'
import { OptimizationDraft } from './OptimizationDraft'
import type { Composition, OptimizationExecution, OptimizationSummary, Outcome, Solution, SolverResult } from './types'

const defaults = { page: 1, desfecho: '', de: '', ate: '', especieId: '', fase: '', sortDirection: 'desc' }
const dateTime = (value: string) => new Date(value).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' })
const cell = 'px-3 py-2 align-top'
const dietNote = 'Ótimo matemático não certifica uma dieta. Confira fontes, aplicabilidade e avaliação do responsável.'
const measure = (value?: string | null) => value == null ? '—' : display(value)

export function OptimizationsPage() {
  const restored = useRestoredListState(optimizationPath, defaults)
  const [filters, setFilters] = useState(restored)
  const [query, setQuery] = useState(restored)
  const [results, setResults] = useState<Page<OptimizationSummary> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [retry, setRetry] = useState(0)
  const navigation = useRememberListState(optimizationPath, query)
  const filter = (key: keyof typeof defaults, value: string) => setFilters((old) => ({ ...old, [key]: value }))

  useEffect(() => {
    let live = true
    setLoading(true)
    setError(null)
    void page<OptimizationSummary>(optimizationApi, {
      ...query, pageSize: 25,
      de: query.de ? `${query.de}T00:00:00-03:00` : undefined,
      ate: query.ate ? `${query.ate}T23:59:59.9999999-03:00` : undefined,
    }).then((list) => {
      if (live) setResults(list)
    }).catch((e: unknown) => {
      if (live) setError(message(e))
    }).finally(() => {
      if (live) setLoading(false)
    })
    return () => { live = false }
  }, [query, retry])

  return <Frame title="Otimização assistida">
    <Section>
      <p>Ajuste uma mistura de referência para atender às metas nutricionais informadas.</p>
      <Link className={primary} to={`${optimizationPath}/nova`}>Nova otimização</Link>
    </Section>
    <Section title="Histórico de otimizações">
      <form className="space-y-3" onSubmit={(event) => { event.preventDefault(); setQuery({ ...filters, page: 1 }) }}>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <Choice label="Desfecho" value={filters.desfecho} options={['', ...Object.keys(outcomeLabels)]} captions={{ '': 'Todos', ...outcomeLabels }} onChange={(v) => filter('desfecho', v)} />
          <Field label="Data inicial (AAAA-MM-DD)" value={filters.de} onChange={(v) => filter('de', v)} />
          <Field label="Data final (AAAA-MM-DD)" value={filters.ate} onChange={(v) => filter('ate', v)} />
          <ServerSelect<{ id: string; nomeComum: string }> label="Espécie" path="/especies" value={filters.especieId || null} caption={(s) => s.nomeComum} onChange={(v) => filter('especieId', v ?? '')} />
          <Field label="Fase de uso" value={filters.fase} onChange={(v) => filter('fase', v)} />
          <Choice label="Ordenação" value={filters.sortDirection} options={['desc', 'asc']} captions={{ desc: 'Mais recentes primeiro', asc: 'Mais antigas primeiro' }} onChange={(v) => filter('sortDirection', v)} />
        </div>
        <div className="flex flex-wrap gap-2">
          <button className={button} type="submit">Aplicar filtros</button>
          <button className={button} type="button" onClick={() => { setFilters(defaults); setQuery(defaults) }}>Limpar filtros</button>
        </div>
      </form>
      {loading && <p role="status">Carregando otimizações…</p>}
      {!loading && error && <>
        <ErrorNotice error={error} />
        <button className={button} type="button" onClick={() => setRetry((v) => v + 1)}>Tentar novamente</button>
      </>}
      {!loading && !error && results && <>
        {results.items.length === 0 ? <p>Nenhuma otimização encontrada.</p> : <ul className="divide-y divide-slate-200">
          {results.items.map((entry) => <li key={entry.id} className="flex flex-wrap items-center justify-between gap-3 py-4">
            <div className="min-w-0"><p className="font-medium">{outcomeLabel(entry.desfecho)}</p><p>{dateTime(entry.createdAtUtc)}{entry.fase ? ` · ${entry.fase}` : ''}</p></div>
            <Link className={button} to={`${optimizationPath}/${entry.id}`} state={navigation}>Visualizar otimização</Link>
          </li>)}
        </ul>}
        <Pager current={query.page} total={results.totalPages} onChange={(v) => setQuery((old) => ({ ...old, page: v }))} />
      </>}
    </Section>
  </Frame>
}

const descriptions: Record<Outcome, string> = {
  SolucaoViavel: 'As quantidades transferíveis atenderam às metas e aos limites informados.',
  InviabilidadeDemonstrada: 'As restrições informadas não admitem uma solução para os dados conferidos.',
  DadosIncompletos: 'Confira as observações, fontes e hipóteses necessárias para completar o pedido.',
  DadosIncompativeis: 'Confira os campos, as unidades e o contexto identificados no diagnóstico.',
  LimiteTempo: 'O cálculo atingiu seu limite de tempo. Um candidato apresentado aqui serve apenas para diagnóstico.',
  ErroAlgoritmo: 'O cálculo foi interrompido por uma falha. Use o identificador desta execução para consultar o diagnóstico.',
  Inconclusivo: 'O cálculo não produziu uma conclusão que possa ser usada para criar um rascunho.',
}

export function OptimizationDetailsPage() {
  const { optimizationId } = useParams()
  const { record, state, retry } = useRecordDetails(optimizationId, getOptimization)
  useEffect(() => {
    if (record?.estado !== 'EmCalculo') return
    const timer = window.setTimeout(retry, 2000)
    return () => window.clearTimeout(timer)
  }, [record?.estado, retry])
  return <DetailsPage title="Visualizar otimização" listPath={optimizationPath} listLabel="o histórico de otimizações" state={state} onRetry={retry}>
    {record && <OptimizationResult key={record.id} record={record} />}
  </DetailsPage>
}

function OptimizationResult({ record }: { record: OptimizationExecution }) {
  const outcome = record.resultado?.desfecho
  const motor = record.resultado?.motor
  const viable = outcome === 'SolucaoViavel'
  return <>
    <Section title={outcomeLabel(outcome)}>
      {outcome ? <p>{descriptions[outcome]}</p> : <p role="status">O cálculo está em andamento. Esta página consulta o resultado.</p>}
      <p className="rounded-lg bg-amber-50 p-3 text-sm">{dietNote}</p>
      <p className="text-sm">Custo e disponibilidade de estoque não foram avaliados.</p>
      <dl className="grid gap-3 sm:grid-cols-3">
        <DetailField label="Massa da mistura">{display(record.pedido.massaKg)} kg BN</DetailField>
        <DetailField label="Corte das entradas">{dateTime(record.entradas.corteUtc)}</DetailField>
        <DetailField label="Referência">{record.pedido.referenciaDescricao ?? (record.pedido.receitaVersaoId ? <Link className="underline" to={`/producao/receitas/versoes/${record.pedido.receitaVersaoId}`}>Versão da receita usada</Link> : null)}</DetailField>
      </dl>
      {record.resultado && <Link className={button} to={`${optimizationPath}/nova?${new URLSearchParams({ anterior: record.id })}`}>Preparar novo pedido a partir deste</Link>}
    </Section>
    {motor?.proof && <Section title="Prova de inviabilidade">
      <p>{motor.proof.code}</p>
      <ul className="list-disc space-y-1 pl-5">{motor.proof.details.map((detail, i) => <li key={i}>{detail}</li>)}</ul>
      {motor.proof.constraintIds.length > 0 && <details><summary className="cursor-pointer underline">Restrições da prova</summary>
        <ul className="list-disc break-all pl-5">{motor.proof.constraintIds.map((id) => <li key={id}>{id}{motor.proof?.multipliers?.[id] ? ` · multiplicador ${motor.proof.multipliers[id]}` : ''}</li>)}</ul>
      </details>}
    </Section>}
    {motor?.candidate && <SolutionDetails record={record} candidate={motor.candidate} viable={viable} />}
    <CoefficientMatrix inputs={record.entradas} />
    {(record.entradas.composicaoReferencia || record.resultado?.composicao) && <Section title="Composição complementar">
      {record.entradas.composicaoReferencia && <CompositionDetails label="Composição da referência" composition={record.entradas.composicaoReferencia} />}
      {record.resultado?.composicao && <CompositionDetails label={viable ? 'Composição da solução' : 'Composição do candidato'} composition={record.resultado.composicao} />}
    </Section>}
    <FrozenContext record={record} motor={motor} />
    {viable && <OptimizationDraft execution={record} />}
  </>
}

function CompositionDetails({ composition, label }: { composition: Composition; label: string }) {
  return <details className="rounded-lg border p-3">
    <summary className="cursor-pointer font-medium">{label}</summary>
    <div className="mt-3 space-y-3">
      <dl className="grid gap-3 sm:grid-cols-3">
        <DetailField label="Massa em base natural">{composition.massaKg == null ? 'Indeterminada' : `${display(composition.massaKg)} kg`}</DetailField>
        <DetailField label="Matéria seca">{composition.materiaSecaKg == null ? 'Indeterminada' : `${display(composition.materiaSecaKg)} kg`}</DetailField>
        <DetailField label="Umidade">{composition.umidadePercentual == null ? 'Indeterminada' : `${display(composition.umidadePercentual)}%`}</DetailField>
      </dl>
      {composition.avisos.length > 0 && <ul className="list-disc space-y-1 pl-5 text-sm">{composition.avisos.map((warning, i) => <li key={i}>{warning}</li>)}</ul>}
      <TableRegion label={label}>
        <table className="w-full text-left text-sm">
          <caption className="sr-only">{label}</caption>
          <thead className="bg-slate-50"><tr>{['Componente', 'Concentração BN', 'Concentração MS', 'Contribuição conhecida', 'Cobertura BN', 'Estado'].map((column) => <th key={column} scope="col" className={cell}>{column}</th>)}</tr></thead>
          <tbody className="divide-y divide-slate-200">{composition.componentes.map((v, i) => <tr key={i}>
            <th scope="row" className={cell}>{v.componente} · {v.metodo}{v.contexto && ` · ${v.contexto}`}</th>
            <td className={cell}>{v.valorBN == null ? 'Indeterminada' : `${display(v.valorBN)} ${v.unidade}`}{v.estimado && ' · Estimada'}</td>
            <td className={cell}>{v.valorMS == null ? 'Indeterminada' : `${display(v.valorMS)} ${v.unidade}`}{v.estimadoMS && ' · Estimada'}</td>
            <td className={cell}>{v.contribuicaoConhecida == null ? 'Indeterminada' : `${display(v.contribuicaoConhecida)} ${v.unidade.startsWith('MJ') ? 'MJ' : 'g'}`}</td>
            <td className={cell}>{v.coberturaPercentual == null ? 'Indeterminada' : `${display(v.coberturaPercentual)}%`}</td>
            <td className={cell}>{v.estado}</td>
          </tr>)}</tbody>
        </table>
      </TableRegion>
    </div>
  </details>
}

function SolutionDetails({ record, candidate, viable }: { record: OptimizationExecution; candidate: Solution; viable: boolean }) {
  const labels: Record<string, string> = {
    OtimoNumericoContinuo: 'Ótimo numérico no modelo contínuo',
    ViavelSemProvaDeOtimo: 'Candidato viável sem prova de ótimo',
    ViavelAposQuantizacao: 'Viável após arredondamento e revalidação',
  }
  const itemName = (id?: string | null) => record.entradas.candidatos.find((c) => c.id === id)?.nome ?? id ?? ''
  const goalName = (id?: string | null) => {
    const goal = record.pedido.metas.find((g) => g.id === id)
    return goal ? `${goal.componente} · ${goal.metodo}` : id ?? ''
  }
  return <>
    <Section title={viable ? 'Mistura de referência e solução' : 'Candidato para diagnóstico'}>
      {viable && <p>{labels[candidate.objectiveQuality] ?? candidate.objectiveQuality}</p>}
      <dl className="grid gap-3 sm:grid-cols-3">
        <DetailField label="Desvio total no cálculo contínuo">{measure(candidate.rawDeviationKg)} kg</DetailField>
        <DetailField label="Desvio após arredondamento">{candidate.quantizedDeviationKg == null ? 'Indeterminado' : `${display(candidate.quantizedDeviationKg)} kg`}</DetailField>
        <DetailField label="Massa redistribuída (metade do desvio)">{candidate.redistributedKg == null ? 'Indeterminado' : `${display(candidate.redistributedKg)} kg`}</DetailField>
      </dl>
      <TableRegion label="Massas da referência e do candidato">
        <table className="w-full text-left text-sm">
          <caption className="sr-only">Massas da referência e do candidato</caption>
          <thead className="bg-slate-50"><tr>{['Ingrediente', 'Referência kg BN', 'Contínuo kg BN', 'Após arredondamento kg BN', 'Resíduo kg', 'Inclusão %'].map((label) => <th className={cell} key={label} scope="col">{label}</th>)}</tr></thead>
          <tbody className="divide-y divide-slate-200">{record.pedido.candidatos.map((c) => <tr key={c.id}>
            <th scope="row" className={cell}>{itemName(c.id)}</th>
            <td className={cell}>{display(c.referenciaKg)}</td>
            <td className={cell}>{measure(candidate.rawMasses[c.id])}</td>
            <td className={cell}>{measure(candidate.quantizedMasses?.[c.id])}</td>
            <td className={cell}>{measure(candidate.residualsKg?.[c.id])}</td>
            <td className={cell}>{measure(candidate.constraints.find((constraint) => constraint.kind === 'Inclusao' && constraint.referenceId === c.id)?.value)}</td>
          </tr>)}</tbody>
        </table>
      </TableRegion>
    </Section>
    <Section title="Metas, limites e folgas">
      <TableRegion label="Restrições da solução">
        <table className="w-full text-left text-sm">
          <caption className="sr-only">Restrições da solução</caption>
          <thead className="bg-slate-50"><tr>{['Restrição', 'Valor', 'Mínimo', 'Máximo', 'Folga inferior', 'Folga superior', 'Tolerância de proximidade', 'Situação'].map((label) => <th className={cell} key={label} scope="col">{label}</th>)}</tr></thead>
          <tbody className="divide-y divide-slate-200">{candidate.constraints.map((constraint) => <tr key={constraint.id}>
            <th className={cell} scope="row">{constraint.kind === 'Massa' ? 'Balanço de massa' : constraint.kind === 'Inclusao' ? `Inclusão · ${itemName(constraint.referenceId)}` : goalName(constraint.referenceId)}</th>
            <td className={cell}>{display(constraint.value)} {constraint.unit}</td>
            <td className={cell}>{measure(constraint.minimum)}</td><td className={cell}>{measure(constraint.maximum)}</td>
            <td className={cell}>{measure(constraint.lowerSlack)}</td><td className={cell}>{measure(constraint.upperSlack)}</td>
            <td className={cell}>{measure(constraint.activityTolerance)}</td>
            <td className={cell}>{constraint.violated ? 'Violada' : constraint.active ? 'Ativa' : constraint.nearBoundary ? 'Próxima do limite' : 'Com folga'}</td>
          </tr>)}</tbody>
        </table>
      </TableRegion>
      {record.pedido.metas.map((goal) => <details key={goal.id} className="rounded-lg border p-3">
        <summary className="cursor-pointer font-medium">Contribuições · {goalName(goal.id)}</summary>
        <ul className="mt-2 list-disc space-y-1 pl-5 text-sm">{candidate.contributions.filter((c) => c.goalId === goal.id).map((c) => <li key={c.ingredientId}>
          {itemName(c.ingredientId)} · {display(c.value)} {c.unit}
        </li>)}</ul>
      </details>)}
    </Section>
  </>
}

function FrozenContext({ record, motor }: { record: OptimizationExecution; motor?: SolverResult | null }) {
  const native = motor?.native
  const metadata = native?.metadata
  return <Section title="Pedido e execução registrados">
    <dl className="grid gap-3 sm:grid-cols-3">
      <DetailField label="Identificador">{record.id}</DetailField>
      <DetailField label="Registrado em">{dateTime(record.createdAtUtc)}</DetailField>
      <DetailField label="Modelo">{motor?.model?.version ?? record.pedido.versaoModelo}</DetailField>
      <DetailField label="Espécie (identificador)">{record.pedido.especieId}</DetailField>
      <DetailField label="Fase de uso">{record.pedido.fase}</DetailField>
      <DetailField label="Preparação">{record.pedido.preparo}</DetailField>
    </dl>
    {record.pedido.metas.length > 0 && <details className="rounded-lg border p-3">
      <summary className="cursor-pointer font-medium">Metas e fontes do pedido</summary>
      <ul className="mt-3 divide-y divide-slate-200">{record.pedido.metas.map((g) => <li key={g.id} className="space-y-1 py-2 text-sm">
        <p className="font-medium">{g.componente} · {g.metodo} · {g.contexto || 'Contexto não informado'}</p>
        <p>{g.minimo != null ? `Mínimo ${display(g.minimo)} ${g.unidade} BN. ` : ''}{g.maximo != null ? `Máximo ${display(g.maximo)} ${g.unidade} BN.` : ''}</p>
        <p>{g.origem === 'ReferenciaTecnica' ? 'Referência técnica' : 'Informada pelo usuário'} · {g.aceitarEstimados ? 'Estimativas aceitas nesta meta' : 'Estimativas excluídas'}</p>
        {g.fonte && <p className="break-words">{g.fonte}</p>}
        {g.justificativa && <p className="break-words">{g.justificativa}</p>}
      </li>)}</ul>
    </details>}
    <details className="rounded-lg border p-3">
      <summary className="cursor-pointer font-medium">Identificação técnica do cálculo</summary>
      <dl className="mt-3 grid gap-3 sm:grid-cols-3">
        <DetailField label="Código do resultado">{record.resultado?.codigo}</DetailField>
        <DetailField label="Correlação do diagnóstico">{record.resultado?.correlacao}</DetailField>
        <DetailField label="Versão da aplicação">{record.resultado?.versaoAplicacao}</DetailField>
        <DetailField label="Estado do solver">{native?.rawStatus ?? native?.status}</DetailField>
        <DetailField label="Versão do solver">{metadata?.version}</DetailField>
        <DetailField label="Algoritmo">{metadata?.algorithm}</DetailField>
        <DetailField label="Tempo do solver (s)">{metadata?.seconds}</DetailField>
        <DetailField label="Orçamento do solver (s)">{metadata?.budgetSeconds}</DetailField>
        <DetailField label="Limite nativo de tempo (s)">{metadata?.nativeTimeLimitSeconds}</DetailField>
        <DetailField label="Plataforma">{metadata?.runtime}</DetailField>
        <DetailField label="Threads e semente">{metadata?.threads == null ? null : `${metadata.threads} · ${metadata.seed ?? '—'}`}</DetailField>
        <DetailField label="Tolerâncias primal e dual">{metadata?.primalTolerance == null ? null : `${metadata.primalTolerance} · ${metadata.dualTolerance ?? '—'}`}</DetailField>
        <DetailField label="Limiar de coeficiente nativo">{metadata?.smallMatrixValue}</DetailField>
        <DetailField label="Tamanho de inteiro do solver (bytes)">{metadata?.highsIntBytes}</DetailField>
        <DetailField label="SHA-256 do binário">{metadata?.binarySha256}</DetailField>
        <DetailField label="Hash do pedido">{record.hashPedido}</DetailField>
        <DetailField label="Hash das entradas">{record.resultado?.hashEntradas}</DetailField>
        <DetailField label="Hash do modelo">{record.resultado?.hashModelo}</DetailField>
        <DetailField label="Hash do modelo nativo">{record.resultado?.hashModeloNativo}</DetailField>
        <DetailField label="Hash do resultado">{record.hashResultado}</DetailField>
      </dl>
    </details>
  </Section>
}
