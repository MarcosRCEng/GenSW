import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/hooks/useAuth'
import { display, message, read } from '../formulation/api'
import { button, Check, Choice, ErrorNotice, Field, Frame, primary, Section, ServerSelect } from '../formulation/FormControls'
import type { Component, Item, Profile, Recipe, RecipeVersion } from '../formulation/types'
import { useUnsavedChanges } from '../../shared/navigation/useUnsavedChanges'
import { getOptimization, isGuid, optimizationPath, prepareOptimization, recoverOptimization, submitOptimization } from './api'
import { changedLimits, eligibleInput, importCandidates, itemLabel, kgField, newCandidate, newGoal, profileLabel, requestFromForm, type CandidateForm } from './formData'
import { CoefficientMatrix } from './CoefficientMatrix'
import { RecoveryNotice } from './RecoveryNotice'
import { useRecoverableCommand } from './useRecoverableCommand'
import type { OptimizationExecution, OptimizationGoal, OptimizationInputs, OptimizationRequest } from './types'

type Species = { id: string; nomeComum: string }

export function OptimizationFormPage() {
  const [params] = useSearchParams()
  const previous = params.get('anterior')
  return <OptimizationForm key={previous ?? 'nova'} prior={isGuid(previous) ? previous : null} />
}

function OptimizationForm({ prior }: { prior: string | null }) {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [origin, setOrigin] = useState('manual')
  const [mass, setMass] = useState('')
  const [unit, setUnit] = useState('kg')
  const [reference, setReference] = useState('')
  const [recipe, setRecipe] = useState<Recipe | null>(null)
  const [version, setVersion] = useState<RecipeVersion | null>(null)
  const [importedMass, setImportedMass] = useState<string | null>(null)
  const [species, setSpecies] = useState<string | null>(null)
  const [phase, setPhase] = useState('')
  const [preparation, setPreparation] = useState('')
  const [candidates, setCandidates] = useState<CandidateForm[]>([])
  const [goals, setGoals] = useState<OptimizationGoal[]>([])
  const [components, setComponents] = useState<Component[]>([])
  const [error, setError] = useState<string | null>(null)
  const [lookupError, setLookupError] = useState<string | null>(null)
  const [lookupLoading, setLookupLoading] = useState(true)
  const [loadingPrior, setLoadingPrior] = useState(Boolean(prior))
  const [lookupRevision, setLookupRevision] = useState(0)
  const [busy, setBusy] = useState(false)
  const [dirty, setDirty] = useState(false)
  const [review, setReview] = useState<{ body: OptimizationRequest; inputs: OptimizationInputs } | null>(null)
  const reviewSection = useRef<HTMLHeadingElement>(null)
  const unsaved = useUnsavedChanges(dirty)
  const request = useRecoverableCommand(`${user?.userId ?? 'sessao'}:execucao`, submitOptimization, recoverOptimization)
  const change = () => { setReview(null); setDirty(true); setError(null); request.clearError() }

  useEffect(() => {
    let live = true
    setLookupError(null)
    setLookupLoading(true)
    void read<Component[]>('/componentes-nutricionais').then((list) => {
      if (live) setComponents(list)
    }).catch((e: unknown) => {
      if (live) setLookupError(message(e))
    }).finally(() => {
      if (live) setLookupLoading(false)
    })
    return () => { live = false }
  }, [lookupRevision])

  useEffect(() => {
    if (!prior) return
    let live = true
    void (async () => {
      const result = await getOptimization(prior)
      const old = result.pedido
      let parent: Recipe | null = null
      let source: RecipeVersion | null = null
      if (old.receitaId && old.receitaVersaoId) {
        [parent, source] = await Promise.all([
          read<Recipe>(`/receitas/${old.receitaId}`),
          read<RecipeVersion>(`/receitas/versoes/${old.receitaVersaoId}`),
        ])
      }
      if (!live) return
      setMass(old.massaKg)
      setReference(old.referenciaDescricao ?? '')
      setSpecies(old.especieId)
      setPhase(old.fase ?? '')
      setPreparation(old.preparo ?? '')
      setGoals(old.metas)
      setOrigin(parent ? 'receita' : 'manual')
      setRecipe(parent)
      setVersion(source)
      const originalByItem = new Map(source?.conteudo.entradas.map((line) => [line.itemId, line]))
      const referencesCurrent = parent?.revisao === old.receitaRevisao && source?.revisao === old.receitaVersaoRevisao
      setImportedMass(parent && referencesCurrent ? old.massaKg : null)
      setCandidates(old.candidatos.map((c) => {
        const frozen = result.entradas.candidatos.find((item) => item.id === c.id)
        const original = originalByItem.get(c.itemId)
        return {
          ...c,
          nomeItem: frozen?.nome,
          perfil: frozen?.perfil,
          unidadeReferencia: 'kg',
          inclusaoMinima: c.inclusaoMinima ?? '0',
          inclusaoMaxima: c.inclusaoMaxima ?? '100',
          limitesImportados: original ? { min: original.inclusaoMinima ?? '0', max: original.inclusaoMaxima ?? '100' } : undefined,
        }
      }))
      if (parent && !referencesCurrent) setError('A receita de referência mudou. Importe a composição novamente para revisar as versões atuais.')
    })().catch((e: unknown) => {
      if (live) setError(message(e))
    }).finally(() => {
      if (live) setLoadingPrior(false)
    })
    return () => { live = false }
  }, [prior])

  const updateCandidate = (id: string, patch: Partial<CandidateForm>) => {
    change()
    setCandidates((lines) => lines.map((line) => line.id === id ? { ...line, ...patch } : line))
  }
  const updateGoal = (id: string, patch: Partial<OptimizationGoal>) => {
    change()
    setGoals((old) => old.map((goal) => goal.id === id ? { ...goal, ...patch } : goal))
  }

  const importRecipe = async () => {
    if (!version || busy) return
    change()
    setBusy(true)
    try {
      const normalized = kgField(mass, unit, 'Massa da mistura')
      if (normalized === '0') throw new Error('Informe uma massa positiva para a mistura.')
      const lines = await importCandidates(version, normalized)
      setCandidates(lines)
      setImportedMass(normalized)
    } catch (e) {
      setImportedMass(null)
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }

  const preview = async (event: FormEvent) => {
    event.preventDefault()
    if (busy || request.key) return
    request.clearError()
    setError(null)
    setBusy(true)
    setReview(null)
    try {
      if (origin === 'receita' && !recipe) throw new Error('Selecione uma receita e uma versão publicada.')
      const body = requestFromForm({ mass, unit, reference, recipe, version, importedMass, prior, species, phase, preparation, candidates, goals })
      const inputs = await prepareOptimization(body)
      setReview({ body, inputs })
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }

  useEffect(() => {
    if (review) reviewSection.current?.focus()
  }, [review])

  async function open(promise: Promise<OptimizationExecution | undefined> | undefined) {
    const result = await promise
    if (result) {
      unsaved.markSaved()
      navigate(`${optimizationPath}/${result.id}`)
    }
  }

  return <Frame title={prior ? 'Novo pedido a partir do resultado' : 'Nova otimização assistida'}>
    <Link className={button} to={prior ? `${optimizationPath}/${prior}` : optimizationPath}>{prior ? 'Voltar para o resultado' : 'Voltar para o histórico'}</Link>
    <RecoveryNotice keyValue={request.key} busy={request.busy} absent={request.absent} canRetry={request.canRetry} error={request.error}
      label="o pedido" onConsult={() => void open(request.consult())} onRetry={() => void open(request.retry())} onClear={request.clear} />
    {loadingPrior ? <p role="status">Carregando pedido de referência…</p> : <form className="space-y-6" onSubmit={(event) => void preview(event)}>
      <fieldset disabled={busy || Boolean(request.key)} className="min-w-0 space-y-6">
        <Section title="Mistura de referência">
          <p>Ajuste as massas dos ingredientes para atender às metas, com a menor mudança em relação à mistura de referência.</p>
          <div className="grid gap-4 sm:grid-cols-3">
            <Choice label="Origem da mistura" value={origin} options={['manual', 'receita']} captions={{ manual: 'Formulação manual', receita: 'Versão publicada de receita' }} onChange={(v) => {
              change(); setOrigin(v); setRecipe(null); setVersion(null); setCandidates([]); setImportedMass(null)
            }} />
            <Field label="Massa da mistura (base natural)" value={mass} onChange={(v) => { change(); setMass(v) }} required />
            <Choice label="Unidade da massa" value={unit} options={['kg', 'g']} onChange={(v) => { change(); setUnit(v) }} />
          </div>
          {origin === 'manual' ? <Field label="Identificação da formulação de referência" value={reference} onChange={(v) => { change(); setReference(v) }} required /> : <>
            <div className="grid gap-3 sm:grid-cols-2">
              <ServerSelect<Recipe> label="Receita" path="/receitas" params={{ ativo: true }} value={recipe?.id ?? null}
                selectedCaption={recipe?.nome} caption={(r) => `${r.codigo} · ${r.nome}`} onChange={(_, r) => { change(); setRecipe(r ?? null); setVersion(null); setCandidates([]); setImportedMass(null) }} />
              {recipe && <ServerSelect<RecipeVersion> key={recipe.id} label="Versão publicada" path={`/receitas/${recipe.id}/versoes`} params={{ estado: 'Publicado' }}
                value={version?.id ?? null} selectedCaption={version ? `Versão ${version.numero}` : undefined}
                caption={(v) => `Versão ${v.numero} · ${v.conteudo.tamanhoReferencia} ${v.conteudo.unidadeReferencia}`}
                onChange={(_, v) => {
                  change(); setVersion(v ?? null); setCandidates([]); setImportedMass(null)
                  if (v) { setMass(v.conteudo.tamanhoReferencia); setUnit(v.conteudo.unidadeReferencia === 'g' ? 'g' : 'kg') }
                }} />}
            </div>
            <button type="button" className={button} disabled={!version} onClick={() => void importRecipe()}>Importar composição para a massa informada</button>
            {importedMass && <p role="status">Composição carregada para {display(importedMass)} kg BN.</p>}
          </>}
        </Section>
        <Section title="Contexto nutricional">
          <div className="grid gap-3 sm:grid-cols-3">
            <ServerSelect<Species> label="Espécie do contexto" path="/especies" value={species} caption={(s) => s.nomeComum} onChange={(v) => { change(); setSpecies(v) }} />
            <Field label="Fase de uso" value={phase} onChange={(v) => { change(); setPhase(v) }} />
            <Field label="Preparo comum dos ingredientes (opcional)" value={preparation} onChange={(v) => { change(); setPreparation(v) }}
              hint="Quando preenchido, deve coincidir com o preparo registrado em todos os perfis usados." />
          </div>
        </Section>
        <Section title="Ingredientes e limites de inclusão">
          <p className="text-sm">O cálculo admite até 50 ingredientes incluídos. Use inclusão máxima zero e justifique cada exclusão.</p>
          {candidates.map((line, index) => <CandidateEditor key={line.id} line={line} index={index} imported={origin === 'receita'}
            usedItems={candidates.filter((c) => c.id !== line.id).map((c) => c.itemId)}
            update={(patch) => updateCandidate(line.id, patch)} remove={() => { change(); setCandidates((old) => old.filter((c) => c.id !== line.id)) }} />)}
          <button type="button" className={button} disabled={candidates.length >= 100} onClick={() => { change(); setCandidates((old) => [...old, newCandidate()]) }}>Adicionar ingrediente</button>
        </Section>
        <Section title="Metas em base natural">
          <ErrorNotice error={lookupError} />
          {lookupLoading && <p role="status">Carregando componentes nutricionais…</p>}
          {!lookupLoading && !lookupError && components.length === 0 && <p>Nenhum componente nutricional disponível.</p>}
          {lookupError && <button type="button" className={button} onClick={() => setLookupRevision((r) => r + 1)}>Recarregar componentes</button>}
          {goals.map((goal, index) => <GoalEditor key={goal.id} goal={goal} components={components} index={index}
            update={(patch) => updateGoal(goal.id, patch)} remove={() => { change(); setGoals((old) => old.filter((g) => g.id !== goal.id)) }} />)}
          <button type="button" className={button} disabled={goals.length >= 50 || !components.length} onClick={() => { change(); setGoals((old) => [...old, newGoal()]) }}>Adicionar meta</button>
        </Section>
        <ErrorNotice error={error} />
        {busy && <p role="status">Consultando a API…</p>}
        <button type="submit" className={primary} disabled={Boolean(lookupError)}>Revisar coeficientes</button>
      </fieldset>
    </form>}
    {review && <>
      <Section>
        <h2 ref={reviewSection} tabIndex={-1} className="text-xl font-semibold">Conferir pedido</h2>
        <p>{display(review.body.massaKg)} kg BN · {review.body.candidatos.filter((c) => c.inclusaoMaxima !== '0').length} ingredientes incluídos · {review.body.metas.length} metas obrigatórias</p>
        <p>Objetivo de menor alteração da mistura de referência. Custo e disponibilidade de estoque ficam fora deste cálculo.</p>
        <details>
          <summary className="cursor-pointer font-medium">Ingredientes e inclusões</summary>
          <ul className="mt-2 list-disc space-y-1 pl-5 text-sm">{review.body.candidatos.map((line) => <li key={line.id}>
            {review.inputs.candidatos.find((c) => c.id === line.id)?.nome ?? line.itemId} · referência {display(line.referenciaKg)} kg BN · {display(line.inclusaoMinima)}% a {display(line.inclusaoMaxima)}%
            {line.justificativa && <span className="break-words"> · {line.justificativa}</span>}
          </li>)}</ul>
        </details>
        <ul className="list-disc space-y-1 pl-5 text-sm">{review.body.metas.map((g) => <li key={g.id}>
          {g.componente} · {g.metodo} · {g.minimo !== null ? `mínimo ${display(g.minimo)} ` : ''}{g.maximo !== null ? `máximo ${display(g.maximo)} ` : ''}{g.unidade} BN · {g.aceitarEstimados ? 'Estimativas aceitas nesta meta' : 'Estimativas excluídas'}
        </li>)}</ul>
      </Section>
      <CoefficientMatrix inputs={review.inputs} />
      {request.errorStatus === 409 && <p>Revise as referências e os coeficientes antes de enviar novamente.</p>}
      <button type="button" className={primary} disabled={request.busy || Boolean(request.key) || request.errorStatus === 409} onClick={() => void open(request.submit(review.body))}>Executar otimização</button>
    </>}
  </Frame>
}

function CandidateEditor({ line, index, imported, usedItems, update, remove }: {
  line: CandidateForm; index: number; imported: boolean; usedItems: string[]; update: (patch: Partial<CandidateForm>) => void; remove: () => void
}) {
  const original = line.limitesImportados
  const label = `Ingrediente ${index + 1}`
  return <fieldset className="min-w-0 space-y-4 rounded-xl border p-3 sm:p-4">
    <legend className="px-2 font-semibold">{label}</legend>
    {original ? <p className="font-medium">{line.nomeItem} · {display(line.referenciaKg)} kg BN na receita</p> : <ServerSelect<Item>
      label={`Item do ingrediente ${index + 1}`} path="/itens" params={{ ativo: true, classe: 'Alimentar', capacidade: 'entrada' }}
      value={line.itemId || null} selectedCaption={line.nomeItem} eligible={(i) => eligibleInput(i) && !usedItems.includes(i.id)} caption={itemLabel}
      onChange={(_, i) => update({ itemId: i?.id ?? '', itemRevisao: i?.revisao ?? 0, nomeItem: i ? itemLabel(i) : undefined, perfilId: null, perfilRevisao: null, perfil: null })} />}
    {line.itemId && <ServerSelect<Profile> key={line.itemId} label={`Perfil publicado do ingrediente ${index + 1}`} path={`/itens/${line.itemId}/perfis-nutricionais`}
      params={{ estado: 'Publicado' }} value={line.perfilId} selectedCaption={line.perfil ? profileLabel(line.perfil) : undefined} caption={profileLabel}
      onChange={(_, p) => update({ perfilId: p?.id ?? null, perfilRevisao: p?.revisao ?? null, perfil: p ?? null })} />}
    {line.perfil && <p className="break-words text-sm">{[line.perfil.conteudo.fonte, line.perfil.conteudo.metodo, line.perfil.conteudo.preparacao, line.perfil.conteudo.contexto, line.perfil.conteudo.dataFonte].filter(Boolean).join(' · ')}</p>}
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      {!original && !imported && <>
        <Field label={`Massa de referência do ingrediente ${index + 1}`} value={line.referenciaKg} onChange={(v) => update({ referenciaKg: v })} required />
        <Choice label={`Unidade de referência do ingrediente ${index + 1}`} value={line.unidadeReferencia} options={['kg', 'g']} onChange={(v) => update({ unidadeReferencia: v })} />
      </>}
      <Field label={`Inclusão mínima (%) do ingrediente ${index + 1}`} value={line.inclusaoMinima} onChange={(v) => update({ inclusaoMinima: v })} required />
      <Field label={`Inclusão máxima (%) do ingrediente ${index + 1}`} value={line.inclusaoMaxima} onChange={(v) => update({ inclusaoMaxima: v })} required />
    </div>
    {original && <p className="text-sm">Limites na versão publicada: {display(original.min)}% a {display(original.max)}%.</p>}
    {!original && imported && <p className="text-sm">Ingrediente adicional, com massa de referência 0 kg.</p>}
    <Check label={`Excluir o ingrediente ${index + 1} deste pedido`} checked={line.inclusaoMinima === '0' && line.inclusaoMaxima === '0'} onChange={(v) => update({ inclusaoMinima: v ? '0' : original?.min ?? '0', inclusaoMaxima: v ? '0' : original?.max ?? '100' })} />
    <Field label={`Justificativa de exclusão ou alteração de limites do ingrediente ${index + 1}`} value={line.justificativa ?? ''}
      onChange={(v) => update({ justificativa: v })} required={line.inclusaoMaxima.trim() === '0' || changedLimits(line)} />
    {!original && <button type="button" className={button} onClick={remove}>Remover ingrediente {index + 1}</button>}
  </fieldset>
}

function GoalEditor({ goal, components, index, update, remove }: {
  goal: OptimizationGoal; components: Component[]; index: number; update: (patch: Partial<OptimizationGoal>) => void; remove: () => void
}) {
  const component = components.find((c) => c.codigo === goal.componente)
  const energy = component?.grandeza === 'energia'
  return <fieldset className="min-w-0 space-y-4 rounded-xl border p-3 sm:p-4">
    <legend className="px-2 font-semibold">Meta {index + 1}</legend>
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <Choice label={`Componente da meta ${index + 1}`} value={goal.componente} options={['', ...components.map((c) => c.codigo)]}
        captions={{ '': 'Selecione o componente', ...Object.fromEntries(components.map((c) => [c.codigo, `${c.nome} · ${c.codigo} · v${c.versao}`])) }}
        onChange={(v) => update({ componente: v, unidade: components.find((c) => c.codigo === v)?.grandeza === 'energia' ? 'MJ/kg' : 'g/kg' })} />
      <Field label={`Método da meta ${index + 1}`} value={goal.metodo} onChange={(v) => update({ metodo: v })} required />
      <Field label={`Contexto da meta ${index + 1}`} value={goal.contexto} onChange={(v) => update({ contexto: v })} required={energy} />
      <Choice label={`Unidade da meta ${index + 1}`} value={goal.unidade} options={energy ? ['MJ/kg', 'kcal/kg'] : ['g/kg', 'g/100g', '%', 'mg/kg']}
        onChange={(v) => update({ unidade: v })} />
      <Field label={`Mínimo da meta ${index + 1}`} value={goal.minimo ?? ''} onChange={(v) => update({ minimo: v || null })} />
      <Field label={`Máximo da meta ${index + 1}`} value={goal.maximo ?? ''} onChange={(v) => update({ maximo: v || null })} />
      <Choice label={`Origem da meta ${index + 1}`} value={goal.origem} options={['InformadaUsuario', 'ReferenciaTecnica']} onChange={(v) => update({ origem: v })} />
      <Field label={`Fonte da meta ou hipótese ${index + 1}`} value={goal.fonte ?? ''} onChange={(v) => update({ fonte: v || null })}
        required={goal.origem === 'ReferenciaTecnica' || goal.aceitarEstimados} hint="Para referência técnica, informe edição, seção, data e aplicabilidade." />
      <Field label={`Justificativa da meta ${index + 1}`} value={goal.justificativa ?? ''} onChange={(v) => update({ justificativa: v })}
        required={goal.origem === 'InformadaUsuario' || goal.aceitarEstimados}
        hint={goal.aceitarEstimados ? 'Inclua o motivo e a hipótese para aceitar a estimativa nesta meta.' : undefined} />
    </div>
    <Check label={`Aceitar estimativas na meta ${index + 1}`} checked={goal.aceitarEstimados} onChange={(v) => update({ aceitarEstimados: v })} />
    <button type="button" className={button} onClick={remove}>Remover meta {index + 1}</button>
  </fieldset>
}
