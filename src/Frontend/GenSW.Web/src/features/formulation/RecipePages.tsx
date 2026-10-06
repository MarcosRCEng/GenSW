import { useEffect, useState, type FormEvent } from 'react'
import {
  Link,
  useLocation,
  useNavigate,
  useParams,
  useSearchParams,
} from 'react-router-dom'
import {
  DetailField,
  DetailStatus,
  DetailsPage,
} from '../../shared/details/DetailsPage'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import {
  listReturnState,
  useRememberListState,
  useRestoredListState,
} from '../../shared/details/listNavigation'
import { decimal, message, page, read, send } from './api'
import { AuditPanel, CollectionPanel } from './CatalogPages'
import {
  button,
  Choice,
  ErrorNotice,
  Field,
  Frame,
  Pager,
  primary,
  Section,
  ServerSelect,
} from './FormControls'
import { RecipeEditor, RecipeRead } from './RecipeEditor'
import { emptyRecipe, normalizeRecipe } from './recipeData'
import type {
  Page,
  Recipe,
  RecipeData,
  RecipeHeader,
  RecipeVersion,
} from './types'

const getRecipe = (id: string) => read<Recipe>(`/receitas/${id}`)
const getVersion = (id: string) =>
  read<RecipeVersion>(`/receitas/versoes/${id}`)
