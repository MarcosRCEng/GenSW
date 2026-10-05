import { Link } from 'react-router-dom'
import { useEffect, useState, type FormEvent } from 'react'
import { api, save, message, type Page, type Photo, type PhotoSummary } from './api'
import { AuthenticatedPhoto } from './AuthenticatedPhoto'
import './evolution.css'

function PhotoEditor({ item, root, run }: { item: Photo; root: string; run: (action: () => Promise<unknown>) => void }) {
  const [caption, setCaption] = useState(item.legenda ?? ''), [date, setDate] = useState(item.dataCaptura ?? '')
  return <details><summary>Editar legenda e data</summary><form onSubmit={e => { e.preventDefault(); run(() => save(`${root}/${item.id}/metadados`, { legenda: caption || null, dataCaptura: date || null })) }}>
    <label>Legenda<input maxLength={200} value={caption} onChange={e => setCaption(e.target.value)} /></label><label>Data da captura<input type="date" value={date} max={new Date().toISOString().slice(0, 10)} onChange={e => setDate(e.target.value)} /></label><button type="submit">Salvar metadados</button>
  </form></details>
}

export function ImageGallery({ ownerId, kind, readOnly = false }: { ownerId: string; kind: 'animais' | 'variedades'; readOnly?: boolean }) {
  const root = `/${kind}/${ownerId}/imagens`
  const [items, setItems] = useState<Page<Photo> | null>(null), [revision, setRevision] = useState(0)
  const [active, setActive] = useState(true), [page, setPage] = useState(1)
  const [preferred, setPreferred] = useState<{ origem: string; imagem: PhotoSummary | null } | null>(null)
  const [file, setFile] = useState<File | null>(null), [caption, setCaption] = useState(''), [date, setDate] = useState('')
  const [busy, setBusy] = useState(false), [error, setError] = useState('')
  useEffect(() => {
    const controller = new AbortController(); setItems(null); setError('')
    void Promise.all([api<Page<Photo>>(`${root}?ativo=${active}&page=${page}&pageSize=50`, controller.signal), api<{ origem: string; imagem: PhotoSummary | null }>(`${root}/preferencial`, controller.signal)])
      .then(([list, preference]) => { setItems(list); setPreferred(preference) }).catch(e => { if (!controller.signal.aborted) setError(message(e)) })
    return () => controller.abort()
  }, [root, active, page, revision])
  async function run(action: () => Promise<unknown>) {
    if (readOnly) return
    setBusy(true); setError('')
    try { await action(); setRevision(x => x + 1) } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }
  function upload(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (!file) return
    if (file.size > 5 * 1024 * 1024) { setError('Arquivo excede 5 MiB.'); return }
    const form = e.currentTarget
    const body = new FormData(); body.append('arquivo', file); body.append('legenda', caption); if (date) body.append('dataCaptura', date)
    void run(async () => { await save(root, body, 'POST'); form.reset(); setFile(null); setCaption(''); setDate('') })
  }
  function move(index: number, delta: number) {
    if (!items) return
    const ids = items.items.map(x => x.id); [ids[index], ids[index + delta]] = [ids[index + delta], ids[index]]
    void run(() => save(`${root}/ordem`, { ids }))
  }
  return <section className="animal-panel" aria-label={kind === 'animais' ? 'Imagens do animal' : 'Imagens da variedade'}><h2>{kind === 'animais' ? 'Imagens do animal' : 'Imagens da variedade'}</h2>
    <p>{kind === 'animais' ? 'Aparência deste indivíduo ao longo do tempo.' : 'Catálogo visual da variedade. Estas imagens não representam indivíduos na árvore.'}</p>{!readOnly && <p>JPEG, PNG ou WebP estático · até 5 MiB · 50 imagens ativas.</p>}
    {error && <><p role="alert">{error}</p><button onClick={() => { setError(''); setRevision(x => x + 1) }}>Recarregar galeria</button></>}
    {!readOnly && <fieldset disabled={busy}><form onSubmit={upload} className="animal-fields"><label>Arquivo<input type="file" required accept="image/jpeg,image/png,image/webp" onChange={e => setFile(e.target.files?.[0] ?? null)} /></label><label>Legenda do upload<input maxLength={200} value={caption} onChange={e => setCaption(e.target.value)} /></label><label>Data da captura do upload<input type="date" value={date} max={new Date().toISOString().slice(0, 10)} onChange={e => setDate(e.target.value)} /></label><button type="submit" disabled={!file}>Enviar imagem</button></form></fieldset>}
    <p className="animal-notice">Foto preferencial: {preferred?.origem === 'representativa' ? 'escolha explícita' : preferred?.origem === 'ordenacao' ? 'primeira disponível na ordem' : 'nenhuma disponível'}.</p>
    <label>Exibir imagens<select value={String(active)} onChange={e => { setActive(e.target.value === 'true'); setPage(1) }}><option value="true">Ativas</option><option value="false">Inativas</option></select></label>
    {!items ? !error && <p role="status">Carregando imagens…</p> : <><fieldset disabled={busy} className="animal-gallery">{items.items.map((x, index) => <article key={`${x.id}-${revision}`}>
      <AuthenticatedPhoto path={x.ativa ? x.thumbnailPath : undefined} name={x.legenda ?? 'animal'} /><p>{x.legenda || 'Sem legenda'}</p><p>{x.dataCaptura || 'Data não informada'}</p><p>{x.representativa ? '★ Representativa' : preferred?.imagem?.id === x.id ? 'Preferencial pela ordem' : x.ativa ? 'Ativa' : 'Inativa'}</p>
      <Link className="font-semibold text-emerald-700 underline" to={`/${kind}/${ownerId}/imagens/${x.id}`}>Visualizar</Link>
      {!readOnly && <><PhotoEditor item={x} root={root} run={action => void run(action)} />
      <div className="animal-actions">{x.ativa && <><button onClick={() => void run(() => save(`${root}/${x.id}/representativa`, { representativa: !x.representativa }))}>{x.representativa ? 'Remover preferência' : 'Usar como representativa'}</button><button aria-label={`Mover ${x.legenda || 'imagem'} para antes`} disabled={index === 0} onClick={() => move(index, -1)}>↑ Antes</button><button aria-label={`Mover ${x.legenda || 'imagem'} para depois`} disabled={index === items.items.length - 1} onClick={() => move(index, 1)}>↓ Depois</button></>}<button onClick={() => void run(() => save(`${root}/${x.id}/ativo`, { ativo: !x.ativa }))}>{x.ativa ? 'Inativar imagem' : 'Reativar imagem'}</button></div></>}
    </article>)}</fieldset>{!items.items.length && <p>Nenhuma imagem {active ? 'ativa' : 'inativa'}.</p>}<div className="animal-actions"><button disabled={page === 1} onClick={() => setPage(x => x - 1)}>Imagens anteriores</button><span>Página {page} de {Math.max(1, items.totalPages)}</span><button disabled={page >= items.totalPages} onClick={() => setPage(x => x + 1)}>Próximas imagens</button></div></>}
  </section>
}
