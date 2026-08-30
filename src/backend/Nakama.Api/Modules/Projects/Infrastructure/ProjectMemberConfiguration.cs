using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Projects.Domain;

namespace Nakama.Api.Modules.Projects.Infrastructure;

public sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("project_members", table => table.HasCheckConstraint(
            "ck_project_members_role",
            "role IN ('Owner', 'Member')"));

        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).HasColumnName("id");
        builder.Property(member => member.ProjectId).HasColumnName("project_id").IsRequired();
        builder.Property(member => member.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(member => member.Role).HasColumnName("role").HasMaxLength(32).HasConversion<string>().IsRequired();
        builder.Property(member => member.JoinedAt).HasColumnName("joined_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(member => member.ProjectId).HasDatabaseName("ix_project_members_project_id");
        builder.HasIndex(member => member.UserId).HasDatabaseName("ix_project_members_user_id");
        builder.HasIndex(member => new { member.ProjectId, member.UserId }).IsUnique().HasDatabaseName("ux_project_members_project_user");
        builder.HasOne<Project>().WithMany().HasForeignKey(member => member.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
