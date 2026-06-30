using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TwitchStressToolkit.Infrastructure.Logging;

/// <summary>
/// Writes crash/error logs to a dedicated file for post-mortem analysis.
/// Thread-safe, non-blocking (uses Channel/Queue + background writer).
/// </summary>
public sealed class ErrorLogService : IAsyncDisposable
{
    private readonly ILogger<ErrorLogService> _logger;
    private readonly string _logDirectory;
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writerTask;
    private bool _disposed;

    public ErrorLogService(ILogger<ErrorLogService> logger, string dataDirectory)
    {
        _logger = logger;
        _logDirectory = Path.Combine(dataDirectory, "logs");
        Directory.CreateDirectory(_logDirectory);

        _writerTask = Task.Run(WriteLoopAsync);
    }

    public void LogError(string category, string message, Exception? exception = null)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var threadId = Environment.CurrentManagedThreadId;

        var entry = $"[{timestamp}] [{threadId}] [{category}] {message}";

        if (exception is not null)
        {
            entry += $"{Environment.NewLine}  Exception: {exception.GetType().Name}: {exception.Message}";
            entry += $"{Environment.NewLine}  StackTrace: {exception.StackTrace}";

            if (exception.InnerException is not null)
            {
                entry += $"{Environment.NewLine}  InnerException: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}";
            }
        }

        _queue.Enqueue(entry);

        // Also write to Serilog
        _logger.LogError(exception, "[{Category}] {Message}", category, message);
    }

    private async Task WriteLoopAsync()
    {
        var filePath = Path.Combine(_logDirectory, $"errors-{DateTime.UtcNow:yyyy-MM-dd}.log");

        await using var writer = new StreamWriter(filePath, append: true, encoding: System.Text.Encoding.UTF8)
        {
            AutoFlush = false
        };

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                while (_queue.TryDequeue(out var entry))
                {
                    await writer.WriteLineAsync(entry);
                    await writer.WriteLineAsync(); // Blank line between entries
                }

                await writer.FlushAsync();
                await Task.Delay(500, _cts.Token); // Batch writes every 500ms
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                // Last resort — write to console if file logging fails
                System.Diagnostics.Debug.WriteLine($"ErrorLogService failed: {ex.Message}");
            }
        }

        // Flush remaining entries on shutdown
        while (_queue.TryDequeue(out var entry))
        {
            await writer.WriteLineAsync(entry);
            await writer.WriteLineAsync();
        }
        await writer.FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();

        try
        {
            await _writerTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch { }

        _cts.Dispose();
    }
}
