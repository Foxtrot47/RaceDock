// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

public enum LogLevel { Info, Warning, Error, Fatal }

/// <summary>
/// Writes one NDJSON record per event so a session can be read back by machine, not just by eye.
/// </summary>
/// <remarks>
/// Ambient like <see cref="FeedDump"/>, because the feed sources that fail are far from the code that
/// owns a sink. A write that fails drops the handle instead of disabling the log, so a transient lock
/// costs one record rather than the rest of the session.
/// </remarks>
public static class AppLog
{
    private const long MaximumBytes = 5L * 1024 * 1024;
    private const int KeptFiles = 3;
    private const string FileName = "app.ndjson";

    private static readonly object Gate = new();
    private static string _folder = AppPaths.Logs;
    private static StreamWriter? _writer;
    private static long _written;

    public static string Folder => _folder;

    /// <summary>Redirects the log. For tests, and for any host that resolves its own root.</summary>
    public static void UseFolder(string folder)
    {
        lock (Gate)
        {
            CloseCore();
            _folder = folder;
        }
    }

    /// <summary>Closes the current file so it can be read or moved. The next write reopens it.</summary>
    public static void Close()
    {
        lock (Gate) CloseCore();
    }

    public static void Info(string source, string message, object? data = null) =>
        Write(LogLevel.Info, source, message, null, data);

    public static void Warning(string source, string message, Exception? exception = null, object? data = null) =>
        Write(LogLevel.Warning, source, message, exception, data);

    public static void Error(string source, string message, Exception? exception = null, object? data = null) =>
        Write(LogLevel.Error, source, message, exception, data);

    public static void Fatal(string source, string message, Exception? exception = null, object? data = null) =>
        Write(LogLevel.Fatal, source, message, exception, data);

    private static void Write(LogLevel level, string source, string message, Exception? exception, object? data)
    {
        lock (Gate)
        {
            try
            {
                _writer ??= Open();
                var line = JsonSerializer.Serialize(
                    new AppLogEntry(DateTimeOffset.UtcNow, level.ToString(), source, message, exception?.ToString(), Describe(data)));
                _writer.WriteLine(line);
                _written += line.Length + Environment.NewLine.Length;
                if (_written >= MaximumBytes) Roll();
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                CloseCore();
            }
        }
    }

    // A caller's diagnostic object must never be able to end the record it was meant to explain.
    private static JsonNode? Describe(object? data)
    {
        if (data is null) return null;
        try { return JsonSerializer.SerializeToNode(data); }
        catch (Exception failure) when (failure is NotSupportedException or JsonException)
        {
            return JsonValue.Create(data.ToString());
        }
    }

    private static StreamWriter Open()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, FileName);
        _written = File.Exists(path) ? new FileInfo(path).Length : 0;
        // Shared for reading so the log can be tailed while RaceDock is running.
        return new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
    }

    private static void Roll()
    {
        CloseCore();
        File.Delete(Path.Combine(_folder, $"{FileName}.{KeptFiles - 1}"));
        for (var index = KeptFiles - 2; index >= 1; index--)
        {
            var from = Path.Combine(_folder, $"{FileName}.{index}");
            if (File.Exists(from)) File.Move(from, Path.Combine(_folder, $"{FileName}.{index + 1}"), true);
        }
        var current = Path.Combine(_folder, FileName);
        if (File.Exists(current)) File.Move(current, Path.Combine(_folder, $"{FileName}.1"), true);
    }

    private static void CloseCore()
    {
        _writer?.Dispose();
        _writer = null;
        _written = 0;
    }
}

/// <summary>One logged event. Serialized as a single NDJSON line.</summary>
public readonly record struct AppLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Source,
    string Message,
    string? Exception,
    JsonNode? Data);
