using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using TerminalShell.Core;
using TerminalShell.Core.Config;
using TerminalShell.Core.Security;
using TerminalShell.Models;

namespace TerminalShell.Services;

public sealed class RemoteWebConsoleService : IDisposable
{
    private const string AuthCookieName = "terminalshell_remote_auth";
    private const string RemoteBasePath = "/remote";
    private static readonly TimeSpan DashboardPushInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SessionPushInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StopServerTimeout = TimeSpan.FromSeconds(2);
    internal const int DetailTailLineCount = 800;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] BusyMarkers =
    [
        "Working (",
        "Working...",
        "Working…",
        "esc to interrupt",
        "Orbiting",
        "thinking"
    ];

    private static readonly string[] CompletionMarkers =
    [
        "Worked for "
    ];

    private static readonly string[] PromptMarkers =
    [
        ">",
        "›"
    ];

    private readonly IConfigManager _configManager;
    private readonly Action _requestMonitorRefresh;
    private readonly object _sessionLock = new();
    private readonly Dictionary<TerminalSession, RemoteRuntimeState> _runtimeStates = new();
    private readonly ConcurrentDictionary<string, LoginAttemptState> _loginAttempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly object _socketLock = new();
    private readonly HashSet<WebSocket> _activeSockets = new();

    private List<TerminalSession> _sessions = new();
    private WebApplication? _app;
    private CancellationTokenSource? _serverShutdownCts;
    private string _activeHostKey = string.Empty;
    private bool _disposed;
    private string _lastNotStartedReason = string.Empty;
    private string _lastPublishedHostStateKey = string.Empty;
    private string _lastPublishedWarningKey = string.Empty;

    public event EventHandler<RemoteWebConsoleHostStateChangedEventArgs>? HostStateChanged;

    public RemoteWebConsoleService(IConfigManager configManager, Action requestMonitorRefresh)
    {
        _configManager = configManager;
        _requestMonitorRefresh = requestMonitorRefresh;
    }

    public void ApplyConfiguration(IEnumerable<TerminalSession> sessions)
    {
        UpdateSessionBindings(sessions);
        _ = Task.Run(ApplyServerStateAsync);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_sessionLock)
        {
            foreach (TerminalSession session in _runtimeStates.Keys.ToList())
            {
                session.UserCommandSent -= Session_UserCommandSent;
            }
        }

        SignalServerShutdown();

        if (!_lifecycleLock.Wait(StopServerTimeout))
        {
            SimpleLogger.Log("[RemoteWebConsole] Dispose skipped waiting for lifecycle lock after timeout.");
            return;
        }

        try
        {
            StopServerCoreAsync().GetAwaiter().GetResult();
        }
        finally
        {
            _lifecycleLock.Release();
            _lifecycleLock.Dispose();
        }
    }

    private void UpdateSessionBindings(IEnumerable<TerminalSession> sessions)
    {
        HashSet<TerminalSession> nextSessions = new(sessions);

        lock (_sessionLock)
        {
            foreach (TerminalSession removedSession in _runtimeStates.Keys.Where(session => !nextSessions.Contains(session)).ToList())
            {
                removedSession.UserCommandSent -= Session_UserCommandSent;
                _runtimeStates.Remove(removedSession);
            }

            foreach (TerminalSession session in nextSessions)
            {
                if (_runtimeStates.ContainsKey(session))
                {
                    continue;
                }

                _runtimeStates[session] = new RemoteRuntimeState();
                session.UserCommandSent += Session_UserCommandSent;
            }

            _sessions = nextSessions.ToList();
        }
    }

    private void Session_UserCommandSent(TerminalSession session, TerminalCommandSentEventArgs command)
    {
        lock (_sessionLock)
        {
            if (!_runtimeStates.TryGetValue(session, out RemoteRuntimeState? state))
            {
                state = new RemoteRuntimeState();
                _runtimeStates[session] = state;
            }

            state.HasSeenBusyState = false;
            state.LastCommandUtc = DateTimeOffset.UtcNow;
            state.LastSnapshot = string.Empty;
            state.LastChangedUtc = DateTimeOffset.UtcNow;
            state.IsFailed = false;
            state.LastFailureKeyword = string.Empty;
        }
    }

    private async Task ApplyServerStateAsync()
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            AppConfig config = _configManager.Config;
            if (!ShouldStartServer(config, out string reason))
            {
                if (_app != null)
                {
                    await StopServerCoreAsync().ConfigureAwait(false);
                }

                LogNotStartedReason(reason);
                PublishHostState(
                    isRunning: false,
                    hasError: false,
                    titleSuffix: string.Empty,
                    statusText: reason,
                    shouldWarnUser: false,
                    warningMessage: string.Empty);
                return;
            }

            string nextHostKey = BuildHostKey(config);
            if (_app != null && string.Equals(_activeHostKey, nextHostKey, StringComparison.Ordinal))
            {
                return;
            }

            await StopServerCoreAsync().ConfigureAwait(false);
            await StartServerCoreAsync(config, nextHostKey).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private bool ShouldStartServer(AppConfig config, out string reason)
    {
        if (!config.EnableRemoteWebConsole)
        {
            reason = "Remote Web Console disabled.";
            return false;
        }

        if (string.Equals(config.RemoteProtocolMode, "HTTPS", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Remote Web Console HTTPS mode is reserved in the current build. Host not started.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.RemotePasswordHash))
        {
            reason = "Remote Web Console password missing. Host not started.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static string BuildHostKey(AppConfig config)
    {
        return $"{config.RemoteProtocolMode}|{config.RemoteBindAddress}|{config.RemotePort}";
    }

    private void LogNotStartedReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || string.Equals(reason, _lastNotStartedReason, StringComparison.Ordinal))
        {
            return;
        }

        _lastNotStartedReason = reason;
        SimpleLogger.Log($"[RemoteWebConsole] {reason}");
    }

    private void PublishHostState(bool isRunning, bool hasError, string titleSuffix, string statusText, bool shouldWarnUser, string warningMessage)
    {
        string normalizedTitleSuffix = titleSuffix ?? string.Empty;
        string normalizedStatusText = statusText ?? string.Empty;
        string normalizedWarningMessage = warningMessage ?? string.Empty;
        string stateKey = $"{isRunning}|{hasError}|{normalizedTitleSuffix}|{normalizedStatusText}";
        string warningKey = shouldWarnUser ? normalizedWarningMessage : string.Empty;

        if (string.Equals(stateKey, _lastPublishedHostStateKey, StringComparison.Ordinal)
            && string.Equals(warningKey, _lastPublishedWarningKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastPublishedHostStateKey = stateKey;
        _lastPublishedWarningKey = warningKey;

        HostStateChanged?.Invoke(
            this,
            new RemoteWebConsoleHostStateChangedEventArgs(
                isRunning,
                hasError,
                normalizedTitleSuffix,
                normalizedStatusText,
                shouldWarnUser,
                normalizedWarningMessage));
    }

    private async Task StartServerCoreAsync(AppConfig config, string hostKey)
    {
        try
        {
            ResetServerShutdownSignal();

            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel(options => ConfigureListen(options, config));

            WebApplication app = builder.Build();
            app.Use(async (context, next) =>
            {
                if (string.Equals(context.Request.Path.Value, RemoteBasePath, StringComparison.Ordinal))
                {
                    context.Response.Redirect($"{RemoteBasePath}/", permanent: false);
                    return;
                }

                await next().ConfigureAwait(false);
            });

            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30)
            });

            MapRoutes(app);

            await app.StartAsync().ConfigureAwait(false);
            _app = app;
            _activeHostKey = hostKey;
            _lastNotStartedReason = string.Empty;
            string displayUrl = RemoteAccessUrlResolver.BuildDisplayUrl(config.RemoteProtocolMode, config.RemoteBindAddress, config.RemotePort);
            SimpleLogger.Log($"[RemoteWebConsole] Listening on {displayUrl}");
            PublishHostState(
                isRunning: true,
                hasError: false,
                titleSuffix: $"[Remote:{config.RemotePort}]",
                statusText: $"Remote Web Console listening on {displayUrl}",
                shouldWarnUser: false,
                warningMessage: string.Empty);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "RemoteWebConsoleService.StartServerCoreAsync");
            _activeHostKey = string.Empty;
            _app = null;
            string failureMessage = BuildStartupFailureMessage(config, ex);
            PublishHostState(
                isRunning: false,
                hasError: true,
                titleSuffix: $"[Remote Error:{config.RemotePort}]",
                statusText: failureMessage,
                shouldWarnUser: true,
                warningMessage: failureMessage);
        }
    }

    private static void ConfigureListen(KestrelServerOptions options, AppConfig config)
    {
        IPAddress listenAddress = ResolveListenAddress(config.RemoteBindAddress);
        options.Listen(listenAddress, config.RemotePort, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
        });
    }

    private static IPAddress ResolveListenAddress(string? bindAddress)
    {
        string normalized = string.IsNullOrWhiteSpace(bindAddress) ? "0.0.0.0" : bindAddress.Trim();
        if (string.Equals(normalized, "0.0.0.0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "*", StringComparison.Ordinal)
            || string.Equals(normalized, "+", StringComparison.Ordinal))
        {
            return IPAddress.Any;
        }

        if (string.Equals(normalized, "::", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "[::]", StringComparison.OrdinalIgnoreCase))
        {
            return IPAddress.IPv6Any;
        }

        if (string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return IPAddress.Loopback;
        }

        return IPAddress.TryParse(normalized, out IPAddress? parsedAddress)
            ? parsedAddress
            : IPAddress.Any;
    }

    private async Task StopServerCoreAsync()
    {
        WebApplication? app = _app;
        _app = null;
        _activeHostKey = string.Empty;
        SignalServerShutdown();

        if (app == null)
        {
            return;
        }

        try
        {
            using CancellationTokenSource stopCts = new(StopServerTimeout);
            await app.StopAsync(stopCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "RemoteWebConsoleService.StopServerCoreAsync");
        }
        finally
        {
            try
            {
                Task disposeTask = app.DisposeAsync().AsTask();
                if (await Task.WhenAny(disposeTask, Task.Delay(StopServerTimeout)).ConfigureAwait(false) == disposeTask)
                {
                    await disposeTask.ConfigureAwait(false);
                }
                else
                {
                    SimpleLogger.Log("[RemoteWebConsole] DisposeAsync timed out; process exit will release remaining server resources.");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "RemoteWebConsoleService.DisposeAsync");
            }
        }
    }

    internal static string BuildStartupFailureMessage(AppConfig config, Exception ex)
    {
        if (ContainsSocketError(ex, SocketError.AddressAlreadyInUse)
            || ContainsText(ex, "address already in use")
            || ContainsText(ex, "only one usage of each socket address"))
        {
            return $"Remote Web Console failed to start because port {config.RemotePort} is already in use. Close the other program or change Remote Port.";
        }

        if (ContainsSocketError(ex, SocketError.AccessDenied))
        {
            return $"Remote Web Console failed to start because access to {config.RemoteBindAddress}:{config.RemotePort} was denied.";
        }

        string baseMessage = ex.GetBaseException().Message?.Trim() ?? "Unknown error.";
        return $"Remote Web Console failed to start on {config.RemoteBindAddress}:{config.RemotePort}. {baseMessage}";
    }

    private static bool ContainsSocketError(Exception? exception, SocketError socketError)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is SocketException socketException && socketException.SocketErrorCode == socketError)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsText(Exception? exception, string text)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message)
                && current.Message.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void ResetServerShutdownSignal()
    {
        CancellationTokenSource? previousCts;
        lock (_socketLock)
        {
            previousCts = _serverShutdownCts;
            _serverShutdownCts = new CancellationTokenSource();
        }

        if (previousCts == null)
        {
            return;
        }

        try
        {
            previousCts.Cancel();
        }
        catch
        {
        }
        finally
        {
            previousCts.Dispose();
        }
    }

    private CancellationToken RegisterSocket(WebSocket socket)
    {
        lock (_socketLock)
        {
            _activeSockets.Add(socket);
            return _serverShutdownCts?.Token ?? CancellationToken.None;
        }
    }

    private void UnregisterSocket(WebSocket socket)
    {
        lock (_socketLock)
        {
            _activeSockets.Remove(socket);
        }
    }

    private void SignalServerShutdown()
    {
        List<WebSocket> socketsToAbort;
        CancellationTokenSource? shutdownCts;

        lock (_socketLock)
        {
            socketsToAbort = _activeSockets.ToList();
            shutdownCts = _serverShutdownCts;
            _serverShutdownCts = null;
        }

        if (shutdownCts != null)
        {
            try
            {
                shutdownCts.Cancel();
            }
            catch
            {
            }
            finally
            {
                shutdownCts.Dispose();
            }
        }

        foreach (WebSocket socket in socketsToAbort)
        {
            try
            {
                socket.Abort();
            }
            catch
            {
            }
        }
    }

    private void MapRoutes(WebApplication app)
    {
        app.MapGet($"{RemoteBasePath}/", ServeIndexAsync);
        app.MapPost($"{RemoteBasePath}/auth/login", LoginAsync);
        app.MapPost($"{RemoteBasePath}/auth/logout", LogoutAsync);

        app.MapGet($"{RemoteBasePath}/api/bootstrap", GetBootstrapAsync);
        app.MapGet($"{RemoteBasePath}/api/sessions", GetSessionsAsync);
        app.MapGet($"{RemoteBasePath}/api/sessions/{{sessionName}}", GetSessionAsync);
        app.MapPost($"{RemoteBasePath}/api/sessions/{{sessionName}}/send", SendCommandAsync);
        app.MapPost($"{RemoteBasePath}/api/sessions/{{sessionName}}/drafts/{{draftId}}/send", SendDraftAsync);
        app.MapDelete($"{RemoteBasePath}/api/sessions/{{sessionName}}/drafts/{{draftId}}", DeleteDraftAsync);
        app.MapPost($"{RemoteBasePath}/api/sessions/{{sessionName}}/auto-draft", ToggleAutoDraftAsync);

        app.MapGet($"{RemoteBasePath}/ws/dashboard", DashboardSocketAsync);
        app.MapGet($"{RemoteBasePath}/ws/session/{{sessionName}}", SessionSocketAsync);
    }

    private static string? GetAuthToken(HttpContext context)
    {
        context.Request.Cookies.TryGetValue(AuthCookieName, out string? token);
        return token;
    }

    private bool TryAuthorizeRequest(HttpContext context, bool renewSession, out RemoteAuthSession? authSession)
    {
        authSession = null;
        string? token = GetAuthToken(context);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        AppConfig config = _configManager.Config;
        if (!TryDecodeAuthToken(token, config, out RemoteAuthSession session))
        {
            DeleteAuthCookie(context.Response);
            return false;
        }

        if (!IsAuthSessionAlive(session))
        {
            DeleteAuthCookie(context.Response);
            return false;
        }

        if (renewSession)
        {
            DateTimeOffset nextIdleExpiry = DateTimeOffset.UtcNow.AddMinutes(GetSessionTimeoutMinutes(config));
            if (nextIdleExpiry > session.AbsoluteExpiresUtc)
            {
                nextIdleExpiry = session.AbsoluteExpiresUtc;
            }

            session = new RemoteAuthSession
            {
                IdleExpiresUtc = nextIdleExpiry,
                AbsoluteExpiresUtc = session.AbsoluteExpiresUtc
            };

            string renewedToken = BuildAuthToken(config, session);
            context.Response.Cookies.Append(AuthCookieName, renewedToken, BuildCookieOptions(session.AbsoluteExpiresUtc));
        }

        authSession = session;
        return true;
    }

    private bool IsAuthSessionAlive(RemoteAuthSession session)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return session.IdleExpiresUtc > now && session.AbsoluteExpiresUtc > now;
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpRequest request)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(request.Body, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private static async Task WriteJsonAsync(HttpContext context, int statusCode, object payload)
    {
        context.Response.StatusCode = statusCode;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context)
    {
        await WriteJsonAsync(context, StatusCodes.Status401Unauthorized, new
        {
            success = false,
            message = "Unauthorized."
        });
    }

    private static async Task SendJsonAsync(WebSocket socket, object payload, CancellationToken cancellationToken)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private string GetClientKey(HttpContext context)
    {
        string? ip = context.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? "unknown-client" : ip;
    }

    private bool TryGetLockoutRemaining(string clientKey, out int remainingSeconds)
    {
        remainingSeconds = 0;
        if (!_loginAttempts.TryGetValue(clientKey, out LoginAttemptState? state))
        {
            return false;
        }

        if (!state.LockedUntilUtc.HasValue || state.LockedUntilUtc.Value <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        remainingSeconds = (int)Math.Ceiling((state.LockedUntilUtc.Value - DateTimeOffset.UtcNow).TotalSeconds);
        return remainingSeconds > 0;
    }

    private void RegisterFailedLogin(string clientKey)
    {
        LoginAttemptState state = _loginAttempts.GetOrAdd(clientKey, _ => new LoginAttemptState());
        state.FailedAttempts++;

        int maxAttempts = Math.Clamp(_configManager.Config.RemoteMaxLoginAttempts, 1, 20);
        if (state.FailedAttempts < maxAttempts)
        {
            return;
        }

        state.FailedAttempts = 0;
        state.LockedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_configManager.Config.RemoteLoginLockoutMinutes, 1, 1440));
    }

    private async Task ServeIndexAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(RemoteWebConsolePage.Html);
    }

    private async Task LoginAsync(HttpContext context)
    {
        LoginRequest? request = await ReadJsonAsync<LoginRequest>(context.Request);
        if (request == null || string.IsNullOrWhiteSpace(request.Password))
        {
            await WriteJsonAsync(context, StatusCodes.Status400BadRequest, new { success = false, message = "Password is required." });
            return;
        }

        string clientKey = GetClientKey(context);
        if (TryGetLockoutRemaining(clientKey, out int lockoutSeconds))
        {
            await WriteJsonAsync(context, StatusCodes.Status429TooManyRequests, new
            {
                success = false,
                message = $"Too many failed logins. Try again in {lockoutSeconds} seconds."
            });
            return;
        }

        if (!PasswordHashUtility.VerifyPassword(request.Password, _configManager.Config.RemotePasswordHash))
        {
            RegisterFailedLogin(clientKey);
            await WriteJsonAsync(context, StatusCodes.Status401Unauthorized, new
            {
                success = false,
                message = "Password incorrect."
            });
            return;
        }

        _loginAttempts.TryRemove(clientKey, out _);

        AppConfig config = _configManager.Config;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset absoluteExpiresUtc = now.AddDays(GetCookieLifetimeDays(config));
        DateTimeOffset idleExpiresUtc = now.AddMinutes(GetSessionTimeoutMinutes(config));
        if (idleExpiresUtc > absoluteExpiresUtc)
        {
            idleExpiresUtc = absoluteExpiresUtc;
        }

        RemoteAuthSession authSession = new()
        {
            IdleExpiresUtc = idleExpiresUtc,
            AbsoluteExpiresUtc = absoluteExpiresUtc
        };

        string token = BuildAuthToken(config, authSession);
        context.Response.Cookies.Append(AuthCookieName, token, BuildCookieOptions(absoluteExpiresUtc));
        await WriteJsonAsync(context, StatusCodes.Status200OK, new
        {
            success = true,
            idleExpiresUtc,
            absoluteExpiresUtc
        });
    }

    private async Task LogoutAsync(HttpContext context)
    {
        DeleteAuthCookie(context.Response);

        await WriteJsonAsync(context, StatusCodes.Status200OK, new { success = true });
    }

    private async Task GetBootstrapAsync(HttpContext context)
    {
        if (!TryAuthorizeRequest(context, renewSession: true, out _))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        await WriteJsonAsync(context, StatusCodes.Status200OK, new
        {
            protocolMode = _configManager.Config.RemoteProtocolMode,
            sessions = BuildSessionSummaries()
        });
    }

    private async Task GetSessionsAsync(HttpContext context)
    {
        if (!TryAuthorizeRequest(context, renewSession: true, out _))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        await WriteJsonAsync(context, StatusCodes.Status200OK, new
        {
            sessions = BuildSessionSummaries()
        });
    }

    private async Task GetSessionAsync(HttpContext context, string sessionName)
    {
        if (!TryAuthorizeRequest(context, renewSession: true, out _))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        TerminalSession? session = FindSessionByName(sessionName);
        if (session == null)
        {
            await WriteJsonAsync(context, StatusCodes.Status404NotFound, new { success = false, message = "Terminal not found." });
            return;
        }

        await WriteJsonAsync(context, StatusCodes.Status200OK, new
        {
            session = BuildSessionDetail(session)
        });
    }

    private async Task SendCommandAsync(HttpContext context, string sessionName)
    {
        if (!TryAuthorizeRequest(context, renewSession: true, out _))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        TerminalSession? session = FindSessionByName(sessionName);
        if (session == null)
        {
            await WriteJsonAsync(context, StatusCodes.Status404NotFound, new { success = false, message = "Terminal not found." });
            return;
        }

        SendCommandRequest? request = await ReadJsonAsync<SendCommandRequest>(context.Request);
        if (request == null || request.Command == null)
        {
            await WriteJsonAsync(context, StatusCodes.Status400BadRequest, new { success = false, message = "Command payload is invalid." });
            return;
        }

        bool success = session.TrySendCommandText(request.Command);
        await WriteJsonAsync(context, success ? StatusCodes.Status200OK : StatusCodes.Status500InternalServerError, new
        {
            success,
            session = BuildSessionDetail(session)
        });
    }

    private async Task SendDraftAsync(HttpContext context, string sessionName, string draftId)
    {
        if (!TryAuthorizeRequest(context, renewSession: true, out _))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        TerminalSession? session = FindSessionByName(sessionName);
        if (session == null)
        {
            await WriteJsonAsync(context, StatusCodes.Status404NotFound, new { success = false, message = "Terminal not found." });
            return;
        }

        bool success = session.TrySendDraftById(draftId);
        await WriteJsonAsync(context, success ? StatusCodes.Status200OK : StatusCodes.Status404NotFound, new
        {
            success,
            session = BuildSessionDetail(session)
        });
    }

    private async Task DeleteDraftAsync(HttpContext context, string sessionName, string draftId)
    {
        if (!TryAuthorizeRequest(context, renewSession: true, out _))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        TerminalSession? session = FindSessionByName(sessionName);
        if (session == null)
        {
            await WriteJsonAsync(context, StatusCodes.Status404NotFound, new { success = false, message = "Terminal not found." });
            return;
        }

        bool success = session.DeleteDraftById(draftId);
        await WriteJsonAsync(context, success ? StatusCodes.Status200OK : StatusCodes.Status404NotFound, new
        {
            success,
            session = BuildSessionDetail(session)
        });
    }

    private async Task ToggleAutoDraftAsync(HttpContext context, string sessionName)
    {
        if (!TryAuthorizeRequest(context, renewSession: true, out _))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        TerminalSession? session = FindSessionByName(sessionName);
        if (session == null)
        {
            await WriteJsonAsync(context, StatusCodes.Status404NotFound, new { success = false, message = "Terminal not found." });
            return;
        }

        ToggleAutoDraftRequest? request = await ReadJsonAsync<ToggleAutoDraftRequest>(context.Request);
        if (request == null)
        {
            await WriteJsonAsync(context, StatusCodes.Status400BadRequest, new { success = false, message = "Enabled flag is required." });
            return;
        }

        session.IsAutoDraftQueueEnabled = request.Enabled;

        TerminalConfig? config = _configManager.Config.Terminals.FirstOrDefault(terminal =>
            string.Equals(terminal.Name, session.Name, StringComparison.Ordinal));
        if (config != null)
        {
            config.EnableAutoSubmitDraftQueueOnCompletion = request.Enabled;
            _configManager.Save();
        }

        _requestMonitorRefresh();

        await WriteJsonAsync(context, StatusCodes.Status200OK, new
        {
            success = true,
            session = BuildSessionDetail(session)
        });
    }

    private async Task DashboardSocketAsync(HttpContext context)
    {
        if (!TryAuthorizeRequest(context, renewSession: true, out RemoteAuthSession? authSession))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
        CancellationToken serverShutdownToken = RegisterSocket(socket);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, serverShutdownToken);
        using PeriodicTimer timer = new(DashboardPushInterval);

        try
        {
            await SendJsonAsync(socket, new
            {
                type = "dashboard",
                sessions = BuildSessionSummaries()
            }, linkedCts.Token);

            while (socket.State == WebSocketState.Open
                   && !linkedCts.Token.IsCancellationRequested
                   && IsAuthSessionAlive(authSession!))
            {
                try
                {
                    if (!await timer.WaitForNextTickAsync(linkedCts.Token))
                    {
                        break;
                    }

                    await SendJsonAsync(socket, new
                    {
                        type = "dashboard",
                        sessions = BuildSessionSummaries()
                    }, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException)
                {
                    break;
                }
            }
        }
        finally
        {
            UnregisterSocket(socket);
            await CloseSocketAsync(socket);
        }
    }

    private async Task SessionSocketAsync(HttpContext context, string sessionName)
    {
        if (!TryAuthorizeRequest(context, renewSession: true, out RemoteAuthSession? authSession))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
        CancellationToken serverShutdownToken = RegisterSocket(socket);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, serverShutdownToken);
        using PeriodicTimer timer = new(SessionPushInterval);

        try
        {
            while (socket.State == WebSocketState.Open
                   && !linkedCts.Token.IsCancellationRequested
                   && IsAuthSessionAlive(authSession!))
            {
                TerminalSession? session = FindSessionByName(sessionName);
                if (session == null)
                {
                    await SendJsonAsync(socket, new
                    {
                        type = "terminalMissing",
                        sessionName
                    }, linkedCts.Token);
                    break;
                }

                try
                {
                    await SendJsonAsync(socket, new
                    {
                        type = "terminal",
                        session = BuildSessionDetail(session)
                    }, linkedCts.Token);

                    if (!await timer.WaitForNextTickAsync(linkedCts.Token))
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException)
                {
                    break;
                }
            }
        }
        finally
        {
            UnregisterSocket(socket);
            await CloseSocketAsync(socket);
        }
    }

    private static async Task CloseSocketAsync(WebSocket socket)
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
            }
            catch
            {
            }
        }
    }

    private List<object> BuildSessionSummaries()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return GetSessionSnapshot()
            .Select(session =>
            {
                SessionStateSnapshot snapshot = CaptureSessionState(session, 24, now);
                return (object)new
                {
                    name = session.Name,
                    terminalVoiceName = session.TerminalVoiceName,
                    workingDirectory = session.WorkingDirectory,
                    shellType = session.ShellType,
                    status = snapshot.Status,
                    statusReason = snapshot.StatusReason,
                    stableSeconds = snapshot.StableSeconds,
                    hasDrafts = session.HasInputDrafts,
                    draftCount = session.InputDrafts.Count,
                    isAutoDraftQueueEnabled = session.IsAutoDraftQueueEnabled,
                    isAutoDraftQueueActive = session.IsAutoDraftQueueActive,
                    isAutoDraftQueueWaitingForUserInput = session.IsAutoDraftQueueWaitingForUserInput
                };
            })
            .ToList();
    }

    private object BuildSessionDetail(TerminalSession session)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SessionStateSnapshot snapshot = CaptureSessionState(session, DetailTailLineCount, now);
        return new
        {
            name = session.Name,
            terminalVoiceName = session.TerminalVoiceName,
            workingDirectory = session.WorkingDirectory,
            shellType = session.ShellType,
            status = snapshot.Status,
            statusReason = snapshot.StatusReason,
            stableSeconds = snapshot.StableSeconds,
            tailSnapshot = snapshot.Snapshot,
            isAutoDraftQueueEnabled = session.IsAutoDraftQueueEnabled,
            isAutoDraftQueueActive = session.IsAutoDraftQueueActive,
            isAutoDraftQueueWaitingForUserInput = session.IsAutoDraftQueueWaitingForUserInput,
            drafts = session.GetInputDraftsSnapshot().Select(draft => new
            {
                id = draft.Id,
                previewText = draft.PreviewText,
                text = draft.Text,
                updatedAtUtc = draft.UpdatedAtUtc
            })
        };
    }

    private SessionStateSnapshot CaptureSessionState(TerminalSession session, int tailLineCount, DateTimeOffset now)
    {
        string snapshot = NormalizeSnapshot(session.ReadTerminalTailSnapshot(tailLineCount));
        RemoteRuntimeState runtimeState = GetRuntimeState(session);

        if (!string.Equals(runtimeState.LastSnapshot, snapshot, StringComparison.Ordinal))
        {
            runtimeState.LastSnapshot = snapshot;
            runtimeState.LastChangedUtc = now;
        }

        if (!IsProcessAlive(session))
        {
            return new SessionStateSnapshot("Exited", "The terminal process is no longer running.", snapshot, (int)Math.Max(0, (now - runtimeState.LastChangedUtc).TotalSeconds));
        }

        IReadOnlyList<string> failureKeywords = TaskFailureGuard.ParseKeywords(_configManager.Config.TaskFailureKeywords);
        bool hasMatchedFailureKeyword = TaskFailureGuard.TryMatch(snapshot, failureKeywords, out string matchedFailureKeyword);
        if (runtimeState.IsFailed || hasMatchedFailureKeyword)
        {
            if (!runtimeState.IsFailed)
            {
                runtimeState.IsFailed = true;
                runtimeState.LastFailureKeyword = matchedFailureKeyword;
            }

            string failureKeyword = string.IsNullOrWhiteSpace(runtimeState.LastFailureKeyword)
                ? matchedFailureKeyword
                : runtimeState.LastFailureKeyword;

            return new SessionStateSnapshot("Failed", $"Failure keyword detected: {failureKeyword}", snapshot, (int)Math.Max(0, (now - runtimeState.LastChangedUtc).TotalSeconds));
        }

        IReadOnlyList<string> waitKeywords = UserInputWaitGuard.ParseKeywords(_configManager.Config.WaitForUserInputKeywords);
        bool waitingForUserInput = UserInputWaitGuard.IsMatch(snapshot, waitKeywords) || session.IsAutoDraftQueueWaitingForUserInput;
        if (waitingForUserInput)
        {
            return new SessionStateSnapshot("WaitingForInput", "Recent output suggests the model is waiting for your confirmation.", snapshot, (int)Math.Max(0, (now - runtimeState.LastChangedUtc).TotalSeconds));
        }

        if (ContainsBusyMarker(snapshot))
        {
            runtimeState.HasSeenBusyState = true;
            return new SessionStateSnapshot("Busy", "Busy markers detected in recent terminal output.", snapshot, 0);
        }

        if (runtimeState.LastCommandUtc.HasValue
            && !runtimeState.HasSeenBusyState
            && now - runtimeState.LastCommandUtc.Value < TimeSpan.FromSeconds(Math.Max(30, _configManager.Config.TaskCompletionCheckIntervalSeconds)))
        {
            return new SessionStateSnapshot("Submitted", "A command was sent recently. Waiting for the terminal to enter a busy state.", snapshot, (int)Math.Max(0, (now - runtimeState.LastChangedUtc).TotalSeconds));
        }

        TimeSpan stableThreshold = TimeSpan.FromMinutes(Math.Clamp(_configManager.Config.TaskCompletionStableThresholdMinutes, 1, 1440));
        TimeSpan stableDuration = now - runtimeState.LastChangedUtc;
        if (runtimeState.HasSeenBusyState
            && stableDuration >= stableThreshold
            && (ContainsCompletionMarker(snapshot)
                || EndsWithPromptMarker(snapshot)
                || stableDuration >= stableThreshold + stableThreshold))
        {
            return new SessionStateSnapshot("Completed", "Busy markers disappeared and the latest tail stayed stable long enough.", snapshot, (int)Math.Max(0, stableDuration.TotalSeconds));
        }

        return new SessionStateSnapshot("Idle", "No active busy markers detected in the recent tail output.", snapshot, (int)Math.Max(0, stableDuration.TotalSeconds));
    }

    private static bool IsProcessAlive(TerminalSession session)
    {
        try
        {
            return session.AppProcess != null && !session.AppProcess.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private RemoteRuntimeState GetRuntimeState(TerminalSession session)
    {
        lock (_sessionLock)
        {
            if (!_runtimeStates.TryGetValue(session, out RemoteRuntimeState? state))
            {
                state = new RemoteRuntimeState();
                _runtimeStates[session] = state;
            }

            return state;
        }
    }

    private List<TerminalSession> GetSessionSnapshot()
    {
        lock (_sessionLock)
        {
            return _sessions.ToList();
        }
    }

    private TerminalSession? FindSessionByName(string? sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            return null;
        }

        lock (_sessionLock)
        {
            return _sessions.FirstOrDefault(session => string.Equals(session.Name, sessionName, StringComparison.Ordinal));
        }
    }

    private static bool ContainsBusyMarker(string snapshot)
    {
        return BusyMarkers.Any(marker => snapshot.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsCompletionMarker(string snapshot)
    {
        return CompletionMarkers.Any(marker => snapshot.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EndsWithPromptMarker(string snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return false;
        }

        string? lastNonEmptyLine = snapshot
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .LastOrDefault(line => !string.IsNullOrWhiteSpace(line));

        if (string.IsNullOrWhiteSpace(lastNonEmptyLine))
        {
            return false;
        }

        return PromptMarkers.Any(marker => string.Equals(lastNonEmptyLine, marker, StringComparison.Ordinal));
    }

    private static string NormalizeSnapshot(string snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return string.Empty;
        }

        List<string> lines = snapshot
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        return string.Join("\n", lines);
    }

    private CookieOptions BuildCookieOptions(DateTimeOffset expiresUtc)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = false,
            Path = "/",
            Expires = expiresUtc.UtcDateTime,
            MaxAge = expiresUtc - DateTimeOffset.UtcNow
        };
    }

    private static int GetSessionTimeoutMinutes(AppConfig config)
    {
        return Math.Clamp(config.RemoteSessionTimeoutMinutes, 1, 10080);
    }

    private static int GetCookieLifetimeDays(AppConfig config)
    {
        return Math.Clamp(config.RemoteCookieLifetimeDays, 1, 3650);
    }

    private void DeleteAuthCookie(HttpResponse response)
    {
        response.Cookies.Delete(AuthCookieName, new CookieOptions
        {
            Path = "/"
        });

        response.Cookies.Delete(AuthCookieName, new CookieOptions
        {
            Path = RemoteBasePath
        });
    }

    private static string BuildAuthToken(AppConfig config, RemoteAuthSession session)
    {
        RemoteAuthCookiePayload payload = new()
        {
            IdleExpiresUnixSeconds = session.IdleExpiresUtc.ToUnixTimeSeconds(),
            AbsoluteExpiresUnixSeconds = session.AbsoluteExpiresUtc.ToUnixTimeSeconds()
        };

        string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        string payloadSegment = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        byte[] signature = HMACSHA256.HashData(GetAuthSigningKey(config), Encoding.UTF8.GetBytes(payloadSegment));
        return $"{payloadSegment}.{WebEncoders.Base64UrlEncode(signature)}";
    }

    private static bool TryDecodeAuthToken(string token, AppConfig config, out RemoteAuthSession session)
    {
        session = null!;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(config.RemotePasswordHash))
        {
            return false;
        }

        string[] segments = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        try
        {
            byte[] providedSignature = WebEncoders.Base64UrlDecode(segments[1]);
            byte[] expectedSignature = HMACSHA256.HashData(GetAuthSigningKey(config), Encoding.UTF8.GetBytes(segments[0]));
            if (providedSignature.Length != expectedSignature.Length
                || !CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
            {
                return false;
            }

            string payloadJson = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(segments[0]));
            RemoteAuthCookiePayload? payload = JsonSerializer.Deserialize<RemoteAuthCookiePayload>(payloadJson, JsonOptions);
            if (payload == null)
            {
                return false;
            }

            DateTimeOffset idleExpiresUtc = DateTimeOffset.FromUnixTimeSeconds(payload.IdleExpiresUnixSeconds);
            DateTimeOffset absoluteExpiresUtc = DateTimeOffset.FromUnixTimeSeconds(payload.AbsoluteExpiresUnixSeconds);
            if (absoluteExpiresUtc < idleExpiresUtc)
            {
                return false;
            }

            session = new RemoteAuthSession
            {
                IdleExpiresUtc = idleExpiresUtc,
                AbsoluteExpiresUtc = absoluteExpiresUtc
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] GetAuthSigningKey(AppConfig config)
    {
        string secretMaterial = $"{config.RemotePasswordHash}|TerminalShellRemoteAuth|{Environment.MachineName}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(secretMaterial));
    }

    private sealed class RemoteRuntimeState
    {
        public string LastSnapshot { get; set; } = string.Empty;
        public DateTimeOffset LastChangedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? LastCommandUtc { get; set; }
        public bool HasSeenBusyState { get; set; }
        public bool IsFailed { get; set; }
        public string LastFailureKeyword { get; set; } = string.Empty;
    }

    private sealed class RemoteAuthSession
    {
        public DateTimeOffset IdleExpiresUtc { get; set; }
        public DateTimeOffset AbsoluteExpiresUtc { get; set; }
    }

    private sealed class RemoteAuthCookiePayload
    {
        public long IdleExpiresUnixSeconds { get; set; }
        public long AbsoluteExpiresUnixSeconds { get; set; }
    }

    private sealed class LoginAttemptState
    {
        public int FailedAttempts { get; set; }
        public DateTimeOffset? LockedUntilUtc { get; set; }
    }

    private sealed record SessionStateSnapshot(string Status, string StatusReason, string Snapshot, int StableSeconds);
    private sealed record LoginRequest(string Password);
    private sealed record SendCommandRequest(string Command);
    private sealed record ToggleAutoDraftRequest(bool Enabled);
}
