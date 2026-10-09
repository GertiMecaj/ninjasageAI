using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace NinjaSageAI;
internal static class Native {
 [DllImport("NinjaSageHook.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode,ExactSpelling=true)]
 internal static extern uint LaunchGame(string exe,string original,string patched,string log,string eventName,out uint pid,out SafeProcessHandle process);
 [DllImport("kernel32.dll",SetLastError=true)] internal static extern uint WaitForSingleObject(SafeProcessHandle process,uint milliseconds);
 [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool GetExitCodeProcess(SafeProcessHandle process,out uint code);
 [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool TerminateProcess(SafeProcessHandle process,uint code);
}
