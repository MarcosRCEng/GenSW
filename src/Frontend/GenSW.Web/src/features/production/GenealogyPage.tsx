import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { httpRequest } from '../../shared/http/httpClient'
import { buttonClass, Input, Notice, Pagination, Panel, ScrollTable, Select } from '../inventory/InventoryControls'
import { inventoryError, queryPath } from '../inventory/inventoryService'
import { parseGenealogy, parseProductionPage } from './productionService'
import { ProductionFrame, ReferenceLabel } from './ProductionControls'
import type { GenealogyEdge, ProductionPage } from './types'

export function ProductionGenealogyPage() {
  const { loteId } = useParams()
  return <Genealogy key={loteId} loteId={loteId} />
}
function Genealogy({ loteId }: { loteId?: string }) {
  const [direction, setDirection] = useState('ambas')
  const [depth, setDepth] = useState('3')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<ProductionPage<GenealogyEdge> | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let live = true; setData(null); setError(null)
    if (!loteId) { setError('Lote não informado.'); return }
    if (!/^\d+$/.test(depth) || Number(depth) < 1 || Number(depth) > 10) { setError('Informe profundidade entre 1 e 10.'); return }
    void httpRequest<unknown>(queryPath(`/estoque/lotes/${loteId}/genealogia`, { direcao: direction, profundidade: depth, page, pageSize: 25 }), { authenticated: true }).then(raw => parseProductionPage(raw, parseGenealogy)).then(v => { if (live) setData(v) }).catch(e => { if (live) setError(inventoryError(e)) })
    return () => { live = false }
  }, [loteId, direction, depth, page, retry])
  return <ProductionFrame title="Genealogia física de materiais"><Panel>{loteId && <p><ReferenceLabel kind="lote" id={loteId} /></p>}<p>Cada ligação representa material consumido numa execução e seu lote de saída. Multissaída não implica rateio causal, nutricional ou de custos.</p><div className="grid min-w-0 gap-4 sm:grid-cols-2"><Select label="Direção" value={direction} onChange={v => { setDirection(v); setPage(1) }} options={[[ 'ambas', 'Entradas e saídas'], ['origens', 'Ancestrais'], ['destinos', 'Descendentes']]} /><Input label="Profundidade máxima" value={depth} onChange={v => { setDepth(v); setPage(1) }} hint="De 1 a 10 níveis; resultados paginados." /></div></Panel><Notice error={error} retry={() => setRetry(v => v + 1)} />{!data && !error && <p role="status">Consultando genealogia…</p>}{data && <Panel>{!data.items.length ? <p>Nenhuma transformação encontrada neste corte.</p> : <ScrollTable label="Cadeia de transformação"><thead><tr><th scope="col">Lote consumido</th><th scope="col">Execução da ordem</th><th scope="col">Lote produzido</th><th scope="col">Percorrer</th></tr></thead><tbody>{data.items.map((edge, index) => <tr key={`${edge.execucaoId}/${edge.loteEntradaId}/${edge.loteSaidaId}/${index}`}><td><ReferenceLabel kind="lote" id={edge.loteEntradaId} /></td><td><ReferenceLabel kind="ordem" id={edge.ordemId} /></td><td><ReferenceLabel kind="lote" id={edge.loteSaidaId} /></td><td><div className="flex flex-wrap gap-2"><Link className={buttonClass} to={`/producao/genealogia/${edge.loteEntradaId}`}>Consultar entrada</Link><Link className={buttonClass} to={`/producao/genealogia/${edge.loteSaidaId}`}>Consultar saída</Link></div></td></tr>)}</tbody></ScrollTable>}<Pagination page={page} totalPages={data.totalPages} onChange={setPage} /></Panel>}</ProductionFrame>
}
