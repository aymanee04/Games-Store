
using System.Globalization;
using GameStore.Api.AI.DTOs;
using GameStore.Api.AI.Interfaces;
using Microsoft.VisualBasic.FileIO;

namespace GameStore.Api.AI.Services;

public class CsvGameParser : ICsvGameParser
{
    public List<ExtractedGameDto> Parse(string content)
    {
        using var reader = new StringReader(content);

        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };

        parser.SetDelimiters(",");

        if (parser.EndOfData)
            throw new InvalidOperationException("The CSV file is empty.");

        var headers = parser.ReadFields();

        if (headers is null)
            throw new InvalidOperationException("Could not read CSV headers.");

        var columnIndexes = headers
            .Select((header, index) => new
            {
                Name = header.Trim(),
                Index = index
            })
            .ToDictionary(
                x => x.Name,
                x => x.Index,
                StringComparer.OrdinalIgnoreCase
            );

        string[] requiredColumns =
        [
            "Name",
            "GenreName",
            "StudioName",
            "Price",
            "ReleaseDate"
        ];

        foreach (var column in requiredColumns)
        {
            if (!columnIndexes.ContainsKey(column))
                throw new InvalidOperationException(
                    $"Missing required CSV column: {column}");
        }

        var games = new List<ExtractedGameDto>();

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();

            if (fields is null)
                continue;

            string GetValue(string column)
            {
                var index = columnIndexes[column];
                return index < fields.Length ? fields[index].Trim() : "";
            }

            if (requiredColumns.All(c => string.IsNullOrWhiteSpace(GetValue(c))))
                continue;

            if (!decimal.TryParse(
                    GetValue("Price"),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var price))
            {
                throw new InvalidOperationException(
                    $"Invalid price for game: {GetValue("Name")}");
            }

            DateOnly? releaseDate = null;
            var dateValue = GetValue("ReleaseDate");

            if (!string.IsNullOrWhiteSpace(dateValue))
            {
                if (!DateOnly.TryParseExact(
                        dateValue,
                        ["M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedDate))
                {
                    throw new InvalidOperationException(
                        $"Invalid release date for game: {GetValue("Name")}");
                }

                releaseDate = parsedDate;
            }

            games.Add(new ExtractedGameDto(
                GetValue("Name"),
                GetValue("GenreName"),
                GetValue("StudioName"),
                price,
                releaseDate
            ));
        }

        return games;
    }
}
