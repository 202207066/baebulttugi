param([switch]$Remove)
$ErrorActionPreference = 'Stop'
# 키를 인수나 파일로 받지 않아 터미널 기록과 배포 폴더에 남기지 않습니다.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class BaebulttugiCredentialSetup {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct Credential {
        public uint Flags, Type;
        public string Target, Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize;
        public IntPtr Blob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias, UserName;
    }
    [DllImport("advapi32.dll", EntryPoint="CredWriteW", CharSet=CharSet.Unicode, SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    public static extern bool Write(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint="CredDeleteW", CharSet=CharSet.Unicode, SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    public static extern bool Delete(string target, uint type, uint flags);
}
'@
$targetName = 'Baebulttugi/HolidayApiKey'
if ($Remove) {
    if (-not [BaebulttugiCredentialSetup]::Delete($targetName,1,0) -and [Runtime.InteropServices.Marshal]::GetLastWin32Error() -ne 1168) { throw '공휴일 키를 삭제하지 못했습니다.' }
    Write-Output '현재 Windows 사용자의 공휴일 키를 삭제했습니다.'
    return
}
$secret = Read-Host '공공데이터포털의 디코딩된 서비스 키를 입력하세요' -AsSecureString
if ($secret.Length -eq 0 -or $secret.Length -gt 2560) { $secret.Dispose(); throw '키 길이를 확인해 주세요.' }
$secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToCoTaskMemUnicode($secret)
try {
    $credential = New-Object BaebulttugiCredentialSetup+Credential
    $credential.Type = 1
    $credential.Target = $targetName
    $credential.UserName = [Environment]::UserName
    $credential.BlobSize = $secret.Length * 2
    $credential.Blob = $secretPointer
    $credential.Persist = 2
    if (-not [BaebulttugiCredentialSetup]::Write([ref]$credential,0)) { throw '공휴일 키를 저장하지 못했습니다.' }
    Write-Output '현재 Windows 사용자의 자격 증명 관리자에 저장했습니다. 달력의 월을 다시 선택하면 적용됩니다.'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeCoTaskMemUnicode($secretPointer)
    $secret.Dispose()
}
