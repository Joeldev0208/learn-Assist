using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using learn_Assist.Services;
using Xunit;

namespace learn_assist.Tests;

public class WebContentReaderFetchTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Handler { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Handler is not null)
                return Handler(request, cancellationToken);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>Hola mundo</body></html>", Encoding.UTF8, "text/html")
            });
        }
    }

    [Fact]
    public async Task FetchAsync_ValidHtmlUrl_ReturnsExtractedText()
    {
        var handler = new MockHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body><p>Hola mundo</p></body></html>", Encoding.UTF8, "text/html")
            })
        };
        using var client = new HttpClient(handler);
        var reader = new WebContentReader(client);

        var result = await reader.FetchAsync("https://ejemplo.com/pagina");

        Assert.True(result.Success);
        Assert.Contains("Hola mundo", result.Text);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task FetchAsync_EmptyUrl_ReturnsFail()
    {
        var reader = new WebContentReader();
        var result = await reader.FetchAsync("   ");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Null(result.Text);
    }

    [Fact]
    public async Task FetchAsync_InvalidScheme_ReturnsFail()
    {
        var reader = new WebContentReader();
        var result = await reader.FetchAsync("ftp://archivos.com/documento.txt");
        Assert.False(result.Success);
        Assert.Contains("http", result.Error!);
    }

    [Fact]
    public async Task FetchAsync_LocalhostBlocked_ReturnsFail()
    {
        var reader = new WebContentReader();
        var result = await reader.FetchAsync("http://localhost:5000/api/datos");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task FetchAsync_PrivateIpBlocked_ReturnsFail()
    {
        var reader = new WebContentReader();
        var result = await reader.FetchAsync("http://192.168.1.1/config");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task FetchAsync_NotFound_ReturnsFailWithStatusCode()
    {
        var handler = new MockHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))
        };
        using var client = new HttpClient(handler);
        var reader = new WebContentReader(client);

        var result = await reader.FetchAsync("https://ejemplo.com/no-existe");
        Assert.False(result.Success);
        Assert.Contains("404", result.Error!);
    }

    [Fact]
    public async Task FetchAsync_ImageContentType_ReturnsFail()
    {
        var handler = new MockHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("fake-image-bytes", Encoding.UTF8, "image/png")
            })
        };
        using var client = new HttpClient(handler);
        var reader = new WebContentReader(client);

        var result = await reader.FetchAsync("https://ejemplo.com/img.png");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task FetchAsync_JsonContentType_IsAccepted()
    {
        var handler = new MockHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"clave\":\"valor\"}", Encoding.UTF8, "application/json")
            })
        };
        using var client = new HttpClient(handler);
        var reader = new WebContentReader(client);

        var result = await reader.FetchAsync("https://ejemplo.com/api/datos");
        Assert.True(result.Success);
        Assert.Contains("valor", result.Text!);
    }

    [Fact]
    public async Task FetchTextAsync_BackwardCompatibility_ReturnsTextOrNull()
    {
        var handler = new MockHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<p>Texto de prueba</p>", Encoding.UTF8, "text/html")
            })
        };
        using var client = new HttpClient(handler);
        var reader = new WebContentReader(client);

        var text = await reader.FetchTextAsync("https://ejemplo.com/pagina");
        Assert.Equal("Texto de prueba", text);

        var failResult = await reader.FetchTextAsync("ftp://invalido.com");
        Assert.Null(failResult);
    }
}
