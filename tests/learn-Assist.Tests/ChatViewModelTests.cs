using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using learn_Assist.Models;
using learn_Assist.Services;
using learn_Assist.ViewModels;
using Xunit;

namespace learn_assist.Tests;

public class ChatViewModelTests
{
    [Fact]
    public async Task SendMessage_WithSystemPrompt_CreatesSingleSystemMessage()
    {
        // Arrange
        var aiService = new MockAiServiceForTests();
        var vm = new ChatViewModel(aiService);

        vm.MessageText = "¿Qué es una clase?";
        vm.SetSystemPrompt("Contexto del estudiante: Universidad X\n\n---\n\nDocumentos importados: syllabus.pdf");

        var userMessage = new ChatMessage
        {
            Role = MessageRole.User,
            Content = "¿Qué es una clase?",
            Timestamp = DateTime.Now,
        };
        vm.Messages.Add(userMessage);

        // Act
        await vm.SendMessageAsync();

        // Assert
        Assert.Equal(1, aiService.CallCount);
        var systemMessages = aiService.LastHistory
            .FindAll(m => m.Role == MessageRole.System);
        Assert.Single(systemMessages);
        // El system message debe contener ambas partes (perfil + documento).
        var systemContent = systemMessages[0].Content;
        Assert.Contains("Contexto del estudiante: Universidad X", systemContent);
        Assert.Contains("Documentos importados: syllabus.pdf", systemContent);
        // El primer mensaje del historial enviado a la IA debe ser el system message.
        Assert.Equal(MessageRole.System, aiService.LastHistory[0].Role);
    }

    [Fact]
    public async Task SendMessage_WithOnlyProfile_CreatesSingleSystemMessage()
    {
        var aiService = new MockAiServiceForTests();
        var vm = new ChatViewModel(aiService);

        vm.MessageText = "Hola";
        vm.SetSystemPrompt("Solo perfil");

        var userMessage = new ChatMessage
        {
            Role = MessageRole.User,
            Content = "Hola",
            Timestamp = DateTime.Now,
        };
        vm.Messages.Add(userMessage);

        await vm.SendMessageAsync();

        var systemMessages = aiService.LastHistory
            .FindAll(m => m.Role == MessageRole.System);
        Assert.Single(systemMessages);
        Assert.Contains("Solo perfil", systemMessages[0].Content);
    }

    [Fact]
    public async Task SendMessage_WithOnlyDocument_CreatesSingleSystemMessage()
    {
        var aiService = new MockAiServiceForTests();
        var vm = new ChatViewModel(aiService);

        vm.MessageText = "Hola";
        vm.SetSystemPrompt("Solo documento");

        var userMessage = new ChatMessage
        {
            Role = MessageRole.User,
            Content = "Hola",
            Timestamp = DateTime.Now,
        };
        vm.Messages.Add(userMessage);

        await vm.SendMessageAsync();

        var systemMessages = aiService.LastHistory
            .FindAll(m => m.Role == MessageRole.System);
        Assert.Single(systemMessages);
        Assert.Contains("Solo documento", systemMessages[0].Content);
    }

    [Fact]
    public async Task SendMessage_WithoutSystemPrompt_NoSystemMessage()
    {
        var aiService = new MockAiServiceForTests();
        var vm = new ChatViewModel(aiService);

        vm.MessageText = "Hola";
        vm.SetSystemPrompt(null);

        var userMessage = new ChatMessage
        {
            Role = MessageRole.User,
            Content = "Hola",
            Timestamp = DateTime.Now,
        };
        vm.Messages.Add(userMessage);

        await vm.SendMessageAsync();

        var systemMessages = aiService.LastHistory
            .FindAll(m => m.Role == MessageRole.System);
        Assert.Empty(systemMessages);
        // El primer mensaje debe ser el de usuario.
        Assert.Equal(MessageRole.User, aiService.LastHistory[0].Role);
    }

    [Fact]
    public async Task SendMessage_AddsUserAndAssistantMessages()
    {
        var aiService = new MockAiServiceForTests();
        var vm = new ChatViewModel(aiService);

        vm.MessageText = "Pregunta de prueba";
        vm.SetSystemPrompt(null);

        var userMessage = new ChatMessage
        {
            Role = MessageRole.User,
            Content = "Pregunta de prueba",
            Timestamp = DateTime.Now,
        };
        vm.Messages.Add(userMessage);

        await vm.SendMessageAsync();

        // El historial enviado a la IA debe contener el mensaje de usuario.
        Assert.Contains(aiService.LastHistory, m =>
            m.Role == MessageRole.User && m.Content == "Pregunta de prueba");
        // La respuesta del asistente se agrega a la colección de mensajes.
        Assert.Contains(vm.Messages, m =>
            m.Role == MessageRole.Assistant &&
            m.Content == "Respuesta de prueba del asistente.");
    }
}
