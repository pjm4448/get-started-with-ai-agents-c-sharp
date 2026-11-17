using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

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
    public async Task AnonymousRequestsCannotAccessTheApplication(string path)
    {
        await using var factory = new TestWebHost();
        using var client = CreateClient(factory);

        using var response = path.StartsWith("/_blazor", StringComparison.Ordinal)
            ? await client.PostAsync(path, null)
            : await client.GetAsync(path);

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
        var cookies = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "test-user")],
            CookieAuthenticationDefaults.AuthenticationScheme));
        var ticket = new AuthenticationTicket(principal, CookieAuthenticationDefaults.AuthenticationScheme);
        client.DefaultRequestHeaders.Add("Cookie",
            $"{cookies.Cookie.Name}={cookies.TicketDataFormat.Protect(ticket)}");

        using var response = await client.GetAsync("/");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("You are signed in.", html);
        Assert.Contains("Chat is unavailable until Foundry agent integration is implemented.", html);
        Assert.Matches("<textarea[^>]*disabled", html);
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

    private sealed class TestWebHost(string environment = "Development") : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Entra:TenantId", "11111111-1111-4111-8111-111111111111");
            builder.UseSetting("Entra:ClientId", "22222222-2222-4222-8222-222222222222");
            builder.UseSetting("Entra:CertificateThumbprint", new string('A', 40));
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
