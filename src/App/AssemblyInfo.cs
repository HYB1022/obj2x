using System.Reflection;
using System.Runtime.InteropServices;

// exe의 파일 정보 (탐색기 '자세히' 탭에 보이는 이름/버전/설명). 서명이 없는 exe에 이 정보까지 없으면
// 백신이 출처를 알 수 없는 프로그램으로 보고 더 의심한다.
[assembly: AssemblyTitle("Bus Depot")]
[assembly: AssemblyDescription("OBJ to DirectX .X to OMSI .O3D batch converter (Blender 2.79b + OmsiXConv)")]
[assembly: AssemblyProduct("Bus Depot")]
[assembly: AssemblyCompany("HYB1022")]
[assembly: AssemblyCopyright("Copyright (C) HYB1022. GPL-3.0-or-later. https://github.com/HYB1022/obj2x")]
[assembly: AssemblyVersion(Obj2x.AppInfo.Version)]
[assembly: AssemblyFileVersion(Obj2x.AppInfo.Version)]
[assembly: AssemblyInformationalVersion(Obj2x.AppInfo.Version)]
[assembly: ComVisible(false)]
