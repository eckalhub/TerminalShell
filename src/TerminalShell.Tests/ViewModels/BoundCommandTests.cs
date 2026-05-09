using CommunityToolkit.Mvvm.Input;
using TerminalShell.ViewModels;

namespace TerminalShell.Tests.ViewModels;

public class BoundCommandTests
{
    [Fact]
    public void Execute_ShouldForwardPreBoundParameter()
    {
        string? captured = null;
        RelayCommand<string?> innerCommand = new(value => captured = value);
        BoundCommand boundCommand = new(innerCommand, "service-shell");

        boundCommand.Execute(null);

        Assert.Equal("service-shell", captured);
    }

    [Fact]
    public void CanExecute_ShouldUsePreBoundParameterInsteadOfRuntimeParameter()
    {
        RelayCommand<string?> innerCommand = new(_ => { }, value => value == "safe");
        BoundCommand boundCommand = new(innerCommand, "safe");

        bool actual = boundCommand.CanExecute("unsafe");

        Assert.True(actual);
    }
}
