using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TerminalShell.Services;

public static class RemoteAccessUrlResolver
{
    private const string RemotePath = "/remote/";

    private static readonly string[] VirtualAdapterKeywords =
    {
        "virtual",
        "vmware",
        "virtualbox",
        "hyper-v",
        "vethernet",
        "wsl",
        "wintun",
        "wireguard",
        "zerotier",
        "tailscale",
        "vpn",
        "tunnel",
        "tap-",
        "tap ",
        "sangfor",
        "openvpn",
        "anyconnect",
        "hamachi",
        "docker"
    };

    public static string BuildDisplayUrl(string? protocolMode, string? bindAddress, int port)
    {
        string scheme = string.Equals(protocolMode, "HTTPS", StringComparison.OrdinalIgnoreCase) ? "https" : "http";
        int effectivePort = Math.Clamp(port, 1024, 65535);
        RemoteDisplayInfo displayInfo = ResolveRuntimeDisplayInfo(bindAddress);
        return $"{scheme}://{displayInfo.Host}:{effectivePort}{RemotePath}";
    }

    public static string GetDisplaySourceText(string? bindAddress)
    {
        return ResolveRuntimeDisplayInfo(bindAddress).SourceText;
    }

    private static RemoteDisplayInfo ResolveRuntimeDisplayInfo(string? bindAddress)
    {
        return ResolveDisplayInfo(bindAddress, GetRuntimeCandidates());
    }

    internal static RemoteDisplayInfo ResolveDisplayInfo(string? bindAddress, IReadOnlyCollection<RemoteDisplayCandidate> candidates)
    {
        if (TryResolveExplicitHost(bindAddress, out string explicitHost))
        {
            return new RemoteDisplayInfo(explicitHost, "Manual bind address", true);
        }

        RemoteDisplayCandidate? preferredCandidate = SelectPreferredCandidate(candidates);
        if (preferredCandidate is RemoteDisplayCandidate candidate)
        {
            return new RemoteDisplayInfo(candidate.Address.ToString(), BuildAdapterDisplayName(candidate), false);
        }

        return new RemoteDisplayInfo(IPAddress.Loopback.ToString(), "Loopback fallback", false);
    }

    private static IReadOnlyCollection<RemoteDisplayCandidate> GetRuntimeCandidates()
    {
        List<RemoteDisplayCandidate> candidates = new();

        try
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                IPInterfaceProperties properties = networkInterface.GetIPProperties();
                bool hasIpv4Gateway = properties.GatewayAddresses.Any(gateway =>
                    gateway.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.Any.Equals(gateway.Address)
                    && !IPAddress.None.Equals(gateway.Address));

                bool isVirtualLike = IsVirtualLike(networkInterface);
                string adapterName = string.IsNullOrWhiteSpace(networkInterface.Name)
                    ? networkInterface.Description?.Trim() ?? string.Empty
                    : networkInterface.Name.Trim();
                string adapterDescription = networkInterface.Description?.Trim() ?? string.Empty;

                foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
                {
                    if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    candidates.Add(new RemoteDisplayCandidate(
                        unicastAddress.Address,
                        adapterName,
                        adapterDescription,
                        networkInterface.NetworkInterfaceType,
                        hasIpv4Gateway,
                        isVirtualLike));
                }
            }
        }
        catch
        {
        }

        return candidates;
    }

    internal static RemoteDisplayCandidate? SelectPreferredCandidate(IEnumerable<RemoteDisplayCandidate> candidates)
    {
        return candidates
            .Where(candidate => candidate.Address.AddressFamily == AddressFamily.InterNetwork)
            .Where(candidate => !IPAddress.IsLoopback(candidate.Address))
            .Where(candidate => !IsExcludedAddress(candidate.Address))
            .OrderBy(GetAdapterPriority)
            .ThenBy(candidate => GetAddressPriority(candidate.Address))
            .ThenBy(candidate => BuildAdapterDisplayName(candidate), StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Address.ToString(), StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool TryResolveExplicitHost(string? bindAddress, out string host)
    {
        host = string.Empty;
        if (string.IsNullOrWhiteSpace(bindAddress))
        {
            return false;
        }

        string normalized = bindAddress.Trim();
        if (string.Equals(normalized, "0.0.0.0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "*", StringComparison.Ordinal)
            || string.Equals(normalized, "+", StringComparison.Ordinal)
            || string.Equals(normalized, "::", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "[::]", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            host = "localhost";
            return true;
        }

        if (IPAddress.TryParse(normalized, out IPAddress? address))
        {
            host = address.ToString();
            return true;
        }

        host = normalized;
        return true;
    }

    private static int GetAdapterPriority(RemoteDisplayCandidate candidate)
    {
        if (!candidate.IsVirtualLike && candidate.HasIpv4Gateway)
        {
            return 0;
        }

        if (!candidate.IsVirtualLike)
        {
            return 1;
        }

        if (candidate.HasIpv4Gateway)
        {
            return 2;
        }

        return 3;
    }

    private static int GetAddressPriority(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        if (IsPrivateLanAddress(bytes))
        {
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return 0;
            }

            if (bytes[0] == 10)
            {
                return 1;
            }

            if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            {
                return 2;
            }
        }

        if (IsCarrierGradeNatAddress(bytes))
        {
            return 3;
        }

        return 4;
    }

    private static string BuildAdapterDisplayName(RemoteDisplayCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.AdapterName))
        {
            return string.IsNullOrWhiteSpace(candidate.AdapterDescription)
                ? "Auto-detected adapter"
                : candidate.AdapterDescription;
        }

        if (string.IsNullOrWhiteSpace(candidate.AdapterDescription)
            || string.Equals(candidate.AdapterName, candidate.AdapterDescription, StringComparison.OrdinalIgnoreCase))
        {
            return candidate.AdapterName;
        }

        return $"{candidate.AdapterName} ({candidate.AdapterDescription})";
    }

    private static bool IsVirtualLike(NetworkInterface networkInterface)
    {
        if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Loopback or NetworkInterfaceType.Ppp)
        {
            return true;
        }

        string composite = $"{networkInterface.Name} {networkInterface.Description}".ToLowerInvariant();
        return VirtualAdapterKeywords.Any(keyword => composite.Contains(keyword, StringComparison.Ordinal));
    }

    private static bool IsExcludedAddress(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return IsApipa(bytes)
            || IsBenchmarkingAddress(bytes)
            || IsDocumentationAddress(bytes)
            || IsZeroAddress(bytes);
    }

    private static bool IsPrivateLanAddress(byte[] bytes)
    {
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }

    private static bool IsCarrierGradeNatAddress(byte[] bytes)
    {
        return bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
    }

    private static bool IsApipa(byte[] bytes)
    {
        return bytes[0] == 169 && bytes[1] == 254;
    }

    private static bool IsBenchmarkingAddress(byte[] bytes)
    {
        return bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19);
    }

    private static bool IsDocumentationAddress(byte[] bytes)
    {
        return (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2)
            || (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100)
            || (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
    }

    private static bool IsZeroAddress(byte[] bytes)
    {
        return bytes[0] == 0;
    }
}

internal readonly record struct RemoteDisplayInfo(string Host, string SourceText, bool IsExplicitBindAddress);

internal readonly record struct RemoteDisplayCandidate(
    IPAddress Address,
    string AdapterName,
    string AdapterDescription,
    NetworkInterfaceType InterfaceType,
    bool HasIpv4Gateway,
    bool IsVirtualLike);
