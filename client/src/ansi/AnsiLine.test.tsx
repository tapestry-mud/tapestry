import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { AnsiLine } from './AnsiLine'

describe('AnsiLine', () => {
  it('renders plain text token', () => {
    render(<AnsiLine tokens={[{ text: 'hello', styles: {} }]} />)
    expect(screen.getByText('hello')).toBeDefined()
  })

  it('applies fg color class', () => {
    const { container } = render(
      <AnsiLine tokens={[{ text: 'red text', styles: { fg: 'text-ansi-red' } }]} />
    )
    expect(container.querySelector('.text-ansi-red')).not.toBeNull()
  })

  it('applies font-bold class for bold', () => {
    const { container } = render(
      <AnsiLine tokens={[{ text: 'bold', styles: { bold: true } }]} />
    )
    expect(container.querySelector('.font-bold')).not.toBeNull()
  })

  it('applies both fg and bold when both set', () => {
    const { container } = render(
      <AnsiLine tokens={[{ text: 'bold red', styles: { fg: 'text-ansi-red', bold: true } }]} />
    )
    const span = container.querySelector('span span')
    expect(span?.className).toContain('text-ansi-red')
    expect(span?.className).toContain('font-bold')
  })

  it('renders multiple tokens as separate spans', () => {
    render(<AnsiLine tokens={[
      { text: 'red', styles: { fg: 'text-ansi-red' } },
      { text: ' plain', styles: {} },
    ]} />)
    expect(screen.getByText('red')).toBeDefined()
    expect(screen.getByText(' plain', { normalizer: (s) => s })).toBeDefined()
  })

  it('HTML in token text is escaped, not injected', () => {
    const { container } = render(
      <AnsiLine tokens={[{ text: '<script>evil</script>', styles: {} }]} />
    )
    expect(container.innerHTML).toContain('&lt;script&gt;')
    expect(container.querySelector('script')).toBeNull()
  })

  it('renders empty token array as empty span', () => {
    const { container } = render(<AnsiLine tokens={[]} />)
    expect(container.querySelector('span')?.textContent).toBe('')
  })
})
