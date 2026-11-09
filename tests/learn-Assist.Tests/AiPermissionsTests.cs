using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using learn_Assist.Models;
using learn_Assist.Services;
using Xunit;

namespace learn_assist.Tests;

public class AiPermissionsTests
{
    private static string CreateTempFilePath()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "learn-assist-tests",
            Guid.NewGuid().ToString("N"));
        return Path.Combine(directory, "ai-permissions.json");
    }

    private static void DeleteTempDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private static UserDocument Document(string name, string type = "file") => new()
    {
        Name = name,
        Type = type,
        Content = "contenido de prueba",
    };

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var permissions = new AiPermissionsStore(CreateTempFilePath()).Load();

        Assert.True(permissions.AllowResources);
        Assert.True(permissions.AllowWebResources);
        Assert.True(permissions.AllowLocalFiles);
        Assert.False(permissions.AllowAgentTools);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var path = CreateTempFilePath();
        try
        {
            var store = new AiPermissionsStore(path);
            store.Save(new AiPermissions
            {
                AllowResources = true,
                AllowWebResources = false,
                AllowLocalFiles = true,
                AllowAgentTools = true,
            });

            var loaded = new AiPermissionsStore(path).Load();

            Assert.True(loaded.AllowResources);
            Assert.False(loaded.AllowWebResources);
            Assert.True(loaded.AllowLocalFiles);
            Assert.True(loaded.AllowAgentTools);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsDefaultsInsteadOfThrowing()
    {
        var path = CreateTempFilePath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ esto no es json");

            var loaded = new AiPermissionsStore(path).Load();

            Assert.True(loaded.AllowResources);
            Assert.False(loaded.AllowAgentTools);
        }
        finally
        {
            DeleteTempDirectory(path);
        }
    }

    [Fact]
    public void FilterDocuments_Default_IncludesWebAndLocalFiles()
    {
        var documents = new[] { Document("libro.pdf"), Document("articulo", "web") };

        var filtered = AiPermissionPolicy.FilterDocuments(documents, AiPermissions.Default);

        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void FilterDocuments_MasterOff_ReturnsEmpty()
    {
        var permissions = new AiPermissions { AllowResources = false };
        var documents = new[] { Document("libro.pdf"), Document("articulo", "web") };

        var filtered = AiPermissionPolicy.FilterDocuments(documents, permissions);

        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterDocuments_WebOff_ExcludesOnlyWebDocuments()
    {
        var permissions = new AiPermissions { AllowWebResources = false };
        var documents = new[] { Document("libro.pdf"), Document("articulo", "web") };

        var filtered = AiPermissionPolicy.FilterDocuments(documents, permissions);

        Assert.Single(filtered);
        Assert.Equal("libro.pdf", filtered[0].Name);
    }

    [Fact]
    public void FilterDocuments_LocalOff_ExcludesOnlyLocalDocuments()
    {
        var permissions = new AiPermissions { AllowLocalFiles = false };
        var documents = new[] { Document("libro.pdf"), Document("articulo", "web") };

        var filtered = AiPermissionPolicy.FilterDocuments(documents, permissions);

        Assert.Single(filtered);
        Assert.Equal("articulo", filtered[0].Name);
    }

    [Fact]
    public void FilterDocuments_WebType_IsCaseInsensitive()
    {
        var documents = new[] { Document("articulo", "WEB") };

        Assert.True(AiPermissionPolicy.IsWebDocument(documents[0]));
        Assert.Single(AiPermissionPolicy.FilterDocuments(documents, AiPermissions.Default));
    }

    [Fact]
    public void BuildPromptBlock_Default_BlocksAgentTools()
    {
        var block = AiPermissionPolicy.BuildPromptBlock(AiPermissions.Default);

        Assert.Contains("PERMISOS DE LA IA", block);
        Assert.Contains("Herramientas del agente (shell, edición de archivos, red): bloqueadas", block);
        Assert.Contains("No ejecutes comandos", block);
    }

    [Fact]
    public void BuildPromptBlock_WhenResourcesBlocked_ReportsBlockedSources()
    {
        var block = AiPermissionPolicy.BuildPromptBlock(new AiPermissions
        {
            AllowResources = false,
            AllowAgentTools = true,
        });

        Assert.Contains("Recursos marcados: bloqueados", block);
        Assert.Contains("Recursos web: bloqueados", block);
        Assert.Contains("Archivos locales: bloqueados", block);
        Assert.Contains("Herramientas del agente (shell, edición de archivos, red): permitidas", block);
        Assert.DoesNotContain("No ejecutes comandos", block);
    }
}
