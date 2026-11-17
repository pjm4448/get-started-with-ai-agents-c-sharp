using AiAgents.ServiceDefaults;
using AiAgents.Web.Authentication;
using AiAgents.Web.Components;

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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

await app.RunAsync();

public partial class Program;
