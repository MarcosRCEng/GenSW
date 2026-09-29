export type OffspringRecordType=1|2; export type OffspringOrigin=1|2
export interface Offspring {id:string;cicloReprodutivoId:string;loteOrigemId:string|null;tipoRegistro:OffspringRecordType;quantidade:number;quantidadeDesdobrada:number;origem:OffspringOrigin;data:string;pesoGramas:number|null;sexo:1|2|3;condicao:string;observacao:string|null;animalId:string|null;createdAtUtc:string;updatedAtUtc:string}
export interface OffspringPage {items:Offspring[];page:number;pageSize:number;totalItems:number;totalPages:number}
export type OffspringInput={origem:OffspringOrigin;data:string;pesoGramas:number|null;sexo:1|2|3;condicao:string;observacao:string|null}
export interface OffspringConversion {prole:Offspring;animal:{id:string;codigoInterno:string};paiSugerido:{id:string;codigoInterno:string;nome:string|null};maeSugerida:{id:string;codigoInterno:string;nome:string|null}}
