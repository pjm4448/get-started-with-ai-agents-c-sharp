var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.AiAgents_Web>("web", launchProfileName: "https")
    .WithEndpoint("https", endpoint => endpoint.Port = 7124);

await builder.Build().RunAsync();
