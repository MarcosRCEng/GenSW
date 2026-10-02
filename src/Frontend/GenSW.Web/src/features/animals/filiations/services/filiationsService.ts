import { httpRequest } from '../../../../shared/http/httpClient'
export type FiliationType = 1 | 2
export interface Filiation { id:string; animalId:string; progenitorId:string; tipoFiliacao:FiliationType; ativa:boolean; dataRegistro:string|null; dataFim:string|null; progenitor?: { id:string; codigoInterno:string; nome:string|null; ativo:boolean } }
export interface Pedigree { animalId:string; codigoInterno:string; nome:string|null; progenitores:Pedigree[] }
const root=(id:string)=>`/animais/${id}`
export const listFiliations=(id:string)=>httpRequest<Filiation[]>(`${root(id)}/filiacoes`,{authenticated:true})
export const createFiliation=(id:string, body:{progenitorId:string;tipoFiliacao:FiliationType;dataRegistro:string|null})=>httpRequest<Filiation>(`${root(id)}/filiacoes`,{method:'POST',authenticated:true,body})
export const getPedigree=(id:string)=>httpRequest<Pedigree>(`${root(id)}/pedigree?generations=3`,{authenticated:true})
