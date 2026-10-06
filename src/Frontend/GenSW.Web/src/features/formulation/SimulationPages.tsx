import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { DetailField, DetailsPage } from '../../shared/details/DetailsPage'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { decimal, display, message, read, send } from './api'
import { CollectionPanel } from './CatalogPages'
import {
  button,
  Check,
  Choice,
  ErrorNotice,
  Field,
  Frame,
  primary,
  Section,
  ServerSelect,
} from './FormControls'
import { RecipeEditor, RecipeRead } from './RecipeEditor'
import { normalizeRecipe } from './recipeData'
import type {
  Comparison,
  Component,
  Goal,
  RecipeData,
  RecipeVersion,
  Simulation,
  SimulationCommand,
  Summary,
} from './types'

const getSimulation = (id: string) =>
  read<Simulation>(`/simulacoes-formulacao/${id}`)
const getComparison = (id: string) =>
  read<Comparison>(`/comparacoes-formulacao/${id}`)
function useRequestKey() {
  const request = useRef<{ body: string; key: string } | null>(null)
  return (body: unknown) => {
    const json = JSON.stringify(body)
    if (request.current?.body !== json)
      request.current = { body: json, key: crypto.randomUUID() }
    return request.current.key
  }
}
export function FormulationPage() {
  const [ids, setIds] = useState<string[]>([])
  const [basis, setBasis] = useState('BN')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const navigate = useNavigate()
  const key = useRequestKey()
  const compare = async () => {
    const body = { simulacaoIds: ids, base: basis }
    setBusy(true)
    setError(null)
    try {
      const result = await send<Comparison>(
        '/comparacoes-formulacao',
        body,
        'POST',
        key(body),
      )
      navigate(`/producao/formulacao/comparacoes/${result.id}`)
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <Frame title="Formulação e comparação">
      <Section>
        <p>
          Compare composições e metas informadas, mantendo versões, fontes e
          hipóteses de cada simulação.
        </p>
        <Link className={primary} to="/producao/formulacao/nova">
          Nova simulação
        </Link>
        <div className="flex flex-wrap items-end gap-3">
          <Choice
            label="Base da comparação"
            value={basis}
            onChange={setBasis}
            options={['BN', 'MS']}
          />
          <button
            className={button}
            disabled={busy || ids.length < 2}
            onClick={() => void compare()}
          >
            Comparar selecionadas ({ids.length})
          </button>
        </div>
        <ErrorNotice error={error} />
      </Section>
      <CollectionPanel<Summary>
        path="/simulacoes-formulacao"
        title="Simulações imutáveis"
        render={(s) => (
          <div className="flex flex-wrap items-center gap-3">
            <Check
              label={`Selecionar ${new Date(s.createdAtUtc).toLocaleString('pt-BR')} · ${s.id}`}
              checked={ids.includes(s.id)}
              onChange={(selected) =>
                setIds((old) =>
                  selected ? [...old, s.id] : old.filter((id) => id !== s.id),
                )
              }
            />
            <Link className={button} to={`/producao/formulacao/${s.id}`}>
              Visualizar simulação
            </Link>
          </div>
        )}
      />
      <CollectionPanel<Summary>
        path="/comparacoes-formulacao"
        title="Comparações salvas"
        render={(s) => (
          <Link
            className={button}
            to={`/producao/formulacao/comparacoes/${s.id}`}
          >
            {new Date(s.createdAtUtc).toLocaleString('pt-BR')} · Visualizar
            comparação
          </Link>
        )}
      />
    </Frame>
  )
}
export function SimulationFormPage() {
  const [params] = useSearchParams()
  const prior = params.get('anterior')
  const initialVersion = params.get('receita')
  const navigate = useNavigate()
  const key = useRequestKey()
  const [recipeId, setRecipeId] = useState(initialVersion)
  const [version, setVersion] = useState<RecipeVersion | null>(null)
  const [size, setSize] = useState('')
  const [unit, setUnit] = useState('kg')
  const [species, setSpecies] = useState<string | null>(null)
  const [phase, setPhase] = useState('')
  const [goals, setGoals] = useState<Goal[]>([])
  const [variation, setVariation] = useState<RecipeData | null>(null)
  const [components, setComponents] = useState<Component[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    let live = true
    void read<Component[]>('/componentes-nutricionais')
      .then((c) => {
        if (live) setComponents(c)
      })
      .catch((e) => {
        if (live) setError(message(e))
      })
    return () => {
      live = false
    }
  }, [])
  useEffect(() => {
    if (!recipeId) {
      setVersion(null)
      return
    }
    let live = true
    void read<RecipeVersion>(`/receitas/versoes/${recipeId}`)
      .then((v) => {
        if (live) {
          setVersion(v)
          if (!prior) {
            setSize(v.conteudo.tamanhoReferencia)
            setUnit(v.conteudo.unidadeReferencia)
          }
        }
      })
      .catch((e) => {
        if (live) setError(message(e))
      })
    return () => {
      live = false
    }
  }, [recipeId, prior])
  useEffect(() => {
    if (!prior) return
    let live = true
    void getSimulation(prior)
      .then((s) => {
        if (live) {
          setRecipeId(s.conteudo.pedido.receitaVersaoId)
          setSize(s.conteudo.pedido.tamanho)
          setUnit(s.conteudo.pedido.unidade)
          setSpecies(s.conteudo.pedido.especieId)
          setPhase(s.conteudo.pedido.fase ?? '')
          setGoals(s.conteudo.pedido.metas)
          setVariation(
            s.conteudo.pedido.variacao ?? s.conteudo.receita.conteudo,
          )
        }
      })
      .catch((e) => {
        if (live) setError(message(e))
      })
    return () => {
      live = false
    }
  }, [prior])
  const goal = (index: number, patch: Partial<Goal>) =>
    setGoals((g) => g.map((v, i) => (i === index ? { ...v, ...patch } : v)))
  const save = async (e: FormEvent) => {
    e.preventDefault()
    if (!recipeId) {
      setError('Selecione uma versão publicada.')
      return
    }
    setBusy(true)
    setError(null)
    const payload: SimulationCommand = {
      receitaVersaoId: recipeId,
      tamanho: decimal(size),
      unidade: unit,
      especieId: species,
      fase: phase || null,
      anteriorId: prior,
      variacao: variation ? normalizeRecipe(variation) : null,
      metas: goals.map((g) => ({
        ...g,
        minimo: g.minimo === null ? null : decimal(g.minimo),
        maximo: g.maximo === null ? null : decimal(g.maximo),
      })),
    }
    try {
      const result = await send<Simulation>(
        '/simulacoes-formulacao',
        payload,
        'POST',
        key(payload),
      )
      navigate(`/producao/formulacao/${result.id}`)
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <Frame title={prior ? 'Nova variação da simulação' : 'Nova simulação'}>
      <form className="space-y-6" onSubmit={save}>
        <Section title="Receita e contexto">
          <ServerSelect<RecipeVersion>
            label="Versão da receita"
            path="/receitas/versoes"
            value={recipeId}
            onChange={(id, selected) => {
              setRecipeId(id)
              setVersion(selected ?? null)
              setVariation(null)
            }}
            params={{ estado: 'Publicado', ativo: true }}
            caption={(v) =>
              `v${v.numero} · ${v.conteudo.observacao ?? v.receitaId}`
            }
          />
          <div className="grid gap-4 sm:grid-cols-3">
            <Field
              label="Tamanho da simulação"
              value={size}
              onChange={setSize}
              required
            />
            <Choice
              label="Unidade do tamanho"
              value={unit}
              onChange={setUnit}
              options={['kg', 'g', 'L', 'mL', 'un']}
            />
            <Field label="Fase de uso" value={phase} onChange={setPhase} />
          </div>
          <ServerSelect<{ id: string; nomeComum: string }>
            label="Espécie do contexto (opcional)"
            path="/especies"
            value={species}
            onChange={setSpecies}
            caption={(s) => s.nomeComum}
          />
          <p className="text-sm">
            Energia usa somente modalidade, método e contexto compatíveis;
            seleção de perfil é explícita nas linhas.
          </p>
          {version && (
            <Check
              label="Variar manualmente sem alterar a versão publicada"
              checked={variation !== null}
              onChange={(v) =>
                setVariation(v ? structuredClone(version.conteudo) : null)
              }
            />
          )}
        </Section>
        {variation && <RecipeEditor data={variation} onChange={setVariation} />}
        <Section title="Metas informadas">
          {goals.map((g, index) => (
            <fieldset key={index} className="space-y-3 rounded-xl border p-4">
              <legend className="px-2 font-semibold">Meta {index + 1}</legend>
              <div className="grid gap-3 sm:grid-cols-3">
                <Choice
                  label="Componente da meta"
                  value={g.componente}
                  options={components.map((c) => c.codigo)}
                  onChange={(v) =>
                    goal(index, {
                      componente: v,
                      unidade:
                        components.find((c) => c.codigo === v)?.grandeza ===
                        'energia'
                          ? 'MJ/kg'
                          : 'g/kg',
                    })
                  }
                />
                <Field
                  label="Método compatível"
                  value={g.metodo}
                  onChange={(v) => goal(index, { metodo: v })}
                  required
                />
                <Field
                  label="Contexto compatível"
                  value={g.contexto}
                  onChange={(v) => goal(index, { contexto: v })}
                />
                <Choice
                  label="Base da meta"
                  value={g.base}
                  options={['BN', 'MS']}
                  onChange={(v) => goal(index, { base: v })}
                />
                <Choice
                  label="Unidade da meta"
                  value={g.unidade}
                  options={
                    components.find((c) => c.codigo === g.componente)
                      ?.grandeza === 'energia'
                      ? ['MJ/kg', 'kcal/kg']
                      : ['g/kg', 'g/100g', '%', 'mg/kg']
                  }
                  onChange={(v) => goal(index, { unidade: v })}
                />
                <Field
                  label="Mínimo (opcional)"
                  value={g.minimo ?? ''}
                  onChange={(v) => goal(index, { minimo: v || null })}
                />
                <Field
                  label="Máximo (opcional)"
                  value={g.maximo ?? ''}
                  onChange={(v) => goal(index, { maximo: v || null })}
                />
                <Choice
                  label="Origem da meta"
                  value={g.origem}
                  options={['InformadaUsuario', 'ReferenciaTecnica']}
                  onChange={(v) => goal(index, { origem: v })}
                />
                <Field
                  label="Fonte da meta técnica"
                  value={g.fonte ?? ''}
                  onChange={(v) => goal(index, { fonte: v || null })}
                  required={g.origem === 'ReferenciaTecnica'}
                />
              </div>
              <Check
                label="Aceitar dados estimados nesta meta"
                checked={g.aceitarEstimados}
                onChange={(v) => goal(index, { aceitarEstimados: v })}
              />
              <button
                type="button"
                className={button}
                onClick={() =>
                  setGoals((gs) => gs.filter((_, i) => i !== index))
                }
              >
                Remover meta {index + 1}
              </button>
            </fieldset>
          ))}
          <button
            type="button"
            className={button}
            onClick={() =>
              setGoals((g) => [
                ...g,
                {
                  componente: 'PB',
                  metodo: '',
                  contexto: '',
                  base: 'BN',
                  unidade: 'g/kg',
                  minimo: null,
                  maximo: null,
                  origem: 'InformadaUsuario',
                  fonte: null,
                  aceitarEstimados: true,
                },
              ])
            }
          >
            Adicionar meta
          </button>
        </Section>
        <ErrorNotice error={error} />
        <button className={primary} disabled={busy}>
          {busy ? 'Calculando…' : 'Calcular e salvar simulação'}
        </button>
      </form>
    </Frame>
  )
}
export function SimulationDetailsPage() {
  const { simulationId } = useParams()
  const {
    record: s,
    state,
    retry,
  } = useRecordDetails(simulationId, getSimulation)
  return (
    <DetailsPage
      title="Visualizar simulação"
      listPath="/producao/formulacao"
      listLabel="Formulação"
      state={state}
      onRetry={retry}
    >
      {s && (
        <>
          <Section
            title={`${s.conteudo.cabecalho.nome} · v${s.conteudo.receita.numero}`}
          >
            <p className="break-all">
              Simulação {s.id} ·{' '}
              {new Date(s.createdAtUtc).toLocaleString('pt-BR')}
            </p>
            <dl className="grid gap-4 sm:grid-cols-3">
              <DetailField label="Massa BN kg">
                {display(s.conteudo.resultado.massaKg)}
              </DetailField>
              <DetailField label="Matéria seca kg">
                {display(s.conteudo.resultado.materiaSecaKg)}
              </DetailField>
              <DetailField label="Umidade %">
                {display(s.conteudo.resultado.umidadePercentual)}
              </DetailField>
              <DetailField label="Rendimento principal %">
                {display(s.conteudo.rendimentoPercentual)}
              </DetailField>
              <DetailField label="Balanço de massa kg">
                {display(s.conteudo.balancoKg)}
              </DetailField>
              <DetailField label="Motor">
                {s.conteudo.resultado.motor}
              </DetailField>
            </dl>
            {[...s.conteudo.resultado.avisos, ...s.conteudo.problemas].map(
              (a, i) => (
                <p
                  className="rounded border border-amber-300 bg-amber-50 p-3"
                  key={i}
                >
                  {a}
                </p>
              ),
            )}
            <div className="flex flex-wrap gap-3">
              <Link
                className={button}
                to={`/producao/formulacao/nova?anterior=${s.id}`}
              >
                Nova variação explícita
              </Link>
              <Link
                className={button}
                to={`/producao/receitas/${s.conteudo.receita.receitaId}/versoes/nova`}
                state={{
                  variacao:
                    s.conteudo.pedido.variacao ?? s.conteudo.receita.conteudo,
                }}
              >
                Criar rascunho desta variação
              </Link>
            </div>
          </Section>
          <Section title="Composição e contribuições">
            {s.conteudo.resultado.componentes.map((r, i) => (
              <article key={i} className="space-y-3 rounded-xl border p-4">
                <h3 className="font-semibold">
                  {r.componente} · {r.metodo} ·{' '}
                  {r.contexto || 'Contexto não informado'}
                </h3>
                <p className="break-words">
                  BN: {display(r.valorBN)} {r.unidade} · MS:{' '}
                  {display(r.valorMS)} {r.unidade}
                </p>
                <p>
                  {r.estado}
                  {r.estimado ? ' · Com dados estimados' : ''} · Cobertura:{' '}
                  {display(r.coberturaPercentual)}%
                </p>
                {r.estimadoMS && (
                  <p>Resultado em MS usa dados estimados de matéria seca.</p>
                )}
                {r.valorBN === null && (
                  <p>
                    Contribuição conhecida: {display(r.contribuicaoConhecidaBN)}{' '}
                    {r.unidade} BN; total indeterminado.
                  </p>
                )}
                {r.kcalBN && (
                  <p className="break-words">
                    Mesma modalidade: {display(r.kcalBN)} kcal/kg BN
                    (termoquímica).
                  </p>
                )}
                <details>
                  <summary className="cursor-pointer font-medium">
                    Contribuições e fontes de {r.componente}
                  </summary>
                  <ul className="mt-3 space-y-3">
                    {r.contribuicoes.map((c, index) => (
                      <li className="rounded bg-slate-50 p-3" key={index}>
                        <p>
                          {c.item} · {display(c.massaKg)} kg BN
                        </p>
                        <p className="break-words">
                          Quantidade componente:{' '}
                          {display(c.quantidadeComponente)}{' '}
                          {r.unidade === 'MJ/kg' ? 'MJ' : 'g'} ·{' '}
                          {c.origem ?? 'Origem indeterminada'}
                        </p>
                        <p>Fonte: {c.fonte ?? 'Não informada'}</p>
                        {c.motivo && (
                          <p className="text-amber-800">{c.motivo}</p>
                        )}
                      </li>
                    ))}
                  </ul>
                </details>
              </article>
            ))}
          </Section>
          <Section title="Metas">
            {s.conteudo.resultado.metas.length ? (
              s.conteudo.resultado.metas.map((m, i) => (
                <article key={i} className="rounded border p-4">
                  <h3 className="font-semibold">
                    {m.meta.componente} · {m.meta.base} · {m.estado}
                  </h3>
                  <p>
                    {m.meta.minimo ?? 'Sem mínimo'} –{' '}
                    {m.meta.maximo ?? 'Sem máximo'} {m.meta.unidade}
                  </p>
                  <p>{m.motivo}</p>
                  <p>
                    Origem: {m.meta.origem} ·{' '}
                    {m.meta.fonte ?? 'Informada pelo usuário'}
                  </p>
                </article>
              ))
            ) : (
              <p>Nenhuma meta informada.</p>
            )}
          </Section>
          <Section title="Snapshot de linhas e perfis">
            <details>
              <summary>Receita congelada / variação escalonada</summary>
              <RecipeRead data={s.conteudo.escalonada} />
            </details>
            {s.conteudo.linhas.map((l, i) => (
              <details key={i}>
                <summary>
                  {l.nome} · Perfil {l.perfilNumero ?? 'ausente'} · {l.caminho}
                </summary>
                {l.perfil && (
                  <div className="space-y-2">
                    <p>Fonte congelada: {l.perfil.fonte}</p>
                    <p>
                      {l.perfil.contexto} · {l.perfil.referenciaAmostra}
                    </p>
                    <p>
                      {l.perfil.preparacao} · {l.perfil.metodo}
                    </p>
                    {l.perfil.valores.map((v) => (
                      <p key={v.componente}>
                        {v.componente}: {v.valor ?? v.estado} {v.unidade}{' '}
                        {v.base} · {v.origem ?? 'Não informada'} ·{' '}
                        {v.hipotese ?? ''}
                      </p>
                    ))}
                  </div>
                )}
              </details>
            ))}
          </Section>
        </>
      )}
    </DetailsPage>
  )
}
export function ComparisonDetailsPage() {
  const { comparisonId } = useParams()
  const {
    record: c,
    state,
    retry,
  } = useRecordDetails(comparisonId, getComparison)
  return (
    <DetailsPage
      title="Visualizar comparação"
      listPath="/producao/formulacao"
      listLabel="Formulação"
      state={state}
      onRetry={retry}
    >
      {c && (
        <Section title={`Comparação em ${c.conteudo.pedido.base}`}>
          <p>
            Dados ausentes ou contexto incompatível são identificados como não
            comparáveis; o snapshot mantém os resultados originais.
          </p>
          <p className="text-sm text-slate-600 sm:hidden">
            Deslize a tabela para consultar todas as simulações.
          </p>
          <div
            role="region"
            aria-label="Tabela comparativa de simulações"
            tabIndex={0}
            className="max-w-full overflow-x-auto rounded focus:ring-2 focus:ring-emerald-600"
          >
            <table className="w-full text-left text-sm">
              <thead>
                <tr>
                  <th className="p-3">Componente / método / contexto</th>
                  {c.conteudo.simulacoes.map((s) => (
                    <th className="min-w-48 p-3" key={s.id}>
                      <Link
                        className="underline"
                        to={`/producao/formulacao/${s.id}`}
                      >
                        {s.conteudo.cabecalho.nome} ·{' '}
                        {s.conteudo.pedido.tamanho} {s.conteudo.pedido.unidade}
                        <p className="font-normal">
                          Simulação {s.id.slice(0, 8)}
                          {s.conteudo.pedido.variacao ? ' · Variação' : ''}
                        </p>
                      </Link>
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {c.conteudo.linhas.map((r, i) => (
                  <tr key={i} className="border-t">
                    <th className="p-3">
                      {r.componente} · {r.metodo} · {r.contexto}
                      <p>
                        {r.comparavel
                          ? 'Comparável'
                          : 'Não comparável / dados incompletos'}
                      </p>
                    </th>
                    {r.valores.map((v) => (
                      <td className="p-3" key={v.simulacaoId}>
                        <p className="break-all">
                          {display(v.valor)} {r.unidade}
                        </p>
                        <p>
                          {v.estado}
                          {v.estimado ? ' · Estimado' : ''}
                        </p>
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Section>
      )}
    </DetailsPage>
  )
}
