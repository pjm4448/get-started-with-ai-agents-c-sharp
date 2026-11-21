using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using AiAgents.Setup;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

if (args is not (["validate", _] or ["validate-search", _, _] or ["preview-search-indexer", _, _] or ["preview-search-datasource", _, _, _, _] or ["preview-search-index" or "preview-search-skillset", _, _, _, _, _] or ["apply", _, _, "--approve-upload"] or
    ["apply-version", _, _, _, _, "--approve-upload", "--approve-agent-version"] or
    ["replace-version", _, _, _, _, "--approve-replacement", "--approve-upload", "--approve-agent-version"]))
{
    Console.Error.WriteLine("Usage: validate <manifest.json> | validate-search <markdown-manifest.json> <pdf-manifest.json> | " +
        "preview-search-index <index-name> <embedding-resource-uri> <deployment-name> <model-name> <dimensions> | " +
        "preview-search-skillset <index-name> <embedding-resource-uri> <deployment-name> <model-name> <dimensions> | " +
        "preview-search-indexer <index-name> <markdown|pdf> | " +
        "preview-search-datasource <index-name> <markdown|pdf> <storage-resource-id> <container> | " +
        "apply <manifest.json> <local-state.json> --approve-upload | " +
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
    if (args[0] == "preview-search-datasource")
    {
        var source = SearchIndexDefinition.CreateDataSource(args[1], args[2], args[3], args[4]);
        Console.WriteLine(ModelReaderWriter.Write(source, new ModelReaderWriterOptions("W")).ToString());
        return 0;
    }

    if (args[0] == "preview-search-indexer")
    {
        var indexer = SearchIndexDefinition.CreateIndexer(args[1], args[2]);
        Console.WriteLine(ModelReaderWriter.Write(indexer, new ModelReaderWriterOptions("W")).ToString());
        return 0;
    }

    if (args[0] is "preview-search-index" or "preview-search-skillset")
    {
        if (!Uri.TryCreate(args[2], UriKind.Absolute, out var modelEndpoint) ||
            !int.TryParse(args[5], NumberStyles.None, CultureInfo.InvariantCulture, out int dimensions))
        {
            throw new ArgumentException("The embedding resource URI and dimensions are invalid.");
        }

        var wireOptions = new ModelReaderWriterOptions("W");
        var definition = args[0] == "preview-search-index"
            ? ModelReaderWriter.Write(SearchIndexDefinition.Create(args[1], modelEndpoint, args[3], args[4], dimensions), wireOptions)
            : ModelReaderWriter.Write(SearchIndexDefinition.CreateSkillset(args[1], modelEndpoint, args[3], args[4], dimensions), wireOptions);
        Console.WriteLine(definition.ToString());
        return 0;
    }

    var products = args[0] == "validate-search"
        ? await SearchCorpus.LoadAsync(args[1], args[2], cancellation.Token)
        : await ProductManifest.LoadAsync(args[1], cancellation.Token);
    log.LogInformation("Validated {FileCount} product files against the approved manifest.", products.Files.Count);
    if (args[0] is "validate" or "validate-search")
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
        if (args[0].StartsWith("preview-search-", StringComparison.Ordinal))
        {
            log.LogError("Check the Search name and file type, Azure resource URI or ID, container name, model deployment and default embedding dimensions.");
        }
        else
        {
            log.LogError("Check Foundry:ProjectEndpoint, Foundry:ModelDeployment, Foundry:IngestionTimeout and command arguments.");
        }
    }

    return 1;
}
