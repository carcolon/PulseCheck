import { useMemo, useState } from 'react'
import Button from '@mui/material/Button'
import { SoftButton } from './ui'
import { allOperationsAudience, formatAudience, parseAudienceOperations } from '../utils/campaigns'

type AudienceSelectorProps = {
  operations: string[]
  value: string[]
  onChange: (operations: string[]) => void
  compact?: boolean
}

export function AudienceSelector({ operations, value, onChange, compact = false }: AudienceSelectorProps) {
  const [query, setQuery] = useState('')
  const selected = useMemo(
    () => value.filter((item) => operations.some((operation) => operation.toLowerCase() === item.toLowerCase())),
    [operations, value],
  )
  const normalizedQuery = query.trim().toLowerCase()
  const filteredOperations = operations.filter((operation) => operation.toLowerCase().includes(normalizedQuery))
  const isAllSelected = selected.length === 0

  function toggleOperation(operation: string) {
    if (selected.some((item) => item.toLowerCase() === operation.toLowerCase())) {
      onChange(selected.filter((item) => item.toLowerCase() !== operation.toLowerCase()))
      return
    }

    onChange([...selected, operation])
  }

  return (
    <div className="rounded-2xl border border-[#d5e5eb] bg-white p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-[#6a818b]">Audiencia</p>
          <p className="mt-1 text-sm font-semibold text-[#183d4b]">{formatAudience(selected)}</p>
        </div>
        <SoftButton
          type="button"
          onClick={() => onChange([])}
          sx={isAllSelected ? { backgroundColor: '#ecf7fa' } : undefined}
        >
          Todas
        </SoftButton>
      </div>

      <input
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        className="input-field mt-3"
        placeholder="Buscar operación"
      />

      <div className={`mt-3 grid gap-2 overflow-auto pr-1 ${compact ? 'max-h-44' : 'max-h-56'}`}>
        {operations.length === 0 ? (
          <p className="rounded-xl border border-[#d9e8ee] bg-[#f8fbfd] px-3 py-2 text-sm text-[#5f7782]">
            Aún no hay operaciones disponibles. Se enviará a todas las operaciones.
          </p>
        ) : null}
        {filteredOperations.map((operation) => {
          const active = selected.some((item) => item.toLowerCase() === operation.toLowerCase())
          return (
            <Button
              key={operation}
              type="button"
              onClick={() => toggleOperation(operation)}
              variant="outlined"
              sx={{
                justifyContent: 'space-between',
                borderRadius: '12px',
                borderColor: active ? '#008aab' : '#d7e5eb',
                backgroundColor: active ? '#ecf7fa' : '#fbfdff',
                color: active ? '#0d3a49' : '#4b6672',
                textTransform: 'none',
                px: 1.5,
                py: 1,
                '&:hover': {
                  borderColor: '#008aab',
                  backgroundColor: active ? '#e0f3f7' : '#f6fbfd',
                },
              }}
            >
              <span className="truncate">{operation}</span>
              <span className="text-xs font-semibold">{active ? 'Seleccionada' : 'Agregar'}</span>
            </Button>
          )
        })}
      </div>

      <input type="hidden" name="audience" value={isAllSelected ? allOperationsAudience : selected.join(', ')} />
    </div>
  )
}

export function parseAudienceSelection(audience: string, operations: string[]) {
  return parseAudienceOperations(audience).filter((item) =>
    operations.some((operation) => operation.toLowerCase() === item.toLowerCase()),
  )
}
