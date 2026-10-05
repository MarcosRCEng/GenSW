import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useRememberListState, useRestoredListState } from '../../shared/details/listNavigation'
import { useAuth } from '../auth/hooks/useAuth'
import { EntryEditor } from './EntryEditor'
import {
  brl,
  cents,
  dateLabel,
  decimal,
  errorMessage,
  finance,
  fromCents,
  monthPath,
  today,
  type Audit,
  type Category,
  type Closing,
  type Config,
  type EntryPage,
  type EntryView,
  type Summary,
} from './financial'
import './financial.css'

type Panel =
  | { kind: 'entry' | 'adjust' | 'cancel' | 'history'; entry?: EntryView }
  | { kind: 'close' }
  | null
export function FinancialPage() {
  const initial = useRestoredListState('/financeiro', {
    month: today().slice(0, 7), page: 1,
    filters: { search: '', tipo: '', categoriaId: '', cancelado: '', de: '', ate: '' },
  })
  const { user } = useAuth(),
    admin = user?.roles.includes('Admin') === true
  const [config, setConfig] = useState<Config | null>(),
    [categories, setCategories] = useState<Category[]>([]),
    [month, setMonth] = useState(initial.month),
    [summary, setSummary] = useState<Summary | null>(null),
    [list, setList] = useState<EntryPage | null>(null),
    [closings, setClosings] = useState<Closing[]>([])
  const [filters, setFilters] = useState(initial.filters),
    [page, setPage] = useState(initial.page),
    [panel, setPanel] = useState<Panel>(null),
    [error, setError] = useState(''),
    [notice, setNotice] = useState(''),
    [loading, setLoading] = useState(true),
    [busy, setBusy] = useState(false),
    [revision, setRevision] = useState(0)
  const [start, setStart] = useState(`${today().slice(0, 7)}-01`),
    [balance, setBalance] = useState('0'),
    [categoryName, setCategoryName] = useState(''),
    [natureza, setNatureza] = useState(2),
    [reason, setReason] = useState(''),
    [checkedBalance, setCheckedBalance] = useState(''),
    [audits, setAudits] = useState<Audit[]>([])
  const sending = useRef(false),
    panelRef = useRef<HTMLElement>(null)
  const detailsState = useRememberListState('/financeiro', { month, page, filters })
  const reload = useCallback(() => setRevision((x) => x + 1), [])
  useEffect(() => {
    let active = true
    setLoading(true)
    setError('')
    void Promise.all([
      finance<Config | null>('configuracao'),
      finance<Category[]>('categorias'),
      finance<Closing[]>('fechamentos'),
    ])
      .then(([c, cats, snapshots]) => {
        if (active) {
          setConfig(c)
          setCategories(cats)
          setClosings(snapshots)
          if (c) {
            setStart(c.dataInicio)
            setBalance(c.saldoInicial)
            if (month < c.dataInicio.slice(0, 7))
              setMonth(c.dataInicio.slice(0, 7))
          }
        }
      })
      .catch((e) => {
        if (active) setError(errorMessage(e))
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => {
      active = false
    }
  }, [revision, month])
  useEffect(() => {
    if (!config) return
    let active = true
    setList(null)
    setError('')
    const [year, mon] = month.split('-'),
      q = new URLSearchParams({
        ano: year,
        mes: String(Number(mon)),
        page: String(page),
      })
    Object.entries(filters).forEach(([k, v]) => {
      if (v) q.set(k, v)
    })
    void Promise.all([
      finance<Summary>(monthPath(month)),
      finance<EntryPage>(`lancamentos?${q}`),
    ])
      .then(([s, rows]) => {
        if (active) {
          setSummary(s)
          setList(rows)
        }
      })
      .catch((e) => {
        if (active) setError(errorMessage(e))
      })
    return () => {
      active = false
    }
  }, [config, month, page, filters, revision])
  useEffect(() => {
    if (!panel) return
    panelRef.current?.focus()
    setReason('')
    setCheckedBalance('')
    setAudits([])
    let active = true
    if (panel.kind === 'history')
      void finance<Audit[]>(
        `lancamentos/${panel.entry!.lancamento.id}/historico`,
      )
        .then((x) => {
          if (active) setAudits(x)
        })
        .catch((e) => {
          if (active) setError(errorMessage(e))
        })
    return () => {
      active = false
    }
  }, [panel])
  const run = async (action: () => Promise<unknown>) => {
    if (sending.current) return
    sending.current = true
    setBusy(true)
    setError('')
    try {
      await action()
      setPanel(null)
      setNotice('Operação registrada com sucesso.')
      reload()
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      sending.current = false
      setBusy(false)
    }
  }
  const updateFilter = (key: keyof typeof filters, value: string) => {
    setPage(1)
    setFilters((x) => ({ ...x, [key]: value }))
  }
  const configure = (e: FormEvent) => {
    e.preventDefault()
    void run(async () => {
      const c = await finance<Config>('configuracao', 'POST', {
        dataInicio: start,
        saldoInicial: decimal(balance),
        versaoEsperada: config?.versao,
      })
      setConfig(c)
      setMonth(c.dataInicio.slice(0, 7))
    })
  }
  const createCategory = (e: FormEvent) => {
    e.preventDefault()
    void run(async () => {
      await finance('categorias', 'POST', { nome: categoryName, natureza })
      setCategoryName('')
    })
  }
  const closedEntry = (entry: EntryView) =>
    closings.some(
      (c) => c.mes.slice(0, 7) === entry.lancamento.dataMovimento.slice(0, 7),
    )
  return (
    <main className="finance">
      <header className="finance-header">
        <div>
          <Link to="/">← Voltar ao início</Link>
          <p className="finance-eyebrow">Financeiro</p>
          <h1>Fluxo de caixa</h1>
          <p>Recebimentos e pagamentos realizados · Caixa único em BRL</p>
        </div>
        <button className="secondary" onClick={reload} disabled={busy}>
          Recarregar caixa
        </button>
      </header>
      {error && (
        <p role="alert" className="finance-error">
          {error}{' '}
          <button onClick={reload} className="secondary">
            Recarregar
          </button>
        </p>
      )}
      {notice && (
        <p role="status" className="finance-notice">
          {notice}
        </p>
      )}
      {loading && <p role="status">Carregando caixa…</p>}
      {config === null && (
        <section className="finance-panel">
          <h2>Comece pelo saldo inicial</h2>
          <p>
            Informe o mês de início do controle e o saldo existente. Esse saldo
            não gera uma receita.
          </p>
          {admin ? (
            <form onSubmit={configure}>
              <div className="finance-grid">
                <label>
                  Mês inicial
                  <input
                    required
                    type="month"
                    max={today().slice(0, 7)}
                    value={start.slice(0, 7)}
                    onChange={(e) => setStart(`${e.target.value}-01`)}
                  />
                </label>
                <label>
                  Saldo inicial (R$; negativo permitido)
                  <input
                    required
                    inputMode="decimal"
                    value={balance}
                    onChange={(e) => setBalance(e.target.value)}
                  />
                </label>
              </div>
              <button disabled={busy}>Configurar caixa</button>
            </form>
          ) : (
            <p>Um Admin precisa configurar o início do controle.</p>
          )}
        </section>
      )}
      {config && (
        <>
          <section className="finance-toolbar">
            <label>
              Mês do caixa
              <input
                type="month"
                required
                min={config.dataInicio.slice(0, 7)}
                value={month}
                onChange={(e) => {
                  if (e.target.value) {
                    setMonth(e.target.value)
                    setPage(1)
                    setPanel(null)
                  }
                }}
              />
            </label>
            <span
              className={`finance-badge ${summary?.fechado ? 'closed' : ''}`}
            >
              {summary
                ? summary.fechado
                  ? 'Fechado · imutável'
                  : 'Aberto'
                : 'Carregando…'}
            </span>
            <div className="finance-actions">
              <button
                disabled={!summary || summary.fechado || busy}
                onClick={() => setPanel({ kind: 'entry' })}
              >
                Novo lançamento
              </button>
              {admin && (
                <button
                  className="secondary"
                  disabled={
                    !summary ||
                    summary.fechado ||
                    month >= today().slice(0, 7) ||
                    busy
                  }
                  onClick={() => setPanel({ kind: 'close' })}
                >
                  Fechar mês
                </button>
              )}
            </div>
          </section>
          {summary && (
            <>
              <p className="finance-muted">
                Indicadores de todos os lançamentos efetivos do mês; os filtros
                e páginas abaixo não alteram esses totais.
              </p>
              <section aria-label="Resumo mensal" className="finance-stats">
                {[
                  ['Saldo de abertura', summary.saldoAbertura],
                  ['Receitas', summary.receitas],
                  ['Despesas', summary.despesas],
                  ['Resultado do caixa', summary.resultado],
                  ['Saldo final', summary.saldoFinal],
                ].map(([label, value]) => (
                  <article key={label}>
                    <h2>{label}</h2>
                    <p className={cents(value) < 0n ? 'negative' : ''}>
                      {brl(value)}
                    </p>
                  </article>
                ))}
              </section>
            </>
          )}
          {panel && (
            <section
              ref={panelRef}
              tabIndex={-1}
              className="finance-panel"
              aria-label="Operação selecionada"
            >
              {panel.kind === 'entry' || panel.kind === 'adjust' ? (
                <EntryEditor
                  key={`${panel.kind}/${panel.entry?.lancamento.id ?? 'new'}`}
                  categories={categories}
                  initial={panel.entry}
                  adjustment={panel.kind === 'adjust'}
                  onCancel={() => setPanel(null)}
                  onDone={() => {
                    setPanel(null)
                    setNotice('Lançamento registrado.')
                    reload()
                  }}
                />
              ) : panel.kind === 'close' ? (
                <form
                  onSubmit={(e) => {
                    e.preventDefault()
                    void run(() =>
                      finance(`${monthPath(month)}/fechamento`, 'POST', {
                        versaoEsperada: summary!.versao,
                        observacao: reason,
                        saldoConferido: checkedBalance
                          ? decimal(checkedBalance)
                          : null,
                      }),
                    )
                  }}
                >
                  <h2>Confirmar fechamento de {month}</h2>
                  <p>
                    Essa ação é definitiva, sem reabertura. Feche todos os meses
                    em sequência, inclusive meses vazios.
                  </p>
                  <p>
                    Receitas {brl(summary!.receitas)} · Despesas{' '}
                    {brl(summary!.despesas)} · Saldo final{' '}
                    <strong>{brl(summary!.saldoFinal)}</strong>
                  </p>
                  <label>
                    Observação obrigatória
                    <textarea
                      required
                      maxLength={2000}
                      value={reason}
                      onChange={(e) => setReason(e.target.value)}
                    />
                  </label>
                  <label>
                    Saldo conferido (R$, opcional)
                    <input
                      inputMode="decimal"
                      value={checkedBalance}
                      onChange={(e) => setCheckedBalance(e.target.value)}
                    />
                  </label>
                  {checkedBalance && (
                    <p>
                      Diferença:{' '}
                      {(() => {
                        try {
                          return brl(
                            fromCents(
                              cents(decimal(checkedBalance)) -
                                cents(summary!.saldoFinal),
                            ),
                          )
                        } catch {
                          return 'Informe um valor válido.'
                        }
                      })()}
                      . Nenhum ajuste automático será criado.
                    </p>
                  )}
                  <label className="finance-checkbox">
                    <input type="checkbox" required />
                    Conferi os totais e entendo que o mês ficará imutável.
                  </label>
                  <button disabled={busy}>
                    {busy ? 'Fechando…' : 'Confirmar fechamento definitivo'}
                  </button>
                  <button
                    type="button"
                    className="secondary"
                    disabled={busy}
                    onClick={() => setPanel(null)}
                  >
                    Voltar
                  </button>
                </form>
              ) : panel.kind === 'cancel' ? (
                <form
                  onSubmit={(e) => {
                    e.preventDefault()
                    void run(() =>
                      finance(
                        `lancamentos/${panel.entry!.lancamento.id}/cancelamento`,
                        'POST',
                        {
                          versaoEsperada: panel.entry!.lancamento.versao,
                          motivo: reason,
                        },
                      ),
                    )
                  }}
                >
                  <h2>Cancelar lançamento</h2>
                  <p>
                    {panel.entry!.lancamento.descricao} ·{' '}
                    {brl(panel.entry!.lancamento.valor)}. O registro permanecerá
                    no histórico e será removido dos totais.
                  </p>
                  <label>
                    Motivo obrigatório
                    <textarea
                      required
                      maxLength={2000}
                      value={reason}
                      onChange={(e) => setReason(e.target.value)}
                    />
                  </label>
                  <button disabled={busy}>Confirmar cancelamento</button>
                  <button
                    type="button"
                    className="secondary"
                    onClick={() => setPanel(null)}
                  >
                    Voltar
                  </button>
                </form>
              ) : (
                <>
                  <h2>Histórico do lançamento</h2>
                  <p>
                    {panel.entry!.lancamento.descricao} · Versão{' '}
                    {panel.entry!.lancamento.versao}
                  </p>
                  {panel.entry!.lancamento.lancamentoOriginalId && (
                    <p>
                      Ajuste referente ao original{' '}
                      <button
                        className="secondary"
                        onClick={() =>
                          void finance<EntryView>(
                            `lancamentos/${panel.entry!.lancamento.lancamentoOriginalId}`,
                          )
                            .then((entry) =>
                              setPanel({ kind: 'history', entry }),
                            )
                            .catch((e) => setError(errorMessage(e)))
                        }
                      >
                        Consultar original
                      </button>
                    </p>
                  )}
                  {audits.length === 0 ? (
                    <p>Carregando histórico…</p>
                  ) : (
                    audits.map((a) => <AuditRow key={a.id} audit={a} />)
                  )}
                  <button className="secondary" onClick={() => setPanel(null)}>
                    Voltar
                  </button>
                </>
              )}
            </section>
          )}
          <section className="finance-panel">
            <h2>Lançamentos do mês</h2>
            <fieldset className="finance-grid">
              <legend>Filtros da lista</legend>
              <label>
                Pesquisar descrição
                <input
                  type="search"
                  value={filters.search}
                  onChange={(e) => updateFilter('search', e.target.value)}
                />
              </label>
              <label>
                Tipo
                <select
                  value={filters.tipo}
                  onChange={(e) => updateFilter('tipo', e.target.value)}
                >
                  <option value="">Todos</option>
                  <option value="1">Receita</option>
                  <option value="2">Despesa</option>
                </select>
              </label>
              <label>
                Categoria
                <select
                  value={filters.categoriaId}
                  onChange={(e) => updateFilter('categoriaId', e.target.value)}
                >
                  <option value="">Todas</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.nome}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Situação
                <select
                  value={filters.cancelado}
                  onChange={(e) => updateFilter('cancelado', e.target.value)}
                >
                  <option value="">Todas</option>
                  <option value="false">Efetivos</option>
                  <option value="true">Cancelados</option>
                </select>
              </label>
              <label>
                De
                <input
                  type="date"
                  value={filters.de}
                  onChange={(e) => updateFilter('de', e.target.value)}
                />
              </label>
              <label>
                Até
                <input
                  type="date"
                  value={filters.ate}
                  onChange={(e) => updateFilter('ate', e.target.value)}
                />
              </label>
            </fieldset>
            {!list ? (
              <p role="status">Carregando lançamentos…</p>
            ) : list.items.length === 0 ? (
              <p>
                Nenhum lançamento encontrado neste mês com os filtros
                escolhidos.
              </p>
            ) : (
              <div
                className="finance-table"
                tabIndex={0}
                role="region"
                aria-label="Tabela de lançamentos, rolagem horizontal"
              >
                <table>
                  <caption>{list.totalItems} lançamentos encontrados</caption>
                  <thead>
                    <tr>
                      <th>Data / descrição</th>
                      <th>Categoria / referências</th>
                      <th>Tipo / valor</th>
                      <th>Situação</th>
                      <th>Ações</th>
                    </tr>
                  </thead>
                  <tbody>
                    {list.items.map((entry) => {
                      const x = entry.lancamento,
                        closed = closedEntry(entry)
                      return (
                        <tr key={x.id}>
                          <td>
                            {dateLabel(x.dataMovimento)}
                            <strong>{x.descricao}</strong>
                            <small>
                              {
                                [
                                  '',
                                  'Dinheiro',
                                  'Pix',
                                  'Transferência',
                                  'Cartão',
                                  'Outro',
                                ][x.formaPagamento]
                              }
                            </small>
                          </td>
                          <td>
                            {entry.categoriaNome}
                            {entry.pessoaNome && (
                              <small>{entry.pessoaNome}</small>
                            )}
                            {entry.animalNome && (
                              <small>{entry.animalNome}</small>
                            )}
                          </td>
                          <td>
                            {x.tipo === 1 ? 'Receita' : 'Despesa'}
                            <strong>{brl(x.valor)}</strong>
                            {x.origem !== 1 && (
                              <small>
                                Ajuste{' '}
                                {x.origem === 2 ? 'de reversão' : 'substituto'}
                              </small>
                            )}
                          </td>
                          <td>
                            {x.cancelado
                              ? 'Cancelado'
                              : entry.revertido
                                ? 'Efetivo · revertido por ajuste'
                                : 'Efetivo'}
                          </td>
                          <td>
                            <div className="finance-actions">
                              <Link className="secondary" to={`/financeiro/lancamentos/${x.id}`} state={detailsState}>Visualizar</Link>
                              <button
                                className="secondary"
                                onClick={() =>
                                  setPanel({ kind: 'history', entry })
                                }
                              >
                                Histórico
                              </button>
                              {!x.cancelado && !closed && x.origem === 1 && (
                                <>
                                  <button
                                    className="secondary"
                                    onClick={() =>
                                      setPanel({ kind: 'entry', entry })
                                    }
                                  >
                                    Corrigir
                                  </button>
                                  <button
                                    className="secondary"
                                    onClick={() =>
                                      setPanel({ kind: 'cancel', entry })
                                    }
                                  >
                                    Cancelar
                                  </button>
                                </>
                              )}
                              {!x.cancelado && (closed || x.origem !== 1) && (
                                <button
                                  className="secondary"
                                  onClick={() =>
                                    setPanel({ kind: 'adjust', entry })
                                  }
                                >
                                  Ajustar
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
            {list && (
              <nav
                aria-label="Páginas de lançamentos"
                className="finance-actions"
              >
                <button
                  className="secondary"
                  disabled={page <= 1}
                  onClick={() => setPage((x) => x - 1)}
                >
                  Anterior
                </button>
                <span>
                  Página {page} de {Math.max(1, list.totalPages)}
                </span>
                <button
                  className="secondary"
                  disabled={page >= list.totalPages}
                  onClick={() => setPage((x) => x + 1)}
                >
                  Próxima
                </button>
              </nav>
            )}
          </section>
          <details className="finance-panel">
            <summary>Histórico dos fechamentos ({closings.length})</summary>
            {closings.length === 0 ? (
              <p>Nenhum mês fechado ainda.</p>
            ) : (
              closings.map((c) => (
                <article className="finance-audit" key={c.id}>
                  <h3>{c.mes.slice(0, 7)}</h3>
                  <p>
                    Abertura {brl(c.saldoAbertura)} · Receitas {brl(c.receitas)}{' '}
                    · Despesas {brl(c.despesas)} · Saldo final{' '}
                    {brl(c.saldoFinal)}
                  </p>
                  {c.saldoConferido !== null && (
                    <p>
                      Conferido {brl(c.saldoConferido)} · Diferença{' '}
                      {brl(
                        fromCents(
                          cents(c.saldoConferido) - cents(c.saldoFinal),
                        ),
                      )}
                    </p>
                  )}
                  <p>
                    {c.observacao} ·{' '}
                    {new Date(c.createdAtUtc).toLocaleString('pt-BR', {
                      timeZone: 'America/Sao_Paulo',
                    })}
                  </p>
                  <p className="finance-muted">
                    Autor: {c.autorId} · UTC: {c.createdAtUtc}
                  </p>
                </article>
              ))
            )}
          </details>
          {admin && (
            <details className="finance-panel">
              <summary>Configuração do início do controle</summary>
              <p>
                Início {dateLabel(config.dataInicio)} · Saldo{' '}
                {brl(config.saldoInicial)}. A API bloqueia alterações após o
                primeiro lançamento ou fechamento.
              </p>
              <form onSubmit={configure}>
                <div className="finance-grid">
                  <label>
                    Mês inicial
                    <input
                      type="month"
                      required
                      value={start.slice(0, 7)}
                      onChange={(e) => setStart(`${e.target.value}-01`)}
                    />
                  </label>
                  <label>
                    Saldo inicial (R$)
                    <input
                      inputMode="decimal"
                      required
                      value={balance}
                      onChange={(e) => setBalance(e.target.value)}
                    />
                  </label>
                </div>
                <button disabled={busy}>Salvar configuração</button>
              </form>
            </details>
          )}
        </>
      )}
      <details className="finance-panel">
        <summary>Categorias financeiras</summary>
        <p>
          O nome pode mudar; natureza e código de venda são preservados.
          Inativar não altera o histórico.
        </p>
        {categories.map((c) => (
          <CategoryRow
            key={c.id}
            category={c}
            disabled={busy}
            detailsState={detailsState}
            onSave={(nome, ativa) =>
              void run(() =>
                finance(`categorias/${c.id}`, 'PUT', {
                  nome,
                  ativa,
                  versaoEsperada: c.versao,
                }),
              )
            }
          />
        ))}
        <form onSubmit={createCategory}>
          <h3>Nova categoria</h3>
          <div className="finance-grid">
            <label>
              Nome
              <input
                required
                maxLength={100}
                value={categoryName}
                onChange={(e) => setCategoryName(e.target.value)}
              />
            </label>
            <label>
              Natureza
              <select
                value={natureza}
                onChange={(e) => setNatureza(Number(e.target.value))}
              >
                <option value="1">Receita</option>
                <option value="2">Despesa</option>
              </select>
            </label>
          </div>
          <button disabled={busy}>Criar categoria</button>
        </form>
      </details>
    </main>
  )
}
function CategoryRow({
  category,
  disabled,
  detailsState,
  onSave,
}: {
  category: Category
  disabled: boolean
  detailsState: ReturnType<typeof useRememberListState>
  onSave: (nome: string, ativa: boolean) => void
}) {
  const [name, setName] = useState(category.nome)
  useEffect(() => setName(category.nome), [category.nome])
  return (
    <form
      className="finance-category"
      onSubmit={(e) => {
        e.preventDefault()
        onSave(name, category.ativa)
      }}
    >
      <label>
        {category.natureza === 1 ? 'Receita' : 'Despesa'} ·{' '}
        {category.ativa ? 'Ativa' : 'Inativa'}
        <input
          required
          maxLength={100}
          aria-label={`Nome de ${category.nome}`}
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </label>
      <Link className="secondary" to={`/financeiro/categorias/${category.id}`} state={detailsState}>Visualizar</Link>
      <button disabled={disabled} className="secondary">
        Renomear
      </button>
      <button
        disabled={disabled}
        className="secondary"
        type="button"
        onClick={() => onSave(name, !category.ativa)}
      >
        {category.ativa ? 'Inativar' : 'Ativar'}
      </button>
    </form>
  )
}
function AuditRow({ audit: a }: { audit: Audit }) {
  return (
    <article className="finance-audit">
      <h3>
        {a.operacao} ·{' '}
        {new Date(a.createdAtUtc).toLocaleString('pt-BR', {
          timeZone: 'America/Sao_Paulo',
        })}
      </h3>
      <p>{a.motivo}</p>
      <p className="finance-muted">
        Autor: {a.autorId} · Auditoria UTC: {a.createdAtUtc}
      </p>
      {[a.antesJson, a.depoisJson].map((json, i) => {
        if (!json) return null
        const data = JSON.parse(json) as {
          Descricao: string
          Valor: string
          Tipo: number
          DataMovimento: string
          Cancelado: boolean
          PessoaId: string | null
          AnimalId: string | null
        }
        return (
          <p key={i}>
            {i === 0 ? 'Antes' : 'Depois'}: {data.Descricao} ·{' '}
            {data.Tipo === 1 ? 'Receita' : 'Despesa'} {brl(data.Valor)} ·{' '}
            {dateLabel(data.DataMovimento)} ·{' '}
            {data.Cancelado ? 'Cancelado' : 'Efetivo'}
            {data.PessoaId && ' · Pessoa vinculada'}
            {data.AnimalId && ' · Animal vinculado'}
          </p>
        )
      })}
    </article>
  )
}
