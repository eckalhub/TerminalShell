using System;
using System.Runtime.InteropServices;
using System.Threading;
using TerminalShell.Interop;

namespace TerminalShell.Core;

public sealed class TerminalProcessJob : IDisposable
{
    private static readonly Lazy<TerminalProcessJob> LazyInstance = new(() => new TerminalProcessJob());
    private readonly IntPtr _jobHandle;
    private int _disposed;

    public static TerminalProcessJob Instance => LazyInstance.Value;

    private TerminalProcessJob()
    {
        IntPtr jobHandle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
        if (jobHandle == IntPtr.Zero)
        {
            _jobHandle = IntPtr.Zero;
            return;
        }

        NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION limitInformation = new();
        limitInformation.BasicLimitInformation.LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

        int length = Marshal.SizeOf<NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        IntPtr infoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(limitInformation, infoPtr, false);
            bool configured = NativeMethods.SetInformationJobObject(
                jobHandle,
                NativeMethods.JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                infoPtr,
                (uint)length);

            if (!configured)
            {
                NativeMethods.CloseHandle(jobHandle);
                _jobHandle = IntPtr.Zero;
                return;
            }

            _jobHandle = jobHandle;
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
        }
    }

    public bool TryAddProcess(IntPtr processHandle)
    {
        if (_jobHandle == IntPtr.Zero || processHandle == IntPtr.Zero || Interlocked.CompareExchange(ref _disposed, 0, 0) != 0)
        {
            return false;
        }

        try
        {
            bool assigned = NativeMethods.AssignProcessToJobObject(_jobHandle, processHandle);
            if (!assigned)
            {
                int error = Marshal.GetLastWin32Error();
                SimpleLogger.LogError(new InvalidOperationException($"AssignProcessToJobObject failed: {error}"), nameof(TerminalProcessJob));
            }

            return assigned;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, nameof(TerminalProcessJob));
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_jobHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_jobHandle);
        }
    }
}
