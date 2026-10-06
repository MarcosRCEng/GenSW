import { decimal } from './api'
import type { RecipeData, RecipeInput, RecipeOutput } from './types'
export const emptyRecipe = (): RecipeData => ({
  tipo: 'MisturaSimples',
  modo: 'Quantidade',
  tamanhoReferencia: '',
  unidadeReferencia: 'kg',
  entradas: [],
  saidas: [],
  perdas: [],
  etapas: [],
  observacao: null,
  retencoes: [],
})
export const newInput = (): RecipeInput => ({
  id: crypto.randomUUID(),
  itemId: '',
  papel: 'Alimentar',
  quantidade: '',
  unidade: 'kg',
  escala: 'Variavel',
  perfilId: null,
  conversaoId: null,
  subReceitaVersaoId: null,
  subSaidaId: null,
  inclusaoMinima: null,
  inclusaoMaxima: null,
})
export const newOutput = (): RecipeOutput => ({
  id: crypto.randomUUID(),
  itemId: '',
  nome: '',
  quantidade: '',
  unidade: 'kg',
  escala: 'Variavel',
  principal: true,
  perfilId: null,
  conversaoId: null,
  massaLiquidaKg: null,
  massaDrenadaKg: null,
  massaBrutaKg: null,
  metodoMedicao: null,
})
export function normalizeRecipe(d: RecipeData): RecipeData {
  const opt = (v: string | null) => (v === null || v === '' ? null : decimal(v))
  return {
    ...d,
    tamanhoReferencia: decimal(d.tamanhoReferencia),
    entradas: d.entradas.map((i) => ({
      ...i,
      quantidade: decimal(i.quantidade),
      inclusaoMinima: opt(i.inclusaoMinima),
      inclusaoMaxima: opt(i.inclusaoMaxima),
    })),
    saidas: d.saidas.map((o) => ({
      ...o,
      quantidade: decimal(o.quantidade),
      massaLiquidaKg: opt(o.massaLiquidaKg),
      massaDrenadaKg: opt(o.massaDrenadaKg),
      massaBrutaKg: opt(o.massaBrutaKg),
    })),
    perdas: d.perdas.map((p) => ({ ...p, quantidade: decimal(p.quantidade) })),
    retencoes: d.retencoes.map((r) => ({ ...r, fator: decimal(r.fator) })),
  }
}
