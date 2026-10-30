namespace Obj2x {
// 버전과 저장소. 릴리스할 때 Version을 바꾼다.
public static class AppInfo {
    public const string Version = "2.7.6";
    public const string Name = "Bus Depot";        // 화면에 보이는 이름 (v2.7.2까지는 obj2x)
    public const string Exe = "BusDepot";          // 실행 파일/설정 파일 이름
    public const string Repo = "HYB1022/obj2x";
    public static string RepoUrl { get { return "https://github.com/" + Repo; } }
}
}
