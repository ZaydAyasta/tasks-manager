using Microsoft.EntityFrameworkCore;
using Nakama.Api.BuildingBlocks.Persistence;
using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Identity.Authentication;
using Nakama.Api.Modules.Projects.Domain;
using Nakama.Api.Modules.Tasks.Domain;
using DomainTaskStatus = Nakama.Api.Modules.Tasks.Domain.TaskStatus;

namespace Nakama.Api.Modules.Dashboard.Features;

public sealed record AdminDashboardResponse(
    DashboardSummaryResponse Summary,
    IReadOnlyList<DashboardProjectResponse> Projects,
    IReadOnlyList<DashboardAttentionItemResponse> Attention);

public sealed record DashboardSummaryResponse(
    int ActiveProjects,
    int ActiveTasks,
    int BlockedTasks,
    int InReviewTasks,
    int OverdueTasks,
    int CriticalTasks);

public sealed record DashboardProjectResponse(
    Guid Id,
    string Name,
    string Status,
    DashboardTaskProgressResponse TaskProgress,
    int BlockedCount,
    int InReviewCount,
    int OverdueCount);

public sealed record DashboardTaskProgressResponse(int Completed, int Total);

public sealed record DashboardAttentionItemResponse(
    Guid TaskId,
    string Title,
    Guid ProjectId,
    string ProjectName,
    string Status,
    string Priority,
    DateTimeOffset? DueDate,
    IReadOnlyList<DashboardAssigneeResponse> Assignees);

public sealed record DashboardAssigneeResponse(Guid Id, string FullName);

