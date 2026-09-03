import { useEffect, useMemo, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import gsap from 'gsap'
import { motion } from 'framer-motion'
import {
  Avatar,
  Box,
  Button,
  Chip,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Paper,
  Typography,
} from '@mui/material'
import AnalyticsOutlinedIcon from '@mui/icons-material/AnalyticsOutlined'
import AutoGraphOutlinedIcon from '@mui/icons-material/AutoGraphOutlined'
import CampaignOutlinedIcon from '@mui/icons-material/CampaignOutlined'
import ChevronLeftOutlinedIcon from '@mui/icons-material/ChevronLeftOutlined'
import ChevronRightOutlinedIcon from '@mui/icons-material/ChevronRightOutlined'
import DevicesOutlinedIcon from '@mui/icons-material/DevicesOutlined'
import GroupsOutlinedIcon from '@mui/icons-material/GroupsOutlined'
import LogoutOutlinedIcon from '@mui/icons-material/LogoutOutlined'
import ManageAccountsOutlinedIcon from '@mui/icons-material/ManageAccountsOutlined'
import SettingsOutlinedIcon from '@mui/icons-material/SettingsOutlined'
import SupervisorAccountOutlinedIcon from '@mui/icons-material/SupervisorAccountOutlined'
import TimelineOutlinedIcon from '@mui/icons-material/TimelineOutlined'
import { useAdminPanel } from '../hooks/useAdminPanel'
import type { AdminSession, Tab } from '../types'
import { tabCopy } from '../constants'
import { ErrorBanner, TabIntro } from './AppHeader'
import { DashboardAtmosphere } from './DashboardAtmosphere'
import { PulseLoader } from './PulseLoader'
import { AgentsTab } from '../views/AgentsTab'
import { ActivityTab } from '../views/ActivityTab'
import { CampaignsTab } from '../views/CampaignsTab'
import { OverviewTab } from '../views/OverviewTab'
import { ResponsesTab } from '../views/ResponsesTab'
import { SettingsTab } from '../views/SettingsTab'
import { AdminsTab } from '../views/AdminsTab'
import { TransformationalLeadersTab } from '../views/TransformationalLeadersTab'
import { getAllowedTabs, hasAdminRole } from '../utils/adminRoles'

const navItems: Array<{ id: Tab; label: string; icon: ReactNode; accent: string }> = [
  { id: 'overview', label: 'Resumen', icon: <AnalyticsOutlinedIcon />, accent: '#49aef2' },
  { id: 'campaigns', label: 'Campañas', icon: <CampaignOutlinedIcon />, accent: '#f6a642' },
  { id: 'agents', label: 'Agentes', icon: <DevicesOutlinedIcon />, accent: '#7cd9ff' },
  { id: 'activity', label: 'Actividad', icon: <TimelineOutlinedIcon />, accent: '#48c7a8' },
  { id: 'responses', label: 'Insights', icon: <AutoGraphOutlinedIcon />, accent: '#8c7cf7' },
  { id: 'admins', label: 'Admins', icon: <ManageAccountsOutlinedIcon />, accent: '#f06a9c' },
  { id: 'lt', label: 'LT', icon: <SupervisorAccountOutlinedIcon />, accent: '#f06a9c' },
  { id: 'settings', label: 'Configuración', icon: <SettingsOutlinedIcon />, accent: '#9de9f0' },
]

export function AdminShell({
  session,
  profilePhotoUrl,
  activeTab,
  onTabChange,
  onLogout,
}: {
  session: AdminSession
  profilePhotoUrl: string | null
  activeTab: Tab
  onTabChange: (tab: Tab) => void
  onLogout: () => Promise<void> | void
}) {
  const rootRef = useRef<HTMLDivElement | null>(null)
  const sidebarRef = useRef<HTMLElement | null>(null)
  const hasAnimatedSidebarRef = useRef(false)
  const [isCreateCampaignOpen, setIsCreateCampaignOpen] = useState(false)
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false)
  const allowedTabs = useMemo(() => getAllowedTabs(session.user.role), [session.user.role])
  const isOwner = hasAdminRole(session.user.role, 'Owner')
  const panel = useAdminPanel(session.token, session.csrfToken, activeTab, session.user.role)

  useEffect(() => {
    if (!rootRef.current) return
    gsap.fromTo(
      rootRef.current.querySelectorAll('[data-animate]'),
      { opacity: 0, y: 14 },
      { opacity: 1, y: 0, duration: 0.45, stagger: 0.04, clearProps: 'all' },
    )
  }, [])

  useEffect(() => {
    if (!sidebarRef.current) return

    if (!hasAnimatedSidebarRef.current) {
      hasAnimatedSidebarRef.current = true
      return
    }

    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    if (reduceMotion) return

    const sidebar = sidebarRef.current
    const navButtons = sidebar.querySelectorAll('.MuiListItemButton-root')
    const textNodes = sidebar.querySelectorAll('.admin-sidebar__text, .admin-sidebar__nav-text, .admin-sidebar__logout-label')
    const expandedWidth = 248
    const collapsedWidth = 72
    const expandedPadding = 20
    const collapsedPadding = 10
    const fromWidth = isSidebarCollapsed ? expandedWidth : collapsedWidth
    const toWidth = isSidebarCollapsed ? collapsedWidth : expandedWidth
    const fromPadding = isSidebarCollapsed ? expandedPadding : collapsedPadding
    const toPadding = isSidebarCollapsed ? collapsedPadding : expandedPadding

    gsap.killTweensOf([sidebar, ...navButtons, ...textNodes])
    const timeline = gsap.timeline({
      defaults: { ease: 'power3.inOut' },
      onStart: () => {
        gsap.set(sidebar, { transition: 'none' })
      },
      onComplete: () => {
        gsap.set(sidebar, { clearProps: 'width,minWidth,maxWidth,paddingLeft,paddingRight,transition,transform' })
      },
    })

    if (isSidebarCollapsed) {
      timeline
        .fromTo(
          sidebar,
          {
            width: fromWidth,
            minWidth: fromWidth,
            maxWidth: fromWidth,
            paddingLeft: fromPadding,
            paddingRight: fromPadding,
          },
          {
            width: toWidth,
            minWidth: toWidth,
            maxWidth: toWidth,
            paddingLeft: toPadding,
            paddingRight: toPadding,
            duration: 0.52,
            ease: 'expo.inOut',
          },
          0,
        )
        .to(textNodes, {
          opacity: 0,
          x: -12,
          duration: 0.42,
          ease: 'power2.inOut',
          clearProps: 'transform,opacity',
        }, 0)
    } else {
      timeline
        .fromTo(
          sidebar,
          {
            width: fromWidth,
            minWidth: fromWidth,
            maxWidth: fromWidth,
            paddingLeft: fromPadding,
            paddingRight: fromPadding,
          },
          {
            width: toWidth,
            minWidth: toWidth,
            maxWidth: toWidth,
            paddingLeft: toPadding,
            paddingRight: toPadding,
            duration: 0.52,
            ease: 'expo.inOut',
          },
        )
        .fromTo(
          navButtons,
          { x: -5, opacity: 0.78 },
          { x: 0, opacity: 1, duration: 0.24, stagger: 0.018, ease: 'power2.out', clearProps: 'transform,opacity' },
          0.18,
        )
        .fromTo(
          textNodes,
          { opacity: 0, x: -10 },
          { opacity: 1, x: 0, duration: 0.32, stagger: 0.012, ease: 'power2.out', clearProps: 'transform,opacity' },
          0.22,
        )
    }
  }, [isSidebarCollapsed])

  if (!panel.hasLoadedOnce && panel.isRefreshing) {
    return <PulseLoader title="Cargando panel administrativo" caption="Sincronizando campañas, agentes y respuestas" />
  }

  return (
    <Box ref={rootRef} className="admin-layout admin-layout--mui">
      <DashboardAtmosphere />
      <Paper
        ref={sidebarRef}
        component="aside"
        elevation={0}
        className={isSidebarCollapsed ? 'admin-sidebar admin-sidebar--mui admin-sidebar--collapsed' : 'admin-sidebar admin-sidebar--mui'}
        sx={{
          background: 'linear-gradient(180deg, #092333 0%, #0b2030 48%, #071926 100%) !important',
          color: '#ffffff',
          borderColor: 'rgba(18, 63, 79, 0.84)',
          borderRadius: '0 !important',
          boxShadow: 'none',
        }}
      >
        <IconButton
          aria-label={isSidebarCollapsed ? 'Expandir menu lateral' : 'Contraer menu lateral'}
          onClick={() => setIsSidebarCollapsed((value) => !value)}
          className="admin-sidebar__toggle"
        >
          {isSidebarCollapsed ? <ChevronRightOutlinedIcon /> : <ChevronLeftOutlinedIcon />}
        </IconButton>

        <Box className="admin-sidebar__body">
          <Box className="admin-sidebar__brand-lockup">
            <Box component="img" src="/favicon.ico" alt="" className="admin-sidebar__brand-logo" />
            <Box className="admin-sidebar__text">
              <Typography variant="h6" sx={{ fontFamily: 'Sora, Manrope, sans-serif', fontWeight: 900, color: '#ffffff', lineHeight: 1 }}>
                PulseCheck
              </Typography>
            </Box>
          </Box>

          <List disablePadding className="admin-sidebar__nav admin-sidebar__nav--mui">
            {navItems.filter((item) => allowedTabs.includes(item.id)).map((item) => {
              const selected = item.id === panel.tab
              return (
                <ListItemButton
                  key={item.id}
                  selected={selected}
                  onClick={() => onTabChange(item.id)}
                  style={{ '--nav-accent': item.accent } as CSSProperties}
                  sx={{
                    mb: 0.75,
                    borderRadius: 0,
                    minHeight: 46,
                    justifyContent: isSidebarCollapsed ? 'center' : 'flex-start',
                    color: selected ? '#ffffff' : 'rgba(219,244,248,0.82)',
                    bgcolor: selected ? 'rgba(0,190,214,0.18)' : 'transparent',
                    border: '1px solid transparent',
                    borderLeft: selected ? `4px solid ${item.accent}` : '4px solid transparent',
                    '&:hover': { bgcolor: 'rgba(255,255,255,0.10)', color: '#ffffff' },
                    '&.Mui-selected:hover': { bgcolor: 'rgba(0,190,214,0.22)' },
                  }}
                >
                  <ListItemIcon className="admin-sidebar__nav-icon" sx={{ minWidth: isSidebarCollapsed ? 0 : 38, justifyContent: 'center' }}>{item.icon}</ListItemIcon>
                  <ListItemText className="admin-sidebar__nav-text" primary={<Typography sx={{ fontSize: 14, fontWeight: 800 }}>{item.label}</Typography>} />
                </ListItemButton>
              )
            })}
          </List>
        </Box>

        <Paper
          elevation={0}
          className="admin-sidebar__session"
          sx={{ bgcolor: 'transparent', borderColor: 'transparent', borderRadius: 0, boxShadow: 'none' }}
        >
          <Box sx={{ display: 'grid', gap: 1.5 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
              <Avatar src={profilePhotoUrl ?? undefined} className="admin-sidebar__user-avatar" sx={{ width: 36, height: 36, bgcolor: 'primary.light', color: 'primary.dark', fontWeight: 900 }}>
                {session.user.displayName.slice(0, 1).toUpperCase()}
              </Avatar>
              <Box className="admin-sidebar__text" sx={{ minWidth: 0 }}>
                <Typography noWrap variant="body2" sx={{ color: '#ffffff', fontWeight: 800 }}>
                  {session.user.displayName}
                </Typography>
                <Typography noWrap variant="caption" sx={{ color: 'rgba(255,255,255,0.56)' }}>
                  {session.user.email}
                </Typography>
              </Box>
            </Box>
            <Button
              fullWidth
              variant="outlined"
              startIcon={<LogoutOutlinedIcon />}
              className="admin-sidebar__logout-button"
              onClick={() => void onLogout()}
              sx={{
                color: '#ffffff',
                borderColor: 'rgba(255,255,255,0.18)',
                '&:hover': { borderColor: 'rgba(255,255,255,0.34)', bgcolor: 'rgba(255,255,255,0.08)' },
              }}
            >
              <span className="admin-sidebar__logout-label">Cerrar sesion</span>
            </Button>
          </Box>
        </Paper>
      </Paper>

      <Box component="main" className="admin-main-shell">
        <Paper elevation={0} className="admin-command-bar" data-animate>
          <Box sx={{ display: 'flex', flexDirection: { xs: 'column', md: 'row' }, alignItems: { xs: 'flex-start', md: 'center' }, justifyContent: 'space-between', gap: 2 }}>
            <Box>
              <Typography variant="caption" className="admin-command-bar__eyebrow">
                Admin suite
              </Typography>
              <Typography variant="h5" sx={{ fontFamily: 'Sora, Manrope, sans-serif', fontWeight: 800 }}>
                {panel.tab === 'overview' ? 'Consola operativa' : tabCopy[panel.tab].title}
              </Typography>
            </Box>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 1 }}>
              <Chip icon={<GroupsOutlinedIcon />} label={`${panel.overview.registeredDevices} agentes`} />
              <Chip color="primary" label={`${panel.overview.activeCampaigns} campañas activas`} />
            </Box>
          </Box>
        </Paper>

        <Box className="admin-main-panel admin-main-panel--mui">
          <motion.div
            key={panel.tab}
            className="admin-main-content"
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.22, ease: 'easeOut' }}
          >
            <ErrorBanner message={panel.error} />

            {panel.tab !== 'overview' && panel.tab !== 'admins' && panel.tab !== 'settings' && panel.tab !== 'agents' && panel.tab !== 'activity' && panel.tab !== 'lt' ? (
              <TabIntro title={tabCopy[panel.tab].title} description={tabCopy[panel.tab].description} />
            ) : null}

            {panel.tab === 'overview' ? (
              <OverviewTab
                overview={panel.overview}
                devices={panel.devices}
                activityEvents={panel.agentActivityEvents}
                role={session.user.role}
                onOpenCampaigns={() => onTabChange('campaigns')}
                onOpenAgents={() => onTabChange('agents')}
                onOpenActivity={() => onTabChange('activity')}
                onOpenInsights={() => onTabChange('responses')}
              />
            ) : null}

            {panel.tab === 'campaigns' ? (
              <CampaignsTab
                campaignFilter={panel.campaignFilter}
                deliveryMode={panel.deliveryMode}
                editingCampaignId={panel.editingCampaignId}
                filteredCampaigns={panel.filteredCampaigns}
                forceResponse={panel.forceResponse}
                frequencyMode={panel.frequencyMode}
                isSaving={panel.isSaving}
                audienceOptions={panel.audienceOptions}
                questions={panel.questions}
                scheduleDays={panel.scheduleDays}
                scheduleTime={panel.scheduleTime}
                selectedAudienceOperations={panel.selectedAudienceOperations}
                searchTerm={panel.searchTerm}
                onCampaignFilterChange={panel.setCampaignFilter}
                onCancelEdit={() => panel.setEditingCampaignId(null)}
                isCreateDialogOpen={isCreateCampaignOpen}
                onCreateCampaign={panel.handleCreateCampaign}
                onDeleteCampaign={panel.deleteCampaign}
                onDeliveryModeChange={panel.setDeliveryMode}
                onEditCampaign={panel.setEditingCampaignId}
                onOpenCreateDialog={() => setIsCreateCampaignOpen(true)}
                onCloseCreateDialog={() => setIsCreateCampaignOpen(false)}
                onForceResponseChange={panel.setForceResponse}
                onFrequencyModeChange={panel.setFrequencyMode}
                onQuestionsChange={panel.setQuestions}
                onScheduleDaysChange={panel.setScheduleDays}
                onScheduleTimeChange={panel.setScheduleTime}
                onSelectedAudienceOperationsChange={panel.setSelectedAudienceOperations}
                onSearchTermChange={panel.setSearchTerm}
                onSetStatus={panel.setStatus}
                onUpdateCampaign={panel.updateCampaign}
              />
            ) : null}

            {panel.tab === 'agents' ? <AgentsTab devices={panel.devices} /> : null}

            {panel.tab === 'activity' ? <ActivityTab activityEvents={panel.agentActivityEvents} devices={panel.devices} /> : null}

            {panel.tab === 'responses' ? (
              <ResponsesTab
                campaigns={panel.campaigns}
                responses={panel.responses}
                operations={panel.audienceOptions.operations}
                isExportingReport={panel.isExportingReport}
                onExportReport={(filters) => void panel.exportReport(filters)}
              />
            ) : null}

            {panel.tab === 'admins' && isOwner ? (
              <AdminsTab
                admins={panel.admins}
                isCreatingAdmin={panel.isCreatingAdmin}
                deletingAdminId={panel.deletingAdminId}
                updatingAdminId={panel.updatingAdminId}
                onCreateAdmin={panel.createAdmin}
                onDeleteAdmin={panel.deleteAdmin}
                onUpdateAdminStatus={panel.updateAdminStatus}
              />
            ) : null}

            {panel.tab === 'lt' && isOwner ? (
              <TransformationalLeadersTab
                options={panel.transformationalLeaderOptions}
                savingSolvoId={panel.savingTransformationalLeaderSolvoId}
                onSaveAssignment={panel.saveTransformationalLeaderAssignment}
                onClearAssignment={panel.clearTransformationalLeaderAssignment}
              />
            ) : null}

            {panel.tab === 'settings' && isOwner ? (
              <SettingsTab
                connectionState={panel.connectionState}
                clientInactivityAlertOptions={panel.clientInactivityAlertOptions}
                savingClientAlert={panel.savingClientAlert}
                deletingClientAlertId={panel.deletingClientAlertId}
                onSaveClientAlert={panel.saveClientInactivityAlert}
                onDeleteClientAlert={panel.deleteClientInactivityAlert}
              />
            ) : null}
          </motion.div>
        </Box>
      </Box>
    </Box>
  )
}
