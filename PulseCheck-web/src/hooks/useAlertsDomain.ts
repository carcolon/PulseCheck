import { useState } from 'react'
import { apiBaseUrl } from '../constants'
import type { ClientInactivityAlertOptions, ClientInactivityAlertSetting } from '../types'
import type { AuthorizedFetch } from './adminPanelTypes'

export function useAlertsDomain({
  authorizedFetch,
  setError,
}: {
  authorizedFetch: AuthorizedFetch
  setError: (message: string | null) => void
}) {
  const [clientInactivityAlertOptions, setClientInactivityAlertOptions] = useState<ClientInactivityAlertOptions>({ clients: [], operations: [], settings: [] })
  const [savingClientAlert, setSavingClientAlert] = useState(false)
  const [deletingClientAlertId, setDeletingClientAlertId] = useState<string | null>(null)

  async function saveClientInactivityAlert(input: { id?: string | null; client: string; operation: string; alertThresholdMinutes: number; isEnabled: boolean; additionalRecipientEmails: string[] }) {
    try {
      setError(null)
      setSavingClientAlert(true)
      const response = await authorizedFetch(`${apiBaseUrl}/api/client-inactivity-alerts`, {
        method: 'PUT',
        body: JSON.stringify(input),
      })

      if (!response.ok) {
        const payload = await response.json().catch(() => null)
        const message = typeof payload?.message === 'string'
          ? payload.message
          : 'No fue posible guardar la regla de inactividad.'
        throw new Error(message)
      }

      const setting = await response.json() as ClientInactivityAlertSetting
      setClientInactivityAlertOptions((current) => ({
        ...current,
        settings: [setting, ...current.settings.filter((item) => item.id !== setting.id)]
          .sort((left, right) => `${left.client}|${left.operation}`.localeCompare(`${right.client}|${right.operation}`)),
      }))
    } catch (clientAlertError) {
      setError(clientAlertError instanceof Error ? clientAlertError.message : 'Error inesperado al guardar la regla de inactividad.')
    } finally {
      setSavingClientAlert(false)
    }
  }

  async function deleteClientInactivityAlert(id: string) {
    try {
      setError(null)
      setDeletingClientAlertId(id)
      const response = await authorizedFetch(`${apiBaseUrl}/api/client-inactivity-alerts/${id}`, {
        method: 'DELETE',
      })

      if (!response.ok) {
        throw new Error('No fue posible eliminar la regla de inactividad.')
      }

      setClientInactivityAlertOptions((current) => ({
        ...current,
        settings: current.settings.filter((item) => item.id !== id),
      }))
    } catch (clientAlertError) {
      setError(clientAlertError instanceof Error ? clientAlertError.message : 'Error inesperado al eliminar la regla de inactividad.')
    } finally {
      setDeletingClientAlertId(null)
    }
  }

  return {
    clientInactivityAlertOptions,
    setClientInactivityAlertOptions,
    savingClientAlert,
    deletingClientAlertId,
    saveClientInactivityAlert,
    deleteClientInactivityAlert,
  }
}
