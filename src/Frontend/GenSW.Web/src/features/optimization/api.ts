import { read, send } from '../formulation/api'
import { httpRequest } from '../../shared/http/httpClient'
import { InvalidApiResponseError } from '../../shared/http/httpErrors'
import type { DraftCommand, DraftDestination, DraftPreview, DraftReceipt, OptimizationExecution, OptimizationInputs, OptimizationRequest, Outcome } from './types'

export const optimizationPath = '/producao/formulacao/otimizacoes'
export const optimizationApi = '/otimizacoes-formulacao'

export const outcomeLabels: Record<Outcome, string> = {
  SolucaoViavel: 'Solução viável',
  InviabilidadeDemonstrada: 'Inviabilidade demonstrada',
  DadosIncompletos: 'Dados incompletos',
  DadosIncompativeis: 'Dados incompatíveis',
  LimiteTempo: 'Limite de tempo',
  ErroAlgoritmo: 'Erro do algoritmo',
  Inconclusivo: 'Inconclusivo',
}

export const outcomeLabel = (outcome: Outcome | null | undefined) => outcome ? outcomeLabels[outcome] : 'Em cálculo'

export function isGuid(value: unknown): value is string {
  return typeof value === 'string' && /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value)
}

function execution(value: OptimizationExecution): OptimizationExecution {
  if (!value || !isGuid(value.id) || !value.pedido || !value.entradas ||
      (value.estado !== 'EmCalculo' && (value.estado !== 'Finalizada' || !value.resultado || !(value.resultado.desfecho in outcomeLabels)))) {
    throw new InvalidApiResponseError()
  }
  validateInputs(value.entradas)
  return value
}

function validateInputs(value: OptimizationInputs) {
  if (!value || !Array.isArray(value.candidatos) || !Array.isArray(value.metas) || !Array.isArray(value.diagnosticos) || typeof value.corteUtc !== 'string') {
    throw new InvalidApiResponseError()
  }
  return value
}

function receipt(value: DraftReceipt): DraftReceipt {
  if (!value || !isGuid(value.id) || !isGuid(value.otimizacaoId) || !isGuid(value.receitaId) || !isGuid(value.versaoId)) throw new InvalidApiResponseError()
  return value
}

const withKey = (chave: string) => new URLSearchParams({ chave })
export const getOptimization = async (id: string) => execution(await read<OptimizationExecution>(`${optimizationApi}/${encodeURIComponent(id)}`))
export const recoverOptimization = async (key: string) => execution(await read<OptimizationExecution>(`${optimizationApi}/por-chave?${withKey(key)}`))
export const prepareOptimization = async (body: OptimizationRequest) => validateInputs(await send<OptimizationInputs>(`${optimizationApi}/previas`, body))
export const previewDraft = async (id: string, body: DraftDestination) => {
  const value = await send<DraftPreview>(`${optimizationApi}/${id}/previas-rascunho`, body)
  if (!value || !value.hash || !Array.isArray(value.conteudo?.entradas) || !Array.isArray(value.avisos) || typeof value.exigeAceiteResiduos !== 'boolean') throw new InvalidApiResponseError()
  return value
}
export const getDraftOrigin = async (id: string) => receipt(await read<DraftReceipt>(`${optimizationApi}/rascunhos/por-versao/${encodeURIComponent(id)}`))
export const recoverDraft = async (id: string, key: string) => receipt(await read<DraftReceipt>(`${optimizationApi}/${id}/rascunhos/por-chave?${withKey(key)}`))

async function submit<T>(path: string, body: unknown, key: string) {
  return httpRequest<T>(path, {
    authenticated: true,
    method: 'POST',
    body,
    headers: { 'Idempotency-Key': key },
    signal: AbortSignal.timeout(35000),
  })
}

export const submitOptimization = async (body: OptimizationRequest, key: string) => execution(await submit<OptimizationExecution>(optimizationApi, body, key))
export const submitDraft = async (id: string, body: DraftCommand, key: string) => receipt(await submit<DraftReceipt>(`${optimizationApi}/${id}/rascunhos`, body, key))
