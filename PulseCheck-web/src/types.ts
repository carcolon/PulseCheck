export type Tab = 'overview' | 'campaigns' | 'agents' | 'activity' | 'responses' | 'admins' | 'lt' | 'settings'
export type AdminRole = 'Owner' | 'HRAdmin' | 'WorkforceAdmin'
export type AppView = 'landing' | 'login' | 'admin'
export type CampaignStatus = 'Draft' | 'Active' | 'Paused'
export type DeliveryMode = 'now' | 'scheduled'
export type FrequencyMode = 'hourly' | 'custom' | 'weekly' | 'biweekly' | 'monthly' | 'quarterly'
export type ConnectionState = 'connecting' | 'live' | 'offline'
export type CampaignFilter = 'All' | CampaignStatus
export type QuestionType = 'Scale' | 'Text' | 'YesNo' | 'Choice'
export type ReportRange = 'daily' | 'weekly'

export type CampaignQuestion = {
  id: string
  text: string
  type: QuestionType
  minValue: number | null
  maxValue: number | null
  placeholder: string | null
  options: string[] | null
}

export type Campaign = {
  id: string
  name: string
  audience: string
  scheduleRule: string
  deliveryWindowStart: string
  deliveryWindowEnd: string
  status: CampaignStatus
  questions: CampaignQuestion[]
  createdBy: string
  createdAtUtc: string
  updatedAtUtc: string
  deletedAtUtc: string | null
}

export type CampaignAudienceOptions = {
  operations: string[]
}

export type ClientInactivityAlertSetting = {
  id: string
  client: string
  operation: string
  alertThresholdMinutes: number
  isEnabled: boolean
  additionalRecipientEmails: string[]
  createdAtUtc: string
  updatedAtUtc: string
}

export type ClientInactivityAlertOptions = {
  clients: string[]
  operations: string[]
  settings: ClientInactivityAlertSetting[]
}

export type TransformationalLeaderCandidate = {
  solvoId: string
  fullName: string
  corporateEmail: string
  jobTitleCode: string
  status: string
  currentOperation: string
  client: string
  department: string
  assignedOperation: string
  assignedOperations: string[]
  assignmentUpdatedAtUtc: string | null
}

export type TransformationalLeaderOptions = {
  operations: string[]
  leaders: TransformationalLeaderCandidate[]
}

export type Device = {
  deviceId: string
  hostname: string
  userName: string
  email: string
  operation: string
  client: string
  department: string
  operatingSystem: string
  agentVersion: string
  lastSeenAtUtc: string
  lastSeenAtLocal?: string | null
}

export type AgentActivityEvent = {
  id: string
  deviceId: string
  userId: string
  userName: string
  email: string
  department: string
  hostname: string
  eventType: 'SessionLocked' | 'SessionUnlocked' | 'DeviceSuspended' | 'DeviceResumed' | string
  lockReason: 'ManualLock' | 'AutoLock' | 'PowerSuspend' | string | null
  idleSecondsAtLock: number | null
  durationSeconds: number | null
  occurredAtUtc: string
  occurredAtLocal?: string | null
}

export type ResponseItem = {
  id: string
  campaignId: string
  questionId: string
  questionText: string
  questionType: QuestionType
  deviceId: string
  userId: string
  userName: string
  email: string
  department: string
  hostname: string
  numericValue: number | null
  minValue: number | null
  maxValue: number | null
  textValue: string | null
  submissionId: string
  answeredAtUtc: string
}

export type OverviewAlertTone = 'positive' | 'warning' | 'critical'
export type OverviewHealthTone = 'healthy' | 'attention' | 'risk' | 'neutral'
export type OverviewMetricTrend = 'up' | 'down' | 'warning' | 'neutral'

export type OverviewAlert = {
  tone: OverviewAlertTone
  eyebrow: string
  title: string
  text: string
}

export type OverviewMetric = {
  label: string
  value: string
  context: string
  trend: OverviewMetricTrend
}

export type OverviewPulseTrendPoint = {
  day: string
  pulse: number | null
}

export type OverviewResponseMixBucket = {
  label: string
  value: number
  percentage: number
}

export type OverviewScaleDistributionBucket = {
  label: string
  value: number
  percentage: number
}

export type OverviewActionItem = {
  campaignId: string
  title: string
  detail: string
  status: CampaignStatus
  actionLabel: string
}

export type OverviewInsight = {
  tone: 'positive' | 'attention'
  eyebrow: string
  title: string
  text: string
}

export type DashboardOverview = {
  healthTone: OverviewHealthTone
  healthLabel: string
  hasSignal: boolean
  activeCampaigns: number
  registeredDevices: number
  responsesToday: number
  averageMood: number | null
  pulseDelta: string | null
  participationRate: number | null
  pendingAlerts: number
  latestEvent: string
  alerts: OverviewAlert[]
  metrics: OverviewMetric[]
  pulseTrend: OverviewPulseTrendPoint[]
  responseMix: OverviewResponseMixBucket[]
  scaleDistribution: OverviewScaleDistributionBucket[]
  noResponseCount: number
  actions: OverviewActionItem[]
  recentActivity: string[]
  insight: OverviewInsight
}

export type AdminUser = {
  id: string
  email: string
  displayName: string
  role: AdminRole | string
}

export type AdminAccount = AdminUser & {
  authenticationMode: 'Entra' | 'Local' | string
  role: AdminRole | string
  isActive: boolean
  createdAtUtc: string
  lastLoginAtUtc: string | null
}

export type AdminSession = {
  token: string
  csrfToken: string
  expiresAtUtc: string
  user: AdminUser
  solvoId?: string
  operation?: string
  operations?: string[]
  returnToPath?: string | null
}

export type TlSession = AdminSession & {
  solvoId: string
  operation: string
  operations: string[]
}

export type TlWeekOption = {
  id: string
  label: string
  startsAt: string
  endsAt: string
}

export type TlQuestionOption = {
  id: string
  text: string
  type: QuestionType
  minValue: number | null
  maxValue: number | null
  options: string[]
}

export type TlCampaignOption = {
  id: string
  name: string
  status: CampaignStatus
  deletedAtUtc: string | null
  weekIds: string[]
  questions: TlQuestionOption[]
}

export type TlResponseRow = {
  id: string
  campaignId: string
  questionId: string
  weekId: string
  campaignName: string
  questionText: string
  questionType: QuestionType
  numericValue: number | null
  textValue: string | null
  userName: string
  email: string
  employeeId: string
  leaderSolvoId: string
  leaderFullName: string
  leaderCorporateEmail: string
  internalEmployeeCategory: string
  jobTitle: string
  operation: string
  employeeStatus: string
  department: string
  hostname: string
  answeredAtUtc: string
}

export type TlDashboard = {
  displayName: string
  solvoId: string
  operation: string
  operations: string[]
  weeks: TlWeekOption[]
  campaigns: TlCampaignOption[]
  responses: TlResponseRow[]
}

export type TlExportJob = {
  id: string
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed' | string
  fileName: string
  responseCount: number
  error: string | null
  createdAtUtc: string
  updatedAtUtc: string
  completedAtUtc: string | null
  downloadedAtUtc: string | null
}
