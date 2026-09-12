import { useEffect, useMemo, useRef, useState } from 'react'
import type { CSSProperties } from 'react'
import { PieChart } from 'echarts/charts'
import { GraphicComponent, LegendComponent, TooltipComponent } from 'echarts/components'
import * as echarts from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import type { EChartsOption } from 'echarts'
import type { AgentActivityEvent, DashboardOverview, Device } from '../types'
import { hasAdminRole } from '../utils/adminRoles'

echarts.use([PieChart, GraphicComponent, LegendComponent, TooltipComponent, CanvasRenderer])

const ACTIVE_ACTIVITY_HOURS = 48
const OVERVIEW_CHART_REFRESH_MS = 60 * 60 * 1000
const responseTypeColors = ['#00a9be', '#f3c86a', '#6b7cff', '#0ea5a8']

function formatPercent(value: number) {
  if (!Number.isFinite(value)) return '0%'
  return `${Math.round(value)}%`
}

function formatDecimalPercent(value: number, total: number) {
  if (!total) return '0.0%'
  return `${((value / total) * 100).toFixed(1)}%`
}

function normalizeKey(value: string | null | undefined) {
  return value?.trim().toLowerCase() ?? ''
}

function getDeviceUserKey(device: Device) {
  return normalizeKey(device.email) || normalizeKey(device.userName) || normalizeKey(device.deviceId) || normalizeKey(device.hostname)
}

function getDeviceKeys(device: Device) {
  return [
    normalizeKey(device.email),
    normalizeKey(device.userName),
    normalizeKey(device.deviceId),
    normalizeKey(device.hostname),
  ].filter(Boolean)
}

function getActivityKeys(event: AgentActivityEvent) {
  return [
    normalizeKey(event.email),
    normalizeKey(event.userName),
    normalizeKey(event.userId),
    normalizeKey(event.deviceId),
    normalizeKey(event.hostname),
  ].filter(Boolean)
}

function getActiveUserCounts(devices: Device[], activityEvents: AgentActivityEvent[]) {
  const now = Date.now()
  const maxAgeMs = ACTIVE_ACTIVITY_HOURS * 60 * 60 * 1000
  const installedUsers = new Set<string>()
  const identifierToUser = new Map<string, string>()
  const activeUsers = new Set<string>()

  devices.forEach((device) => {
    const userKey = getDeviceUserKey(device)
    if (!userKey) return

    installedUsers.add(userKey)
    getDeviceKeys(device).forEach((key) => identifierToUser.set(key, userKey))
  })

  activityEvents.forEach((event) => {
    const timestamp = Date.parse(event.occurredAtUtc)
    if (!Number.isFinite(timestamp) || now - timestamp > maxAgeMs) return

    const matchedUser = getActivityKeys(event).map((key) => identifierToUser.get(key)).find(Boolean)
    if (matchedUser) activeUsers.add(matchedUser)
  })

  return {
    active: activeUsers.size,
    inactive: Math.max(installedUsers.size - activeUsers.size, 0),
  }
}

function getAdaptiveDonutSize(value: number) {
  const digits = Math.max(String(Math.max(0, value)).length, 2)
  return Math.min(520, Math.max(280, 230 + digits * 28))
}

function getCenterFontSize(value: number) {
  const digits = Math.max(String(Math.max(0, value)).length, 1)
  return Math.max(28, Math.min(64, 74 - digits * 6))
}

function shouldRefreshHourlySnapshot(lastSnapshotAt: number) {
  return lastSnapshotAt === 0 || Date.now() - lastSnapshotAt >= OVERVIEW_CHART_REFRESH_MS
}

function normalizeResponseType(label: string) {
  const value = label.toLowerCase()
  if (value.includes('escala') || value.includes('scale') || value.includes('numer')) return 'Calificacion'
  if (value.includes('texto') || value.includes('text') || value.includes('abierta')) return 'Comentario'
  if (value.includes('si') || value.includes('sí') || value.includes('sÃ­') || value.includes('yes')) return 'Si/No'
  if (value.includes('choice') || value.includes('opcion') || value.includes('opci') || value.includes('personal')) return 'Personalizada'
  return label
}

