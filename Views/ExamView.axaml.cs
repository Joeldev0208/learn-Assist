using Avalonia.Controls;

namespace learn_Assist.Views;

public partial class ExamView : Window
{
    public ExamView(Window? owner = null)
    {
        InitializeComponent();
        if (owner is not null)
            Owner = owner;
    }
}
