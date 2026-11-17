using AiAgents.ServiceDefaults;
using AiAgents.Web.Authentication;
using AiAgents.Web.Components;
using Microsoft.AspNetCore.Http.Connections;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Production hosting is not enabled until agent integration and deployment validation are complete.");
}

builder.AddServiceDefaults();
builder.Services.AddEntraSignIn(builder.Configuration, builder.Environment);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsync("The request could not be completed. Check the server diagnostics.");
}));
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapDefaultEndpoints();
app.MapAccountEndpoints();
var components = app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();
components.Add(endpoint =>
{
    // Configure the generated hub without mapping a second Blazor endpoint.
    var connection = endpoint.Metadata.OfType<HttpConnectionDispatcherOptions>().SingleOrDefault();
    if (connection is not null)
    {
        connection.CloseOnAuthenticationExpiration = true;
    }
});

await app.RunAsync();

public partial class Program;
