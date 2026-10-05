import { httpRequest } from '../../shared/http/httpClient'
import { HttpError } from '../../shared/http/httpErrors'

export type Natureza = 1 | 2
export interface Config {
  dataInicio: string
  saldoInicial: string
  versao: number
}
export interface Category {
  id: string
  nome: string
  natureza: Natureza
  codigo: string | null
  ativa: boolean
  versao: number
}
export interface EntryInput {
  tipo: Natureza
  dataMovimento: string
  valor: string
  descricao: string
  categoriaId: string
  formaPagamento: number
  pessoaId: string | null
  animalId: string | null
  observacao: string | null
}
export interface Entry extends EntryInput {
  id: string
  cancelado: boolean
  versao: number
  origem: number
  lancamentoOriginalId: string | null
  motivoAjuste: string | null
}
export interface EntryView {
  lancamento: Entry
  categoriaNome: string
  pessoaNome: string | null
  animalNome: string | null
  revertido: boolean
}
export interface EntryPage {
  items: EntryView[]
  page: number
  totalItems: number
  totalPages: number
}
export interface Summary {
  mes: string
  fechado: boolean
  versao: number
  saldoAbertura: string
  receitas: string
  despesas: string
  resultado: string
  saldoFinal: string
  quantidadeReceitas: number
  quantidadeDespesas: number
}
export interface Closing {
  id: string
  mes: string
  saldoAbertura: string
  receitas: string
  despesas: string
  saldoFinal: string
  saldoConferido: string | null
  observacao: string
  autorId: string
  createdAtUtc: string
}
export interface Audit {
  id: string
  operacao: string
  motivo: string | null
  antesJson: string | null
  depoisJson: string
  autorId: string
  createdAtUtc: string
}
export const finance = <T>(
  path: string,
  method: 'GET' | 'POST' | 'PUT' | 'PATCH' = 'GET',
  body?: unknown,
  key?: string,
) =>
  httpRequest<T>(`/financeiro/${path}`, {
    authenticated: true,
    method,
    body,
    headers: key ? { 'Idempotency-Key': key } : undefined,
  })
export const monthPath = (month: string) => {
  const [year, mon] = month.split('-')
  return `meses/${year}/${Number(mon)}`
}
export function today() {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'America/Sao_Paulo',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(new Date())
  const part = (kind: string) => parts.find((p) => p.type === kind)!.value
  return `${part('year')}-${part('month')}-${part('day')}`
}
export function decimal(value: string, positive = false): string {
  const normalized = value.trim().replace(',', '.')
  if (!/^-?\d{1,16}(\.\d{1,2})?$/.test(normalized))
    throw new Error(
      'Informe reais com até duas casas decimais, sem separador de milhares.',
    )
  const [whole, fraction = ''] = normalized.split('.')
  const result = `${whole}.${fraction.padEnd(2, '0')}`
  if (positive && cents(result) <= 0n)
    throw new Error('O valor deve ser maior que zero.')
  return result
}
export function cents(value: string): bigint {
  const sign = value.startsWith('-') ? -1n : 1n
  const [whole, fraction = ''] = value.replace('-', '').split('.')
  return sign * (BigInt(whole) * 100n + BigInt(fraction.padEnd(2, '0')))
}
export function fromCents(value: bigint): string {
  const abs = value < 0n ? -value : value
  return `${value < 0n ? '-' : ''}${abs / 100n}.${String(abs % 100n).padStart(2, '0')}`
}
export function brl(value: string): string {
  const money = cents(value)
  const abs = money < 0n ? -money : money
  return `${money < 0n ? '−' : ''}R$ ${new Intl.NumberFormat('pt-BR').format(abs / 100n)},${String(abs % 100n).padStart(2, '0')}`
}
export const dateLabel = (date: string) => date.split('-').reverse().join('/')
export function errorMessage(error: unknown): string {
  if (error instanceof HttpError)
    return (
      error.detail ??
      (error.status === 409
        ? 'Conflito. Recarregue e confira os dados antes de tentar novamente.'
        : error.status === 403
          ? 'Essa operação exige o papel Admin.'
          : 'Não foi possível concluir a operação.')
    )
  return error instanceof Error
    ? error.message
    : 'Não foi possível concluir a operação.'
}
