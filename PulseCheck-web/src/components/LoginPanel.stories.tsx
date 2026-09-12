import type { Meta, StoryObj } from '@storybook/react-vite'
import { LoginPanel } from './LoginPanel'

const meta = {
  title: 'Components/LoginPanel',
  component: LoginPanel,
  parameters: {
    layout: 'fullscreen',
  },
} satisfies Meta<typeof LoginPanel>

export default meta
type Story = StoryObj<typeof meta>

export const MicrosoftEntra: Story = {
  args: {
    error: null,
    isEntraConfigured: true,
    onSubmit: async () => undefined,
    onMicrosoftLogin: async () => undefined,
  },
}

export const LocalCredentials: Story = {
  args: {
    error: null,
    isEntraConfigured: false,
    onSubmit: async () => undefined,
    onMicrosoftLogin: async () => undefined,
  },
}

export const WithError: Story = {
  args: {
    error: 'No se pudo validar el acceso. Revisa tus credenciales.',
    isEntraConfigured: false,
    onSubmit: async () => undefined,
    onMicrosoftLogin: async () => undefined,
  },
}
