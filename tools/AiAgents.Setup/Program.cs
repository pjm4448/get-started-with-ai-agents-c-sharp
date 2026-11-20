using System.ClientModel;
using AiAgents.Setup;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

if (args is not (["validate", _] or ["apply", _, _, "--approve-upload"] or
    ["apply-version", _, _, _, _, "--approve-upload", "--approve-agent-version"] or
    ["replace-version", _, _, _, _, "--approve-replacement", "--approve-upload", "--approve-agent-version"]))
{
    Console.Error.WriteLine("Usage: validate <manifest.json> | apply <manifest.json> <local-state.json> --approve-upload | " +
        "apply-version <expanded-manifest.json> <baseline-manifest.json> <baseline-state.json> <new-state.json> --approve-upload --approve-agent-version | " +
        "replace-version <replacement-manifest.json> <baseline-manifest.json> <baseline-state.json> <new-state.json> --approve-replacement --approve-upload --approve-agent-version");
    return 2;
}

using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(15));
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    EnvironmentName = Environments.Development,
    Args = []
});
builder.Configuration.AddUserSecrets<Program>(optional: true).AddEnvironmentVariables();
using var host = builder.Build();
var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ProductSetup");
try
{
    var products = await ProductManifest.LoadAsync(args[1], cancellation.Token);
    log.LogInformation("Validated {FileCount} product files against the approved manifest.", products.Files.Count);
    if (args[0] == "validate")
    {
        return 0;
    }

    var options = new FileSearchSetupOptions();
    builder.Configuration.GetSection("Foundry").Bind(options);
    options.TenantId = builder.Configuration["Entra:TenantId"];
    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        TenantId = options.TenantId,
        ExcludeEnvironmentCredential = true,
        ExcludeWorkloadIdentityCredential = true,
        ExcludeManagedIdentityCredential = true,
        ExcludeInteractiveBrowserCredential = true,
        ExcludeBrokerCredential = true
    });
    using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(45)
    };
    var setup = new FileSearchSetup(options, credential, http);
    string version;
    if (args[0] is "apply-version" or "replace-version")
    {
        var baselineProducts = await ProductManifest.LoadAsync(args[2], cancellation.Token);
        version = args[0] == "replace-version"
            ? await setup.ApplyReplacementAsync(products, baselineProducts, args[3], args[4], cancellation.Token)
            : await setup.ApplyVersionAsync(products, baselineProducts, args[3], args[4], cancellation.Token);
    }
    else
    {
        version = await setup.ApplyAsync(products, args[2], cancellation.Token);
    }
    log.LogInformation("Verified retrieval agent {AgentName}, version {Version}. Web configuration was not changed.",
        FileSearchSetup.AgentName, version);
    return 0;
}
catch (Exception exception)
{
    // Provider exceptions can contain request/response data. Log classifications only.
    log.LogError("Setup failed: {ExceptionType}, HTTP status {Status}. Retain local state; see docs/retrieval.md before retrying.",
        exception.GetType().Name, exception is ClientResultException sdk ? sdk.Status :
            exception is HttpRequestException http ? (int?)http.StatusCode : null);
    if (exception is InvalidDataException or TimeoutException)
    {
        log.LogError("{SetupDiagnostic}", exception.Message);
    }
    else if (exception is ArgumentException)
    {
        log.LogError("Check Foundry:ProjectEndpoint, Foundry:ModelDeployment, Foundry:IngestionTimeout and command arguments.");
    }

    return 1;
}
