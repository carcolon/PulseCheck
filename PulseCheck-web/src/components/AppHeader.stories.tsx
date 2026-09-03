import type { Meta, StoryObj } from '@storybook/react-vite'
import { useState } from 'react'
import type { DashboardOverview, Tab } from '../types'
import { AppHeader, ErrorBanner, TabIntro, TabNav } from './AppHeader'

const overview: DashboardOverview = {
  healthTone: 'healthy',
  healthLabel: 'Operativo',
  hasSignal: true,
  activeCampaigns: 4,
  registeredDevices: 128,
  responsesToday: 96,
  averageMood: 4.2,
  pulseDelta: '+8%',
  participationRate: 0.78,
  pendingAlerts: 2,
  latestEvent: 'Ultimo check-in hace 3 minutos',
  alerts: [
    {
      tone: 'positive',
      eyebrow: 'Participacion',
      title: 'Respuesta alta',
      text: 'El pulso diario mantiene una cobertura estable.',
    },
    {
      tone: 'warning',
      eyebrow: 'Seguimiento',
      title: '2 equipos sin actividad',
      text: 'Conviene revisar la operacion de soporte antes del cierre.',
    },
  ],
  metrics: [],
  pulseTrend: [],
  responseMix: [],
  scaleDistribution: [],
  noResponseCount: 8,
  actions: [],
  recentActivity: [],
  insight: {
    tone: 'positive',
    eyebrow: 'Insight',
    title: 'Mejora sostenida',
    text: 'La operacion reporta mejor energia que la semana anterior.',
  },
}

const meta = {
  title: 'Components/AppHeader',
  parameters: {
    layout: 'fullscreen',
  },
} satisfies Meta

export default meta
type Story = StoryObj<typeof meta>

export const Header: Story = {
  render: () => (
    <div className="mx-auto grid max-w-6xl gap-4 p-6">
      <AppHeader
        overview={overview}
        isRefreshing={false}
        subtitle="Actualizado hace 5 minutos"
        onRefresh={() => undefined}
        onNewCampaign={() => undefined}
      />
    </div>
  ),
}

export const HeaderRefreshing: Story = {
  render: () => (
    <div className="mx-auto grid max-w-6xl gap-4 p-6">
      <AppHeader
        overview={{ ...overview, alerts: [] }}
        isRefreshing
        subtitle="Sin alertas criticas"
        onRefresh={() => undefined}
        onNewCampaign={() => undefined}
      />
    </div>
  ),
}

export const IntroAndError: Story = {
  render: () => (
    <div className="mx-auto grid max-w-3xl gap-4 p-6">
      <TabIntro title="Insights" description="Analiza respuestas por campana, operacion y fecha." />
      <ErrorBanner message="No se pudieron sincronizar los datos en tiempo real." />
    </div>
  ),
}

export const Navigation: Story = {
  render: function NavigationStory() {
    const [tab, setTab] = useState<Tab>('overview')

    return (
      <div className="mx-auto max-w-4xl p-6">
        <TabNav tab={tab} connectionState="live" onChange={setTab} />
      </div>
    )
  },
}
