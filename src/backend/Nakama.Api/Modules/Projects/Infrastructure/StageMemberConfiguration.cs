using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Projects.Domain;
namespace Nakama.Api.Modules.Projects.Infrastructure;
public sealed class StageMemberConfiguration : IEntityTypeConfiguration<StageMember>
{
    public void Configure(EntityTypeBuilder<StageMember> b)
    {
        b.ToTable("stage_members", t => t.HasCheckConstraint("ck_stage_members_role", "role IN ('Responsible', 'Member')")); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.StageId).HasColumnName("stage_id"); b.Property(x => x.UserId).HasColumnName("user_id"); b.Property(x => x.Role).HasColumnName("role").HasMaxLength(32).HasConversion<string>(); b.Property(x => x.AssignedAt).HasColumnName("assigned_at").HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.StageId).HasDatabaseName("ix_stage_members_stage_id"); b.HasIndex(x => x.UserId).HasDatabaseName("ix_stage_members_user_id"); b.HasIndex(x => new { x.StageId, x.UserId }).IsUnique().HasDatabaseName("ux_stage_members_stage_user");
        b.HasOne<Stage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Cascade); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
