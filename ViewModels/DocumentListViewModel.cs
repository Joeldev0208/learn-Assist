using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using learn_Assist.Models;

namespace learn_Assist.ViewModels;

public partial class DocumentListViewModel : ViewModelBase
{
    public ObservableCollection<UserDocument> Documents { get; } = [];

    public DocumentListViewModel()
    {
        Documents.Add(new UserDocument { Name = "React Documentation.pdf", Type = "pdf" });
        Documents.Add(new UserDocument { Name = "Design Tokens.fig", Type = "figma" });
        Documents.Add(new UserDocument { Name = "Architecture Overview.md", Type = "md" });
        Documents.Add(new UserDocument { Name = "API Reference.pdf", Type = "pdf" });
        Documents.Add(new UserDocument { Name = "Component Library.zip", Type = "zip" });
    }
}
