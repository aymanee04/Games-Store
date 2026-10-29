namespace GameStore.Api.Entities;

public class Studio
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<Game> Games { get; set; } = [];
}