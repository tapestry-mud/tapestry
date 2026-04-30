import { beforeEach, describe, it, expect } from 'vitest'
import { useCharStore } from './charStore'

const blank = {
  name: '', race: '', class: '', level: 0,
  hp: 0, maxHp: 0, mana: 0, maxMana: 0, mv: 0, maxMv: 0,
  str: 0, int: 0, wis: 0, dex: 0, con: 0, luk: 0,
  alignment: 0, alignmentBucket: '',
  gold: 0, hungerTier: '', hungerValue: 100,
}

beforeEach(() => { useCharStore.setState(blank) })

describe('charStore', () => {
  it('updateVitals maps GMCP fields to store', () => {
    useCharStore.getState().updateVitals({ hp: 340, maxhp: 340, mana: 120, maxmana: 150, mv: 200, maxmv: 200 })
    const s = useCharStore.getState()
    expect(s.hp).toBe(340)
    expect(s.maxHp).toBe(340)
    expect(s.mana).toBe(120)
    expect(s.mv).toBe(200)
  })

  it('clamps hp below 0 to 0', () => {
    useCharStore.getState().updateVitals({ hp: -10, maxhp: 100, mana: 0, maxmana: 100, mv: 0, maxmv: 100 })
    expect(useCharStore.getState().hp).toBe(0)
  })

  it('clamps hp above maxHp to maxHp', () => {
    useCharStore.getState().updateVitals({ hp: 999, maxhp: 100, mana: 0, maxmana: 100, mv: 0, maxmv: 100 })
    expect(useCharStore.getState().hp).toBe(100)
  })

  it('updateStatus sets name/race/class/level', () => {
    useCharStore.getState().updateStatus({ name: 'Raegar', race: 'Human', class: 'Warrior', level: 10 })
    const s = useCharStore.getState()
    expect(s.name).toBe('Raegar')
    expect(s.level).toBe(10)
  })

  it('updateStatus maps attributes from expanded Char.Status', () => {
    useCharStore.getState().updateStatus({
      name: 'Raegar', race: 'Human', class: 'Warrior', level: 10,
      str: 15, int: 12, wis: 10, dex: 14, con: 13, luk: 9,
      alignment: 250, alignmentBucket: 'good',
      gold: 500, hungerTier: 'full', hungerValue: 80,
    })
    const s = useCharStore.getState()
    expect(s.str).toBe(15)
    expect(s.int).toBe(12)
    expect(s.wis).toBe(10)
    expect(s.dex).toBe(14)
    expect(s.con).toBe(13)
    expect(s.luk).toBe(9)
    expect(s.alignment).toBe(250)
    expect(s.alignmentBucket).toBe('good')
    expect(s.gold).toBe(500)
    expect(s.hungerTier).toBe('full')
    expect(s.hungerValue).toBe(80)
  })

  it('updateStatus defaults optional fields to zero when absent', () => {
    useCharStore.getState().updateStatus({ name: 'Raegar', race: 'Human', class: 'Warrior', level: 10 })
    const s = useCharStore.getState()
    expect(s.str).toBe(0)
    expect(s.gold).toBe(0)
    expect(s.alignmentBucket).toBe('')
    expect(s.hungerValue).toBe(100)
  })
})
