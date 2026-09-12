import type { ReactNode } from 'react'
import { createPortal } from 'react-dom'
import Button from '@mui/material/Button'
import type { ButtonProps } from '@mui/material/Button'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Paper from '@mui/material/Paper'
import Typography from '@mui/material/Typography'
import CloseOutlinedIcon from '@mui/icons-material/CloseOutlined'

export const inputClassName = 'input-field'

const baseButtonSx = {
  borderRadius: '999px',
  fontWeight: 800,
  textTransform: 'none',
  minHeight: 40,
  transition: 'transform 160ms ease, box-shadow 160ms ease, background-color 160ms ease, border-color 160ms ease',
  '@media (prefers-reduced-motion: reduce)': {
    transition: 'none',
  },
}

function compactButtonSx(size: ButtonProps['size']) {
  return size === 'small'
    ? {
        minHeight: 34,
        px: 2,
        py: 0.55,
        fontSize: 13,
      }
    : {}
}

export function PrimaryButton(props: ButtonProps) {
  return (
    <Button
      variant="contained"
      {...props}
      sx={{
        ...baseButtonSx,
        background: 'linear-gradient(135deg, #008aab, #28c4d8)',
        boxShadow: '0 18px 32px rgba(0, 138, 171, 0.22)',
        color: '#ffffff',
        px: 3,
        ...compactButtonSx(props.size),
        '&:hover': {
          background: 'linear-gradient(135deg, #007894, #16b6cc)',
          boxShadow: '0 18px 32px rgba(0, 138, 171, 0.28)',
          transform: 'translateY(-2px)',
          '@media (prefers-reduced-motion: reduce)': {
            transform: 'none',
          },
        },
        '&:active': {
          transform: 'translateY(0) scale(0.99)',
          boxShadow: '0 10px 20px rgba(0, 138, 171, 0.2)',
        },
        '&.Mui-disabled': {
          color: 'rgba(255,255,255,0.72)',
          background: 'linear-gradient(135deg, #7bb9c7, #8bd7e2)',
          transform: 'none',
          boxShadow: 'none',
        },
        ...props.sx,
      }}
    />
  )
}

export function SoftButton(props: ButtonProps) {
  return (
    <Button
      variant="outlined"
      {...props}
      sx={{
        ...baseButtonSx,
        borderColor: '#bfd5dd',
        backgroundColor: '#ffffff',
        color: '#2f4d5b',
        px: 2.5,
        ...compactButtonSx(props.size),
        '&:hover': {
          borderColor: '#8fb7c5',
          backgroundColor: '#f6fbfd',
          boxShadow: '0 10px 22px rgba(9,55,69,0.08)',
          transform: 'translateY(-2px)',
          '@media (prefers-reduced-motion: reduce)': {
            transform: 'none',
          },
        },
        '&:active': {
          transform: 'translateY(0) scale(0.99)',
          boxShadow: '0 6px 14px rgba(9,55,69,0.06)',
        },
        '&.Mui-disabled': {
          transform: 'none',
          boxShadow: 'none',
        },
        ...props.sx,
      }}
    />
  )
}

export function Stat({
  label,
  value,
  context,
  trend,
}: {
  label: string
  value: string
  context?: string
  trend?: 'up' | 'down' | 'warning' | 'neutral'
}) {
  return (
    <article className="metric-card">
      <p className="text-xs uppercase tracking-[0.2em] text-[#5f7782]">{label}</p>
      <p className="mt-2 font-display text-3xl font-bold text-[#0d3140]">{value}</p>
      {context ? <p className={`metric-card__context metric-card__context--${trend ?? 'up'}`}>{context}</p> : null}
    </article>
  )
}

export function Card({ title, children }: { title: string; children: ReactNode }) {
  return (
    <article data-animate className="glass-panel p-5">
      <h2 className="section-title">{title}</h2>
      <div className="mt-3 grid gap-2">{children}</div>
    </article>
  )
}

export function Row({ left, right }: { left: string; right: string }) {
  return (
    <div className="flex items-center justify-between rounded-xl border border-[#d7e8ee] bg-[#f8fcfd] px-3 py-2 text-sm">
      <span className="text-[#1a3d4b]">{left}</span>
      <span className="text-[#5f7782]">{right}</span>
    </div>
  )
}

export function TabPill({ active, label, onClick }: { active: boolean; label: string; onClick: () => void }) {
  return (
    <Button
      type="button"
      onClick={onClick}
      variant={active ? 'contained' : 'outlined'}
      sx={{
        ...baseButtonSx,
        borderColor: active ? '#008aab' : '#cadde4',
        backgroundColor: active ? '#ecf7fa' : '#ffffff',
        color: active ? '#0d3a49' : '#506a75',
        boxShadow: 'none',
        '&:hover': {
          borderColor: '#008aab',
          backgroundColor: active ? '#e0f3f7' : '#f6fbfd',
          boxShadow: 'none',
        },
      }}
    >
      {label}
    </Button>
  )
}

