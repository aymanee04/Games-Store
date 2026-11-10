namespace GameStore.Api.AI.Interfaces;

public interface IDocumentExtractionService
{
    Task<string> ExtractTextAsync(
        IFormFile file,
        CancellationToken ct);
}