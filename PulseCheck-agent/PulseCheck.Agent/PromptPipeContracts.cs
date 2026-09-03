using System.Text.Json.Serialization;

namespace PulseCheck.Agent;

public static class PromptPipe
{
    public const string Name = "PulseCheck.Agent.Prompt.v1";
}

public sealed record PromptPipeRequest(
    string MessageType,
    Guid RequestId,
    AgentCampaignConfiguration Campaign,
    bool ForceResponseForPostponeLimit,
    DateTimeOffset SentAtUtc);

public sealed record PromptPipeResponse(
    string MessageType,
    Guid RequestId,
    PromptResult? Result,
    string? Error,
    DateTimeOffset SentAtUtc);

[JsonSerializable(typeof(PromptPipeRequest))]
[JsonSerializable(typeof(PromptPipeResponse))]
[JsonSerializable(typeof(AgentCampaignConfiguration))]
[JsonSerializable(typeof(AgentQuestion))]
[JsonSerializable(typeof(PromptResult))]
[JsonSerializable(typeof(PromptAnswer))]
internal sealed partial class PromptPipeJsonContext : JsonSerializerContext;
