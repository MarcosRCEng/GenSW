import { Link } from 'react-router-dom'
import { useRecordDetails } from '../../shared/details/useRecordDetails'
import { button, ErrorNotice, Section } from '../formulation/FormControls'
import { getDraftOrigin, optimizationPath } from './api'

export function DraftOrigin({ versionId, expected }: { versionId: string; expected?: string }) {
  const { record, state, retry } = useRecordDetails(versionId, getDraftOrigin)
  if (record?.versaoId === versionId && (!expected || record.otimizacaoId === expected)) {
    return <Section title="Origem do rascunho">
      <p>Este rascunho foi criado a partir de uma otimização assistida. O resultado conserva as quantidades e as fontes da criação original.</p>
      <Link className={button} to={`${optimizationPath}/${record.otimizacaoId}`}>Visualizar otimização de origem</Link>
    </Section>
  }
  if (!expected) return null
  if (state === 'loading') return <p role="status">Conferindo origem do rascunho…</p>
  return <div className="space-y-2">
    <ErrorNotice error="A origem da otimização não pôde ser conferida." />
    {state !== 'not-found' && <button className={button} type="button" onClick={retry}>Consultar origem novamente</button>}
  </div>
}
