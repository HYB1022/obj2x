# obj2x

OMSI용 일괄 변환 도구. Blender에서 내보낸 `.obj`를 `.x`(Blender 2.79b) → `.o3d`(OmsiXConv)로 폴더 단위 변환한다.
사용자용 설명은 [README.md](README.md) 참고.

## 빌드 / 실행

- 빌드: `build.bat [출력 exe 경로]`. Windows에 기본 포함된 .NET Framework 4 `csc.exe`로 컴파일한다. 프로젝트 파일(.csproj), MSBuild, NuGet 없음.
  1. `src\Core\*.cs`만 따로 컴파일해 본다. UI 라이브러리 없이 빌드돼야 한다(로직/UI 분리 검사).
  2. `src\Core` + `src\App`을 `obj2x.exe`(WPF)로 컴파일하고, `src\App\Views\*.xaml`을 `Obj2x.Views.<파일>.xaml` 리소스로 넣는다.
- 실행: `obj2x.bat` (exe가 없으면 먼저 빌드).
- 자체 점검: `obj2x.exe --selftest`. 문구표, 설정, FastO3D, 엔진(중지 포함), 변환 준비, 모든 XAML 읽기, 메인 창 + ViewModel(페이지/테마/언어/마법사/면책/업데이트 표시)을 점검하고 결과를 `obj2x_selftest.txt`에 `PASS`/`FAIL` 줄로 남긴다. 코드를 바꾼 뒤 빌드하고 이것부터 돌린다.
- 스크린샷: `obj2x.exe --shots <폴더>` (개발용, 모든 화면을 PNG로 저장). 창 내용을 직접 그려 저장하므로 가려져도 된다. 애니메이션은 꺼진 상태로 찍힌다.
- 시작 페이지 지정: `obj2x.exe --page about` (개발용, 성능 측정 등).
- 프레임 측정: `obj2x.exe --fps [--page about]` 또는 `--fps nav`(0.4초마다 페이지 전환)로 3초간 프레임 간격을 `obj2x_fps.txt`에 남긴다. 창이 활성이어야 정보 화면 배경이 움직인다.
- README 화면: `obj2x.exe --readme-shots <폴더>`로 찍어서 `docs/images/`에 넣는다. 사용자 경로가 보이는 화면은 찍지 않는다.

## 제약

- 라이선스: GPL-3.0-or-later (v2.2까지는 MIT). 외부 코드를 가져올 때는 GPL과 호환되는 라이선스인지 확인한다.
- **C# 5 / .NET Framework 4**: `csc.exe` v4.0.30319가 지원하는 문법만 쓴다. 문자열 보간 `$""`, `?.`, `nameof`, 식 본문 멤버(`=>`), 자동 속성 초기값, `out var`, 튜플 문법은 컴파일되지 않는다. 람다 대신 `delegate { }`를 쓰는 기존 스타일을 따른다.
- 외부 의존성 없음. 참조는 .NET Framework에 들어 있는 WPF(`PresentationFramework` 등)와 `System.IO.Compression`뿐이다. WinForms는 쓰지 않는다(폴더 선택도 `FolderPicker`의 Windows 기본 창).
- **XAML은 실행할 때 `XamlReader`로 읽는다**(MSBuild 없이 빌드하려고). 그래서:
  - XAML 오류는 컴파일할 때 잡히지 않는다. 고친 뒤 `--selftest`의 "XAML: 모든 화면 읽기"로 확인한다.
  - `x:Class`, 이벤트 속성(`Click="..."`)을 쓸 수 없다. 동작은 ViewModel의 명령(`Command="{Binding ...}"`)으로, 화면 쪽 처리는 C# 컨트롤/첨부 속성(`ui:Behave`)으로 한다.
  - 사용자 컨트롤은 `xmlns:ui="clr-namespace:Obj2x.UI;assembly=obj2x"`로 쓴다. 출력 exe 이름이 `obj2x`여야 한다.
  - 문구는 `{ui:Tr 키}`, 색은 `{DynamicResource 이름}`(Theme.cs가 넣는 브러시)으로 쓴다. 스타일 키를 브러시 이름(`Card`, `Btn` 등)과 같게 지으면 브러시가 대신 잡히므로 겹치지 않게 한다.
- **성능**: 직접 그리는 컨트롤(`OnRender`)에서는 `new SolidColorBrush`/`new Pen` 대신 `Theme.Brush(색)`/`Art.Pen`/`Art.RoundPen`(캐시)을 쓴다. 애니메이션은 화면 주사율(보통 60fps)에 맞춰 `CompositionTarget.Rendering`이나 WPF 애니메이션으로 돌린다. `Rendering`은 구독만 해도 WPF가 매 프레임 다시 그리므로, 끝나면 바로 구독을 끊고(`AboutHero`) 계속 도는 것은 창이 비활성/최소화면 멈춘다(`HeroBackdrop`). 움직이는 요소는 레이아웃 속성(`Canvas.Top` 등) 대신 `RenderTransform`/`Opacity`를 애니메이션한다.
- 변환 스레드에서 화면으로 보내는 일은 `IUi.Post`로 보낸다. `MainWindow`가 모아서 Background 우선순위로 한 번에 처리하고, 로그도 줄을 모아 한 번에 붙인다.
- `obj2x.py`는 **Blender 2.79b**(Python 3.5)에서 `blender -b -P obj2x.py -- <obj 폴더> <x 폴더> [작업 파일]`로 실행된다. f-string을 쓰지 않는다.

