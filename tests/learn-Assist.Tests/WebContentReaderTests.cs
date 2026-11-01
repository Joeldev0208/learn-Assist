using learn_Assist.Services;
using Xunit;

namespace learn_assist.Tests;

public class WebContentReaderTests
{
    [Fact]
    public void ExtractText_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, WebContentReader.ExtractText(null!));
        Assert.Equal(string.Empty, WebContentReader.ExtractText(string.Empty));
        Assert.Equal(string.Empty, WebContentReader.ExtractText("   "));
    }

    [Fact]
    public void ExtractText_PlainText_ReturnsSameText()
    {
        var result = WebContentReader.ExtractText("Hola mundo");
        Assert.Equal("Hola mundo", result);
    }

    [Fact]
    public void ExtractText_RemovesHtmlTags()
    {
        var html = "<p>Este es un <strong>párrafo</strong> de prueba.</p>";
        var result = WebContentReader.ExtractText(html);
        Assert.Equal("Este es un párrafo de prueba.", result);
    }

    [Fact]
    public void ExtractText_RemovesScriptAndStyle()
    {
        var html = "<html><head><style>body{color:red;}</style></head><body><script>alert('x');</script>Contenido real</body></html>";
        var result = WebContentReader.ExtractText(html);
        Assert.DoesNotContain("style", result);
        Assert.DoesNotContain("script", result);
        Assert.DoesNotContain("alert", result);
        Assert.Contains("Contenido real", result);
    }

    [Fact]
    public void ExtractText_DecodesHtmlEntities()
    {
        var html = "A &amp; B &lt; C &gt; D &quot;E&quot; &nbsp; F";
        var result = WebContentReader.ExtractText(html);
        // &nbsp; se convierte en espacio y los espacios múltiples se colapsan a uno
        Assert.Contains("A & B < C > D \"E\" F", result);
    }

    [Fact]
    public void ExtractText_CollapsesWhitespace()
    {
        var html = "Línea 1\n\n\n   Línea 2\t\tLínea 3";
        var result = WebContentReader.ExtractText(html);
        Assert.Equal("Línea 1 Línea 2 Línea 3", result);
    }

    [Fact]
    public void ExtractText_MixedContent_ReturnsCleanText()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <head><title>Mi página</title></head>
            <body>
                <h1>Título del artículo</h1>
                <p>Este es el <strong>contenido principal</strong> de la página.</p>
                <script>console.log('hola');</script>
                <div class="sidebar">Enlace: <a href="/contacto">Contacto</a></div>
            </body>
            </html>
            """;
        var result = WebContentReader.ExtractText(html);
        Assert.Contains("Título del artículo", result);
        Assert.Contains("contenido principal", result);
        Assert.Contains("Enlace:", result);
        Assert.Contains("Contacto", result);
        Assert.DoesNotContain("<html>", result);
        Assert.DoesNotContain("console.log", result);
    }
}
