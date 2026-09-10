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

// The sample does not provision its own schema any other way (no EnsureCreated,
// no external migration step) — a genuinely empty database would make
// SynchronizeAsync below fail with "Invalid object name 'tb_dicionario_dados'"
// instead of exercising the sync. Applying the real EF Core migration here,
// before the synchronizer runs, is what makes Scenario A work against an empty
// database; it also seeds the initial rows via MigrationSeedStrategy's HasData
// (T071-T072), so the synchronizer's very first run sees an Unchanged dictionary.
using (var migrationScope = app.Services.CreateScope())
{
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

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
