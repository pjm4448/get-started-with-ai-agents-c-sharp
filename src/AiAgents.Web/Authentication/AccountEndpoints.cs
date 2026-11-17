using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AiAgents.Web.Authentication;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapPost("/account/sign-out", async Task<Results<SignOutHttpResult, BadRequest<string>>> (
            HttpContext context,
            IAntiforgery antiforgery,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                loggerFactory.CreateLogger("AiAgents.Web.Authentication.AccountEndpoints")
                    .LogWarning("Rejected sign-out request with an invalid antiforgery token.");
                return TypedResults.BadRequest("The sign-out request is invalid. Reload the page and try again.");
            }

            return TypedResults.SignOut(
                new AuthenticationProperties { RedirectUri = "/signed-out" },
                [CookieAuthenticationDefaults.AuthenticationScheme]);
        })
            .RequireAuthorization()
            .WithSummary("End this application's local sign-in session.")
            .ExcludeFromDescription();
    }
}
