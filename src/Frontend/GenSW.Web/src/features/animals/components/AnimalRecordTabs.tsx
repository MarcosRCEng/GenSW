import { useEffect, useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'

const tabs = [
  ['ficha', 'Dados gerais'],
  ['producoes-ovos', 'Postura'],
  ['pesagens', 'Pesagens'],
  ['genealogia', 'Genealogia'],
  ['propriedade', 'Propriedade'],
  ['identificacoes', 'Identificações'],
  ['registros', 'Registros'],
  ['imagens', 'Imagens'],
  ['evolucao', 'Evolução'],
] as const

export type AnimalRecordTabId = typeof tabs[number][0]

interface AnimalRecordTabsProps {
  panels: Partial<Record<AnimalRecordTabId, ReactNode>>
  initialSection?: string
  onTabChange?: (tab: AnimalRecordTabId) => void
}

export function AnimalRecordTabs({ panels, initialSection, onTabChange }: AnimalRecordTabsProps) {
  const prefix = useId()
  const availableTabs = tabs.filter(([tab]) => panels[tab] !== undefined)
  const [navigation, setNavigation] = useState(() => {
    const tab = tabs.find(([id]) => id === initialSection)?.[0] ?? 'ficha'
    return { tab, opened: new Set<AnimalRecordTabId>([tab]) }
  })
  const activeTab = panels[navigation.tab] !== undefined ? navigation.tab : 'ficha'
  const restored = useRef(false)
  const tabButtons = useRef<Partial<Record<AnimalRecordTabId, HTMLButtonElement | null>>>({})
  const activePanel = useRef<HTMLDivElement>(null)

  useEffect(() => { onTabChange?.(activeTab) }, [activeTab, onTabChange])

  useEffect(() => {
    if (restored.current || activeTab !== initialSection || !activePanel.current) return
    restored.current = true
    const target = activePanel.current.querySelector<HTMLElement>(`#record-${activeTab}`) ?? activePanel.current
    target.focus({ preventScroll: true })
    target.scrollIntoView?.({ block: 'nearest' })
  }, [activeTab, initialSection])

  function selectTab(tab: AnimalRecordTabId) {
    restored.current = true
    setNavigation(current => ({ tab, opened: new Set([...current.opened, activeTab, tab]) }))
  }

  function onTabKey(event: KeyboardEvent<HTMLButtonElement>, index: number) {
    const count = availableTabs.length
    let nextIndex: number
    switch (event.key) {
      case 'Home': nextIndex = 0; break
      case 'End': nextIndex = count - 1; break
      case 'ArrowLeft': nextIndex = (index + count - 1) % count; break
      case 'ArrowRight': nextIndex = (index + 1) % count; break
      default: return
    }
    event.preventDefault()
    const [nextTab] = availableTabs[nextIndex]
    selectTab(nextTab)
    tabButtons.current[nextTab]?.focus()
  }

  return <div className="min-w-0 space-y-4">
    <div aria-label="Ficha do animal" className="grid grid-cols-3 gap-1 border-b border-slate-200 px-1 py-1 sm:flex sm:flex-wrap" role="tablist">
      {availableTabs.map(([tab, label], index) => <button
        aria-controls={`${prefix}-panel-${tab}`}
        aria-selected={activeTab === tab}
        className="min-h-11 min-w-0 break-words rounded-t-lg border-b-2 border-transparent px-1 text-xs font-semibold text-slate-700 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-emerald-700 aria-selected:border-emerald-700 aria-selected:bg-emerald-50 aria-selected:text-emerald-900 sm:px-3 sm:text-sm"
        id={`${prefix}-tab-${tab}`}
        key={tab}
        onClick={() => selectTab(tab)}
        onKeyDown={event => onTabKey(event, index)}
        ref={element => { tabButtons.current[tab] = element }}
        role="tab"
        tabIndex={activeTab === tab ? 0 : -1}
        type="button"
      >{label}</button>)}
    </div>
    {availableTabs.map(([tab]) => <div
      aria-labelledby={`${prefix}-tab-${tab}`}
      className="min-w-0"
      hidden={activeTab !== tab}
      id={`${prefix}-panel-${tab}`}
      key={tab}
      ref={activeTab === tab ? activePanel : undefined}
      role="tabpanel"
      tabIndex={0}
    >{(activeTab === tab || navigation.opened.has(tab)) && panels[tab]}</div>)}
  </div>
}
