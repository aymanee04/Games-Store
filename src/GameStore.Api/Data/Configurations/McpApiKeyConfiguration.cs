using GameStore.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GameStore.Api.Data.Configurations;

public class McpApiKeyConfiguration : IEntityTypeConfiguration<McpApiKey>
{
    public void Configure(EntityTypeBuilder<McpApiKey> builder)
    {
        builder.HasKey(k => k.Id);

        var lowercaseConverter = new ValueConverter<string, string>(
            value => value.Trim().ToLowerInvariant(),
            value => value);

        builder.Property(k => k.UserId).IsRequired().HasMaxLength(450);
        builder.Property(k => k.Name).IsRequired().HasMaxLength(100).HasConversion(lowercaseConverter);
        builder.Property(k => k.KeyHash).IsRequired().HasMaxLength(64);
        builder.Property(k => k.KeyPrefix).IsRequired().HasMaxLength(16);
        builder.Property(k => k.CreatedAtUtc).IsRequired();
        builder.Property(k => k.ExpiresAtUtc).IsRequired();

        // Every request with a key looks it up by hash
        builder.HasIndex(k => k.KeyHash).IsUnique();
        builder.HasIndex(k => k.UserId);

        // Deleting a user deletes their keys
        builder.HasOne(k => k.User)
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}