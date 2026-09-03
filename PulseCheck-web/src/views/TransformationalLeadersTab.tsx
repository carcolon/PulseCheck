import { useMemo, useState } from 'react'
import {
  Box,
  Checkbox,
  Chip,
  CircularProgress,
  FormControl,
  IconButton,
  ListItemText,
  MenuItem,
  Select,
  Tooltip,
} from '@mui/material'
import type { SelectChangeEvent } from '@mui/material/Select'
import CheckOutlinedIcon from '@mui/icons-material/CheckOutlined'
import ClearOutlinedIcon from '@mui/icons-material/ClearOutlined'
import type { TransformationalLeaderCandidate, TransformationalLeaderOptions } from '../types'
import { SoftButton, StatusChip, inputClassName } from '../components/ui'

export function TransformationalLeadersTab({
  options,
  savingSolvoId,
  onSaveAssignment,
  onClearAssignment,
}: {
  options: TransformationalLeaderOptions
  savingSolvoId: string | null
  onSaveAssignment: (solvoId: string, operations: string[]) => Promise<void>
  onClearAssignment: (solvoId: string) => Promise<void>
}) {
  const [search, setSearch] = useState('')
  const [operationFilter, setOperationFilter] = useState('')
  const [assignmentFilter, setAssignmentFilter] = useState<'all' | 'assigned' | 'unassigned'>('all')
  const [draftAssignments, setDraftAssignments] = useState<Record<string, string[]>>({})

  const assignedCount = options.leaders.filter((leader) => getAssignedOperations(leader).length > 0).length
  const unassignedCount = Math.max(0, options.leaders.length - assignedCount)

  const filteredLeaders = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase()
    return options.leaders.filter((leader) => {
      const effectiveOperations = getEffectiveOperations(leader, draftAssignments)
      if (operationFilter && !effectiveOperations.some((operation) => operation === operationFilter)) {
        return false
      }

      if (assignmentFilter === 'assigned' && effectiveOperations.length === 0) {
        return false
      }

      if (assignmentFilter === 'unassigned' && effectiveOperations.length > 0) {
        return false
      }

      if (!normalizedSearch) {
        return true
      }

      return [
        leader.fullName,
        leader.solvoId,
        leader.corporateEmail,
        leader.currentOperation,
        leader.client,
        leader.department,
      ].join(' ').toLowerCase().includes(normalizedSearch)
    })
  }, [assignmentFilter, draftAssignments, operationFilter, options.leaders, search])

  const coverage = useMemo(() => {
    const counts = new Map<string, number>()
    for (const leader of options.leaders) {
      const operations = getAssignedOperations(leader)
      if (operations.length === 0) {
        counts.set('Sin asignar', (counts.get('Sin asignar') ?? 0) + 1)
        continue
      }

      for (const operation of operations) {
        counts.set(operation, (counts.get(operation) ?? 0) + 1)
      }
    }

    return Array.from(counts.entries())
      .map(([operation, count]) => ({ operation, count }))
      .sort((left, right) => {
        if (left.operation === 'Sin asignar') return 1
        if (right.operation === 'Sin asignar') return -1
        return left.operation.localeCompare(right.operation)
      })
  }, [options.leaders])

  function updateDraft(solvoId: string, operations: string[]) {
    setDraftAssignments((current) => ({ ...current, [solvoId]: uniqueOperations(operations) }))
  }

  async function saveDraft(leader: TransformationalLeaderCandidate) {
    const operations = getEffectiveOperations(leader, draftAssignments)
    if (operations.length === 0) {
      return
    }

    await onSaveAssignment(leader.solvoId, operations)
    setDraftAssignments((current) => {
      const next = { ...current }
      delete next[leader.solvoId]
      return next
    })
  }

  async function clearAssignment(leader: TransformationalLeaderCandidate) {
    await onClearAssignment(leader.solvoId)
    setDraftAssignments((current) => {
      const next = { ...current }
      delete next[leader.solvoId]
      return next
    })
  }

  return (
    <section className="mt-4 grid gap-4 font-sans">
      <article className="glass-panel p-5">
        <div className="border-b border-[#d7e8ee] pb-5">
          <h2 className="font-display text-3xl font-black leading-tight text-[#0d3140]">TL Activos</h2>
        </div>

        <div className="mt-5 grid gap-3 md:grid-cols-4">
          <Metric label="Candidatos TL" value={options.leaders.length} />
          <Metric label="Asignados" value={assignedCount} />
          <Metric label="Sin operacion" value={unassignedCount} />
          <Metric label="Operaciones" value={options.operations.length} />
        </div>

        <div className="mt-5 overflow-hidden rounded-[24px] border border-[#d7e8ee] bg-white shadow-[0_16px_34px_rgba(13,49,64,0.06)]">
          <div className="flex flex-col gap-3 border-b border-[#d7e8ee] p-4 lg:flex-row lg:items-start lg:justify-between">
            <div>
              <p className="text-xs font-black uppercase tracking-[0.18em] text-[#00758d]">People source</p>
              <h3 className="mt-2 text-lg font-black text-[#0d3140]">TL activos</h3>
            </div>
            <SoftButton type="button" size="small" onClick={() => exportLeadersCsv(filteredLeaders, draftAssignments)}>
              Exportar vista
            </SoftButton>
          </div>

          <div className="grid gap-3 border-b border-[#d7e8ee] bg-[#fbfdfe] p-4 lg:grid-cols-[minmax(260px,1fr)_minmax(180px,260px)_minmax(150px,220px)]">
            <label className="grid gap-1.5">
              <span className="text-[11px] font-black uppercase tracking-[0.14em] text-[#6b8590]">Buscar persona</span>
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                className={inputClassName}
                placeholder="Nombre, Solvo ID o email"
              />
            </label>
            <label className="grid gap-1.5">
              <span className="text-[11px] font-black uppercase tracking-[0.14em] text-[#6b8590]">Operacion</span>
              <select value={operationFilter} onChange={(event) => setOperationFilter(event.target.value)} className={inputClassName}>
                <option value="">Todas las operaciones</option>
                {options.operations.map((operation) => (
                  <option key={operation} value={operation}>{operation}</option>
                ))}
              </select>
            </label>
            <label className="grid gap-1.5">
              <span className="text-[11px] font-black uppercase tracking-[0.14em] text-[#6b8590]">Asignacion</span>
              <select value={assignmentFilter} onChange={(event) => setAssignmentFilter(event.target.value as typeof assignmentFilter)} className={inputClassName}>
                <option value="all">Todos</option>
                <option value="assigned">Asignados</option>
                <option value="unassigned">Sin asignar</option>
              </select>
            </label>
          </div>

          <div className="border-b border-[#d7e8ee] bg-white p-4">
            <div className="mb-3">
              <p className="text-xs font-black uppercase tracking-[0.18em] text-[#00758d]">Cobertura</p>
              <h3 className="mt-1 text-base font-black text-[#0d3140]">TL por operacion</h3>
            </div>
            {coverage.length === 0 ? (
              <p className="rounded-2xl border border-dashed border-[#cfe1e8] bg-[#f8fcfd] px-4 py-5 text-sm font-semibold text-[#607985]">
                Todavia no hay TL activos para mostrar.
              </p>
            ) : (
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4">
                {coverage.map((item) => (
                  <div key={item.operation} className="min-w-0 rounded-[18px] border border-[#d7e8ee] bg-[#f8fcfd] p-3 shadow-[0_8px_18px_rgba(9,55,69,0.04)]">
                    <div className="flex items-center justify-between gap-3">
                      <strong className="min-w-0 truncate text-sm text-[#12394a]">{item.operation}</strong>
                      <span className="shrink-0 rounded-full border border-[#b9dce5] bg-[#e9fbfd] px-2.5 py-1 text-xs font-black text-[#607985]">{item.count}</span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="overflow-x-auto">
            <table className="w-full table-fixed border-collapse">
              <thead>
                <tr className="bg-[#f4fbfd] text-left text-[11px] font-black uppercase tracking-[0.14em] text-[#6b8590]">
                  <th className="w-[30%] border-b border-[#d7e8ee] px-4 py-3">Persona</th>
                  <th className="w-[13%] border-b border-[#d7e8ee] px-4 py-3">Solvo ID</th>
                  <th className="w-[12%] border-b border-[#d7e8ee] px-4 py-3">Job title code</th>
                  <th className="w-[11%] border-b border-[#d7e8ee] px-4 py-3">Status</th>
                  <th className="w-[24%] border-b border-[#d7e8ee] px-4 py-3">Operacion asignada</th>
                  <th className="w-[10%] border-b border-[#d7e8ee] px-4 py-3"></th>
                </tr>
              </thead>
              <tbody>
                {filteredLeaders.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="px-4 py-8 text-center text-sm font-semibold text-[#607985]">
                      No hay TL activos que coincidan con los filtros.
                    </td>
                  </tr>
                ) : filteredLeaders.map((leader) => {
                  const effectiveOperations = getEffectiveOperations(leader, draftAssignments)
                  const isSaving = savingSolvoId === leader.solvoId
                  return (
                    <tr key={leader.solvoId} className="border-b border-[#e1edf2] align-middle">
                      <td className="px-4 py-3">
                        <div className="grid grid-cols-[38px_minmax(0,1fr)] items-center gap-3">
                          <div className="grid h-9 w-9 place-items-center rounded-2xl bg-[#eff9fc] text-xs font-black text-[#00758d]">
                            {getInitials(leader.fullName || leader.solvoId)}
                          </div>
                          <div className="min-w-0">
                            <p className="truncate text-sm font-black text-[#12394a]">{leader.fullName || 'Sin nombre'}</p>
                            <p className="truncate text-xs font-semibold text-[#66808c]">{leader.corporateEmail || 'Sin correo'}</p>
                          </div>
                        </div>
                      </td>
                      <td className="truncate px-4 py-3 text-sm font-bold text-[#274957]">{leader.solvoId}</td>
                      <td className="truncate px-4 py-3 text-sm font-bold text-[#274957]">{leader.jobTitleCode}</td>
                      <td className="px-4 py-3"><StatusChip tone="active">{leader.status}</StatusChip></td>
                      <td className="px-4 py-3">
                        <OperationMultiSelect
                          operations={options.operations}
                          value={effectiveOperations}
                          onChange={(operations) => updateDraft(leader.solvoId, operations)}
                        />
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center justify-end gap-2">
                          <Tooltip title="Guardar asignacion">
                            <span>
                              <IconButton
                                size="small"
                                onClick={() => void saveDraft(leader)}
                                disabled={isSaving || effectiveOperations.length === 0}
                                sx={{
                                  border: '1px solid #b9dce5',
                                  borderRadius: 2.5,
                                  color: '#00758d',
                                  bgcolor: '#eff9fc',
                                  transition: 'transform 160ms ease, box-shadow 160ms ease, background-color 160ms ease',
                                  '&:hover': {
                                    bgcolor: '#e4f7fb',
                                    boxShadow: '0 10px 20px rgba(9,55,69,0.08)',
                                    transform: 'translateY(-1px)',
                                  },
                                }}
                              >
                                {isSaving ? <CircularProgress size={16} /> : <CheckOutlinedIcon fontSize="small" />}
                              </IconButton>
                            </span>
                          </Tooltip>
                          <Tooltip title="Quitar asignacion">
                            <span>
                              <IconButton
                                size="small"
                                onClick={() => void clearAssignment(leader)}
                                disabled={isSaving || getAssignedOperations(leader).length === 0}
                                sx={{
                                  border: '1px solid #d7e8ee',
                                  borderRadius: 2.5,
                                  color: '#607985',
                                  bgcolor: '#ffffff',
                                  transition: 'transform 160ms ease, box-shadow 160ms ease, background-color 160ms ease',
                                  '&:hover': {
                                    bgcolor: '#f6fbfd',
                                    boxShadow: '0 10px 20px rgba(9,55,69,0.08)',
                                    transform: 'translateY(-1px)',
                                  },
                                }}
                              >
                                <ClearOutlinedIcon fontSize="small" />
                              </IconButton>
                            </span>
                          </Tooltip>
                        </div>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </div>
      </article>
    </section>
  )
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-[20px] border border-[#d7e8ee] bg-[#f8fcfd] p-4 shadow-[0_10px_24px_rgba(9,55,69,0.05)]">
      <p className="text-[11px] font-black uppercase tracking-[0.16em] text-[#6b8590]">{label}</p>
      <strong className="mt-3 block font-display text-3xl font-black text-[#0d3140]">{value}</strong>
    </div>
  )
}

function OperationMultiSelect({
  operations,
  value,
  onChange,
}: {
  operations: string[]
  value: string[]
  onChange: (operations: string[]) => void
}) {
  function handleChange(event: SelectChangeEvent<string[]>) {
    const nextValue = event.target.value
    onChange(typeof nextValue === 'string' ? nextValue.split(',') : nextValue)
  }

  return (
    <FormControl fullWidth size="small">
      <Select
        multiple
        displayEmpty
        value={value}
        onChange={handleChange}
        renderValue={(selected) => {
          const selectedOperations = selected as string[]
          if (selectedOperations.length === 0) {
            return <span className="text-sm font-bold text-[#7d949d]">Sin asignar</span>
          }

          return (
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, maxHeight: 64, overflow: 'hidden' }}>
              {selectedOperations.slice(0, 2).map((operation) => (
                <Chip key={operation} label={operation} size="small" />
              ))}
              {selectedOperations.length > 2 ? <Chip label={`+${selectedOperations.length - 2}`} size="small" /> : null}
            </Box>
          )
        }}
        sx={{
          bgcolor: '#fff',
          borderRadius: '16px',
          fontWeight: 700,
          minHeight: 44,
          '& .MuiOutlinedInput-notchedOutline': { borderColor: '#b9dce5' },
          '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: '#86c7d5' },
        }}
        MenuProps={{
          slotProps: {
            paper: {
              sx: {
                maxHeight: 320,
                mt: 0.5,
                border: '1px solid #d7e8ee',
                boxShadow: '0 18px 38px rgba(13,49,64,0.12)',
              },
            },
          },
        }}
      >
        {operations.map((operation) => (
          <MenuItem key={operation} value={operation}>
            <Checkbox checked={value.includes(operation)} />
            <ListItemText primary={operation} />
          </MenuItem>
        ))}
      </Select>
    </FormControl>
  )
}

function getAssignedOperations(leader: TransformationalLeaderCandidate) {
  return uniqueOperations(leader.assignedOperations?.length ? leader.assignedOperations : [leader.assignedOperation])
}

function getEffectiveOperations(leader: TransformationalLeaderCandidate, drafts: Record<string, string[]>) {
  return drafts[leader.solvoId] ?? getAssignedOperations(leader)
}

function uniqueOperations(values: string[]) {
  return Array.from(new Set(values.map((value) => value.trim()).filter(Boolean)))
}

function getInitials(value: string) {
  return value
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((item) => item.slice(0, 1).toUpperCase())
    .join('')
}

function exportLeadersCsv(leaders: TransformationalLeaderCandidate[], drafts: Record<string, string[]>) {
  const rows = [
    ['Full name', 'Corporate email', 'Solvo ID', 'Job title code', 'Status', 'Current operation', 'Assigned operation', 'Client', 'Department'],
    ...leaders.map((leader) => [
      leader.fullName,
      leader.corporateEmail,
      leader.solvoId,
      leader.jobTitleCode,
      leader.status,
      leader.currentOperation,
      getEffectiveOperations(leader, drafts).join(', '),
      leader.client,
      leader.department,
    ]),
  ]
  const csv = rows.map((row) => row.map(escapeCsvValue).join(',')).join('\r\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `pulsecheck-tl-activos-${new Date().toISOString().slice(0, 10)}.csv`
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

function escapeCsvValue(value: string) {
  return `"${value.replaceAll('"', '""')}"`
}