function buildDonutOption({
  data,
  colors,
  centerLabel,
  centerValue,
  centerColor = '#0b3341',
  centerFontSize = 36,
}: {
  data: Array<{ name: string; value: number }>
  colors: string[]
  centerLabel: string
  centerValue: string
  centerColor?: string
  centerFontSize?: number
}): EChartsOption {
  return {
    color: colors,
    tooltip: {
      trigger: 'item',
      formatter: '{b}: {c} ({d}%)',
      borderWidth: 0,
      backgroundColor: 'rgba(7, 35, 51, 0.92)',
      textStyle: { color: '#ffffff', fontFamily: 'Manrope' },
    },
    legend: { show: false },
    graphic: [
      {
        type: 'text',
        left: 'center',
        top: '39%',
        z: 100,
        silent: true,
        style: {
          text: centerValue,
          fill: centerColor,
          fontSize: centerFontSize,
          fontWeight: 800,
          fontFamily: 'Manrope',
          align: 'center',
          verticalAlign: 'middle',
        },
      },
      {
        type: 'text',
        left: 'center',
        top: '57%',
        z: 100,
        silent: true,
        style: {
          text: centerLabel,
          fill: '#5d7480',
          fontSize: 13,
          fontWeight: 700,
          fontFamily: 'Manrope',
          align: 'center',
          verticalAlign: 'middle',
        },
      },
    ],
    series: [
      {
        type: 'pie',
        radius: ['66%', '86%'],
        center: ['50%', '50%'],
        avoidLabelOverlap: true,
        label: { show: false },
        labelLine: { show: false },
        itemStyle: {
          borderColor: '#ffffff',
          borderWidth: 4,
        },
        data,
      },
    ],
  }
}

function buildPieOption({
  data,
  colors,
}: {
  data: Array<{ name: string; value: number }>
  colors: string[]
}): EChartsOption {
  return {
    color: colors,
    tooltip: {
      trigger: 'item',
      formatter: '{b}: {c} ({d}%)',
      borderWidth: 0,
      backgroundColor: 'rgba(7, 35, 51, 0.92)',
      textStyle: { color: '#ffffff', fontFamily: 'Manrope' },
    },
    legend: { show: false },
    series: [
      {
        type: 'pie',
        radius: '74%',
        center: ['50%', '52%'],
        avoidLabelOverlap: true,
        label: {
          show: true,
          formatter: '{d}%',
          color: '#0d3140',
          fontFamily: 'Manrope',
          fontWeight: 800,
        },
        labelLine: { length: 12, length2: 8 },
        itemStyle: {
          borderColor: '#ffffff',
          borderWidth: 4,
        },
        data,
      },
    ],
  }
}

function buildResponseTypeOption(data: Array<{ name: string; value: number }>): EChartsOption {
  return {
    color: responseTypeColors,
    tooltip: {
      trigger: 'item',
      formatter: '{b}: {c} ({d}%)',
      borderWidth: 0,
      backgroundColor: 'rgba(7, 35, 51, 0.92)',
      textStyle: { color: '#ffffff', fontFamily: 'Manrope' },
    },
    legend: { show: false },
    series: [
      {
        type: 'pie',
        radius: ['56%', '78%'],
        center: ['50%', '52%'],
        label: { show: false },
        labelLine: { show: false },
        itemStyle: {
          borderColor: '#ffffff',
          borderWidth: 4,
        },
        data,
      },
    ],
  }
}

function EChart({ option }: { option: EChartsOption }) {
  const ref = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!ref.current) return undefined

    const chart = echarts.init(ref.current)
    chart.setOption(option)

    const resizeObserver = new ResizeObserver(() => chart.resize())
    resizeObserver.observe(ref.current)

    return () => {
      resizeObserver.disconnect()
      chart.dispose()
    }
  }, [option])

  return <div ref={ref} className="overview-donut" />
}

function ChartPanel({
  eyebrow,
  title,
  description,
  option,
  legend,
  note,
  wide = false,
  chartMinWidth,
}: {
  eyebrow: string
  title: string
  description: string
  option: EChartsOption | null
  legend: Array<{ label: string; value: string; color: string; context: string }>
  note: string
  wide?: boolean
  chartMinWidth?: number
}) {
  const chartStyle = chartMinWidth
    ? ({ '--overview-chart-size': `${chartMinWidth}px` } as CSSProperties)
    : undefined

  return (
    <article className={wide ? 'overview-chart-panel overview-chart-panel--wide' : 'overview-chart-panel'} data-animate>
      <div className="overview-chart-panel__heading">
        <span>{eyebrow}</span>
        <h3>{title}</h3>
        <p>{description}</p>
      </div>

      {option ? (
        <div className="overview-chart-shell" style={chartStyle}>
          <EChart option={option} />
          <div className="overview-chart-legend">
            {legend.map((item) => (
              <div className="overview-legend-item" key={item.label}>
                <span className="overview-legend-dot" style={{ backgroundColor: item.color }} />
                <strong>{item.value}</strong>
                <div>
                  <span>{item.label}</span>
                  <small>{item.context}</small>
                </div>
              </div>
            ))}
          </div>
        </div>
      ) : (
        <div className="overview-empty-state">Todavia no hay datos suficientes para esta grafica.</div>
      )}

      <p className="overview-chart-note">{note}</p>
    </article>
  )
}

