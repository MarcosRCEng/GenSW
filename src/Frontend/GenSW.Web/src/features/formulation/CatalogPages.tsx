import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
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
import { decimal, display, message, page, read, send } from './api'
import {
  button,
  Check,
  Choice,
  ErrorNotice,
  Field,
  Frame,
  Pager,
  primary,
  Section,
  ServerSelect,
} from './FormControls'
import type {
  Audit,
  Category,
  Conversion,
  ConversionData,
  Item,
  ItemData,
  Page,
  Profile,
} from './types'

const getItem = (id: string) => read<Item>(`/itens/${id}`)
const empty: ItemData = {
  codigo: '',
  nome: '',
  descricao: null,
  categoriaId: null,
  classe: 'Alimentar',
  unidade: 'kg',
  podeEntrar: true,
  podeProduzir: false,
  usoInterno: false,
  venda: false,
}
export function ItemsPage() {
  const defaults = useRestoredListState('/itens', {
    search: '',
    ativo: 'true',
    classe: '',
    capacidade: '',
    categoriaId: '',
    sortBy: 'nome',
    sortDirection: 'asc',
    page: 1,
  })
  const [filters, setFilters] = useState(defaults)
  const navigation = useRememberListState('/itens', filters)
  const [result, setResult] = useState<Page<Item> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true
    setResult(null)
    setError(null)
    void page<Item>('/itens', filters)
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
  const filter = (key: keyof typeof filters, value: string) =>
    setFilters((f) => ({ ...f, [key]: value, page: 1 }))
  return (
    <Frame title="Insumos e produtos">
      <Section>
        <p>
          Identidade única do material, com usos cumulativos e conversões
          explícitas.
        </p>
        <div className="flex flex-wrap gap-3">
          <Link className={primary} to="/itens/novo" state={navigation}>
            Novo item
          </Link>
          <Link className={button} to="/itens/categorias">
            Categorias
          </Link>
        </div>
        <div className="grid gap-4 sm:grid-cols-3">
          <Field
            label="Buscar código ou nome"
            value={filters.search}
            onChange={(v) => filter('search', v)}
          />
          <Choice
            label="Status"
            value={filters.ativo}
            onChange={(v) => filter('ativo', v)}
            options={['', 'true', 'false']}
          />
          <Choice
            label="Classe"
            value={filters.classe}
            onChange={(v) => filter('classe', v)}
            options={[
              '',
              'Alimentar',
              'OutroMaterialIncorporado',
              'Embalagem',
              'Consumivel',
            ]}
          />
          <Choice
            label="Capacidade"
            value={filters.capacidade}
            onChange={(v) => filter('capacidade', v)}
            options={['', 'entrada', 'producao', 'interno', 'venda']}
          />
          <Choice
            label="Ordenar"
            value={filters.sortBy}
            onChange={(v) => filter('sortBy', v)}
            options={['nome', 'codigo', 'createdAtUtc']}
          />
          <Choice
            label="Direção"
            value={filters.sortDirection}
            onChange={(v) => filter('sortDirection', v)}
            options={['asc', 'desc']}
          />
        </div>
        <ServerSelect<Category>
          label="Categoria"
          path="/categorias-itens"
          value={filters.categoriaId || null}
          onChange={(v) => filter('categoriaId', v ?? '')}
          caption={(c) => `${c.nome} (${c.ativo ? 'Ativa' : 'Inativa'})`}
        />
        <ErrorNotice error={error} />
        {error && (
          <button className={button} onClick={() => setRetry((r) => r + 1)}>
            Tentar novamente
          </button>
        )}
        {!result && !error && <p role="status">Carregando itens…</p>}
        {result && (
          <>
            <p>{result.totalItems} itens encontrados.</p>
            <ul className="divide-y">
              {result.items.map((i) => (
                <li
                  className="flex flex-wrap items-center justify-between gap-3 py-4"
                  key={i.id}
                >
                  <div>
                    <strong>
                      {i.codigo} — {i.nome}
                    </strong>
                    <p className="text-sm text-slate-600">
                      {i.classe} · {i.unidade} · {i.ativo ? 'Ativo' : 'Inativo'}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Link
                      className={button}
                      to={`/itens/${i.id}`}
                      state={navigation}
                    >
                      Visualizar
                    </Link>
                    <Link
                      className={button}
                      to={`/itens/${i.id}/editar`}
                      state={navigation}
                    >
                      Editar
                    </Link>
                  </div>
                </li>
              ))}
            </ul>
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
export function ItemFormPage() {
  const { id } = useParams()
  const [data, setData] = useState<ItemData>(empty)
  const [revision, setRevision] = useState(0)
  const [loading, setLoading] = useState(Boolean(id))
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [retry, setRetry] = useState(0)
  const [fixed, setFixed] = useState(false)
  const navigate = useNavigate()
  const location = useLocation()
  const back = listReturnState(location.state, '/itens')
  useEffect(() => {
    if (!id) return
    let live = true
    setLoading(true)
    setError(null)
    void getItem(id)
      .then((i) => {
        if (live) {
          setData(i)
          setRevision(i.revisao)
          setFixed(i.unidadeFixada)
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
  const change = <K extends keyof ItemData>(k: K, v: ItemData[K]) =>
    setData((d) => ({ ...d, [k]: v }))
  const save = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const result = await send<Item>(
        id ? `/itens/${id}` : '/itens',
        id ? { conteudo: data, versaoEsperada: revision } : data,
        id ? 'PUT' : 'POST',
      )
      navigate(`/itens/${result.id}`, { state: back })
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <Frame title={id ? 'Editar item' : 'Novo item'}>
      <Link className={button} to={back?.list.path ?? '/itens'} state={back}>
        Voltar para Insumos e produtos
      </Link>
      <ErrorNotice error={error} />
      {loading ? (
        <>
          <p role="status">Carregando item…</p>
          {error && (
            <button className={button} onClick={() => setRetry((r) => r + 1)}>
              Tentar novamente
            </button>
          )}
        </>
      ) : (
        <form onSubmit={save}>
          <Section>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field
                label="Código"
                required
                value={data.codigo}
                onChange={(v) => change('codigo', v)}
              />
              <Field
                label="Nome"
                required
                value={data.nome}
                onChange={(v) => change('nome', v)}
              />
              <Choice
                label="Classe material"
                value={data.classe}
                onChange={(v) => change('classe', v)}
                options={[
                  'Alimentar',
                  'OutroMaterialIncorporado',
                  'Embalagem',
                  'Consumivel',
                ]}
              />
              <Choice
                label="Unidade canônica"
                value={data.unidade}
                onChange={(v) => change('unidade', v)}
                options={['kg', 'L', 'un']}
                disabled={fixed}
              />
            </div>
            {fixed && <p>Unidade fixada por publicação anterior.</p>}
            <ServerSelect<Category>
              label="Categoria"
              path="/categorias-itens"
              value={data.categoriaId}
              onChange={(v) => change('categoriaId', v)}
              caption={(c) => c.nome}
              params={{ ativo: true }}
            />
            <Field
              label="Descrição"
              multiline
              value={data.descricao ?? ''}
              onChange={(v) => change('descricao', v || null)}
            />
            <fieldset className="grid gap-3 sm:grid-cols-2">
              <legend className="mb-2 font-semibold">
                Capacidades cumulativas
              </legend>
              <Check
                label="Pode entrar em receita"
                checked={data.podeEntrar}
                onChange={(v) => change('podeEntrar', v)}
              />
              <Check
                label="Pode ser produzido"
                checked={data.podeProduzir}
                onChange={(v) => change('podeProduzir', v)}
              />
              <Check
                label="Uso interno"
                checked={data.usoInterno}
                onChange={(v) => change('usoInterno', v)}
              />
              <Check
                label="Destinação potencial à venda"
                checked={data.venda}
                onChange={(v) => change('venda', v)}
              />
            </fieldset>
            <button className={primary} disabled={busy}>
              {busy ? 'Salvando…' : 'Salvar item'}
            </button>
          </Section>
        </form>
      )}
    </Frame>
  )
}
export function CollectionPanel<T extends { id: string }>({
  path,
  title,
  render,
}: {
  path: string
  title: string
  render: (item: T) => React.ReactNode
}) {
  const [current, setCurrent] = useState(1)
  const [data, setData] = useState<Page<T> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true
    setError(null)
    setData(null)
    void page<T>(path, { page: current })
      .then((r) => {
        if (live) setData(r)
      })
      .catch((e) => {
        if (live) setError(message(e))
      })
    return () => {
      live = false
    }
  }, [path, current, retry])
  return (
    <Section title={title}>
      <ErrorNotice error={error} />
      {error && (
        <button className={button} onClick={() => setRetry((v) => v + 1)}>
          Tentar novamente
        </button>
      )}
      {!data && !error && <p role="status">Carregando…</p>}
      {data && (
        <>
          {data.items.length ? (
            <ul className="space-y-4">
              {data.items.map((i) => (
                <li key={i.id}>{render(i)}</li>
              ))}
            </ul>
          ) : (
            <p>Nenhum registro.</p>
          )}
          <Pager
            current={current}
            total={data.totalPages}
            onChange={setCurrent}
          />
        </>
      )}
    </Section>
  )
}
export function AuditPanel({ path }: { path: string }) {
  return (
    <CollectionPanel<Audit>
      path={path}
      title="Histórico"
      render={(a) => (
        <details>
          <summary>
            {a.operacao} · {new Date(a.createdAtUtc).toLocaleString('pt-BR')}
          </summary>
          <p>Autor: {a.autorId}</p>
          <p>Antes</p>
          <pre className="overflow-x-auto whitespace-pre-wrap break-words text-xs">
            {JSON.stringify(JSON.parse(a.antesJson), null, 2)}
          </pre>
          <p>Depois</p>
          <pre className="overflow-x-auto whitespace-pre-wrap break-words text-xs">
            {JSON.stringify(JSON.parse(a.depoisJson), null, 2)}
          </pre>
        </details>
      )}
    />
  )
}
export function ItemDetailsPage() {
  const { id } = useParams()
  const { record: item, state, retry } = useRecordDetails(id, getItem)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const status = async () => {
    if (!item) return
    setBusy(true)
    try {
      await send(
        `/itens/${item.id}/ativo`,
        { ativo: !item.ativo, versaoEsperada: item.revisao },
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
      title="Visualizar item"
      listPath="/itens"
      listLabel="Insumos e produtos"
      state={state}
      onRetry={retry}
      editPath={item ? `/itens/${item.id}/editar` : undefined}
    >
      {item && (
        <>
          <Section>
            <dl className="grid gap-4 sm:grid-cols-2">
              <DetailField label="Código">{item.codigo}</DetailField>
              <DetailField label="Nome">{item.nome}</DetailField>
              <DetailField label="Classe">{item.classe}</DetailField>
              <DetailField label="Unidade canônica">{item.unidade}</DetailField>
              <DetailField label="Status">
                <DetailStatus active={item.ativo} />
              </DetailField>
              <DetailField label="Revisão">{item.revisao}</DetailField>
              <DetailField label="Capacidades">
                {[
                  item.podeEntrar && 'Entrada',
                  item.podeProduzir && 'Produzido',
                  item.usoInterno && 'Uso interno',
                  item.venda && 'Venda potencial',
                ]
                  .filter(Boolean)
                  .join(', ') || 'Nenhuma'}
              </DetailField>
              <DetailField label="Descrição">{item.descricao}</DetailField>
            </dl>
            <ErrorNotice error={error} />
            <div className="flex flex-wrap gap-3">
              <button
                className={button}
                disabled={busy}
                onClick={() => void status()}
              >
                {item.ativo ? 'Inativar' : 'Reativar'}
              </button>
              {item.ativo && (
                <>
                  <Link className={button} to={`/itens/${item.id}/perfis/novo`}>
                    Novo perfil
                  </Link>
                  <Link
                    className={button}
                    to={`/itens/${item.id}/conversoes/nova`}
                  >
                    Nova conversão
                  </Link>
                </>
              )}
            </div>
          </Section>
          <CollectionPanel<Profile>
            path={`/itens/${item.id}/perfis-nutricionais`}
            title="Perfis nutricionais"
            render={(p) => (
              <Link className={button} to={`/producao/perfis/${p.id}`}>
                {p.conteudo.nome} · v{p.numero} · {p.estado}
              </Link>
            )}
          />
          <CollectionPanel<Conversion>
            path={`/itens/${item.id}/conversoes`}
            title="Conversões publicadas"
            render={(c) => (
              <div>
                <strong>
                  v{c.numero}: 1 {c.origem} = {display(String(c.fator))}{' '}
                  {c.destino}
                </strong>
                <p>
                  {c.proveniencia} · {c.metodo} · {c.dataFonte}
                </p>
                <p>Fonte: {c.fonte}</p>
                <p>
                  Contexto: {c.contexto} · Amostra: {c.referenciaAmostra}
                </p>
              </div>
            )}
          />
          <AuditPanel path={`/itens/${item.id}/historico`} />
        </>
      )}
    </DetailsPage>
  )
}
export function CategoriesPage() {
  const [name, setName] = useState('')
  const [edit, setEdit] = useState<Category | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [epoch, setEpoch] = useState(0)
  const save = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    try {
      await send(
        edit ? `/categorias-itens/${edit.id}` : '/categorias-itens',
        {
          nome: name,
          ativo: edit?.ativo ?? true,
          versaoEsperada: edit?.revisao ?? 0,
        },
        edit ? 'PUT' : 'POST',
      )
      setEdit(null)
      setName('')
      setEpoch((v) => v + 1)
      setError(null)
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <Frame title="Categorias de itens">
      <Section>
        <form onSubmit={save} className="space-y-4">
          <Field
            label="Nome da categoria"
            value={name}
            onChange={setName}
            required
          />
          {edit && (
            <Check
              label="Ativa"
              checked={edit.ativo}
              onChange={(v) => setEdit({ ...edit, ativo: v })}
            />
          )}
          <ErrorNotice error={error} />
          <button className={primary} disabled={busy}>
            {edit ? 'Salvar categoria' : 'Criar categoria'}
          </button>
          {edit && (
            <button
              type="button"
              className={`${button} ml-2`}
              onClick={() => {
                setEdit(null)
                setName('')
              }}
            >
              Cancelar edição
            </button>
          )}
        </form>
      </Section>
      <CollectionPanel<Category>
        key={epoch}
        path="/categorias-itens"
        title="Categorias"
        render={(c) => (
          <div className="flex flex-wrap items-center gap-3">
            <span>
              {c.nome} · {c.ativo ? 'Ativa' : 'Inativa'}
            </span>
            <button
              className={button}
              onClick={() => {
                setEdit(c)
                setName(c.nome)
              }}
            >
              Editar / alterar status
            </button>
          </div>
        )}
      />
    </Frame>
  )
}
export function ConversionFormPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [data, setData] = useState<ConversionData>({
    origem: 'L',
    destino: 'kg',
    fator: '',
    fonte: '',
    metodo: '',
    dataFonte: '',
    contexto: '',
    referenciaAmostra: '',
    proveniencia: 'Declarado',
  })
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const change = (key: keyof ConversionData, value: string) =>
    setData((d) => ({ ...d, [key]: value }))
  const save = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    try {
      await send(`/itens/${id}/conversoes`, {
        ...data,
        fator: decimal(data.fator),
      })
      navigate(`/itens/${id}`)
    } catch (e) {
      setError(message(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <Frame title="Nova conversão explícita">
      <Link className={button} to={`/itens/${id}`}>
        Voltar ao item
      </Link>
      <form onSubmit={save}>
        <Section>
          <p>
            A publicação fixa a unidade do item. Outra medição cria outra
            versão, sem alterar esta.
          </p>
          <div className="grid gap-4 sm:grid-cols-2">
            <Choice
              label="Origem"
              value={data.origem}
              onChange={(v) => change('origem', v)}
              options={['kg', 'g', 'L', 'mL', 'un']}
            />
            <Choice
              label="Destino"
              value={data.destino}
              onChange={(v) => change('destino', v)}
              options={['kg', 'g', 'L', 'mL', 'un']}
            />
            <Field
              label="Fator (destino por 1 origem)"
              value={data.fator}
              onChange={(v) => change('fator', v)}
              required
            />
            <Choice
              label="Proveniência"
              value={data.proveniencia}
              onChange={(v) => change('proveniencia', v)}
              options={['Medido', 'Declarado', 'Estimado']}
            />
            {(
              ['fonte', 'metodo', 'contexto', 'referenciaAmostra'] as const
            ).map((k) => (
              <Field
                key={k}
                label={
                  {
                    fonte: 'Fonte',
                    metodo: 'Método',
                    contexto: 'Contexto e condições',
                    referenciaAmostra:
                      'Referência da amostra / lote documental',
                  }[k]
                }
                value={data[k]}
                onChange={(v) => change(k, v)}
                required
              />
            ))}
            <Field
              label="Data da fonte (AAAA-MM-DD)"
              value={data.dataFonte}
              onChange={(v) => change('dataFonte', v)}
              required
            />
          </div>
          <ErrorNotice error={error} />
          <button className={primary} disabled={busy}>
            Publicar conversão
          </button>
        </Section>
      </form>
    </Frame>
  )
}
