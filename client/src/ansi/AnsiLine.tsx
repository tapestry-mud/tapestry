import type { AnsiToken, AnsiStyles } from '../types/game'

function tokenClass(styles: AnsiStyles): string {
  const classes: string[] = []
  if (styles.fg) { classes.push(styles.fg) }
  if (styles.bold) { classes.push('font-bold') }
  return classes.join(' ')
}

interface AnsiLineProps {
  tokens: AnsiToken[]
}

export function AnsiLine({ tokens }: AnsiLineProps) {
  return (
    <span>
      {tokens.map((token, i) => (
        <span key={i} className={tokenClass(token.styles)}>
          {token.text}
        </span>
      ))}
    </span>
  )
}
