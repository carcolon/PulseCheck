import { useState } from 'react'
import { apiBaseUrl } from '../constants'
import type { AdminAccount, AdminRole } from '../types'
import type { AuthorizedFetch } from './adminPanelTypes'

export function useAdminsDomain({
  authorizedFetch,
  setError,
}: {
  authorizedFetch: AuthorizedFetch
  setError: (message: string | null) => void
}) {
  const [admins, setAdmins] = useState<AdminAccount[]>([])
  const [isCreatingAdmin, setIsCreatingAdmin] = useState(false)
  const [deletingAdminId, setDeletingAdminId] = useState<string | null>(null)
  const [updatingAdminId, setUpdatingAdminId] = useState<string | null>(null)

  async function createAdmin(email: string, roles: AdminRole[]) {
    const normalizedEmail = email.trim().toLowerCase()
    if (!normalizedEmail) {
      setError('Debes ingresar un correo electronico.')
      return
    }

    try {
      setError(null)
      setIsCreatingAdmin(true)
      const response = await authorizedFetch(`${apiBaseUrl}/api/admin-users`, {
        method: 'POST',
        body: JSON.stringify({ email: normalizedEmail, roles }),
      })

      if (!response.ok) {
        throw new Error('No fue posible crear el administrador.')
      }

      const admin = await response.json()
      setAdmins((list) => [admin, ...list.filter((item) => item.id !== admin.id)])
    } catch (adminError) {
      setError(adminError instanceof Error ? adminError.message : 'Error inesperado al crear administrador.')
    } finally {
      setIsCreatingAdmin(false)
    }
  }

  async function deleteAdmin(id: string) {
    try {
      setError(null)
      setDeletingAdminId(id)
      const response = await authorizedFetch(`${apiBaseUrl}/api/admin-users/${id}`, {
        method: 'DELETE',
      })

      if (!response.ok) {
        const payload = await response.json().catch(() => null)
        const message = typeof payload?.message === 'string'
          ? payload.message
          : 'No fue posible eliminar el administrador.'
        throw new Error(message)
      }

      setAdmins((list) => list.filter((item) => item.id !== id))
    } catch (adminError) {
      setError(adminError instanceof Error ? adminError.message : 'Error inesperado al eliminar administrador.')
    } finally {
      setDeletingAdminId(null)
    }
  }

  async function updateAdminStatus(id: string, isActive: boolean) {
    try {
      setError(null)
      setUpdatingAdminId(id)
      const response = await authorizedFetch(`${apiBaseUrl}/api/admin-users/${id}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ isActive }),
      })

      if (!response.ok) {
        const payload = await response.json().catch(() => null)
        const message = typeof payload?.message === 'string'
          ? payload.message
          : 'No fue posible actualizar el administrador.'
        throw new Error(message)
      }

      const admin = await response.json()
      setAdmins((list) => list.map((item) => (item.id === admin.id ? admin : item)))
    } catch (adminError) {
      setError(adminError instanceof Error ? adminError.message : 'Error inesperado al actualizar administrador.')
    } finally {
      setUpdatingAdminId(null)
    }
  }

  return {
    admins,
    setAdmins,
    isCreatingAdmin,
    deletingAdminId,
    updatingAdminId,
    createAdmin,
    deleteAdmin,
    updateAdminStatus,
  }
}

