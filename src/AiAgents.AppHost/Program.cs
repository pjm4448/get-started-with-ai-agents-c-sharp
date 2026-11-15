var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.AiAgents_Web>("web");

await builder.Build().RunAsync();
