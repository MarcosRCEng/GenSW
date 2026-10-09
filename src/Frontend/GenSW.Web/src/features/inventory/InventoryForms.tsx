import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { httpRequest } from '../../shared/http/httpClient'
import { useAuth } from '../auth/hooks/useAuth'
import type { Item } from '../formulation/types'
import { InventoryFrame, Panel, Input, Select, Notice, ReferenceSelect, buttonClass, primaryClass } from './InventoryControls'
import { inventoryRead, inventoryError, parseLocal, parseLote, parseItem, parseProperty, parseProfile, parseConversion, parseResponsible } from './inventoryService'
import { useInventoryMutation } from './useInventoryMutation'
import type { Local, Lote } from './types'
import { inventoryOrigin } from './inventoryNavigation'

const optional = (value: string) => value.trim() || null
const statusCaption = (active: boolean) => active ? '' : ' · Inativo'

export function LocalFormPage() {
  const { id } = useParams()
  return <LocalForm key={id ?? 'novo'} />
}

function LocalForm() {
  const { id } = useParams()
  const location = useLocation()
  const navigate = useNavigate()
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  const [record, setRecord] = useState<Local | null>(null)
  const [form, setForm] = useState({ codigo: '', nome: '', descricao: '', propriedadeId: '', finalidade: 'Ordinario', ativo: true, motivo: '' })
  const [loading, setLoading] = useState(Boolean(id))
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  const { submit, busy, resetAttempt } = useInventoryMutation()
  const origin = inventoryOrigin(location.state, 'locais')
  const returnState = origin.state
  useEffect(() => {
    let live = true
    if (!id) return
    setLoading(true); setError(null)
    void inventoryRead(`locais/${id}`, parseLocal).then(x => { if (live) { setRecord(x); if (retry === 0) setForm({ codigo: x.codigo, nome: x.nome, descricao: x.descricao ?? '', propriedadeId: x.propriedadeId ?? '', finalidade: x.finalidade, ativo: x.ativo, motivo: '' }); setLoading(false) } }).catch(e => { if (live) { setError(inventoryError(e)); setLoading(false) } })
    return () => { live = false }
  }, [id, retry])
  const set = (key: keyof typeof form, value: string) => { resetAttempt(); setForm(x => ({ ...x, [key]: value })) }
  const save = async (e: FormEvent) => {
    e.preventDefault(); setError(null)
    try {
      const result = await submit(id ? `locais/${id}` : 'locais', { ...form, descricao: optional(form.descricao), propriedadeId: optional(form.propriedadeId), finalidade: record?.finalidade ?? form.finalidade, ativo: record?.ativo ?? form.ativo, versaoEsperada: record?.revisao ?? 0, motivo: form.motivo.trim() || 'Cadastro de local' }, parseLocal, id ? 'PUT' : 'POST')
      if (result) void navigate(`/estoque/locais/${result.id}`, { state: returnState })
    } catch (e) { setError(inventoryError(e)) }
  }
  return <InventoryFrame title={id ? 'Editar local de estoque' : 'Novo local de estoque'}><Notice error={error} retry={id ? () => setRetry(x => x + 1) : undefined} />{loading ? <p role="status">Carregando local…</p> : <Panel><form className="space-y-5" onSubmit={save}><div className="grid gap-4 sm:grid-cols-2"><Input label="Código" value={form.codigo} onChange={v => set('codigo', v)} maxLength={50} required /><Input label="Nome" value={form.nome} onChange={v => set('nome', v)} maxLength={200} required /><Input label="Descrição" value={form.descricao} onChange={v => set('descricao', v)} multiline /><ReferenceSelect label="Propriedade opcional" path="/propriedades" value={form.propriedadeId || null} params={{ ativo: true }} onChange={v => set('propriedadeId', v ?? '')} parse={parseProperty} caption={x => `${x.nome}${statusCaption(x.ativo)}`} />{!id && <><Select label="Finalidade" value={form.finalidade} onChange={v => set('finalidade', v)} options={[[ 'Ordinario', 'Ordinário'], ['Segregacao', 'Segregação']]} />{admin && <label className="flex items-center gap-2"><input type="checkbox" checked={form.ativo} onChange={e => { resetAttempt(); setForm(x => ({ ...x, ativo: e.target.checked })) }} />Local inicialmente ativo</label>}</>}<Input label="Motivo do cadastro ou alteração" value={form.motivo} onChange={v => set('motivo', v)} required={Boolean(id)} /></div><p className="text-sm text-slate-600">Propriedade contextualiza o local. Seu cadastro não cria local automaticamente nem altera permissões.</p>{id && <p>Revisão atual {record?.revisao}. Recarregar mantém o preenchimento para revisão consciente. Status e finalidade possuem comandos administrativos próprios.</p>}<div className="flex flex-wrap gap-3"><button className={primaryClass} disabled={busy || (Boolean(id) && !record)} type="submit">{busy ? 'Salvando…' : 'Salvar local'}</button><Link className={buttonClass} to={returnState?.list.path ?? origin.path} state={returnState}>Voltar para {origin.label}</Link></div></form></Panel>}</InventoryFrame>
}

