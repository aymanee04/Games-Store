using System.Net.Http.Headers;

namespace GameStore.Client.Services;

/// <summary>Adds "Authorization: Bearer ..." to every API call when the user is logged in.</summary>
public class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthClient _auth;

    public AuthTokenHandler(AuthClient auth)
    {
        _auth = auth;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _auth.GetValidAccessTokenAsync();

        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
