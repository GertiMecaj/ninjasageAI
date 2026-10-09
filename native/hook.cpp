#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <detours.h>
#include <string>
#include <vector>
#include <algorithm>
static decltype(&CreateFileW) RealW=CreateFileW;
static decltype(&CreateFileA) RealA=CreateFileA;
static WCHAR original[32768],replacement[32768],logpath[32768];
static HMODULE selfModule;
static void Log(const char* message) {
 DWORD saved=GetLastError(),written=0;
 HANDLE f=RealW(logpath,FILE_APPEND_DATA,FILE_SHARE_READ|FILE_SHARE_WRITE,nullptr,OPEN_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);
 if(f!=INVALID_HANDLE_VALUE){WriteFile(f,message,(DWORD)strlen(message),&written,nullptr);CloseHandle(f);}
 SetLastError(saved);
}
static bool Match(LPCWSTR name,DWORD access,DWORD disposition) {
 if(!name || disposition!=OPEN_EXISTING || (access&(GENERIC_WRITE|FILE_WRITE_DATA|FILE_APPEND_DATA))) return false;
 WCHAR full[32768];DWORD n=GetFullPathNameW(name,32768,full,nullptr);
 if(!n || n>=32768)return false;
 const WCHAR* normalized=full;
 if(wcsncmp(normalized,L"\\\\?\\",4)==0)normalized+=4;
 return _wcsicmp(normalized,original)==0;
}
static HANDLE WINAPI HookW(LPCWSTR p,DWORD a,DWORD s,LPSECURITY_ATTRIBUTES sa,DWORD d,DWORD f,HANDLE t){
 if(Match(p,a,d)){HANDLE h=RealW(replacement,a,s,sa,d,f,t);Log(h==INVALID_HANDLE_VALUE?"REDIRECT_FAILED\r\n":"REDIRECTED\r\n");return h;}
 return RealW(p,a,s,sa,d,f,t);
}
static HANDLE WINAPI HookA(LPCSTR p,DWORD a,DWORD s,LPSECURITY_ATTRIBUTES sa,DWORD d,DWORD f,HANDLE t){
 WCHAR wide[32768];UINT cp=AreFileApisANSI()?CP_ACP:CP_OEMCP;
 if(p && MultiByteToWideChar(cp,0,p,-1,wide,32768)>0 && Match(wide,a,d))return HookW(wide,a,s,sa,d,f,t);
 return RealA(p,a,s,sa,d,f,t);
}
extern "C" DWORD __cdecl LaunchGame(const WCHAR* exe,const WCHAR* swf,const WCHAR* patched,const WCHAR* log,DWORD* pid){
 WCHAR dllWide[32768];if(!GetModuleFileNameW(selfModule,dllWide,32768))return GetLastError();
 char dllPath[32768];BOOL used=FALSE;
 if(!WideCharToMultiByte(CP_ACP,WC_NO_BEST_FIT_CHARS,dllWide,-1,dllPath,32768,nullptr,&used)||used)return ERROR_NO_UNICODE_TRANSLATION;
 // Build a private environment block, leaving the launcher environment untouched.
 LPWCH block=GetEnvironmentStringsW();if(!block)return GetLastError();
 std::vector<std::wstring> vars;
 for(LPWCH p=block;*p;p+=wcslen(p)+1){if(_wcsnicmp(p,L"NSAI_",5)!=0)vars.emplace_back(p);}
 FreeEnvironmentStringsW(block);
 vars.push_back(std::wstring(L"NSAI_ORIGINAL=")+swf);vars.push_back(std::wstring(L"NSAI_PATCHED=")+patched);vars.push_back(std::wstring(L"NSAI_LOG=")+log);
 std::sort(vars.begin(),vars.end(),[](const auto& a,const auto& b){return _wcsicmp(a.c_str(),b.c_str())<0;});
 std::vector<WCHAR> env;for(const auto& v:vars){env.insert(env.end(),v.begin(),v.end());env.push_back(0);}env.push_back(0);
 std::wstring cmd=L"\""+std::wstring(exe)+L"\"",dir=exe;size_t slash=dir.find_last_of(L"\\/");if(slash==std::wstring::npos)return ERROR_INVALID_NAME;dir.resize(slash);
 STARTUPINFOW si={sizeof(si)};PROCESS_INFORMATION pi={};
 if(!DetourCreateProcessWithDllExW(exe,cmd.data(),nullptr,nullptr,FALSE,CREATE_SUSPENDED|CREATE_UNICODE_ENVIRONMENT,env.data(),dir.c_str(),&si,&pi,dllPath,nullptr))return GetLastError();
 if(ResumeThread(pi.hThread)==(DWORD)-1){DWORD e=GetLastError();TerminateProcess(pi.hProcess,e);CloseHandle(pi.hThread);CloseHandle(pi.hProcess);return e;}
 *pid=pi.dwProcessId;CloseHandle(pi.hThread);CloseHandle(pi.hProcess);return 0;
}
BOOL WINAPI DllMain(HINSTANCE h,DWORD reason,LPVOID){
 if(DetourIsHelperProcess())return TRUE;
 if(reason==DLL_PROCESS_ATTACH){
  selfModule=h;DisableThreadLibraryCalls(h);DetourRestoreAfterWith();
  DWORD n=GetEnvironmentVariableW(L"NSAI_ORIGINAL",original,32768);
  if(!n)return TRUE; // Loaded as the launcher's native bridge, without hooks.
  if(n>=32768)return FALSE;
  n=GetEnvironmentVariableW(L"NSAI_PATCHED",replacement,32768);if(!n||n>=32768)return FALSE;
  n=GetEnvironmentVariableW(L"NSAI_LOG",logpath,32768);if(!n||n>=32768)return FALSE;
  LONG e=DetourTransactionBegin();if(e!=NO_ERROR)return FALSE;
  e=DetourUpdateThread(GetCurrentThread());
  if(e==NO_ERROR)e=DetourAttach(&(PVOID&)RealW,HookW);
  if(e==NO_ERROR)e=DetourAttach(&(PVOID&)RealA,HookA);
  if(e!=NO_ERROR){DetourTransactionAbort();Log("HOOK_FAILED\r\n");return FALSE;}
  e=DetourTransactionCommit();if(e!=NO_ERROR){Log("HOOK_FAILED\r\n");return FALSE;}
  Log("HOOK_READY\r\n");
 }
 return TRUE;
}
