using System;
using System.IO;
using Xunit;
using learn_Assist.Services;

namespace learn_assist.Tests;

public class FileContentReaderTests : IDisposable
{
    private readonly string _tempDir;

    public FileContentReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void ExtractText_NullOrEmptyPath_ReturnsNull()
    {
        Assert.Null(FileContentReader.ExtractText(null!));
        Assert.Null(FileContentReader.ExtractText(string.Empty));
        Assert.Null(FileContentReader.ExtractText("   "));
    }

    [Fact]
    public void ExtractText_FileNotExists_ReturnsNull()
    {
        var missing = Path.Combine(_tempDir, "no-existe.txt");
        Assert.Null(FileContentReader.ExtractText(missing));
    }

    [Fact]
    public void ExtractText_PlainTextFile_ReturnsContent()
    {
        var file = Path.Combine(_tempDir, "sample.txt");
        const string content = "Hola mundo\nEsta es una línea de prueba.";
        File.WriteAllText(file, content);

        var result = FileContentReader.ExtractText(file);

        Assert.Equal(content, result);
    }

    [Fact]
    public void ExtractText_EmptyFile_ReturnsNull()
    {
        var file = Path.Combine(_tempDir, "vacío.txt");
        File.WriteAllText(file, string.Empty);

        var result = FileContentReader.ExtractText(file);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractText_UnsupportedExtension_ReturnsNull()
    {
        var file = Path.Combine(_tempDir, "datos.xyz");
        File.WriteAllText(file, "contenido");

        var result = FileContentReader.ExtractText(file);

        Assert.Null(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
