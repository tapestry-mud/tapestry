import { WheelMark } from './WheelMark'
import './preauth.css'

export function LandingPage({ onEnter }: { onEnter: () => void }) {
  return (
    <main className="preauth">
      <div className="pa-landing-hero">
        <img src="/assets/tapestry-logo.png" alt="Tapestry" className="pa-login-logo" />
        <WheelMark size={120} />
        <div className="pa-fili" aria-hidden="true">&diams; &loz; &diams;</div>

        <h1 className="pa-landing-title">
          TAPE<span className="slash">/</span>STRY
        </h1>
        <p className="pa-landing-tagline">A Wheel-Turned MUD</p>

        <section aria-label="World lore">
          <p className="pa-landing-lore">
            What was, will be again. The Great Wheel turns, and the Pattern remembers
            every thread ever woven into its design. When you step into this world,
            you are not merely playing a game -- you are adding your thread to an
            ancient tapestry.
          </p>
        </section>

        <section aria-label="Features" className="pa-pillars">
          <div className="pa-pillar">
            <span className="pa-pillar-icon" aria-hidden="true">&#x25C8;</span>
            <h2>A World That Remembers</h2>
            <p>
              Every action ripples through the Pattern. The world evolves around you,
              shaped by the choices of all who walk within it.
            </p>
          </div>
          <div className="pa-pillar">
            <span className="pa-pillar-icon" aria-hidden="true">&#x25C6;</span>
            <h2>Seven Threads, One Cloth</h2>
            <p>
              Seven great threads weave through all things. Find your thread,
              learn its resonance, and discover your place in the Pattern.
            </p>
          </div>
          <div className="pa-pillar">
            <span className="pa-pillar-icon" aria-hidden="true">&#x25C7;</span>
            <h2>A Client of This Age</h2>
            <p>
              A modern interface for an ancient art. Rich panels, keyboard shortcuts,
              and accessibility built from the ground up.
            </p>
          </div>
        </section>

        <button className="pa-enter-btn" type="button" onClick={onEnter}>
          Enter the Pattern &rarr;
        </button>

        <div className="pa-below">
          <span>
            <span className="pa-conn-led" aria-hidden="true" />
            {' '}Server online
          </span>
          <span className="dot" aria-hidden="true">&middot;</span>
          <a href="https://discord.gg/tapestry" target="_blank" rel="noopener noreferrer">Discord</a>
          <span className="dot" aria-hidden="true">&middot;</span>
          <a href="https://github.com/mallek/tapestry-public" target="_blank" rel="noopener noreferrer">GitHub</a>
        </div>
      </div>
    </main>
  )
}
