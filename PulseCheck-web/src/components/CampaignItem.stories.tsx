import type { Meta, StoryObj } from '@storybook/react-vite'
import { useState } from 'react'
import type { Campaign, CampaignStatus } from '../types'
import { CampaignItem } from './CampaignItem'

const audienceOptions = ['CX Bogota', 'Soporte Norte', 'Backoffice', 'Ventas', 'Calidad']

const baseCampaign: Campaign = {
  id: 'campaign-1',
  name: 'Pulso diario operacion CX',
  audience: 'CX Bogota, Backoffice',
  scheduleRule: '0 30 10 ? * MON,TUE,WED,THU,FRI#hide-after-answered',
  deliveryWindowStart: '09:00:00',
  deliveryWindowEnd: '18:00:00',
  status: 'Active',
  questions: [
    {
      id: 'question-1',
      text: 'Como calificas tu energia hoy?',
      type: 'Scale',
      minValue: 1,
      maxValue: 5,
      placeholder: null,
      options: null,
    },
    {
      id: 'question-2',
      text: 'Que bloqueo deberiamos revisar?',
      type: 'Text',
      minValue: null,
      maxValue: null,
      placeholder: 'Describe brevemente la situacion',
      options: null,
    },
  ],
  createdBy: 'admin@pulsecheck.local',
  createdAtUtc: '2026-07-29T15:00:00Z',
  updatedAtUtc: '2026-07-30T15:00:00Z',
  deletedAtUtc: null,
}

const meta = {
  title: 'Components/CampaignItem',
  component: CampaignItem,
  parameters: {
    layout: 'centered',
  },
} satisfies Meta<typeof CampaignItem>

export default meta
type Story = StoryObj<typeof meta>

export const Active: Story = {
  render: function ActiveCampaignItemStory() {
    const [campaign, setCampaign] = useState(baseCampaign)
    const [isEditing, setIsEditing] = useState(false)

    return (
      <div className="w-[760px] max-w-[calc(100vw-48px)]">
        <CampaignItem
          campaign={campaign}
          isEditing={isEditing}
          audienceOptions={audienceOptions}
          onEdit={() => setIsEditing(true)}
          onCancel={() => setIsEditing(false)}
          onSave={async (updated) => {
            setCampaign(updated)
            setIsEditing(false)
          }}
          onDelete={async () => undefined}
          onSetStatus={async (_id, status) => setCampaign((current) => ({ ...current, status }))}
        />
      </div>
    )
  },
}

export const Editing: Story = {
  render: function EditingCampaignItemStory() {
    const [campaign, setCampaign] = useState(baseCampaign)

    return (
      <div className="w-[760px] max-w-[calc(100vw-48px)]">
        <CampaignItem
          campaign={campaign}
          isEditing
          audienceOptions={audienceOptions}
          onEdit={() => undefined}
          onCancel={() => undefined}
          onSave={async (updated) => setCampaign(updated)}
          onDelete={async () => undefined}
          onSetStatus={async () => undefined}
        />
      </div>
    )
  },
}

export const Statuses: Story = {
  render: function CampaignStatusesStory() {
    const statuses: CampaignStatus[] = ['Draft', 'Active', 'Paused']

    return (
      <div className="grid w-[760px] max-w-[calc(100vw-48px)] gap-3">
        {statuses.map((status) => (
          <CampaignItem
            key={status}
            campaign={{ ...baseCampaign, id: `campaign-${status}`, name: `Campana ${status}`, status }}
            isEditing={false}
            audienceOptions={audienceOptions}
            onEdit={() => undefined}
            onCancel={() => undefined}
            onSave={async () => undefined}
            onDelete={async () => undefined}
            onSetStatus={async () => undefined}
          />
        ))}
      </div>
    )
  },
}
