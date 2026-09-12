import { useState } from 'react'
import { apiBaseUrl } from '../constants'
import type { ReportRange, ResponseItem } from '../types'
import type { AuthorizedFetch } from './adminPanelTypes'

export type ReportExportFilters = {
  campaignId?: string | null
  campaignSearch?: string
  operation?: string
  date?: string
}

export function useResponsesDomain({
  authorizedFetch,
  setError,
  setEvents,
}: {
  authorizedFetch: AuthorizedFetch
  setError: (message: string | null) => void
  setEvents: React.Dispatch<React.SetStateAction<string[]>>
}) {
  const [responses, setResponses] = useState<ResponseItem[]>([])
  const [reportRange, setReportRange] = useState<ReportRange>('daily')
  const [isExportingReport, setIsExportingReport] = useState(false)

  async function exportReport(filters: ReportExportFilters = {}) {
    try {
      setError(null)
      setIsExportingReport(true)
      const parameters = new URLSearchParams()
      if (filters.date) {
        parameters.set('range', 'custom')
        parameters.set('from', filters.date)
        parameters.set('to', filters.date)
      } else {
        parameters.set('range', 'all')
      }

      if (filters.campaignId) {
        parameters.set('campaignId', filters.campaignId)
      }

      if (filters.campaignSearch?.trim()) {
        parameters.set('campaignSearch', filters.campaignSearch.trim())
      }

      if (filters.operation?.trim()) {
        parameters.set('operation', filters.operation.trim())
      }

      const response = await authorizedFetch(`${apiBaseUrl}/api/dashboard/report/excel?${parameters.toString()}`, {
        method: 'GET',
      })

      if (!response.ok) {
        throw new Error('No fue posible generar el reporte Excel.')
      }

      const blob = await response.blob()
      const disposition = response.headers.get('content-disposition') ?? ''
      const fileNameMatch = disposition.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i)
      const fallbackName = `PulseCheck-Reporte-${filters.date ? 'custom' : 'all'}.xlsx`
      const fileName = fileNameMatch?.[1]
        ? decodeURIComponent(fileNameMatch[1].replace(/"/g, '').trim())
        : fallbackName

      const url = window.URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = fileName
      document.body.append(anchor)
      anchor.click()
      anchor.remove()
      window.URL.revokeObjectURL(url)

      setEvents((list) => [`Reporte ${filters.date ? 'filtrado' : 'completo'} exportado (${fileName}).`, ...list].slice(0, 8))
    } catch (exportError) {
      setError(exportError instanceof Error ? exportError.message : 'Error inesperado al exportar reporte.')
    } finally {
      setIsExportingReport(false)
    }
  }

  return {
    responses,
    setResponses,
    reportRange,
    setReportRange,
    isExportingReport,
    exportReport,
  }
}
