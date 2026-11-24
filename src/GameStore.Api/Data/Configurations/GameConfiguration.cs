using GameStore.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GameStore.Api.Data.Configurations;

public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.HasKey(g => g.Id);

        var lowercaseConverter = new ValueConverter<string, string>(
            value => value.Trim().ToLowerInvariant(),
            value => value);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasConversion(lowercaseConverter);

        builder.Property(g => g.Price)
            .HasPrecision(8, 2);

        builder.HasOne(g => g.Genre)
            .WithMany(genre => genre.Games)
            .HasForeignKey(g => g.GenreId)
            .OnDelete(DeleteBehavior.Restrict);   // cannot delete a genre that still has games

        builder.HasIndex(g => g.Name);
    }
}
