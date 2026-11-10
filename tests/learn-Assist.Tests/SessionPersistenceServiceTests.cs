using System;
using System.IO;
using System.Threading.Tasks;
using learn_Assist.Models;
using learn_Assist.Services;
using Xunit;

namespace learn_assist.Tests;

public sealed class SessionPersistenceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "learn-assist-session-tests", Guid.NewGuid().ToString("N"));

    public SessionPersistenceServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ForUser_UsesDifferentDirectoryPerUser()
    {
        var first = SessionPersistenceService.ForUser(_root, "user-one");
        var second = SessionPersistenceService.ForUser(_root, "user-two");

        Assert.NotEqual(first.SessionsDirectory, second.SessionsDirectory);
        Assert.EndsWith(Path.Combine(_root, "user-one"), first.SessionsDirectory);
        Assert.EndsWith(Path.Combine(_root, "user-two"), second.SessionsDirectory);
    }

    [Fact]
    public async Task SaveSessionAsync_AssignsStorageFileName()
    {
        var persistence = new SessionPersistenceService(Path.Combine(_root, "user"));
        var session = new ChatSession
        {
            Title = "Repaso",
        };
        session.Messages.Add(new ChatMessage
        {
            Role = MessageRole.User,
            Content = "Hola",
        });

        await persistence.SaveSessionAsync(session);

        Assert.False(string.IsNullOrWhiteSpace(session.StorageFileName));
        Assert.True(File.Exists(Path.Combine(persistence.SessionsDirectory!, session.StorageFileName!)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
