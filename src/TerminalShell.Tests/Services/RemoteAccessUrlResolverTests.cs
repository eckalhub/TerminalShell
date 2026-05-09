using System.Net;
using System.Net.NetworkInformation;
using TerminalShell.Services;

namespace TerminalShell.Tests.Services;

public class RemoteAccessUrlResolverTests
{
    [Fact]
    public void ResolveDisplayInfo_ShouldPreferExplicitBindAddress()
    {
        RemoteDisplayInfo info = RemoteAccessUrlResolver.ResolveDisplayInfo("192.168.3.168", Array.Empty<RemoteDisplayCandidate>());

        Assert.Equal("192.168.3.168", info.Host);
        Assert.Equal("Manual bind address", info.SourceText);
        Assert.True(info.IsExplicitBindAddress);
    }

    [Fact]
    public void ResolveDisplayInfo_ShouldPreferPhysicalLanAdapter_AndIgnoreBenchmarkTunnelAddress()
    {
        RemoteDisplayInfo info = RemoteAccessUrlResolver.ResolveDisplayInfo("0.0.0.0", new[]
        {
            new RemoteDisplayCandidate(
                IPAddress.Parse("198.18.0.1"),
                "Meta",
                "Meta Tunnel",
                NetworkInterfaceType.Tunnel,
                true,
                true),
            new RemoteDisplayCandidate(
                IPAddress.Parse("192.168.3.168"),
                "Slot0D x8",
                "Realtek PCIe GbE Family Controller #2",
                NetworkInterfaceType.Ethernet,
                true,
                false)
        });

        Assert.Equal("192.168.3.168", info.Host);
        Assert.Contains("Slot0D x8", info.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDisplayInfo_ShouldPreferPhysicalLanOverVirtualLan()
    {
        RemoteDisplayInfo info = RemoteAccessUrlResolver.ResolveDisplayInfo("0.0.0.0", new[]
        {
            new RemoteDisplayCandidate(
                IPAddress.Parse("192.168.32.1"),
                "vEthernet (WSL)",
                "Hyper-V Virtual Ethernet Adapter",
                NetworkInterfaceType.Ethernet,
                false,
                true),
            new RemoteDisplayCandidate(
                IPAddress.Parse("10.8.0.15"),
                "Intel I219-V",
                "Intel Ethernet Connection",
                NetworkInterfaceType.Ethernet,
                true,
                false)
        });

        Assert.Equal("10.8.0.15", info.Host);
        Assert.Contains("Intel I219-V", info.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDisplayInfo_ShouldFallbackToVirtualAdapter_WhenNoPhysicalCandidateExists()
    {
        RemoteDisplayInfo info = RemoteAccessUrlResolver.ResolveDisplayInfo("0.0.0.0", new[]
        {
            new RemoteDisplayCandidate(
                IPAddress.Parse("100.72.18.9"),
                "Tailscale",
                "Tailscale Tunnel",
                NetworkInterfaceType.Tunnel,
                true,
                true)
        });

        Assert.Equal("100.72.18.9", info.Host);
        Assert.Contains("Tailscale", info.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDisplayUrl_ShouldAlwaysUseRemotePath()
    {
        string url = RemoteAccessUrlResolver.BuildDisplayUrl("HTTP", "localhost", 18080);

        Assert.Equal("http://localhost:18080/remote/", url);
    }
}