export function ModePill({ active, label, onClick }: { active: boolean; label: string; onClick: () => void }) {
  return (
    <Button
      type="button"
      onClick={onClick}
      variant="outlined"
      sx={{
        borderRadius: '16px',
        borderColor: active ? '#008aab' : '#cadde4',
        backgroundColor: active ? '#ecf7fa' : '#ffffff',
        color: active ? '#0d3a49' : '#506a75',
        fontWeight: 700,
        justifyContent: 'flex-start',
        textAlign: 'left',
        textTransform: 'none',
        px: 1.5,
        py: 1,
        '&:hover': {
          borderColor: '#008aab',
          backgroundColor: active ? '#e0f3f7' : '#f6fbfd',
        },
      }}
    >
      {label}
    </Button>
  )
}

export function SmallBtn({ label, onClick }: { label: string; onClick: () => void }) {
  return <SoftButton type="button" onClick={onClick} size="small">{label}</SoftButton>
}

export function StatusChip({
  tone = 'neutral',
  children,
}: {
  tone?: 'active' | 'inactive' | 'warning' | 'neutral'
  children: ReactNode
}) {
  const className = tone === 'active'
    ? 'border-[#9bd3db] bg-[#eefbfc] text-[#00758d]'
    : tone === 'inactive'
      ? 'border-[#d9dfe3] bg-[#f3f5f6] text-[#657985]'
      : tone === 'warning'
        ? 'border-[#f5c48b] bg-[#fff8ed] text-[#9a5a00]'
        : 'border-[#c9e1e8] bg-[#f7fbfd] text-[#416371]'

  return (
    <span className={`inline-flex max-w-full items-center rounded-full border px-2.5 py-1 text-xs font-semibold leading-tight ${className}`}>
      <span className="truncate">{children}</span>
    </span>
  )
}

export function DetailRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 rounded-xl border border-[#d7e8ee] bg-[#f8fcfd] px-3 py-2 text-sm">
      <span className="shrink-0 font-semibold text-[#1a3d4b]">{label}</span>
      <span className="min-w-0 text-right text-[#5f7782]">{value}</span>
    </div>
  )
}

export function ModalShell({
  eyebrow,
  title,
  children,
  onClose,
  maxWidthClassName = 'max-w-lg',
  zIndexClassName = 'z-[9999]',
}: {
  eyebrow?: string
  title: string
  children: ReactNode
  onClose: () => void
  maxWidthClassName?: string
  zIndexClassName?: string
}) {
  return createPortal(
    <div className={`admin-modal-overlay ${zIndexClassName}`} style={{ zIndex: 9999 }}>
      <div className={`admin-modal-shell w-full ${maxWidthClassName} rounded-2xl border border-[#cfe2e9] bg-white p-5 shadow-2xl`}>
        <div className="flex items-start justify-between gap-3">
          <div>
            {eyebrow ? <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[#0082a0]">{eyebrow}</p> : null}
            <h3 className="mt-1 text-lg font-bold text-[#0d3140]">{title}</h3>
          </div>
          <IconButton size="small" onClick={onClose} aria-label="Cerrar">
            <CloseOutlinedIcon fontSize="small" />
          </IconButton>
        </div>
        {children}
      </div>
    </div>,
    document.body,
  )
}

export function MetricCard({
  icon,
  title,
  value,
  detail,
  onClick,
}: {
  icon?: ReactNode
  title: string
  value: string
  detail?: string
  onClick?: () => void
}) {
  return (
    <Paper
      data-animate
      elevation={0}
      component={onClick ? 'button' : 'article'}
      type={onClick ? 'button' : undefined}
      onClick={onClick}
      className={`rounded-[20px] border border-[#cfe2e9] bg-white p-5 text-left shadow-[0_16px_34px_rgba(13,49,64,0.07)] ${onClick ? 'transition hover:-translate-y-0.5 hover:border-[#8fc7d4] hover:shadow-[0_18px_38px_rgba(13,49,64,0.1)]' : ''}`}
    >
      <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 2 }}>
        {icon ? <Box sx={{ display: 'grid', placeItems: 'center', width: 44, height: 44, borderRadius: 3, bgcolor: '#e9fbfd', color: '#00758d' }}>{icon}</Box> : null}
        <Box sx={{ minWidth: 0 }}>
          <Typography className="text-xs font-black uppercase tracking-[0.18em] text-[#00758d]">{title}</Typography>
          <Typography className="mt-3 line-clamp-2 text-2xl font-black text-[#0d3140]">{value}</Typography>
          {detail ? <Typography className="mt-2 text-sm leading-6 text-[#5f7782]">{detail}</Typography> : null}
        </Box>
      </Box>
    </Paper>
  )
}
