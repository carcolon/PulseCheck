import { motion, useReducedMotion } from 'framer-motion'
import type { Transition } from 'framer-motion'

const lineTransition: Transition = {
  duration: 7,
  repeat: Infinity,
  ease: 'easeInOut',
}

export function DashboardAtmosphere() {
  const prefersReducedMotion = useReducedMotion()
  const animatedProps = prefersReducedMotion
    ? {}
    : {
        animate: { opacity: [0.18, 0.42, 0.18], scaleX: [0.96, 1.02, 0.96] },
        transition: lineTransition,
      }

  return (
    <div className="dashboard-atmosphere" aria-hidden="true">
      <motion.span className="dashboard-atmosphere__trace dashboard-atmosphere__trace--top" {...animatedProps} />
      <motion.span
        className="dashboard-atmosphere__trace dashboard-atmosphere__trace--middle"
        {...(prefersReducedMotion
          ? {}
          : {
              animate: { opacity: [0.1, 0.32, 0.1], scaleX: [1, 0.94, 1] },
              transition: { ...lineTransition, delay: 1.2 },
            })}
      />
      <motion.span
        className="dashboard-atmosphere__trace dashboard-atmosphere__trace--vertical"
        {...(prefersReducedMotion
          ? {}
          : {
              animate: { opacity: [0.12, 0.34, 0.12], scaleY: [0.92, 1.06, 0.92] },
              transition: { ...lineTransition, delay: 0.6 },
            })}
      />
      <motion.span
        className="dashboard-atmosphere__pulse"
        {...(prefersReducedMotion
          ? {}
          : {
              animate: { x: ['-8%', '108%'], opacity: [0, 0.45, 0] },
              transition: { duration: 9, repeat: Infinity, ease: 'easeInOut', delay: 0.8 },
            })}
      />
    </div>
  )
}
