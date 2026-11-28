import { useEffect, useRef, useState } from 'react'
import { httpRequest } from '../../../shared/http/httpClient'
import { subscribeToSessionGenerationChange } from '../../../shared/http/sessionManager'

let running = 0
const queue: (() => void)[] = []
async function download(path: string, signal: AbortSignal) {
  signal.throwIfAborted()
  await new Promise<void>((resolve, reject) => {
    const start = () => {
      signal.removeEventListener('abort', cancel)
      running++
      resolve()
    }
    const cancel = () => {
      const index = queue.indexOf(start)
      if (index >= 0) queue.splice(index, 1)
      reject(signal.reason)
    }
    if (running < 4) {
      start()
    } else {
      queue.push(start)
      signal.addEventListener('abort', cancel, { once: true })
    }
  })
  try {
    signal.throwIfAborted()
    return await httpRequest<Blob>(path.replace(/^\/api\/v1/, ''), { authenticated: true, responseType: 'blob', signal })
  } finally { running--; queue.shift()?.() }
}

export function AuthenticatedPhoto({ path, name, presentation = 'avatar' }: { path?: string; name: string; presentation?: 'avatar' | 'full' }) {
  const host = useRef<HTMLSpanElement>(null)
  const [photo, setPhoto] = useState<{ path: string; url: string } | null>(null)
  const url = photo && photo.path === path ? photo.url : null
  useEffect(() => {
    const controller = new AbortController()
    let objectUrl: string | null = null
    let started = false
    setPhoto(null)
    const start = () => {
      if (!path || started) return
      started = true
      void download(path, controller.signal).then(blob => {
        if (controller.signal.aborted) return
        objectUrl = URL.createObjectURL(blob); setPhoto({ path, url: objectUrl })
      }).catch(() => { /* A single failure shows the neutral placeholder. */ })
    }
    const observer = typeof IntersectionObserver === 'undefined' ? null : new IntersectionObserver(entries => {
      if (entries.some(x => x.isIntersecting)) { start(); observer?.disconnect() }
    })
    const dispose = () => {
      observer?.disconnect()
      controller.abort()
      if (objectUrl) URL.revokeObjectURL(objectUrl)
      objectUrl = null
    }
    const unsubscribe = subscribeToSessionGenerationChange(() => {
      dispose()
      setPhoto(null)
    })
    if (observer && host.current) observer.observe(host.current); else start()
    return () => { unsubscribe(); dispose() }
  }, [path])
  return <span ref={host} className={presentation === 'full' ? 'animal-photo-full' : 'animal-photo'}>{url ? <img src={url} alt={`Foto de ${name}`} onError={() => { URL.revokeObjectURL(url); setPhoto(null) }} /> : <span role="img" aria-label={`${name}: sem foto disponível`}>◇</span>}</span>
}
