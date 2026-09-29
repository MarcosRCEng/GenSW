export type BreedingStatus = 1 | 2 | 3 | 4
export interface BreedingAnimal { id: string; codigoInterno: string; nome: string | null }
export interface Breeding { id:string; machoId:string; femeaId:string; status:BreedingStatus; dataInicio:string|null; dataFim:string|null; objetivo:string|null; observacao:string|null; createdAtUtc:string; updatedAtUtc:string; macho:BreedingAnimal; femea:BreedingAnimal }
export interface BreedingsPage { items: Breeding[]; page:number; pageSize:number; totalItems:number; totalPages:number }
export interface BreedingRequest { machoId:string; femeaId:string; status:BreedingStatus; dataInicio:string|null; dataFim:string|null; objetivo:string|null; observacao:string|null }