export function RecipesPage() {
  const defaults = useRestoredListState('/producao/receitas', {
    search: '',
    ativo: 'true',
    page: 1,
    sortBy: 'nome',
    sortDirection: 'asc',
  })
  const [filters, setFilters] = useState(defaults)
  const navigation = useRememberListState('/producao/receitas', filters)
  const [result, setResult] = useState<Page<Recipe> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true
    setResult(null)
    setError(null)
    void page<Recipe>('/receitas', filters)
      .then((r) => {
        if (live) setResult(r)
      })
      .catch((e) => {
        if (live) setError(message(e))
      })
    return () => {
      live = false
    }
  }, [filters, retry])
  return (
    <Frame title="Receitas">
      <Section>
        <p>
          Especificações versionadas de misturas e processamentos, com
          quantidades esperadas.
        </p>
        <Link
          className={primary}
          to="/producao/receitas/nova"
          state={navigation}
        >
          Nova receita
        </Link>
        <div className="grid gap-4 sm:grid-cols-3">
          <Field
            label="Buscar receita"
            value={filters.search}
            onChange={(v) => setFilters((f) => ({ ...f, search: v, page: 1 }))}
          />
          <Choice
            label="Status"
            value={filters.ativo}
            options={['', 'true', 'false']}
            onChange={(v) => setFilters((f) => ({ ...f, ativo: v, page: 1 }))}
          />
          <Choice
            label="Ordenar por"
            value={filters.sortBy}
            options={['nome', 'codigo', 'createdAtUtc']}
            onChange={(v) => setFilters((f) => ({ ...f, sortBy: v, page: 1 }))}
          />
        </div>
        <ErrorNotice error={error} />
        {error && (
          <button className={button} onClick={() => setRetry((v) => v + 1)}>
            Tentar novamente
          </button>
        )}
        {!result && !error && <p role="status">Carregando receitas…</p>}
        {result && (
          <>
            <ul className="divide-y">
              {result.items.map((r) => (
                <li
                  key={r.id}
                  className="flex flex-wrap items-center justify-between gap-3 py-4"
                >
                  <div>
                    <strong>
                      {r.codigo} — {r.nome}
                    </strong>
                    <p>{r.ativo ? 'Ativa' : 'Inativa'}</p>
                  </div>
                  <div className="flex gap-2">
                    <Link
                      className={button}
                      to={`/producao/receitas/${r.id}`}
                      state={navigation}
                    >
                      Visualizar
                    </Link>
                    <Link
                      className={button}
                      to={`/producao/receitas/${r.id}/editar`}
                      state={navigation}
                    >
                      Editar
                    </Link>
                  </div>
                </li>
              ))}
            </ul>
            {!result.items.length && <p>Nenhuma receita.</p>}
            <Pager
              current={filters.page}
              total={result.totalPages}
              onChange={(v) => setFilters((f) => ({ ...f, page: v }))}
            />
          </>
        )}
      </Section>
    </Frame>
  )
}
export function RecipeHeaderFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const back = listReturnState(location.state, '/producao/receitas')
  const [data, setData] = useState<RecipeHeader>({
    codigo: '',
    nome: '',
    finalidade: '',
  })
  const [revision, setRevision] = useState(0)
  const [loading, setLoading] = useState(Boolean(id))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    if (!id) return
    let live = true
    setError(null)
    void getRecipe(id)
      .then((r) => {
        if (live) {
          setData(r)
          setRevision(r.revisao)
          setLoading(false)
        }
      })
      .catch((e) => {
        if (live) setError(message(e))
      })
    return () => {
      live = false
    }
  }, [id, retry])
  const save = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const r = await send<Recipe>(
        id ? `/receitas/${id}` : '/receitas',
        id ? { conteudo: data, versaoEsperada: revision } : data,
        id ? 'PUT' : 'POST',
      )
      navigate(`/producao/receitas/${r.id}`, { state: back })
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <Frame title={id ? 'Editar receita' : 'Nova receita'}>
      <Link
        className={button}
        to={back?.list.path ?? '/producao/receitas'}
        state={back}
      >
        Voltar para Receitas
      </Link>
      <ErrorNotice error={error} />
      {loading ? (
        <>
          <p role="status">Carregando…</p>
          <button className={button} onClick={() => setRetry((v) => v + 1)}>
            Tentar novamente
          </button>
        </>
      ) : (
        <form onSubmit={save}>
          <Section>
            <Field
              label="Código da receita"
              value={data.codigo}
              required
              onChange={(v) => setData((d) => ({ ...d, codigo: v }))}
            />
            <Field
              label="Nome da receita"
              value={data.nome}
              required
              onChange={(v) => setData((d) => ({ ...d, nome: v }))}
            />
            <Field
              label="Finalidade"
              value={data.finalidade}
              required
              multiline
              onChange={(v) => setData((d) => ({ ...d, finalidade: v }))}
            />
            <button className={primary} disabled={busy}>
              Salvar receita
            </button>
          </Section>
        </form>
      )}
    </Frame>
  )
}
export function RecipeDetailsPage() {
  const { id } = useParams()
  const { record: r, state, retry } = useRecordDetails(id, getRecipe)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const status = async () => {
    if (!r) return
    setBusy(true)
    try {
      await send(
        `/receitas/${r.id}/ativo`,
        { ativo: !r.ativo, versaoEsperada: r.revisao },
        'PATCH',
      )
      retry()
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <DetailsPage
      title="Visualizar receita"
      listPath="/producao/receitas"
      listLabel="Receitas"
      state={state}
      onRetry={retry}
      editPath={r ? `/producao/receitas/${r.id}/editar` : undefined}
    >
      {r && (
        <>
          <Section>
            <dl className="grid gap-4 sm:grid-cols-2">
              <DetailField label="Código">{r.codigo}</DetailField>
              <DetailField label="Nome">{r.nome}</DetailField>
              <DetailField label="Finalidade">{r.finalidade}</DetailField>
              <DetailField label="Status">
                <DetailStatus active={r.ativo} />
              </DetailField>
            </dl>
            <ErrorNotice error={error} />
            <div className="flex flex-wrap gap-3">
              <button
                className={button}
                disabled={busy}
                onClick={() => void status()}
              >
                {r.ativo ? 'Inativar receita' : 'Reativar receita'}
              </button>
              {r.ativo && (
                <Link
                  className={primary}
                  to={`/producao/receitas/${r.id}/versoes/nova`}
                >
                  Novo rascunho
                </Link>
              )}
            </div>
          </Section>
          <CollectionPanel<RecipeVersion>
            path={`/receitas/${r.id}/versoes`}
            title="Versões"
            render={(v) => (
              <Link
                className={button}
                to={`/producao/receitas/versoes/${v.id}`}
              >
                Versão {v.numero} · {v.estado} · {v.conteudo.tipo}
              </Link>
            )}
          />
          <AuditPanel path={`/receitas/${r.id}/historico`} />
        </>
      )}
    </DetailsPage>
  )
}
export function RecipeVersionFormPage() {
  const { id, versionId } = useParams()
  const [params] = useSearchParams()
  const source = params.get('origem')
  const location = useLocation()
  const navigate = useNavigate()
  const [data, setData] = useState<RecipeData>(
    () =>
      (location.state as { variacao?: RecipeData } | null)?.variacao ??
      emptyRecipe(),
  )
  const [revision, setRevision] = useState(0)
  const [loading, setLoading] = useState(Boolean(versionId || source))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    const target = versionId ?? source
    if (!target) return
    let live = true
    setError(null)
    void getVersion(target)
      .then((v) => {
        if (live) {
          if (versionId && v.estado !== 'Rascunho') {
            setError('Versão imutável; crie outro rascunho.')
            return
          }
          setData(v.conteudo)
          setRevision(v.revisao)
          setLoading(false)
        }
      })
      .catch((e) => {
        if (live) setError(message(e))
      })
    return () => {
      live = false
    }
  }, [versionId, source, retry])
  const save = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const payload = normalizeRecipe(data)
      const v = await send<RecipeVersion>(
        versionId
          ? `/receitas/versoes/${versionId}`
          : `/receitas/${id}/versoes`,
        versionId ? { conteudo: payload, versaoEsperada: revision } : payload,
        versionId ? 'PUT' : 'POST',
      )
      navigate(`/producao/receitas/versoes/${v.id}`)
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <Frame
      title={
        versionId ? 'Editar rascunho da receita' : 'Novo rascunho da receita'
      }
    >
      <Link
        className={button}
        to={
          versionId
            ? `/producao/receitas/versoes/${versionId}`
            : `/producao/receitas/${id}`
        }
      >
        Voltar
      </Link>
      <ErrorNotice error={error} />
      {loading ? (
        <>
          <p role="status">Carregando…</p>
          <button className={button} onClick={() => setRetry((v) => v + 1)}>
            Tentar novamente
          </button>
        </>
      ) : (
        <form className="space-y-6" onSubmit={save}>
          <RecipeEditor data={data} onChange={setData} />
          <button className={primary} disabled={busy}>
            {busy ? 'Salvando…' : 'Salvar rascunho da receita'}
          </button>
        </form>
      )}
    </Frame>
  )
}
export function RecipeVersionDetailsPage() {
  const { versionId } = useParams()
  const { record: v, state, retry } = useRecordDetails(versionId, getVersion)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [size, setSize] = useState('')
  const [unit, setUnit] = useState('kg')
  const [scaled, setScaled] = useState<RecipeData | null>(null)
  const [compare, setCompare] = useState<RecipeVersion | null>(null)
  const command = async (operation: string) => {
    if (!v) return
    setBusy(true)
    setError(null)
    try {
      await send(`/receitas/versoes/${v.id}/${operation}`, {
        versaoEsperada: v.revisao,
      })
      retry()
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  const scale = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      setScaled(
        await read<RecipeData>(
          `/receitas/versoes/${v!.id}/escalonamento?${new URLSearchParams({ tamanho: decimal(size), unidade: unit })}`,
        ),
      )
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <DetailsPage
      title="Visualizar versão da receita"
      listPath={v ? `/producao/receitas/${v.receitaId}` : '/producao/receitas'}
      listLabel="Receita"
      state={state}
      onRetry={retry}
      editPath={
        v?.estado === 'Rascunho'
          ? `/producao/receitas/versoes/${v.id}/editar`
          : undefined
      }
    >
      {v && (
        <>
          <Section title={`Versão ${v.numero} · ${v.estado}`}>
            <ErrorNotice error={error} />
            <div className="flex flex-wrap gap-3">
              {v.estado === 'Rascunho' && (
                <button
                  className={primary}
                  disabled={busy}
                  onClick={() => void command('publicacao')}
                >
                  Publicar receita
                </button>
              )}
              {v.estado === 'Publicado' && (
                <>
                  <button
                    className={button}
                    disabled={busy}
                    onClick={() => void command('inativacao')}
                  >
                    Inativar versão
                  </button>
                  <Link
                    className={primary}
                    to={`/producao/formulacao/nova?receita=${v.id}`}
                  >
                    Simular esta versão
                  </Link>
                </>
              )}
              <Link
                className={button}
                to={`/producao/receitas/${v.receitaId}/versoes/nova?origem=${v.id}`}
              >
                Criar nova revisão
              </Link>
            </div>
            <RecipeRead data={v.conteudo} />
          </Section>
          <Section title="Prévia de escalonamento">
            <form
              className="grid items-end gap-3 sm:grid-cols-3"
              onSubmit={scale}
            >
              <Field
                label="Tamanho solicitado"
                value={size}
                onChange={setSize}
                required
              />
              <Choice
                label="Unidade solicitada"
                value={unit}
                options={['kg', 'g', 'L', 'mL', 'un']}
                onChange={setUnit}
              />
              <button className={button} disabled={busy}>
                Calcular prévia no servidor
              </button>
            </form>
            {scaled && <RecipeRead data={scaled} />}
          </Section>
          <Section title="Comparar conteúdo com outra versão">
            <ServerSelect<RecipeVersion>
              label="Outra versão"
              path={`/receitas/${v.receitaId}/versoes`}
              value={compare?.id ?? null}
              onChange={(_, selected) => setCompare(selected ?? null)}
              caption={(o) => `v${o.numero} · ${o.estado}`}
            />
            {compare && (
              <div className="grid gap-6 lg:grid-cols-2">
                <div>
                  <h3 className="mb-3 font-bold">v{v.numero}</h3>
                  <RecipeRead data={v.conteudo} />
                </div>
                <div>
                  <h3 className="mb-3 font-bold">v{compare.numero}</h3>
                  <RecipeRead data={compare.conteudo} />
                </div>
              </div>
            )}
          </Section>
        </>
      )}
    </DetailsPage>
  )
}
