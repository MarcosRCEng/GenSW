import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { isHttpError } from '../../../shared/http/httpErrors'
import {
  createPessoa,
  getPessoaById,
  updatePessoa,
} from '../services/peopleService'
import { TipoPessoa } from '../types/people'

type LoadState = 'loading' | 'ready' | 'inactive' | 'not-found' | 'error'

function pessoaTypeLabel(tipoPessoa: TipoPessoa): string {
  return tipoPessoa === TipoPessoa.Fisica ? 'Pessoa física' : 'Pessoa jurídica'
}

export function PeopleFormPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const isEdit = id !== undefined

  const [loadState, setLoadState] = useState<LoadState>(isEdit ? 'loading' : 'ready')
  const [retryKey, setRetryKey] = useState(0)
  const [tipoPessoa, setTipoPessoa] = useState<TipoPessoa | ''>('')
  const [nome, setNome] = useState('')
  const [nomeFantasia, setNomeFantasia] = useState('')
  const [tipoError, setTipoError] = useState(false)
  const [nomeError, setNomeError] = useState(false)
  const [nomeFantasiaError, setNomeFantasiaError] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  useEffect(() => {
    if (!id) return

    let isCurrent = true
    setLoadState('loading')
    setSaveError(null)

    void getPessoaById(id)
      .then((pessoa) => {
        if (!isCurrent) return

        if (!pessoa.ativo) {
          setLoadState('inactive')
          return
        }

        setTipoPessoa(pessoa.tipoPessoa)
        setNome(pessoa.nome)
        setNomeFantasia(pessoa.nomeFantasia ?? '')
        setLoadState('ready')
      })
      .catch((error: unknown) => {
        if (!isCurrent) return
        setLoadState(isHttpError(error) && error.status === 404 ? 'not-found' : 'error')
      })

    return () => {
      isCurrent = false
    }
  }, [id, retryKey])

  const handleTipoPessoaChange = (value: string) => {
    const nextType = value ? (Number(value) as TipoPessoa) : ''
    setTipoPessoa(nextType)
    setTipoError(false)
    if (nextType !== TipoPessoa.Juridica) {
      setNomeFantasia('')
      setNomeFantasiaError(false)
    }
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (isSaving || loadState !== 'ready') return

    const normalizedName = nome.trim()
    const hasTypeError = !isEdit && tipoPessoa === ''
    const hasNameError = normalizedName.length < 2 || normalizedName.length > 200
    const normalizedTradeName = nomeFantasia.trim()
    const hasTradeNameError =
      tipoPessoa === TipoPessoa.Juridica && normalizedTradeName.length > 200

    setTipoError(hasTypeError)
    setNomeError(hasNameError)
    setNomeFantasiaError(hasTradeNameError)
    setSaveError(null)

    if (hasTypeError || hasNameError || hasTradeNameError || tipoPessoa === '') return

    const normalizedNomeFantasia =
      tipoPessoa === TipoPessoa.Juridica && normalizedTradeName ? normalizedTradeName : null

    setIsSaving(true)
    try {
      if (isEdit && id) {
        await updatePessoa(id, {
          nome: normalizedName,
          nomeFantasia: normalizedNomeFantasia,
        })
      } else {
        await createPessoa({
          tipoPessoa,
          nome: normalizedName,
          nomeFantasia: normalizedNomeFantasia,
        })
      }
      navigate('/pessoas')
    } catch (error: unknown) {
      if (isHttpError(error)) {
        if (error.status === 400) {
          setSaveError('Revise os dados informados.')
        } else if (isEdit && error.status === 409) {
          setSaveError('A pessoa está inativa. Reative-a antes de editar.')
        } else if (isEdit && error.status === 404) {
          setSaveError('Pessoa não encontrada.')
        } else {
          setSaveError('Não foi possível salvar a pessoa.')
        }
      } else {
        setSaveError('Não foi possível salvar a pessoa.')
      }
    } finally {
      setIsSaving(false)
    }
  }

  const controlClassName =
    'mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-900 focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:cursor-not-allowed disabled:bg-slate-100'

  const returnLink = (
    <Link
      className="inline-flex rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2"
      to="/pessoas"
    >
      Voltar para Pessoas
    </Link>
  )

  if (isEdit && loadState === 'loading') {
    return (
      <main className="flex min-h-screen items-center justify-center bg-slate-50 px-6">
        <p className="text-slate-600" role="status">Carregando pessoa…</p>
      </main>
    )
  }

  if (isEdit && loadState === 'inactive') {
    return (
      <main className="min-h-screen bg-slate-50 px-4 py-8">
        <section className="mx-auto max-w-2xl rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
          <h1 className="text-2xl font-bold text-slate-900">Esta pessoa está inativa.</h1>
          <p className="mt-3 text-slate-600">Reative-a na listagem antes de editar.</p>
          <div className="mt-6">{returnLink}</div>
        </section>
      </main>
    )
  }

  if (isEdit && loadState === 'not-found') {
    return (
      <main className="min-h-screen bg-slate-50 px-4 py-8">
        <section className="mx-auto max-w-2xl rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
          <h1 className="text-2xl font-bold text-slate-900">Pessoa não encontrada.</h1>
          <div className="mt-6">{returnLink}</div>
        </section>
      </main>
    )
  }

  if (isEdit && loadState === 'error') {
    return (
      <main className="min-h-screen bg-slate-50 px-4 py-8">
        <section className="mx-auto max-w-2xl rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm">
          <p className="font-medium text-red-700" role="alert">Não foi possível carregar a pessoa.</p>
          <button
            className="mt-4 rounded-lg border border-red-300 bg-white px-4 py-2 text-sm font-semibold text-red-700 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-600 focus:ring-offset-2"
            onClick={() => setRetryKey((current) => current + 1)}
            type="button"
          >
            Tentar novamente
          </button>
        </section>
      </main>
    )
  }

  return (
    <main className="min-h-screen bg-slate-50 px-4 py-8 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-2xl">
        <p className="text-sm font-semibold uppercase tracking-[0.2em] text-emerald-700">GenSW</p>
        <h1 className="mt-2 text-3xl font-bold tracking-tight text-slate-900">
          {isEdit ? 'Editar pessoa' : 'Nova pessoa'}
        </h1>

        <form
          className="mt-8 space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
          noValidate
          onSubmit={handleSubmit}
        >
          {isEdit ? (
            <div>
              <span className="text-sm font-medium text-slate-700">Tipo de pessoa</span>
              <p className="mt-1 text-slate-900">
                {tipoPessoa === '' ? '' : pessoaTypeLabel(tipoPessoa)}
              </p>
            </div>
          ) : (
            <div>
              <label className="text-sm font-medium text-slate-700" htmlFor="person-type">
                Tipo de pessoa
              </label>
              <select
                aria-invalid={tipoError}
                className={controlClassName}
                id="person-type"
                onChange={(event) => handleTipoPessoaChange(event.target.value)}
                value={tipoPessoa}
              >
                <option value="">Selecione</option>
                <option value={TipoPessoa.Fisica}>Pessoa física</option>
                <option value={TipoPessoa.Juridica}>Pessoa jurídica</option>
              </select>
              {tipoError ? <p className="mt-1 text-sm text-red-700">Selecione o tipo de pessoa.</p> : null}
            </div>
          )}

          <div>
            <label className="text-sm font-medium text-slate-700" htmlFor="person-name">Nome</label>
            <input
              aria-invalid={nomeError}
              className={controlClassName}
              id="person-name"
              maxLength={200}
              minLength={2}
              onChange={(event) => {
                setNome(event.target.value)
                setNomeError(false)
              }}
              required
              type="text"
              value={nome}
            />
            {nomeError ? (
              <p className="mt-1 text-sm text-red-700">Informe um nome entre 2 e 200 caracteres.</p>
            ) : null}
          </div>

          {tipoPessoa === TipoPessoa.Juridica ? (
            <div>
              <label className="text-sm font-medium text-slate-700" htmlFor="person-trade-name">
                Nome fantasia
              </label>
              <input
                aria-invalid={nomeFantasiaError}
                className={controlClassName}
                id="person-trade-name"
                maxLength={200}
                onChange={(event) => {
                  setNomeFantasia(event.target.value)
                  setNomeFantasiaError(false)
                }}
                type="text"
                value={nomeFantasia}
              />
              {nomeFantasiaError ? (
                <p className="mt-1 text-sm text-red-700">Informe um nome fantasia com até 200 caracteres.</p>
              ) : null}
            </div>
          ) : null}

          {saveError ? <p className="text-sm font-medium text-red-700" role="alert">{saveError}</p> : null}

          <div className="flex flex-wrap gap-3">
            <button
              className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
              disabled={isSaving}
              type="submit"
            >
              {isSaving ? 'Salvando…' : 'Salvar'}
            </button>
            <Link
              className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600 focus:ring-offset-2"
              to="/pessoas"
            >
              Cancelar
            </Link>
          </div>
        </form>
      </div>
    </main>
  )
}
