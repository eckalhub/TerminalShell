using System.Reflection;
using System.Runtime.InteropServices;

namespace TerminalShell.Services;

public sealed class SpeechPlaybackOptions
{
    public string VoiceName { get; init; } = string.Empty;
    public int Rate { get; init; }
    public int Volume { get; init; } = 100;
}

public sealed class SpeechVoiceOption
{
    public string VoiceName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public interface ITaskCompletionSpeechService : IDisposable
{
    Task SpeakAsync(string text, SpeechPlaybackOptions? options = null, CancellationToken cancellationToken = default);
}

public sealed class TaskCompletionSpeechService : ITaskCompletionSpeechService
{
    private readonly SemaphoreSlim _speakLock = new(1, 1);
    private bool _disposed;

    public async Task SpeakAsync(string text, SpeechPlaybackOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_disposed || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await _speakLock.WaitAsync(cancellationToken);
        try
        {
            await RunOnStaThreadAsync(() => SpeakCore(text, options), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Core.SimpleLogger.LogError(ex, "TaskCompletionSpeechService.SpeakAsync");
        }
        finally
        {
            _speakLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _speakLock.Dispose();
    }

    public static List<SpeechVoiceOption> GetInstalledVoices()
    {
        List<SpeechVoiceOption> voices = new()
        {
            new() { VoiceName = string.Empty, DisplayName = "[System Default]" }
        };

        Type? sapiType = Type.GetTypeFromProgID("SAPI.SpVoice");
        if (sapiType == null)
        {
            return voices;
        }

        object? voice = Activator.CreateInstance(sapiType);
        object? voiceTokens = null;

        try
        {
            if (voice == null)
            {
                return voices;
            }

            voiceTokens = sapiType.InvokeMember("GetVoices", BindingFlags.InvokeMethod, null, voice, null);
            if (voiceTokens == null)
            {
                return voices;
            }

            Type tokenCollectionType = voiceTokens.GetType();
            int count = Convert.ToInt32(tokenCollectionType.InvokeMember("Count", BindingFlags.GetProperty, null, voiceTokens, null));
            for (int i = 0; i < count; i++)
            {
                object? token = null;
                try
                {
                    token = tokenCollectionType.InvokeMember("Item", BindingFlags.InvokeMethod, null, voiceTokens, new object[] { i });
                    if (token == null)
                    {
                        continue;
                    }

                    Type tokenType = token.GetType();
                    string displayName = Convert.ToString(tokenType.InvokeMember("GetDescription", BindingFlags.InvokeMethod, null, token, null)) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        voices.Add(new SpeechVoiceOption
                        {
                            VoiceName = displayName,
                            DisplayName = displayName
                        });
                    }
                }
                finally
                {
                    ReleaseComObject(token);
                }
            }
        }
        catch (Exception ex)
        {
            Core.SimpleLogger.LogError(ex, "TaskCompletionSpeechService.GetInstalledVoices");
        }
        finally
        {
            ReleaseComObject(voiceTokens);
            ReleaseComObject(voice);
        }

        return voices;
    }

    private static Task RunOnStaThreadAsync(Action action, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.IsCancellationRequested)
        {
            tcs.TrySetCanceled(cancellationToken);
            return tcs.Task;
        }

        var thread = new Thread(() =>
        {
            try
            {
                action();
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        thread.Start();
        return tcs.Task;
    }

    private static void SpeakCore(string text, SpeechPlaybackOptions? options)
    {
        Type? sapiType = Type.GetTypeFromProgID("SAPI.SpVoice");
        if (sapiType == null)
        {
            return;
        }

        object? voice = Activator.CreateInstance(sapiType);
        object? voiceTokens = null;
        object? selectedToken = null;

        try
        {
            if (voice == null)
            {
                return;
            }

            int rate = Math.Clamp(options?.Rate ?? 0, -10, 10);
            int volume = Math.Clamp(options?.Volume ?? 100, 0, 100);
            sapiType.InvokeMember("Rate", BindingFlags.SetProperty, null, voice, new object[] { rate });
            sapiType.InvokeMember("Volume", BindingFlags.SetProperty, null, voice, new object[] { volume });

            if (!string.IsNullOrWhiteSpace(options?.VoiceName))
            {
                voiceTokens = sapiType.InvokeMember("GetVoices", BindingFlags.InvokeMethod, null, voice, null);
                selectedToken = FindMatchingVoiceToken(voiceTokens, options.VoiceName);
                if (selectedToken != null)
                {
                    sapiType.InvokeMember("Voice", BindingFlags.SetProperty, null, voice, new object[] { selectedToken });
                }
            }

            sapiType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, new object[] { text, 0 });
        }
        finally
        {
            ReleaseComObject(selectedToken);
            ReleaseComObject(voiceTokens);
            ReleaseComObject(voice);
        }
    }

    private static object? FindMatchingVoiceToken(object? voiceTokens, string targetVoiceName)
    {
        if (voiceTokens == null || string.IsNullOrWhiteSpace(targetVoiceName))
        {
            return null;
        }

        Type tokenCollectionType = voiceTokens.GetType();
        int count = Convert.ToInt32(tokenCollectionType.InvokeMember("Count", BindingFlags.GetProperty, null, voiceTokens, null));
        for (int i = 0; i < count; i++)
        {
            object? token = null;
            bool keepToken = false;

            try
            {
                token = tokenCollectionType.InvokeMember("Item", BindingFlags.InvokeMethod, null, voiceTokens, new object[] { i });
                if (token == null)
                {
                    continue;
                }

                Type tokenType = token.GetType();
                string description = Convert.ToString(tokenType.InvokeMember("GetDescription", BindingFlags.InvokeMethod, null, token, null)) ?? string.Empty;
                if (string.Equals(description, targetVoiceName, StringComparison.OrdinalIgnoreCase))
                {
                    keepToken = true;
                    return token;
                }
            }
            finally
            {
                if (!keepToken)
                {
                    ReleaseComObject(token);
                }
            }
        }

        return null;
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance != null && Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }
}
