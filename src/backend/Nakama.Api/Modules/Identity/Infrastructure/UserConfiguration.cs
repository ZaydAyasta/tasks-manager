using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Infrastructure;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table => table.HasCheckConstraint(
            "ck_users_role",
            "role IN ('Admin', 'Collaborator')"));

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id");
        builder.Property(user => user.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
        builder.Property(user => user.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(user => user.Role).HasColumnName("role").HasMaxLength(32).HasConversion<string>().IsRequired();
        builder.Property(user => user.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(user => user.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(user => user.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(user => user.Email).IsUnique().HasDatabaseName("ux_users_email");
    }
}
