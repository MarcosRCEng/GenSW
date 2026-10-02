import { useEffect, useRef, useState } from 'react'
import { api, message, type Tree, type TreeNode, type Relations } from './api'
import { AuthenticatedPhoto } from './AuthenticatedPhoto'

function layout(tree: Tree) {
  const levels = new Map<string, number>([[tree.raizId, 0]])
  for (let pass = 0; pass < tree.nos.length; pass++) {
    let changed = false
    for (const edge of tree.arestas) {
      const parent = levels.get(edge.progenitorId), child = levels.get(edge.descendenteId)
      if (parent === undefined && child !== undefined) { levels.set(edge.progenitorId, child - 1); changed = true }
      if (child === undefined && parent !== undefined) { levels.set(edge.descendenteId, parent + 1); changed = true }
    }
    if (!changed) break
  }
  const groups = new Map<number, TreeNode[]>()
  for (const node of tree.nos) { const level = levels.get(node.animalId) ?? 0; groups.set(level, [...(groups.get(level) ?? []), node]) }
  const min = Math.min(...groups.keys()), max = Math.max(...groups.keys())
  const width = Math.max(620, ...Array.from(groups.values(), x => x.length * 205 + 40))
  const positions = new Map<string, { x: number; y: number }>()
  for (const [level, nodes] of groups) nodes.forEach((node, index) => positions.set(node.animalId, { x: (width - nodes.length * 205) / 2 + index * 205 + 12, y: (level - min) * 250 + 30 }))
  return { width, height: (max - min + 1) * 250 + 40, positions }
}

