import { decimalInput, parseCommittedEvent, parseEvent, parseHistory, parsePage, parsePreview, parseSaldo, showQuantity, today } from './inventoryService'
import { event, page, preview, stock } from './inventoryFixtures'

describe('Contratos físicos de estoque', () => {
  it('conserva decimal e bigint como strings exatas, incluindo sequência acima de 2^53', () => {
    expect(parseSaldo(stock)).toMatchObject({ quantidade: '10.000000', sequenciaAte: '9007199254740993', avisos: ['Validade não informada'], elegivel: true })
    expect(parsePage(page([event]), parseEvent).items[0].sequencia).toBe('9007199254740993')
    expect(showQuantity('99999999999999.123456')).toBe('99999999999999,123456')
  })
  it.each([{ quantidade: 10 }, { sequenciaAte: 9007199254740992 }, { sequenciaAte: '9223372036854775808' }, { quantidade: '1e3' }, { quantidade: '-1' }, { avisos: [1] }])('rejeita transporte físico inválido %j', change => {
    expect(() => parseSaldo({ ...stock, ...change })).toThrow()
  })
  it('rejeita contagem fracionária e aceita aviso opcional sem torná-lo impedimento', () => {
    expect(() => parseSaldo({ ...stock, unidade: 'un', quantidade: '1.5' })).toThrow()
    expect(parseSaldo({ ...stock, avisos: null }).avisos).toEqual([])
  })
  it('rejeita frações canônicas de un também em movimentos e prévias', () => {
    expect(() => parseEvent({ ...event, movimentos: [{ ...event.movimentos[0], unidade: 'un', quantidade: '1.5' }] })).toThrow()
    expect(() => parsePreview({ ...preview, unidade: 'un', quantidade: { ...preview.quantidade, unidade: 'un', normalizada: '1.5' } })).toThrow()
    expect(() => parsePreview({ ...preview, unidade: 'un', quantidade: { ...preview.quantidade, unidade: 'un' }, posicoes: [{ ...preview.posicoes[0], saldoAnterior: '1.5' }] })).toThrow()
  })
  it('valida todas as posições e o conteúdo numérico da prévia antes de mostrar confirmação', () => {
    expect(parsePreview(preview).quantidade.sentido).toBeNull()
    expect(() => parsePreview({ ...preview, posicoes: [{ ...preview.posicoes[0], saldoPosterior: 11 }] })).toThrow()
    expect(() => parsePreview({ ...preview, quantidade: { ...preview.quantidade, normalizada: '1.0000001' } })).toThrow()
  })
  it('distingue envelope durável da escrita e evento direto da leitura', () => {
    expect(parseCommittedEvent({ evento: event, previa: preview }).id).toBe(event.id)
    expect(() => parseCommittedEvent(event)).toThrow()
    expect(() => parseEvent({ evento: event, previa: preview })).toThrow()
    expect(() => parseCommittedEvent({ evento: event, previa: {} })).toThrow()
  })
  it('rejeita snapshots inválidos, conservando os snapshots válidos sem reinterpretar valores', () => {
    expect(() => parseEvent({ ...event, snapshotJson: 'invalid' })).toThrow()
    expect(() => parseHistory({ id: 'h', registroId: 'l', tipo: 'Lote', operacao: 'Edicao', sequencia: '1', autorId: 'u', createdAtUtc: event.createdAtUtc, motivo: '', antesJson: '{}', depoisJson: 'invalid' })).toThrow()
  })
  it('aceita decimal local sem arredondar e zero somente em contagem alvo', () => {
    expect(decimalInput(' 1,123456 ')).toBe('1.123456')
    expect(decimalInput('0', true)).toBe('0')
    for (const value of ['0', '-1', '1e2', '1.2345678', '100000000000000', '1.000,1']) expect(() => decimalInput(value)).toThrow()
    expect(today()).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })
})