## 코드 구조

### `src/Core` (네임스페이스 `Obj2x`) — 로직. WPF/WinForms를 참조하지 않는다

| 파일 | 역할 |
| --- | --- |
| `AppInfo.cs` | 버전(`AppInfo.Version`), 저장소 |
| `Engine.cs` | 변환 엔진: Blender 실행, OmsiXConv 병렬 창 자동 조작, 중지/재시도 |
| `FastO3D.cs` | 내장 빠른 변환(실험적): obj → o3d 직접 생성 |
| `Conversion.cs` | 변환 전 검사와 준비: 도구 경로 확인, 옵션 만들기, 대상 obj 고르기 |
| `Updater.cs` | GitHub 최신 릴리스 확인, zip 받아서 exe 교체 |
| `Settings.cs` | `obj2x.ini` 읽기/쓰기 |
| `Strings.cs`, `StringsData.cs`, `StringsDe.cs` | 문구표 `L.T("키")` (한국어/영어/독일어). 새 문구는 모든 언어에 추가한다(`--selftest`가 빠진 키와 `{0}` 불일치를 잡는다). 언어를 늘리려면 `L.Languages`에 한 줄, `Fill<언어>` 표, `FillExtra`의 `X(...)`와 `TrFast`에 그 언어를 넣는다 |
| `Win32.cs` | OmsiXConv 창 찾기/조작 P/Invoke |

### `src/App` (네임스페이스 `Obj2x.UI`) — WPF 화면

| 파일 | 역할 |
| --- | --- |
| `Program.cs` | 진입점, `--selftest`/`--shots`, 첫 실행이면 환영 화면 |
| `MainViewModel*.cs` | 화면 상태와 동작(속성/명령). 화면 컨트롤은 모르고, 대화 상자·파일 선택은 `IUi`로 부탁한다 |
| `MainWindow.cs` | Shell/페이지 XAML을 붙이고 `IUi` 구현: 페이지 전환 애니메이션, 사이드바 알약, 창 크기 배율, 대화 상자. 대시보드 외 페이지는 처음 열 때 만든다 |
| `WelcomeWindow.cs` | 첫 실행 환영 화면 |
| `Views/*.xaml` | `Styles`(공통 스타일), `Shell`(사이드바/상태줄), `Dashboard`, `Wizard`, `Advanced`, `Settings`, `About` |
| `Controls.cs`, `Hero.cs`, `Art.cs` | 직접 그리는 컨트롤(메뉴, 단계 표시, 방식 카드, 세그먼트, 숫자 상자), 정보 화면(`HeroBackdrop` 그라데이션 + `AboutHero` 내용), 아이콘/로고 |
| `Theme.cs` | 라이트/다크 × 인디고/틸/로즈 팔레트 → 앱 리소스 브러시, 브러시 캐시, 테마 전환 크로스페이드 |
| `Dialogs.cs` | 창 안에 어둡게 깔리는 대화 상자(메시지, 면책 조항, 선택 목록) |
| `Mvvm.cs` | `Observable`, `Cmd`, `Loc`/`Tr`(문구 바인딩), `Xaml`(리소스 XAML 읽기), `UiTree`, `Pump` |
| `SelfTest.cs`, `Shots.cs` | `--selftest`, `--shots`, `--readme-shots` |

## 릴리스

- 버전은 `src/Core/AppInfo.cs`의 `Version`에서 바꾸고, README의 변경 내역에도 적는다.
- 자동 업데이트는 최신 릴리스의 `.zip` 첨부 파일을 받아 exe를 교체한다. 릴리스 zip `obj2x.zip`에는 `obj2x.exe`, `obj2x.bat`, `obj2x.py`, `obj2x_gui.ps1`, `LICENSE`를 같은 폴더(최상위)에 넣는다. GPL-3.0이라 바이너리와 함께 라이선스 원문을 배포해야 한다.
  - C# 업데이터는 zip 안의 `obj2x.exe`를, v2.0 이하의 PowerShell 업데이터는 `obj2x_gui.ps1`을 찾아 그 폴더를 덮어쓴 뒤 `obj2x.bat`을 실행한다. `obj2x_gui.ps1`(exe 런처)을 빼면 v2.0 사용자의 업데이트가 실패한다.
- `obj2x.exe`와 `obj2x.ini`는 저장소에 넣지 않는다(`.gitignore`). exe는 릴리스 zip으로만 배포한다.
