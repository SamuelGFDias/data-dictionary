using DataDictionary.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Sample.Api;

/// <summary>
/// Minimal sample <see cref="DbContext"/> demonstrating the wiring required by
/// <c>quickstart.md</c> Scenario A: <see cref="ModelBuilderExtensions.ApplyDataDictionary"/>
/// maps the dictionary's own tables (<c>tb_dicionario_dados</c> and, optionally,
/// <c>tb_dicionario_enum</c>) into this context's model.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyDataDictionary();
    }
}
