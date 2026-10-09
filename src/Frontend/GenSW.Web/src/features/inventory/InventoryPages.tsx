import { useEffect, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import { useAuth } from '../auth/hooks/useAuth'
import { httpRequest } from '../../shared/http/httpClient'
import { DetailField, DetailsPage } from '../../shared/details/DetailsPage'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { useRememberListState, useRestoredListState } from '../../shared/details/listNavigation'
import { InventoryFrame, Panel, Input, Select, Notice, Pagination, ReferenceSelect, ScrollTable, buttonClass } from './InventoryControls'
import { inventoryError, inventoryList, inventoryRead, parseEvent, parseHistory, parseLocal, parseLote, parseReconciliation, parseSaldo, parseItem, parseProperty, parseResponsible, parseProfile, parseConversion, showQuantity, showTime } from './inventoryService'
import { operationInfo, operationLabel, type Event, type History, type InventoryPage, type Local, type Lote, type Reconciliation, type Saldo } from './types'
import { inventoryOrigin } from './inventoryNavigation'

type Resource = 'locais' | 'lotes' | 'saldos' | 'movimentos' | 'reconciliacao'
type Row = Local | Lote | Saldo | Event | Reconciliation
const names: Record<Resource, string> = { locais: 'Locais de estoque', lotes: 'Lotes de materiais', saldos: 'Saldos de estoque', movimentos: 'Movimentos de estoque', reconciliacao: 'Reconciliação de estoque' }
const parsers: Record<Resource, (v: unknown) => Row> = { locais: parseLocal, lotes: parseLote, saldos: parseSaldo, movimentos: parseEvent, reconciliacao: parseReconciliation }
const activeOptions = [['', 'Todos'], ['true', 'Ativos'], ['false', 'Inativos']] as const
const booleanOptions = [['', 'Todos'], ['true', 'Sim'], ['false', 'Não']] as const
const situationOptions = [['', 'Todas'], ['Liberado', 'Liberado'], ['Bloqueado', 'Bloqueado'], ['Encerrado', 'Encerrado']] as const
const definitionClass = 'grid min-w-0 gap-5 rounded-xl border bg-white p-4 sm:grid-cols-2 sm:p-6'
const readLocal = (id: string) => inventoryRead(`locais/${id}`, parseLocal)
const readLote = (id: string) => inventoryRead(`lotes/${id}`, parseLote)
const readEvent = (id: string) => inventoryRead(`movimentos/${id}`, parseEvent)

function ReferenceText<T>({ path, parse, caption }: { path: string; parse: (v: unknown) => T; caption: (v: T) => string }) {
  const [value, setValue] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true; setValue(null); setError(null)
    void httpRequest<unknown>(path, { authenticated: true }).then(parse).then(x => { if (live) setValue(x) }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [path, parse, retry])
  return value ? <>{caption(value)}</> : error ? <span>{error} <button className="text-emerald-700 underline" type="button" onClick={() => setRetry(x => x + 1)}>Reconsultar referência</button></span> : <span>Consultando referência…</span>
}

function OperationLinks() {
  const { user } = useAuth()
  const admin = user?.roles.includes('Admin') === true
  return <nav className="flex flex-wrap gap-2" aria-label="Operações de estoque">{Object.entries(operationInfo).filter(([, op]) => !op.admin || admin).map(([type, op]) => <Link className={buttonClass} key={type} to={`/estoque/operacoes/${type}`}>{op.label}</Link>)}</nav>
}

export function InventoryListPage({ resource }: { resource: Resource }) {
  const path = `/estoque/${resource}`
  const initial = useRestoredListState<Record<string, string | number>>(path, { page: 1, pageSize: 25, search: '', sortBy: resource === 'movimentos' ? 'sequencia' : resource === 'saldos' ? 'itemCodigo' : 'codigo', sortDirection: resource === 'movimentos' ? 'desc' : 'asc' })
  const [filters, setFilters] = useState(initial)
  const [data, setData] = useState<InventoryPage<Row> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [retry, setRetry] = useState(0)
  const navigation = useRememberListState(path, filters)
  const encoded = JSON.stringify(filters)
  useEffect(() => {
    let live = true
    setLoading(true); setError(null)
    void inventoryList(resource, JSON.parse(encoded), parsers[resource]).then(result => {
      if (!live) return
      if (result.totalPages > 0 && result.page > result.totalPages) { setFilters(x => ({ ...x, page: result.totalPages })); return }
      setData(result); setLoading(false)
    }).catch(e => { if (live) { setError(inventoryError(e)); setLoading(false) } })
    return () => { live = false }
  }, [resource, encoded, retry])
  const set = (key: string, value: string) => setFilters(x => ({ ...x, [key]: value, page: 1 }))
  const value = (key: string) => String(filters[key] ?? '')
  const itemFilter = ['lotes', 'saldos', 'movimentos'].includes(resource)
  const localFilter = ['lotes', 'saldos', 'movimentos', 'reconciliacao'].includes(resource)
  const lotFilter = ['saldos', 'movimentos', 'reconciliacao'].includes(resource)
  return <InventoryFrame title={names[resource]}>
    {resource !== 'reconciliacao' && <Panel>{resource === 'locais' || resource === 'lotes' ? <Link className={buttonClass} to={`${path}/novo`} state={navigation}>Novo {resource === 'locais' ? 'local' : 'lote'}</Link> : null}<OperationLinks /></Panel>}
    <Panel title="Filtros"><div className="grid min-w-0 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {['locais', 'lotes'].includes(resource) && <Input label="Buscar código ou nome" value={value('search')} onChange={v => set('search', v)} maxLength={200} />}
      {['locais', 'lotes', 'saldos'].includes(resource) && <Select label="Status" value={value('ativo')} onChange={v => set('ativo', v)} options={activeOptions} />}
      {resource === 'locais' && <Select label="Finalidade" value={value('finalidade')} onChange={v => set('finalidade', v)} options={[[ '', 'Todas'], ['Ordinario', 'Ordinário'], ['Segregacao', 'Segregação']]} />}
      {['lotes', 'saldos'].includes(resource) && <><Select label="Situação do lote" value={value('situacao')} onChange={v => set('situacao', v)} options={situationOptions} /><Input label="Validade até" type="date" value={value('validadeAte')} onChange={v => set('validadeAte', v)} /><Select label="Sem validade informada" value={value('semValidade')} onChange={v => set('semValidade', v)} options={booleanOptions} /><Select label="Com saldo" value={value('comSaldo')} onChange={v => set('comSaldo', v)} options={booleanOptions} /></>}
      {resource === 'saldos' && <Select label="Elegível para uso ordinário" value={value('elegivel')} onChange={v => set('elegivel', v)} options={booleanOptions} />}
      {resource === 'movimentos' && <><Select label="Tipo de movimento" value={value('tipo')} onChange={v => set('tipo', v)} options={[[ '', 'Todos'], ...Object.values(operationInfo).map(op => [op.eventType, op.label] as const)]} /><Input label="Data inicial" type="date" value={value('dataDe')} onChange={v => set('dataDe', v)} /><Input label="Data final" type="date" value={value('dataAte')} onChange={v => set('dataAte', v)} /><Input label="Sequência inicial" value={value('seqDe')} onChange={v => set('seqDe', v)} /><Input label="Sequência limite" value={value('seqAte')} onChange={v => set('seqAte', v)} /><ReferenceSelect label="Autor" path="/estoque/responsaveis" value={value('autorId') || null} onChange={v => set('autorId', v ?? '')} parse={parseResponsible} caption={x => `${x.nome}${x.ativo ? '' : ' · Inativo'}`} /></>}
      {itemFilter && <ReferenceSelect label="Item" path="/itens" value={value('itemId') || null} onChange={v => set('itemId', v ?? '')} parse={parseItem} caption={x => `${x.codigo} — ${x.nome}${x.ativo ? '' : ' · Inativo'}`} />}
      {localFilter && <ReferenceSelect label="Local" path="/estoque/locais" value={value('localId') || null} onChange={v => set('localId', v ?? '')} parse={parseLocal} caption={x => `${x.codigo} — ${x.nome}${x.ativo ? '' : ' · Inativo'}`} />}
      {lotFilter && <ReferenceSelect label="Lote" path="/estoque/lotes" value={value('loteId') || null} onChange={v => set('loteId', v ?? '')} parse={parseLote} caption={x => `${x.codigo} — ${x.itemNome} · ${x.situacao}`} />}
      {['locais', 'saldos'].includes(resource) && <ReferenceSelect label="Propriedade" path="/propriedades" value={value('propriedadeId') || null} onChange={v => set('propriedadeId', v ?? '')} parse={parseProperty} caption={x => `${x.nome}${x.ativo ? '' : ' · Inativa'}`} />}
      {resource !== 'reconciliacao' && <><Select label="Ordenar por" value={value('sortBy')} onChange={v => set('sortBy', v)} options={resource === 'movimentos' ? [['sequencia', 'Sequência']] : resource === 'saldos' ? [['itemCodigo', 'Item'], ['loteCodigo', 'Lote'], ['localCodigo', 'Local']] : resource === 'lotes' ? [['codigo', 'Código'], ['createdAtUtc', 'Cadastro']] : [['codigo', 'Código'], ['nome', 'Nome'], ['createdAtUtc', 'Cadastro']]} /><Select label="Direção" value={value('sortDirection')} onChange={v => set('sortDirection', v)} options={[[ 'asc', 'Crescente'], ['desc', 'Decrescente']]} /></>}
      <Select label="Registros por página" value={value('pageSize')} onChange={v => setFilters(x => ({ ...x, pageSize: Number(v), page: 1 }))} options={[[ '10', '10'], ['25', '25'], ['50', '50'], ['100', '100']]} />
    </div><button className={buttonClass} type="button" onClick={() => setFilters({ page: 1, pageSize: 25, search: '', sortBy: resource === 'movimentos' ? 'sequencia' : resource === 'saldos' ? 'itemCodigo' : 'codigo', sortDirection: resource === 'movimentos' ? 'desc' : 'asc' })}>Limpar filtros</button></Panel>
    <Notice error={error} retry={() => setRetry(x => x + 1)} />{loading && <p role="status">Carregando estoque…</p>}
    {data && !loading && !error && <Panel><p className="text-sm text-slate-600">{data.totalItems} registros · observado em {showTime(data.observadoEmUtc)} · sequência {data.sequenciaAte}. Cada página pode refletir uma consulta mais recente.</p>
      {resource === 'reconciliacao' && <p>Consulta do livro e da projeção. Divergências exigem diagnóstico; esta tela não modifica saldos.</p>}
      {!data.items.length ? <p>{resource === 'reconciliacao' ? 'Sem divergências no corte consultado.' : 'Nenhum registro encontrado.'}</p> : <ListTable resource={resource} rows={data.items} navigation={navigation} />}
      <Pagination page={Number(filters.page)} totalPages={data.totalPages} onChange={page => setFilters(x => ({ ...x, page }))} />
    </Panel>}
  </InventoryFrame>
}

function ListTable({ resource, rows, navigation }: { resource: Resource; rows: Row[]; navigation: unknown }) {
  const heads = resource === 'locais' ? ['Código / nome', 'Finalidade', 'Propriedade', 'Status', 'Ações'] : resource === 'lotes' ? ['Lote', 'Item', 'Situação', 'Validade', 'Ações'] : resource === 'saldos' ? ['Item / lote', 'Local', 'Físico contabilizado', 'Utilizável', 'Disponibilidade', 'Ações'] : resource === 'movimentos' ? ['Sequência', 'Operação', 'Data operacional', 'Motivo', 'Ações'] : ['Lote', 'Local', 'Livro', 'Projeção', 'Diferença']
  return <ScrollTable label={names[resource]}><thead><tr>{heads.map(h => <th scope="col" key={h}>{h}</th>)}</tr></thead><tbody>{rows.map((row, index) => {
    if (resource === 'locais') { const r = row as Local; return <tr key={r.id}><td>{r.codigo} — {r.nome}</td><td>{r.finalidade === 'Segregacao' ? 'Segregação' : 'Ordinário'}</td><td>{r.propriedadeNome ?? 'Não vinculada'}</td><td>{r.ativo ? 'Ativo' : 'Inativo'}</td><td><div className="flex gap-3"><Link className="font-semibold text-emerald-700 underline" to={`/estoque/locais/${r.id}`} state={navigation}>Visualizar</Link><Link className="font-semibold text-emerald-700 underline" to={`/estoque/locais/${r.id}/editar`} state={navigation}>Editar</Link></div></td></tr> }
    if (resource === 'lotes') { const r = row as Lote; return <tr key={r.id}><td>{r.codigo}{!r.ativo && ' · Inativo'}</td><td>{r.itemCodigo} — {r.itemNome}</td><td>{r.situacao}</td><td>{r.validade ?? 'Não informada'}</td><td><div className="flex gap-3"><Link className="font-semibold text-emerald-700 underline" to={`/estoque/lotes/${r.id}`} state={navigation}>Visualizar</Link><Link className="font-semibold text-emerald-700 underline" to={`/estoque/lotes/${r.id}/editar`} state={navigation}>Editar</Link></div></td></tr> }
    if (resource === 'saldos') { const r = row as Saldo; return <tr key={`${r.loteId}/${r.localId}`}><td>{r.itemCodigo} — {r.itemNome}<br />Lote {r.loteCodigo}</td><td>{r.localCodigo} — {r.localNome}</td><td>{showQuantity(r.quantidade)} {r.unidade}</td><td>{showQuantity(r.quantidadeElegivel)} {r.unidade}</td><td><p>{r.elegivel ? 'Elegível' : r.motivosIndisponibilidade.join(' · ')}</p>{r.avisos.length > 0 && <p className="mt-1 text-amber-800">Aviso: {r.avisos.join(' · ')}</p>}</td><td><Link className="font-semibold text-emerald-700 underline" to={`/estoque/lotes/${r.loteId}`} state={navigation}>Visualizar lote</Link></td></tr> }
    if (resource === 'movimentos') { const r = row as Event; return <tr key={r.id}><td>{r.sequencia}</td><td>{operationLabel(r.tipo)}</td><td>{r.dataOperacional}</td><td className="min-w-48 max-w-md break-words">{r.motivo}</td><td><Link className="font-semibold text-emerald-700 underline" to={`/estoque/movimentos/${r.id}`} state={navigation}>Visualizar</Link></td></tr> }
    const r = row as Reconciliation; return <tr key={index}><td><Link className="text-emerald-700 underline" to={`/estoque/lotes/${r.loteId}`} state={navigation}>{r.loteId}</Link></td><td><Link className="text-emerald-700 underline" to={`/estoque/locais/${r.localId}`} state={navigation}>{r.localId}</Link></td><td>{showQuantity(r.quantidadeLivro)} {r.unidade}</td><td>{showQuantity(r.quantidadeProjecao)} {r.unidade}</td><td>{showQuantity(r.diferenca)} {r.unidade}</td></tr>
  })}</tbody></ScrollTable>
}

export function InventoryHistory({ kind, id }: { kind: 'locais' | 'lotes'; id: string }) {
  const [page, setPage] = useState(1)
  const [data, setData] = useState<InventoryPage<History> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true; setData(null); setError(null)
    void inventoryList(`${kind}/${id}/historico`, { page, pageSize: 25 }, parseHistory).then(x => { if (live) setData(x) }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [kind, id, page, retry])
  return <Panel title="Histórico"><Notice error={error} retry={() => setRetry(x => x + 1)} />{!data && !error && <p role="status">Carregando histórico…</p>}{data && <>{!data.items.length && <p>Nenhum histórico registrado.</p>}{data.items.map(row => <details key={row.id} className="rounded-lg border p-3"><summary className="cursor-pointer font-medium">{row.operacao} · {showTime(row.createdAtUtc)} · sequência {row.sequencia}</summary><p className="my-2">{row.motivo}</p><p className="text-sm">Autor: <ReferenceText path={`/estoque/responsaveis/${row.autorId}`} parse={parseResponsible} caption={x => `${x.nome}${x.ativo ? '' : ' · Inativo'}`} /></p><Snapshot value={row.antesJson} label="Antes" /><Snapshot value={row.depoisJson} label="Depois" /></details>)}<Pagination page={page} totalPages={data.totalPages} onChange={setPage} /></>}</Panel>
}

function Snapshot({ value, label }: { value: string; label: string }) {
  return <details className="mt-3 min-w-0"><summary className="cursor-pointer">{label}</summary><pre className="mt-2 max-w-full overflow-auto whitespace-pre-wrap break-words rounded bg-slate-50 p-3 text-xs">{value}</pre></details>
}

function frozenName(snapshot: string, kind: 'lote' | 'local', id: string): string | null {
  const source: unknown = JSON.parse(snapshot)
  if (!source || typeof source !== 'object') return null
  const data = source as Record<string, unknown>
  const record = kind === 'lote' ? data.lote : Array.isArray(data.locais) ? data.locais.find(x => x && typeof x === 'object' && 'id' in x && x.id === id) : null
  if (!record || typeof record !== 'object' || !('codigo' in record) || typeof record.codigo !== 'string') return null
  const name = kind === 'lote' ? data.item : record
  return `${record.codigo}${name && typeof name === 'object' && 'nome' in name && typeof name.nome === 'string' ? ` — ${name.nome}` : ''}`
}

export function LocalDetailsPage() {
  const { id } = useParams()
  const origin = inventoryOrigin(useLocation().state, 'locais')
  const { user } = useAuth()
  const { record, state, retry } = useRecordDetails(id, readLocal)
  const admin = user?.roles.includes('Admin') === true
  return <DetailsPage title="Visualizar local de estoque" listPath={origin.path} listLabel={origin.label} state={state} onRetry={retry} editPath={id ? `/estoque/locais/${id}/editar` : undefined}>{record && <>
    <dl className={definitionClass}><DetailField label="Código">{record.codigo}</DetailField><DetailField label="Nome">{record.nome}</DetailField><DetailField label="Status">{record.ativo ? 'Ativo' : 'Inativo'}</DetailField><DetailField label="Finalidade">{record.finalidade === 'Segregacao' ? 'Segregação' : 'Ordinário'}</DetailField><DetailField label="Descrição">{record.descricao}</DetailField><DetailField label="Propriedade">{record.propriedadeId ? <Link className="text-emerald-700 underline" to={`/propriedades/${record.propriedadeId}`}>{record.propriedadeNome}</Link> : 'Não vinculada'}</DetailField><DetailField label="Revisão">{record.revisao}</DetailField></dl>
    <Panel><Link className={buttonClass} to="/estoque/saldos" state={{ list: { path: '/estoque/saldos', filters: { localId: record.id, page: 1, pageSize: 25, sortBy: 'itemCodigo', sortDirection: 'asc' } } }}>Consultar saldos do local</Link>{admin && <nav className="mt-3 flex flex-wrap gap-2" aria-label="Administração do local"><Link className={buttonClass} to={`/estoque/locais/${record.id}/comandos/ativo`}>{record.ativo ? 'Inativar local' : 'Reativar local'}</Link><Link className={buttonClass} to={`/estoque/locais/${record.id}/comandos/finalidade`}>Alterar finalidade</Link></nav>}</Panel>
    <InventoryHistory kind="locais" id={record.id} />
  </>}</DetailsPage>
}

export function LoteDetailsPage() {
  const { id } = useParams()
  const origin = inventoryOrigin(useLocation().state, 'lotes')
  const { user } = useAuth()
  const { record, state, retry } = useRecordDetails(id, readLote)
  const admin = user?.roles.includes('Admin') === true
  return <DetailsPage title="Visualizar lote de material" listPath={origin.path} listLabel={origin.label} state={state} onRetry={retry} editPath={id ? `/estoque/lotes/${id}/editar` : undefined}>{record && <>
    <dl className={definitionClass}><DetailField label="Código interno">{record.codigo}</DetailField><DetailField label="Item"><Link className="text-emerald-700 underline" to={`/itens/${record.itemId}`}>{record.itemCodigo} — {record.itemNome}{!record.itemAtivo && ' · Inativo'}</Link></DetailField><DetailField label="Unidade canônica">{record.unidade}</DetailField><DetailField label="Código externo">{record.codigoExterno}</DetailField><DetailField label="Status">{record.ativo ? 'Ativo' : 'Inativo'}</DetailField><DetailField label="Situação">{record.situacao}</DetailField><DetailField label="Origem">{record.origem}</DetailField><DetailField label="Fonte">{record.fonte}</DetailField><DetailField label="Responsável">{record.responsavelNome}</DetailField><DetailField label="Data de origem">{record.dataOrigem}</DetailField><DetailField label="Fabricação">{record.fabricacao}</DetailField><DetailField label="Coleta">{record.coleta}</DetailField><DetailField label="Validade">{record.validade ?? 'Não informada — não comprova adequação para uso'}</DetailField><DetailField label="Fonte da validade">{record.fonteValidade}</DetailField><DetailField label="Perfil nutricional">{record.perfilNutricionalId ? <Link className="text-emerald-700 underline" to={`/producao/perfis/${record.perfilNutricionalId}`}><ReferenceText path={`/perfis-nutricionais/${record.perfilNutricionalId}`} parse={parseProfile} caption={x => `${x.conteudo.nome} · versão ${x.numero} · ${x.estado}`} /></Link> : 'Não selecionado — composição desconhecida'}</DetailField><DetailField label="Conversão documental">{record.conversaoItemId ? <ReferenceText path={`/estoque/conversoes/${record.conversaoItemId}`} parse={parseConversion} caption={x => `${x.origem} → ${x.destino} · versão ${x.numero} · ${x.fonte} · ${x.proveniencia}`} /> : 'Não selecionada'}</DetailField><DetailField label="Aplicabilidade">{record.aplicabilidade}</DetailField><DetailField label="Revisão">{record.revisao}</DetailField></dl>
    <Panel><Link className={buttonClass} to="/estoque/saldos" state={{ list: { path: '/estoque/saldos', filters: { loteId: record.id, page: 1, pageSize: 25, sortBy: 'itemCodigo', sortDirection: 'asc' } } }}>Consultar posições do lote</Link><nav className="mt-3 flex flex-wrap gap-2" aria-label="Operar lote">{Object.entries(operationInfo).filter(([, op]) => !op.admin || admin).map(([type, op]) => <Link key={type} className={buttonClass} to={`/estoque/operacoes/${type}?lote=${record.id}`}>{op.label}</Link>)}</nav>{admin && <nav className="mt-3 flex flex-wrap gap-2" aria-label="Administração do lote">{[['ativo', record.ativo ? 'Inativar lote' : 'Reativar lote'], ['bloqueio', 'Bloquear lote'], ['liberacao', 'Liberar lote'], ['encerramento', 'Encerrar lote'], ['validade', 'Corrigir validade'], ['referencias', 'Alterar referências']].map(([type, label]) => <Link className={buttonClass} key={type} to={`/estoque/lotes/${record.id}/comandos/${type}`}>{label}</Link>)}</nav>}</Panel><InventoryHistory kind="lotes" id={record.id} />
  </>}</DetailsPage>
}

export function EventDetailsPage() {
  const { id } = useParams()
  const origin = inventoryOrigin(useLocation().state, 'movimentos')
  const { record, state, retry } = useRecordDetails(id, readEvent)
  return <DetailsPage title="Visualizar movimento de estoque" listPath={origin.path} listLabel={origin.label} state={state} onRetry={retry}>{record && <>
    <dl className={definitionClass}><DetailField label="Sequência">{record.sequencia}</DetailField><DetailField label="Operação">{operationLabel(record.tipo)}</DetailField><DetailField label="Registro">{showTime(record.createdAtUtc)}</DetailField><DetailField label="Algoritmo físico">{record.algoritmo}</DetailField><DetailField label="Data operacional">{record.dataOperacional}</DetailField><DetailField label="Data observada">{record.dataObservada}</DetailField><DetailField label="Autor"><ReferenceText path={`/estoque/responsaveis/${record.autorId}`} parse={parseResponsible} caption={x => `${x.nome}${x.ativo ? '' : ' · Inativo'}`} /></DetailField><DetailField label="Responsável"><ReferenceText path={`/estoque/responsaveis/${record.responsavelId}`} parse={parseResponsible} caption={x => `${x.nome}${x.ativo ? '' : ' · Inativo'}`} /></DetailField><DetailField label="Motivo">{record.motivo}</DetailField><DetailField label="Documento">{record.documento}</DetailField><DetailField label="Evento referenciado">{record.eventoReferenciaId ? <Link className="text-emerald-700 underline" to={`/estoque/movimentos/${record.eventoReferenciaId}`}>Consultar evento original</Link> : null}</DetailField></dl>
    <Panel title="Linhas imutáveis"><ScrollTable label="Linhas do movimento"><thead><tr>{['Sentido', 'Lote / local', 'Declarado', 'Canônico', 'Resíduo', 'Saldo anterior → posterior'].map(h => <th scope="col" key={h}>{h}</th>)}</tr></thead><tbody>{record.movimentos.map(line => <tr key={line.id}><td>{line.sentido === 'Saida' ? 'Saída' : 'Entrada'}</td><td><Link className="text-emerald-700 underline" to={`/estoque/lotes/${line.loteId}`}>{frozenName(line.snapshotJson, 'lote', line.loteId) ?? <ReferenceText path={`/estoque/lotes/${line.loteId}`} parse={parseLote} caption={x => `${x.codigo} — ${x.itemNome}`} />}</Link> / <Link className="text-emerald-700 underline" to={`/estoque/locais/${line.localId}`}>{frozenName(line.snapshotJson, 'local', line.localId) ?? <ReferenceText path={`/estoque/locais/${line.localId}`} parse={parseLocal} caption={x => `${x.codigo} — ${x.nome}`} />}</Link></td><td>{showQuantity(line.quantidadeDeclarada)} {line.unidadeDeclarada}</td><td>{showQuantity(line.quantidade)} {line.unidade}</td><td>{showQuantity(line.residuo)} {line.unidade}</td><td>{showQuantity(line.saldoAnterior)} → {showQuantity(line.saldoPosterior)} {line.unidade}</td></tr>)}</tbody></ScrollTable>{record.movimentos.map(line => <Snapshot key={line.id} value={line.snapshotJson} label={`Referências e evidências da linha ${line.ordinal}`} />)}</Panel><Panel title="Snapshot do fato"><p>O evento é imutável. Correções exigem ajuste presente, motivo e conferência física.</p><Snapshot value={record.snapshotJson} label="Dados e fontes congelados" /></Panel>
  </>}</DetailsPage>
}
