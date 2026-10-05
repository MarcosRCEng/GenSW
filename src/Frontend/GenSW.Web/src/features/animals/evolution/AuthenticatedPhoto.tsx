import { useEffect, useRef, useState } from 'react'
import { httpRequest } from '../../../shared/http/httpClient'
import { subscribeToSessionGenerationChange } from '../../../shared/http/sessionManager'

let running = 0
const queue: (() => void)[] = []
async function download(path: string, signal: AbortSignal) {
  await new Promise<void>(resolve => {
    const start = () => { running++; resolve() }
    if (running < 4) start(); else queue.push(start)
  })
  try {
    signal.throwIfAborted()
    return await httpRequest<Blob>(path.replace(/^\/api\/v1/, ''), { authenticated: true, responseType: 'blob', signal })
  } finally { running--; queue.shift()?.() }
}

export function AuthenticatedPhoto({ path, name, presentation = 'avatar' }: { path?: string; name: string; presentation?: 'avatar' | 'full' }) {
  const host = useRef<HTMLSpanElement>(null)
  const [url, setUrl] = useState<string | null>(null)
  useEffect(() => {
    const controller = new AbortController()
    let objectUrl: string | null = null
    let started = false
    setUrl(null)
    const clear = () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); objectUrl = null; setUrl(null) }
    const unsubscribe = subscribeToSessionGenerationChange(clear)
    const start = () => {
      if (!path || started) return
      started = true
      void download(path, controller.signal).then(blob => {
        if (controller.signal.aborted) return
        objectUrl = URL.createObjectURL(blob); setUrl(objectUrl)
      }).catch(() => { /* A single failure shows the neutral placeholder. */ })
    }
    const observer = typeof IntersectionObserver === 'undefined' ? null : new IntersectionObserver(entries => {
      if (entries.some(x => x.isIntersecting)) { start(); observer?.disconnect() }
    })
    if (observer && host.current) observer.observe(host.current); else start()
    return () => { observer?.disconnect(); unsubscribe(); controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl) }
  }, [path])
  return <span ref={host} className={presentation === 'full' ? 'animal-photo-full' : 'animal-photo'}>{url ? <img src={url} alt={`Foto de ${name}`} onError={() => { URL.revokeObjectURL(url); setUrl(null) }} /> : <span role="img" aria-label={`${name}: sem foto disponível`}>◇</span>}</span>
}
