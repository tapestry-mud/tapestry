import { useState } from 'react'
import { Panel, Group as PanelGroup, Separator as PanelResizeHandle } from 'react-resizable-panels'
import {
  DndContext, DragOverlay, PointerSensor, useSensor, useSensors,
  type DragEndEvent, type DragStartEvent,
} from '@dnd-kit/core'
import { SortableContext, arrayMove, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { TopBar } from './TopBar'
import { SortablePanel } from './SortablePanel'
import { useLayoutStore } from '../stores/layoutStore'
import { useIsMobile } from '../hooks/useIsMobile'
import { useLandmarkCycling } from '../hooks/useLandmarkCycling'
import { Announcer } from '../accessibility/Announcer'
import { SkipLinks } from '../accessibility/SkipLinks'
import { CharacterPanel }   from '../panels/CharacterPanel'
import { StatsPanel }       from '../panels/StatsPanel'
import { VitalsPanel }      from '../panels/VitalsPanel'
import { EffectsPanel }     from '../panels/EffectsPanel'
import { CombatTargetPanel } from '../panels/CombatTargetPanel'
import { XPPanel }          from '../panels/XPPanel'
import { RoomViewPanel }    from '../panels/RoomViewPanel'
import { OutputViewport }   from '../panels/OutputViewport'
import { MapPanel }         from '../panels/MapPanel'
import { TimeWeatherPanel } from '../panels/TimeWeatherPanel'
import { EquipmentPanel }   from '../panels/EquipmentPanel'
import { InventoryPanel }   from '../panels/InventoryPanel'
import { NearbyPanel }      from '../panels/NearbyPanel'
import { ChatPanel }        from '../panels/ChatPanel'
import { CommandsDrawer }   from '../panels/CommandsDrawer'
import { CommandBar }       from '../controls/CommandBar'
import { Hotbar }           from '../controls/Hotbar'
import { DebugDrawer }      from '../debug/DebugDrawer'
import { SettingsModal }    from '../controls/SettingsModal'

const PANEL_COMPONENTS: Record<string, React.ComponentType> = {
  CharacterPanel,
  StatsPanel,
  VitalsPanel,
  EffectsPanel,
  CombatTargetPanel,
  XPPanel,
  MapPanel,
  TimeWeatherPanel,
  EquipmentPanel,
  InventoryPanel,
  NearbyPanel,
  ChatPanel,
}

export function GameLayout() {
  const { panels, setPanelColumn, setPanelOrder } = useLayoutStore()
  const [activeId, setActiveId] = useState<string | null>(null)
  const isMobile = useIsMobile()
  useLandmarkCycling()

  const leftPanels  = panels.filter((p) => p.column === 'left').sort((a, b) => a.order - b.order)
  const rightPanels = panels.filter((p) => p.column === 'right').sort((a, b) => a.order - b.order)

  const sensors = useSensors(useSensor(PointerSensor, {
    activationConstraint: { distance: 5 },
  }))

  const handleDragStart = ({ active }: DragStartEvent) => {
    setActiveId(active.id as string)
  }

  const handleDragEnd = ({ active, over }: DragEndEvent) => {
    setActiveId(null)
    if (!over || active.id === over.id) { return }

    const activePanel = panels.find((p) => p.id === active.id)
    const overPanel   = panels.find((p) => p.id === over.id)
    if (!activePanel || !overPanel) { return }

    if (activePanel.column !== overPanel.column) {
      setPanelColumn(activePanel.id, overPanel.column)
    } else {
      const columnPanels = panels
        .filter((p) => p.column === activePanel.column)
        .sort((a, b) => a.order - b.order)
      const oldIndex = columnPanels.findIndex((p) => p.id === active.id)
      const newIndex = columnPanels.findIndex((p) => p.id === over.id)
      const reordered = arrayMove(columnPanels, oldIndex, newIndex)
      reordered.forEach((p, i) => { setPanelOrder(p.id, i) })
    }
  }

  const ActiveComponent = activeId ? PANEL_COMPONENTS[activeId] : null

  const allPanelIds = [...leftPanels, ...rightPanels].map((p) => p.id)

  return (
    <div className="flex flex-col h-screen w-screen overflow-hidden bg-surface-deep">
      <SkipLinks />
      <Announcer />
      <TopBar />

      <main className="flex-1 overflow-hidden">
        {isMobile ? (
          <div className="flex flex-col h-full overflow-hidden">
            <OutputViewport />

            <div className="sr-only" role="complementary" aria-label="Game panels">
              {allPanelIds.map((id) => {
                const Component = PANEL_COMPONENTS[id]
                if (!Component) { return null }
                return <Component key={id} />
              })}
            </div>
          </div>
        ) : (
          <DndContext sensors={sensors} onDragStart={handleDragStart} onDragEnd={handleDragEnd}>
            <PanelGroup orientation="horizontal" className="h-full">

              <Panel defaultSize="25%" minSize="15%" maxSize="40%"
                className="flex flex-col overflow-y-auto border-r border-border gap-1 p-1">
                <div id="left-panels" tabIndex={-1} role="complementary" aria-label="Left game panels" className="flex flex-col gap-1 outline-none">
                <SortableContext items={leftPanels.map((p) => p.id)} strategy={verticalListSortingStrategy}>
                  {leftPanels.map((p) => {
                    const Component = PANEL_COMPONENTS[p.id]
                    if (!Component) { return null }
                    return (
                      <SortablePanel key={p.id} id={p.id}>
                        <Component />
                      </SortablePanel>
                    )
                  })}
                </SortableContext>
                </div>
              </Panel>

              <PanelResizeHandle className="w-1 bg-border hover:bg-accent cursor-col-resize transition-colors" />

              <Panel defaultSize="50%" minSize="30%" className="flex flex-col overflow-hidden">
                <RoomViewPanel />
                <OutputViewport />
                <Hotbar />
              </Panel>

              <PanelResizeHandle className="w-1 bg-border hover:bg-accent cursor-col-resize transition-colors" />

              <Panel defaultSize="25%" minSize="15%" maxSize="40%"
                className="flex flex-col overflow-y-auto border-l border-border gap-1 p-1">
                <div id="right-panels" tabIndex={-1} role="complementary" aria-label="Right game panels" className="flex flex-col gap-1 outline-none">
                <SortableContext items={rightPanels.map((p) => p.id)} strategy={verticalListSortingStrategy}>
                  {rightPanels.map((p) => {
                    const Component = PANEL_COMPONENTS[p.id]
                    if (!Component) { return null }
                    return (
                      <SortablePanel key={p.id} id={p.id}>
                        <Component />
                      </SortablePanel>
                    )
                  })}
                </SortableContext>
                </div>
              </Panel>

            </PanelGroup>

            <DragOverlay>
              {ActiveComponent && (
                <div className="opacity-80 shadow-lg">
                  <ActiveComponent />
                </div>
              )}
            </DragOverlay>
          </DndContext>
        )}
      </main>

      <CommandBar />
      <CommandsDrawer />
      <DebugDrawer />
      <SettingsModal />
    </div>
  )
}
