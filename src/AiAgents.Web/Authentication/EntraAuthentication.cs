using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Web;

namespace AiAgents.Web.Authentication;

public static class EntraAuthentication
{
    public static IServiceCollection AddEntraSignIn(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        string tenantId = RequiredId(configuration, "Entra:TenantId");
        string clientId = RequiredId(configuration, "Entra:ClientId");
        CredentialDescription credential;
        if (environment.IsDevelopment())
        {
            string? thumbprint = configuration["Entra:CertificateThumbprint"];
            if (thumbprint is null || thumbprint.Length != 40 || !thumbprint.All(Uri.IsHexDigit))
            {
                throw new InvalidOperationException(
                    "Entra:CertificateThumbprint must be a 40-character hexadecimal certificate thumbprint in Development.");
            }

            credential = new CredentialDescription
            {
                SourceType = CredentialSource.StoreWithThumbprint,
                CertificateStorePath = "CurrentUser/My",
                CertificateThumbprint = thumbprint
            };
        }
        else
        {
            if (!string.IsNullOrEmpty(configuration["Entra:CertificateThumbprint"]))
            {
                throw new InvalidOperationException(
                    "Entra:CertificateThumbprint is permitted only in Development. Use managed identity in other environments.");
            }

            credential = new CredentialDescription
            {
                SourceType = CredentialSource.SignedAssertionFromManagedIdentity,
                ManagedIdentityClientId = RequiredId(configuration, "Entra:ManagedIdentityClientId")
            };
        }

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = OpenIdConnectDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddMicrosoftIdentityWebApp(
                options =>
                {
                    options.Instance = "https://login.microsoftonline.com/";
                    options.TenantId = tenantId;
                    options.ClientId = clientId;
                    options.ResponseType = "code";
                    options.UsePkce = true;
                    options.UseTokenLifetime = true;
                    options.SaveTokens = false;
                    options.ClientCredentials = [credential];
                },
                options =>
                {
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.SlidingExpiration = false;
                })
            .EnableTokenAcquisitionToCallDownstreamApi()
            .AddInMemoryTokenCaches();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }

    private static string RequiredId(IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        if (!Guid.TryParse(value, out Guid id) || id == Guid.Empty)
        {
            throw new InvalidOperationException($"{key} must be a non-empty GUID.");
        }

        return id.ToString();
    }
}
