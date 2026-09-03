namespace PulseCheck.Domain.Abstractions;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
