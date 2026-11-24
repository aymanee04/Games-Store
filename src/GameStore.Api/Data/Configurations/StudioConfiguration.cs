using GameStore.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GameStore.Api.Data.Configurations;

public class StudioConfiguration : IEntityTypeConfiguration<Studio>
{
    public void Configure(EntityTypeBuilder<Studio> builder)
    {
        builder.HasKey(g => g.Id);
        
        var lowercaseConverter = new ValueConverter<string, string>(
            value => value.Trim().ToLowerInvariant(),
            value => value);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasConversion(lowercaseConverter);

        builder.HasIndex(g => g.Name).IsUnique();
        
        builder.HasMany(g => g.Games)
            .WithOne(g => g.Studio)
            .HasForeignKey(g => g.StudioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}