namespace Nakama.Api.BuildingBlocks.Errors;

public sealed record BusinessProblem(string Title, string Detail, int Status);
