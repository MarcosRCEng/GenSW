import { parseEggAnchor, parseEggCandidate, parseEggCommandResult, parseEggDetail, parseEggPage, parseEggPreview, parseEggProof, integerCount } from './eggBridgeService'
import { eggAnchor, eggCandidate, eggDetail, eggPage, eggPreview, eggProof, eggScope } from './eggBridgeFixtures'

describe('Contratos F03 e contagem exata', () => {
  it('preserva revisões bigint e contagens históricas como strings', () => {
    expect(parseEggAnchor(eggAnchor).revisao).toBe('9007199254740993')
    expect(parseEggProof(eggProof).contagem).toBe('99999999999999')
    expect(parseEggPreview(eggPreview).sequenciaAte).toBe('9007199254740994')
    expect(parseEggDetail(eggDetail)).toEqual(eggDetail)
    expect(parseEggCommandResult('Recorte', { recorte: eggScope, decisoes: [] })).toEqual({ kind: 'recorte', scope: eggScope })
  })
  it.each([5, '1.5', '1e2', '-1', '100000000000000'])('recusa contagem não canônica %s no retorno', count => {
    expect(() => parseEggProof({ ...eggProof, contagem: count })).toThrow('inválidos')
  })
  it.each(['0', '2.0', '1,2', '1e2', '-1', '100000000000000'])('não converte peso ou fração em ordinal %s', count => expect(() => integerCount(count)).toThrow('inteira'))
  it('recusa bigint numérico, seleção incompleta e listagens inconsistentes', () => {
    expect(() => parseEggAnchor({ ...eggAnchor, revisao: 9007199254740992 })).toThrow()
    expect(() => parseEggPreview({ ...eggPreview, origens: [null] })).toThrow()
    expect(() => parseEggPage({ ...eggPage([eggCandidate()]), pageSize: 101 }, parseEggCandidate)).toThrow()
    expect(() => parseEggDetail({ ...eggDetail, tratamentos: [{ tipo: 'ReabrirId' }] })).toThrow()
  })
})
