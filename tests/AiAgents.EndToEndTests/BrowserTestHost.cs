using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AiAgents.Application;
using Azure.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace AiAgents.EndToEndTests;

internal sealed class BrowserTestHost : WebApplicationFactory<Program>
{
    private readonly X509Certificate2 _certificate = CreateCertificate();

    public BrowserAgent Backend { get; } = new();

    public Uri Address => new(Services.GetRequiredService<IServer>().Features
        .Get<IServerAddressesFeature>()?.Addresses.Single()
        ?? throw new InvalidOperationException("The browser test server did not start."));

    public BrowserTestHost()
    {
        UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(_certificate)));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Entra:TenantId", "11111111-1111-4111-8111-111111111111");
        builder.UseSetting("Entra:ClientId", "22222222-2222-4222-8222-222222222222");
        builder.UseSetting("Entra:CertificateThumbprint", new string('A', 40));
        builder.UseSetting("Chat:Enabled", "true");
        builder.UseSetting("Foundry:ProjectEndpoint", "https://foundry.example/api/projects/test");
        builder.UseSetting("Foundry:AgentName", "offline-browser-agent");
        builder.UseSetting("Foundry:AgentVersion", "1");
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAgentClient>();
            services.RemoveAll<IConversationStore>();
            services.AddSingleton<IAgentClient>(Backend);
            services.AddSingleton<IConversationStore>(Backend);
            services.RemoveAll<TokenCredential>();
            services.AddSingleton<TokenCredential, OfflineCredential>();
            services.RemoveAll<IDataProtectionProvider>();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme,
                options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                    new OpenIdConnectConfiguration
                    {
                        Issuer = "https://login.microsoftonline.com/11111111-1111-4111-8111-111111111111/v2.0",
                        AuthorizationEndpoint = "https://login.microsoftonline.com/11111111-1111-4111-8111-111111111111/oauth2/v2.0/authorize",
                        TokenEndpoint = "https://login.microsoftonline.com/11111111-1111-4111-8111-111111111111/oauth2/v2.0/token"
                    }));
        });
    }

    public async Task<Microsoft.Playwright.Cookie> SignInAsync(Guid objectId)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Scheme = Address.Scheme;
        context.Request.Host = new HostString(Address.Host, Address.Port);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "browser-test-user"),
                new Claim(ClaimConstants.Tid, "11111111-1111-4111-8111-111111111111"),
                new Claim(ClaimConstants.Oid, objectId.ToString())
            ], CookieAuthenticationDefaults.AuthenticationScheme));
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties
            {
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10)
            });
        string header = Assert.IsType<string>(Assert.Single(context.Response.Headers.SetCookie)).Split(';')[0];
        int separator = header.IndexOf('=', StringComparison.Ordinal);
        return new Microsoft.Playwright.Cookie
        {
            Name = header[..separator],
            Value = header[(separator + 1)..],
            Url = Address.AbsoluteUri,
            Secure = true,
            HttpOnly = true,
            SameSite = Microsoft.Playwright.SameSiteAttribute.Lax
        };
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            _certificate.Dispose();
        }
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        // Windows TLS cannot use the ephemeral key returned by CreateSelfSigned.
        return X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null);
    }

    private sealed class OfflineCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Azure token acquisition is forbidden in offline browser tests.");

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Azure token acquisition is forbidden in offline browser tests.");
    }
}
