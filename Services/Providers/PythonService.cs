using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services.Providers;

public sealed class PythonService : IAiService
{
    private readonly ApiConfig _config;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private Process? _process;
    private StreamWriter? _input;
    private StreamReader? _output;
    private bool _disposed;

    public PythonService(ApiConfig config)
    {
        _config = config;
    }

    public async Task<string> AskAsync(string message, List<ChatMessage> history)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _requestLock.WaitAsync();
        try
        {
            EnsureStarted();
            var request = JsonSerializer.Serialize(new
            {
                id = Guid.NewGuid().ToString("N"),
                message,
                history,
                model = string.IsNullOrWhiteSpace(_config.Model) ? "local-planner" : _config.Model,
            });

            await _input!.WriteLineAsync(request);
            await _input.FlushAsync();

            var line = await _output!.ReadLineAsync()
                ?? throw new InvalidOperationException("El worker Python terminó inesperadamente.");
            using var response = JsonDocument.Parse(line);
            var root = response.RootElement;

            if (root.TryGetProperty("error", out var error))
                throw new InvalidOperationException(error.GetString() ?? "El worker Python devolvió un error.");

            if (!root.TryGetProperty("text", out var text))
                throw new InvalidOperationException("El worker Python devolvió una respuesta inválida.");

            return text.GetString() ?? string.Empty;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private void EnsureStarted()
    {
        if (_process is { HasExited: false })
            return;

        DisposeProcess();
        var workerPath = ResolveWorkerPath();
        if (!File.Exists(workerPath))
            throw new FileNotFoundException("No se encontró el worker Python.", workerPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = ResolvePythonExecutable(),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(workerPath) ?? AppContext.BaseDirectory,
        };
        startInfo.ArgumentList.Add(workerPath);

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("No se pudo iniciar Python.");
        _input = _process.StandardInput;
        _output = _process.StandardOutput;
        _ = DrainErrorsAsync(_process.StandardError);
    }

    private string ResolvePythonExecutable() =>
        string.IsNullOrWhiteSpace(_config.PythonExecutablePath)
            ? OperatingSystem.IsWindows() ? "python.exe" : "python3"
            : _config.PythonExecutablePath.Trim();

    private string ResolveWorkerPath() =>
        string.IsNullOrWhiteSpace(_config.PythonWorkerPath)
            ? Path.Combine(AppContext.BaseDirectory, "python-worker", "worker.py")
            : Path.GetFullPath(_config.PythonWorkerPath, AppContext.BaseDirectory);

    private static async Task DrainErrorsAsync(StreamReader errors)
    {
        while (await errors.ReadLineAsync() is not null)
        {
        }
    }

    private void DisposeProcess()
    {
        _input?.Dispose();
        _output?.Dispose();
        if (_process is not null && !_process.HasExited)
            _process.Kill(entireProcessTree: true);
        _process?.Dispose();
        _input = null;
        _output = null;
        _process = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        DisposeProcess();
        _requestLock.Dispose();
    }
}
