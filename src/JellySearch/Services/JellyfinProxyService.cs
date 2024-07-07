using JellySearch.Helpers;
using Microsoft.Extensions.Primitives;

namespace JellySearch.Services;

public class JellyfinProxyService : IHostedService, IDisposable
{
    private HttpClient Client { get; }
    private ILogger Log { get; set; }

    private string? JellyfinUrl { get; set; } = Environment.GetEnvironmentVariable("JELLYFIN_URL");
    //private string? JellyfinToken { get; set; } = Environment.GetEnvironmentVariable("JELLYFIN_TOKEN");

    private string JellyfinSearchUrl { get; } = "{0}/Users/{1}/Items{2}";
    private string JellyfinAltSearchUrl { get; } = "{0}/Items{1}";

    public JellyfinProxyService(ILoggerFactory logFactory)
    {
        this.Client = new HttpClient();
        this.Log = logFactory.CreateLogger<JellyfinProxyService>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        this.Dispose();
    }

    private string GetUrl(string? userId, string query)
    {
        if(userId == null)
            return string.Format(this.JellyfinAltSearchUrl, this.JellyfinUrl, query); // Search without user ID (e.g. genres)
        else
            return string.Format(this.JellyfinSearchUrl, this.JellyfinUrl, userId, query);

    }

    public async Task<string?> ProxySearchRequest(string authorization, string? legacyToken, string? userId, Dictionary<string, StringValues> arguments)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, this.GetUrl(userId, HttpHelper.GetQueryString(arguments)));

        request.Headers.Add("Authorization", authorization);

        if(legacyToken != null)
            request.Headers.Add("X-Mediabrowser-Token", legacyToken);

        var response = await this.Client.SendAsync(request);

        if(response.StatusCode == System.Net.HttpStatusCode.OK)
            return await response.Content.ReadAsStringAsync();
        else
        {
            this.Log.LogError("Got error from Jellyfin: {error}", response.StatusCode);
            this.Log.LogError("{error}", await response.Content.ReadAsStringAsync());
            return null;
        }
    }

    public async Task<string> ProxyRequest(string authorization, string? legacyToken, string path, string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, string.Format("{0}{1}{2}", this.JellyfinUrl, path, query));

        request.Headers.Add("Authorization", authorization);

        if(legacyToken != null)
            request.Headers.Add("X-Mediabrowser-Token", legacyToken);

        var response = await this.Client.SendAsync(request);

        if(response.StatusCode == System.Net.HttpStatusCode.OK)
            return await response.Content.ReadAsStringAsync();
        else
        {
            this.Log.LogError("Got error from Jellyfin: {error}", response.StatusCode);
            this.Log.LogError("{error}", await response.Content.ReadAsStringAsync());
            return null;
        }
    }

    public void Dispose()
    {
        this.Client.Dispose();
    }
}
