namespace wpf;

public static class RuntimeEdition
{
#if LOCAL_EDITION
    public static bool IsLocal => true;
#else
    public static bool IsLocal => false;
#endif
    public static string Name => IsLocal ? "로컬" : "인터넷";
    public static string StorageDescription => IsLocal ? "이 PC에 저장합니다" : "로그인한 Google 계정의 Sheets에 저장합니다";
}
