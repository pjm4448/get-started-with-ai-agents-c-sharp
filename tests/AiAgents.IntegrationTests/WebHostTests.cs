using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using AiAgents.Web.Authentication;
using AiAgents.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Identity.Web;

namespace AiAgents.IntegrationTests;

public sealed class WebHostTests
{
    [Theory]
    [InlineData("/alive")]
    [InlineData("/health")]
    public async Task ProbesAreAvailableWithoutSignIn(string path)
    {
        await using var factory = new TestWebHost();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/_blazor/negotiate?negotiateVersion=1")]
    [InlineData("/account/sign-out")]
    public async Task AnonymousRequestsCannotAccessTheApplication(string path)
    {
        await using var factory = new TestWebHost();
        using var client = CreateClient(factory);

        using var response = path == "/"
            ? await client.GetAsync(path)
            : await client.PostAsync(path, null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith(
            "https://login.microsoftonline.com/11111111-1111-4111-8111-111111111111/",
            response.Headers.Location.AbsoluteUri);
    }

    [Fact]
    public async Task SignedInUsersSeeTheUnavailableChatWithoutAnAgentFallback()
    {
        await using var factory = new TestWebHost();
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("Cookie", await CreateSessionCookieAsync(factory));

        using var response = await client.GetAsync("/");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("You are signed in.", html);
        Assert.Contains("Chat is unavailable until an approved Foundry agent is configured and chat is explicitly enabled.", html);
        Assert.Matches("<textarea[^>]*disabled", html);
        Assert.Contains("Sign out of this app", html);
        Assert.Null(factory.Services.GetService<IAgentClient>());
        Assert.Null(factory.Services.GetService<IConversationStore>());
    }

    [Fact]
    public async Task EnabledChatWithMissingFoundryConfigurationFailsAtStartup()
    {
        await using var factory = new TestWebHost(chatEnabled: true);

        var exception = Assert.Throws<OptionsValidationException>(() => CreateClient(factory));

        Assert.Contains("Enabled chat requires Foundry:ProjectEndpoint", exception.Message);
    }

    [Theory]
    [InlineData("http://foundry.example/api/projects/test")]
    [InlineData("https://foundry.example/api/projects/test?unexpected=value")]
    public async Task EnabledChatWithInvalidEndpointFailsAtStartup(string endpoint)
    {
        await using var factory = new TestWebHost(chatEnabled: true, foundryConfigured: true, projectEndpoint: endpoint);

        var exception = Assert.Throws<OptionsValidationException>(() => CreateClient(factory));

        Assert.Contains("Enabled chat requires Foundry:ProjectEndpoint", exception.Message);
        Assert.DoesNotContain(endpoint, exception.Message);
    }

    [Fact]
    public async Task EnabledChatCanPrerenderWithoutAccessingBrowserStorageOrFoundry()
    {
        await using var factory = new TestWebHost(chatEnabled: true, foundryConfigured: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("Cookie", await CreateSessionCookieAsync(factory));

        using var response = await client.GetAsync("/");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Loading conversation...", html);
        Assert.DoesNotContain("Chat is unavailable", html);
        Assert.Matches("<textarea[^>]*disabled", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task SignOutRejectsMissingOrInvalidAntiforgeryToken(string? token)
    {
        await using var factory = new TestWebHost();
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("Cookie", await CreateSessionCookieAsync(factory));
        using var form = new FormUrlEncodedContent(token is null
            ? []
            : new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

        using var response = await client.PostAsync("/account/sign-out", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var page = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task SignOutWithTheRenderedTokenClearsTheCookieAndReturnsToAnAnonymousPage()
    {
        await using var factory = new TestWebHost();
        using var client = CreateClient(factory);
        string cookie = await CreateSessionCookieAsync(factory);
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        var principal = await AuthenticateSessionAsync(factory, cookie);
        var sessions = factory.Services.GetRequiredService<LocalSessionTicketStore>();
        var ended = sessions.GetSessionEnded(principal);
        string html = await client.GetStringAsync("/");
        var token = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.None, TimeSpan.FromSeconds(1));
        Assert.True(token.Success);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value)
        });

        using var response = await client.PostAsync("/account/sign-out", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/signed-out", response.Headers.Location?.OriginalString);
        var cookies = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith($"{cookies.Cookie.Name}=;", StringComparison.Ordinal)
                && cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.Ordinal));
        Assert.True(ended.IsCancellationRequested);
        Assert.Throws<ConversationAccessException>(() => sessions.GetSessionEnded(principal));
        using var replayedPage = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, replayedPage.StatusCode);
        using var replayedNegotiation = await client.PostAsync("/_blazor/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Redirect, replayedNegotiation.StatusCode);

        client.DefaultRequestHeaders.Remove("Cookie");
        using var signedOut = await client.GetAsync("/signed-out");
        Assert.Equal(HttpStatusCode.OK, signedOut.StatusCode);
        Assert.Contains("Signed out of this app", await signedOut.Content.ReadAsStringAsync());
        using var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
    }

    [Fact]
    public async Task SignOutCannotBeTriggeredWithAGetRequest()
    {
        await using var factory = new TestWebHost();
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("Cookie", await CreateSessionCookieAsync(factory));

        using var response = await client.GetAsync("/account/sign-out");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
    }

    [Fact]
    public async Task ExpiredSessionsCannotNegotiateABlazorConnection()
    {
        await using var factory = new TestWebHost();
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("Cookie",
            await CreateSessionCookieAsync(factory, DateTimeOffset.UtcNow.AddSeconds(1)));
        await Task.Delay(TimeSpan.FromSeconds(1.2));

        using var response = await client.PostAsync("/_blazor/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("login.microsoftonline.com", response.Headers.Location?.Host);
    }

    [Fact]
    public async Task LegacySelfContainedCookiesCannotBypassTheServerSessionStore()
    {
        await using var factory = new TestWebHost();
        using var client = CreateClient(factory);
        var cookies = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "legacy-user")], CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) },
            CookieAuthenticationDefaults.AuthenticationScheme);
        client.DefaultRequestHeaders.Add("Cookie", $"{cookies.Cookie.Name}={cookies.TicketDataFormat.Protect(ticket)}");

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("login.microsoftonline.com", response.Headers.Location?.Host);
    }

    [Fact]
    public async Task ActiveBlazorConnectionClosesWhenTheSessionExpires()
    {
        await using var factory = new TestWebHost();
        using var client = CreateClient(factory);
        var sockets = factory.Server.CreateWebSocketClient();
        string cookie = await CreateSessionCookieAsync(factory, DateTimeOffset.UtcNow.AddSeconds(5));
        sockets.ConfigureRequest = request => request.Headers.Cookie = cookie;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var socket = await sockets.ConnectAsync(
            new Uri("wss://localhost:7124/_blazor"), timeout.Token);
        await socket.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"protocol\":\"blazorpack\",\"version\":1}\u001e")),
            WebSocketMessageType.Text, true, timeout.Token);
        var buffer = new byte[4096];
        var handshake = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        Assert.Equal("{}\u001e", Encoding.UTF8.GetString(buffer, 0, handshake.Count));

        WebSocketReceiveResult received;
        do
        {
            received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
        }
        while (received.MessageType != WebSocketMessageType.Close);

        Assert.Equal(WebSocketState.CloseReceived, socket.State);
    }

    [Fact]
    public async Task ProductionHostingRemainsBlocked()
    {
        await using var factory = new TestWebHost("Production");

        var error = Assert.Throws<InvalidOperationException>(() => CreateClient(factory));

        Assert.Contains("Production hosting is not enabled", error.Message);
    }

    private static HttpClient CreateClient(TestWebHost factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost:7124"),
            AllowAutoRedirect = false
        });

    private static async Task<string> CreateSessionCookieAsync(TestWebHost factory, DateTimeOffset? expires = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost", 7124);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim(ClaimConstants.Tid, "11111111-1111-4111-8111-111111111111"),
                new Claim(ClaimConstants.Oid, "33333333-3333-4333-8333-333333333333")
            ],
            CookieAuthenticationDefaults.AuthenticationScheme));
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties
            {
                IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresUtc = expires ?? DateTimeOffset.UtcNow.AddMinutes(30)
            });
        return Assert.IsType<string>(Assert.Single(context.Response.Headers.SetCookie)).Split(';')[0];
    }

    private static async Task<ClaimsPrincipal> AuthenticateSessionAsync(TestWebHost factory, string cookie)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost", 7124);
        context.Request.Headers.Cookie = cookie;
        var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.True(result.Succeeded);
        return Assert.IsType<ClaimsPrincipal>(result.Principal);
    }

    private sealed class TestWebHost(
        string environment = "Development", bool chatEnabled = false, bool foundryConfigured = false,
        string? projectEndpoint = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Entra:TenantId", "11111111-1111-4111-8111-111111111111");
            builder.UseSetting("Entra:ClientId", "22222222-2222-4222-8222-222222222222");
            builder.UseSetting("Entra:CertificateThumbprint", new string('A', 40));
            builder.UseSetting("Chat:Enabled", chatEnabled.ToString());
            builder.UseSetting("Foundry:ProjectEndpoint", foundryConfigured ? projectEndpoint ?? "https://foundry.example/api/projects/test" : "");
            builder.UseSetting("Foundry:AgentName", foundryConfigured ? "test-agent" : "");
            builder.UseSetting("Foundry:AgentVersion", foundryConfigured ? "1" : "");
            builder.ConfigureServices(services =>
                services.PostConfigure<OpenIdConnectOptions>(
                    OpenIdConnectDefaults.AuthenticationScheme,
                    options => options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(
                            new OpenIdConnectConfiguration
                            {
                                Issuer = "https://login.microsoftonline.com/11111111-1111-4111-8111-111111111111/v2.0",
                                AuthorizationEndpoint = "https://login.microsoftonline.com/11111111-1111-4111-8111-111111111111/oauth2/v2.0/authorize",
                                TokenEndpoint = "https://login.microsoftonline.com/11111111-1111-4111-8111-111111111111/oauth2/v2.0/token"
                            })));
        }
    }
}
