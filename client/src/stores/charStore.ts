import { create } from 'zustand'
import type { CharVitals, CharStatus } from '../types/gmcp'

interface CharState {
  name: string; race: string; class: string; level: number
  hp: number; maxHp: number; mana: number; maxMana: number; mv: number; maxMv: number
  str: number; int: number; wis: number; dex: number; con: number; luk: number
  alignment: number; alignmentBucket: string
  gold: number
  hungerTier: string; hungerValue: number
  isAdmin: boolean
  updateVitals: (data: CharVitals) => void
  updateStatus: (data: CharStatus) => void
}

export const useCharStore = create<CharState>()((set) => ({
  name: '', race: '', class: '', level: 0,
  hp: 0, maxHp: 0, mana: 0, maxMana: 0, mv: 0, maxMv: 0,
  str: 0, int: 0, wis: 0, dex: 0, con: 0, luk: 0,
  alignment: 0, alignmentBucket: '',
  gold: 0,
  hungerTier: '', hungerValue: 100,
  isAdmin: false,
  updateVitals: (data) =>
    set({
      hp: Math.max(0, Math.min(data.hp, data.maxhp)),
      maxHp: data.maxhp,
      mana: Math.max(0, Math.min(data.mana, data.maxmana)),
      maxMana: data.maxmana,
      mv: Math.max(0, Math.min(data.mv, data.maxmv)),
      maxMv: data.maxmv,
    }),
  updateStatus: (data) =>
    set({
      name: data.name,
      race: data.race,
      class: data.class,
      level: data.level,
      str: data.str ?? 0,
      int: data.int ?? 0,
      wis: data.wis ?? 0,
      dex: data.dex ?? 0,
      con: data.con ?? 0,
      luk: data.luk ?? 0,
      alignment: data.alignment ?? 0,
      alignmentBucket: data.alignmentBucket ?? '',
      gold: data.gold ?? 0,
      hungerTier: data.hungerTier ?? '',
      hungerValue: data.hungerValue ?? 100,
      isAdmin: data.isAdmin ?? false,
    }),
}))
