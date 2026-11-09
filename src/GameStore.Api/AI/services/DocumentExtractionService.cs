using Microsoft.VisualBasic.FileIO;
using GameStore.Api.AI.Interfaces;

namespace GameStore.Api.AI.Services;

public class DocumentExtractionService : IDocumentExtractionService
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    public async Task<string> ExtractTextAsync(
        IFormFile file,
        CancellationToken ct)
    {
        if (file.Length == 0)
            throw new InvalidOperationException("The uploaded file is empty.");

        if (file.Length > MaxFileSize)
            throw new InvalidOperationException("Maximum file size is 5 MB.");

        var extension = Path.GetExtension(file.FileName);

        if (!string.Equals(extension, ".csv",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only CSV files are supported currently.");
        }

        await using var stream = file.OpenReadStream();

        using var reader = new StreamReader(stream);

        var content = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("The CSV file contains no data.");

        return content;
    }

    // private static string ParseCsv(string content)
    // {
    //     using var reader = new StringReader(content);
    //
    //     using var parser = new TextFieldParser(reader)
    //     {
    //         TextFieldType = FieldType.Delimited,
    //         HasFieldsEnclosedInQuotes = true,
    //         TrimWhiteSpace = true
    //     };
    //
    //     parser.SetDelimiters(",");
    //
    //     var rows = new List<string>();
    //
    //     while (!parser.EndOfData)
    //     {
    //         var fields = parser.ReadFields();
    //
    //         if (fields is null)
    //             continue;
    //
    //         rows.Add(string.Join(" | ", fields));
    //     }
    //
    //     return string.Join(Environment.NewLine, rows);
    // }
}