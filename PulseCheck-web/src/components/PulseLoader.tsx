import gsap from 'gsap'
import { useEffect, useRef } from 'react'
import { PulseMark } from './PulseMark'

export function PulseLoader({
  title = 'Cargando',
  caption = 'Preparando la experiencia PulseCheck',
  fullScreen = true,
}: {
  title?: string
  caption?: string
  fullScreen?: boolean
}) {
  const rootRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!rootRef.current) return

    const context = gsap.context(() => {
      gsap.set('.pulse-loader__wave', { scale: 0.65, opacity: 0 })

      gsap.timeline({ repeat: -1, repeatDelay: 0.12 })
        .to('.pulse-loader__heart', { scale: 1.12, duration: 0.16, ease: 'power2.out' })
        .to('.pulse-loader__heart', { scale: 0.98, duration: 0.1, ease: 'power2.in' })
        .to('.pulse-loader__heart', { scale: 1.18, duration: 0.14, ease: 'power2.out' })
        .to('.pulse-loader__heart', { scale: 1, duration: 0.28, ease: 'power3.out' })
        .to('.pulse-loader__heart', { scale: 1, duration: 0.44, ease: 'none' })

      gsap.to('.pulse-loader__heart img', {
        rotate: 2,
        duration: 0.35,
        ease: 'sine.inOut',
        repeat: -1,
        yoyo: true,
      })

      gsap.to('.pulse-loader__glow', {
        scale: 1.2,
        opacity: 0.82,
        duration: 0.48,
        ease: 'sine.inOut',
        repeat: -1,
        yoyo: true,
      })

      gsap.to('.pulse-loader__shadow', {
        scaleX: 0.78,
        opacity: 0.12,
        duration: 0.48,
        ease: 'sine.inOut',
        repeat: -1,
        yoyo: true,
      })

      for (const [index, selector] of ['.pulse-loader__wave--one', '.pulse-loader__wave--two'].entries()) {
        gsap.timeline({ repeat: -1, repeatDelay: 0.2, delay: index * 0.32 })
          .to(selector, { scale: 0.92, opacity: 0.28, duration: 0.08, ease: 'power1.out' })
          .to(selector, { scale: 1.42, opacity: 0, duration: 0.82, ease: 'power2.out' })
      }

      gsap.to('.pulse-loader__dots span', {
        y: -3,
        opacity: 0.45,
        stagger: 0.15,
        duration: 0.35,
        repeat: -1,
        yoyo: true,
        ease: 'sine.inOut',
      })
    }, rootRef)

    return () => context.revert()
  }, [])

  return (
    <div
      ref={rootRef}
      className={`pulse-loader ${fullScreen ? 'pulse-loader--fullscreen' : 'pulse-loader--inline'}`}
    >
      <div className="pulse-loader__scene">
        <div className="pulse-loader__wave pulse-loader__wave--one" />
        <div className="pulse-loader__wave pulse-loader__wave--two" />
        <div className="pulse-loader__glow" />
        <div className="pulse-loader__shadow" />
        <div className="pulse-loader__heart">
          <PulseMark className="h-30 w-30 md:h-34 md:w-34" />
        </div>
      </div>
      <div className="pulse-loader__copy">
        <p className="pulse-loader__title">{title}</p>
        <p className="pulse-loader__caption">
          {caption}
          <span className="pulse-loader__dots"><span>.</span><span>.</span><span>.</span></span>
        </p>
      </div>
    </div>
  )
}
