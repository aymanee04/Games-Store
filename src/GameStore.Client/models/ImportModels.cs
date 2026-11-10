namespace GameStore.Client.Models;

/// <summary>What POST /api/ai/games/import/extract* returns for each game.</summary>
public record ExtractedGameResponse(
    string Name,
    string GenreName,
    string StudioName,
    decimal Price,
    DateOnly? ReleaseDate);

/// <summary>One game sent to POST /api/ai/games/import/confirm.</summary>
public record ImportGamePayload(
    string Name,
    string GenreName,
    string StudioName,
    decimal Price,
    DateOnly ReleaseDate);

public record ImportConfirmRequest(List<ImportGamePayload> Games);

public record RowValidation(string? Name, string? Genre, string? Studio, string? Price, string? Date)
{
    public bool IsValid => Name is null && Genre is null && Studio is null && Price is null && Date is null;
}

/// <summary>A mutable table row the admin can edit before saving.</summary>
public class ImportRow
{
    // Mirrors the server-side rules (CreateGameRequest / ValidReleaseDate).
    private const int MaxNameLength = 100;
    private const decimal MaxPrice = 1000m;
    private const int MinYear = 1950;
    private const int MaxYearsAhead = 15;

    public string Name { get; set; } = "";
    public string GenreName { get; set; } = "";
    public string StudioName { get; set; } = "";
    public decimal? Price { get; set; }
    public DateOnly? ReleaseDate { get; set; }

    /// <summary>Set when the AI/CSV gave a genre that is not in the database.</summary>
    public string? GenreHint { get; set; }
    public string? StudioHint { get; set; }

    public static ImportRow From(
        ExtractedGameResponse source,
        IEnumerable<string> genres,
        IEnumerable<string> studios)
    {
        var row = new ImportRow
        {
            Name = source.Name,
            Price = source.Price,
            ReleaseDate = source.ReleaseDate
        };

        (row.GenreName, row.GenreHint) = Match(source.GenreName, genres);
        (row.StudioName, row.StudioHint) = Match(source.StudioName, studios);
        return row;
    }

    // A <select> can only show values that exist as options, so unknown names are
    // cleared (and remembered as a hint) instead of silently looking selected.
    private static (string Value, string? Hint) Match(string? suggested, IEnumerable<string> options)
    {
        var trimmed = suggested?.Trim() ?? "";
        var hit = options.FirstOrDefault(o => string.Equals(o, trimmed, StringComparison.OrdinalIgnoreCase));

        if (hit is not null) return (hit, null);
        return ("", trimmed.Length == 0 ? null : $"\"{trimmed}\" does not exist yet");
    }

    public RowValidation Validate(IEnumerable<string> genres, IEnumerable<string> studios)
    {
        string? name = null, genre = null, studio = null, price = null, date = null;

        if (string.IsNullOrWhiteSpace(Name)) name = "Name is required";
        else if (Name.Trim().Length > MaxNameLength) name = $"Max {MaxNameLength} characters";

        if (!genres.Contains(GenreName, StringComparer.OrdinalIgnoreCase)) genre = "Choose a genre";
        if (!studios.Contains(StudioName, StringComparer.OrdinalIgnoreCase)) studio = "Choose a studio";

        if (Price is null) price = "Price is required";
        else if (Price < 0 || Price > MaxPrice) price = $"Between 0 and {MaxPrice:0}";

        if (ReleaseDate is null) date = "Date is required";
        else
        {
            var min = new DateOnly(MinYear, 1, 1);
            var max = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(MaxYearsAhead);
            if (ReleaseDate < min || ReleaseDate > max) date = $"Between {MinYear} and {max.Year}";
        }

        return new RowValidation(name, genre, studio, price, date);
    }

    public ImportGamePayload ToPayload() =>
        new(Name.Trim(), GenreName, StudioName, Price!.Value, ReleaseDate!.Value);
}
