import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { apiBaseUrl, csrfHeaderName, initialOverview } from '../constants'
import type {
  AgentActivityEvent,
  Campaign,
  ConnectionState,
  DashboardOverview,
  Device,
  ResponseItem,
  Tab,
  TransformationalLeaderCandidate,
  TransformationalLeaderOptions,
} from '../types'
import { normalizeCampaign } from '../utils/campaigns'
import { normalizeResponse } from '../utils/responses'
import type { AuthorizedFetch } from './adminPanelTypes'
import { useAdminsDomain } from './useAdminsDomain'
import { useAlertsDomain } from './useAlertsDomain'
import { useCampaignsDomain } from './useCampaignsDomain'
import { useResponsesDomain } from './useResponsesDomain'
import { hasAdminRole } from '../utils/adminRoles'

export function useAdminPanel(token: string, csrfToken: string, activeTab: Tab, role: string) {
  const [tab, setTab] = useState<Tab>(activeTab)
  const [connectionState, setConnectionState] = useState<ConnectionState>('connecting')
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [overview, setOverview] = useState<DashboardOverview>(initialOverview)
  const [devices, setDevices] = useState<Device[]>([])
  const [agentActivityEvents, setAgentActivityEvents] = useState<AgentActivityEvent[]>([])
  const [events, setEvents] = useState<string[]>([])
  const [transformationalLeaderOptions, setTransformationalLeaderOptions] = useState<TransformationalLeaderOptions>({ operations: [], leaders: [] })
  const [savingTransformationalLeaderSolvoId, setSavingTransformationalLeaderSolvoId] = useState<string | null>(null)
  const [hasLoadedOnce, setHasLoadedOnce] = useState(false)
  const loadInFlightRef = useRef<Promise<void> | null>(null)
  const pendingRefreshRef = useRef(false)
  const liveRefreshTimerRef = useRef<number | null>(null)
  const lastRefreshStartedAtRef = useRef(0)

  const authorizedFetch: AuthorizedFetch = async (input, init) => {
    const headers = new Headers(init?.headers ?? {})
    if (token) {
      headers.set('Authorization', `Bearer ${token}`)
    }

    const method = init?.method?.toUpperCase() ?? 'GET'
    if (method !== 'GET' && method !== 'HEAD' && method !== 'OPTIONS') {
      headers.set(csrfHeaderName, csrfToken)
    }

    if (init?.body && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json')
    }

    const response = await fetch(input, {
      ...init,
      headers,
      credentials: 'include',
      cache: init?.cache ?? 'no-store',
    })

    if (response.status === 401) {
      throw new Error('Tu sesion ya no es valida. Inicia sesion otra vez.')
    }

    return response
  }

  const campaignsDomain = useCampaignsDomain({
    authorizedFetch,
    loadData,
    setError,
    setTab,
  })
  const responsesDomain = useResponsesDomain({
    authorizedFetch,
    setError,
    setEvents,
  })
  const adminsDomain = useAdminsDomain({
    authorizedFetch,
    setError,
  })
  const alertsDomain = useAlertsDomain({
    authorizedFetch,
    setError,
  })

  useEffect(() => {
    void loadData()
  }, [token, role])

  useEffect(() => {
    setTab(activeTab)
  }, [activeTab])

  useEffect(() => {
    const intervalId = window.setInterval(() => {
      void loadData()
    }, 30_000)

    return () => window.clearInterval(intervalId)
  }, [token, role])

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/admin-notifications`, {
        accessTokenFactory: () => token,
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.onreconnecting(() => setConnectionState('connecting'))
    connection.onreconnected(() => setConnectionState('live'))
    connection.onclose(() => setConnectionState('offline'))
    connection.on('campaignCreated', (campaign: unknown) =>
      campaignsDomain.setCampaigns((list) => [normalizeCampaign(campaign), ...list.filter((item) => item.id !== (campaign as Campaign).id)]))
    connection.on('campaignUpdated', (campaign: unknown) =>
      campaignsDomain.setCampaigns((list) => list.map((item) => (item.id === (campaign as Campaign).id ? normalizeCampaign(campaign) : item))))
    connection.on('responseReceived', () => scheduleLiveRefresh())
    connection.on('deviceHeartbeat', () => scheduleLiveRefresh())
    connection.on('agentActivityReceived', (event: AgentActivityEvent) => {
      const cutoff = Date.now() - 48 * 60 * 60 * 1000
      setAgentActivityEvents((list) => [event, ...list.filter((item) => item.id !== event.id)]
        .filter((item) => new Date(item.occurredAtUtc).getTime() >= cutoff)
        .sort((left, right) => new Date(right.occurredAtUtc).getTime() - new Date(left.occurredAtUtc).getTime())
        .slice(0, 5000))
      scheduleLiveRefresh()
      setEvents((list) => {
        const activityMessage = event.eventType === 'SessionLocked'
          ? `${event.userName} bloqueo ${event.hostname}.`
          : event.eventType === 'SessionUnlocked'
            ? `${event.userName} desbloqueo ${event.hostname}.`
            : event.eventType === 'DeviceSuspended'
              ? `${event.userName} suspendio ${event.hostname}.`
              : event.eventType === 'DeviceResumed'
                ? `${event.userName} reanudo ${event.hostname}.`
                : event.eventType === 'DeviceStarted'
                  ? `${event.userName} inicio ${event.hostname}.`
                  : event.eventType === 'DeviceShutdown'
                    ? `${event.userName} apago o cerro sesion en ${event.hostname}.`
                    : `${event.userName} registro actividad en ${event.hostname}.`
        return [activityMessage, ...list].slice(0, 8)
      })
    })
    connection.on('liveEvent', (event: { message: string }) => setEvents((list) => [event.message, ...list].slice(0, 8)))
    void connection.start().then(() => setConnectionState('live')).catch(() => setConnectionState('offline'))
    return () => {
      if (liveRefreshTimerRef.current !== null) {
        window.clearTimeout(liveRefreshTimerRef.current)
        liveRefreshTimerRef.current = null
      }

      void connection.stop()
    }
  }, [token])

  function scheduleLiveRefresh() {
    if (liveRefreshTimerRef.current !== null) {
      return
    }

    const elapsedSinceLastRefresh = Date.now() - lastRefreshStartedAtRef.current
    const refreshDelay = Math.max(1_500, 10_000 - elapsedSinceLastRefresh)

    liveRefreshTimerRef.current = window.setTimeout(() => {
      liveRefreshTimerRef.current = null
      void loadData()
    }, refreshDelay)
  }

  async function loadData() {
    if (loadInFlightRef.current) {
      pendingRefreshRef.current = true
      return loadInFlightRef.current
    }

    const loadTask = runLoadData()
    loadInFlightRef.current = loadTask

    try {
      await loadTask
    } finally {
      loadInFlightRef.current = null
    }
  }

  async function runLoadData() {
    try {
      setIsRefreshing(true)

      do {
        pendingRefreshRef.current = false
        setError(null)
        lastRefreshStartedAtRef.current = Date.now()
        const stamp = Date.now()
        const canUseCampaigns = hasAdminRole(role, 'Owner') || hasAdminRole(role, 'HRAdmin')
        const canUseWorkforce = hasAdminRole(role, 'Owner') || hasAdminRole(role, 'WorkforceAdmin')
        const isOwner = hasAdminRole(role, 'Owner')
        const [overviewResponse, campaignsResponse, audienceOptionsResponse, clientAlertsResponse, transformationalLeadersResponse, devicesResponse, responsesResponse, agentActivityResponse, adminsResponse] = await Promise.all([
          authorizedFetch(`${apiBaseUrl}/api/dashboard/overview?ts=${stamp}`).catch(() => null),
          canUseCampaigns ? authorizedFetch(`${apiBaseUrl}/api/campaigns?includeDeleted=true&ts=${stamp}`) : Promise.resolve(null),
          canUseCampaigns ? authorizedFetch(`${apiBaseUrl}/api/campaigns/audience-options?ts=${stamp}`) : Promise.resolve(null),
          isOwner ? authorizedFetch(`${apiBaseUrl}/api/client-inactivity-alerts/options?ts=${stamp}`).catch(() => null) : Promise.resolve(null),
          isOwner ? authorizedFetch(`${apiBaseUrl}/api/transformational-leaders/options?ts=${stamp}`).catch(() => null) : Promise.resolve(null),
          canUseWorkforce ? authorizedFetch(`${apiBaseUrl}/api/devices?ts=${stamp}`).catch(() => null) : Promise.resolve(null),
          canUseCampaigns ? authorizedFetch(`${apiBaseUrl}/api/responses?ts=${stamp}`) : Promise.resolve(null),
          canUseWorkforce ? authorizedFetch(`${apiBaseUrl}/api/agent/activity-events/recent?ts=${stamp}`) : Promise.resolve(null),
          isOwner ? authorizedFetch(`${apiBaseUrl}/api/admin-users?ts=${stamp}`) : Promise.resolve(null),
        ])

        if ((campaignsResponse && !campaignsResponse.ok)
          || (audienceOptionsResponse && !audienceOptionsResponse.ok)
          || (responsesResponse && !responsesResponse.ok)
          || (agentActivityResponse && !agentActivityResponse.ok)
          || (adminsResponse && !adminsResponse.ok)) {
          throw new Error('No fue posible cargar informacion del panel.')
        }

        if (overviewResponse?.ok) {
          setOverview(await overviewResponse.json())
        }

        campaignsDomain.setCampaigns(campaignsResponse ? (await campaignsResponse.json()).map(normalizeCampaign) : [])
        campaignsDomain.setAudienceOptions(audienceOptionsResponse ? await audienceOptionsResponse.json() : { operations: [] })
        alertsDomain.setClientInactivityAlertOptions(clientAlertsResponse?.ok ? await clientAlertsResponse.json() : { clients: [], operations: [], settings: [] })
        setTransformationalLeaderOptions(transformationalLeadersResponse?.ok ? await transformationalLeadersResponse.json() : { operations: [], leaders: [] })
        setDevices(devicesResponse?.ok ? await devicesResponse.json() : [])
        setAgentActivityEvents(agentActivityResponse ? await agentActivityResponse.json() : [])
        adminsDomain.setAdmins(adminsResponse ? await adminsResponse.json() : [])
        responsesDomain.setResponses(responsesResponse ? (await responsesResponse.json()).map(normalizeResponse).sort((a: ResponseItem, b: ResponseItem) => new Date(b.answeredAtUtc).getTime() - new Date(a.answeredAtUtc).getTime()) : [])
        setHasLoadedOnce(true)
      } while (pendingRefreshRef.current)
    } catch (loadError) {
      pendingRefreshRef.current = false
      setError(loadError instanceof Error ? loadError.message : 'Error inesperado.')
    } finally {
      setIsRefreshing(false)
    }
  }

  async function saveTransformationalLeaderAssignment(solvoId: string, operations: string[]) {
    const normalizedSolvoId = solvoId.trim()
    const normalizedOperations = uniqueStrings(operations)
    if (!normalizedSolvoId || normalizedOperations.length === 0) {
      return
    }

    try {
      setSavingTransformationalLeaderSolvoId(normalizedSolvoId)
      setError(null)
      const response = await authorizedFetch(`${apiBaseUrl}/api/transformational-leaders/assignments`, {
        method: 'PUT',
        body: JSON.stringify({
          solvoId: normalizedSolvoId,
          operation: normalizedOperations[0],
          operations: normalizedOperations,
        }),
      })

      if (!response.ok) {
        throw new Error('No fue posible guardar la asignacion del TL.')
      }

      const updatedLeader = await response.json() as TransformationalLeaderCandidate
      setTransformationalLeaderOptions((current) => ({
        ...current,
        leaders: current.leaders.map((leader) => (
          leader.solvoId === updatedLeader.solvoId ? updatedLeader : leader
        )),
      }))
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : 'No fue posible guardar la asignacion del TL.')
    } finally {
      setSavingTransformationalLeaderSolvoId(null)
    }
  }

  async function clearTransformationalLeaderAssignment(solvoId: string) {
    const normalizedSolvoId = solvoId.trim()
    if (!normalizedSolvoId) {
      return
    }

    try {
      setSavingTransformationalLeaderSolvoId(normalizedSolvoId)
      setError(null)
      const response = await authorizedFetch(`${apiBaseUrl}/api/transformational-leaders/assignments/${encodeURIComponent(normalizedSolvoId)}`, {
        method: 'DELETE',
      })

      if (!response.ok && response.status !== 404) {
        throw new Error('No fue posible limpiar la asignacion del TL.')
      }

      setTransformationalLeaderOptions((current) => ({
        ...current,
        leaders: current.leaders.map((leader) => (
          leader.solvoId === normalizedSolvoId
            ? { ...leader, assignedOperation: '', assignedOperations: [], assignmentUpdatedAtUtc: null }
            : leader
        )),
      }))
    } catch (deleteError) {
      setError(deleteError instanceof Error ? deleteError.message : 'No fue posible limpiar la asignacion del TL.')
    } finally {
      setSavingTransformationalLeaderSolvoId(null)
    }
  }

  return {
    tab,
    setTab,
    connectionState,
    ...campaignsDomain,
    isRefreshing,
    error,
    setError,
    overview,
    ...alertsDomain,
    devices,
    agentActivityEvents,
    ...responsesDomain,
    ...adminsDomain,
    transformationalLeaderOptions,
    savingTransformationalLeaderSolvoId,
    saveTransformationalLeaderAssignment,
    clearTransformationalLeaderAssignment,
    events,
    hasLoadedOnce,
    loadData,
  }
}

function uniqueStrings(values: string[]) {
  return Array.from(new Set(values.map((value) => value.trim()).filter(Boolean)))
}
