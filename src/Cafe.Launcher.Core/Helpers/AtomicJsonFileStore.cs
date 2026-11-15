using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cafe.Launcher.Core.Services.Diagnostics;

namespace Cafe.Launcher.Core.Helpers;

/// <summary>
/// Persists small JSON documents through a temporary file and a single replace
/// operation so callers never expose a partially serialized document.
/// </summary>
public static class AtomicJsonFileStore
{
    public static async Task<T?> ReadAsync<T>(
        string path,
        JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            options,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task WriteAsync<T>(
        string path,
        T value,
        JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        // Restrict the empty temporary file before writing any private state.
                        File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
                    {
                        LocalDiagnostics.LogSync(LogEntrySeverity.Warn, "FileStore",
                            $"Could not restrict state file permissions: {exception.Message}");
                    }
                }

                await JsonSerializer.SerializeAsync(
                    stream,
                    value,
                    options,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
