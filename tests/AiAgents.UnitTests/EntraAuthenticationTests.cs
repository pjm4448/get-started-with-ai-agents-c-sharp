using AiAgents.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;

namespace AiAgents.UnitTests;

public sealed class EntraAuthenticationTests
{
    private const string TenantId = "11111111-1111-4111-8111-111111111111";
    private const string ClientId = "22222222-2222-4222-8222-222222222222";
    private const string IdentityId = "33333333-3333-4333-8333-333333333333";

    [Theory]
    [InlineData("Entra:TenantId", null)]
    [InlineData("Entra:ClientId", "not-a-guid")]
    [InlineData("Entra:ManagedIdentityClientId", "00000000-0000-0000-0000-000000000000")]
    public void RejectsInvalidConfiguration(string key, string? value)
    {
        var settings = ValidSettings();
        settings[key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var error = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddEntraSignIn(configuration, new TestEnvironment()));

        Assert.Contains(key, error.Message);
    }

    [Fact]
    public void RegistersTenantScopedKeylessSignInAndAuthorization()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(ValidSettings()).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddEntraSignIn(configuration, new TestEnvironment());
        using var provider = services.BuildServiceProvider();

        var identity = provider.GetRequiredService<IOptionsMonitor<MicrosoftIdentityOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        Assert.NotNull(identity.ClientCredentials);
        var credential = Assert.Single(identity.ClientCredentials);
        Assert.Equal(TenantId, identity.TenantId);
        Assert.Equal(ClientId, identity.ClientId);
        Assert.Equal(CredentialSource.SignedAssertionFromManagedIdentity, credential.SourceType);
        Assert.Equal(IdentityId, credential.ManagedIdentityClientId);
        Assert.Null(identity.ClientSecret);
        Assert.Equal("code", identity.ResponseType);
        Assert.True(identity.UsePkce);
        Assert.False(identity.SaveTokens);

        var oidc = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        Assert.Equal(ClientId, oidc.ClientId);
        Assert.Equal("code", oidc.ResponseType);
        Assert.True(oidc.UsePkce);
        Assert.False(oidc.SaveTokens);

        var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);

        var auth = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, auth.DefaultScheme);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, auth.DefaultAuthenticateScheme);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, auth.DefaultSignInScheme);
        Assert.Equal(OpenIdConnectDefaults.AuthenticationScheme, auth.DefaultChallengeScheme);
        var policy = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy;
        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Entra:TenantId"] = TenantId,
        ["Entra:ClientId"] = ClientId,
        ["Entra:ManagedIdentityClientId"] = IdentityId
    };

    [Fact]
    public void DevelopmentUsesOnlyTheConfiguredCertificate()
    {
        var settings = ValidSettings();
        settings.Remove("Entra:ManagedIdentityClientId");
        settings["Entra:CertificateThumbprint"] = new string('A', 40);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEntraSignIn(configuration, new TestEnvironment { EnvironmentName = Environments.Development });
        using var provider = services.BuildServiceProvider();

        var identity = provider.GetRequiredService<IOptionsMonitor<MicrosoftIdentityOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        Assert.NotNull(identity.ClientCredentials);
        var credential = Assert.Single(identity.ClientCredentials);
        Assert.Equal(CredentialSource.StoreWithThumbprint, credential.SourceType);
        Assert.Equal("CurrentUser/My", credential.CertificateStorePath);
        Assert.Equal(new string('A', 40), credential.CertificateThumbprint);
        Assert.Null(credential.ManagedIdentityClientId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public void DevelopmentRejectsMissingOrInvalidCertificate(string? thumbprint)
    {
        var settings = ValidSettings();
        settings["Entra:CertificateThumbprint"] = thumbprint;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddEntraSignIn(
                configuration, new TestEnvironment { EnvironmentName = Environments.Development }));

        Assert.Contains("Entra:CertificateThumbprint", error.Message);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void NonDevelopmentRejectsCertificateConfiguration(string environmentName)
    {
        var settings = ValidSettings();
        settings["Entra:CertificateThumbprint"] = new string('A', 40);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddEntraSignIn(
                configuration, new TestEnvironment { EnvironmentName = environmentName }));

        Assert.Contains("only in Development", error.Message);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
