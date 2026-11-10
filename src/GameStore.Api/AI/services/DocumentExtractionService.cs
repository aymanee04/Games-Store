using GameStore.Api.AI.Interfaces;
using GameStore.Api.Exceptions;

namespace GameStore.Api.AI.Services;

public class DocumentExtractionService : IDocumentExtractionService
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    public async Task<string> ExtractTextAsync(
        IFormFile file,
        CancellationToken ct)
    {
        if (file.Length == 0)
            throw new BadRequestException("The uploaded file is empty.");

        if (file.Length > MaxFileSize)
            throw new BadRequestException("Maximum file size is 5 MB.");

        var extension = Path.GetExtension(file.FileName);

        if (!string.Equals(extension, ".csv",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                "Only CSV files are supported currently.");
        }

        await using var stream = file.OpenReadStream();

        using var reader = new StreamReader(stream);

        var content = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(content))
            throw new BadRequestException("The CSV file contains no data.");

        return content;
    }
}