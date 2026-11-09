using System.Runtime.InteropServices;

namespace wpf;

/// <summary>공휴일 키는 배포 설정 대신 현재 Windows 사용자의 자격 증명 저장소에서 읽습니다.</summary>
public static class HolidayApiKeyStore
{
    public const string TargetName = "Baebulttugi/HolidayApiKey";
    public static string? ReadError { get; private set; }

    public static string Read()
    {
        ReadError=null;
        if(!CredRead(TargetName,1,0,out var pointer))
        {
            int error=Marshal.GetLastWin32Error();
            if(error!=1168)ReadError="공휴일 키 저장소를 읽지 못했습니다. 설정 도구에서 다시 등록해 주세요.";
            return "";
        }
        try
        {
            var credential=Marshal.PtrToStructure<Credential>(pointer);
            if(credential.BlobSize>5120 || credential.BlobSize%2!=0)
            { ReadError="공휴일 키 형식을 확인해 주세요.";return ""; }
            return (Marshal.PtrToStringUni(credential.Blob,(int)credential.BlobSize/2)??"").Trim();
        }
        finally { CredFree(pointer); }
    }

    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags,Type;
        public string Target,Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize;
        public IntPtr Blob;
        public uint Persist,AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias,UserName;
    }
    [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target,uint type,uint flags,out IntPtr credential);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credential);
}
