using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace learn_Assist.Models;

public partial class ChatSession : ObservableObject
{
    [ObservableProperty]
    public partial Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    public partial string Title { get; set; } = "New Chat";

    public ObservableCollection<ChatMessage> Messages { get; set; } = [];

    [ObservableProperty]
    public partial DateTime CreatedAt { get; set; } = DateTime.Now;

    public string? StorageFileName { get; set; }
}
