using System.Net.Http.Headers;
using System.Net.Http.Json;
using GameStore.Client.Models;
using GameStore.Shared.Dtos;
using Microsoft.AspNetCore.Components.Forms;

namespace GameStore.Client.Services;

/// <summary>Admin-only calls. The bearer token is added by AuthTokenHandler.</summary>
public class ImportApi(HttpClient http)
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    public async Task<List<ExtractedGameResponse>> ExtractFromFileAsync(IBrowserFile file, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(MaxFileBytes, ct);

        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "File", file.Name);   // field name must match ExtractGamesRequest.File

        using var response = await http.PostAsync("api/ai/extractCsv", content, ct);
        return await ReadGamesAsync(response, ct);
    }

    public async Task<List<GameDto>> ConfirmAsync(IEnumerable<ImportRow> rows, CancellationToken ct = default)
    {
        var request = new ImportConfirmRequest(rows.Select(r => r.ToPayload()).ToList());

        using var response = await http.PostAsJsonAsync("api/ai/confirm", request, ct);
        await ApiErrors.EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<GameDto>>(cancellationToken: ct) ?? [];
    }

    private static async Task<List<ExtractedGameResponse>> ReadGamesAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await ApiErrors.EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ExtractedGameResponse>>(cancellationToken: ct) ?? [];
    }
}