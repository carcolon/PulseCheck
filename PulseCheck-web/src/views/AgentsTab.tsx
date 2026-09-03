import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import {
  Box,
  Chip,
  Dialog,
  DialogContent,
  DialogTitle,
  Divider,
  Drawer,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography,
} from '@mui/material'
import CloseOutlinedIcon from '@mui/icons-material/CloseOutlined'
import DevicesOutlinedIcon from '@mui/icons-material/DevicesOutlined'
import DomainOutlinedIcon from '@mui/icons-material/DomainOutlined'
import SearchOutlinedIcon from '@mui/icons-material/SearchOutlined'
import VerifiedOutlinedIcon from '@mui/icons-material/VerifiedOutlined'
import type { Device } from '../types'
import { DetailRow } from '../components/ui'

const pageSize = 50

export function AgentsTab({
  devices,
}: {
  devices: Device[]
}) {
  const [searchTerm, setSearchTerm] = useState('')
  const [operationFilter, setOperationFilter] = useState('All')
  const [versionFilter, setVersionFilter] = useState('All')
  const [selectedDevice, setSelectedDevice] = useState<Device | null>(null)
  const [versionsOpen, setVersionsOpen] = useState(false)
  const [page, setPage] = useState(0)

  const operations = useMemo(
    () => uniqueValues(devices.map((device) => getDeviceOperation(device))),
    [devices],
  )
  const versions = useMemo(
    () => uniqueValues(devices.map((device) => device.agentVersion || 'sin versión')),
    [devices],
  )
  const filteredDevices = useMemo(() => {
    const searchTokens = getSearchTokens(searchTerm)

    return devices
      .filter((device) => {
        const operation = getDeviceOperation(device)
        const version = device.agentVersion || 'sin versión'
        if (operationFilter !== 'All' && operation !== operationFilter) return false
        if (versionFilter !== 'All' && version !== versionFilter) return false
        if (searchTokens.length === 0) return true

        return matchesSearchTokens([
          device.userName,
          device.email,
          device.hostname,
          device.deviceId,
          device.operation,
          device.department,
          device.agentVersion,
        ], searchTokens)
      })
      .sort((left, right) => new Date(right.lastSeenAtUtc).getTime() - new Date(left.lastSeenAtUtc).getTime())
  }, [devices, operationFilter, searchTerm, versionFilter])

  const latestVersion = versions
    .filter((version) => version !== 'sin versión')
    .sort(compareVersions)
    .at(-1) ?? 'sin versión'
  const latestVersionCount = devices.filter((device) => (device.agentVersion || 'sin versión') === latestVersion).length
  const versionDistribution = useMemo(
    () =>
      versions
        .map((version) => ({
          version,
          count: devices.filter((device) => (device.agentVersion || 'sin versión') === version).length,
        }))
        .sort((left, right) => compareVersions(right.version, left.version))
        .slice(0, 5),
    [devices, versions],
  )

  useEffect(() => {
    setPage(0)
  }, [operationFilter, searchTerm, versionFilter])

  const pagedDevices = useMemo(
    () => filteredDevices.slice(page * pageSize, page * pageSize + pageSize),
    [filteredDevices, page],
  )

  return (
    <section className="agents-console" data-animate>
      <Box className="agents-console__metrics">
        <MetricTile icon={<DevicesOutlinedIcon />} label="Agentes registrados" value={devices.length.toString()} helper={`${filteredDevices.length} en vista`} />
        <MetricTile icon={<DomainOutlinedIcon />} label="Operaciones" value={operations.length.toString()} helper="con agente instalado" />
        <MetricTile
          icon={<VerifiedOutlinedIcon />}
          label="Última versión"
          value={latestVersion}
          helper={`${latestVersionCount} dispositivo${latestVersionCount === 1 ? '' : 's'}`}
          onClick={() => setVersionsOpen(true)}
        />
      </Box>

      <Paper elevation={0} className="agents-console__panel">
        <Box className="agents-console__toolbar">
          <Box>
            <Typography variant="h5" className="agents-console__title">
              Inventario de agentes
            </Typography>
            <Typography variant="body2" className="agents-console__subtitle">
              Vista operativa para monitorear instalación, versión y asignación por operación.
            </Typography>
          </Box>
          <Chip className="agents-console__result-chip" label={`${filteredDevices.length} resultados`} />
        </Box>

        <Box className="agents-console__filters">
          <Box className="agents-console__search">
            <TextField
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
              placeholder="Buscar usuario, correo, equipo o versión"
              size="small"
              fullWidth
              slotProps={{
                input: {
                  startAdornment: <SearchOutlinedIcon sx={{ mr: 1, color: '#78909a' }} />,
                },
              }}
            />
          </Box>
          <Box className="agents-console__filter-group">
            <FormControl size="small" className="agents-console__select">
              <InputLabel>Operación</InputLabel>
              <Select label="Operación" value={operationFilter} onChange={(event) => setOperationFilter(event.target.value)}>
                <MenuItem value="All">Todas</MenuItem>
                {operations.map((operation) => (
                  <MenuItem key={operation} value={operation}>{operation}</MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl size="small" className="agents-console__select agents-console__select--version">
              <InputLabel>Versión</InputLabel>
              <Select label="Versión" value={versionFilter} onChange={(event) => setVersionFilter(event.target.value)}>
                <MenuItem value="All">Todas</MenuItem>
                {versions.map((version) => (
                  <MenuItem key={version} value={version}>{version}</MenuItem>
                ))}
              </Select>
            </FormControl>
          </Box>
        </Box>

        {devices.length === 0 ? (
          <EmptyState />
        ) : (
          <>
            <TableContainer className="agents-console__table-wrap">
              <Table className="agents-console__table" size="small" stickyHeader>
                <TableHead>
                  <TableRow>
                    <TableCell>Usuario</TableCell>
                    <TableCell>Equipo</TableCell>
                    <TableCell>Versión</TableCell>
                    <TableCell>Operación</TableCell>
                    <TableCell>Último check-in</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {pagedDevices.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={5}>
                        <Box className="agents-console__empty-row">No hay agentes que coincidan con los filtros actuales.</Box>
                      </TableCell>
                    </TableRow>
                  ) : (
                    pagedDevices.map((device) => (
                      <TableRow
                        key={device.deviceId}
                        hover
                        className="agents-console__row"
                        onClick={() => setSelectedDevice(device)}
                      >
                        <TableCell>
                          <Typography className="agents-console__primary">{device.userName || 'Sin usuario'}</Typography>
                          <Typography className="agents-console__secondary">{device.email || 'sin correo'}</Typography>
                        </TableCell>
                        <TableCell>
                          <Typography className="agents-console__primary">{device.hostname || device.deviceId}</Typography>
                          <Typography className="agents-console__secondary">{device.operatingSystem || 'SO no reportado'}</Typography>
                        </TableCell>
                        <TableCell>
                          <Chip size="small" variant="outlined" label={device.agentVersion || 'sin versión'} className="agents-console__version-chip" />
                        </TableCell>
                        <TableCell>{getDeviceOperation(device)}</TableCell>
                        <TableCell className="agents-console__muted">{formatDateTime(device.lastSeenAtUtc)}</TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </TableContainer>
            <TablePagination
              component="div"
              className="agents-console__pagination"
              count={filteredDevices.length}
              page={page}
              rowsPerPage={pageSize}
              rowsPerPageOptions={[pageSize]}
              onPageChange={(_, nextPage) => setPage(nextPage)}
              labelRowsPerPage="Filas por página"
              labelDisplayedRows={({ from, to, count }) => `${from}-${to} de ${count}`}
            />
          </>
        )}
      </Paper>

      <Drawer anchor="right" open={Boolean(selectedDevice)} onClose={() => setSelectedDevice(null)}>
        {selectedDevice ? (
          <Box className="agent-detail-drawer">
            <Box className="agent-detail-drawer__header">
              <Box>
                <Typography variant="h6" className="agent-detail-drawer__title">
                  {selectedDevice.hostname || selectedDevice.deviceId}
                </Typography>
                <Typography variant="body2" className="agent-detail-drawer__subtitle">
                  {selectedDevice.userName || 'Sin usuario asignado'}
                </Typography>
              </Box>
              <IconButton onClick={() => setSelectedDevice(null)} aria-label="Cerrar detalle">
                <CloseOutlinedIcon />
              </IconButton>
            </Box>

            <Divider />

            <Box className="agent-detail-drawer__section">
              <DetailRow label="Usuario" value={selectedDevice.userName || 'Sin usuario'} />
              <DetailRow label="Correo" value={selectedDevice.email || 'sin correo'} />
              <DetailRow label="Operación" value={getDeviceOperation(selectedDevice)} />
            </Box>

            <Box className="agent-detail-drawer__section">
              <DetailRow label="Equipo" value={selectedDevice.hostname || selectedDevice.deviceId} />
              <DetailRow label="Device ID" value={selectedDevice.deviceId} />
              <DetailRow label="Sistema operativo" value={selectedDevice.operatingSystem || 'SO no reportado'} />
              <DetailRow label="Versión del agente" value={selectedDevice.agentVersion || 'sin versión'} />
              <DetailRow label="Último check-in" value={formatDateTime(selectedDevice.lastSeenAtUtc)} />
            </Box>
          </Box>
        ) : null}
      </Drawer>

      <Dialog open={versionsOpen} onClose={() => setVersionsOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle className="agent-version-dialog__title">
          Últimas 5 versiones del agente
          <IconButton aria-label="Cerrar versiones" onClick={() => setVersionsOpen(false)}>
            <CloseOutlinedIcon />
          </IconButton>
        </DialogTitle>
        <DialogContent>
          <Box className="agent-version-list">
            {versionDistribution.length === 0 ? (
              <Box className="agents-console__empty-row">No hay versiones reportadas todavía.</Box>
            ) : (
              versionDistribution.map((item, index) => (
                <Box key={item.version} className="agent-version-row">
                  <Box>
                    <Typography className="agent-version-row__rank">#{index + 1}</Typography>
                    <Typography className="agent-version-row__version">{item.version}</Typography>
                  </Box>
                  <Chip
                    className={item.version === latestVersion ? 'agent-version-row__chip agent-version-row__chip--latest' : 'agent-version-row__chip'}
                    label={`${item.count} dispositivo${item.count === 1 ? '' : 's'}`}
                  />
                </Box>
              ))
            )}
          </Box>
        </DialogContent>
      </Dialog>
    </section>
  )
}

function MetricTile({
  icon,
  label,
  value,
  helper,
  onClick,
}: {
  icon: ReactNode
  label: string
  value: string
  helper: string
  onClick?: () => void
}) {
  return (
    <Paper
      elevation={0}
      className={onClick ? 'agents-metric agents-metric--clickable' : 'agents-metric'}
      component={onClick ? 'button' : 'div'}
      type={onClick ? 'button' : undefined}
      onClick={onClick}
    >
      <Box className="agents-metric__icon">{icon}</Box>
      <Box>
        <Typography className="agents-metric__label">{label}</Typography>
        <Typography className="agents-metric__value">{value}</Typography>
        <Typography className="agents-metric__helper">{helper}</Typography>
      </Box>
    </Paper>
  )
}

function uniqueValues(values: string[]) {
  return Array.from(new Set(values.map((value) => value.trim()).filter(Boolean)))
    .sort((left, right) => left.localeCompare(right))
}

function getDeviceOperation(device: Device) {
  return device.operation || device.department || 'Sin operación'
}

function compareVersions(left: string, right: string) {
  return left.localeCompare(right, undefined, { numeric: true, sensitivity: 'base' })
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

function formatDateTime(value: string) {
  if (!value) {
    return 'Sin actividad'
  }

  return new Date(value).toLocaleString()
}

function EmptyState() {
  return (
    <div className="rounded-lg border border-[#d7e8ee] bg-[#f8fcfd] px-3 py-8 text-center text-sm text-[#5f7782]">
      No hay agentes registrados todavía.
    </div>
  )
}
