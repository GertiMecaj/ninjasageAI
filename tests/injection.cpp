#include <windows.h>
#include <detours.h>
#include <string>
#include <cstdio>
int wmain(int argc,wchar_t** argv){
 if(argc!=5)return 20;
 SetEnvironmentVariableW(L"NSAI_ORIGINAL",argv[2]);SetEnvironmentVariableW(L"NSAI_PATCHED",argv[3]);
 std::wstring log=std::wstring(argv[3])+L".log";SetEnvironmentVariableW(L"NSAI_LOG",log.c_str());
 char dll[32768];WideCharToMultiByte(CP_ACP,0,argv[4],-1,dll,32768,nullptr,nullptr);
 std::wstring cmd=L"\""+std::wstring(argv[1])+L"\" \""+argv[2]+L"\" PATCHED";
 STARTUPINFOW si={sizeof(si)};PROCESS_INFORMATION pi={};
 if(!DetourCreateProcessWithDllExW(argv[1],cmd.data(),nullptr,nullptr,FALSE,0,nullptr,nullptr,&si,&pi,dll,nullptr)){printf("launch error %lu\n",GetLastError());return 21;}
 DWORD wait=WaitForSingleObject(pi.hProcess,15000);if(wait!=WAIT_OBJECT_0){TerminateProcess(pi.hProcess,22);return 22;}
 DWORD code;GetExitCodeProcess(pi.hProcess,&code);CloseHandle(pi.hProcess);CloseHandle(pi.hThread);return (int)code;
}
