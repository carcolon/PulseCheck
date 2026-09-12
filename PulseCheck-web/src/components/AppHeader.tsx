import {
  Alert,
  Box,
  Button,
  Chip,
  Paper,
  Typography,
} from '@mui/material'
import AddCircleOutlineIcon from '@mui/icons-material/AddCircleOutlineOutlined'
import RefreshOutlinedIcon from '@mui/icons-material/RefreshOutlined'
import type { DashboardOverview, Tab } from '../types'

type AppHeaderProps = {
  overview: DashboardOverview
  isRefreshing: boolean
  subtitle?: string
  onRefresh: () => void
  onNewCampaign?: () => void
  onOpenSettings?: () => void
}

export function AppHeader({
  overview,
  isRefreshing,
  subtitle,
  onRefresh,
  onNewCampaign,
}: AppHeaderProps) {
  return (
    <>
      <Paper data-animate elevation={0} className="dashboard-hero dashboard-hero--mui">
        <Box className="dashboard-hero__inner">
          <Box className="dashboard-hero__copy">
            <Typography
              variant="h3"
              sx={{
                maxWidth: 620,
                color: '#0d3140',
                fontSize: { xs: '1.8rem', md: '2.35rem' },
                lineHeight: 1.08,
              }}
            >
              Consola operativa
            </Typography>
            <Typography variant="body1" sx={{ mt: 1, maxWidth: 680, color: '#49636f', lineHeight: 1.65 }}>
              Pulso, respuestas y campañas listas para seguimiento.
            </Typography>
            {subtitle ? (
              <Typography variant="caption" sx={{ mt: 1.25, display: 'block', color: 'primary.main', fontWeight: 900, letterSpacing: '0.08em', textTransform: 'uppercase' }}>
                {subtitle}
              </Typography>
            ) : null}
          </Box>

          <Box className="dashboard-hero__compact-actions">
            {onNewCampaign ? (
            <Button variant="contained" color="secondary" startIcon={<AddCircleOutlineIcon />} onClick={onNewCampaign}>
              Nueva campaña
            </Button>
            ) : null}
            <Button variant="outlined" startIcon={<RefreshOutlinedIcon />} onClick={onRefresh}>
              {isRefreshing ? 'Refrescando...' : 'Refrescar'}
            </Button>
          </Box>
        </Box>
      </Paper>

      {overview.alerts.length > 0 ? (
        <section data-animate className="mt-4">
          <div className="dashboard-alerts dashboard-alerts--inline">
            {overview.alerts.map((alert) => (
              <article key={alert.title} className={`alert-card alert-card--${alert.tone}`}>
                <p className="alert-card__eyebrow">{alert.eyebrow}</p>
                <p className="alert-card__title">{alert.title}</p>
                <p className="alert-card__text">{alert.text}</p>
              </article>
            ))}
          </div>
        </section>
      ) : null}
    </>
  )
}

export function TabIntro({ title, description }: { title: string; description: string }) {
  return (
    <Paper data-animate elevation={0} sx={{ border: '1px solid #d5e6ec', borderRadius: 3, px: 2, py: 1.75 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 900, color: '#12394a' }}>
        {title}
      </Typography>
      <Typography variant="body2" sx={{ mt: 0.5, color: '#5f7883' }}>
        {description}
      </Typography>
    </Paper>
  )
}

export function ErrorBanner({ message }: { message: string | null }) {
  if (!message) return null
  return (
    <Alert data-animate severity="error" sx={{ borderRadius: 3 }}>
      {message}
    </Alert>
  )
}

export function TabNav({
  tab,
  connectionState,
  onChange,
}: {
  tab: Tab
  connectionState: string
  onChange: (tab: Tab) => void
}) {
  const tabs: Array<{ id: Tab; label: string }> = [
    { id: 'overview', label: 'Resumen' },
    { id: 'campaigns', label: 'Campañas' },
    { id: 'agents', label: 'Agentes' },
    { id: 'responses', label: 'Insights' },
    { id: 'admins', label: 'Admins' },
    { id: 'settings', label: 'Config' },
  ]

  return (
    <Paper data-animate elevation={0} sx={{ border: '1px solid #d4e5eb', borderRadius: 3, p: 1.5 }}>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
        {tabs.map((item) => (
          <Button
            key={item.id}
            type="button"
            onClick={() => onChange(item.id)}
            variant={tab === item.id ? 'contained' : 'outlined'}
          >
            {item.label}
          </Button>
        ))}
        <Chip sx={{ ml: 'auto' }} label={connectionState} />
      </Box>
    </Paper>
  )
}
