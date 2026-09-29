export type AnimalRegistrationType = 1 | 2
export interface AnimalRegistration { id: string; animalId: string; tipoRegistro: AnimalRegistrationType; numeroRegistro: string; ativo: boolean; dataInicio: string; dataFim: string | null; createdAtUtc: string; updatedAtUtc: string }
