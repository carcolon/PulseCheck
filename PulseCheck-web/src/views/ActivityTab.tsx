import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import {
  Box,
  Button,
  Chip,
  Divider,
  Drawer,
  IconButton,
  InputAdornment,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import CloseRoundedIcon from '@mui/icons-material/CloseRounded'
import PersonSearchRoundedIcon from '@mui/icons-material/PersonSearchRounded'
import SearchRoundedIcon from '@mui/icons-material/SearchRounded'
import type { AgentActivityEvent, Device } from '../types'

type ActivityFilter = 'all' | 'locked' | 'unlocked' | 'idle'
type DevicePresenceFilter = 'active' | 'inactive'

type DeviceActivity = {
  id: string
  deviceId: string
  hostname: string
  userName: string
  email: string
  operation: string
  lastEventAtUtc: string
  lastSeenAtUtc: string | null
  lastSeenAtLocal: string | null
  localOffsetSource: string | null
  hasRecentActivity: boolean
  lockCount: number
  unlockCount: number
  suspendCount: number
  resumeCount: number
  idleCount: number
  latestIdleSeconds: number | null
  locks: AgentActivityEvent[]
  unlocks: AgentActivityEvent[]
  powerEvents: AgentActivityEvent[]
  idleEvents: AgentActivityEvent[]
  sessions: ActivitySession[]
}

type ActivitySession = {
  id: string
  lock: AgentActivityEvent | null
  unlock: AgentActivityEvent | null
}

const lookbackHours = 48
const maxEventsPerKind = 10
const pageSize = 50

function formatDuration(totalSeconds: number | null) {
  if (totalSeconds === null) return 'Sin dato'
  if (totalSeconds < 60) return `${totalSeconds} s`
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60
  if (hours > 0) return `${hours} h ${minutes} min ${seconds} s`
  return `${minutes} min ${seconds} s`
}

function formatEventTime(value: string) {
  const match = value.match(
    /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?(?:\.\d+)?(Z|[+-]\d{2}:\d{2})?$/,
  )
  if (!match) {
    return new Date(value).toLocaleString()
  }

  const [, year, month, day, hour, minute, second = '00', offset = ''] = match
  const formattedOffset = offset === 'Z' ? 'UTC' : offset ? `UTC${offset}` : 'hora local'
  return `${day}/${month}/${year}, ${hour}:${minute}:${second} - ${formattedOffset}`
}

function getOffsetFromDateTimeOffset(value: string | null | undefined) {
  if (!value) return null
  const match = value.match(/(Z|[+-]\d{2}:\d{2})$/)
  if (!match) return null
  return match[1] === 'Z' ? '+00:00' : match[1]
}

function formatUtcWithOffset(value: string, offset: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return formatEventTime(value)

  const sign = offset.startsWith('-') ? -1 : 1
  const [hours, minutes] = offset.slice(1).split(':').map(Number)
  const offsetMilliseconds = sign * ((hours * 60 + minutes) * 60 * 1000)
  const localDate = new Date(date.getTime() + offsetMilliseconds)
  const pad = (input: number) => input.toString().padStart(2, '0')
  const formattedOffset = `UTC${offset}`

  return `${pad(localDate.getUTCDate())}/${pad(localDate.getUTCMonth() + 1)}/${localDate.getUTCFullYear()}, ${pad(localDate.getUTCHours())}:${pad(localDate.getUTCMinutes())}:${pad(localDate.getUTCSeconds())} - ${formattedOffset}`
}

function formatDeviceLocalTime(event: AgentActivityEvent) {
  if (event.occurredAtLocal) {
    return formatEventTime(event.occurredAtLocal)
  }

  return `${formatEventTime(event.occurredAtUtc)} - sin hora local del agente`
}

function formatDeviceCheckIn(device: Pick<DeviceActivity, 'lastSeenAtLocal' | 'lastSeenAtUtc' | 'localOffsetSource'>) {
  const sourceOffset = getOffsetFromDateTimeOffset(device.localOffsetSource)

  if (device.lastSeenAtLocal && (!sourceOffset || sourceOffset === '+00:00')) {
    return formatEventTime(device.lastSeenAtLocal)
  }

  if (device.lastSeenAtUtc) {
    if (sourceOffset && sourceOffset !== '+00:00') {
      return formatUtcWithOffset(device.lastSeenAtUtc, sourceOffset)
    }

    if (device.lastSeenAtLocal) {
      return formatEventTime(device.lastSeenAtLocal)
    }

    return `${formatEventTime(device.lastSeenAtUtc)} - sin hora local del agente`
  }

  return 'Sin check-in'
}

function describeEvent(event: AgentActivityEvent) {
  if (event.eventType === 'DeviceSuspended') {
    return event.idleSecondsAtLock === null
      ? 'Equipo suspendido'
      : `Equipo suspendido tras ${formatDuration(event.idleSecondsAtLock)} de inactividad`
  }

  if (event.eventType === 'DeviceResumed') {
    return event.durationSeconds === null
      ? 'Equipo reanudado'
      : `Equipo reanudado despues de ${formatDuration(event.durationSeconds)}`
  }

  if (event.eventType === 'DeviceStarted') {
    return 'Equipo encendido o agente iniciado'
  }

  if (event.eventType === 'DeviceShutdown') {
    return 'Apagado o cierre de sesion detectado'
  }

  if (event.eventType === 'SessionUnlocked') {
    return `Desbloqueo despues de ${formatDuration(event.durationSeconds)}`
  }

  if (event.lockReason === 'AutoLock') {
    return event.idleSecondsAtLock === null
      ? 'Bloqueo por inactividad'
      : `Bloqueo por inactividad tras ${formatDuration(event.idleSecondsAtLock)}`
  }

  return 'Bloqueo manual'
}

function getEventTime(event: AgentActivityEvent) {
  return new Date(event.occurredAtUtc).getTime()
}

function isActivityEvent(event: AgentActivityEvent) {
  return event.eventType === 'SessionLocked' ||
    event.eventType === 'SessionUnlocked' ||
    event.eventType === 'DeviceSuspended' ||
    event.eventType === 'DeviceResumed' ||
    event.eventType === 'DeviceStarted' ||
    event.eventType === 'DeviceShutdown'
}

function getDeviceKey(event: AgentActivityEvent) {
  return event.deviceId.trim().toLowerCase() || event.hostname.trim().toLowerCase()
}

function getDeviceKeyFromDevice(device: Device) {
  return device.deviceId.trim().toLowerCase() || device.hostname.trim().toLowerCase()
}

function getSearchTokens(value: string) {
  return normalizeSearchText(value)
    .split(/\s+/)
    .filter(Boolean)
}

function matchesSearchTokens(values: Array<string | null | undefined>, searchTokens: string[]) {
  const searchableText = normalizeSearchText(values.filter(Boolean).join(' '))
  return searchTokens.every((token) => searchableText.includes(token))
}

function normalizeSearchText(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
}

function buildDeviceLookup(devices: Device[]) {
  const lookup = new Map<string, Device>()
  devices.forEach((device) => {
    if (device.deviceId) {
      lookup.set(device.deviceId.trim().toLowerCase(), device)
    }

    if (device.hostname) {
      lookup.set(device.hostname.trim().toLowerCase(), device)
    }
  })
  return lookup
}

function resolveOperation(event: AgentActivityEvent, device?: Device) {
  return device?.operation || device?.department || event.department || 'Sin operacion'
}

function resolveDeviceOperation(device: Device) {
  return device.operation || device.department || 'Sin operacion'
}

function buildActivitySessions(events: AgentActivityEvent[]) {
  const sessions: ActivitySession[] = []
  let pendingLock: AgentActivityEvent | null = null

  const orderedEvents = [...events].sort((left, right) => getEventTime(left) - getEventTime(right))
  for (const event of orderedEvents) {
    if (event.eventType === 'SessionLocked') {
      if (pendingLock) {
        sessions.push({
          id: pendingLock.id,
          lock: pendingLock,
          unlock: null,
        })
      }

      pendingLock = event
      continue
    }

    if (event.eventType !== 'SessionUnlocked') {
      continue
    }

    if (pendingLock) {
      sessions.push({
        id: `${pendingLock.id}-${event.id}`,
        lock: pendingLock,
        unlock: event,
      })
      pendingLock = null
      continue
    }

    sessions.push({
      id: event.id,
      lock: null,
      unlock: event,
    })
  }

  if (pendingLock) {
    sessions.push({
      id: pendingLock.id,
      lock: pendingLock,
      unlock: null,
    })
  }

  return sessions
    .sort((left, right) => {
      const leftEvent = left.unlock ?? left.lock
      const rightEvent = right.unlock ?? right.lock
      return getEventTime(rightEvent!) - getEventTime(leftEvent!)
    })
    .slice(0, maxEventsPerKind)
}

function MetricCard({
  icon,
  label,
  value,
  helper,
  active = false,
  onClick,
}: {
  icon: ReactNode
  label: string
  value: string
  helper: string
  active?: boolean
  onClick?: () => void
}) {
  return (
    <Paper
      className={active ? 'activity-metric activity-metric--active' : 'activity-metric'}
      component={onClick ? 'button' : 'div'}
      elevation={0}
      onClick={onClick}
      type={onClick ? 'button' : undefined}
    >
      <Box className="activity-metric__icon">{icon}</Box>
      <Box className="activity-metric__content">
        <Typography className="activity-metric__label">{label}</Typography>
        <Typography className="activity-metric__value">{value}</Typography>
        <Typography className="activity-metric__helper">{helper}</Typography>
      </Box>
    </Paper>
  )
}

function ActivitySessionTimeline({
  title,
  sessions,
  emptyText,
}: {
  title: string
  sessions: ActivitySession[]
  emptyText: string
}) {
  return (
    <Box className="activity-timeline">
      <Typography className="activity-section-label">{title}</Typography>
      {sessions.length === 0 ? (
        <Box className="activity-empty">{emptyText}</Box>
      ) : (
        sessions.map((session) => (
          <Box key={session.id} className="activity-timeline__item">
            <Box className={session.unlock ? 'activity-timeline__dot activity-timeline__dot--unlock' : 'activity-timeline__dot'} />
            <Box className="activity-timeline__content">
              {session.lock ? (
                <>
                  <Typography className="activity-timeline__title">{describeEvent(session.lock)}</Typography>
                  <Typography className="activity-timeline__meta">{session.lock.userName || 'Usuario sin nombre'} - {formatDeviceLocalTime(session.lock)}</Typography>
                  {session.lock.idleSecondsAtLock !== null ? (
                    <Chip className="activity-timeline__chip" label={`Inactividad previa: ${formatDuration(session.lock.idleSecondsAtLock)}`} size="small" />
                  ) : null}
                </>
              ) : null}

              {session.unlock ? (
                <Box sx={{ mt: session.lock ? 1.25 : 0 }}>
                  <Typography className="activity-timeline__title">{describeEvent(session.unlock)}</Typography>
                  <Typography className="activity-timeline__meta">{session.unlock.userName || 'Usuario sin nombre'} - {formatDeviceLocalTime(session.unlock)}</Typography>
                  {session.unlock.durationSeconds !== null ? (
                    <Chip className="activity-timeline__chip activity-timeline__chip--unlock" label={`Bloqueado durante ${formatDuration(session.unlock.durationSeconds)}`} size="small" />
                  ) : null}
                </Box>
              ) : (
                <Chip className="activity-timeline__chip" label="Sin desbloqueo registrado en esta ventana" size="small" />
              )}
            </Box>
          </Box>
        ))
      )}
    </Box>
  )
}

function ActivityEventTimeline({
  title,
  events,
  emptyText,
}: {
  title: string
  events: AgentActivityEvent[]
  emptyText: string
}) {
  return (
    <Box className="activity-timeline">
      <Typography className="activity-section-label">{title}</Typography>
      {events.length === 0 ? (
        <Box className="activity-empty">{emptyText}</Box>
      ) : (
        events.map((event) => (
          <Box key={event.id} className="activity-timeline__item">
            <Box className="activity-timeline__dot activity-timeline__dot--power" />
            <Box className="activity-timeline__content">
              <Typography className="activity-timeline__title">{describeEvent(event)}</Typography>
              <Typography className="activity-timeline__meta">{event.userName || 'Usuario sin nombre'} - {formatDeviceLocalTime(event)}</Typography>
              {event.durationSeconds !== null ? (
                <Chip className="activity-timeline__chip activity-timeline__chip--unlock" label={`Duracion: ${formatDuration(event.durationSeconds)}`} size="small" />
              ) : null}
            </Box>
          </Box>
        ))
      )}
    </Box>
  )
}

export function ActivityTab({ activityEvents, devices }: { activityEvents: AgentActivityEvent[]; devices: Device[] }) {
  const [activityFilter, setActivityFilter] = useState<ActivityFilter>('all')
  const [devicePresenceFilter, setDevicePresenceFilter] = useState<DevicePresenceFilter>('active')
  const [searchTerm, setSearchTerm] = useState('')
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [page, setPage] = useState(0)

  const deviceLookup = useMemo(() => buildDeviceLookup(devices), [devices])
  const sessionEvents = useMemo(() => {
    const cutoff = Date.now() - lookbackHours * 60 * 60 * 1000
    return activityEvents
      .filter((event) =>
        isActivityEvent(event) &&
        new Date(event.occurredAtUtc).getTime() >= cutoff)
      .sort((left, right) => new Date(right.occurredAtUtc).getTime() - new Date(left.occurredAtUtc).getTime())
  }, [activityEvents])

  const deviceActivities = useMemo<DeviceActivity[]>(() => {
    const groups = new Map<string, AgentActivityEvent[]>()

    sessionEvents.forEach((event) => {
      const key = getDeviceKey(event)
      const existing = groups.get(key)
      if (existing) {
        existing.push(event)
      } else {
        groups.set(key, [event])
      }
    })

    const activeActivities = Array.from(groups.entries())
      .map(([key, events]) => {
        const orderedEvents = [...events].sort((left, right) => new Date(right.occurredAtUtc).getTime() - new Date(left.occurredAtUtc).getTime())
        const latestEvent = orderedEvents[0]
        const latestLocalEvent = orderedEvents.find((event) => Boolean(event.occurredAtLocal))
        const device = deviceLookup.get(key) ?? deviceLookup.get(latestEvent.hostname.trim().toLowerCase())
        const locks = orderedEvents.filter((event) => event.eventType === 'SessionLocked')
        const unlocks = orderedEvents.filter((event) => event.eventType === 'SessionUnlocked')
        const powerEvents = orderedEvents.filter((event) =>
          event.eventType === 'DeviceSuspended' ||
          event.eventType === 'DeviceResumed' ||
          event.eventType === 'DeviceStarted' ||
          event.eventType === 'DeviceShutdown')
        const idleEvents = locks.filter((event) => event.lockReason === 'AutoLock')
        const latestIdleSeconds = idleEvents.find((event) => event.idleSecondsAtLock !== null)?.idleSecondsAtLock ?? null

        return {
          id: key,
          deviceId: latestEvent.deviceId,
          hostname: latestEvent.hostname || device?.hostname || 'sin equipo',
          userName: latestEvent.userName || device?.userName || 'Usuario sin nombre',
          email: latestEvent.email || device?.email || 'sin correo',
          operation: resolveOperation(latestEvent, device),
          lastEventAtUtc: latestEvent.occurredAtUtc,
          lastSeenAtUtc: device?.lastSeenAtUtc ?? null,
          lastSeenAtLocal: device?.lastSeenAtLocal ?? null,
          localOffsetSource: latestLocalEvent?.occurredAtLocal ?? null,
          hasRecentActivity: true,
          lockCount: locks.length,
          unlockCount: unlocks.length,
          suspendCount: orderedEvents.filter((event) => event.eventType === 'DeviceSuspended').length,
          resumeCount: orderedEvents.filter((event) => event.eventType === 'DeviceResumed').length,
          idleCount: idleEvents.length,
          latestIdleSeconds,
          locks: locks.slice(0, maxEventsPerKind),
          unlocks: unlocks.slice(0, maxEventsPerKind),
          powerEvents: powerEvents.slice(0, maxEventsPerKind),
          idleEvents: idleEvents.slice(0, maxEventsPerKind),
          sessions: buildActivitySessions(orderedEvents),
        }
      })
      .sort((left, right) => new Date(right.lastEventAtUtc).getTime() - new Date(left.lastEventAtUtc).getTime())

    const inactiveActivities = devices
      .filter((device) => !groups.has(getDeviceKeyFromDevice(device)))
      .map((device): DeviceActivity => ({
        id: getDeviceKeyFromDevice(device),
        deviceId: device.deviceId,
        hostname: device.hostname || device.deviceId || 'sin equipo',
        userName: device.userName || 'Usuario sin nombre',
        email: device.email || 'sin correo',
        operation: resolveDeviceOperation(device),
        lastEventAtUtc: '',
        lastSeenAtUtc: device.lastSeenAtUtc || null,
        lastSeenAtLocal: device.lastSeenAtLocal || null,
        localOffsetSource: null,
        hasRecentActivity: false,
        lockCount: 0,
        unlockCount: 0,
        suspendCount: 0,
        resumeCount: 0,
        idleCount: 0,
        latestIdleSeconds: null,
        locks: [],
        unlocks: [],
        powerEvents: [],
        idleEvents: [],
        sessions: [],
      }))
      .sort((left, right) => new Date(right.lastSeenAtUtc ?? 0).getTime() - new Date(left.lastSeenAtUtc ?? 0).getTime())

    return [...activeActivities, ...inactiveActivities]
  }, [deviceLookup, devices, sessionEvents])

  const stats = useMemo(() => ({
    activeDevices: deviceActivities.filter((device) => device.hasRecentActivity).length,
    inactiveDevices: deviceActivities.filter((device) => !device.hasRecentActivity).length,
    locks: sessionEvents.filter((event) => event.eventType === 'SessionLocked').length,
    unlocks: sessionEvents.filter((event) => event.eventType === 'SessionUnlocked').length,
    idle: sessionEvents.filter((event) => event.eventType === 'SessionLocked' && event.lockReason === 'AutoLock').length,
    power: sessionEvents.filter((event) =>
      event.eventType === 'DeviceSuspended' ||
      event.eventType === 'DeviceResumed' ||
      event.eventType === 'DeviceStarted' ||
      event.eventType === 'DeviceShutdown').length,
  }), [deviceActivities, sessionEvents])

  const filteredDevices = useMemo(() => {
    const searchTokens = getSearchTokens(searchTerm)

    return deviceActivities.filter((device) => {
      if (devicePresenceFilter === 'active' && !device.hasRecentActivity) return false
      if (devicePresenceFilter === 'inactive' && device.hasRecentActivity) return false
      if (devicePresenceFilter === 'active') {
      if (activityFilter === 'locked' && device.lockCount === 0) return false
      if (activityFilter === 'unlocked' && device.unlockCount === 0) return false
      if (activityFilter === 'idle' && device.idleCount === 0) return false
      }
      if (searchTokens.length === 0) return true

      return matchesSearchTokens([device.userName, device.email, device.hostname, device.deviceId, device.operation], searchTokens)
    })
  }, [activityFilter, devicePresenceFilter, searchTerm, deviceActivities])

  useEffect(() => {
    setPage(0)
  }, [activityFilter, devicePresenceFilter, searchTerm])

  useEffect(() => {
    if (filteredDevices.length === 0) {
      setSelectedDeviceId(null)
      return
    }

    if (!selectedDeviceId || !filteredDevices.some((device) => device.id === selectedDeviceId)) {
      setSelectedDeviceId(filteredDevices[0].id)
    }
  }, [filteredDevices, selectedDeviceId])

  const selectedDevice = filteredDevices.find((device) => device.id === selectedDeviceId) ?? null
  const pagedDevices = useMemo(
    () => filteredDevices.slice(page * pageSize, page * pageSize + pageSize),
    [filteredDevices, page],
  )

  const handleSelectDevice = (deviceId: string) => {
    setSelectedDeviceId(deviceId)
    setDrawerOpen(true)
  }

  return (
    <section className="activity-console">
      <Box className="activity-console__metrics activity-console__metrics--presence">
        <MetricCard
          icon={<PersonSearchRoundedIcon />}
          label="Con actividad"
          value={stats.activeDevices.toString()}
          helper={`Con actividad en las ultimas ${lookbackHours} horas`}
          active={devicePresenceFilter === 'active'}
          onClick={() => setDevicePresenceFilter('active')}
        />
        <MetricCard
          icon={<SearchRoundedIcon />}
          label="Sin actividad"
          value={stats.inactiveDevices.toString()}
          helper="Con agente instalado sin actividad reciente"
          active={devicePresenceFilter === 'inactive'}
          onClick={() => setDevicePresenceFilter('inactive')}
        />
      </Box>

      <Box className="activity-console__layout">
        <Paper className="activity-panel activity-panel--main" elevation={0}>
          <Box className="activity-panel__header">
            <Box>
              <Typography className="activity-panel__eyebrow">Actividad de sesion</Typography>
              <Typography className="activity-panel__title">Bloqueos, desbloqueos e inactividad por dispositivo</Typography>
            </Box>
            <Chip
              className="activity-panel__chip"
              label={`${filteredDevices.length} ${devicePresenceFilter === 'active' ? 'con actividad' : 'sin actividad'}`}
            />
          </Box>

          <Box className="activity-toolbar">
            <TextField
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
              placeholder="Buscar usuario, correo, equipo u operacion"
              className="activity-toolbar__search"
              size="small"
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchRoundedIcon fontSize="small" />
                    </InputAdornment>
                  ),
                },
              }}
            />

            <Box className="activity-filter-group">
              <Button className={activityFilter === 'all' ? 'activity-filter activity-filter--active' : 'activity-filter'} onClick={() => setActivityFilter('all')}>
                Todos
              </Button>
              <Button className={activityFilter === 'locked' ? 'activity-filter activity-filter--active' : 'activity-filter'} onClick={() => setActivityFilter('locked')}>
                Bloqueos
              </Button>
              <Button className={activityFilter === 'unlocked' ? 'activity-filter activity-filter--active' : 'activity-filter'} onClick={() => setActivityFilter('unlocked')}>
                Desbloqueos
              </Button>
              <Button className={activityFilter === 'idle' ? 'activity-filter activity-filter--active' : 'activity-filter'} onClick={() => setActivityFilter('idle')}>
                Inactividad
              </Button>
            </Box>
          </Box>

          <TableContainer className="activity-table-wrap">
            <Table className="activity-table" size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Equipo</TableCell>
                  <TableCell>Usuario reciente</TableCell>
                  <TableCell align="right">Bloqueos</TableCell>
                  <TableCell align="right">Desbloqueos</TableCell>
                  <TableCell align="right">Inactividad</TableCell>
                  <TableCell>Idle reciente</TableCell>
                  <TableCell>Ultima Actividad</TableCell>
                  <TableCell>Ultimo check-in</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {filteredDevices.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <Box className="activity-empty">
                        {devicePresenceFilter === 'active'
                          ? `No hay dispositivos con actividad en las ultimas ${lookbackHours} horas que coincidan con el filtro actual.`
                          : 'No hay dispositivos sin actividad que coincidan con el filtro actual.'}
                      </Box>
                    </TableCell>
                  </TableRow>
                ) : (
                  pagedDevices.map((device) => {
                    const latestEvent = [...device.locks, ...device.unlocks, ...device.powerEvents]
                      .sort((left, right) => new Date(right.occurredAtUtc).getTime() - new Date(left.occurredAtUtc).getTime())[0]

                    return (
                      <TableRow
                        key={device.id}
                        hover
                        selected={device.id === selectedDeviceId}
                        className="activity-table__row"
                        onClick={() => handleSelectDevice(device.id)}
                      >
                        <TableCell>
                          <Typography className="activity-hostname">{device.hostname}</Typography>
                          <Typography className="activity-device-count">{device.operation}</Typography>
                        </TableCell>
                        <TableCell>
                          <Typography className="activity-user-name">{device.userName}</Typography>
                          <Typography className="activity-user-email">{device.email}</Typography>
                        </TableCell>
                        <TableCell align="right">
                          <Chip className="activity-count activity-count--lock" label={device.lockCount} size="small" />
                        </TableCell>
                        <TableCell align="right">
                          <Chip className="activity-count activity-count--unlock" label={device.unlockCount} size="small" />
                        </TableCell>
                        <TableCell align="right">
                          <Chip className="activity-count activity-count--lock" label={device.idleCount} size="small" />
                        </TableCell>
                        <TableCell>{formatDuration(device.latestIdleSeconds)}</TableCell>
                        <TableCell>
                          {latestEvent ? (
                            <>
                              <Typography className="activity-event-name">{describeEvent(latestEvent)}</Typography>
                              <Typography className="activity-event-time">{formatDeviceLocalTime(latestEvent)}</Typography>
                            </>
                          ) : 'Sin actividad'}
                        </TableCell>
                        <TableCell>{formatDeviceCheckIn(device)}</TableCell>
                      </TableRow>
                    )
                  })
                )}
              </TableBody>
            </Table>
          </TableContainer>
          <TablePagination
            component="div"
            className="activity-console__pagination"
            count={filteredDevices.length}
            page={page}
            rowsPerPage={pageSize}
            rowsPerPageOptions={[pageSize]}
            onPageChange={(_, nextPage) => setPage(nextPage)}
            labelRowsPerPage="Filas por pagina"
            labelDisplayedRows={({ from, to, count }) => `${from}-${to} de ${count}`}
          />
        </Paper>
      </Box>

      <Drawer anchor="right" open={drawerOpen && selectedDevice !== null} onClose={() => setDrawerOpen(false)} slotProps={{ paper: { className: 'activity-drawer' } }}>
        {selectedDevice ? (
          <Box>
            <Box className="activity-drawer__topbar">
              <Box>
                <Typography className="activity-panel__eyebrow">Detalle de actividad</Typography>
                <Typography className="activity-drawer__title">{selectedDevice.hostname}</Typography>
                <Typography className="activity-drawer__email">{selectedDevice.userName} - {selectedDevice.email}</Typography>
              </Box>
              <Tooltip title="Cerrar">
                <IconButton onClick={() => setDrawerOpen(false)}>
                  <CloseRoundedIcon />
                </IconButton>
              </Tooltip>
            </Box>

            <Box className="activity-drawer__stats">
              <Box className="activity-drawer__stat">
                <span>Bloqueos 48 h</span>
                <strong>{selectedDevice.lockCount}</strong>
              </Box>
              <Box className="activity-drawer__stat">
                <span>Desbloqueos 48 h</span>
                <strong>{selectedDevice.unlockCount}</strong>
              </Box>
              <Box className="activity-drawer__stat">
                <span>Inactividad 48 h</span>
                <strong>{selectedDevice.idleCount}</strong>
              </Box>
              <Box className="activity-drawer__stat">
                <span>Energia 48 h</span>
                <strong>{selectedDevice.suspendCount + selectedDevice.resumeCount}</strong>
              </Box>
              <Box className="activity-drawer__stat">
                <span>Idle reciente</span>
                <strong>{formatDuration(selectedDevice.latestIdleSeconds)}</strong>
              </Box>
              <Box className="activity-drawer__stat">
                <span>Ultimo check-in</span>
                <strong>{formatDeviceCheckIn(selectedDevice)}</strong>
              </Box>
            </Box>

            <Box className="activity-drawer__devices">
              <Typography className="activity-section-label">Dispositivo</Typography>
              <Box className="activity-device-tags">
                <Chip label={selectedDevice.deviceId} size="small" />
                <Chip label={selectedDevice.operation} size="small" />
              </Box>
            </Box>

            <Divider />

            <ActivitySessionTimeline
              title={`Ultimas ${maxEventsPerKind} sesiones`}
              sessions={selectedDevice.sessions}
              emptyText="No hay sesiones de bloqueo y desbloqueo para este dispositivo en la ventana actual."
            />
            <ActivityEventTimeline
              title={`Ultimos ${maxEventsPerKind} eventos de energia`}
              events={selectedDevice.powerEvents}
              emptyText="No hay suspensiones o reanudaciones para este dispositivo en la ventana actual."
            />
          </Box>
        ) : null}
      </Drawer>
    </section>
  )
}
