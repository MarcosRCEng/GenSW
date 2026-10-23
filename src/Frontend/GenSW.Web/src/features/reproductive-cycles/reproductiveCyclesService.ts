import { httpRequest } from '../../shared/http/httpClient'
import type { ReproductiveCycle,ReproductiveCycleRequest,ReproductiveCycleStatus,ReproductiveCycleType,ReproductiveCyclesPage } from './types'
const root='/ciclos-reprodutivos'
export const listReproductiveCycles=({page=1,pageSize=25,...filters}:{cruzamentoId?:string;tipo?:ReproductiveCycleType;status?:ReproductiveCycleStatus;page?:number;pageSize?:number}={})=>{const q=new URLSearchParams();Object.entries({page,pageSize,...filters}).forEach(([k,v])=>{if(v!==undefined)q.set(k,String(v))});return httpRequest<ReproductiveCyclesPage>(`${root}?${q}`,{authenticated:true})}
export const getReproductiveCycle=(id:string)=>httpRequest<ReproductiveCycle>(`${root}/${id}`,{authenticated:true})
export const createReproductiveCycle=(cruzamentoId:string,body:ReproductiveCycleRequest)=>httpRequest<ReproductiveCycle>(`${root}?cruzamentoId=${encodeURIComponent(cruzamentoId)}`,{method:'POST',authenticated:true,body})
export const updateReproductiveCycle=(id:string,body:ReproductiveCycleRequest)=>httpRequest<ReproductiveCycle>(`${root}/${id}`,{method:'PUT',authenticated:true,body})
export const setReproductiveCycleStatus=(id:string,status:ReproductiveCycleStatus)=>httpRequest<ReproductiveCycle>(`${root}/${id}/status`,{method:'PATCH',authenticated:true,body:{status}})
