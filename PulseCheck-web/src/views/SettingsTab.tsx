import { useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { createPortal } from 'react-dom'
import { Button, Chip } from '@mui/material'
import { PrimaryButton, SoftButton, inputClassName } from '../components/ui'
import type { ClientInactivityAlertOptions, ClientInactivityAlertSetting } from '../types'

type PickerKind = 'client' | 'operation' | null
type NotificationMode = 'default' | 'custom'
type ThresholdDraft = {
  id: string
  settingId: string | null
  hours: number
  minutes: number
}
type DeleteConfirmation = {
  scope: string
  settings: ClientInactivityAlertOptions['settings']
} | null

export function SettingsTab({
  connectionState,
  clientInactivityAlertOptions,
  savingClientAlert,
  deletingClientAlertId,
  onSaveClientAlert,
  onDeleteClientAlert,
}: {
  connectionState: string
  clientInactivityAlertOptions: ClientInactivityAlertOptions
  savingClientAlert: boolean
  deletingClientAlertId: string | null
  onSaveClientAlert: (input: { id?: string | null; client: string; operation: string; alertThresholdMinutes: number; isEnabled: boolean; additionalRecipientEmails: string[] }) => Promise<void>
  onDeleteClientAlert: (id: string) => Promise<void>
}) {
  const [editingSettings, setEditingSettings] = useState<ClientInactivityAlertSetting[]>([])
  const [client, setClient] = useState('')
  const [operation, setOperation] = useState('')
  const [thresholds, setThresholds] = useState<ThresholdDraft[]>(() => [createThresholdDraft(30)])
  const [isEnabled, setIsEnabled] = useState(true)
  const [notificationMode, setNotificationMode] = useState<NotificationMode>('default')
  const [additionalRecipientEmails, setAdditionalRecipientEmails] = useState('')
  const [clientSearch, setClientSearch] = useState('')
  const [operationSearch, setOperationSearch] = useState('')
  const [rulesSearch, setRulesSearch] = useState('')
  const [isRuleDialogOpen, setIsRuleDialogOpen] = useState(false)
  const [openPicker, setOpenPicker] = useState<PickerKind>(null)
  const [deleteConfirmation, setDeleteConfirmation] = useState<DeleteConfirmation>(null)

  const configuredScopeCounts = useMemo(() => {
    const counts = new Map<string, number>()
    for (const setting of clientInactivityAlertOptions.settings) {
      const key = buildScopeKey(setting.client, setting.operation)
      counts.set(key, (counts.get(key) ?? 0) + 1)
    }
    return counts
  }, [clientInactivityAlertOptions.settings])

  const clientOptions = useMemo(
    () => filterOptions(clientInactivityAlertOptions.clients, clientSearch),
    [clientInactivityAlertOptions.clients, clientSearch],
  )

  const operationOptions = useMemo(
    () => filterOptions(clientInactivityAlertOptions.operations, operationSearch),
    [clientInactivityAlertOptions.operations, operationSearch],
  )

  const parsedAdditionalRecipientEmails = useMemo(
    () => parseRecipientEmails(additionalRecipientEmails),
    [additionalRecipientEmails],
  )

  const hasInvalidAdditionalRecipientEmails = useMemo(
    () => notificationMode === 'custom' && parsedAdditionalRecipientEmails.some((email) => !isValidEmail(email)),
    [notificationMode, parsedAdditionalRecipientEmails],
  )

  const groupedSettings = useMemo(() => {
    const search = rulesSearch.trim().toLowerCase()
    const groups = new Map<string, ClientInactivityAlertOptions['settings']>()

    for (const setting of clientInactivityAlertOptions.settings) {
      const label = formatScope(setting.client, setting.operation)
      if (search && !label.toLowerCase().includes(search)) {
        continue
      }

      groups.set(label, [...(groups.get(label) ?? []), setting])
    }

    return Array.from(groups.entries())
      .map(([scope, settings]) => ({
        scope,
        settings: settings.sort((left, right) => left.alertThresholdMinutes - right.alertThresholdMinutes),
        updatedAtUtc: settings
          .map((setting) => setting.updatedAtUtc)
          .sort((left, right) => new Date(right).getTime() - new Date(left).getTime())[0],
      }))
      .sort((left, right) => left.scope.localeCompare(right.scope))
  }, [clientInactivityAlertOptions.settings, rulesSearch])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const existingByThreshold = new Map(editingSettings.map((setting) => [setting.alertThresholdMinutes, setting]))
    const normalizedThresholds = normalizeThresholdDrafts(thresholds).map((threshold) => ({
      ...threshold,
      settingId: existingByThreshold.get(threshold.totalMinutes)?.id ?? threshold.settingId,
    }))
    if (normalizedThresholds.length === 0) {
      return
    }

    const recipientEmails = notificationMode === 'custom'
      ? parsedAdditionalRecipientEmails
      : []
    const savedSettingIds = new Set<string>()

    for (const threshold of normalizedThresholds) {
      await onSaveClientAlert({
        id: threshold.settingId,
        client,
        operation,
        alertThresholdMinutes: threshold.totalMinutes,
        isEnabled,
        additionalRecipientEmails: recipientEmails,
      })

      if (threshold.settingId) {
        savedSettingIds.add(threshold.settingId)
      }
    }

    for (const setting of editingSettings) {
      if (!savedSettingIds.has(setting.id)) {
        await onDeleteClientAlert(setting.id)
      }
    }

    closeRuleDialog()
  }

  function editGroup(settings: ClientInactivityAlertOptions['settings']) {
    const orderedSettings = [...settings].sort((left, right) => left.alertThresholdMinutes - right.alertThresholdMinutes)
    const firstSetting = orderedSettings[0]
    if (!firstSetting) {
      return
    }

    setEditingSettings(orderedSettings)
    setClient(firstSetting.client)
    setOperation(firstSetting.operation)
    setThresholds(orderedSettings.map((setting) => createThresholdDraft(setting.alertThresholdMinutes, setting.id)))
    setIsEnabled(orderedSettings.some((setting) => setting.isEnabled))
    const recipientEmails = Array.from(new Set(orderedSettings.flatMap((setting) => setting.additionalRecipientEmails))).sort()
    setNotificationMode(recipientEmails.length > 0 ? 'custom' : 'default')
    setAdditionalRecipientEmails(recipientEmails.join('\n'))
    setClientSearch('')
    setOperationSearch('')
    setOpenPicker(null)
    setIsRuleDialogOpen(true)
  }

  async function deleteGroup(settings: ClientInactivityAlertOptions['settings']) {
    for (const setting of settings) {
      await onDeleteClientAlert(setting.id)
    }

    setDeleteConfirmation(null)
  }

  function openCreateRuleDialog() {
    resetRuleForm()
    setIsRuleDialogOpen(true)
  }

  function closeRuleDialog() {
    resetRuleForm()
    setIsRuleDialogOpen(false)
  }

  function resetRuleForm() {
    setEditingSettings([])
    setClient('')
    setOperation('')
    setThresholds([createThresholdDraft(30)])
    setIsEnabled(true)
    setNotificationMode('default')
    setAdditionalRecipientEmails('')
    setClientSearch('')
    setOperationSearch('')
    setOpenPicker(null)
  }

  function addAdditionalThreshold() {
    setThresholds((current) => [...current, createThresholdDraft(60)])
  }

  function updateAdditionalThreshold(id: string, field: 'hours' | 'minutes', value: number) {
    setThresholds((current) => current.map((threshold) => (
      threshold.id === id
        ? { ...threshold, [field]: value }
        : threshold
    )))
  }

  function removeAdditionalThreshold(id: string) {
    setThresholds((current) => current.length === 1 ? current : current.filter((threshold) => threshold.id !== id))
  }

  return (
    <section className="mt-4 grid gap-4 font-sans">
      <div className="flex items-center gap-3 px-1">
        <span className="text-sm font-semibold text-[#244656]">Realtime</span>
        <RealtimeStatusChip state={connectionState} />
      </div>

      <article className="overflow-hidden rounded-[22px] border border-[#cfe1e8] bg-white shadow-[0_18px_44px_rgba(9,55,69,0.08)]">
        <div className="border-b border-[#d8e8ee] p-5">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.14em] text-[#00758d]">Reglas activas</p>
              <h3 className="mt-1 text-lg font-bold text-[#0d3140]">Alertas de inactividad</h3>
              <p className="mt-2 max-w-3xl text-sm leading-6 text-[#607985]">
                Configura tiempos de bloqueo por cliente, por operacion o por una combinacion de ambos.
              </p>
            </div>
            <PrimaryButton type="button" onClick={openCreateRuleDialog} className="shrink-0" sx={{ px: 3, py: 1.25 }}>
              Nueva regla de inactividad
            </PrimaryButton>
          </div>

          <div className="mt-5">
            <div className="mb-3 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
              <div>
                <p className="text-sm font-semibold text-[#17313c]">{clientInactivityAlertOptions.settings.length} reglas configuradas</p>
                <p className="text-xs text-[#607985]">
                  {groupedSettings.length} alcance{groupedSettings.length === 1 ? '' : 's'} en vista
                </p>
              </div>
              <label className="w-full max-w-md">
                <span className="sr-only">Buscar reglas por cliente u operacion</span>
                <input
                  value={rulesSearch}
                  onChange={(event) => setRulesSearch(event.target.value)}
                  className={inputClassName}
                  placeholder="Buscar por cliente u operacion"
                />
              </label>
            </div>

            <div className="overflow-hidden rounded-2xl border border-[#d7e8ee]">
              {clientInactivityAlertOptions.settings.length === 0 ? (
                <div className="px-4 py-8 text-center text-sm text-[#607985]">
                  Todavia no hay reglas configuradas.
                </div>
              ) : groupedSettings.length === 0 ? (
                <div className="px-4 py-8 text-center text-sm text-[#607985]">
                  No hay reglas que coincidan con la busqueda.
                </div>
              ) : (
                <div className="divide-y divide-[#e1edf2]">
                  {groupedSettings.map((group) => (
                    <div key={group.scope} className="grid gap-4 px-4 py-4 xl:grid-cols-[minmax(260px,1fr)_2fr] xl:items-start">
                      <div className="min-w-0">
                        <p className="truncate text-sm font-bold text-[#17313c]">{group.scope}</p>
                        <p className="mt-1 text-xs text-[#607985]">
                          {group.settings.length} regla{group.settings.length === 1 ? '' : 's'} - Actualizado {formatDate(group.updatedAtUtc)}
                        </p>
                      </div>
                      <div className="rounded-2xl border border-[#e1edf2] bg-[#f8fcfd] px-3 py-3">
                        <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_110px_minmax(150px,1fr)_auto] md:items-center">
                          <div className="flex flex-wrap gap-2">
                            {group.settings.map((setting) => (
                              <span key={setting.id} className="rounded-full border border-[#b9dce5] bg-[#eff9fc] px-3 py-1 text-center text-xs font-bold text-[#00758d]">
                                {formatDuration(setting.alertThresholdMinutes)}
                              </span>
                            ))}
                          </div>
                          <span className={`rounded-full border px-3 py-1 text-center text-xs font-bold ${group.settings.some((setting) => setting.isEnabled) ? 'border-[#9bd3db] bg-[#eefbfc] text-[#00758d]' : 'border-[#d9dfe3] bg-[#f3f5f6] text-[#657985]'}`}>
                            {group.settings.some((setting) => setting.isEnabled) ? 'Activa' : 'Inactiva'}
                          </span>
                          <span className="text-xs font-semibold text-[#607985]">
                            {resolveGroupAdditionalRecipientCount(group.settings) > 0
                              ? `${resolveGroupAdditionalRecipientCount(group.settings)} correo${resolveGroupAdditionalRecipientCount(group.settings) === 1 ? '' : 's'} adicional${resolveGroupAdditionalRecipientCount(group.settings) === 1 ? '' : 'es'}`
                              : 'Lideres directos'}
                          </span>
                          <div className="flex flex-wrap gap-2 md:justify-end">
                            <SoftButton type="button" size="small" onClick={() => editGroup(group.settings)}>
                              Editar
                            </SoftButton>
                            <SoftButton
                              type="button"
                              disabled={group.settings.some((setting) => deletingClientAlertId === setting.id)}
                              onClick={() => setDeleteConfirmation({ scope: group.scope, settings: group.settings })}
                              size="small"
                            >
                              {group.settings.some((setting) => deletingClientAlertId === setting.id) ? 'Eliminando...' : 'Eliminar'}
                            </SoftButton>
                          </div>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      </article>

      {isRuleDialogOpen ? createPortal(
        <div className="admin-modal-overlay" role="dialog" aria-modal="true">
          <div className="admin-modal-shell w-full max-w-[720px] overflow-hidden rounded-[24px] border border-[#cfe1e8] bg-white shadow-[0_24px_70px_rgba(5,31,42,0.28)]">
            <div className="flex items-start justify-between gap-4 border-b border-[#d8e8ee] px-6 py-5">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#00758d]">{editingSettings.length > 0 ? 'Editar reglas' : 'Nueva regla'}</p>
                <h3 className="mt-1 text-xl font-bold text-[#0d3140]">Regla de inactividad</h3>
              </div>
              <SoftButton type="button" onClick={closeRuleDialog} size="small">
                Cerrar
              </SoftButton>
            </div>

            <form onSubmit={handleSubmit} className="admin-modal-body grid gap-4 px-6 py-5">
              <div className="grid gap-4 lg:grid-cols-2">
                <SearchPicker
                  label="Cliente"
                  placeholder="Todos los clientes"
                  searchPlaceholder="Buscar cliente"
                  value={client}
                  options={clientOptions}
                  searchValue={clientSearch}
                  total={clientInactivityAlertOptions.clients.length}
                  isOpen={openPicker === 'client'}
                  onOpenChange={(open) => setOpenPicker(open ? 'client' : null)}
                  onSearchChange={setClientSearch}
                  onSelect={(nextClient) => {
                    setClient(nextClient)
                    setClientSearch('')
                    setOpenPicker(null)
                  }}
                  onClear={() => setClient('')}
                  getMeta={(option) => configuredScopeCounts.get(buildScopeKey(option, operation)) ?? 0}
                />

                <SearchPicker
                  label="Operacion"
                  placeholder="Todas las operaciones"
                  searchPlaceholder="Buscar operacion"
                  value={operation}
                  options={operationOptions}
                  searchValue={operationSearch}
                  total={clientInactivityAlertOptions.operations.length}
                  isOpen={openPicker === 'operation'}
                  onOpenChange={(open) => setOpenPicker(open ? 'operation' : null)}
                  onSearchChange={setOperationSearch}
                  onSelect={(nextOperation) => {
                    setOperation(nextOperation)
                    setOperationSearch('')
                    setOpenPicker(null)
                  }}
                  onClear={() => setOperation('')}
                  getMeta={(option) => configuredScopeCounts.get(buildScopeKey(client, option)) ?? 0}
                />
              </div>

              <div>
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <label className="block text-xs font-semibold uppercase tracking-[0.14em] text-[#647d88]">
                    Tiempos bloqueados
                  </label>
                  <SoftButton type="button" size="small" onClick={addAdditionalThreshold}>
                    Agregar otro tiempo
                  </SoftButton>
                </div>
                <div className="mt-2 grid gap-2">
                  {thresholds.map((threshold, index) => (
                    <div key={threshold.id} className="rounded-2xl border border-[#d7e8ee] bg-[#f8fcfd] p-3">
                      <div className="grid gap-3 sm:grid-cols-[360px_auto] sm:items-end">
                        <div className="grid max-w-[360px] grid-cols-2 gap-3">
                          <label>
                            <span className="mb-1 block text-xs font-semibold text-[#607985]">Horas {index + 1}</span>
                            <input
                              type="number"
                              min={0}
                              max={24}
                              value={threshold.hours}
                              onChange={(event) => updateAdditionalThreshold(threshold.id, 'hours', clampNumber(event.target.value, 0, 24))}
                              className={inputClassName}
                              required
                            />
                          </label>
                          <label>
                            <span className="mb-1 block text-xs font-semibold text-[#607985]">Minutos</span>
                            <input
                              type="number"
                              min={0}
                              max={59}
                              value={threshold.minutes}
                              onChange={(event) => updateAdditionalThreshold(threshold.id, 'minutes', clampNumber(event.target.value, 0, 59))}
                              className={inputClassName}
                              required
                            />
                          </label>
                        </div>
                        <button
                          type="button"
                          disabled={thresholds.length === 1}
                          onClick={() => removeAdditionalThreshold(threshold.id)}
                          className="px-1 py-3 text-sm font-semibold text-[#00758d] transition hover:text-[#005f73] disabled:cursor-not-allowed disabled:text-[#9cb1ba]"
                        >
                          Eliminar
                        </button>
                      </div>
                      <p className="mt-2 text-xs text-[#607985]">
                        Total: {formatDuration(threshold.hours * 60 + threshold.minutes)}
                      </p>
                    </div>
                  ))}
                </div>
                <p className="mt-2 text-xs text-[#607985]">
                  {editingSettings.length > 0
                    ? 'Se actualizara el grupo completo: los tiempos quitados se eliminaran y los tiempos nuevos se crearan.'
                    : 'Se creara una regla independiente por cada tiempo configurado para el mismo alcance y notificacion.'}
                </p>
              </div>

              <div className="rounded-2xl border border-[#d7e8ee] bg-[#f8fcfd] p-4">
                <p className="text-xs font-semibold uppercase tracking-[0.14em] text-[#647d88]">
                  Notificacion
                </p>
                <div className="mt-3 grid gap-3 md:grid-cols-2">
                  <label className={`cursor-pointer rounded-2xl border bg-white p-3 transition ${notificationMode === 'default' ? 'border-[#008aab] shadow-[0_0_0_3px_#dff5f8]' : 'border-[#d7e8ee]'}`}>
                    <span className="flex items-center gap-2 text-sm font-bold text-[#17313c]">
                      <input
                        type="radio"
                        name="notificationMode"
                        checked={notificationMode === 'default'}
                        onChange={() => setNotificationMode('default')}
                        className="h-4 w-4 accent-[#008aab]"
                      />
                      Predeterminado
                    </span>
                    <span className="mt-2 block text-xs leading-5 text-[#607985]">
                      PulseCheck enviara cada alerta al lider directo registrado para la persona afectada.
                    </span>
                  </label>

                  <label className={`cursor-pointer rounded-2xl border bg-white p-3 transition ${notificationMode === 'custom' ? 'border-[#008aab] shadow-[0_0_0_3px_#dff5f8]' : 'border-[#d7e8ee]'}`}>
                    <span className="flex items-center gap-2 text-sm font-bold text-[#17313c]">
                      <input
                        type="radio"
                        name="notificationMode"
                        checked={notificationMode === 'custom'}
                        onChange={() => setNotificationMode('custom')}
                        className="h-4 w-4 accent-[#008aab]"
                      />
                      Personalizado
                    </span>
                    <span className="mt-2 block text-xs leading-5 text-[#607985]">
                      Mantiene al lider directo y agrega copias para los correos definidos en esta regla.
                    </span>
                  </label>
                </div>

                {notificationMode === 'custom' ? (
                  <label className="mt-4 block">
                    <span className="mb-1 block text-xs font-semibold text-[#607985]">Correos adicionales</span>
                    <textarea
                      value={additionalRecipientEmails}
                      onChange={(event) => setAdditionalRecipientEmails(event.target.value)}
                      className={`${inputClassName} min-h-[96px] resize-y py-3`}
                      placeholder="correo1@empresa.com&#10;correo2@empresa.com"
                    />
                    <span className={hasInvalidAdditionalRecipientEmails ? 'mt-2 block text-xs font-semibold text-[#b54708]' : 'mt-2 block text-xs text-[#607985]'}>
                      {hasInvalidAdditionalRecipientEmails
                        ? 'Revisa el formato de los correos adicionales.'
                        : `${parsedAdditionalRecipientEmails.length} correo${parsedAdditionalRecipientEmails.length === 1 ? '' : 's'} adicional${parsedAdditionalRecipientEmails.length === 1 ? '' : 'es'} configurado${parsedAdditionalRecipientEmails.length === 1 ? '' : 's'}.`}
                    </span>
                  </label>
                ) : null}
              </div>

              <label className="flex w-fit items-center gap-2 text-sm font-semibold text-[#244656]">
                <input
                  type="checkbox"
                  checked={isEnabled}
                  onChange={(event) => setIsEnabled(event.target.checked)}
                  className="h-4 w-4 accent-[#008aab]"
                />
                Regla activa
              </label>

              <div className="grid gap-2 pt-2 sm:grid-cols-2">
                <PrimaryButton
                  type="submit"
                  disabled={
                    savingClientAlert ||
                    (!client && !operation) ||
                    normalizeThresholdDrafts(thresholds).length === 0 ||
                    (notificationMode === 'custom' && parsedAdditionalRecipientEmails.length === 0) ||
                    hasInvalidAdditionalRecipientEmails
                  }
                  fullWidth
                >
                  {savingClientAlert ? 'Guardando...' : editingSettings.length > 0 ? 'Actualizar reglas' : 'Guardar reglas'}
                </PrimaryButton>
                <SoftButton type="button" onClick={closeRuleDialog} fullWidth>
                  Cancelar
                </SoftButton>
              </div>
            </form>
          </div>
        </div>,
        document.body,
      ) : null}

      {deleteConfirmation ? createPortal(
        <div className="admin-modal-overlay" role="dialog" aria-modal="true">
          <div className="admin-modal-shell w-full max-w-[440px] rounded-[22px] border border-[#cfe1e8] bg-white p-5 shadow-[0_24px_70px_rgba(5,31,42,0.28)]">
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-[#b54708]">Confirmar eliminacion</p>
            <h3 className="mt-2 text-lg font-bold text-[#0d3140]">Eliminar reglas de inactividad</h3>
            <p className="mt-3 text-sm leading-6 text-[#607985]">
              Se eliminaran {deleteConfirmation.settings.length} regla{deleteConfirmation.settings.length === 1 ? '' : 's'} para {deleteConfirmation.scope}. Esta accion no se puede deshacer.
            </p>
            <div className="mt-5 grid gap-2 sm:grid-cols-2">
              <SoftButton
                type="button"
                onClick={() => setDeleteConfirmation(null)}
                fullWidth
              >
                Cancelar
              </SoftButton>
              <PrimaryButton
                type="button"
                disabled={deleteConfirmation.settings.some((setting) => deletingClientAlertId === setting.id)}
                onClick={() => void deleteGroup(deleteConfirmation.settings)}
                fullWidth
                sx={{ background: '#b42318', '&:hover': { background: '#9f1f15' } }}
              >
                {deleteConfirmation.settings.some((setting) => deletingClientAlertId === setting.id) ? 'Eliminando...' : 'Eliminar reglas'}
              </PrimaryButton>
            </div>
          </div>
        </div>,
        document.body,
      ) : null}
    </section>
  )
}

function SearchPicker({
  label,
  placeholder,
  searchPlaceholder,
  value,
  options,
  searchValue,
  total,
  isOpen,
  onOpenChange,
  onSearchChange,
  onSelect,
  onClear,
  getMeta,
}: {
  label: string
  placeholder: string
  searchPlaceholder: string
  value: string
  options: string[]
  searchValue: string
  total: number
  isOpen: boolean
  onOpenChange: (open: boolean) => void
  onSearchChange: (value: string) => void
  onSelect: (value: string) => void
  onClear: () => void
  getMeta: (value: string) => number
}) {
  const pickerRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!isOpen) {
      return
    }

    function handlePointerDown(event: PointerEvent) {
      if (pickerRef.current && !pickerRef.current.contains(event.target as Node)) {
        onOpenChange(false)
      }
    }

    document.addEventListener('pointerdown', handlePointerDown)
    return () => document.removeEventListener('pointerdown', handlePointerDown)
  }, [isOpen, onOpenChange])

  return (
    <div>
      <label className="block text-xs font-semibold uppercase tracking-[0.14em] text-[#647d88]">
        {label}
      </label>
      <div ref={pickerRef} className="relative mt-2">
        <Button
          type="button"
          onClick={() => onOpenChange(!isOpen)}
          aria-expanded={isOpen}
          aria-haspopup="listbox"
          variant="outlined"
          sx={{
            height: 48,
            width: '100%',
            justifyContent: 'space-between',
            gap: 1.5,
            borderRadius: '12px',
            borderColor: isOpen ? '#008aab' : '#c6dce4',
            backgroundColor: '#ffffff',
            boxShadow: '0 8px 18px rgba(9,55,69,0.08)',
            px: 2,
            textAlign: 'left',
            textTransform: 'none',
            ...(isOpen ? { boxShadow: '0 0 0 4px #dff5f8, 0 8px 18px rgba(9,55,69,0.08)' } : {}),
            '&:hover': {
              borderColor: isOpen ? '#008aab' : '#8fc5d1',
              backgroundColor: '#ffffff',
            },
          }}
        >
          <span className="min-w-0">
            <span className={value ? 'block truncate text-sm font-semibold text-[#0d3140]' : 'block truncate text-sm font-medium text-[#78909a]'}>
              {value || placeholder}
            </span>
          </span>
          <span className={`grid h-7 w-7 shrink-0 place-items-center rounded-full bg-[#eef8fb] text-sm font-bold text-[#00758d] transition ${isOpen ? 'rotate-180' : ''}`}>
            v
          </span>
        </Button>

        {isOpen ? (
          <div className="absolute z-20 mt-2 w-full overflow-hidden rounded-2xl border border-[#bdd9e2] bg-white shadow-[0_18px_40px_rgba(9,55,69,0.18)]">
            <div className="border-b border-[#e1edf2] p-3">
              <input
                value={searchValue}
                onChange={(event) => onSearchChange(event.target.value)}
                className="h-10 w-full rounded-xl border border-[#c6dce4] bg-[#f8fcfd] px-3 text-sm text-[#17313c] outline-none transition placeholder:text-[#78909a] focus:border-[#008aab] focus:ring-4 focus:ring-[#dff5f8]"
                placeholder={searchPlaceholder}
                autoFocus
              />
              <p className="mt-2 text-xs text-[#607985]">
                Mostrando {options.length} de {total}.
              </p>
            </div>
            <div className="max-h-72 overflow-y-auto py-1" role="listbox">
              {value ? (
                <Button
                  type="button"
                  onClick={onClear}
                  fullWidth
                  sx={{
                    justifyContent: 'space-between',
                    color: '#00758d',
                    fontSize: 14,
                    fontWeight: 800,
                    textTransform: 'none',
                    px: 2,
                    py: 1.5,
                    '&:hover': { backgroundColor: '#f3fafc' },
                  }}
                >
                  Limpiar seleccion
                </Button>
              ) : null}
              {options.length === 0 ? (
                <div className="px-4 py-6 text-center text-sm text-[#607985]">No hay resultados.</div>
              ) : (
                options.map((option) => {
                  const ruleCount = getMeta(option)
                  const selected = option.toLowerCase() === value.toLowerCase()
                  return (
                    <Button
                      key={option}
                      type="button"
                      onClick={() => onSelect(option)}
                      role="option"
                      aria-selected={selected}
                      fullWidth
                      sx={{
                        justifyContent: 'space-between',
                        gap: 1.5,
                        color: selected ? '#006f86' : '#17313c',
                        backgroundColor: selected ? '#e9f8fb' : '#ffffff',
                        fontSize: 14,
                        textAlign: 'left',
                        textTransform: 'none',
                        px: 2,
                        py: 1.5,
                        '&:hover': { backgroundColor: '#f3fafc' },
                      }}
                    >
                      <span className="min-w-0 truncate font-semibold">{option}</span>
                      {ruleCount > 0 ? (
                        <span className="shrink-0 rounded-full border border-[#b9dce5] bg-[#eff9fc] px-2 py-0.5 text-xs font-bold text-[#00758d]">
                          {ruleCount} regla{ruleCount === 1 ? '' : 's'}
                        </span>
                      ) : null}
                    </Button>
                  )
                })
              )}
            </div>
          </div>
        ) : null}
      </div>
    </div>
  )
}

function filterOptions(options: string[], searchValue: string) {
  const search = searchValue.trim().toLowerCase()
  return options.filter((item) => !search || item.toLowerCase().includes(search))
}

function RealtimeStatusChip({ state }: { state: string }) {
  const normalized = state.toLowerCase()
  const palette = normalized === 'live'
    ? { background: '#e8f7ef', border: '#8bd3a7', color: '#137a3f' }
    : normalized === 'connecting'
      ? { background: '#fff7df', border: '#e7c85e', color: '#8a6400' }
      : { background: '#fdecec', border: '#ef9a9a', color: '#b42318' }

  return (
    <Chip
      label={state}
      size="small"
      variant="outlined"
      sx={{
        height: 30,
        borderRadius: '999px',
        borderColor: palette.border,
        backgroundColor: palette.background,
        color: palette.color,
        fontWeight: 800,
        textTransform: 'capitalize',
        '& .MuiChip-label': {
          px: 1.5,
        },
      }}
    />
  )
}

function buildScopeKey(client: string, operation: string) {
  return `${client.trim().toLowerCase()}|${operation.trim().toLowerCase()}`
}

function parseRecipientEmails(value: string) {
  return Array.from(new Set(
    value
      .split(/[;,\s]+/)
      .map((item) => item.trim().toLowerCase())
      .filter(Boolean),
  ))
}

function isValidEmail(value: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
}

function clampNumber(value: string, min: number, max: number) {
  const numericValue = Number(value)
  if (!Number.isFinite(numericValue)) {
    return min
  }

  return Math.min(max, Math.max(min, Math.trunc(numericValue)))
}

function createThresholdDraft(totalMinutes: number, settingId: string | null = null): ThresholdDraft {
  return {
    id: crypto.randomUUID(),
    settingId,
    hours: Math.floor(totalMinutes / 60),
    minutes: totalMinutes % 60,
  }
}

function normalizeThresholdDrafts(thresholds: ThresholdDraft[]) {
  const normalized: Array<ThresholdDraft & { totalMinutes: number }> = []
  const seen = new Set<number>()

  for (const threshold of thresholds) {
    const totalMinutes = threshold.hours * 60 + threshold.minutes
    if (totalMinutes < 1 || seen.has(totalMinutes)) {
      continue
    }

    seen.add(totalMinutes)
    normalized.push({ ...threshold, totalMinutes })
  }

  return normalized.sort((left, right) => left.totalMinutes - right.totalMinutes)
}

function resolveGroupAdditionalRecipientCount(settings: ClientInactivityAlertOptions['settings']) {
  return Math.max(0, ...settings.map((setting) => setting.additionalRecipientEmails.length))
}

function formatDuration(totalMinutes: number) {
  if (totalMinutes < 1) {
    return '0 min'
  }

  const hours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60
  if (hours > 0 && minutes > 0) {
    return `${hours} h ${minutes} min`
  }

  if (hours > 0) {
    return `${hours} h`
  }

  return `${minutes} min`
}

function formatScope(client: string, operation: string) {
  const cleanClient = client.trim()
  const cleanOperation = operation.trim()
  if (cleanClient && cleanOperation) {
    return `${cleanClient} / ${cleanOperation}`
  }

  if (cleanClient) {
    return cleanClient
  }

  if (cleanOperation) {
    return cleanOperation
  }

  return 'Todos'
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}
