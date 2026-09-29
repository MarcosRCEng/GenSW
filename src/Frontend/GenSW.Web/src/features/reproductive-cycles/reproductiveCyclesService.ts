import { httpRequest } from '../../shared/http/httpClient'
import type { ReproductiveCycle,ReproductiveCycleRequest,ReproductiveCycleStatus,ReproductiveCycleType,ReproductiveCyclesPage } from './types'
const root='/ciclos-reprodutivos'
export const listReproductiveCycles=(p:{cruzamentoId?:string;tipo?:ReproductiveCycleType;status?:ReproductiveCycleStatus}={})=>{const q=new URLSearchParams();Object.entries(p).forEach(([k,v])=>{if(v!==undefined)q.set(k,String(v))});return httpRequest<ReproductiveCyclesPage>(q.size?`${root}?${q}`:root,{authenticated:true})}
export const getReproductiveCycle=(id:string)=>httpRequest<ReproductiveCycle>(`${root}/${id}`,{authenticated:true})
export const createReproductiveCycle=(cruzamentoId:string,body:ReproductiveCycleRequest)=>httpRequest<ReproductiveCycle>(`${root}?cruzamentoId=${encodeURIComponent(cruzamentoId)}`,{method:'POST',authenticated:true,body})
export const updateReproductiveCycle=(id:string,body:ReproductiveCycleRequest)=>httpRequest<ReproductiveCycle>(`${root}/${id}`,{method:'PUT',authenticated:true,body})
export const setReproductiveCycleStatus=(id:string,status:ReproductiveCycleStatus)=>httpRequest<ReproductiveCycle>(`${root}/${id}/status`,{method:'PATCH',authenticated:true,body:{status}})
