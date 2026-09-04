using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nakama.Api.Modules.Identity.Domain;
using Nakama.Api.Modules.Notifications.Domain;
using Nakama.Api.Modules.Projects.Domain;
using Nakama.Api.Modules.Tasks.Domain;
namespace Nakama.Api.Modules.Notifications.Infrastructure;
public sealed class NotificationConfiguration:IEntityTypeConfiguration<Notification>{public void Configure(EntityTypeBuilder<Notification>b){b.ToTable("notifications");b.HasKey(x=>x.Id);b.Property(x=>x.Type).HasConversion<string>().HasMaxLength(48);b.Property(x=>x.MetadataJson).HasColumnType("jsonb");b.HasIndex(x=>new{x.RecipientUserId,x.CreatedAt});b.HasIndex(x=>new{x.RecipientUserId,x.ReadAt});b.HasOne<User>().WithMany().HasForeignKey(x=>x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);b.HasOne<User>().WithMany().HasForeignKey(x=>x.ActorUserId).OnDelete(DeleteBehavior.Restrict);b.HasOne<Project>().WithMany().HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Restrict);b.HasOne<WorkTask>().WithMany().HasForeignKey(x=>x.TaskId).OnDelete(DeleteBehavior.Restrict);}}
public sealed class NotificationPreferenceConfiguration:IEntityTypeConfiguration<NotificationPreference>{public void Configure(EntityTypeBuilder<NotificationPreference>b){b.ToTable("notification_preferences");b.HasKey(x=>new{x.UserId,x.Type});b.Property(x=>x.Type).HasConversion<string>().HasMaxLength(48);b.HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);}}
