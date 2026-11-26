import { useEffect, useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'

interface RecordTabsProps<T extends string> {
  tabs: readonly (readonly [T, string])[]
  label: string
  panels: Partial<Record<T, ReactNode>>
  activeTab: T
  onSelect: (tab: T) => void
  alwaysMountedTab?: T
  restoreFocusSection?: string
}

export function RecordTabs<T extends string>({ tabs, label, panels, activeTab, onSelect, alwaysMountedTab, restoreFocusSection }: RecordTabsProps<T>) {
  const prefix = useId()
  const availableTabs = tabs.filter(([tab]) => panels[tab] !== undefined)
  const [opened, setOpened] = useState(() => new Set([activeTab]))
  const restored = useRef(false)
  const tabButtons = useRef<Partial<Record<T, HTMLButtonElement | null>>>({})
  const activePanel = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (restored.current || activeTab !== restoreFocusSection || !activePanel.current) return
    restored.current = true
    const target = activePanel.current.querySelector<HTMLElement>(`#record-${activeTab}`) ?? activePanel.current
    target.focus({ preventScroll: true })
    target.scrollIntoView?.({ block: 'nearest' })
  }, [activeTab, restoreFocusSection])

  function selectTab(tab: T) {
    restored.current = true
    setOpened(current => current.has(activeTab) && current.has(tab) ? current : new Set([...current, activeTab, tab]))
    onSelect(tab)
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
    <div aria-label={label} className={`grid ${availableTabs.length === 2 ? 'grid-cols-2' : 'grid-cols-3'} gap-1 border-b border-slate-200 px-1 py-1 sm:flex sm:flex-wrap`} role="tablist">
      {availableTabs.map(([tab, tabLabel], index) => <button
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
      >{tabLabel}</button>)}
    </div>
    {availableTabs.map(([tab]) => <div
      aria-labelledby={`${prefix}-tab-${tab}`}
      className="min-w-0"
      hidden={activeTab !== tab}
      id={`${prefix}-panel-${tab}`}
      key={tab}
      ref={activeTab === tab ? activePanel : undefined}
      role="tabpanel"
      tabIndex={activeTab === tab ? 0 : -1}
    >{(activeTab === tab || alwaysMountedTab === tab || opened.has(tab)) && panels[tab]}</div>)}
  </div>
}