internal static class DashboardEndpoints
{
    private const int AttentionLimit = 15;

    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/dashboard", Get)
            .WithTags("Admin Dashboard")
            .RequireAuthorization(Policies.Admin);
    }

    private static async Task<IResult> Get(NakamaDbContext db, IClock clock, CancellationToken ct)
    {
        var now = clock.UtcNow;

        var projects = await db.Projects
            .AsNoTracking()
            .OrderBy(project => project.Status == ProjectStatus.Active ? 0 : project.Status == ProjectStatus.Paused ? 1 : 2)
            .ThenBy(project => project.Name)
            .Select(project => new ProjectSeed(project.Id, project.Name, project.Status))
            .ToListAsync(ct);

        var projectIds = projects.Select(project => project.Id).ToArray();
        var projectMetrics = projectIds.Length == 0
            ? []
            : await db.Tasks
                .AsNoTracking()
                .Where(task => projectIds.Contains(task.ProjectId))
                .GroupBy(task => task.ProjectId)
                .Select(group => new ProjectMetrics(
                    group.Key,
                    group.Count(task => task.Status == DomainTaskStatus.Completed),
                    group.Count(),
                    group.Count(task => task.Status == DomainTaskStatus.Blocked),
                    group.Count(task => task.Status == DomainTaskStatus.InReview),
                    group.Count(task => task.DueDate.HasValue
                        && task.DueDate.Value < now
                        && task.Status != DomainTaskStatus.Completed
                        && task.Status != DomainTaskStatus.Cancelled)))
                .ToListAsync(ct);

        var metricsByProject = projectMetrics.ToDictionary(metrics => metrics.ProjectId);

        var totals = await db.Tasks
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new TaskTotals(
                group.Count(task => task.Status != DomainTaskStatus.Completed && task.Status != DomainTaskStatus.Cancelled),
                group.Count(task => task.Status == DomainTaskStatus.Blocked),
                group.Count(task => task.Status == DomainTaskStatus.InReview),
                group.Count(task => task.DueDate.HasValue
                    && task.DueDate.Value < now
                    && task.Status != DomainTaskStatus.Completed
                    && task.Status != DomainTaskStatus.Cancelled),
                group.Count(task => task.Priority == TaskPriority.Critical
                    && task.Status != DomainTaskStatus.Completed
                    && task.Status != DomainTaskStatus.Cancelled)))
            .FirstOrDefaultAsync(ct)
            ?? new TaskTotals(0, 0, 0, 0, 0);

        var attention = await (
                from task in db.Tasks.AsNoTracking()
                join project in db.Projects.AsNoTracking() on task.ProjectId equals project.Id
                where task.Status != DomainTaskStatus.Completed
                    && task.Status != DomainTaskStatus.Cancelled
                    && (task.Status == DomainTaskStatus.Blocked
                        || (task.DueDate.HasValue && task.DueDate.Value < now)
                        || task.Status == DomainTaskStatus.InReview
                        || task.Priority == TaskPriority.Critical)
                select new { Task = task, Project = project })
            .OrderBy(item => item.Task.Status == DomainTaskStatus.Blocked && item.Task.Priority == TaskPriority.Critical ? 0
                : item.Task.Status == DomainTaskStatus.Blocked ? 1
                : item.Task.DueDate.HasValue && item.Task.DueDate.Value < now && item.Task.Priority == TaskPriority.Critical ? 2
                : item.Task.DueDate.HasValue && item.Task.DueDate.Value < now ? 3
                : item.Task.Status == DomainTaskStatus.InReview ? 4
                : 5)
            .ThenBy(item => item.Task.DueDate == null)
            .ThenBy(item => item.Task.DueDate)
            .ThenBy(item => item.Task.Id)
            .Select(item => new AttentionSeed(
                item.Task.Id,
                item.Task.Title,
                item.Project.Id,
                item.Project.Name,
                item.Task.Status,
                item.Task.Priority,
                item.Task.DueDate))
            .Take(AttentionLimit)
            .ToListAsync(ct);

        var attentionTaskIds = attention.Select(item => item.TaskId).ToArray();
        var assignees = attentionTaskIds.Length == 0
            ? []
            : await (
                from assignment in db.TaskAssignees.AsNoTracking()
                join user in db.Users.AsNoTracking() on assignment.UserId equals user.Id
                where attentionTaskIds.Contains(assignment.TaskId)
                orderby user.FullName
                select new AttentionAssigneeSeed(assignment.TaskId, new DashboardAssigneeResponse(user.Id, user.FullName)))
            .ToListAsync(ct);

        var assigneesByTask = assignees
            .GroupBy(item => item.TaskId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<DashboardAssigneeResponse>)group.Select(item => item.Assignee).ToList());

        var summary = new DashboardSummaryResponse(
            projects.Count(project => project.Status == ProjectStatus.Active),
            totals.ActiveTasks,
            totals.BlockedTasks,
            totals.InReviewTasks,
            totals.OverdueTasks,
            totals.CriticalTasks);

        var projectResponses = projects.Select(project =>
        {
            var metrics = metricsByProject.GetValueOrDefault(project.Id) ?? new ProjectMetrics(project.Id, 0, 0, 0, 0, 0);
            return new DashboardProjectResponse(
                project.Id,
                project.Name,
                project.Status.ToString(),
                new DashboardTaskProgressResponse(metrics.Completed, metrics.Total),
                metrics.BlockedCount,
                metrics.InReviewCount,
                metrics.OverdueCount);
        }).ToList();

        var attentionResponses = attention.Select(item => new DashboardAttentionItemResponse(
            item.TaskId,
            item.Title,
            item.ProjectId,
            item.ProjectName,
            item.Status.ToString(),
            item.Priority.ToString(),
            item.DueDate,
            assigneesByTask.GetValueOrDefault(item.TaskId) ?? [])).ToList();

        return Results.Ok(new AdminDashboardResponse(summary, projectResponses, attentionResponses));
    }

    private sealed record ProjectSeed(Guid Id, string Name, ProjectStatus Status);
    private sealed record ProjectMetrics(Guid ProjectId, int Completed, int Total, int BlockedCount, int InReviewCount, int OverdueCount);
    private sealed record TaskTotals(int ActiveTasks, int BlockedTasks, int InReviewTasks, int OverdueTasks, int CriticalTasks);
    private sealed record AttentionSeed(Guid TaskId, string Title, Guid ProjectId, string ProjectName, DomainTaskStatus Status, TaskPriority Priority, DateTimeOffset? DueDate);
    private sealed record AttentionAssigneeSeed(Guid TaskId, DashboardAssigneeResponse Assignee);
}
