// Monetary values remain decimal strings. Never pass them through Number/float.
export function decimalString(value: string, scale: number, integerDigits: number, allowZero = true): string {
  const normalized = value.trim().replace(',', '.')
  if (!new RegExp(`^(?:0|[1-9]\\d{0,${integerDigits - 1}})(?:\\.\\d{1,${scale}})?$`).test(normalized)) throw new Error(`Informe um valor não negativo, com até ${integerDigits} inteiros e ${scale} casas decimais.`)
  if (!allowZero && /^0(?:\.0+)?$/.test(normalized)) throw new Error('Informe um valor maior que zero.')
  return normalized
}
export const moneyInput = (value: string) => decimalString(value, 2, 16)
export const unitPriceInput = (value: string) => decimalString(value, 6, 14)
export const quantityInput = (value: string) => decimalString(value, 6, 14, false)
export function percentageInput(value: string): string {
  const normalized = decimalString(value, 6, 3)
  if (scaled(normalized, 6) > 100000000n) throw new Error('Percentual deve estar entre 0 e 100.')
  return normalized
}
export function scaled(value: string, scale: number): bigint {
  const [whole, fraction = ''] = value.split('.')
  if (!/^\d+$/.test(whole) || !/^\d*$/.test(fraction) || fraction.length > scale) throw new Error('Escala decimal inválida.')
  return BigInt(whole) * 10n ** BigInt(scale) + BigInt(fraction.padEnd(scale, '0') || '0')
}
export function validatePercentages(values: string[]) {
  const normalized = values.map(percentageInput)
  if (normalized.reduce((total, value) => total + scaled(value, 6), 0n) !== 100000000n) throw new Error('Os percentuais devem somar exatamente 100%, sem arredondamento automático.')
  return normalized
}
export function displayMoney(value: string | null): string {
  if (value === null) return 'Desconhecido'
  const [whole, fraction = ''] = value.split('.')
  return `R$ ${whole.replace(/\B(?=(\d{3})+(?!\d))/g, '.')},${fraction.padEnd(2, '0')}`
}
export function displayDecimal(value: string | null): string {
  return value === null ? 'Não aplicável ou desconhecido' : value.replace('.', ',')
}

export const isZeroQuantity = (value: string) => /^0(?:\.0+)?$/.test(value)
