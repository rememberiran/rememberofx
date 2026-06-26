using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data;

public partial class QueryLoggingInterceptor : DbCommandInterceptor
{
    private readonly ILogger<QueryLoggingInterceptor> _logger;

    public QueryLoggingInterceptor(ILogger<QueryLoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        LogCommand(eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        LogCommand(eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        LogCommand(eventData, result);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        LogCommand(eventData, result);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        LogCommand(eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        LogCommand(eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        LogCommandFailed(eventData);
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        LogCommandFailed(eventData);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void LogCommand(CommandExecutedEventData eventData, int? rowsAffected = null)
    {
        var sql = eventData.Command.CommandText;
        var operation = GetOperation(sql);
        var tables = GetTableNames(sql);
        var latencyMs = eventData.Duration.TotalMilliseconds;

        _logger.LogInformation(
            "EF {Operation} on [{Tables}] completed in {LatencyMs:F1}ms (rows affected: {RowsAffected})",
            operation,
            tables,
            latencyMs,
            rowsAffected?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-");
    }

    private void LogCommandFailed(CommandErrorEventData eventData)
    {
        var sql = eventData.Command.CommandText;
        var operation = GetOperation(sql);
        var tables = GetTableNames(sql);
        var latencyMs = eventData.Duration.TotalMilliseconds;

        _logger.LogWarning(
            eventData.Exception,
            "EF {Operation} on [{Tables}] failed after {LatencyMs:F1}ms",
            operation,
            tables,
            latencyMs);
    }

    private static string GetOperation(string sql)
    {
        var trimmed = sql.AsSpan().TrimStart();
        if (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return "SELECT";
        }

        if (trimmed.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
        {
            return "INSERT";
        }

        if (trimmed.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            return "UPDATE";
        }

        if (trimmed.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return "DELETE";
        }

        if (trimmed.StartsWith("MERGE", StringComparison.OrdinalIgnoreCase))
        {
            return "MERGE";
        }

        return "OTHER";
    }

    private static string GetTableNames(string sql)
    {
        var matches = TableNameRegex().Matches(sql);
        if (matches.Count == 0)
        {
            return "unknown";
        }

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in matches)
        {
            tables.Add(match.Groups["table"].Value);
        }

        return string.Join(", ", tables);
    }

    [GeneratedRegex(
        @"(?:FROM|JOIN|INTO|UPDATE|DELETE\s+FROM|MERGE)\s+(?:\[?\w+\]?\.)?\[?(?<table>\w+)\]?",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex TableNameRegex();
}
