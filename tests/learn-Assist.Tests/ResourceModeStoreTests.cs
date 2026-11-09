using System;
using System.IO;
using learn_Assist.Services;
using Xunit;

namespace learn_assist.Tests;

public class ResourceModeStoreTests
{
    private static string CreateTempFilePath()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "learn-assist-tests",
            Guid.NewGuid().ToString("N"));
        return Path.Combine(directory, "resource-mode.json");
    }

    [Fact]
    public void Load_MissingFile_ReturnsFalse()
    {
        var store = new ResourceModeStore(CreateTempFilePath());

        Assert.False(store.Load());
    }

    [Fact]
    public void SaveThenLoad_True_RoundTrips()
    {
        var path = CreateTempFilePath();
        try
        {
            var store = new ResourceModeStore(path);
            store.Save(true);

            Assert.True(new ResourceModeStore(path).Load());
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoad_False_RoundTrips()
    {
        var path = CreateTempFilePath();
        try
        {
            var store = new ResourceModeStore(path);
            store.Save(false);

            Assert.False(new ResourceModeStore(path).Load());
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsFalseInsteadOfThrowing()
    {
        var path = CreateTempFilePath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ esto no es json");

            Assert.False(new ResourceModeStore(path).Load());
        }
        finally
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
