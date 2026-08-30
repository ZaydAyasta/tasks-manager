namespace Nakama.Api.BuildingBlocks.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
