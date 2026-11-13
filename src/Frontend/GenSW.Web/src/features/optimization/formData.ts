import { decimal, read } from '../formulation/api'
import type { Item, Profile, Recipe, RecipeData, RecipeVersion } from '../formulation/types'
import type { Candidate, OptimizationGoal, OptimizationRequest } from './types'

export type CandidateForm = Omit<Candidate, 'inclusaoMinima' | 'inclusaoMaxima'> & {
  nomeItem?: string
  perfil?: Profile | null
  unidadeReferencia: string
  inclusaoMinima: string
  inclusaoMaxima: string
  limitesImportados?: { min: string; max: string }
}

export const newCandidate = (): CandidateForm => ({
  id: crypto.randomUUID(), itemId: '', itemRevisao: 0, perfilId: null, perfilRevisao: null,
  referenciaKg: '0', unidadeReferencia: 'kg', inclusaoMinima: '0', inclusaoMaxima: '100', justificativa: null,
})

export const newGoal = (): OptimizationGoal => ({
  id: crypto.randomUUID(), componente: '', metodo: '', contexto: '', base: 'BN', unidade: 'g/kg',
  minimo: null, maximo: null, origem: 'InformadaUsuario', fonte: null, aceitarEstimados: false, justificativa: '',
})

export const itemLabel = (item: Item) => `${item.codigo} · ${item.nome}`
export const profileLabel = (profile: Profile) => `${profile.conteudo.nome} · v${profile.numero} · ${profile.conteudo.referenciaAmostra}`
export const eligibleInput = (item: Item) => item.ativo && item.podeEntrar && item.classe === 'Alimentar' && item.unidade === 'kg'
export const eligibleOutput = (item: Item) => item.ativo && item.podeProduzir && item.classe === 'Alimentar' && item.unidade === 'kg'

export function decimalField(value: string, label: string) {
  const parsed = decimal(value)
  if (!/^(0|[1-9]\d{0,13})(?:\.\d{1,6})?$/.test(parsed)) {
    throw new Error(`${label} deve ter até 14 dígitos inteiros e seis casas decimais, sem separador de milhar.`)
  }
  return parsed.includes('.') ? parsed.replace(/0+$/, '').replace(/\.$/, '') : parsed
}

function microKg(value: string) {
  const [whole, fraction = ''] = value.split('.')
  return BigInt(whole + fraction.padEnd(6, '0'))
}

export function kgField(value: string, unit: string, label: string) {
  const amount = decimalField(value, label)
  if (unit === 'kg') return amount
  if (unit !== 'g') throw new Error(`${label} exige kg ou g.`)
  const scaled = microKg(amount)
  if (scaled % 1000n !== 0n) throw new Error(`${label} não pode ser expressa em kg com seis casas sem arredondamento.`)
  const kg = scaled / 1000n
  const fraction = (kg % 1000000n).toString().padStart(6, '0').replace(/0+$/, '')
  return `${kg / 1000000n}${fraction ? `.${fraction}` : ''}`
}

export async function importCandidates(version: RecipeVersion, mass: string) {
  const data = version.conteudo
  if (data.tipo !== 'MisturaSimples' || data.saidas.length !== 1 || data.perdas.length || data.retencoes.length ||
      data.entradas.some((line) => line.papel !== 'Alimentar' || line.conversaoId || line.subReceitaVersaoId) ||
      new Set(data.entradas.map((line) => line.itemId)).size !== data.entradas.length) {
    throw new Error('Escolha uma mistura simples de saída única, com ingredientes alimentares distintos e sem conversões contextuais ou sub-receitas.')
  }
  if (data.entradas.length > 100) throw new Error('A receita excede o limite de 100 linhas por pedido.')
  const query = new URLSearchParams({ tamanho: mass, unidade: 'kg' })
  const scaled = await read<RecipeData>(`/receitas/versoes/${version.id}/escalonamento?${query}`)
  return Promise.all(scaled.entradas.map(async (line): Promise<CandidateForm> => {
    const [item, profile] = await Promise.all([
      read<Item>(`/itens/${line.itemId}`),
      line.perfilId ? read<Profile>(`/perfis-nutricionais/${line.perfilId}`) : null,
    ])
    const min = line.inclusaoMinima ?? '0'
    const max = line.inclusaoMaxima ?? '100'
    return {
      id: line.id, itemId: item.id, itemRevisao: item.revisao, nomeItem: itemLabel(item),
      perfilId: profile?.id ?? null, perfilRevisao: profile?.revisao ?? null, perfil: profile,
      referenciaKg: kgField(line.quantidade, line.unidade, 'Massa de referência do ingrediente'), unidadeReferencia: 'kg',
      inclusaoMinima: min, inclusaoMaxima: max, justificativa: null, limitesImportados: { min, max },
    }
  }))
}

