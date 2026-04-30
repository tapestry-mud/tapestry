import { render, screen } from '@testing-library/react'
import { act } from 'react'
import { CharacterPanel } from './CharacterPanel'
import { useCharStore } from '../stores/charStore'
import { useSettingsStore } from '../stores/settingsStore'

vi.mock('../terminal/terminalStore')

beforeEach(() => {
  useCharStore.setState({
    name: 'MALLEK', race: 'Human', class: 'Warrior', level: 12,
    hp: 340, maxHp: 340, mana: 120, maxMana: 150, mv: 200, maxMv: 200,
    str: 0, int: 0, wis: 0, dex: 0, con: 0, luk: 0,
    alignment: 0, alignmentBucket: '',
    gold: 0, hungerTier: '', hungerValue: 100,
    updateVitals: useCharStore.getState().updateVitals,
    updateStatus: useCharStore.getState().updateStatus,
  })
  useSettingsStore.setState({ theme: 'dark', characterName: null, settingsOpen: false })
  document.documentElement.classList.remove('theme-light', 'theme-midnight', 'theme-amber')
  localStorage.clear()
})

it('shows character name', () => {
  render(<CharacterPanel />)
  expect(screen.getByText('MALLEK')).toBeTruthy()
})

it('shows class', () => {
  render(<CharacterPanel />)
  expect(screen.getByText(/Warrior/)).toBeTruthy()
})

it('shows level', () => {
  render(<CharacterPanel />)
  expect(screen.getByText(/12/)).toBeTruthy()
})

it('shows fallback when name is empty', () => {
  useCharStore.setState({
    name: '', race: '', class: '', level: 0,
    hp: 0, maxHp: 0, mana: 0, maxMana: 0, mv: 0, maxMv: 0,
    updateVitals: useCharStore.getState().updateVitals,
    updateStatus: useCharStore.getState().updateStatus,
  })
  render(<CharacterPanel />)
  expect(screen.getByText('Unknown')).toBeTruthy()
})

it('calls setCharacter when name transitions from empty to populated', () => {
  useCharStore.setState({ name: '', race: '', class: '', level: 0, hp: 0, maxHp: 0, mana: 0, maxMana: 0, mv: 0, maxMv: 0, updateVitals: useCharStore.getState().updateVitals, updateStatus: useCharStore.getState().updateStatus })
  localStorage.setItem('tapestry:settings:Kael', 'midnight')

  render(<CharacterPanel />)

  act(() => {
    useCharStore.setState({ name: 'Kael' })
  })

  expect(useSettingsStore.getState().characterName).toBe('Kael')
  expect(useSettingsStore.getState().theme).toBe('midnight')
  expect(document.documentElement.classList.contains('theme-midnight')).toBe(true)
})

it('does not call setCharacter when name remains empty', () => {
  useCharStore.setState({ name: '', race: '', class: '', level: 0, hp: 0, maxHp: 0, mana: 0, maxMana: 0, mv: 0, maxMv: 0, updateVitals: useCharStore.getState().updateVitals, updateStatus: useCharStore.getState().updateStatus })
  render(<CharacterPanel />)

  act(() => {
    useCharStore.setState({ name: '' })
  })

  expect(useSettingsStore.getState().characterName).toBeNull()
})
