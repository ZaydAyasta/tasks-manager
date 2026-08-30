using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nakama.Api.Modules.Projects.Domain;
namespace Nakama.Api.Modules.Projects.Infrastructure;
public sealed class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> b)
    {
        b.ToTable("stages", t => t.HasCheckConstraint("ck_stages_position", "position >= 1")); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.ProjectId).HasColumnName("project_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired(); b.Property(x => x.NameNormalized).HasColumnName("name_normalized").HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000); b.Property(x => x.Position).HasColumnName("position"); b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken(); b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.ProjectId).HasDatabaseName("ix_stages_project_id"); b.HasIndex(x => new { x.ProjectId, x.NameNormalized }).IsUnique().HasDatabaseName("ux_stages_project_name"); b.HasIndex(x => new { x.ProjectId, x.Position }).IsUnique().HasDatabaseName("ux_stages_project_position");
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
