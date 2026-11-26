import { useEffect, useState, type ReactNode } from 'react'
import { RecordTabs } from '../../../shared/details/RecordTabs'

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
  const [selected, setSelected] = useState(() => tabs.find(([id]) => id === initialSection)?.[0] ?? 'ficha')
  const activeTab = panels[selected] !== undefined ? selected : 'ficha'

  useEffect(() => { onTabChange?.(activeTab) }, [activeTab, onTabChange])

  return <RecordTabs activeTab={activeTab} label="Ficha do animal" onSelect={setSelected}
    panels={panels} restoreFocusSection={initialSection} tabs={tabs} />
}
