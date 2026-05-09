using System.Windows;
using TerminalShell.Core;

namespace TerminalShell.Views;

public partial class TerminalContentWindow : Window
{
    public TerminalContentWindow(string content)
    {
        InitializeComponent();
        RuntimeAppIdentity.ApplyWindowIcon(this);
        ContentTextBox.Text = content;
    }
}
