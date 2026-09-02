using Microsoft.EntityFrameworkCore;
using Nakama.Api.Modules.Activity.Domain;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Projects.Domain;
using Nakama.Api.Modules.Tasks.Domain;

namespace Nakama.Api.BuildingBlocks.Persistence;

public sealed class NakamaDbContext(DbContextOptions<NakamaDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<StageMember> StageMembers => Set<StageMember>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<TaskAssignee> TaskAssignees => Set<TaskAssignee>();
    public DbSet<TaskBlocker> TaskBlockers => Set<TaskBlocker>();
    public DbSet<Subtask> Subtasks => Set<Subtask>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NakamaDbContext).Assembly);
    }
}
