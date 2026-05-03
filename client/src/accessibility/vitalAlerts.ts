import { announce } from './announceStore'

interface VitalSnapshot {
  hp: number
  maxHp: number
  mana: number
  maxMana: number
  mv: number
  maxMv: number
}

const LOW_THRESHOLD = 0.4
const CRITICAL_THRESHOLD = 0.1

interface VitalAlertState {
  low: boolean
  critical: boolean
}

const state: Record<string, VitalAlertState> = {
  health: { low: false, critical: false },
  mana: { low: false, critical: false },
  movement: { low: false, critical: false },
}

function pct(current: number, max: number): number {
  if (max <= 0) { return 1 }
  return current / max
}

function checkVital(name: string, ratio: number) {
  const s = state[name]

  if (ratio > LOW_THRESHOLD) {
    if (s.low || s.critical) {
      s.low = false
      s.critical = false
    }
    return
  }

  if (ratio <= CRITICAL_THRESHOLD && !s.critical) {
    announce(`Warning: ${name} critical at ${Math.round(ratio * 100)} percent`, 'vitals', 'assertive')
    s.critical = true
    s.low = true
    return
  }

  if (ratio <= LOW_THRESHOLD && !s.low) {
    announce(`${name} low at ${Math.round(ratio * 100)} percent`, 'vitals', 'assertive')
    s.low = true
  }
}

export function checkVitalAlerts(vitals: VitalSnapshot) {
  checkVital('health', pct(vitals.hp, vitals.maxHp))
  checkVital('mana', pct(vitals.mana, vitals.maxMana))
  checkVital('movement', pct(vitals.mv, vitals.maxMv))
}
