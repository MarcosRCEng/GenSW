import type { ReactNode } from 'react'
import { RecordTabs } from '../../../shared/details/RecordTabs'

const tabs = [['ficha', 'Dados gerais'], ['imagens', 'Imagens']] as const

export type VarietyRecordTab = typeof tabs[number][0]

interface VarietyRecordTabsProps {
  activeTab: VarietyRecordTab
  onSelect: (tab: VarietyRecordTab) => void
  general: ReactNode
  images: ReactNode
}

export function VarietyRecordTabs({ activeTab, onSelect, general, images }: VarietyRecordTabsProps) {
  return <RecordTabs activeTab={activeTab} alwaysMountedTab="ficha" label="Ficha da variedade" onSelect={onSelect}
    panels={{ ficha: general, imagens: images }} tabs={tabs} />
}
