import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { isHttpError } from '../../shared/http/httpErrors'
import { desvincularAnimalPropriedade, getAnimalPropriedades, listPropriedades, transferirAnimalPropriedade } from './propertiesService'
import type { AnimalPropriedades, Propriedade } from './types'
import '../animals/evolution/evolution.css'

function today() {
  return new Date().toISOString().slice(0, 10)
}
const formatDate = (value: string) => value.split('-').reverse().join('/')

async function activeProperties() {
  const properties: Propriedade[] = []
  let page = 1
  let totalPages = 1
  do {
    const result = await listPropriedades({ page, pageSize: 100, ativo: true, sortBy: 'nome', sortDirection: 'asc' })
    properties.push(...result.items)
    totalPages = result.totalPages
    page += 1
  } while (page <= totalPages)
  return properties
}

export function AnimalPropertiesPanel({ animalId, readOnly = false }: { animalId: string; readOnly?: boolean }) {
  const [result, setResult] = useState<AnimalPropriedades | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(false)
  const [properties, setProperties] = useState<Propriedade[]>([])
  const [catalogLoading, setCatalogLoading] = useState(!readOnly)
  const [catalogError, setCatalogError] = useState(false)
  const [retryKey, setRetryKey] = useState(0)
  const [destination, setDestination] = useState('')
  const [date, setDate] = useState(today)
  const [notes, setNotes] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  useEffect(() => {
    let current = true
    setLoading(true); setLoadError(false)
    void getAnimalPropriedades(animalId).then((response) => { if (current) setResult(response) })
      .catch(() => { if (current) setLoadError(true) })
      .finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [animalId, retryKey])

  useEffect(() => {
    if (readOnly) return
    let current = true
    setCatalogLoading(true); setCatalogError(false)
    void activeProperties().then((response) => { if (current) setProperties(response) })
      .catch(() => { if (current) setCatalogError(true) })
      .finally(() => { if (current) setCatalogLoading(false) })
    return () => { current = false }
  }, [readOnly, retryKey])

  const currentLink = result?.atual ?? null
  const minimumDate = currentLink?.dataInicio ?? result?.historico.reduce((latest, item) => item.dataFim && item.dataFim > latest ? item.dataFim : latest, '') ?? ''
  const destinations = properties.filter((item) => item.ativo && item.id !== currentLink?.propriedadeId)
  const unavailable = saving || loading || loadError || result === null

  const move = async (unlink: boolean) => {
    if (readOnly || unavailable || (!unlink && (catalogLoading || catalogError)) || (unlink && !currentLink)) return
    setError(null); setSuccess(null)
    if (!date || date > today() || (minimumDate && date < minimumDate)) { setError(`Informe uma data válida${minimumDate ? ` a partir de ${formatDate(minimumDate)}` : ''}, sem ultrapassar hoje.`); return }
    if (!unlink && !destinations.some((item) => item.id === destination)) { setError('Selecione uma propriedade ativa de destino.'); return }
    setSaving(true)
    try {
      const response = unlink && currentLink
        ? await desvincularAnimalPropriedade(animalId, { dataFim: date, vinculoAtualIdEsperado: currentLink.id })
        : await transferirAnimalPropriedade(animalId, { propriedadeId: destination, dataInicio: date, vinculoAtualIdEsperado: currentLink?.id ?? null, observacao: notes.trim() || null })
      setResult(response); setDestination(''); setNotes('')
      setSuccess(unlink ? 'Vínculo encerrado. O animal está sem propriedade e seu histórico foi preservado.' : 'Propriedade atualizada. Histórico preservado.')
    } catch (failure: unknown) {
      if (isHttpError(failure) && failure.status === 409) {
        setError('A movimentação não foi aplicada: o vínculo ou a propriedade de destino mudou, ou o período é incompatível. Os dados foram atualizados; confira o histórico e tente novamente.')
        setDestination(''); setRetryKey((value) => value + 1)
      } else setError(isHttpError(failure) && failure.status === 400 ? 'Confira a data e os dados da movimentação.' : 'Não foi possível registrar a movimentação de propriedade.')
    } finally { setSaving(false) }
  }

  const transfer = (event: FormEvent) => { event.preventDefault(); void move(false) }
  return <section aria-labelledby={`property-panel-${animalId}`} className="animal-panel">
    <h2 id={`property-panel-${animalId}`}>Propriedade do animal</h2>
    <p className="text-sm text-slate-600">O vínculo indica a unidade operacional física e é opcional, inclusive para animais de referência.</p>
    {loading ? <p role="status">Carregando histórico de propriedades…</p> : loadError ? <p role="alert">Não foi possível carregar o histórico de propriedades.</p> : result && <>
      <p><strong>Propriedade atual: </strong>{currentLink ? <><Link className="font-semibold text-emerald-700 underline" to={`/propriedades/${currentLink.propriedadeId}`}>{currentLink.propriedadeNome}</Link>{!currentLink.propriedadeAtiva && ' (inativa)'} — desde {formatDate(currentLink.dataInicio)}</> : 'Sem propriedade'}</p>
      {currentLink && !currentLink.propriedadeAtiva && <p className="animal-notice">A propriedade atual está inativa. O vínculo permanece válido e pode ser transferido ou encerrado na edição do animal.</p>}
      {result.historico.length === 0 ? <p>Nenhuma movimentação de propriedade registrada.</p> : <div className="animal-table"><table><caption className="mb-2 text-left font-semibold">Histórico de propriedades</caption><thead><tr><th scope="col">Propriedade</th><th scope="col">Início</th><th scope="col">Fim</th><th scope="col">Observação</th></tr></thead><tbody>{result.historico.map((item) => <tr key={item.id}>
        <td><Link className="text-emerald-700 underline" to={`/propriedades/${item.propriedadeId}`}>{item.propriedadeNome}</Link>{!item.propriedadeAtiva && ' (inativa)'}</td>
        <td>{formatDate(item.dataInicio)}</td><td>{item.dataFim ? formatDate(item.dataFim) : 'Atual'}</td><td className="max-w-sm whitespace-pre-wrap break-words">{item.observacao || '—'}</td>
      </tr>)}</tbody></table></div>}
    </>}
    {error && <p role="alert">{error}</p>}
    {success && <p role="status">{success}</p>}
    {!readOnly && result && !loadError && !loading && <form noValidate onSubmit={transfer}>
      <h3 className="mt-6 font-semibold">{currentLink ? 'Transferir ou encerrar vínculo' : 'Vincular propriedade'}</h3>
      <p className="text-sm text-slate-600">Uma transferência encerra o vínculo anterior na data informada e inicia o próximo no mesmo dia.</p>
      {catalogLoading && <p role="status">Carregando propriedades ativas…</p>}
      {catalogError && <p role="alert">Não foi possível carregar as propriedades de destino. Atualize o histórico para tentar novamente.</p>}
      {!catalogLoading && !catalogError && destinations.length === 0 && <p className="animal-notice">Não há outra propriedade ativa disponível. <Link className="font-semibold text-emerald-700 underline" to="/propriedades">Abrir Propriedades</Link></p>}
      <div className="animal-fields">
        <label htmlFor={`property-destination-${animalId}`}>Propriedade de destino<select disabled={unavailable || catalogLoading || catalogError} id={`property-destination-${animalId}`} onChange={(event) => setDestination(event.target.value)} value={destination}><option value="">Selecione uma propriedade</option>{destinations.map((property) => <option key={property.id} value={property.id}>{property.nome}</option>)}</select></label>
        <label htmlFor={`property-date-${animalId}`}>Data da movimentação<input disabled={unavailable} id={`property-date-${animalId}`} max={today()} min={minimumDate || undefined} onChange={(event) => setDate(event.target.value)} required type="date" value={date} /></label>
      </div>
      <label htmlFor={`property-move-notes-${animalId}`}>Observação do novo vínculo<textarea disabled={unavailable} id={`property-move-notes-${animalId}`} maxLength={2000} onChange={(event) => setNotes(event.target.value)} rows={2} value={notes} /></label>
      <div className="animal-actions"><button disabled={unavailable || catalogLoading || catalogError || !destination} type="submit">{saving ? 'Salvando…' : currentLink ? 'Transferir propriedade' : 'Vincular propriedade'}</button>{currentLink && <button disabled={unavailable} onClick={() => { void move(true) }} type="button">Encerrar vínculo</button>}</div>
    </form>}
    <div className="animal-actions"><button disabled={saving || loading} onClick={() => { setSuccess(null); setRetryKey((value) => value + 1) }} type="button">Atualizar histórico</button></div>
  </section>
}
