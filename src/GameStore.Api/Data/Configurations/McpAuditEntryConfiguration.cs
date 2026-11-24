using GameStore.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameStore.Api.Data.Configurations;

public class McpAuditEntryConfiguration : IEntityTypeConfiguration<McpAuditEntry>
{
    public void Configure(EntityTypeBuilder<McpAuditEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TimestampUtc).IsRequired();
        builder.Property(e => e.AuthMethod).IsRequired().HasMaxLength(16);
        builder.Property(e => e.Kind).IsRequired().HasMaxLength(16);
        builder.Property(e => e.Target).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ArgumentsJson).HasMaxLength(4000);
        builder.Property(e => e.Error).HasMaxLength(500);
        builder.Property(e => e.UserId).HasMaxLength(450);

        builder.HasIndex(e => e.TimestampUtc);
        builder.HasIndex(e => new { e.UserId, e.TimestampUtc });
    }
}