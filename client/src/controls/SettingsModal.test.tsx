import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { useSettingsStore } from '../stores/settingsStore'
import { SettingsModal } from './SettingsModal'

vi.mock('../terminal/terminalStore')

beforeEach(() => {
  document.documentElement.classList.remove('theme-light', 'theme-midnight', 'theme-amber')
  useSettingsStore.setState({ theme: 'dark', characterName: null, settingsOpen: true })
})

describe('SettingsModal', () => {
  it('renders four theme cards when open', () => {
    render(<SettingsModal />)
    expect(screen.getByText('Dark')).toBeInTheDocument()
    expect(screen.getByText('Light')).toBeInTheDocument()
    expect(screen.getByText('Midnight')).toBeInTheDocument()
    expect(screen.getByText('Amber')).toBeInTheDocument()
  })

  it('renders nothing when settingsOpen is false', () => {
    useSettingsStore.setState({ settingsOpen: false })
    render(<SettingsModal />)
    expect(screen.queryByText('Settings')).not.toBeInTheDocument()
  })

  it('calls setTheme and updates store when a card is clicked', () => {
    render(<SettingsModal />)
    fireEvent.click(screen.getByText('Light'))
    expect(useSettingsStore.getState().theme).toBe('light')
  })

  it('closes when the X button is clicked', () => {
    render(<SettingsModal />)
    fireEvent.click(screen.getByTitle('Close settings'))
    expect(useSettingsStore.getState().settingsOpen).toBe(false)
  })

  it('closes when the backdrop is clicked', () => {
    render(<SettingsModal />)
    fireEvent.click(screen.getByTestId('settings-backdrop'))
    expect(useSettingsStore.getState().settingsOpen).toBe(false)
  })
})
