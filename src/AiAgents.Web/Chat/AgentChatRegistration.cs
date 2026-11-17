using AiAgents.Application;
using AiAgents.Infrastructure;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.Options;

namespace AiAgents.Web.Chat;

public static class AgentChatRegistration
{
    public static IServiceCollection AddAgentChat(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<AgentChatOptions>()
            .Bind(configuration.GetSection("Chat"))
            .Validate(options => options.OperationTimeout > TimeSpan.Zero &&
                options.OperationTimeout <= TimeSpan.FromMinutes(2), "Chat:OperationTimeout must be positive and no longer than two minutes.")
            .ValidateOnStart();

        if (!configuration.GetValue<bool>("Chat:Enabled"))
        {
            return services;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Live chat is currently supported only in Development.");
        }

        services.AddOptions<FoundryAgentOptions>()
            .Bind(configuration.GetSection("Foundry"))
            .Validate(options => options.ProjectEndpoint is
            { IsAbsoluteUri: true, Scheme: "https", UserInfo: "", Query: "", Fragment: "" } &&
                !string.IsNullOrWhiteSpace(options.AgentName) && !string.IsNullOrWhiteSpace(options.AgentVersion),
            "Enabled chat requires Foundry:ProjectEndpoint (absolute HTTPS), Foundry:AgentName and Foundry:AgentVersion.")
            .ValidateOnStart();
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            TenantId = configuration["Entra:TenantId"],
            ExcludeEnvironmentCredential = true,
            ExcludeWorkloadIdentityCredential = true,
            ExcludeManagedIdentityCredential = true,
            ExcludeInteractiveBrowserCredential = true,
            ExcludeBrokerCredential = true
        }));
        // Keep generative requests outside the shared HTTP resilience pipeline.
        services.AddKeyedSingleton<HttpClient>("FoundryAgent", (_, _) => new HttpClient(
            new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        {
            Timeout = Timeout.InfiniteTimeSpan
        });
        services.AddScoped(provider => new FoundryAgentClient(
            provider.GetRequiredService<IOptions<FoundryAgentOptions>>().Value,
            provider.GetRequiredService<TokenCredential>(),
            provider.GetRequiredKeyedService<HttpClient>("FoundryAgent")));
        services.AddScoped<IAgentClient>(provider => provider.GetRequiredService<FoundryAgentClient>());
        services.AddScoped<IConversationStore>(provider => provider.GetRequiredService<FoundryAgentClient>());
        services.AddScoped<AgentConversationService>();
        services.AddScoped<ProtectedSessionStorage>();
        services.AddScoped<IConversationBookmark, ConversationBookmark>();
        services.AddTransient<ChatWorkspace>();
        return services;
    }
}
