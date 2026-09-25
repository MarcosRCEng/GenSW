import { useEffect, useState, type FormEvent } from 'react'
import { isHttpError } from '../../../../shared/http/httpErrors'
import {
  createAnimalIdentification,
  listAnimalIdentifications,
  setAnimalIdentificationAtivo,
  setAnimalIdentificationPrincipal,
  updateAnimalIdentificationMetadata,
} from '../services/identificationsService'
import type { AnimalIdentification, AnimalIdentificationPage, AnimalIdentificationType, UpdateAnimalIdentificationMetadataRequest } from '../types'

type LoadState = 'loading' | 'ready' | 'error'

const PAGE_SIZE = 10
const TYPES: ReadonlyArray<{ value: AnimalIdentificationType; label: string }> = [
  { value: 1, label: 'Anilha' },
  { value: 2, label: 'Brinco' },
  { value: 3, label: 'Microchip' },
  { value: 4, label: 'Tatuagem' },
  { value: 5, label: 'Marca' },
  { value: 6, label: 'Outro' },
]

const control = 'mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-900 focus:border-emerald-600 focus:outline-none focus:ring-2 focus:ring-emerald-600/20 disabled:cursor-not-allowed disabled:bg-slate-100'
const primaryButton = 'rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-60'
const secondaryButton = 'rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-700 disabled:cursor-not-allowed disabled:opacity-60'

function typeLabel(tipo: AnimalIdentificationType): string {
  return TYPES.find((item) => item.value === tipo)?.label ?? 'Tipo desconhecido'
}

function safeMutationMessage(error: unknown): string {
  if (isHttpError(error) && error.status === 400) return 'Os dados informados para a identificação são inválidos.'
  if (isHttpError(error) && error.status === 404) return 'Animal ou identificação não encontrada.'
  if (isHttpError(error) && error.status === 409) return 'A identificação conflita com outro registro.'
  return 'Não foi possível atualizar as identificações físicas.'
}

function displayDate(value: string | null): string {
  if (!value) return 'Não informada'
  const [year, month, day] = value.split('-')
  return `${day}/${month}/${year}`
}

interface AnimalIdentificationsPanelProps { animalId: string }
interface MetadataDirtyState { dataAplicacao: boolean; observacao: boolean }

