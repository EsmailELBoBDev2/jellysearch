using JellySearch.Helpers;
using Microsoft.Extensions.Primitives;

namespace JellySearch.Services;

public class JellyfinProxyService : IHostedService, IDisposable
{
    private HttpClient Client { get; }
    private ILogger Log { get; set; }

    private string? JellyfinUrl { get; set; } = Environment.GetEnvironmentVariable("JELLYFIN_URL");
    private string? JellyfinToken { get; set; } = Environment.GetEnvironmentVariable("JELLYFIN_TOKEN");

    private string JellyfinSearchUrl { get; } = "{0}/Users/{1}/Items{2}";

    public JellyfinProxyService(ILoggerFactory logFactory)
    {
        this.Client = new HttpClient();
        this.Log = logFactory.CreateLogger<JellyfinProxyService>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        this.Client.DefaultRequestHeaders.Add("Authorization", string.Format("MediaBrowser Client=\"JellySearch\", Token=\"{0}\"", this.JellyfinToken));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        this.Dispose();
    }

    public async Task<string> ProxySearchRequest(string userId, Dictionary<string, StringValues> arguments)
    {
        var response = await this.Client.GetAsync(string.Format(this.JellyfinSearchUrl, this.JellyfinUrl, userId, HttpHelper.GetQueryString(arguments)));
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> ProxySearchRequest(string userId, string query)
    {
        var response = await this.Client.GetAsync(string.Format(this.JellyfinSearchUrl, this.JellyfinUrl, userId, query));
        return await response.Content.ReadAsStringAsync();
    }

    public void Dispose()
    {
        this.Client.Dispose();
    }
}
