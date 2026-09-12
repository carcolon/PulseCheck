# PulseCheck Agent

Agente Windows .NET Worker para sincronizar configuración y enviar respuestas al backend de PulseCheck.

## Desarrollo local

```powershell
cd .\PulseCheck.Agent
$env:DOTNET_ENVIRONMENT="Development"
dotnet run
```

## Build

```powershell
cd .\PulseCheck.Agent
dotnet build
```

## Windows Service + Tray installer

```powershell
.\build-windows-service-installer.ps1 -Version 1.0.0
```

The generated installer runs per-machine, installs `PulseCheckAgentService` as an automatic Windows Service, and starts `PulseCheck.Agent.exe --tray` at user logon for the interactive UI.

## Pipeline

- `azure-pipelines-agent.yml`
