using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PulseCheck.Agent;

Environment.SetEnvironmentVariable("PULSECHECK_AGENT_MACHINE_STORAGE", "1");

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    EnvironmentName = ResolveEnvironmentName()
});

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PulseCheck Agent Service";
});

builder.Configuration.AddJsonFile("appsettings.runtime.json", optional: true, reloadOnChange: false);
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.AddHttpClient<PulseCheckApiClient>();
builder.Services.AddSingleton<AgentCredentialStore>();
builder.Services.AddSingleton<AgentIdentityResolver>();
builder.Services.AddSingleton<AgentRuntimeState>();
builder.Services.AddSingleton<LocalQueueStore>();
builder.Services.AddSingleton<AgentActivityQueueStore>();
builder.Services.AddSingleton<AgentActivityQueueService>();
builder.Services.AddSingleton<LocalAgentStateStore>();
builder.Services.AddSingleton<SyncWakeSignal>();
builder.Services.AddSingleton<ICampaignPromptService, PipeCampaignPromptService>();
builder.Services.AddHostedService<CampaignNotificationListener>();
builder.Services.AddHostedService<SessionActivityMonitorService>();
builder.Services.AddHostedService<AutoUpdateService>();
builder.Services.AddHostedService<Worker>();

DiagnosticLog.Write("PulseCheck Agent Service starting.");
await builder.Build().RunAsync();

static string ResolveEnvironmentName()
{
    var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    if (string.IsNullOrWhiteSpace(environmentName))
    {
        environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    }

    return string.IsNullOrWhiteSpace(environmentName) ? Environments.Production : environmentName;
}
