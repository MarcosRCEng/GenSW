import { display } from '../formulation/api'
import { Section } from '../formulation/FormControls'
import { TableRegion } from '../../shared/layout/TableRegion'
import type { OptimizationInputs } from './types'

const cell = 'min-w-32 px-3 py-2 align-top'

export function CoefficientMatrix({ inputs }: { inputs: OptimizationInputs }) {
  return <Section title="Coeficientes e fontes">
    {inputs.diagnosticos.length > 0 && <ul className="list-disc space-y-2 rounded-lg bg-amber-50 p-4 pl-8 text-sm" aria-label="Diagnósticos dos dados">
      {inputs.diagnosticos.map((d, index) => {
        const item = inputs.candidatos.find((c) => c.id === d.candidatoId)
        const goal = inputs.metas.find((g) => g.id === d.metaId)
        return <li key={`${d.codigo}:${d.candidatoId}:${d.metaId}:${index}`}>
          {[item?.nome, goal?.componente, d.mensagem].filter(Boolean).join(' · ')} <span className="text-slate-600">({d.codigo})</span>
        </li>
      })}
    </ul>}
    {inputs.candidatos.length > 0 && <TableRegion label="Matriz de ingredientes e metas">
      <table className="w-full text-left text-sm">
        <caption className="sr-only">Matriz de ingredientes e metas</caption>
        <thead className="bg-slate-50"><tr><th scope="col" className={cell}>Ingrediente</th>{inputs.metas.map((g) => <th key={g.id} scope="col" className={cell}>{g.componente} · {g.metodo}</th>)}</tr></thead>
        <tbody className="divide-y divide-slate-200">{inputs.candidatos.map((c) => <tr key={c.id}>
          <th scope="row" className={cell}>
            {c.nome}
            {c.excluido && <p className="font-normal">Excluído no pedido</p>}
            {c.perfil && <p className="font-normal">{c.perfil.conteudo.nome} · v{c.perfil.numero}</p>}
          </th>
          {inputs.metas.map((g) => {
            const coefficient = c.coeficientes.find((v) => v.metaId === g.id)
            return <td key={g.id} className={cell}>{c.excluido ? 'Fora da matriz' : !coefficient ? 'Coeficiente indisponível' : <>
              <p>{coefficient.valorNormalizado == null ? 'Indeterminado' : `${display(coefficient.valorNormalizado)} ${coefficient.unidade ?? ''}`}</p>
              <p>{[coefficient.estado, coefficient.origem, coefficient.codigo].filter(Boolean).join(' · ')}</p>
              {coefficient.fonte && <p className="break-words">Fonte: {coefficient.fonte}</p>}
              {coefficient.hipotese && <p className="break-words">Hipótese: {coefficient.hipotese}</p>}
              {(coefficient.observacao || coefficient.materiaSeca || coefficient.valorExato) && <details className="mt-1">
                <summary className="cursor-pointer underline">Observação e derivação</summary>
                {coefficient.valorExato && <p>Coeficiente exato {coefficient.valorExato} {coefficient.unidade}</p>}
                {coefficient.fracaoSeca && <p>Fração de matéria seca {coefficient.fracaoSeca}</p>}
                {[coefficient.observacao, coefficient.materiaSeca].map((v, i) => v && <p className="break-words" key={i}>
                  {v.componente} · {display(v.valor)} {v.unidade} {v.base} · {v.metodo} · {v.contexto || 'Contexto não informado'} · {[v.origem, v.qualificador, v.fonte, v.hipotese, v.motivo].filter(Boolean).join(' · ')}
                </p>)}
              </details>}
            </>}</td>
          })}
        </tr>)}</tbody>
      </table>
    </TableRegion>}
  </Section>
}
