import { useEffect, useRef } from 'react'

export function useHomeScrollReveal(readyClass: string, revealedClass: string) {
  const pageRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const page = pageRef.current
    const reduceMotion = typeof window.matchMedia === 'function'
      && window.matchMedia('(prefers-reduced-motion: reduce)').matches

    if (!page || reduceMotion || !('IntersectionObserver' in window)) return

    const targets = page.querySelectorAll<HTMLElement>('[data-home-reveal]')
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (!entry.isIntersecting) continue

          entry.target.classList.add(revealedClass)
          observer.unobserve(entry.target)
        }
      },
      { rootMargin: '0px 0px -8% 0px', threshold: 0.12 },
    )

    page.classList.add(readyClass)
    targets.forEach((target) => observer.observe(target))

    return () => {
      observer.disconnect()
      page.classList.remove(readyClass)
    }
  }, [readyClass, revealedClass])

  return pageRef
}
