import { useCallback } from 'react'
import { useParams } from 'react-router-dom'
import { DetailField, DetailStatus, DetailsPage } from '../../../shared/details/DetailsPage'
import { useRecordDetails } from '../../../shared/details/useRecordDetails'
import { HttpError } from '../../../shared/http/httpErrors'
import { api, type Page, type Photo, type Weight } from '../evolution/api'
import { AuthenticatedPhoto } from '../evolution/AuthenticatedPhoto'
import { getAnimalIdentification } from '../identifications/services/identificationsService'
import type { AnimalIdentification } from '../identifications/types'
import { listAnimalRegistrations } from '../registrations/services/registrationsService'
import type { AnimalRegistration } from '../registrations/types'

export type AnimalRelatedSection = 'identificacoes' | 'registros' | 'pesagens' | 'producoes-ovos' | 'imagens'
type EggEntry = { id: string; dataPostura: string; pesoGramas: number; observacao: string | null }
type RelatedRecord = { section: 'identificacoes'; item: AnimalIdentification } | { section: 'registros'; item: AnimalRegistration } | { section: 'pesagens'; item: Weight } | { section: 'producoes-ovos'; item: EggEntry } | { section: 'imagens'; item: Photo }
const titles: Record<AnimalRelatedSection, string> = { identificacoes: 'identificação física', registros: 'registro institucional', pesagens: 'pesagem', 'producoes-ovos': 'produção de ovos', imagens: 'imagem' }
const identificationTypes = ['Anilha', 'Brinco', 'Microchip', 'Tatuagem', 'Marca', 'Outro']
const milestones = ['Livre', 'Nascimento', 'Idade em dias', 'Primeira postura', 'Abate', 'Outro']
const date = (value: string | null) => value?.split('-').reverse().join('/') ?? 'Não informada'

// Some existing APIs expose a collection only. Walk every page until the stable
// identifier is found, so a bookmarked historical item is never limited to page 1.
async function findInPages<T extends { id: string }>(root: string, id: string, filter = ''): Promise<T | undefined> {
  let page = 1
  let totalPages = 1
  do {
    const result = await api<Page<T>>(`${root}?page=${page}&pageSize=100${filter}`)
    const item = result.items.find(candidate => candidate.id === id)
    if (item) return item
    totalPages = result.totalPages
    page += 1
  } while (page <= totalPages)
  return undefined
}

export function AnimalRelatedDetailsPage({ section, kind = 'animais' }: { section: AnimalRelatedSection; kind?: 'animais' | 'variedades' }) {
  const { animalId, varietyId, recordId } = useParams<{ animalId: string; varietyId: string; recordId: string }>()
  const ownerId = kind === 'variedades' ? varietyId : animalId
  const load = useCallback(async (id: string): Promise<RelatedRecord> => {
    if (!ownerId) throw new HttpError(404)
    const root = `/${kind}/${ownerId}/${section}`
    if (section === 'identificacoes') return { section, item: await getAnimalIdentification(ownerId, id) }
    if (section === 'registros') {
      const item = (await listAnimalRegistrations(ownerId)).find(candidate => candidate.id === id)
      if (item) return { section, item }
    }
    if (section === 'pesagens') {
      return { section, item: await api<Weight>(`${root}/${id}`) }
    }
    if (section === 'producoes-ovos') {
      const item = await findInPages<EggEntry>(root, id)
      if (item) return { section, item }
    }
    if (section === 'imagens') {
      const item = await findInPages<Photo>(root, id, '&ativo=true') ?? await findInPages<Photo>(root, id, '&ativo=false')
      if (item) return { section, item }
    }
    throw new HttpError(404)
  }, [kind, ownerId, section])
  const { record, state, retry } = useRecordDetails(recordId, load)
  return <DetailsPage title={`Visualizar ${titles[section]}`} listPath={`/${kind}/${ownerId}`} listLabel={kind === 'animais' ? 'Animal' : 'Variedade'} state={state} onRetry={retry} editPath={ownerId ? `/${kind}/${ownerId}/editar` : undefined}>
    {record && <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"><dl className="grid gap-5 sm:grid-cols-2">
      {record.section === 'identificacoes' && <>
        <DetailField label="Tipo">{identificationTypes[record.item.tipo - 1]}{record.item.descricaoTipo && ` — ${record.item.descricaoTipo}`}</DetailField>
        <DetailField label="Valor">{record.item.valor}</DetailField>
        <DetailField label="Status"><DetailStatus active={record.item.ativo} /></DetailField>
        <DetailField label="Principal">{record.item.principal ? 'Sim' : 'Não'}</DetailField>
        <DetailField label="Data de aplicação">{date(record.item.dataAplicacao)}</DetailField>
        <DetailField label="Observação">{record.item.observacao}</DetailField>
      </>}
      {record.section === 'registros' && <>
        <DetailField label="Tipo">{record.item.tipoRegistro === 1 ? 'SISBOV' : 'UELN'}</DetailField>
        <DetailField label="Número do registro">{record.item.numeroRegistro}</DetailField>
        <DetailField label="Status"><DetailStatus active={record.item.ativo} /></DetailField>
        <DetailField label="Data de início">{date(record.item.dataInicio)}</DetailField>
        <DetailField label="Data de fim">{date(record.item.dataFim)}</DetailField>
      </>}
      {record.section === 'pesagens' && <>
        <DetailField label="Data da medição">{date(record.item.dataMedicao)}</DetailField>
        <DetailField label="Peso">{record.item.pesoGramas.toLocaleString('pt-BR')} g</DetailField>
        <DetailField label="Marco">{milestones[record.item.tipoMarco - 1]}</DetailField>
        <DetailField label="Descrição do marco">{record.item.descricaoMarco}</DetailField>
        <DetailField label="Idade alvo (dias)">{record.item.idadeReferenciaDias}</DetailField>
        <DetailField label="Idade real na medição (dias)">{record.item.idadeDiasNaMedicao}</DetailField>
        <DetailField label="Observação">{record.item.observacao}</DetailField>
      </>}
      {record.section === 'producoes-ovos' && <>
        <DetailField label="Data da postura">{date(record.item.dataPostura)}</DetailField>
        <DetailField label="Peso">{record.item.pesoGramas.toLocaleString('pt-BR')} g</DetailField>
        <DetailField label="Observação">{record.item.observacao}</DetailField>
      </>}
      {record.section === 'imagens' && <>
        <DetailField label="Imagem"><AuthenticatedPhoto path={record.item.ativa ? record.item.conteudoPath : undefined} name={record.item.legenda ?? 'imagem'} presentation="full" />{!record.item.ativa && <p>O conteúdo de imagens inativas não está disponível; os metadados permanecem no histórico.</p>}</DetailField>
        <DetailField label="Legenda">{record.item.legenda}</DetailField>
        <DetailField label="Data da captura">{date(record.item.dataCaptura)}</DetailField>
        <DetailField label="Status"><DetailStatus active={record.item.ativa} /></DetailField>
        <DetailField label="Representativa">{record.item.representativa ? 'Sim' : 'Não'}</DetailField>
        <DetailField label="Ordem">{record.item.ordem}</DetailField>
      </>}
    </dl></section>}
  </DetailsPage>
}
