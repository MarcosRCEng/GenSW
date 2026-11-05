import { describe, expect, it } from 'vitest'
import { displayMoney, moneyInput, quantityInput, unitPriceInput, validatePercentages } from './decimals'

describe('strings monetárias F04', () => {
  it('conserva o máximo monetário exato e distingue desconhecido de zero documentado', () => {
    expect(moneyInput('9999999999999999,99')).toBe('9999999999999999.99')
    expect(displayMoney('9999999999999999.99')).toBe('R$ 9.999.999.999.999.999,99')
    expect(displayMoney(null)).toBe('Desconhecido')
    expect(displayMoney('0')).toBe('R$ 0,00')
    expect(() => moneyInput('')).toThrow()
    expect(() => moneyInput('0.001')).toThrow()
    expect(() => moneyInput('10000000000000000')).toThrow()
  })
  it('exige soma exata a seis casas, sem tolerância float ou normalização', () => {
    expect(validatePercentages(['33,333333', '33.333333', '33.333334'])).toEqual(['33.333333', '33.333333', '33.333334'])
    for (const values of [['99.999999'], ['100.000001'], ['-1', '101'], ['50.0000001', '50']]) expect(() => validatePercentages(values)).toThrow()
  })
  it('não confunde unitário arredondado com total e recusa quantidade zero', () => {
    expect(unitPriceInput('0,003333')).toBe('0.003333')
    expect(() => quantityInput('0')).toThrow()
    expect(() => unitPriceInput('1e3')).toThrow()
  })
})
