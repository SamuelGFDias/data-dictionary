using DataDictionary.Abstractions.Sync;
using Microsoft.EntityFrameworkCore;

namespace DataDictionary.EntityFrameworkCore.SqlServer;

/// <summary>
/// Acquires and releases the distributed synchronization lock (FR-024) on SQL Server
/// via <c>sp_getapplock</c>/<c>sp_releaseapplock</c>, the database's native
/// advisory-lock primitive — no external dependency (research.md §6).
/// </summary>
internal static class SqlServerDictionaryLock
{
    /// <summary>
    /// The lock's logical resource name. Shared verbatim with
    /// <c>PostgreSqlDictionaryLock</c>'s <c>hashtext</c> input — it names what the lock
    /// protects, not a physical object, so both providers use the same literal.
    /// </summary>
    private const string ResourceName = "DataDictionarySync";

    /// <summary>
    /// Attempts to acquire the lock, waiting up to <paramref name="timeout"/>.
    /// </summary>
    /// <param name="dbContext">
    /// The consumer's <see cref="DbContext"/>. Its connection is opened explicitly and
    /// kept open for the lifetime of the returned handle: <c>@LockOwner = 'Session'</c>
    /// ties the lock to this ADO.NET session, and the rest of the sync pass
    /// (<c>GetCurrentAsync</c>/<c>ApplyAsync</c> per enum) runs on this same
    /// <see cref="DbContext"/> afterwards, so the connection must not be recycled while
    /// the lock is held.
    /// </param>
    /// <param name="timeout">How long to wait for the lock before giving up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    internal static async Task<LockAcquisitionResult> AcquireAsync(
        DbContext dbContext,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            var connection = dbContext.Database.GetDbConnection();

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = @timeoutMs;
                SELECT @result;
                """;

            // ADO.NET's own command timeout is unrelated to sp_getapplock's
            // @LockTimeout, but a caller-requested lock timeout longer than the 30s ADO
            // default would otherwise cut the wait short before SQL Server ever returns
            // -1. Give it a few seconds of slack over the requested wait.
            command.CommandTimeout = (int)timeout.TotalSeconds + 5;

            AddParameter(command, "@resource", ResourceName);
            AddParameter(command, "@timeoutMs", (int)timeout.TotalMilliseconds);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            var resultCode = Convert.ToInt32(result);

            // sp_getapplock: >= 0 means the lock was obtained (0 = immediately, 1 =
            // after waiting; 2/3 are grant results that don't apply to a fresh
            // 'Session'-owned request but are non-negative all the same). Any negative
            // code (-1 timeout, -2 cancelled, -3 deadlock victim, or another error) means
            // the lock was NOT obtained — FR-024 treats that as an expected, handled
            // outcome, never an exception.
            if (resultCode >= 0)
            {
                return LockAcquisitionResult.Acquired(new Handle(dbContext));
            }

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

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// The held lock. Disposing releases it via <c>sp_releaseapplock</c> and closes the
    /// connection <see cref="AcquireAsync"/> opened explicitly.
    /// </summary>
    private sealed class Handle(DbContext dbContext) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            var connection = dbContext.Database.GetDbConnection();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "EXEC sp_releaseapplock @Resource = @resource, @LockOwner = 'Session';";
                AddParameter(command, "@resource", ResourceName);

                await command.ExecuteNonQueryAsync();
            }

            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
