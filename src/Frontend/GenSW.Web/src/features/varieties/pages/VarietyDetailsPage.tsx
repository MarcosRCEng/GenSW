import { useState } from 'react'
import { useLocation, useParams } from 'react-router-dom'
import { DetailsPage, DetailField, DetailStatus } from '../../../shared/details/DetailsPage'
import { useRecordDetails } from '../../../shared/details/useRecordDetails'
import { ImageGallery } from '../../animals/evolution/ImageGallery'
import { VarietyRecordTabs, type VarietyRecordTab } from '../components/VarietyRecordTabs'
import { readVarietyImageContext } from '../navigation/varietyImageNavigation'
import { getVariedadeById } from '../services/varietiesService'

export function VarietyDetailsPage() {
  const { id } = useParams<{ id: string }>()
  return <VarietyDetailsRecord id={id} key={id} />
}

function VarietyDetailsRecord({ id }: { id?: string }) {
  const location = useLocation()
  const imageContext = readVarietyImageContext(location.state, location.pathname)
  const [tab, setTab] = useState<VarietyRecordTab>(imageContext ? 'imagens' : 'ficha')
  const { record: variedade, state, retry } = useRecordDetails(id, getVariedadeById)
  return <DetailsPage title="Detalhes da variedade" listPath="/variedades" listLabel="Variedades" state={state} onRetry={retry}
    editPath={variedade ? `/variedades/${variedade.id}/editar` : undefined}
    context={variedade && <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
      <strong className="text-slate-900 [overflow-wrap:anywhere]">{variedade.nome}</strong>
      <DetailStatus active={variedade.ativo} />
    </div>}>
    {variedade && <VarietyRecordTabs activeTab={tab} onSelect={setTab}
      general={<dl className="grid gap-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:grid-cols-2">
        <DetailField label="Nome">{variedade.nome}</DetailField>
        <DetailField label="Espécie">{variedade.especie.nomeComum}{variedade.especie.ativo ? '' : ' (inativa)'}</DetailField>
        <DetailField label="Status"><DetailStatus active={variedade.ativo} /></DetailField>
      </dl>}
      images={<ImageGallery ownerId={variedade.id} kind="variedades" imageContext={imageContext} panelActive={tab === 'imagens'} readOnly />}
    />}
  </DetailsPage>
}
