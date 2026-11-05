import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/hooks/useAuth'
import { PageHeader } from '../../shared/layout/PageHeader'
import { Input, Notice, Panel, ScrollTable, Select, buttonClass } from '../inventory/InventoryControls'
import { inventoryError } from '../inventory/inventoryService'
import { ReferenceLabel } from '../production/ProductionControls'
import { displayDecimal, displayMoney, isZeroQuantity } from './decimals'
import type { CostProductionPreview, CostSummary } from './types'
import type { EvidenceForm, ReceiptForm, AllocationForm, AllocationDestination } from './costForms'
import type { CostCommandControl } from './useCostCommand'

export function CostFrame({ title, children }: { title: string; children: ReactNode }) {
  const { user } = useAuth()
  return <main className="gensw-page min-w-0 px-4 py-6 sm:px-6"><div className="mx-auto max-w-6xl space-y-6"><PageHeader title={title} context="Custos de materiais" /><nav aria-label="Custos de materiais" className="flex flex-wrap gap-2"><Link className={buttonClass} to="/custos-materiais/referencias">Referências</Link><Link className={buttonClass} to="/custos-materiais/estimativas/nova">Estimar por referência</Link><Link className={buttonClass} to="/custos-materiais/lotes">Custos por lote</Link><Link className={buttonClass} to="/custos-materiais/avaliacoes">Avaliações presentes</Link>{user?.roles.includes('Admin') && <Link className={buttonClass} to="/custos-materiais/reconciliacao">Reconciliação de custos</Link>}</nav><p className="text-sm text-slate-600">Este recorte apura materiais. Referências e estimativas ficam separadas do comprovado; não inclui custo industrial completo, margem ou lucro e não lança dinheiro no Caixa.</p>{children}</div></main>
}
export function CostSummaryPanel({ value, title = 'Cobertura dos materiais', components = true, unit = true }: { value: CostSummary; title?: string; components?: boolean; unit?: boolean }) {
  return <Panel title={title}><p className="font-semibold">Cobertura: {value.estado}</p><dl className="grid min-w-0 gap-4 sm:grid-cols-3"><div><dt className="font-semibold">Parcela conhecida</dt><dd className="break-words">{value.estado === 'Desconhecido' ? 'Sem parcela comprovada' : displayMoney(value.valorConhecido)}</dd></div><div><dt className="font-semibold">Total completo de materiais</dt><dd className="break-words">{value.valorTotal === null ? 'Desconhecido — cobertura incompleta' : displayMoney(value.valorTotal)}</dd></div>{unit && <div><dt className="font-semibold">Custo unitário completo</dt><dd>{value.custoUnitarioCompleto === null ? 'Não determinado ou não aplicável' : displayDecimal(value.custoUnitarioCompleto)}</dd></div>}</dl>{value.lacunas.length > 0 && <div className="space-y-2 rounded-lg border border-amber-300 bg-amber-50 p-3"><h3 className="font-semibold">Lacunas preservadas</h3><ul className="list-inside list-disc break-words">{value.lacunas.map((gap, i) => <li key={i}>{gap}</li>)}</ul></div>}{components && value.componentes.length > 0 && <ScrollTable label="Cobertura por componente"><thead><tr>{['Material de origem', 'Quantidade e cobertura', 'Parcela conhecida', 'Evidência', 'Lacunas'].map(label => <th scope="col" key={label}>{label}</th>)}</tr></thead><tbody>{value.componentes.map((component, i) => <tr key={`${component.id}/${i}`}><td><ReferenceLabel kind="item" id={component.itemId} /></td><td>{displayDecimal(component.quantidade)} {component.unidade}<br /><span className="text-xs">Parcela exata da origem: {component.fracaoNumerador}/{component.fracaoDenominador}</span></td><td>{component.valorConhecido === null ? 'Desconhecido' : displayMoney(component.valorConhecido)}</td><td>{component.evidenciaId ? <Link className="text-emerald-700 underline" to={`/custos-materiais/evidencias/${component.evidenciaId}`}>Consultar documento atribuído</Link> : component.valorConhecido === null ? 'Documento ausente' : 'Zero atribuído por rateio manual auditado'}</td><td>{component.lacunas.join(' · ') || 'Sem lacuna declarada neste componente'}</td></tr>)}</tbody></ScrollTable>}<p className="text-xs text-slate-600">Corte monetário {value.sequenciaAte} · revisão {value.revisao}. A cobertura é por componente; kg, L e un não formam um percentual global.</p></Panel>
}
export function CostPending({ control, complete, setError }: { control: CostCommandControl; complete: (value: unknown) => void; setError: (value: string | null) => void }) {
  if (control.recoveryError) return <Notice error={control.recoveryError} />
  if (!control.pending) return null
  const run = async (kind: 'consult' | 'retry') => { setError(null); try { const value = await control[kind](); if (value) complete(value) } catch (error) { setError(inventoryError(error)) } }
  return <Panel title="Pedido monetário aguardando confirmação"><p role="status">O pedido original, sua identidade e sua chave foram preservados. Consulte o resultado ou repita exatamente os mesmos dados antes de editar.</p><div className="flex flex-wrap gap-3"><button className={buttonClass} type="button" disabled={control.busy} onClick={() => void run('consult')}>Consultar resultado original</button><button className={buttonClass} type="button" disabled={control.busy} onClick={() => void run('retry')}>Repetir pedido original</button></div>{control.absent && <p>{control.releaseAllowed ? 'O POST foi recusado e a consulta não encontrou o resultado. Revise os dados conscientemente antes de um novo envio.' : 'Resultado ainda não encontrado. O pedido pode estar em processamento; preserve a chave e não crie outro comando.'}</p>}{control.releaseAllowed && <button className={buttonClass} type="button" onClick={control.release}>Liberar edição após consulta</button>}</Panel>
}
export function EvidenceEditor({ value, onChange }: { value: EvidenceForm; onChange: (value: EvidenceForm) => void }) {
  const change = (key: keyof EvidenceForm, text: string) => onChange({ ...value, [key]: text })
  return <div className="grid min-w-0 gap-4 sm:grid-cols-2"><Input label="Fonte do comprovante" value={value.fonte} onChange={v => change('fonte', v)} required /><Input label="Referência documental protegida" value={value.referenciaDocumento} onChange={v => change('referenciaDocumento', v)} required hint="Referência opaca para conferência autenticada; o sistema não publica o comprovante." /><Input label="Data da fonte do custo" type="date" value={value.dataFonte} onChange={v => change('dataFonte', v)} required /><Input label="Total documentado dos materiais (BRL)" value={value.totalDocumentado} onChange={v => change('totalDocumentado', v)} required hint="Total atribuível à quantidade, até dois decimais. Zero exige declaração documental; vazio não vira zero." /><Input label="Componentes incluídos (um por linha)" value={value.componentes} onChange={v => change('componentes', v)} multiline required /><Input label="Lacunas documentais (uma por linha)" value={value.lacunas} onChange={v => change('lacunas', v)} multiline hint="Discrimine frete, encargos ou outros componentes sem atribuição comprovada." /><Input label="Hash documental (opcional)" value={value.hash} onChange={v => change('hash', v)} maxLength={128} /></div>
}
export function ReceiptCostEditor({ value, onChange }: { value: ReceiptForm; onChange: (value: ReceiptForm) => void }) {
  return <section className="col-span-full min-w-0 space-y-4 rounded-xl border border-emerald-200 p-4"><h2 className="text-xl font-semibold">Custo dos materiais recebidos</h2><p>Informe o total comprovado atribuível a este recebimento ou mantenha desconhecido com motivo. Cotação, peso e pagamento no Caixa não comprovam o custo desta quantidade.</p><Select label="Conhecimento do custo" value={value.choice} onChange={v => onChange({ ...value, choice: v as ReceiptForm['choice'] })} options={[[ '', 'Escolha explicitamente'], ['Conhecido', 'Conhecido por documento atribuível'], ['Desconhecido', 'Desconhecido — registrar lacuna']]} /><Input label="Motivo monetário do recebimento" value={value.motivo} onChange={v => onChange({ ...value, motivo: v })} multiline required />{value.choice === 'Conhecido' && <EvidenceEditor value={value} onChange={next => onChange({ ...value, ...next })} />}</section>
}
export function AllocationEditor({ value, destinations, onChange, pendingAllowed = true }: { value: AllocationForm; destinations: AllocationDestination[]; onChange: (value: AllocationForm) => void; pendingAllowed?: boolean }) {
  return <section className="space-y-4 rounded-xl border border-emerald-200 p-4"><h2 className="text-xl font-semibold">Apuração dos materiais</h2><Select label="Decisão de rateio dos custos" value={value.choice} onChange={v => onChange({ ...value, choice: v as AllocationForm['choice'] })} options={[[ '', 'Escolha explicitamente'], ['apurar', 'Confirmar percentuais manuais por destino'], ...(pendingAllowed ? [[ 'pendente', 'Registrar pendência monetária consciente'] as const] : [])]} /><Input label={value.choice === 'pendente' ? 'Motivo da pendência de rateio' : 'Motivo e confirmação da política de rateio'} value={value.motivo} onChange={v => onChange({ ...value, motivo: v })} required multiline />{value.choice === 'pendente' && <p className="rounded-lg border border-amber-300 bg-amber-50 p-3">O fato físico será registrado. O conhecido consumido ficará não distribuído na execução, fora do valor em estoque; as saídas continuarão desconhecidas. A resolução presente não atualizará automaticamente material já usado.</p>}{value.choice === 'apurar' && <><p>Informe percentuais por parcela de saída e por perda, somando exatamente 100%. Perda física também deve aparecer com participação explícita, mesmo zero justificado. O servidor conserva os centavos por componente; nenhuma baixa adicional ocorre pela perda.</p><div className="grid min-w-0 gap-4 sm:grid-cols-2">{destinations.map(destination => <Input key={destination.id} label={`Participação (%) — ${destination.label}`} value={destination.zero ? '0' : value.percentages[destination.id] ?? ''} disabled={destination.zero} onChange={v => onChange({ ...value, percentages: { ...value.percentages, [destination.id]: v } })} required={!destination.zero} hint={destination.zero ? 'Saída zero não recebe valor, lote ou custo unitário.' : 'Até seis casas; não se calcula automaticamente por peso ou rendimento.'} />)}</div></>}</section>
}
export function CostPreviewPanel({ value }: { value: CostProductionPreview }) {
  return <div className="space-y-4">
    <p className="font-semibold">Apuração: {value.estado}</p>
    {value.estado === 'PendenteRateio' && <p className="rounded-lg bg-amber-50 p-3">O conhecido fica sem distribuição, fora do valor em estoque. As saídas permanecem desconhecidas.</p>}
    <CostSummaryPanel value={value.custo} title="Componentes reais consumidos" unit={false} />
    {value.destinos.length > 0 && <ScrollTable label="Rateio monetário calculado no servidor">
      <thead><tr>{['Destino', 'Participação', 'Conhecido', 'Total completo', 'Unitário completo'].map(h => <th scope="col" key={h}>{h}</th>)}</tr></thead>
      <tbody>{value.destinos.map((destination, i) => {
        const loss = destination.tipo === 'Perda'
        const zeroOutput = !loss && isZeroQuantity(destination.quantidade)
        const unitCost = destination.custo.custoUnitarioCompleto
        const hasRounding = unitCost !== null && destination.unitarioArredondado
        return <tr key={destination.destinoId}>
          <td>{loss ? 'Perda' : 'Saída'} {i + 1} · {displayDecimal(destination.quantidade)} {destination.unidade}</td>
          <td>{displayDecimal(destination.percentual)}%</td>
          <td>{zeroOutput ? 'Não aplicável' : destination.custo.estado === 'Desconhecido' ? 'Não comprovado' : displayMoney(destination.custo.valorConhecido)}</td>
          <td>{zeroOutput ? 'Não aplicável' : displayMoney(destination.custo.valorTotal)}</td>
          <td>
            {loss || zeroOutput ? 'Não aplicável' : displayDecimal(unitCost)}
            {hasRounding && <span className="block text-xs">
              Arredondado a seis casas. O total em centavos é o valor de referência.
              {destination.residuoNumerador !== null && destination.residuoDenominador !== null && <span className="block break-words">
                Resíduo exato de ({destination.residuoNumerador}/{destination.residuoDenominador}) × 0,000001 BRL/{destination.unidade}.
              </span>}
            </span>}
          </td>
        </tr>
      })}</tbody>
    </ScrollTable>}
  </div>
}
