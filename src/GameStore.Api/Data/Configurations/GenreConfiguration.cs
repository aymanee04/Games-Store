using GameStore.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GameStore.Api.Data.Configurations;

public class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.HasKey(g => g.Id);

        var lowercaseConverter = new ValueConverter<string, string>(
            value => value.Trim().ToLowerInvariant(),
            value => value);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion(lowercaseConverter);

        builder.HasIndex(g => g.Name).IsUnique();
    }
}
