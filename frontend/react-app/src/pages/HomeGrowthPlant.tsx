import { useEffect, useRef } from 'react'

function phase(progress: number, start: number, end: number) {
  return Math.min(1, Math.max(0, (progress - start) / (end - start)))
}

function setGrowthProgress(element: HTMLElement, progress: number) {
  element.style.setProperty('--root-growth', String(phase(progress, 0, 0.24)))
  element.style.setProperty('--stem-growth', String(phase(progress, 0.08, 0.66)))
  element.style.setProperty('--leaf-one-growth', String(phase(progress, 0.25, 0.5)))
  element.style.setProperty('--leaf-two-growth', String(phase(progress, 0.42, 0.68)))
  element.style.setProperty('--crown-growth', String(phase(progress, 0.68, 0.94)))
}

export function HomeGrowthPlant() {
  const railRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const rail = railRef.current
    const journey = rail?.parentElement

    if (!rail || !journey) return

    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    if (reduceMotion) {
      setGrowthProgress(rail, 1)
      return
    }

    let animationFrame = 0

    const update = () => {
      animationFrame = 0
      const bounds = journey.getBoundingClientRect()
      const viewportHeight = window.innerHeight
      const start = viewportHeight * 0.78
      const distance = Math.max(bounds.height - viewportHeight * 0.35, 1)
      const progress = Math.min(1, Math.max(0, (start - bounds.top) / distance))
      setGrowthProgress(rail, progress)
    }

    const requestUpdate = () => {
      if (animationFrame) return
      animationFrame = window.requestAnimationFrame(update)
    }

    update()
    window.addEventListener('scroll', requestUpdate, { passive: true })
    window.addEventListener('resize', requestUpdate)

    return () => {
      window.removeEventListener('scroll', requestUpdate)
      window.removeEventListener('resize', requestUpdate)
      if (animationFrame) window.cancelAnimationFrame(animationFrame)
    }
  }, [])

  return (
    <div ref={railRef} className="home-growth-rail" aria-hidden="true">
      <div className="home-growth-sticky">
        <span className="home-growth-label">Cultivation journey</span>
        <svg className="home-growth-plant" viewBox="0 0 160 520" focusable="false">
          <path className="home-plant-soil" d="M20 458 C58 448 106 448 140 458" />
          <ellipse className="home-plant-seed" cx="80" cy="449" rx="12" ry="7" />
          <g className="home-plant-roots">
            <path pathLength="1" d="M80 449 C72 466 58 478 49 493" />
            <path pathLength="1" d="M80 449 C85 469 102 478 112 495" />
            <path pathLength="1" d="M78 455 C77 476 77 490 78 506" />
          </g>
          <path className="home-plant-stem" pathLength="1" d="M80 449 C72 385 89 322 78 258 C69 205 84 142 80 82" />
          <g className="home-plant-leaf home-plant-leaf-one-left">
            <path d="M77 352 C45 344 30 316 33 282 C68 284 91 309 77 352 Z" />
            <path d="M76 349 C61 326 49 308 36 290" />
          </g>
          <g className="home-plant-leaf home-plant-leaf-one-right">
            <path d="M79 319 C105 304 132 308 145 330 C122 351 96 350 79 319 Z" />
            <path d="M83 319 C106 324 123 328 139 331" />
          </g>
          <g className="home-plant-leaf home-plant-leaf-two-left">
            <path d="M78 245 C50 235 36 210 40 181 C71 185 90 209 78 245 Z" />
            <path d="M77 242 C64 220 53 202 43 188" />
          </g>
          <g className="home-plant-leaf home-plant-leaf-two-right">
            <path d="M80 205 C104 186 129 188 145 207 C126 232 100 234 80 205 Z" />
            <path d="M84 204 C104 207 123 208 140 208" />
          </g>
          <g className="home-plant-crown">
            <path d="M80 93 C64 72 67 45 80 23 C94 45 97 72 80 93 Z" />
            <ellipse cx="63" cy="77" rx="8" ry="18" transform="rotate(-28 63 77)" />
            <ellipse cx="97" cy="77" rx="8" ry="18" transform="rotate(28 97 77)" />
            <circle cx="80" cy="45" r="6" />
          </g>
        </svg>
        <div className="home-growth-stage-labels">
          <span>Seed</span>
          <span>Sprout</span>
          <span>Thrive</span>
        </div>
      </div>
    </div>
  )
}
