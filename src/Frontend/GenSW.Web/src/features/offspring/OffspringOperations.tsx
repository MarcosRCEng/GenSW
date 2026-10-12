import { useState, type FormEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { listReturnState } from '../../shared/details/listNavigation'
import { useUnsavedChanges } from '../../shared/navigation/useUnsavedChanges'
import { convertOffspring, splitOffspring } from './offspringService'
import type { Offspring } from './types'

// Operations belong to the existing editing route. Detail pages never mount them.
export function OffspringOperations({ item, pendingChanges = false }: { item: Offspring; pendingChanges?: boolean }) {
  const navigate = useNavigate()
  const location = useLocation()
  const [dirty, setDirty] = useState(false)
  const { markSaved } = useUnsavedChanges(dirty)
  const [species, setSpecies] = useState('')
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  async function split() {
    if (busy || pendingChanges) return
    setBusy(true); setError(null)
    try {
      const result = await splitOffspring(item.id, { origem: item.origem, data: item.data, pesoGramas: item.pesoGramas, sexo: item.sexo, condicao: item.condicao, observacao: item.observacao })
      navigate(`/proles/${result.id}`, { state: listReturnState(location.state, '/proles') })
    } catch { setError('Não foi possível desdobrar o lote.'); setBusy(false) }
  }
  async function convert(event: FormEvent) {
    event.preventDefault()
    if (busy || pendingChanges) return
    if (!species.trim()) { setError('Informe a espécie do Animal.'); return }
    setBusy(true); setError(null)
    try {
      const result = await convertOffspring(item.id, { codigoInterno: code.trim() || null, nome: name.trim() || null, especieId: species.trim(), escopo: 1 })
      markSaved(); setDirty(false)
      navigate(`/animais/${result.animal.id}/editar?paiSugerido=${result.paiSugerido.id}&maeSugerida=${result.maeSugerida.id}`)
    } catch { setError('Não foi possível converter a prole.'); setBusy(false) }
  }
  if (item.animalId) return null
  return <section className="mx-auto mt-6 max-w-xl rounded bg-white p-6">
    <h2 className="text-xl font-bold">Operações da prole</h2>
    <p className="mt-2 text-sm text-slate-600">Estas operações usam os dados salvos da prole. Salve eventuais correções antes de continuar.</p>
    {error && <p className="mt-3 text-red-700" role="alert">{error}</p>}
    {pendingChanges && <p className="mt-3 text-amber-800" role="status">Salve as alterações da prole antes de executar uma operação.</p>}
    {item.tipoRegistro === 2 ? <button className="mt-4 rounded border px-3 py-2" disabled={busy || pendingChanges || item.quantidadeDesdobrada >= item.quantidade} onClick={() => void split()} type="button">Desdobrar uma unidade em registro individual</button> : <form className="mt-5 space-y-3" onChangeCapture={() => setDirty(true)} onSubmit={event => void convert(event)}>
      <h3 className="font-bold">Converter em Animal</h3>
      <p className="text-sm">Os progenitores do cruzamento serão somente sugeridos no fluxo de Filiação. Nenhuma filiação será criada automaticamente.</p>
      <label className="block">Código interno (opcional)<input className="mt-1 w-full rounded border p-2" value={code} onChange={event => setCode(event.target.value)} /></label>
      <label className="block">Nome<input className="mt-1 w-full rounded border p-2" value={name} onChange={event => setName(event.target.value)} /></label>
      <label className="block">ID da espécie<input className="mt-1 w-full rounded border p-2" required value={species} onChange={event => setSpecies(event.target.value)} /></label>
      <button className="rounded bg-emerald-700 px-4 py-2 text-white disabled:opacity-60" disabled={busy || pendingChanges} type="submit">Criar Animal e continuar para confirmação de Filiação</button>
    </form>}
  </section>
}
