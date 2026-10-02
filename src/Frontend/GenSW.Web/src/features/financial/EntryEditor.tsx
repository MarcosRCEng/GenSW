import { useEffect, useRef, useState, type FormEvent } from 'react'
import { ReferencePicker } from './ReferencePicker'
import {
  brl,
  cents,
  decimal,
  errorMessage,
  finance,
  fromCents,
  today,
  type Category,
  type EntryInput,
  type EntryView,
} from './financial'

export function EntryEditor({
  categories,
  initial,
  adjustment = false,
  onDone,
  onCancel,
}: {
  categories: Category[]
  initial?: EntryView
  adjustment?: boolean
  onDone: () => void
  onCancel: () => void
}) {
  const original = initial?.lancamento
  const [form, setForm] = useState<EntryInput>(
    original
      ? {
          tipo: original.tipo,
          dataMovimento: adjustment ? today() : original.dataMovimento,
          valor: original.valor,
          descricao: original.descricao,
          categoriaId: original.categoriaId,
          formaPagamento: original.formaPagamento,
          pessoaId: original.pessoaId,
          animalId: original.animalId,
          observacao: original.observacao,
        }
      : {
          tipo: 2,
          dataMovimento: today(),
          valor: '',
          descricao: '',
          categoriaId:
            categories.find((x) => x.natureza === 2 && x.ativa)?.id ?? '',
          formaPagamento: 2,
          pessoaId: null,
          animalId: null,
          observacao: null,
        },
  )
  const [reason, setReason] = useState(''),
    [adjustKind, setAdjustKind] = useState('substituicao'),
    [preview, setPreview] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState('')
  const sending = useRef(false),
    attempt = useRef<{ payload: string; key: string } | null>(null)
  const heading = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    heading.current?.focus()
  }, [preview])
  const monetary = !adjustment || adjustKind === 'substituicao'
  const set = <K extends keyof EntryInput>(key: K, value: EntryInput[K]) => {
    setPreview(false)
    setForm((x) => ({ ...x, [key]: value }))
  }
  const prepare = (e: FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      if (monetary) setForm((x) => ({ ...x, valor: decimal(x.valor, true) }))
      if (original && !reason.trim()) throw new Error('Informe o motivo.')
      setPreview(true)
    } catch (e) {
      setError(errorMessage(e))
    }
  }
  const save = async () => {
    if (sending.current) return
    sending.current = true
    setBusy(true)
    setError('')
    const body = adjustment
      ? {
          versaoEsperada: original!.versao,
          motivo: reason,
          somenteAnotacao: adjustKind === 'anotacao',
          substituto: adjustKind === 'substituicao' ? form : null,
        }
      : original
        ? { lancamento: form, versaoEsperada: original.versao, motivo: reason }
        : form
    const payload = JSON.stringify(body)
    if (attempt.current?.payload !== payload)
      attempt.current = { payload, key: crypto.randomUUID() }
    try {
      await finance(
        adjustment
          ? `lancamentos/${original!.id}/ajuste`
          : original
            ? `lancamentos/${original.id}`
            : 'lancamentos',
        original && !adjustment ? 'PUT' : 'POST',
        body,
        attempt.current!.key,
      )
      onDone()
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      sending.current = false
      setBusy(false)
    }
  }
  const impact = () => {
    if (!adjustment) return brl(form.valor)
    if (adjustKind === 'anotacao') return brl('0')
    const reversal = cents(original!.valor) * (original!.tipo === 1 ? -1n : 1n)
    const replacement =
      adjustKind === 'substituicao'
        ? cents(form.valor) * (form.tipo === 1 ? 1n : -1n)
        : 0n
    return brl(fromCents(reversal + replacement))
  }
  return (
    <section className="finance-panel" aria-label="Formulário de lançamento">
      <h2 tabIndex={-1} ref={preview ? undefined : heading}>
        {adjustment
          ? 'Ajustar lançamento'
          : original
            ? 'Corrigir lançamento'
            : 'Novo lançamento'}
      </h2>
      {error && (
        <p role="alert" className="finance-error">
          {error} Confira os dados. Para conflito de versão, volte e recarregue
          o caixa.
        </p>
      )}
      {preview ? (
        <div>
          <h3 ref={preview ? heading : undefined} tabIndex={-1}>
            Confira antes de confirmar
          </h3>
          {adjustment ? (
            <>
              <p>
                Original: {original!.descricao} · {brl(original!.valor)}
              </p>
              {adjustKind !== 'anotacao' && (
                <p>
                  Reversão no mês atual:{' '}
                  {original!.tipo === 1 ? 'Despesa' : 'Receita'} de{' '}
                  {brl(original!.valor)}, mantendo referência e categoria
                  originais.
                </p>
              )}
              {adjustKind === 'substituicao' && (
                <p>
                  Substituto: {form.tipo === 1 ? 'Receita' : 'Despesa'} de{' '}
                  {brl(form.valor)} · {form.descricao}
                </p>
              )}
              <p>
                Impacto líquido no caixa atual: <strong>{impact()}</strong>
              </p>
              <p>
                O lançamento original e os fechamentos anteriores serão
                preservados.
              </p>
            </>
          ) : (
            <p>
              {form.tipo === 1 ? 'Receita recebida' : 'Despesa paga'} de{' '}
              <strong>{impact()}</strong> · {form.descricao} ·{' '}
              {form.dataMovimento}
            </p>
          )}
          {reason && <p>Motivo: {reason}</p>}
          <div className="finance-actions">
            <button disabled={busy} onClick={() => void save()}>
              {busy ? 'Salvando…' : 'Confirmar gravação'}
            </button>
            <button
              disabled={busy}
              className="secondary"
              onClick={() => setPreview(false)}
            >
              Revisar campos
            </button>
          </div>
        </div>
      ) : (
        <form onSubmit={prepare}>
          <div className="finance-grid">
            {adjustment && (
              <label>
                Tipo de ajuste
                <select
                  value={adjustKind}
                  onChange={(e) => setAdjustKind(e.target.value)}
                >
                  <option value="substituicao">Reverter e substituir</option>
                  <option value="reversao">Somente reverter</option>
                  <option value="anotacao">Anotar sem movimentar saldo</option>
                </select>
              </label>
            )}
            {monetary && (
              <>
                <label>
                  Natureza
                  <select
                    value={form.tipo}
                    onChange={(e) => {
                      const tipo = Number(e.target.value) as 1 | 2
                      setForm((x) => ({
                        ...x,
                        tipo,
                        categoriaId:
                          categories.find((c) => c.natureza === tipo && c.ativa)
                            ?.id ?? '',
                        animalId: null,
                      }))
                    }}
                  >
                    <option value="1">Receita recebida</option>
                    <option value="2">Despesa paga</option>
                  </select>
                </label>
                <label>
                  Data efetiva
                  <input
                    required
                    type="date"
                    max={today()}
                    readOnly={adjustment}
                    value={form.dataMovimento}
                    onChange={(e) => set('dataMovimento', e.target.value)}
                  />
                </label>
                <label>
                  Valor (R$)
                  <input
                    required
                    inputMode="decimal"
                    placeholder="0,00"
                    value={form.valor}
                    onChange={(e) => set('valor', e.target.value)}
                  />
                </label>
                <label>
                  Categoria
                  <select
                    required
                    value={form.categoriaId}
                    onChange={(e) => {
                      set('categoriaId', e.target.value)
                      set('animalId', null)
                    }}
                  >
                    <option value="">Selecione</option>
                    {categories
                      .filter(
                        (c) =>
                          c.natureza === form.tipo &&
                          (c.ativa || c.id === original?.categoriaId),
                      )
                      .map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.nome}
                          {c.ativa ? '' : ' (inativa)'}
                        </option>
                      ))}
                  </select>
                </label>
                <label>
                  Descrição
                  <input
                    required
                    maxLength={200}
                    value={form.descricao}
                    onChange={(e) => set('descricao', e.target.value)}
                  />
                </label>
                <label>
                  Forma de pagamento
                  <select
                    value={form.formaPagamento}
                    onChange={(e) =>
                      set('formaPagamento', Number(e.target.value))
                    }
                  >
                    {[
                      'Dinheiro',
                      'Pix',
                      'Transferência',
                      'Cartão',
                      'Outro',
                    ].map((x, i) => (
                      <option key={x} value={i + 1}>
                        {x}
                      </option>
                    ))}
                  </select>
                </label>
                <ReferencePicker
                  kind="pessoas"
                  value={form.pessoaId}
                  onChange={(v) => set('pessoaId', v)}
                />
                {form.tipo === 1 &&
                  categories.find((c) => c.id === form.categoriaId)?.codigo ===
                    'VENDA_ANIMAIS' && (
                    <ReferencePicker
                      kind="animais"
                      value={form.animalId}
                      onChange={(v) => set('animalId', v)}
                    />
                  )}
                <label>
                  Observação
                  <textarea
                    maxLength={2000}
                    value={form.observacao ?? ''}
                    onChange={(e) => set('observacao', e.target.value || null)}
                  />
                </label>
              </>
            )}
            {original && (
              <label>
                {adjustKind === 'anotacao' && adjustment
                  ? 'Anotação e motivo (descreva a correção de descrição ou referências)'
                  : 'Motivo obrigatório'}
                <textarea
                  required
                  maxLength={2000}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </label>
            )}
          </div>
          <button>Revisar e confirmar</button>
        </form>
      )}
      <button
        type="button"
        disabled={busy}
        className="secondary"
        onClick={onCancel}
      >
        Voltar ao caixa
      </button>
    </section>
  )
}
