using System.Windows;
using TerminalShell.Core;

namespace TerminalShell.Views;

public partial class AllTerminalsIdleAlertWindow : Window
{
    public event EventHandler? AlertDismissed;

    public AllTerminalsIdleAlertWindow(string message)
    {
        InitializeComponent();
        RuntimeAppIdentity.ApplyWindowIcon(this);
        UpdateMessage(message);
        Closed += (_, _) => AlertDismissed?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateMessage(string message)
    {
        MessageTextBlock.Text = message;
    }

    private void DismissButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
