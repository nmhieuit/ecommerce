using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IntegrationTestSupport;

/// <summary>
/// Counts the SQL statements EF Core actually sends, so an integration test can assert a query
/// path stays at a fixed statement count as the number of related rows grows — the automated form
/// of "enable EF Core query logging and confirm no N+1 pattern is present"
/// (specs/023-audit-n1-unbounded-pagination spec FR-002/SC-002; research.md Decision 6).
/// </summary>
/// <remarks>
/// Register via <c>optionsBuilder.AddInterceptors(interceptor)</c> in a test host only — this is a
/// diagnostic aid for tests, not something any service registers in its production configuration.
/// Counts reader executions (<c>SELECT</c>), which is what a related-data load issues; a test
/// asserting write-path behaviour would need a different hook.
/// </remarks>
public sealed class QueryCountInterceptor : DbCommandInterceptor
{
    private int _executedCommandCount;

    /// <summary>The number of reader-executing commands observed since the last <see cref="Reset"/>.</summary>
    public int ExecutedCommandCount => _executedCommandCount;

    /// <summary>Zeroes the count — call between the setup phase of a test and the operation under test.</summary>
    public void Reset() => Interlocked.Exchange(ref _executedCommandCount, 0);

    public override InterceptionResult<System.Data.Common.DbDataReader> ReaderExecuting(
        System.Data.Common.DbCommand command,
        CommandEventData eventData,
        InterceptionResult<System.Data.Common.DbDataReader> result)
    {
        Interlocked.Increment(ref _executedCommandCount);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command,
        CommandEventData eventData,
        InterceptionResult<System.Data.Common.DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _executedCommandCount);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
