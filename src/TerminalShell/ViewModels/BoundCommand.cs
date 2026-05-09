using System;
using System.Windows.Input;

namespace TerminalShell.ViewModels;

public sealed class BoundCommand : ICommand
{
    private readonly ICommand _innerCommand;
    private readonly object? _parameter;

    public BoundCommand(ICommand innerCommand, object? parameter)
    {
        _innerCommand = innerCommand ?? throw new ArgumentNullException(nameof(innerCommand));
        _parameter = parameter;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => _innerCommand.CanExecuteChanged += value;
        remove => _innerCommand.CanExecuteChanged -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return _innerCommand.CanExecute(_parameter);
    }

    public void Execute(object? parameter)
    {
        _innerCommand.Execute(_parameter);
    }
}
