# PulseCheck API

Backend ASP.NET Core para PulseCheck y repo central de plataforma para infraestructura Azure.

## Contenido

- `PulseCheck.Domain`: entidades y reglas base del dominio
- `PulseCheck.Application`: casos de uso, DTOs y puertos
- `PulseCheck.Infrastructure`: persistencia EF Core, inicializacion y servicios de infraestructura
- `PulseCheck.Api`: API + SignalR
- `infra`: infraestructura Azure con Bicep para `dev` y `prod`
- `pipelines/variables`: variables compartidas de despliegue
- `azure-pipelines-infra.yml`: pipeline de infraestructura
- `azure-pipelines-api.yml`: pipeline de build/deploy de la API

## Arquitectura

El backend sigue una separacion tipo clean architecture / hexagonal:

- `Domain` no depende de ninguna otra capa
- `Application` define casos de uso y puertos
- `Infrastructure` implementa persistencia y adaptadores tecnicos
- `Api` expone HTTP y SignalR como adapters de entrada

La API ya no contiene logica de negocio ni EF directo en controladores.

## Desarrollo local

```powershell
cd .\PulseCheck.Api
dotnet run dev
```

Producción local:

```powershell
cd .\PulseCheck.Api
dotnet run prod
```

Compilar toda la solucion:

```powershell
dotnet build ..\PulseCheck.slnx
```

## Infraestructura

La plantilla `infra/main.bicep` ahora describe la topologia base para ambientes tipo `dev`/`prod` con:

- Azure Static Web App para el panel web
- Azure App Service Linux para la API
- Azure SQL Server + Database
- Application Insights

La autenticacion corporativa con Entra ID se configurara por App Registration y app settings, pero la autorizacion final del panel seguira ocurriendo contra `AdminUsers` en base de datos.

Parámetros por ambiente:

- `infra/dev.bicepparam`
- `infra/prod.bicepparam`

## Pipelines

- `azure-pipelines-infra.yml`
- `azure-pipelines-api.yml`

Variables por ambiente:

- `pipelines/variables/dev.yml`
- `pipelines/variables/prod.yml`

Secretos esperados en Azure DevOps:

- `sqlAdminPassword`
- `staticWebAppDeploymentToken`

Conexiones de servicio esperadas:

- `sc-pulsecheck-dev`
- `sc-pulsecheck-prod`
