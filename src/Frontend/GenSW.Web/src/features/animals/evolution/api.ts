import { httpRequest } from '../../../shared/http/httpClient'
import { isHttpError } from '../../../shared/http/httpErrors'

export interface Page<T> { items: T[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export const api = <T>(path: string, signal?: AbortSignal) => httpRequest<T>(path, { authenticated: true, signal })
export const save = <T>(path: string, body: unknown, method: 'POST' | 'PUT' = 'PUT') => httpRequest<T>(path, { authenticated: true, method, body })
export const message = (error: unknown) => isHttpError(error) && error.detail ? error.detail : 'Não foi possível concluir a operação. Tente novamente.'
export interface Weight { id: string; dataMedicao: string; pesoGramas: number; tipoMarco: number; descricaoMarco: string | null; idadeReferenciaDias: number | null; idadeDiasNaMedicao: number | null; observacao: string | null }
export interface Photo { id: string; legenda: string | null; dataCaptura: string | null; ordem: number; ativa: boolean; representativa: boolean; thumbnailPath: string; conteudoPath: string }
export interface PhotoSummary { id: string; legenda: string | null; thumbnailPath: string }
export interface TreeNode { animalId: string; codigoInterno: string; nome: string | null; sexo: number; ativo: boolean; dataNascimento: string | null; imagem: PhotoSummary | null; origemImagem: string; paiConhecido: boolean; maeConhecida: boolean; temAscendentesAdicionais: boolean; temDescendentesAdicionais: boolean }
export interface TreeEdge { filiacaoId: string; progenitorId: string; descendenteId: string; tipoFiliacao: number }
export interface Tree { raizId: string; nos: TreeNode[]; arestas: TreeEdge[]; truncada: boolean; avisos: string[] }
export interface Relations extends Tree { page: number; totalPages: number }
export interface Candidate { id: string; codigoInterno: string; nome: string | null }
