using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartWattWattFunc.Models;
using SmartWattWattFunc.Configuration;
using SmartWattWattFunc.Integrations.Http;
using SmartWattWattFunc.Integrations.FoxEss;
using SmartWattWattFunc.Integrations.Octopus;
using SmartWattWattFunc.Policies;
using SmartWattWattFunc.Services;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

builder.Services.AddTransient<HttpTrafficLoggingHandler>();
builder.Services.AddHttpClient<IOctopusGraphQlClient, OctopusGraphQlClient>()
    .AddHttpMessageHandler<HttpTrafficLoggingHandler>();
builder.Services.AddHttpClient<IFoxEssClient, FoxEssClient>()
    .AddHttpMessageHandler<HttpTrafficLoggingHandler>();

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new ScheduleOptions
    {
        TimeZoneId = configuration["FoxEss:TimeZone"] ?? "Europe/London",
        DefaultSlot1Start = TimeOfDay.Parse(configuration["FoxEss:DefaultSlot1Start"] ?? "23:30"),
        DefaultSlot1End = TimeOfDay.Parse(configuration["FoxEss:DefaultSlot1End"] ?? "23:59"),
        DefaultSlot2Start = TimeOfDay.Parse(configuration["FoxEss:DefaultSlot2Start"] ?? "00:00"),
        DefaultSlot2End = TimeOfDay.Parse(configuration["FoxEss:DefaultSlot2End"] ?? "05:30"),
        SyncEnabled = bool.Parse(configuration["Sync:Enabled"] ?? "true"),
        TestMode = bool.Parse(configuration["Sync:TestMode"] ?? "false")
    };
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITestModeLogger, TestModeLogger>();
builder.Services.AddSingleton<IForceChargeScheduleBuilder, ForceChargeScheduleBuilder>();
builder.Services.AddSingleton<IEvChargeSyncService, EvChargeSyncService>();

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new OctopusOptions
    {
        GraphQlEndpoint = configuration["Octopus:GraphQlEndpoint"] ?? "https://api.octopus.energy/v1/graphql/",
        AccountNumber = configuration["Octopus:AccountNumber"] ?? string.Empty,
        ApiKey = ResolveSecret(configuration, "OctopusApiKey") ?? configuration["Octopus:ApiKey"] ?? string.Empty
    };
});

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new FoxEssOptions
    {
        BaseUrl = configuration["FoxEss:BaseUrl"] ?? "https://www.foxesscloud.com",
        DeviceSerialNumber = configuration["FoxEss:DeviceSerialNumber"] ?? string.Empty,
        ApiToken = ResolveSecret(configuration, "FoxEssApiToken") ?? configuration["FoxEss:ApiToken"] ?? string.Empty,
        TimeZoneId = configuration["FoxEss:TimeZone"] ?? "Europe/London",
        UserAgent = configuration["FoxEss:UserAgent"] ?? string.Empty
    };
});

builder.Build().Run();

static string? ResolveSecret(IConfiguration configuration, string secretName)
{
    var keyVaultUri = configuration["KeyVaultUri"];
    if (string.IsNullOrWhiteSpace(keyVaultUri))
    {
        return configuration[$"Secrets:{secretName}"];
    }

    var client = new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential());
    try
    {
        return client.GetSecret(secretName).Value.Value;
    }
    catch
    {
        return configuration[$"Secrets:{secretName}"];
    }
}
