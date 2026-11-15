using System.Text.Json;
using Cafe.Launcher.UI.Helpers;
using Cafe.Launcher.Testing;
using Cafe.Launcher.Core.Helpers;

namespace Cafe.Launcher.Tests;

public sealed class AtomicJsonFileStoreTests
{
    [Fact]
    public async Task WriteAsync_ThenReadAsync_RoundTripsAndLeavesNoTemporaryFiles()
    {
        var directory = TestDirectory.Create();
        var path = Path.Combine(directory, "state.json");
        var value = new TestState("ready", 3);

        try
        {
            await AtomicJsonFileStore.WriteAsync(path, value, JsonDefaults.Strict);

            var actual = await AtomicJsonFileStore.ReadAsync<TestState>(
                path,
                JsonDefaults.Strict);

            Assert.Equal(value, actual);
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            directory.Dispose();
        }
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task WriteAsync_OnUnix_RestrictsNewAndReplacedStateFiles()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix permissions only.");
        using var directory = TestDirectory.Create();
        var path = Path.Combine(directory, "settings.json");
        var privateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        await AtomicJsonFileStore.WriteAsync(path, new TestState("new", 1), JsonDefaults.Strict);
        Assert.Equal(privateMode, File.GetUnixFileMode(path));
        File.SetUnixFileMode(path, privateMode | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        await AtomicJsonFileStore.WriteAsync(path, new TestState("replaced", 2), JsonDefaults.Strict);

        Assert.Equal(privateMode, File.GetUnixFileMode(path));
        Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
    }

    private sealed record TestState(string Status, int Attempt);
}
