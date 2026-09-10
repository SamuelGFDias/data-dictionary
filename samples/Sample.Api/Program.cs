using DataDictionary.Abstractions;
using DataDictionary.Abstractions.Configuration;
using DataDictionary.Core;
using DataDictionary.Core.DependencyInjection;
using DataDictionary.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sample.Api;

var builder = WebApplication.CreateBuilder(args);

// Placeholder connection string — this wiring is validated by dotnet build, not by
// running against a real database here (that is covered by the integration tests and
// T080; see quickstart.md Scenario A).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer("Server=localhost;Database=SampleApi;Trusted_Connection=True;"));

builder.Services.AddDataDictionary(b => b
    .AddManifest(Sample.Api.Generated.DataDictionaryManifest.Default)
    .WithSyncMode(SyncMode.Sync));

builder.Services.AddScoped<IDataDictionaryStore>(sp =>
    new EfDataDictionaryStore(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<DataDictionaryOptions>()));

var app = builder.Build();

app.Run();
