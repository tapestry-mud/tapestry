import { OutputViewport } from '../panels/OutputViewport'
import { CommandBar } from '../controls/CommandBar'
import { TopBar } from './TopBar'

export function LoginLayout() {
  return (
    <div className="flex flex-col h-screen bg-surface text-text-primary">
      <TopBar />
      <div className="flex flex-1 overflow-hidden justify-center">
        <div className="flex flex-col w-full max-w-3xl">
          <OutputViewport />
          <CommandBar />
        </div>
      </div>
    </div>
  )
}
