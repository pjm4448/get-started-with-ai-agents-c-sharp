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
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string tenantId = RequiredId(configuration, "Entra:TenantId");
        string clientId = RequiredId(configuration, "Entra:ClientId");
        string managedIdentityClientId = RequiredId(configuration, "Entra:ManagedIdentityClientId");

        services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(
                options =>
                {
                    options.Instance = "https://login.microsoftonline.com/";
                    options.TenantId = tenantId;
                    options.ClientId = clientId;
                    options.ResponseType = "code";
                    options.UsePkce = true;
                    options.SaveTokens = false;
                    options.ClientCredentials =
                    [
                        new CredentialDescription
                        {
                            SourceType = CredentialSource.SignedAssertionFromManagedIdentity,
                            ManagedIdentityClientId = managedIdentityClientId
                        }
                    ];
                },
                options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always)
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
