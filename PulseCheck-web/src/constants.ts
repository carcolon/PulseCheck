import type { DashboardOverview, Tab } from './types'

export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL
const configuredAgentDownloadUrl = import.meta.env.VITE_AGENT_DOWNLOAD_URL?.trim() ?? ''
const isLocalHost = typeof window !== 'undefined' && ['localhost', '127.0.0.1'].includes(window.location.hostname)
export const agentDownloadUrl = configuredAgentDownloadUrl || (isLocalHost ? '/downloads/PulseCheck.Agent.Setup.exe' : '')
export const isAgentDownloadConfigured = agentDownloadUrl.length > 0
export const csrfHeaderName = 'X-PulseCheck-CSRF'
export const entraTenantId = import.meta.env.VITE_ENTRA_TENANT_ID?.trim() ?? ''
export const entraClientId = import.meta.env.VITE_ENTRA_CLIENT_ID?.trim() ?? ''
export const entraApiClientId = import.meta.env.VITE_ENTRA_API_CLIENT_ID?.trim() ?? ''
export const isEntraConfigured = Boolean(entraTenantId && entraClientId && entraApiClientId)

export const dayOptions = ['MON', 'TUE', 'WED', 'THU', 'FRI']

export const initialOverview: DashboardOverview = {
  healthTone: 'neutral',
  healthLabel: 'Sin señal suficiente',
  hasSignal: false,
  activeCampaigns: 0,
  registeredDevices: 0,
  responsesToday: 0,
  averageMood: null,
  pulseDelta: null,
  participationRate: null,
  pendingAlerts: 0,
  latestEvent: 'Sin actividad crítica en los últimos minutos.',
  alerts: [],
  metrics: [],
  pulseTrend: [],
  responseMix: [],
  scaleDistribution: [],
  noResponseCount: 0,
  actions: [],
  recentActivity: [],
  insight: {
    tone: 'attention',
    eyebrow: 'Sin base',
    title: 'No hay suficientes respuestas para inferir tendencia.',
    text: 'En este estado el producto todavía no puede hablar de clima, riesgo o mejora. Primero necesita capturar respuestas reales.',
  },
}

export const tabCopy: Record<Tab, { title: string; description: string }> = {
  overview: {
    title: 'Resumen ejecutivo',
    description: 'Vista general de actividad, tendencia y salud operativa del pulso.',
  },
  campaigns: {
    title: 'Gestión de campañas',
    description: 'Crea campañas, define preguntas y controla estado de ejecución.',
  },
  agents: {
    title: 'Dispositivos y agentes',
    description: 'Monitorea usuarios conectados y última actividad por equipo.',
  },
  activity: {
    title: 'Actividad de sesión',
    description: 'Revisa bloqueos, desbloqueos y tiempos de inactividad por usuario y dispositivo.',
  },
  responses: {
    title: 'Insights',
    description: 'Tendencias, respuestas numéricas y abiertas, con lectura operativa y exportación a Excel.',
  },
  admins: {
    title: 'Administradores',
    description: 'Gestiona los correos autorizados para ingresar con Microsoft Entra ID.',
  },
  lt: {
    title: 'TL Activos',
    description: 'Asigna Transformational Leaders activos a operaciones.',
  },
  settings: {
    title: 'Configuración',
    description: 'Parámetros de entorno y conectividad en tiempo real.',
  },
}