export function LoteFormPage() {
  const { id } = useParams()
  return <LoteForm key={id ?? 'novo'} />
}

function LoteForm() {
  const { id } = useParams()
  const location = useLocation()
  const navigate = useNavigate()
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  const [record, setRecord] = useState<Lote | null>(null)
  const [item, setItem] = useState<Item | null>(null)
  const [form, setForm] = useState({ itemId: '', codigo: '', codigoExterno: '', origem: '', fonte: '', responsavelId: user?.userId ?? '', dataOrigem: '', fabricacao: '', coleta: '', validade: '', fonteValidade: '', responsavelValidadeId: '', perfilNutricionalId: '', conversaoItemId: '', aplicabilidade: '', pendente: false, motivo: '' })
  const [loading, setLoading] = useState(Boolean(id))
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  const { submit, busy, resetAttempt } = useInventoryMutation()
  const origin = inventoryOrigin(location.state, 'lotes')
  const returnState = origin.state
  useEffect(() => {
    let live = true
    if (!id) return
    setLoading(true); setError(null)
    void inventoryRead(`lotes/${id}`, parseLote).then(x => {
      if (live) { setRecord(x); if (retry === 0) setForm({ itemId: x.itemId, codigo: x.codigo, codigoExterno: x.codigoExterno ?? '', origem: x.origem, fonte: x.fonte, responsavelId: x.responsavelId, dataOrigem: x.dataOrigem ?? '', fabricacao: x.fabricacao ?? '', coleta: x.coleta ?? '', validade: x.validade ?? '', fonteValidade: x.fonteValidade ?? '', responsavelValidadeId: x.responsavelValidadeId ?? '', perfilNutricionalId: x.perfilNutricionalId ?? '', conversaoItemId: x.conversaoItemId ?? '', aplicabilidade: x.aplicabilidade ?? '', pendente: x.situacao === 'Bloqueado', motivo: '' }); setLoading(false) }
    }).catch(e => { if (live) { setError(inventoryError(e)); setLoading(false) } })
    return () => { live = false }
  }, [id, retry])
  useEffect(() => {
    let live = true; setItem(null)
    if (!form.itemId) return
    void httpRequest<unknown>(`/itens/${form.itemId}`, { authenticated: true }).then(parseItem).then(x => { if (live) setItem(x) }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [form.itemId, retry])
  const set = (key: keyof typeof form, value: string) => { resetAttempt(); setForm(x => ({ ...x, [key]: value })) }
  const save = async (e: FormEvent) => {
    e.preventDefault(); setError(null)
    try {
      if (!item || !form.responsavelId) throw new Error('Selecione Item e Responsável antes de salvar.')
      const body = { ...form, codigoExterno: optional(form.codigoExterno), dataOrigem: optional(form.dataOrigem), fabricacao: optional(form.fabricacao), coleta: optional(form.coleta), validade: optional(form.validade), fonteValidade: optional(form.fonteValidade), responsavelValidadeId: optional(form.responsavelValidadeId), perfilNutricionalId: optional(form.perfilNutricionalId), conversaoItemId: optional(form.conversaoItemId), aplicabilidade: optional(form.aplicabilidade), versaoEsperada: record?.revisao ?? 0, itemVersaoEsperada: item.revisao, motivo: form.motivo.trim() || 'Cadastro de lote' }
      const result = await submit(id ? `lotes/${id}` : 'lotes', body, parseLote, id ? 'PUT' : 'POST')
      if (result) void navigate(`/estoque/lotes/${result.id}`, { state: returnState })
    } catch (e) { setError(inventoryError(e)) }
  }
  return <InventoryFrame title={id ? 'Editar metadados do lote' : 'Novo lote de material'}><Notice error={error} retry={id ? () => setRetry(x => x + 1) : undefined} />{loading ? <p role="status">Carregando lote…</p> : <Panel><form className="space-y-5" onSubmit={save}><div className="grid min-w-0 gap-4 sm:grid-cols-2">
    {id ? <p>Item {record?.itemCodigo} — {record?.itemNome}; código {record?.codigo}; unidade {record?.unidade}. Identidade física preservada.</p> : <ReferenceSelect label="Item" path="/itens" value={form.itemId || null} params={admin ? {} : { ativo: true, capacidade: 'entrada' }} parse={parseItem} caption={x => `${x.codigo} — ${x.nome} · ${x.unidade}${statusCaption(x.ativo)}`} onChange={(v, x) => { resetAttempt(); setForm(f => ({ ...f, itemId: v ?? '', perfilNutricionalId: '', conversaoItemId: '' })); if (x) setItem(x) }} />}
    <Input label="Código interno único" value={form.codigo} onChange={v => set('codigo', v)} maxLength={50} disabled={Boolean(id)} required /><Input label="Código externo" value={form.codigoExterno} onChange={v => set('codigoExterno', v)} maxLength={100} /><Input label="Origem" value={form.origem} onChange={v => set('origem', v)} maxLength={1000} required /><Input label="Fonte" value={form.fonte} onChange={v => set('fonte', v)} maxLength={1000} required /><ReferenceSelect label="Responsável" path="/estoque/responsaveis" value={form.responsavelId || null} params={{ ativo: true }} onChange={v => set('responsavelId', v ?? '')} parse={parseResponsible} caption={x => `${x.nome}${statusCaption(x.ativo)}`} /><Input label="Data de origem" type="date" value={form.dataOrigem} onChange={v => set('dataOrigem', v)} /><Input label="Fabricação" type="date" value={form.fabricacao} onChange={v => set('fabricacao', v)} /><Input label="Coleta" type="date" value={form.coleta} onChange={v => set('coleta', v)} />
    {!id ? <><Input label="Validade declarada" type="date" value={form.validade} onChange={v => set('validade', v)} hint="Ausência de validade permanece explícita; não certifica uso seguro." />{form.validade && <><Input label="Fonte da validade" value={form.fonteValidade} onChange={v => set('fonteValidade', v)} maxLength={1000} required /><ReferenceSelect label="Responsável pela validade" path="/estoque/responsaveis" value={form.responsavelValidadeId || null} params={{ ativo: true }} onChange={v => set('responsavelValidadeId', v ?? '')} parse={parseResponsible} caption={x => `${x.nome}${statusCaption(x.ativo)}`} /></>}{form.itemId && <><ReferenceSelect label="Perfil nutricional opcional" path={`/itens/${form.itemId}/perfis-nutricionais`} detailPath={v => `/perfis-nutricionais/${v}`} value={form.perfilNutricionalId || null} params={{ estado: 'Publicado' }} onChange={v => set('perfilNutricionalId', v ?? '')} parse={parseProfile} caption={x => `${x.conteudo.nome} · versão ${x.numero} · ${x.estado}`} /><ReferenceSelect label="Conversão documental opcional" path={`/itens/${form.itemId}/conversoes`} detailPath={v => `/estoque/conversoes/${v}`} value={form.conversaoItemId || null} onChange={v => set('conversaoItemId', v ?? '')} parse={parseConversion} caption={x => `${x.origem} → ${x.destino} · versão ${x.numero} · ${x.fonte}`} /><Input label="Justificativa de aplicabilidade" value={form.aplicabilidade} onChange={v => set('aplicabilidade', v)} multiline required={Boolean(form.perfilNutricionalId || form.conversaoItemId)} /></>}<label className="flex items-center gap-2"><input type="checkbox" checked={form.pendente} onChange={e => { resetAttempt(); setForm(x => ({ ...x, pendente: e.target.checked })) }} />Declarar pendência — lote inicialmente Bloqueado</label></> : <p>Validade, situação e referências conservadas. Alterações exigem os comandos Admin no detalhe.</p>}
    <Input label="Motivo do cadastro ou alteração" value={form.motivo} onChange={v => set('motivo', v)} required={Boolean(id)} />
  </div><p className="text-sm text-slate-600">Cadastro vazio não recebe estoque. A unidade do Item será fixada para preservar os fatos físicos.</p>{record && <p>Revisão atual {record.revisao}. Recarregar conserva os campos preenchidos.</p>}<div className="flex flex-wrap gap-3"><button className={primaryClass} type="submit" disabled={busy || !item}>{busy ? 'Salvando…' : 'Salvar lote'}</button><Link className={buttonClass} to={returnState?.list.path ?? origin.path} state={returnState}>Voltar para {origin.label}</Link></div></form></Panel>}</InventoryFrame>
}

export function InventoryStatePage({ kind }: { kind: 'locais' | 'lotes' }) {
  const { id, command } = useParams()
  return <InventoryStateForm key={`${kind}:${id}:${command}`} kind={kind} />
}

function InventoryStateForm({ kind }: { kind: 'locais' | 'lotes' }) {
  const { id, command } = useParams()
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  const navigate = useNavigate()
  const [record, setRecord] = useState<Local | Lote | null>(null)
  const [form, setForm] = useState({ motivo: '', evidencia: '', finalidade: 'Ordinario', validade: '', fonteValidade: '', responsavelId: user?.userId ?? '', perfilNutricionalId: '', conversaoItemId: '', aplicabilidade: '' })
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  const { submit, busy, resetAttempt } = useInventoryMutation()
  const allowed = kind === 'locais' ? ['ativo', 'finalidade'] : ['ativo', 'bloqueio', 'liberacao', 'encerramento', 'validade', 'referencias']
  useEffect(() => {
    let live = true
    if (!id || !admin) return
    const request = kind === 'locais' ? inventoryRead(`locais/${id}`, parseLocal) : inventoryRead(`lotes/${id}`, parseLote)
    void request.then(x => { if (live) { setRecord(x); if (retry === 0) { if ('itemId' in x) setForm(f => ({ ...f, validade: x.validade ?? '', fonteValidade: x.fonteValidade ?? '', responsavelId: x.responsavelValidadeId ?? user?.userId ?? '', perfilNutricionalId: x.perfilNutricionalId ?? '', conversaoItemId: x.conversaoItemId ?? '', aplicabilidade: x.aplicabilidade ?? '' })); else setForm(f => ({ ...f, finalidade: x.finalidade })) } } }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [id, kind, admin, retry, user?.userId])
  const set = (key: keyof typeof form, value: string) => { resetAttempt(); setForm(x => ({ ...x, [key]: value })) }
  const save = async (e: FormEvent) => {
    e.preventDefault(); setError(null)
    if (!record || !id || !command) return
    try {
      const body = { versaoEsperada: record.revisao, motivo: form.motivo, evidencia: form.evidencia, ativo: command === 'ativo' ? !record.ativo : null, finalidade: command === 'finalidade' ? form.finalidade : null, validade: command === 'validade' ? optional(form.validade) : null, fonteValidade: command === 'validade' ? optional(form.fonteValidade) : null, responsavelId: command === 'validade' ? optional(form.responsavelId) : null, perfilNutricionalId: command === 'referencias' ? optional(form.perfilNutricionalId) : null, conversaoItemId: command === 'referencias' ? optional(form.conversaoItemId) : null, aplicabilidade: command === 'referencias' ? optional(form.aplicabilidade) : null }
      const parse = kind === 'locais' ? (v: unknown) => parseLocal(v) as Local | Lote : (v: unknown) => parseLote(v) as Local | Lote
      const result = await submit(`${kind}/${id}/${command}`, body, parse, command === 'ativo' ? 'PATCH' : 'POST')
      if (result) void navigate(`/estoque/${kind}/${id}`)
    } catch (e) { setError(inventoryError(e)) }
  }
  const title = ({ ativo: record?.ativo ? 'Inativar' : 'Reativar', finalidade: 'Alterar finalidade', bloqueio: 'Bloquear lote', liberacao: 'Liberar lote', encerramento: 'Encerrar lote', validade: 'Corrigir validade', referencias: 'Alterar referências' } as Record<string, string>)[command ?? ''] ?? 'Comando administrativo'
  return <InventoryFrame title={title}>{!admin ? <Notice error="Esta operação exige o papel Admin. Nenhum comando foi enviado." /> : !command || !allowed.includes(command) ? <Notice error="Comando não encontrado." /> : <><Notice error={error} retry={() => setRetry(x => x + 1)} />{!record ? !error && <p role="status">Carregando registro…</p> : <Panel><p>{record.codigo} · revisão {record.revisao}. Quantidades e história permanecem preservadas.</p><form onSubmit={save} className="space-y-4"><Input label="Motivo" value={form.motivo} onChange={v => set('motivo', v)} required multiline /><Input label="Evidência da conferência" value={form.evidencia} onChange={v => set('evidencia', v)} required multiline />
    {command === 'finalidade' && <><Select label="Nova finalidade" value={form.finalidade} onChange={v => set('finalidade', v)} options={[[ 'Ordinario', 'Ordinário'], ['Segregacao', 'Segregação']]} /><p>Alteração exige saldo zero em todas as posições.</p></>}
    {command === 'validade' && <><Input label="Validade declarada" type="date" value={form.validade} onChange={v => set('validade', v)} hint="Vazio = não informada; não existe data sanitária inferida." /><Input label="Fonte da validade" value={form.fonteValidade} onChange={v => set('fonteValidade', v)} required={Boolean(form.validade)} /><ReferenceSelect label="Responsável pela validade" path="/estoque/responsaveis" value={form.responsavelId || null} params={{ ativo: true }} parse={parseResponsible} caption={x => x.nome + statusCaption(x.ativo)} onChange={v => set('responsavelId', v ?? '')} /></>}
    {command === 'referencias' && 'itemId' in record && <><ReferenceSelect label="Perfil nutricional opcional" path={`/itens/${record.itemId}/perfis-nutricionais`} detailPath={v => `/perfis-nutricionais/${v}`} value={form.perfilNutricionalId || null} params={{ estado: 'Publicado' }} parse={parseProfile} caption={x => `${x.conteudo.nome} · versão ${x.numero} · ${x.estado}`} onChange={v => set('perfilNutricionalId', v ?? '')} /><ReferenceSelect label="Conversão documental opcional" path={`/itens/${record.itemId}/conversoes`} detailPath={v => `/estoque/conversoes/${v}`} value={form.conversaoItemId || null} parse={parseConversion} caption={x => `${x.origem} → ${x.destino} · versão ${x.numero} · ${x.fonte}`} onChange={v => set('conversaoItemId', v ?? '')} /><Input label="Justificativa de aplicabilidade" value={form.aplicabilidade} onChange={v => set('aplicabilidade', v)} multiline required={Boolean(form.perfilNutricionalId || form.conversaoItemId)} /></>}
    {command === 'liberacao' && <p>Liberação não remove o vencimento; material vencido continua impedido para uso ordinário.</p>}{command === 'encerramento' && <p>Encerramento é terminal e exige saldo zero em todos os locais.</p>}<div className="flex flex-wrap gap-3"><button className={primaryClass} type="submit" disabled={busy}>{busy ? 'Confirmando…' : 'Confirmar comando'}</button><Link className={buttonClass} to={`/estoque/${kind}/${id}`}>Voltar ao detalhe</Link></div></form></Panel>}</>}</InventoryFrame>
}
