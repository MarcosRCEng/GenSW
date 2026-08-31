import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { listPessoas, setPessoaAtivo } from '../services/peopleService'
import {
  TipoPessoa,
  type ListPessoasParams,
  type Pessoa,
  type PessoasPage,
  type PessoaSortBy,
  type SortDirection,
} from '../types/people'

const INITIAL_PAGE_SIZE = 25

const dateFormatter = new Intl.DateTimeFormat('pt-BR', {
  dateStyle: 'short',
  timeZone: 'UTC',
})

function pessoaTypeLabel(tipoPessoa: TipoPessoa): string {
  return tipoPessoa === TipoPessoa.Fisica ? 'Pessoa física' : 'Pessoa jurídica'
}

function createdDateLabel(createdAtUtc: string): string {
  return dateFormatter.format(new Date(createdAtUtc))
}

interface PessoasTableProps {
  changingStatusId: string | null
  items: Pessoa[]
  onChangeStatus: (pessoa: Pessoa) => void
}

function PessoasTable({ changingStatusId, items, onChangeStatus }: PessoasTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
        <thead className="bg-slate-50 text-xs uppercase tracking-wide text-slate-600">
          <tr>
            <th className="px-4 py-3 font-semibold" scope="col">Nome</th>
            <th className="px-4 py-3 font-semibold" scope="col">Nome fantasia</th>
            <th className="px-4 py-3 font-semibold" scope="col">Tipo</th>
            <th className="px-4 py-3 font-semibold" scope="col">Status</th>
            <th className="px-4 py-3 font-semibold" scope="col">Cadastro</th>
            <th className="px-4 py-3 font-semibold" scope="col">Ações</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 bg-white text-slate-700">
          {items.map((pessoa) => (
            <tr key={pessoa.id}>
              <td className="whitespace-nowrap px-4 py-4 font-medium text-slate-900">
                {pessoa.nome}
              </td>
              <td className="whitespace-nowrap px-4 py-4">{pessoa.nomeFantasia || '—'}</td>
              <td className="whitespace-nowrap px-4 py-4">
                {pessoaTypeLabel(pessoa.tipoPessoa)}
              </td>
              <td className="whitespace-nowrap px-4 py-4">
                <span
                  className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${
                    pessoa.ativo
                      ? 'bg-emerald-100 text-emerald-800'
                      : 'bg-slate-200 text-slate-700'
                  }`}
                >
                  {pessoa.ativo ? 'Ativo' : 'Inativo'}
                </span>
              </td>
              <td className="whitespace-nowrap px-4 py-4">
                {createdDateLabel(pessoa.createdAtUtc)}
              </td>
              <td className="whitespace-nowrap px-4 py-4">
                <div className="flex items-center gap-3">
                  {pessoa.ativo ? (
                    <Link
                      className="font-semibold text-emerald-700 hover:text-emerald-900 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2"
                      to={`/pessoas/${pessoa.id}/editar`}
                    >
                      Editar
                    </Link>
                  ) : (
                    <button
                      className="cursor-not-allowed font-semibold text-slate-400"
                      disabled
                      title="Pessoa inativa não pode ser editada."
                      type="button"
                    >
                      Editar
                    </button>
                  )}
                  <button
                    className="font-semibold text-slate-700 hover:text-slate-950 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
                    disabled={changingStatusId === pessoa.id}
                    onClick={() => onChangeStatus(pessoa)}
                    type="button"
                  >
                    {changingStatusId === pessoa.id
                      ? pessoa.ativo ? 'Inativando…' : 'Reativando…'
                      : pessoa.ativo ? 'Inativar' : 'Reativar'}
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function PeopleListPage() {
  const [result, setResult] = useState<PessoasPage | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [hasError, setHasError] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)
  const [changingStatusId, setChangingStatusId] = useState<string | null>(null)
  const [statusMutationError, setStatusMutationError] = useState(false)

  const [searchDraft, setSearchDraft] = useState('')
  const [search, setSearch] = useState<string | undefined>()
  const [tipoPessoa, setTipoPessoa] = useState<TipoPessoa | undefined>()
  const [ativo, setAtivo] = useState<boolean | undefined>()
  const [sortBy, setSortBy] = useState<PessoaSortBy>('nome')
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(INITIAL_PAGE_SIZE)

  useEffect(() => {
    let isCurrent = true
    const params: ListPessoasParams = {
      page,
      pageSize,
      sortBy,
      sortDirection,
      ...(search !== undefined ? { search } : {}),
      ...(tipoPessoa !== undefined ? { tipoPessoa } : {}),
      ...(ativo !== undefined ? { ativo } : {}),
    }

    setIsLoading(true)
    setHasError(false)

    void listPessoas(params)
      .then((response) => {
        if (isCurrent) setResult(response)
      })
      .catch(() => {
        if (isCurrent) setHasError(true)
      })
      .finally(() => {
        if (isCurrent) setIsLoading(false)
      })

    return () => {
      isCurrent = false
    }
  }, [ativo, page, pageSize, reloadKey, search, sortBy, sortDirection, tipoPessoa])

  const applySearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (isLoading) return

    setPage(1)
    setSearch(searchDraft.trim() ? searchDraft : undefined)
    setReloadKey((current) => current + 1)
  }

  const clearFilters = () => {
    if (isLoading) return

    setSearchDraft('')
    setSearch(undefined)
    setTipoPessoa(undefined)
    setAtivo(undefined)
    setSortBy('nome')
    setSortDirection('asc')
    setPage(1)
    setReloadKey((current) => current + 1)
  }

  const changeStatus = async (pessoa: Pessoa) => {
    if (changingStatusId === pessoa.id) return

    setChangingStatusId(pessoa.id)
    setStatusMutationError(false)
    try {
      await setPessoaAtivo(pessoa.id, !pessoa.ativo)
      setReloadKey((current) => current + 1)
    } catch {
      setStatusMutationError(true)
    } finally {
      setChangingStatusId(null)
    }
  }

  const controlClassName =
    'mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:cursor-not-allowed disabled:bg-slate-100'

  return (
    <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-7xl">
        <header className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p>
            <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-900">Pessoas</h1>
            <p className="mt-2 text-slate-600">Consulta do cadastro mestre de pessoas</p>
          </div>
          <div className="flex flex-wrap gap-3">
            <Link
              className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2"
              to="/pessoas/nova"
            >
              Nova pessoa
            </Link>
            <Link
              className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:border-slate-400 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2"
              to="/"
            >
              Voltar ao início
            </Link>
          </div>
        </header>

        <section className="mt-8 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <form className="grid gap-4 md:grid-cols-2 xl:grid-cols-4" onSubmit={applySearch}>
            <div className="md:col-span-2">
              <label className="text-sm font-medium text-slate-700" htmlFor="people-search">
                Buscar por nome ou nome fantasia
              </label>
              <input
                className={controlClassName}
                disabled={isLoading}
                id="people-search"
                onChange={(event) => setSearchDraft(event.target.value)}
                type="search"
                value={searchDraft}
              />
            </div>

            <div>
              <label className="text-sm font-medium text-slate-700" htmlFor="people-type">
                Tipo de pessoa
              </label>
              <select
                className={controlClassName}
                disabled={isLoading}
                id="people-type"
                onChange={(event) => {
                  setPage(1)
                  setTipoPessoa(
                    event.target.value ? (Number(event.target.value) as TipoPessoa) : undefined,
                  )
                }}
                value={tipoPessoa ?? ''}
              >
                <option value="">Todos</option>
                <option value={TipoPessoa.Fisica}>Pessoa física</option>
                <option value={TipoPessoa.Juridica}>Pessoa jurídica</option>
              </select>
            </div>

            <div>
              <label className="text-sm font-medium text-slate-700" htmlFor="people-active">
                Status
              </label>
              <select
                className={controlClassName}
                disabled={isLoading}
                id="people-active"
                onChange={(event) => {
                  setPage(1)
                  setAtivo(
                    event.target.value === '' ? undefined : event.target.value === 'true',
                  )
                }}
                value={ativo === undefined ? '' : String(ativo)}
              >
                <option value="">Todos</option>
                <option value="true">Ativos</option>
                <option value="false">Inativos</option>
              </select>
            </div>

            <div>
              <label className="text-sm font-medium text-slate-700" htmlFor="people-sort">
                Ordenar por
              </label>
              <select
                className={controlClassName}
                disabled={isLoading}
                id="people-sort"
                onChange={(event) => {
                  setPage(1)
                  setSortBy(event.target.value as PessoaSortBy)
                }}
                value={sortBy}
              >
                <option value="nome">Nome</option>
                <option value="tipoPessoa">Tipo</option>
                <option value="ativo">Status</option>
                <option value="createdAtUtc">Cadastro</option>
              </select>
            </div>

            <div>
              <label className="text-sm font-medium text-slate-700" htmlFor="people-direction">
                Direção
              </label>
              <select
                className={controlClassName}
                disabled={isLoading}
                id="people-direction"
                onChange={(event) => {
                  setPage(1)
                  setSortDirection(event.target.value as SortDirection)
                }}
                value={sortDirection}
              >
                <option value="asc">Crescente</option>
                <option value="desc">Decrescente</option>
              </select>
            </div>

            <div>
              <label className="text-sm font-medium text-slate-700" htmlFor="people-page-size">
                Registros por página
              </label>
              <select
                className={controlClassName}
                disabled={isLoading}
                id="people-page-size"
                onChange={(event) => {
                  setPage(1)
                  setPageSize(Number(event.target.value))
                }}
                value={pageSize}
              >
                <option value={25}>25</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </select>
            </div>

            <div className="flex flex-wrap items-end gap-3">
              <button
                className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
                disabled={isLoading}
                type="submit"
              >
                Buscar
              </button>
              <button
                className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
                disabled={isLoading}
                onClick={clearFilters}
                type="button"
              >
                Limpar filtros
              </button>
            </div>
          </form>
        </section>

        {statusMutationError ? (
          <p className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700" role="alert">
            Não foi possível alterar o status da pessoa.
          </p>
        ) : null}

        <section className="mt-6 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          {isLoading && result === null ? (
            <p className="p-8 text-center text-slate-600" role="status">
              Carregando pessoas…
            </p>
          ) : hasError ? (
            <div className="p-8 text-center" role="alert">
              <p className="font-medium text-red-700">Não foi possível carregar as pessoas.</p>
              <button
                className="mt-4 rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-semibold text-red-700 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-600 focus:ring-offset-2"
                onClick={() => setReloadKey((current) => current + 1)}
                type="button"
              >
                Tentar novamente
              </button>
            </div>
          ) : result?.items.length === 0 ? (
            <div className="p-8 text-center">
              <p className="font-medium text-slate-800">Nenhuma pessoa encontrada.</p>
              <p className="mt-2 text-sm text-slate-600">Ajuste os filtros ou realize outra busca.</p>
            </div>
          ) : result ? (
            <PessoasTable
              changingStatusId={changingStatusId}
              items={result.items}
              onChangeStatus={changeStatus}
            />
          ) : null}

          {result && !hasError ? (
            <footer className="flex flex-wrap items-center justify-between gap-4 border-t border-slate-200 px-5 py-4 text-sm text-slate-600">
              <div className="flex flex-wrap gap-3">
                {result.totalPages > 0 ? (
                  <span> Página {result.page} de {result.totalPages} </span>
                ) : null}
                <span>{result.totalItems} registros</span>
                {isLoading ? <span role="status">Atualizando…</span> : null}
              </div>
              <div className="flex gap-2">
                <button
                  className="rounded-lg border border-slate-300 px-3 py-2 font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                  disabled={isLoading || page <= 1}
                  onClick={() => setPage((current) => current - 1)}
                  type="button"
                >
                  Anterior
                </button>
                <button
                  className="rounded-lg border border-slate-300 px-3 py-2 font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
                  disabled={isLoading || result.totalPages === 0 || page >= result.totalPages}
                  onClick={() => setPage((current) => current + 1)}
                  type="button"
                >
                  Próxima
                </button>
              </div>
            </footer>
          ) : null}
        </section>
      </div>
    </main>
  )
}
