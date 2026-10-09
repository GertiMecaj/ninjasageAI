#include <windows.h>
#include <string>
#include <cstdio>
static std::string Read(HANDLE h){char b[128]={};DWORD n=0;if(h==INVALID_HANDLE_VALUE)return "OPEN_FAILED";ReadFile(h,b,127,&n,nullptr);CloseHandle(h);return std::string(b,n);}
static bool QueryAll(const WCHAR* path,bool enabled){
 char ansi[32768];WideCharToMultiByte(CP_ACP,0,path,-1,ansi,32768,nullptr,nullptr);
 WIN32_FILE_ATTRIBUTE_DATA a={},b={};
 return (GetFileAttributesW(path)!=INVALID_FILE_ATTRIBUTES)==enabled &&
        (GetFileAttributesA(ansi)!=INVALID_FILE_ATTRIBUTES)==enabled &&
        (GetFileAttributesExW(path,GetFileExInfoStandard,&a)!=FALSE)==enabled &&
        (GetFileAttributesExA(ansi,GetFileExInfoStandard,&b)!=FALSE)==enabled;
}
static void Stage(const std::wstring& dir,const WCHAR* name){
 auto path=dir+name;HANDLE f=CreateFileW(path.c_str(),GENERIC_WRITE,FILE_SHARE_READ,nullptr,CREATE_ALWAYS,0,nullptr);if(f!=INVALID_HANDLE_VALUE)CloseHandle(f);
}
static int ToggleProbe(){
 WCHAR path[32768],log[32768];
 if(!GetEnvironmentVariableW(L"NSAI_CONTROL",path,32768))return 30;
 if(!GetEnvironmentVariableW(L"NSAI_LOG",log,32768))return 31;
 std::wstring dir=log;dir.resize(dir.find_last_of(L"\\/")+1);
 if(!QueryAll(path,false))return 32;Stage(dir,L"probe.off");
 ULONGLONG end=GetTickCount64()+5000;
 while(GetTickCount64()<end&&!QueryAll(path,true))Sleep(10);
 if(!QueryAll(path,true))return 33;Stage(dir,L"probe.on");
 end=GetTickCount64()+5000;
 while(GetTickCount64()<end&&!QueryAll(path,false))Sleep(10);
 return QueryAll(path,false)?0:34;
}
int wmain(int argc,wchar_t** argv){
 bool live=argc==1;
 wchar_t env[32768];wchar_t* fallback[3]={argv[0],env,const_cast<wchar_t*>(L"PATCHED")};
 if(argc==1){if(!GetEnvironmentVariableW(L"NSAI_ORIGINAL",env,32768))return 13;argv=fallback;argc=3;}
 if(argc!=3)return 10;
 std::string expected;for(const wchar_t* p=argv[2];*p;p++)expected+=(char)*p;
 auto a=Read(CreateFileW(argv[1],GENERIC_READ,FILE_SHARE_READ,nullptr,OPEN_EXISTING,0,nullptr));
 char ansi[32768];WideCharToMultiByte(CP_ACP,0,argv[1],-1,ansi,32768,nullptr,nullptr);
 auto b=Read(CreateFileA(ansi,GENERIC_READ,FILE_SHARE_READ,nullptr,OPEN_EXISTING,0,nullptr));
 // Read-only opens redirect; writes must remain pointed at the original.
 HANDLE w=CreateFileW(argv[1],GENERIC_WRITE,FILE_SHARE_READ,nullptr,OPEN_EXISTING,0,nullptr);
 if(w==INVALID_HANDLE_VALUE)return 12;CloseHandle(w);
 printf("wide=%s ansi=%s expected=%s\n",a.c_str(),b.c_str(),expected.c_str());
 if(a!=expected||b!=expected)return 11;
 return live?ToggleProbe():0;
}