export function AnimalIdentificationsPanel({ animalId }: AnimalIdentificationsPanelProps) {
  const [loadState, setLoadState] = useState<LoadState>('loading')
  const [retryKey, setRetryKey] = useState(0)
  const [pageNumber, setPageNumber] = useState(1)
  const [result, setResult] = useState<AnimalIdentificationPage | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [tipo, setTipo] = useState<AnimalIdentificationType>(1)
  const [descricaoTipo, setDescricaoTipo] = useState('')
  const [valor, setValor] = useState('')
  const [principal, setPrincipal] = useState(false)
  const [dataAplicacao, setDataAplicacao] = useState('')
  const [observacao, setObservacao] = useState('')
  const [metadataDirty, setMetadataDirty] = useState<MetadataDirtyState>({ dataAplicacao: false, observacao: false })
  const [selectedRow, setSelectedRow] = useState<AnimalIdentification | null>(null)
  const [pendingMutationId, setPendingMutationId] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [requestError, setRequestError] = useState<string | null>(null)

  useEffect(() => {
    let current = true
    setLoadState('loading')
    setRequestError(null)
    void listAnimalIdentifications(animalId, { page: pageNumber, pageSize: PAGE_SIZE })
      .then((response) => {
        if (!current) return
        setResult(response)
        setLoadState('ready')
      })
      .catch(() => { if (current) setLoadState('error') })
    return () => { current = false }
  }, [animalId, pageNumber, retryKey])

  const refresh = () => setRetryKey((key) => key + 1)
  const resetCreate = () => {
    setShowCreate(false); setTipo(1); setDescricaoTipo(''); setValor(''); setPrincipal(false)
    setDataAplicacao(''); setObservacao(''); setFormError(null)
  }
  const openCreate = () => {
    setShowCreate(true); setSelectedRow(null); setRequestError(null)
    setTipo(1); setDescricaoTipo(''); setValor(''); setPrincipal(false)
    setDataAplicacao(''); setObservacao(''); setFormError(null)
  }

  const runMutation = async (mutationId: string, action: () => Promise<unknown>, afterSuccess?: () => void) => {
    if (pendingMutationId !== null) return
    setPendingMutationId(mutationId); setRequestError(null)
    try {
      await action()
      afterSuccess?.()
      refresh()
    } catch (error: unknown) {
      setRequestError(safeMutationMessage(error))
    } finally {
      setPendingMutationId(null)
    }
  }

  const handleCreate = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const normalizedValue = valor.trim()
    const normalizedDescription = descricaoTipo.trim()
    if (!normalizedValue) { setFormError('Informe o valor da identificação.'); return }
    if (tipo === 6 && !normalizedDescription) { setFormError('Informe a descrição do tipo.'); return }
    setFormError(null)
    void runMutation('create', () => createAnimalIdentification(animalId, {
      tipo,
      descricaoTipo: tipo === 6 ? normalizedDescription : null,
      valor: normalizedValue,
      principal,
      dataAplicacao: dataAplicacao || null,
      observacao: observacao.trim() || null,
    }), resetCreate)
  }

  const openMetadata = (identification: AnimalIdentification) => {
    setShowCreate(false)
    setSelectedRow(identification)
    setDataAplicacao(identification.dataAplicacao ?? '')
    setObservacao(identification.observacao ?? '')
    setMetadataDirty({ dataAplicacao: false, observacao: false })
    setFormError(null); setRequestError(null)
  }

  const handleMetadata = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!selectedRow) return
    const id = selectedRow.id
    const request: UpdateAnimalIdentificationMetadataRequest = {}
    if (metadataDirty.dataAplicacao) request.dataAplicacao = dataAplicacao || null
    if (metadataDirty.observacao) request.observacao = observacao.trim() || null
    void runMutation(id, () => updateAnimalIdentificationMetadata(animalId, id, request), () => setSelectedRow(null))
  }

  const anyPending = pendingMutationId !== null

  return (
    <section aria-labelledby="animal-identifications-heading" className="mt-8 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900" id="animal-identifications-heading">Identificações físicas</h2>
          <p className="mt-1 text-sm text-slate-600">Marcadores físicos de manejo. Não representam SISBOV, UELN ou registro de associação.</p>
        </div>
        <button className={primaryButton} disabled={anyPending} onClick={openCreate} type="button">Adicionar identificação</button>
      </div>

      {requestError ? <p className="mt-4 text-sm font-medium text-red-700" role="alert">{requestError}</p> : null}

      {showCreate ? (
        <form className="mt-6 space-y-4 rounded-xl border border-emerald-200 bg-emerald-50/40 p-4" noValidate onSubmit={handleCreate}>
          <h3 className="font-semibold text-slate-900">Nova identificação</h3>
          <div className="grid gap-4 sm:grid-cols-2">
            <div><label className="text-sm font-medium text-slate-700" htmlFor="identification-type">Tipo</label><select className={control} disabled={anyPending} id="identification-type" onChange={(event) => { const next = Number(event.target.value) as AnimalIdentificationType; setTipo(next); if (next !== 6) setDescricaoTipo(''); setFormError(null) }} value={tipo}>{TYPES.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select></div>
            {tipo === 6 ? <div><label className="text-sm font-medium text-slate-700" htmlFor="identification-type-description">Descrição do tipo</label><input aria-invalid={formError === 'Informe a descrição do tipo.'} className={control} disabled={anyPending} id="identification-type-description" onChange={(event) => { setDescricaoTipo(event.target.value); setFormError(null) }} required type="text" value={descricaoTipo} /></div> : null}
            <div><label className="text-sm font-medium text-slate-700" htmlFor="identification-value">Valor</label><input className={control} disabled={anyPending} id="identification-value" onChange={(event) => { setValor(event.target.value); setFormError(null) }} required type="text" value={valor} /></div>
            <div><label className="text-sm font-medium text-slate-700" htmlFor="identification-application-date-create">Data de aplicação</label><input className={control} disabled={anyPending} id="identification-application-date-create" onChange={(event) => setDataAplicacao(event.target.value)} type="date" value={dataAplicacao} /></div>
          </div>
          <div><label className="text-sm font-medium text-slate-700" htmlFor="identification-observation-create">Observação</label><textarea className={control} disabled={anyPending} id="identification-observation-create" onChange={(event) => setObservacao(event.target.value)} value={observacao} /></div>
          <label className="flex items-center gap-2 text-sm font-medium text-slate-700"><input checked={principal} disabled={anyPending} onChange={(event) => setPrincipal(event.target.checked)} type="checkbox" />Definir como principal</label>
          {formError ? <p className="text-sm font-medium text-red-700">{formError}</p> : null}
          <div className="flex gap-3"><button className={primaryButton} disabled={anyPending} type="submit">{pendingMutationId === 'create' ? 'Cadastrando…' : 'Cadastrar identificação'}</button><button className={secondaryButton} disabled={anyPending} onClick={resetCreate} type="button">Cancelar</button></div>
        </form>
      ) : null}

      {selectedRow ? (
        <form className="mt-6 space-y-4 rounded-xl border border-slate-200 bg-slate-50 p-4" noValidate onSubmit={handleMetadata}>
          <h3 className="font-semibold text-slate-900">Editar metadados</h3>
          <dl className="grid gap-3 text-sm sm:grid-cols-2"><div><dt className="font-medium text-slate-600">Tipo</dt><dd>{typeLabel(selectedRow.tipo)}{selectedRow.descricaoTipo ? ` — ${selectedRow.descricaoTipo}` : ''}</dd></div><div><dt className="font-medium text-slate-600">Valor</dt><dd>{selectedRow.valor}</dd></div></dl>
          <div><label className="text-sm font-medium text-slate-700" htmlFor="identification-application-date-edit">Data de aplicação</label><input className={control} disabled={anyPending} id="identification-application-date-edit" onChange={(event) => { setDataAplicacao(event.target.value); setMetadataDirty((state) => ({ ...state, dataAplicacao: true })) }} type="date" value={dataAplicacao} /></div>
          <div><label className="text-sm font-medium text-slate-700" htmlFor="identification-observation-edit">Observação</label><textarea className={control} disabled={anyPending} id="identification-observation-edit" onChange={(event) => { setObservacao(event.target.value); setMetadataDirty((state) => ({ ...state, observacao: true })) }} value={observacao} /></div>
          <div className="flex gap-3"><button className={primaryButton} disabled={anyPending} type="submit">{pendingMutationId === selectedRow.id ? 'Salvando…' : 'Salvar metadados'}</button><button className={secondaryButton} disabled={anyPending} onClick={() => setSelectedRow(null)} type="button">Cancelar</button></div>
        </form>
      ) : null}

      {loadState === 'loading' ? <p className="mt-6 text-sm text-slate-600" role="status">Carregando identificações físicas…</p> : null}
      {loadState === 'error' ? <div className="mt-6"><p className="text-sm font-medium text-red-700" role="alert">Não foi possível carregar as identificações físicas.</p><button className={`${secondaryButton} mt-3`} onClick={() => setRetryKey((key) => key + 1)} type="button">Tentar novamente</button></div> : null}
      {loadState === 'ready' && result?.items.length === 0 ? <p className="mt-6 text-sm text-slate-600">Nenhuma identificação física cadastrada.</p> : null}
      {loadState === 'ready' && result && result.items.length > 0 ? (
        <div className="mt-6 overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50 text-slate-700"><tr><th className="px-3 py-2" scope="col">Tipo</th><th className="px-3 py-2" scope="col">Valor</th><th className="px-3 py-2" scope="col">Aplicação</th><th className="px-3 py-2" scope="col">Estado</th><th className="px-3 py-2" scope="col">Ações</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {result.items.map((item) => (
                <tr className={item.principal ? 'bg-emerald-50' : item.ativo ? 'bg-white' : 'bg-slate-50 text-slate-600'} key={item.id}>
                  <td className="px-3 py-3"><span className="font-medium">{typeLabel(item.tipo)}</span>{item.descricaoTipo ? <span className="block text-xs">{item.descricaoTipo}</span> : null}</td>
                  <td className="px-3 py-3"><span>{item.valor}</span>{item.observacao ? <span className="block text-xs text-slate-500">{item.observacao}</span> : null}</td>
                  <td className="px-3 py-3">{displayDate(item.dataAplicacao)}</td>
                  <td className="px-3 py-3">{item.ativo ? <><span>Ativa</span>{item.principal ? <span className="ml-2 rounded-full bg-emerald-100 px-2 py-1 text-xs font-semibold text-emerald-800">Principal</span> : null}</> : <span className="rounded-full bg-slate-200 px-2 py-1 text-xs font-semibold">Histórico (inativa)</span>}</td>
                  <td className="px-3 py-3"><div className="flex flex-wrap gap-2">
                    <button className={secondaryButton} disabled={anyPending} onClick={() => openMetadata(item)} type="button">Editar metadados</button>
                    {item.ativo ? <button className={secondaryButton} disabled={anyPending} onClick={() => void runMutation(item.id, () => setAnimalIdentificationPrincipal(animalId, item.id, !item.principal))} type="button">{item.principal ? 'Remover principal' : 'Definir como principal'}</button> : null}
                    <button className={secondaryButton} disabled={anyPending} onClick={() => void runMutation(item.id, () => setAnimalIdentificationAtivo(animalId, item.id, !item.ativo))} type="button">{item.ativo ? 'Inativar' : 'Reativar'}</button>
                  </div></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {loadState === 'ready' && result && result.totalPages > 1 ? <nav aria-label="Paginação das identificações físicas" className="mt-4 flex items-center justify-between gap-3"><button className={secondaryButton} disabled={pageNumber <= 1 || anyPending} onClick={() => setPageNumber((value) => Math.max(1, value - 1))} type="button">Página anterior</button><span className="text-sm text-slate-600">Página {result.page} de {result.totalPages}</span><button className={secondaryButton} disabled={pageNumber >= result.totalPages || anyPending} onClick={() => setPageNumber((value) => value + 1)} type="button">Próxima página</button></nav> : null}
    </section>
  )
}
