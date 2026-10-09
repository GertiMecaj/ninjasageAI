#include <windows.h>
#include <string>
#include <cstdio>
static std::string Read(HANDLE h){char b[128]={};DWORD n=0;if(h==INVALID_HANDLE_VALUE)return "OPEN_FAILED";ReadFile(h,b,127,&n,nullptr);CloseHandle(h);return std::string(b,n);}
int wmain(int argc,wchar_t** argv){
 if(argc!=3)return 10;
 std::string expected;for(const wchar_t* p=argv[2];*p;p++)expected+=(char)*p;
 auto a=Read(CreateFileW(argv[1],GENERIC_READ,FILE_SHARE_READ,nullptr,OPEN_EXISTING,0,nullptr));
 char ansi[32768];WideCharToMultiByte(CP_ACP,0,argv[1],-1,ansi,32768,nullptr,nullptr);
 auto b=Read(CreateFileA(ansi,GENERIC_READ,FILE_SHARE_READ,nullptr,OPEN_EXISTING,0,nullptr));
 // Read-only opens redirect; writes must remain pointed at the original.
 HANDLE w=CreateFileW(argv[1],GENERIC_WRITE,FILE_SHARE_READ,nullptr,OPEN_EXISTING,0,nullptr);
 if(w==INVALID_HANDLE_VALUE)return 12;CloseHandle(w);
 printf("wide=%s ansi=%s expected=%s\n",a.c_str(),b.c_str(),expected.c_str());
 return a==expected&&b==expected?0:11;
}
