export function WheelMark({ size = 72 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 100 100" aria-hidden="true" className="wheel-mark">
      <g className="ring-out">
        <circle cx="50" cy="50" r="46" fill="none" stroke="#8b6e35" strokeWidth="0.7" strokeDasharray="2 6" />
      </g>
      <g className="ring-in">
        <circle cx="50" cy="50" r="38" fill="none" stroke="#2a3a5e" strokeWidth="0.6" />
      </g>
      <g className="spokes" strokeWidth="1.1" strokeLinecap="round">
        <line x1="50" y1="50" x2="50" y2="10" stroke="oklch(0.78 0.13 85)" opacity="0.85" />
        <line x1="50" y1="50" x2="82" y2="30" stroke="oklch(0.6 0.18 25)" opacity="0.85" />
        <line x1="50" y1="50" x2="70" y2="89" stroke="oklch(0.62 0.13 250)" opacity="0.85" />
        <line x1="50" y1="50" x2="14" y2="60" stroke="oklch(0.65 0.14 145)" opacity="0.85" />
        <line x1="50" y1="50" x2="14" y2="40" stroke="oklch(0.5 0.08 60)" opacity="0.85" />
        <line x1="50" y1="50" x2="70" y2="11" stroke="#e8e4d6" opacity="0.85" />
        <line x1="50" y1="50" x2="82" y2="70" stroke="#8a8d94" opacity="0.85" />
      </g>
      <circle cx="50" cy="50" r="10" fill="#0a1226" stroke="#8b6e35" strokeWidth="0.8" />
      <circle cx="50" cy="50" r="2.4" fill="#f0c872" />
    </svg>
  )
}
