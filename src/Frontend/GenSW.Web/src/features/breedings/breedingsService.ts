import { httpRequest } from '../../shared/http/httpClient'
import type { Breeding, BreedingRequest, BreedingsPage, BreedingStatus } from './types'
const root='/cruzamentos'
export const listBreedings=(params:{page?:number;pageSize?:number;status?:BreedingStatus;machoId?:string;femeaId?:string}={})=>{const q=new URLSearchParams();Object.entries(params).forEach(([k,v])=>{if(v!==undefined)q.set(k,String(v))});return httpRequest<BreedingsPage>(q.size?`${root}?${q}`:root,{authenticated:true})}
export const getBreeding=(id:string)=>httpRequest<Breeding>(`${root}/${id}`,{authenticated:true})
export const createBreeding=(body:BreedingRequest)=>httpRequest<Breeding>(root,{method:'POST',authenticated:true,body})
export const updateBreeding=(id:string,body:BreedingRequest)=>httpRequest<Breeding>(`${root}/${id}`,{method:'PUT',authenticated:true,body})
export const setBreedingStatus=(id:string,status:BreedingStatus)=>httpRequest<Breeding>(`${root}/${id}/status`,{method:'PATCH',authenticated:true,body:{status}})