export function AnimalTree({ animalId }: { animalId: string }) {
  const [root, setRoot] = useState(animalId), [history, setHistory] = useState<string[]>([])
  const [tree, setTree] = useState<Tree | null>(null), [selected, setSelected] = useState(animalId)
  const [error, setError] = useState(''), [busy, setBusy] = useState(false), [revision, setRevision] = useState(0), [zoom, setZoom] = useState(1)
  const [pages, setPages] = useState<Record<string, { next: number; more: boolean }>>({})
  const request = useRef<AbortController | null>(null), viewport = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const controller = new AbortController(); request.current?.abort(); request.current = controller
    setTree(null); setPages({}); setSelected(root); setBusy(true); setError('')
    void api<Tree>(`/animais/${root}/arvore`, controller.signal).then(setTree).catch(e => { if (!controller.signal.aborted) setError(message(e)) }).finally(() => { if (!controller.signal.aborted) setBusy(false) })
    return () => { controller.abort(); request.current?.abort() }
  }, [root, revision])
  function center(id: string) { request.current?.abort(); setHistory(x => [...x.slice(-199), root]); setRoot(id) }
  async function expand(direction: 'ascendentes' | 'descendentes') {
    if (!tree) return
    const key = `${selected}-${direction}`, page = pages[key]?.next ?? 1
    const controller = new AbortController(); request.current?.abort(); request.current = controller; setBusy(true); setError('')
    try {
      const result = await api<Relations>(`/animais/${selected}/arvore/relacoes?direcao=${direction}&page=${page}&pageSize=20`, controller.signal)
      if (controller.signal.aborted) return
      const nodes = new Map(tree.nos.map(x => [x.animalId, x])), edges = new Map(tree.arestas.map(x => [x.filiacaoId, x]))
      result.nos.forEach(x => nodes.set(x.animalId, x)); result.arestas.forEach(x => edges.set(x.filiacaoId, x))
      if (nodes.size > 200 || edges.size > 400) { setError('Limite da tela atingido. Recentre no animal selecionado para continuar.'); return }
      setTree({ ...tree, nos: [...nodes.values()], arestas: [...edges.values()], avisos: [...new Set([...tree.avisos, ...result.avisos])] })
      setPages(x => ({ ...x, [key]: { next: page + 1, more: page < result.totalPages } }))
    } catch (e) { if (!controller.signal.aborted) setError(message(e)) } finally { if (!controller.signal.aborted) setBusy(false) }
  }
  const drawing = tree ? layout(tree) : null, focal = tree?.nos.find(x => x.animalId === selected)
  useEffect(() => {
    if (!tree || !viewport.current) return
    const position = layout(tree).positions.get(root)
    const element = viewport.current
    function align() {
      if (!position) return
      element.scrollLeft = Math.max(0, position.x * zoom - element.clientWidth / 2 + 90 * zoom)
      element.scrollTop = Math.max(0, (position.y + 95) * zoom - element.clientHeight / 2)
    }
    align()
    const observer = new ResizeObserver(align)
    observer.observe(element)
    return () => observer.disconnect()
  }, [root, tree, zoom])
  return <div className="animal-tree"><h3 className="mt-5 text-lg font-bold">Árvore genealógica</h3><p>Ascendentes acima · animal central · descendentes abaixo. Selecione um cartão para explorar.</p>
    <div className="animal-actions"><button disabled={busy || !history.length} onClick={() => { request.current?.abort(); setRoot(history[history.length - 1]); setHistory(x => x.slice(0, -1)) }}>Voltar na árvore</button><button disabled={busy} onClick={() => setRevision(x => x + 1)}>Atualizar árvore</button><label>Zoom<select value={zoom} onChange={e => setZoom(Number(e.target.value))}><option value="0.6">60%</option><option value="0.8">80%</option><option value="1">100%</option><option value="1.2">120%</option></select></label></div>
    {error && <p role="alert">{error}</p>}{busy && <p role="status">Carregando árvore…</p>}
    {tree && drawing && <>{tree.truncada && <p className="animal-notice">Recorte limitado. Expanda uma relação ou recentre para continuar.</p>}{tree.avisos.length > 0 && <p className="animal-notice">Atenção: relações legadas inconsistentes ou cíclicas neste recorte. Revisão manual necessária.</p>}
      <div ref={viewport} className="genealogy-viewport" tabIndex={0} role="region" aria-label="Diagrama genealógico com rolagem"><div style={{ width: drawing.width * zoom, height: drawing.height * zoom }}><div className="genealogy-canvas" style={{ width: drawing.width, height: drawing.height, transform: `scale(${zoom})` }}>
        <svg className="genealogy-connectors" width={drawing.width} height={drawing.height} aria-hidden="true">{tree.arestas.map(edge => {
          const p = drawing.positions.get(edge.progenitorId)!, c = drawing.positions.get(edge.descendenteId)!, middle = (p.y + 190 + c.y) / 2
          return <g key={edge.filiacaoId}><path d={`M ${p.x + 90} ${p.y + 190} V ${middle} H ${c.x + 90} V ${c.y}`} /><text x={(p.x + c.x) / 2 + 94} y={middle - 5}>{edge.tipoFiliacao === 1 ? 'Pai' : 'Mãe'}</text></g>
        })}</svg>
        {tree.nos.map(node => { const p = drawing.positions.get(node.animalId)!; const repeated = tree.arestas.filter(x => x.progenitorId === node.animalId).length > 1
          return <article className="genealogy-card" key={node.animalId} data-root={node.animalId === root} style={{ left: p.x, top: p.y, height: 190 }}><button aria-pressed={selected === node.animalId} aria-label={`Selecionar ${node.nome || node.codigoInterno}`} onClick={() => setSelected(node.animalId)}><AuthenticatedPhoto path={node.imagem?.thumbnailPath} name={node.nome || node.codigoInterno} /><strong>{node.nome || node.codigoInterno}</strong><small>{node.codigoInterno} · {node.sexo === 1 ? 'Macho' : node.sexo === 2 ? 'Fêmea' : 'Sexo indefinido'}</small><small>{node.ativo ? 'Ativo' : 'Inativo · histórico'}{repeated ? ' · Compartilhado' : ''}</small></button>{!node.paiConhecido && <small>Pai não informado</small>}{!node.maeConhecida && <small>Mãe não informada</small>}</article>
        })}
      </div></div></div>
      {focal && <div className="animal-notice"><strong>Selecionado: {focal.nome || focal.codigoInterno}</strong><div className="animal-actions"><button disabled={busy || selected === root} onClick={() => center(selected)}>Recentrar neste animal</button>{(['ascendentes', 'descendentes'] as const).map(direction => <button key={direction} disabled={busy || pages[`${selected}-${direction}`]?.more === false || (pages[`${selected}-${direction}`] === undefined && !(direction === 'ascendentes' ? focal.temAscendentesAdicionais : focal.temDescendentesAdicionais))} onClick={() => void expand(direction)}>Expandir {direction}{pages[`${selected}-${direction}`]?.more ? ' · próxima página' : ''}</button>)}</div></div>}
      <details><summary>Relações em texto acessível ({tree.nos.length} animais)</summary><ul>{tree.nos.map(node => <li key={node.animalId}><button onClick={() => setSelected(node.animalId)}>{node.codigoInterno} — {node.nome || 'Nome não informado'}</button>{!node.ativo && ' (inativo)'}<ul>{tree.arestas.filter(x => x.descendenteId === node.animalId).map(edge => <li key={edge.filiacaoId}>{edge.tipoFiliacao === 1 ? 'Pai' : 'Mãe'}: {tree.nos.find(x => x.animalId === edge.progenitorId)?.codigoInterno}</li>)}{!node.paiConhecido && <li>Pai não informado</li>}{!node.maeConhecida && <li>Mãe não informada</li>}</ul></li>)}</ul></details>
    </>}
  </div>
}
