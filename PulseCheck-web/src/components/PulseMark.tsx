export function PulseMark({ className = '' }: { className?: string }) {
  return (
    <div className={`pulse-mark ${className}`}>
      <img src="/favicon.ico" alt="PulseCheck" className="pulse-mark__icon" />
      <span className="pulse-mark__ring pulse-mark__ring--one" />
      <span className="pulse-mark__ring pulse-mark__ring--two" />
    </div>
  )
}