export function requestFromForm({ mass, unit, reference, recipe, version, importedMass, prior, species, phase, preparation, candidates, goals }: {
  mass: string
  unit: string
  reference: string
  recipe: Recipe | null
  version: RecipeVersion | null
  importedMass: string | null
  prior: string | null
  species: string | null
  phase: string
  preparation: string
  candidates: CandidateForm[]
  goals: OptimizationGoal[]
}): OptimizationRequest {
  const massaKg = kgField(mass, unit, 'Massa da mistura')
  if (massaKg === '0') throw new Error('Informe uma massa positiva para a mistura.')
  if (recipe && (!version || importedMass !== massaKg)) throw new Error('Importe a composição da versão escolhida para a massa atual.')
  if (!recipe && !reference.trim()) throw new Error('Identifique a formulação usada como referência.')
  if (!candidates.length || !goals.length) throw new Error('Informe ao menos um ingrediente e uma meta.')
  if (candidates.some((c) => !c.itemId)) throw new Error('Selecione o item de cada ingrediente.')
  if (new Set(candidates.map((c) => c.itemId)).size !== candidates.length) throw new Error('Informe cada ingrediente uma única vez.')
  const linhas = candidates.map((c): Candidate => {
    const inclusaoMinima = decimalField(c.inclusaoMinima, 'Inclusão mínima')
    const inclusaoMaxima = decimalField(c.inclusaoMaxima, 'Inclusão máxima')
    const justificativa = c.justificativa?.trim() || null
    if ((inclusaoMaxima === '0' || changedLimits(c)) && !justificativa) throw new Error('Justifique toda exclusão e alteração de limites importados.')
    return {
      id: c.id, itemId: c.itemId, itemRevisao: c.itemRevisao, perfilId: c.perfilId, perfilRevisao: c.perfilRevisao,
      referenciaKg: kgField(c.referenciaKg, c.unidadeReferencia, 'Massa de referência do ingrediente'),
      inclusaoMinima, inclusaoMaxima, justificativa,
    }
  })
  if (linhas.reduce((total, line) => total + microKg(line.referenciaKg), 0n) !== microKg(massaKg)) {
    throw new Error('A soma das massas de referência dos ingredientes precisa ser igual à massa da mistura.')
  }
  const metas = goals.map((goal) => {
    const minimo = goal.minimo?.trim() ? decimalField(goal.minimo, 'Mínimo da meta') : null
    const maximo = goal.maximo?.trim() ? decimalField(goal.maximo, 'Máximo da meta') : null
    if (!goal.componente || !goal.metodo.trim() || (minimo === null && maximo === null)) {
      throw new Error('Cada meta precisa de componente, método e pelo menos um limite.')
    }
    const fonte = goal.fonte?.trim() || null
    const justificativa = goal.justificativa?.trim() || null
    if (((goal.origem === 'InformadaUsuario' || goal.aceitarEstimados) && !justificativa) ||
        ((goal.origem === 'ReferenciaTecnica' || goal.aceitarEstimados) && !fonte)) {
      throw new Error('Justifique cada meta e informe a fonte para referência técnica ou aceite de estimativa.')
    }
    return { ...goal, minimo, maximo, fonte, justificativa }
  })
  return {
    versaoModelo: 'f05-n1/1', modo: 'N1', disponibilidade: false, massaKg, referenciaDescricao: recipe ? null : reference.trim(),
    receitaId: recipe?.id ?? null, receitaRevisao: recipe?.revisao ?? null, receitaVersaoId: version?.id ?? null,
    receitaVersaoRevisao: version?.revisao ?? null, anteriorId: prior, especieId: species,
    fase: phase.trim() ? phase : null, preparo: preparation.trim() ? preparation : null,
    candidatos: linhas, metas,
  }
}

export function changedLimits(candidate: CandidateForm) {
  const original = candidate.limitesImportados
  if (!original) return false
  try {
    return decimalField(candidate.inclusaoMinima, 'Mínimo') !== decimalField(original.min, 'Mínimo') ||
      decimalField(candidate.inclusaoMaxima, 'Máximo') !== decimalField(original.max, 'Máximo')
  } catch { return true }
}
