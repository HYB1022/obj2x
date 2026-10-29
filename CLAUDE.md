# obj2x

OMSI용 일괄 변환 도구. Blender에서 내보낸 `.obj`를 `.x`(Blender 2.79b) → `.o3d`(OmsiXConv)로 폴더 단위 변환한다.
사용자용 설명은 [README.md](README.md) 참고.

## 빌드 / 실행

- 빌드: `build.bat`. Windows에 기본 포함된 .NET Framework 4 `csc.exe`로 `src\*.cs`를 `obj2x.exe`(WinForms)로 컴파일한다. 프로젝트 파일(.csproj)과 NuGet 없음.
- 실행: `obj2x.bat` (exe가 없으면 먼저 빌드).
- 자체 점검: `obj2x.exe --selftest`. 설정, 문구, 변환기, 엔진(중지 포함), 화면 구성을 점검하고 `PASS`/`FAIL` 줄을 출력한다. 코드를 바꾼 뒤 빌드하고 이것부터 돌린다.
- 스크린샷: `obj2x.exe --shots <폴더>` (개발용, 화면을 PNG로 저장).

## 제약

- 라이선스: GPL-3.0-or-later (v2.2까지는 MIT). 외부 코드를 가져올 때는 GPL과 호환되는 라이선스인지 확인한다.
- **C# 5 / .NET Framework 4**: `csc.exe` v4.0.30319가 지원하는 문법만 쓴다. 문자열 보간 `$""`, `?.`, `nameof`, 식 본문 멤버, `out var`, 튜플 문법은 컴파일되지 않는다. 람다 대신 `delegate { }`를 쓰는 기존 스타일을 따른다.
- 외부 의존성 없음. 참조는 `System.Windows.Forms.dll`, `System.Drawing.dll`뿐이다.
- `obj2x.py`는 **Blender 2.79b**(Python 3.5)에서 `blender -b -P obj2x.py -- <obj 폴더> <x 폴더> [작업 파일]`로 실행된다. f-string을 쓰지 않는다.

## 코드 구조 (`src/`, 네임스페이스 `Obj2x`)

| 파일 | 역할 |
| --- | --- |
| `Program.cs` | 진입점, `AppVersion`, `Repo`, 첫 실행 환영 화면/면책 조항 |
| `Engine.cs` | 변환 엔진: Blender 실행, OmsiXConv 병렬 창 자동 조작, 중지/재시도 |
| `FastO3D.cs` | 내장 빠른 변환(실험적): obj → o3d 직접 생성 |
| `MainForm.cs` + `Main*.cs` | `partial class MainForm`: 대시보드, 마법사, 설정, 업데이트(GitHub Releases) |
| `Settings.cs` | `obj2x.ini` 읽기/쓰기 |
| `Strings.cs`, `StringsData.cs` | 한국어/영어 문구표 `L.T("키")`. 새 문구는 ko/en 둘 다 추가한다 |
| `Ui.cs`, `Parts.cs`, `Overlays.cs`, `Smooth.cs`, `Theme.cs` | 직접 그리는 UI 컨트롤, 애니메이션, 라이트/다크 테마 |
| `Native.cs`, `Pin.cs` | Win32 P/Invoke |
| `SelfTest.cs`, `Shots.cs` | `--selftest`, `--shots` |

## 릴리스

- 버전은 `Program.AppVersion`에서 바꾸고, README의 변경 내역에도 적는다.
- 자동 업데이트는 최신 릴리스의 `.zip` 첨부 파일을 받아 exe를 교체한다. 릴리스 zip `obj2x.zip`에는 `obj2x.exe`, `obj2x.bat`, `obj2x.py`, `obj2x_gui.ps1`, `LICENSE`를 같은 폴더(최상위)에 넣는다. GPL-3.0이라 바이너리와 함께 라이선스 원문을 배포해야 한다.
  - C# 업데이터는 zip 안의 `obj2x.exe`를, v2.0 이하의 PowerShell 업데이터는 `obj2x_gui.ps1`을 찾아 그 폴더를 덮어쓴 뒤 `obj2x.bat`을 실행한다. `obj2x_gui.ps1`(exe 런처)을 빼면 v2.0 사용자의 업데이트가 실패한다.
- `obj2x.exe`와 `obj2x.ini`는 저장소에 넣지 않는다(`.gitignore`). exe는 릴리스 zip으로만 배포한다.
