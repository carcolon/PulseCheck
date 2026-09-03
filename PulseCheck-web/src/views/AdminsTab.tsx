import { useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { IconButton, Menu, MenuItem, Tooltip } from '@mui/material'
import MoreVertOutlinedIcon from '@mui/icons-material/MoreVertOutlined'
import type { AdminAccount, AdminRole } from '../types'
import { DetailRow, ModalShell, PrimaryButton, StatusChip, inputClassName } from '../components/ui'
import { formatAdminRoles, hasAdminRole, parseAdminRoles } from '../utils/adminRoles'

type AdminFilter = 'all' | 'active' | 'pending' | 'inactive'

export function AdminsTab({
  admins,
  isCreatingAdmin,
  deletingAdminId,
  updatingAdminId,
  onCreateAdmin,
  onDeleteAdmin,
  onUpdateAdminStatus,
}: {
  admins: AdminAccount[]
  isCreatingAdmin: boolean
  deletingAdminId: string | null
  updatingAdminId: string | null
  onCreateAdmin: (email: string, roles: AdminRole[]) => Promise<void>
  onDeleteAdmin: (id: string) => Promise<void>
  onUpdateAdminStatus: (id: string, isActive: boolean) => Promise<void>
}) {
  const [email, setEmail] = useState('')
  const [selectedRoles, setSelectedRoles] = useState<AdminRole[]>(['HRAdmin'])
  const [searchTerm, setSearchTerm] = useState('')
  const [filter, setFilter] = useState<AdminFilter>('all')
  const [isInviteOpen, setIsInviteOpen] = useState(false)
  const [selectedAdmin, setSelectedAdmin] = useState<AdminAccount | null>(null)

  const activeAdmins = admins.filter((admin) => admin.isActive)
  const pendingAdmins = admins.filter((admin) => admin.isActive && !admin.lastLoginAtUtc)
  const lastLogin = admins
    .filter((admin) => admin.lastLoginAtUtc)
    .sort((left, right) => new Date(right.lastLoginAtUtc ?? 0).getTime() - new Date(left.lastLoginAtUtc ?? 0).getTime())[0]

  const filteredAdmins = useMemo(() => {
    const normalizedSearch = searchTerm.trim().toLowerCase()
    return admins.filter((admin) => {
      if (filter === 'active' && !admin.isActive) return false
      if (filter === 'pending' && (!admin.isActive || admin.lastLoginAtUtc)) return false
      if (filter === 'inactive' && admin.isActive) return false
      if (!normalizedSearch) return true
      return `${admin.displayName} ${admin.email} ${admin.authenticationMode} ${formatAdminRoles(admin.role)}`.toLowerCase().includes(normalizedSearch)
    })
  }, [admins, filter, searchTerm])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await onCreateAdmin(email, selectedRoles)
    setEmail('')
    setSelectedRoles(['HRAdmin'])
    setIsInviteOpen(false)
  }

  function requestDelete(admin: AdminAccount) {
    if (window.confirm(`Eliminar admin ${admin.email}?`)) {
      void onDeleteAdmin(admin.id)
    }
  }

  return (
    <section className="font-sans">
      <div className="overflow-hidden rounded-[22px] border border-[#cfe1e8] bg-white shadow-[0_18px_44px_rgba(9,55,69,0.08)]">
        <div className="flex flex-col gap-4 border-b border-[#d8e8ee] bg-white pb-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="px-5 pt-5">
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#00758d]">Control de acceso</p>
            <h2 className="mt-1 text-xl font-bold text-[#0d3140]">Accesos administrativos</h2>
            <p className="mt-1 max-w-2xl text-sm text-[#5f7782]">
              Controla quien puede acceder al panel administrativo con Microsoft Entra ID.
            </p>
          </div>
          <div className="flex shrink-0 flex-wrap gap-2 px-5 lg:pt-5">
            <PrimaryButton type="button" onClick={() => setIsInviteOpen(true)} size="small">
              Invitar admin
            </PrimaryButton>
          </div>
        </div>

        <div className="grid gap-0 border-b border-[#d8e8ee] bg-[#f7fbfd] md:grid-cols-3">
          <AdminSummaryItem label="Activos" value={activeAdmins.length.toString()} context={`${admins.length} registrados`} />
          <AdminSummaryItem label="Ultimo ingreso" value={lastLogin ? formatCompactDate(lastLogin.lastLoginAtUtc) : 'Sin ingresos'} context={lastLogin?.email ?? 'Sin actividad registrada'} />
          <AdminSummaryItem label="Pendientes" value={pendingAdmins.length.toString()} context="Activos sin primer acceso" />
        </div>

        <div className="flex flex-col gap-3 px-5 py-4 xl:flex-row xl:items-center xl:justify-between">
          <p className="text-sm font-semibold text-[#17313c]">{filteredAdmins.length} admins autorizados</p>
          <div className="grid gap-2 sm:grid-cols-[minmax(220px,320px)_150px]">
            <input
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
              className={inputClassName}
              placeholder="Buscar por nombre o correo"
            />
            <select value={filter} onChange={(event) => setFilter(event.target.value as AdminFilter)} className={inputClassName}>
              <option value="all">Todos</option>
              <option value="active">Activos</option>
              <option value="pending">Pendientes</option>
              <option value="inactive">Inactivos</option>
            </select>
          </div>
        </div>

        <div className="border-t border-[#d3e5ec] bg-white">
          {filteredAdmins.length === 0 ? (
            <div className="px-4 py-8 text-center text-sm text-[#5f7782]">
              No hay administradores para este filtro.
            </div>
          ) : (
            <div className="w-full">
              <table className="w-full table-fixed border-collapse text-left">
                <colgroup>
                  <col className="w-[18%]" />
                  <col className="w-[24%]" />
                  <col className="w-[11%]" />
                  <col className="w-[9%]" />
                  <col className="w-[8%]" />
                  <col className="w-[12%]" />
                  <col className="w-[8%]" />
                  <col className="w-[10%]" />
                </colgroup>
                <thead>
                  <tr className="border-b border-[#d3e5ec] bg-[#f7fbfd] text-[11px] font-semibold uppercase tracking-[0.12em] text-[#6b828d]">
                    <th className="px-3 py-3">Admin</th>
                    <th className="px-3 py-3">Correo</th>
                    <th className="px-3 py-3">Metodo</th>
                    <th className="px-3 py-3">Rol</th>
                    <th className="px-3 py-3">Estado</th>
                    <th className="px-3 py-3">Ultimo ingreso</th>
                    <th className="px-3 py-3">Creado</th>
                    <th className="px-3 py-3 text-right">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredAdmins.map((admin) => (
                    <tr key={admin.id} className="border-b border-[#e4f0f4] text-sm last:border-b-0 hover:bg-[#fbfdfe]">
                      <td className="px-3 py-3 align-middle">
                        <p className="truncate font-semibold text-[#17313c]">{admin.displayName || admin.email}</p>
                      </td>
                      <td className="px-3 py-3 align-middle text-[#4f6f7b]">
                        <p className="truncate">{admin.email}</p>
                      </td>
                      <td className="px-3 py-3 align-middle">
                        <StatusChip>{admin.authenticationMode === 'Entra' ? 'Microsoft Entra' : admin.authenticationMode}</StatusChip>
                      </td>
                      <td className="px-3 py-3 align-middle">
                        <StatusChip tone={hasAdminRole(admin.role, 'Owner') ? 'active' : 'neutral'}>{formatAdminRoles(admin.role)}</StatusChip>
                      </td>
                      <td className="px-3 py-3 align-middle">
                        <StatusChip tone={admin.isActive ? 'active' : 'inactive'}>{admin.isActive ? 'Activo' : 'Inactivo'}</StatusChip>
                      </td>
                      <td className="px-3 py-3 align-middle text-[#4f6f7b]">{admin.lastLoginAtUtc ? formatCompactDate(admin.lastLoginAtUtc) : 'Pendiente'}</td>
                      <td className="px-3 py-3 align-middle text-[#4f6f7b]">{formatShortDate(admin.createdAtUtc)}</td>
                      <td className="px-3 py-3 align-middle">
                        <div className="flex justify-end">
                          <AdminActions
                            admin={admin}
                            deletingAdminId={deletingAdminId}
                            updatingAdminId={updatingAdminId}
                            onView={() => setSelectedAdmin(admin)}
                            onDelete={() => requestDelete(admin)}
                            onUpdateAdminStatus={onUpdateAdminStatus}
                          />
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {isInviteOpen ? (
        <AdminInviteDialog
          email={email}
          selectedRoles={selectedRoles}
          isSaving={isCreatingAdmin}
          onEmailChange={setEmail}
          onSelectedRolesChange={setSelectedRoles}
          onClose={() => setIsInviteOpen(false)}
          onSubmit={handleSubmit}
        />
      ) : null}

      {selectedAdmin ? (
        <AdminDetailDialog admin={selectedAdmin} onClose={() => setSelectedAdmin(null)} />
      ) : null}
    </section>
  )
}

function AdminSummaryItem({ label, value, context }: { label: string; value: string; context: string }) {
  return (
    <article className="border-b border-[#d8e8ee] px-5 py-3 last:border-b-0 md:border-b-0 md:border-r md:last:border-r-0">
      <p className="text-xs font-semibold uppercase tracking-[0.14em] text-[#6b828d]">{label}</p>
      <p className="mt-1 truncate text-lg font-bold text-[#0d3140]">{value}</p>
      <p className="mt-1 truncate text-xs text-[#6b828d]">{context}</p>
    </article>
  )
}

function AdminActions({
  admin,
  deletingAdminId,
  updatingAdminId,
  onView,
  onDelete,
  onUpdateAdminStatus,
}: {
  admin: AdminAccount
  deletingAdminId: string | null
  updatingAdminId: string | null
  onView: () => void
  onDelete: () => void
  onUpdateAdminStatus: (id: string, isActive: boolean) => Promise<void>
}) {
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null)
  const isBusy = deletingAdminId === admin.id || updatingAdminId === admin.id

  function closeMenu() {
    setMenuAnchor(null)
  }

  return (
    <>
      <Tooltip title="Acciones">
        <span>
          <IconButton
            size="small"
            disabled={isBusy}
            onClick={(event) => setMenuAnchor(event.currentTarget)}
            aria-label={`Acciones para ${admin.email}`}
            sx={{
              border: '1px solid #bfd5dd',
              color: '#2f4d5b',
              backgroundColor: '#fff',
              '&:hover': { backgroundColor: '#f6fbfd', borderColor: '#8fb7c5' },
            }}
          >
            <MoreVertOutlinedIcon fontSize="small" />
          </IconButton>
        </span>
      </Tooltip>
      <Menu
        anchorEl={menuAnchor}
        open={Boolean(menuAnchor)}
        onClose={closeMenu}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        <MenuItem
          onClick={() => {
            closeMenu()
            onView()
          }}
        >
          Ver detalle
        </MenuItem>
        <MenuItem
          disabled={updatingAdminId === admin.id}
          onClick={() => {
            closeMenu()
            void onUpdateAdminStatus(admin.id, !admin.isActive)
          }}
        >
          {admin.isActive ? 'Desactivar acceso' : 'Activar acceso'}
        </MenuItem>
        <MenuItem
          disabled={deletingAdminId === admin.id}
          onClick={() => {
            closeMenu()
            onDelete()
          }}
          sx={{ color: '#b54708' }}
        >
          Eliminar admin
        </MenuItem>
      </Menu>
    </>
  )
}

function AdminInviteDialog({
  email,
  selectedRoles,
  isSaving,
  onEmailChange,
  onSelectedRolesChange,
  onClose,
  onSubmit,
}: {
  email: string
  selectedRoles: AdminRole[]
  isSaving: boolean
  onEmailChange: (value: string) => void
  onSelectedRolesChange: (value: AdminRole[]) => void
  onClose: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}) {
  const isOwnerSelected = selectedRoles.includes('Owner')

  function toggleRole(role: AdminRole) {
    if (role === 'Owner') {
      onSelectedRolesChange(isOwnerSelected ? ['HRAdmin'] : ['Owner'])
      return
    }

    const nextRoles = selectedRoles
      .filter((item) => item !== 'Owner')
      .filter((item) => item !== role)
    if (!selectedRoles.includes(role)) {
      nextRoles.push(role)
    }

    onSelectedRolesChange(nextRoles.length === 0 ? ['HRAdmin'] : parseAdminRoles(nextRoles.join(',')))
  }

  return (
    <ModalShell eyebrow="Nuevo acceso" title="Invitar admin" onClose={onClose} maxWidthClassName="max-w-xl">
      <form onSubmit={onSubmit}>
        <label className="mt-5 block text-xs font-semibold uppercase tracking-[0.14em] text-[#5f7782]" htmlFor="admin-email">
          Correo corporativo
        </label>
        <input
          id="admin-email"
          type="email"
          value={email}
          onChange={(event) => onEmailChange(event.target.value)}
          className={`${inputClassName} mt-2`}
          placeholder="nombre.apellido@solvoglobal.com"
          required
        />
        <p className="mt-3 text-sm leading-6 text-[#5f7782]">
          El usuario se guarda como administrador Entra activo. Nombre, tenant y object id se completan al iniciar sesion.
        </p>
        <div className="mt-5">
          <p className="text-xs font-semibold uppercase tracking-[0.14em] text-[#5f7782]">Rol de acceso</p>
          <div className="mt-3 grid gap-3 md:grid-cols-3">
            <RoleOption
              title="Owner"
              description="Acceso total, incluida configuracion y administradores."
              checked={isOwnerSelected}
              onChange={() => toggleRole('Owner')}
            />
            <RoleOption
              title="HR Admin"
              description="Campanas, insights y exportacion de respuestas."
              checked={!isOwnerSelected && selectedRoles.includes('HRAdmin')}
              disabled={isOwnerSelected}
              onChange={() => toggleRole('HRAdmin')}
            />
            <RoleOption
              title="Workforce Admin"
              description="Agentes, actividad e inactividad por dispositivo."
              checked={!isOwnerSelected && selectedRoles.includes('WorkforceAdmin')}
              disabled={isOwnerSelected}
              onChange={() => toggleRole('WorkforceAdmin')}
            />
          </div>
        </div>
        <PrimaryButton type="submit" disabled={isSaving} fullWidth sx={{ mt: 2.5 }}>
          {isSaving ? 'Agregando...' : 'Agregar admin'}
        </PrimaryButton>
      </form>
    </ModalShell>
  )
}

function RoleOption({
  title,
  description,
  checked,
  disabled,
  onChange,
}: {
  title: string
  description: string
  checked: boolean
  disabled?: boolean
  onChange: () => void
}) {
  return (
    <label className={`flex cursor-pointer flex-col rounded-2xl border p-3 transition ${checked ? 'border-[#00a9be] bg-[#eefcfe]' : 'border-[#d4e7ed] bg-[#f8fcfd]'} ${disabled ? 'opacity-55' : ''}`}>
      <span className="flex items-center gap-2 text-sm font-black text-[#12394a]">
        <input type="checkbox" checked={checked} disabled={disabled} onChange={onChange} className="h-4 w-4 accent-[#008da8]" />
        {title}
      </span>
      <span className="mt-2 text-xs leading-5 text-[#5f7782]">{description}</span>
    </label>
  )
}

function AdminDetailDialog({ admin, onClose }: { admin: AdminAccount; onClose: () => void }) {
  return (
    <ModalShell eyebrow="Detalle" title={admin.displayName || admin.email} onClose={onClose}>
        <div className="mt-5 grid gap-2">
          <DetailRow label="Correo" value={admin.email} />
          <DetailRow label="Metodo" value={admin.authenticationMode === 'Entra' ? 'Microsoft Entra' : admin.authenticationMode} />
          <DetailRow label="Rol" value={formatAdminRoles(admin.role)} />
          <DetailRow label="Estado" value={admin.isActive ? 'Activo' : 'Inactivo'} />
          <DetailRow label="Creado" value={formatDate(admin.createdAtUtc)} />
          <DetailRow label="Ultimo ingreso" value={admin.lastLoginAtUtc ? formatDate(admin.lastLoginAtUtc) : 'Pendiente'} />
        </div>
    </ModalShell>
  )
}

function formatDate(value: string | null | undefined) {
  if (!value) return 'N/A'
  return new Date(value).toLocaleString()
}

function formatCompactDate(value: string | null | undefined) {
  if (!value) return 'N/A'
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

function formatShortDate(value: string | null | undefined) {
  if (!value) return 'N/A'
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: '2-digit',
    year: '2-digit',
  }).format(new Date(value))
}
