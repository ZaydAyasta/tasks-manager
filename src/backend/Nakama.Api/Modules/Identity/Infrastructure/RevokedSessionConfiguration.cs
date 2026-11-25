using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nakama.Api.Modules.Identity.Domain;

namespace Nakama.Api.Modules.Identity.Infrastructure;

internal sealed class RevokedSessionConfiguration : IEntityTypeConfiguration<RevokedSession>
{
    public void Configure(EntityTypeBuilder<RevokedSession> builder)
    {
        builder.ToTable("revoked_sessions");
        builder.HasKey(session => session.Fingerprint);
        builder.Property(session => session.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
        builder.Property(session => session.ExpiresAt).HasColumnName("expires_at");
        builder.HasIndex(session => session.ExpiresAt);
    }
}
