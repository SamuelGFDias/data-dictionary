using DataDictionary.Abstractions.Sync;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DataDictionary.EntityFrameworkCore.PostgreSql;

/// <summary>
/// Acquires and releases the distributed synchronization lock (FR-024) on PostgreSQL
/// via <c>pg_advisory_lock</c>/<c>pg_advisory_unlock</c>, the database's native
/// advisory-lock primitive — no external dependency (research.md §6).
/// </summary>
internal static class PostgreSqlDictionaryLock
{
    /// <summary>
    /// The lock's logical resource name, hashed into the bigint key
    /// <c>pg_advisory_lock</c> requires. Shared verbatim with
    /// <c>SqlServerDictionaryLock</c>'s <c>@Resource</c> — it names what the lock
    /// protects, not a physical object, so both providers use the same literal.
    /// </summary>
    private const string ResourceName = "DataDictionarySync";

    /// <summary>
    /// Attempts to acquire the lock, waiting up to <paramref name="timeout"/>.
    /// </summary>
    /// <param name="dbContext">
    /// The consumer's <see cref="DbContext"/>. Its connection is opened explicitly and
    /// kept open for the lifetime of the returned handle, the same way and for the same
    /// reason as <c>SqlServerDictionaryLock</c>: the rest of the sync pass
    /// (<c>GetCurrentAsync</c>/<c>ApplyAsync</c> per enum) runs on this same
    /// <see cref="DbContext"/> afterwards.
    /// </param>
    /// <param name="timeout">How long to wait for the lock before giving up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    internal static async Task<LockAcquisitionResult> AcquireAsync(
        DbContext dbContext,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        var connection = dbContext.Database.GetDbConnection();

        try
        {
            // pg_advisory_lock blocks indefinitely — PostgreSQL gives it no timeout
            // argument — so the wait is bounded via the session's statement_timeout GUC
            // instead: it cancels the blocking SELECT below if the lock isn't granted in
            // time. SET does not accept bind parameters in PostgreSQL's grammar (the
            // value must be a literal), so the already-validated integer is interpolated
            // directly rather than parameterized; it never comes from external input.
            await using (var setTimeoutCommand = connection.CreateCommand())
            {
                setTimeoutCommand.CommandText =
                    $"SET statement_timeout = {(int)timeout.TotalMilliseconds};";
                await setTimeoutCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var lockCommand = connection.CreateCommand())
            {
                lockCommand.CommandText = "SELECT pg_advisory_lock(hashtext(@resource)::bigint);";

                // ADO.NET's own command timeout is unrelated to statement_timeout, but a
                // caller-requested lock timeout longer than the 30s ADO default would
                // otherwise cut the wait short before Postgres ever cancels the query.
                // Give it a few seconds of slack over the requested wait.
                lockCommand.CommandTimeout = (int)timeout.TotalSeconds + 5;

                var resourceParameter = lockCommand.CreateParameter();
                resourceParameter.ParameterName = "@resource";
                resourceParameter.Value = ResourceName;
                lockCommand.Parameters.Add(resourceParameter);

                // The return value (always true once granted) is irrelevant — what
                // matters is whether this throws.
                await lockCommand.ExecuteScalarAsync(cancellationToken);
            }

            await using (var resetTimeoutCommand = connection.CreateCommand())
            {
                resetTimeoutCommand.CommandText = "SET statement_timeout = 0;";
                await resetTimeoutCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            return LockAcquisitionResult.Acquired(new Handle(dbContext));
        }
        catch (PostgresException ex) when (ex.SqlState == "57014")
        {
            // query_canceled: statement_timeout fired before pg_advisory_lock was
            // granted, i.e. the lock is held elsewhere. FR-024 treats this as an
            // expected, handled outcome, never an exception.
            await dbContext.Database.CloseConnectionAsync();
            return LockAcquisitionResult.NotAcquired;
        }
        catch
        {
            // A genuine infrastructure failure (e.g. the connection drops mid-call) is
            // not the "lock is held elsewhere" case — let it propagate, but close the
            // connection we opened above so it isn't leaked.
            await dbContext.Database.CloseConnectionAsync();
            throw;
        }
    }

    /// <summary>
    /// The held lock. Disposing releases it via <c>pg_advisory_unlock</c> and closes the
    /// connection <see cref="AcquireAsync"/> opened explicitly.
    /// </summary>
    private sealed class Handle(DbContext dbContext) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            var connection = dbContext.Database.GetDbConnection();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT pg_advisory_unlock(hashtext(@resource)::bigint);";

                var resourceParameter = command.CreateParameter();
                resourceParameter.ParameterName = "@resource";
                resourceParameter.Value = ResourceName;
                command.Parameters.Add(resourceParameter);

                await command.ExecuteScalarAsync();
            }

            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
