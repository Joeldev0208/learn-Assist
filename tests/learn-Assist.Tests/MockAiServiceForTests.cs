using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_assist.Tests;

/// <summary>
/// Implementación simple de <see cref="IAiService"/> para tests unitarios.
/// Devuelve una respuesta fija y registra el historial recibido para inspección.
/// </summary>
public sealed class MockAiServiceForTests : IAiService
{
    public List<ChatMessage> LastHistory { get; private set; } = [];
    public string LastMessage { get; private set; } = string.Empty;
    public int CallCount { get; private set; }

    public void Dispose()
    {
        // No hay recursos que liberar en el mock.
    }

    public Task<string> AskAsync(string message, List<ChatMessage> history)
    {
        LastMessage = message ?? string.Empty;
        LastHistory = history ?? [];
        CallCount++;
        return Task.FromResult("Respuesta de prueba del asistente.");
    }
}
