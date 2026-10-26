using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Sekretujo.App.Services;

public static class ProcessHardening
{
    private const int PR_SET_DUMPABLE = 4;

    private const uint DACL_SECURITY_INFORMATION = 0x4;
    private const uint SDDL_REVISION_1 = 1;
    // PROCESS_TERMINATE | PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE: Task Manager can still list and kill
    // the process, but nothing running as the same user gets PROCESS_VM_READ, thread creation or WRITE_DAC.
    private const string AllowedRights = "0x101001";

    public static void Apply()
    {
        // Both mechanisms also block a debugger from attaching or reading memory.
        if (Debugger.IsAttached) return;

        if (OperatingSystem.IsLinux())
            prctl(PR_SET_DUMPABLE, 0, 0, 0, 0);
        else if (OperatingSystem.IsWindows())
            RestrictProcessDacl();
    }

    [SupportedOSPlatform("windows")]
    private static void RestrictProcessDacl()
    {
        var userSid = WindowsIdentity.GetCurrent().User?.Value;
        if (userSid is null) return;

        // The OW (owner rights) ACE removes the owner's implicit WRITE_DAC, so the DACL can't simply be reverted.
        var sddl = $"D:P(A;;{AllowedRights};;;{userSid})(A;;{AllowedRights};;;OW)";
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, SDDL_REVISION_1, out var descriptor, IntPtr.Zero))
            return;

        try
        {
            SetKernelObjectSecurity(GetCurrentProcess(), DACL_SECURITY_INFORMATION, descriptor);
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    [DllImport("libc.so.6", SetLastError = true)]
    private static extern int prctl(int option, nuint arg2, nuint arg3, nuint arg4, nuint arg5);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string stringSecurityDescriptor, uint stringSdRevision, out IntPtr securityDescriptor, IntPtr securityDescriptorSize);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool SetKernelObjectSecurity(IntPtr handle, uint securityInformation, IntPtr securityDescriptor);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
