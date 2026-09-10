using DataDictionary.Abstractions;
using DataDictionary.Abstractions.Configuration;
using DataDictionary.Core;
using DataDictionary.Core.DependencyInjection;
using DataDictionary.Core.Sync;
using DataDictionary.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

// Registering via AddDataDictionary/WithSyncMode above only configures the
// synchronizer — it does not run it. DataDictionarySynchronizer is Scoped, so it
// must be resolved from a DI scope; this is what actually triggers the boot-time
// synchronization documented as the library's central value. A destructive
// divergence under the default OnBreakingChange.Fail policy makes
// SynchronizeAsync throw DataDictionarySyncException — left uncaught here on
// purpose, so the process fails fast at boot instead of silently starting up
// with a stale dictionary.
using (var scope = app.Services.CreateScope())
{
    var synchronizer = scope.ServiceProvider.GetRequiredService<DataDictionarySynchronizer>();
    await synchronizer.SynchronizeAsync(CancellationToken.None);
}

app.Run();
