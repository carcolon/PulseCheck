import type { Meta, StoryObj } from '@storybook/react-vite'
import { useState } from 'react'
import { AudienceSelector } from './AudienceSelector'

const operations = ['CX Bogota', 'Soporte Norte', 'Backoffice', 'Ventas', 'Calidad']

const meta = {
  title: 'Components/AudienceSelector',
  component: AudienceSelector,
  parameters: {
    layout: 'centered',
  },
} satisfies Meta<typeof AudienceSelector>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {
  render: function AudienceSelectorStory() {
    const [value, setValue] = useState<string[]>([])

    return (
      <div className="w-96">
        <AudienceSelector operations={operations} value={value} onChange={setValue} />
      </div>
    )
  },
}

export const Compact: Story = {
  render: function CompactAudienceSelectorStory() {
    const [value, setValue] = useState<string[]>(['Backoffice'])

    return (
      <div className="w-96">
        <AudienceSelector operations={operations} value={value} onChange={setValue} compact />
      </div>
    )
  },
}

export const Empty: Story = {
  args: {
    operations: [],
    value: [],
    onChange: () => undefined,
  },
}
