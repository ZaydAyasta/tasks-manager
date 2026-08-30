using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Projects.Domain;

namespace Nakama.Api.Modules.Projects.Infrastructure;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects", table => table.HasCheckConstraint(
            "ck_projects_status",
            "status IN ('Active', 'Paused', 'Completed', 'Cancelled')"));

        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id).HasColumnName("id");
        builder.Property(project => project.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(project => project.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(project => project.Status).HasColumnName("status").HasMaxLength(32).HasConversion<string>().IsRequired();
        builder.Property(project => project.StartDate).HasColumnName("start_date").HasColumnType("date");
        builder.Property(project => project.EndDate).HasColumnName("end_date").HasColumnType("date");
        builder.Property(project => project.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(project => project.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(project => project.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(project => project.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();

        builder.HasIndex(project => project.Status).HasDatabaseName("ix_projects_status");
        builder.HasOne<User>().WithMany().HasForeignKey(project => project.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
