import type { Meta, StoryObj } from '@storybook/react-vite'
import { useState } from 'react'
import {
  DetailRow,
  MetricCard,
  ModalShell,
  ModePill,
  PrimaryButton,
  SoftButton,
  StatusChip,
  inputClassName,
} from './ui'

const meta = {
  title: 'Components/UI',
  parameters: {
    layout: 'centered',
  },
} satisfies Meta

export default meta
type Story = StoryObj<typeof meta>

export const Buttons: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-3">
      <PrimaryButton>Guardar cambios</PrimaryButton>
      <PrimaryButton disabled>Guardando</PrimaryButton>
      <SoftButton>Cancelar</SoftButton>
      <SoftButton size="small">Editar</SoftButton>
    </div>
  ),
}

export const SelectionPills: Story = {
  render: function SelectionPillsStory() {
    const [activeMode, setActiveMode] = useState('now')

    return (
      <div className="grid min-w-72 gap-2">
        <ModePill active={activeMode === 'now'} label="Enviar ahora" onClick={() => setActiveMode('now')} />
        <ModePill active={activeMode === 'scheduled'} label="Programar" onClick={() => setActiveMode('scheduled')} />
      </div>
    )
  },
}

export const StatusChips: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2">
      <StatusChip tone="active">Activo</StatusChip>
      <StatusChip tone="inactive">Inactivo</StatusChip>
      <StatusChip tone="warning">Pendiente</StatusChip>
      <StatusChip>Microsoft Entra</StatusChip>
    </div>
  ),
}

export const FormElements: Story = {
  render: () => (
    <div className="grid w-80 gap-3">
      <input className={inputClassName} placeholder="Nombre de campana" />
      <select className={inputClassName} defaultValue="daily">
        <option value="daily">Diaria</option>
        <option value="weekly">Semanal</option>
      </select>
    </div>
  ),
}

export const DetailRows: Story = {
  render: () => (
    <div className="grid w-96 gap-2">
      <DetailRow label="Correo" value="admin@pulsecheck.local" />
      <DetailRow label="Rol" value="Owner" />
      <DetailRow label="Estado" value={<StatusChip tone="active">Activo</StatusChip>} />
    </div>
  ),
}

export const Metrics: Story = {
  render: () => (
    <div className="w-80">
      <MetricCard title="Agentes activos" value="128" detail="Ultimas 48 horas" />
    </div>
  ),
}

export const Modal: Story = {
  render: function ModalStory() {
    const [isOpen, setIsOpen] = useState(false)

    return (
      <>
        <PrimaryButton onClick={() => setIsOpen(true)}>Abrir modal</PrimaryButton>
        {isOpen ? (
          <ModalShell eyebrow="Detalle" title="Invitar admin" onClose={() => setIsOpen(false)}>
            <div className="mt-4 grid gap-3">
              <input className={inputClassName} placeholder="correo@empresa.com" />
              <PrimaryButton onClick={() => setIsOpen(false)}>Enviar invitacion</PrimaryButton>
            </div>
          </ModalShell>
        ) : null}
      </>
    )
  },
}
