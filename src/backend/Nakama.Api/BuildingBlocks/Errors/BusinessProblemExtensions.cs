namespace Nakama.Api.BuildingBlocks.Errors;

public static class BusinessProblemExtensions
{
    public static IResult ToProblem(this BusinessProblem problem) => Results.Problem(
        title: problem.Title,
        detail: problem.Detail,
        statusCode: problem.Status,
        type: $"https://httpstatuses.com/{problem.Status}");

    public static BusinessProblem Validation(string detail) => new("Validation failed", detail, StatusCodes.Status400BadRequest);
    public static BusinessProblem NotFound(string detail) => new("Resource not found", detail, StatusCodes.Status404NotFound);
    public static BusinessProblem Conflict(string detail) => new("Business conflict", detail, StatusCodes.Status409Conflict);
}