function ResponseTypePanel({
  data,
  option,
}: {
  data: Array<{ name: string; value: number }>
  option: EChartsOption | null
}) {
  const total = data.reduce((sum, item) => sum + item.value, 0)

  return (
    <article className="overview-chart-panel overview-chart-panel--response-mix" data-animate>
      <div className="overview-chart-panel__heading">
        <span>Campañas</span>
        <h3>Insights</h3>
        <p>Aquí se ve qué parte de las respuestas llegó como calificación, comentario escrito, respuesta de sí/no u opción personalizada.</p>
      </div>

      {option ? (
        <div className="overview-response-mix-shell">
          <EChart option={option} />
          <div className="overview-response-mix-list">
            {data.map((item, index) => (
              <div className="overview-response-mix-item" key={item.name}>
                <span className="overview-legend-dot" style={{ backgroundColor: responseTypeColors[index % responseTypeColors.length] }} />
                <div className="overview-response-mix-item__copy">
                  <strong>{item.name}</strong>
                  <span>{formatDecimalPercent(item.value, total)} - {item.value} respuestas</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      ) : (
        <div className="overview-empty-state">Todavia no hay datos suficientes para esta grafica.</div>
      )}
    </article>
  )
}

export function OverviewTab({
  overview,
  devices = [],
  activityEvents = [],
  role,
}: {
  overview: DashboardOverview
  devices?: Device[]
  activityEvents?: AgentActivityEvent[]
  role: string
  onOpenCampaigns?: () => void
  onOpenAgents?: () => void
  onOpenActivity?: () => void
  onOpenInsights?: () => void
}) {
  const isOwner = hasAdminRole(role, 'Owner')
  const canSeeWorkforce = isOwner || hasAdminRole(role, 'WorkforceAdmin')
  const canSeeHr = isOwner || hasAdminRole(role, 'HRAdmin')

  const liveUserCounts = useMemo(() => getActiveUserCounts(devices, activityEvents), [devices, activityEvents])
  const [workforceSnapshot, setWorkforceSnapshot] = useState(liveUserCounts)
  const workforceSnapshotAtRef = useRef(0)
  const [hrSnapshot, setHrSnapshot] = useState(() => ({
    noResponseCount: overview.noResponseCount,
    participationRate: overview.participationRate,
    registeredDevices: overview.registeredDevices,
    responseMix: overview.responseMix,
  }))
  const hrSnapshotAtRef = useRef(0)

  useEffect(() => {
    const snapshotTotal = workforceSnapshot.active + workforceSnapshot.inactive
    const liveTotal = liveUserCounts.active + liveUserCounts.inactive
    const shouldRefreshSnapshot =
      shouldRefreshHourlySnapshot(workforceSnapshotAtRef.current) ||
      (snapshotTotal === 0 && liveTotal > 0) ||
      snapshotTotal !== liveTotal

    if (!shouldRefreshSnapshot) return

    workforceSnapshotAtRef.current = Date.now()
    setWorkforceSnapshot(liveUserCounts)
  }, [liveUserCounts, workforceSnapshot.active, workforceSnapshot.inactive])

  useEffect(() => {
    const snapshotTotal = hrSnapshot.responseMix.reduce((total, item) => total + item.value, 0)
    const liveTotal = overview.responseMix.reduce((total, item) => total + item.value, 0)
    const shouldRefreshSnapshot =
      shouldRefreshHourlySnapshot(hrSnapshotAtRef.current) ||
      (snapshotTotal === 0 && liveTotal > 0)

    if (!shouldRefreshSnapshot) return

    hrSnapshotAtRef.current = Date.now()
    setHrSnapshot({
      noResponseCount: overview.noResponseCount,
      participationRate: overview.participationRate,
      registeredDevices: overview.registeredDevices,
      responseMix: overview.responseMix,
    })
  }, [
    hrSnapshot.responseMix,
    overview.noResponseCount,
    overview.participationRate,
    overview.registeredDevices,
    overview.responseMix,
  ])

  const userCounts = workforceSnapshot
  const totalUsers = userCounts.active + userCounts.inactive
  const activePercent = totalUsers > 0 ? (userCounts.active / totalUsers) * 100 : 0
  const inactivePercent = totalUsers > 0 ? (userCounts.inactive / totalUsers) * 100 : 0
  const workforceChartSize = getAdaptiveDonutSize(userCounts.active)

  const responsesCount = useMemo(
    () => hrSnapshot.responseMix.reduce((total, item) => total + item.value, 0),
    [hrSnapshot.responseMix],
  )
  const pendingResponseCount = Math.max(hrSnapshot.noResponseCount, 0)
  const respondedDeviceCount = hrSnapshot.registeredDevices > 0
    ? Math.max(0, hrSnapshot.registeredDevices - pendingResponseCount)
    : responsesCount
  const participationTotal = respondedDeviceCount + pendingResponseCount
  const respondedPercent =
    hrSnapshot.participationRate ?? (participationTotal > 0 ? (respondedDeviceCount / participationTotal) * 100 : 0)

  const responseTypeData = useMemo(() => {
    const buckets = new Map<string, number>()
    hrSnapshot.responseMix.forEach((item) => {
      const key = normalizeResponseType(item.label)
      buckets.set(key, (buckets.get(key) ?? 0) + item.value)
    })
    return Array.from(buckets.entries()).map(([name, value]) => ({ name, value }))
  }, [hrSnapshot.responseMix])

  const workforceOption = useMemo(
    () => totalUsers
      ? buildDonutOption({
        data: [
          { name: 'Activas', value: userCounts.active },
          { name: 'No activas', value: userCounts.inactive },
        ],
        colors: ['#46aef0', '#bfc5c9'],
        centerValue: String(userCounts.active),
        centerLabel: 'activas',
        centerColor: '#0bbfd0',
        centerFontSize: getCenterFontSize(userCounts.active),
      })
      : null,
    [activePercent, totalUsers, userCounts.active, userCounts.inactive],
  )

  const participationOption = useMemo(
    () => participationTotal
      ? buildPieOption({
        data: [
          { name: 'Respondieron', value: respondedDeviceCount },
          { name: 'No respondieron', value: pendingResponseCount },
        ],
        colors: ['#48c7a8', '#bfc5c9'],
      })
      : null,
    [participationTotal, pendingResponseCount, respondedDeviceCount],
  )

  const responseTypeOption = useMemo(
    () => responseTypeData.length ? buildResponseTypeOption(responseTypeData) : null,
    [responseTypeData],
  )

  const panels = [
    canSeeWorkforce ? (
      <ChartPanel
        key="workforce"
        eyebrow="Workforce"
        title="Actividad"
        description={`Usuarios con agente instalado que registran actividad real en las últimas ${ACTIVE_ACTIVITY_HOURS} horas.`}
        option={workforceOption}
        chartMinWidth={workforceChartSize}
        legend={[
          { label: 'Activas', value: `${userCounts.active} (${formatPercent(activePercent)})`, color: '#46aef0', context: 'con actividad 48 h' },
          { label: 'No activas', value: `${userCounts.inactive} (${formatPercent(inactivePercent)})`, color: '#bfc5c9', context: 'sin actividad 48 h' },
        ]}
        note={`${totalUsers} usuarios con agente instalado en la vista actual.`}
        wide={!canSeeHr}
      />
    ) : null,
    canSeeHr ? (
      <ChartPanel
        key="participation"
        eyebrow="HR"
        title="Participación"
        description="Participación acumulada de las campañas con respuestas disponibles en el panel."
        option={participationOption}
        legend={[
          { label: 'Respondieron', value: String(respondedDeviceCount), color: '#48c7a8', context: 'usuarios con respuesta' },
          { label: 'No respondieron', value: String(pendingResponseCount), color: '#bfc5c9', context: 'pendientes estimados' },
        ]}
        note={`${formatPercent(respondedPercent)} de participacion registrada.`}
      />
    ) : null,
    canSeeHr ? <ResponseTypePanel key="response-types" data={responseTypeData} option={responseTypeOption} /> : null,
  ].filter(Boolean)

  return (
    <section className={`overview-redesign overview-redesign--${panels.length}`}>
      <header className="overview-redesign__header" data-animate>
        <span>Resumen operativo</span>
        <h2>Lectura ejecutiva de PulseCheck</h2>
      </header>

      <div className="overview-charts">{panels}</div>
    </section>
  )
}
