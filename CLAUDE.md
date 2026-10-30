# Bus Depot (v2.7.2까지 obj2x)

OMSI 2용 변환·제작 도구 (v2.7.2까지 이름은 obj2x). Blender에서 내보낸 `.obj`를 `.x`(Blender 2.79b) → `.o3d`(OmsiXConv)로 폴더 단위 변환한다.
사용자용 설명은 [README.md](README.md) 참고.

## 빌드 / 실행

- 빌드: `build.bat [출력 exe 경로]`. Windows에 기본 포함된 .NET Framework 4 `csc.exe`로 컴파일한다. 프로젝트 파일(.csproj), MSBuild, NuGet 없음.
  1. `src\Core\*.cs`만 따로 컴파일해 본다. UI 라이브러리 없이 빌드돼야 한다(로직/UI 분리 검사).
  2. `src\Core` + `src\App`을 `BusDepot.exe`(WPF)로 컴파일하고, `src\App\Views\*.xaml`을 `Obj2x.Views.<파일>.xaml` 리소스로 넣는다.
  3. `src\Launcher\Launcher.cs`를 `obj2x.exe`(작은 런처)로 컴파일한다. 같은 폴더의 `BusDepot.exe`를 받은 인수 그대로 띄우고 끝난다. 예전 이름의 바로 가기와 v2.7.2 이하의 자동 업데이트(zip의 `obj2x.exe`로 교체한 뒤 다시 실행)가 계속 동작하게 하려고 둔다.
- 실행: `BusDepot.bat` (exe가 없으면 먼저 빌드).
- 자체 점검: `BusDepot.exe --selftest`. 문구표, 설정, FastO3D, 엔진(중지 포함), 변환 준비, 모든 XAML 읽기, 메인 창 + ViewModel(페이지/테마/언어/마법사/면책/업데이트 표시)을 점검하고 결과를 `obj2x_selftest.txt`에 `PASS`/`FAIL` 줄로 남긴다. 코드를 바꾼 뒤 빌드하고 이것부터 돌린다.
- 스크린샷: `BusDepot.exe --shots <폴더>` (개발용, 모든 화면을 PNG로 저장). 창 내용을 직접 그려 저장하므로 가려져도 된다. 애니메이션은 꺼진 상태로 찍힌다.
- 시작 페이지 지정: `BusDepot.exe --page about` (개발용, 성능 측정 등).
- 프레임 측정: `BusDepot.exe --fps [--page about]` 또는 `--fps nav`(0.4초마다 페이지 전환)로 3초간 프레임 간격을 `BusDepot_fps.txt`에 남긴다. 창이 활성이어야 정보 화면 배경이 움직인다.
- README 화면: `BusDepot.exe --readme-shots <폴더>`로 찍어서 `docs/images/`에 넣는다. 사용자 경로가 보이는 화면은 찍지 않는다.
- 모델 보기 확인: `BusDepot.exe --view-shot <model.cfg> <png> [light|dark] [ext|int|all]`(화면 캡처), `--save-model <model.cfg> <out.blend|.obj> [blender.exe]`(저장하기를 대화 상자 없이).
- 소개 영상: `python tools/video/make_video.py --exe <BusDepot.exe> --out <폴더> [--model <cfg>] [--split]`. 대본은 `tools/video/narration.json`(장면 id = `--video-frames` 장면 이름). 음성은 edge-tts(Microsoft 온라인 음성), 언어마다 오디오/자막 트랙이 따로 들어간 MP4 하나를 만든다. 화면은 영어 UI, 작업 폴더는 사용자 경로가 안 보이게 드라이브 바로 아래 `obj2x_demo`.
- README 맨 위 움직이는 그림: `BusDepot.exe --welcome-frames <폴더> [light|dark]`(2배 해상도)로 환영 화면을 한 바퀴(13초) 초당 25장 찍고, `tools/mkwebp.py`(Pillow)로 처음과 끝을 섞어 이음매 없이 반복되는 WebP(`docs/images/welcome-light.webp`/`welcome-dark.webp`)로 만든다.

## 제약

- 라이선스: GPL-3.0-or-later (v2.2까지는 MIT). 외부 코드를 가져올 때는 GPL과 호환되는 라이선스인지 확인한다.
- **C# 5 / .NET Framework 4**: `csc.exe` v4.0.30319가 지원하는 문법만 쓴다. 문자열 보간 `$""`, `?.`, `nameof`, 식 본문 멤버(`=>`), 자동 속성 초기값, `out var`, 튜플 문법은 컴파일되지 않는다. 람다 대신 `delegate { }`를 쓰는 기존 스타일을 따른다.
- 외부 의존성 없음. 참조는 .NET Framework에 들어 있는 WPF(`PresentationFramework` 등)와 `System.IO.Compression`뿐이다. 글꼴은 `fonts/`(나눔스퀘어 네오, Noto Sans KR, 둘 다 SIL OFL 1.1 — `fonts/OFL.txt`)를 exe 옆에 두고 읽는다. 설정의 글꼴 드롭다운(`Settings.Font` = `nanum`/`noto`/`misans`/`system`)을 `Theme.MakeFont`가 받는다. MiSans는 라이선스가 글꼴 파일 재배포를 막아서 넣지 않는다. 사용자가 설치했거나 `fonts`에 넣어 둔 것만 쓰고(`Theme.MiSans()`), 없으면 나눔스퀘어 네오로 대신한다. 글자를 굵게 제대로 그리려면 `TextOptions.TextFormattingMode`는 `Ideal`이어야 한다. 릴리스 zip에도 `fonts` 폴더를 넣는다. WinForms는 쓰지 않는다(폴더 선택도 `FolderPicker`의 Windows 기본 창).
- **XAML은 실행할 때 `XamlReader`로 읽는다**(MSBuild 없이 빌드하려고). 그래서:
  - XAML 오류는 컴파일할 때 잡히지 않는다. 고친 뒤 `--selftest`의 "XAML: 모든 화면 읽기"로 확인한다.
  - `x:Class`, 이벤트 속성(`Click="..."`)을 쓸 수 없다. 동작은 ViewModel의 명령(`Command="{Binding ...}"`)으로, 화면 쪽 처리는 C# 컨트롤/첨부 속성(`ui:Behave`)으로 한다.
  - 사용자 컨트롤은 `xmlns:ui="clr-namespace:Obj2x.UI;assembly=BusDepot"`로 쓴다. 출력 exe 이름이 `BusDepot`이어야 한다. C# 네임스페이스(`Obj2x`, `Obj2x.UI`)와 리소스 이름(`Obj2x.Views.*`)은 예전 이름 그대로 둔다.
  - 문구는 `{ui:Tr 키}`, 색은 `{DynamicResource 이름}`(Theme.cs가 넣는 브러시)으로 쓴다. 스타일 키를 브러시 이름(`Card`, `Btn` 등)과 같게 지으면 브러시가 대신 잡히므로 겹치지 않게 한다.
- **성능**: 직접 그리는 컨트롤(`OnRender`)에서는 `new SolidColorBrush`/`new Pen` 대신 `Theme.Brush(색)`/`Art.Pen`/`Art.RoundPen`(캐시)을 쓴다. 애니메이션은 화면 주사율(보통 60fps)에 맞춰 `CompositionTarget.Rendering`이나 WPF 애니메이션으로 돌린다. `Rendering`은 구독만 해도 WPF가 매 프레임 다시 그리므로, 끝나면 바로 구독을 끊고(`AboutHero`) 계속 도는 것은 창이 비활성/최소화면 멈춘다(`HeroBackdrop`). 움직이는 요소는 레이아웃 속성(`Canvas.Top` 등) 대신 `RenderTransform`/`Opacity`를 애니메이션한다.
- **애니메이션 시간은 모두 개발자 옵션의 배율(`Motion.Scale`)을 따라야 한다.** C#은 `Motion.D(초)`를 쓰고, 프레임마다 도는 것은 경과 시간을 `Motion.Scale`로 나눈다. XAML에는 스토리보드(`Duration="..."`)를 쓰지 않는다. 트리거에서 `ui:Anim.Opacity`/`ui:Anim.X`/`ui:Anim.Angle`을 Setter로 바꾸면 `Effects.cs`가 배율대로 움직인다. 누름 효과(누른 지점에서 퍼지는 물결)는 템플릿에 `<ui:Ripple/>`, 직접 그리는 컨트롤은 `RippleFx`를 쓴다.
- **백신 오진 주의**: 서명이 없는 exe라서 Windows Defender의 머신러닝 진단(`...!ml`)에 걸리기 쉽다. exe가 자기 자신을 다른 폴더(특히 `%TEMP%`)에 복사해서 실행하거나 창 없이 띄우는 코드를 넣지 않는다(v2.7.1이 `Trojan:Win32/Bearfoos.A!ml`로 오진됨). 릴리스 전에 `"C:\Program Files\Windows Defender\MpCmdRun.exe" -Scan -ScanType 3 -File <exe> -DisableRemediation`으로 검사해 본다.
- 변환 스레드에서 화면으로 보내는 일은 `IUi.Post`로 보낸다. `MainWindow`가 모아서 Background 우선순위로 한 번에 처리하고, 로그도 줄을 모아 한 번에 붙인다.
- `BusDepot.py`(v2.7.2까지 `obj2x.py`)는 **Blender 2.79b**(Python 3.5)에서 `blender -b -P BusDepot.py -- <obj 폴더> <x 폴더> [작업 파일]`로 실행된다. f-string을 쓰지 않는다.

## 코드 구조

### `src/Core` (네임스페이스 `Obj2x`) — 로직. WPF/WinForms를 참조하지 않는다

| 파일 | 역할 |
| --- | --- |
| `AppInfo.cs` | 버전(`AppInfo.Version`), 저장소 |
| `Engine.cs` | 변환 엔진: Blender 실행, OmsiXConv 병렬 창 자동 조작, 중지/재시도 |
| `FastO3D.cs` | 내장 빠른 변환(실험적): obj → o3d 직접 생성 |
| `Conversion.cs` | 변환 전 검사와 준비: 도구 경로 확인, 옵션 만들기, 대상 obj 고르기 |
| `Updater.cs` | GitHub 최신 릴리스 확인, zip 받아서 exe 교체 |
| `Settings.cs` | `BusDepot.ini` 읽기/쓰기 (처음 실행할 때 `obj2x.ini`가 있으면 복사해 온다 — `Program.cs`) |
| `Strings.cs` | 문구표 `L.T("키")`와 작은 JSON 읽기/쓰기. 문구는 저장소의 `lang/<코드>.json`(ko/en/de/ja/zh)에 있고 빌드 때 exe에 들어간다. exe 옆 `lang` 폴더의 `<코드>.json`이 그 위에 덮어쓴다(새 언어 추가/일부 수정). 새 문구는 `lang/*.json` 모두에 넣는다(`--selftest`가 빠진 키와 `{0}` 불일치를 잡는다) |
| `Win32.cs` | OmsiXConv 창 찾기/조작 P/Invoke |
| `O3d.cs` | o3d 읽기(버전 1/3/5/7, 32비트 번호. 키가 `0xFFFFFFFF`가 아니면 암호화로 보고 거부 — 공식 차량은 암호화돼 있고 풀지 않는다), `ModelCfg`(model.cfg/.sco의 `[mesh]`·`[LOD]`·`[visible]`·`[viewpoint]`·`[matl_alpha]`·`[CTC]`·`[CTCTexture]`), `Repaint`(.cti: `[item]` 도색/자리/파일, `[setvar]` 변수/값 → `VisOn`), `ObjExport`(FastO3D의 반대: z 뒤집기, 면 방향 반대, v 뒤집기 → Blender에서 원래 방향) |
| `Omsi.cs` | OMSI 2: 설치 폴더 찾기(Steam), o3d(버전 1)/mtl에서 텍스처 이름 읽기, 알파 판별, 변환 전 텍스처 검사, `ScoMaker`(.sco + model/texture 복사), `SplineSpec`(.sli 프로필/경로/기본 텍스처 BMP). 형식은 OMSI 설치 폴더의 실제 파일들을 보고 맞췄다 |

### `src/App` (네임스페이스 `Obj2x.UI`) — WPF 화면

| 파일 | 역할 |
| --- | --- |
| `Program.cs` | 진입점, `--selftest`/`--shots`, 첫 실행이면 환영 화면 |
| `MainViewModel*.cs` | 화면 상태와 동작(속성/명령). 화면 컨트롤은 모르고, 대화 상자·파일 선택·알림(`Toast`)은 `IUi`로 부탁한다. `MainViewModel.Omsi.cs`는 오브젝트/스플라인 만들기, `MainViewModel.Viewer.cs`는 모델 보기(.blend 저장은 obj를 만든 뒤 `BusDepot.py --blend`. Blender는 `Settings.BlendExe` 또는 설치된 것 중 가장 새 버전 — `Omsi.FindBlenders`가 Program Files와 Steam을 찾는다. 2.79로 저장하면 2.8 이상에서 텍스처가 안 보이고 옛 화면 배치가 열린다), `MainViewModel.Dev.cs`는 개발자 옵션(정보 화면의 버전을 7번 누르면 켜짐, `Settings.DevMode`) |
| `MainWindow.cs` | Shell/페이지 XAML을 붙이고 `IUi` 구현: 페이지 전환 애니메이션, 창 크기 배율, 대화 상자. 대시보드 외 페이지는 처음 열 때 만든다 |
| `WelcomeWindow.cs` | 첫 실행 환영 화면 |
| `Views/*.xaml` | `Styles`(공통 스타일), `Shell`(샤오미 태블릿 설정 앱처럼 둘로 나눈 화면: 왼쪽 큰 제목 + 앱 카드 + 카드로 묶은 메뉴), `Dashboard`, `Wizard`, `Object`/`Spline`(OMSI 제작), `Viewer`(모델 보기), `Advanced`, `Settings`, `Dev`(개발자 옵션, HyperOS 설정 화면 모양), `About` |
| `Controls.cs`, `Hero.cs`, `Art.cs` | 직접 그리는 컨트롤(메뉴, 단계 표시, 방식 카드, 세그먼트, 숫자 상자, 스플라인 미리보기), 정보 화면(`HeroBackdrop` 그라데이션 + `AboutHero` 내용), 아이콘/로고 |
| `Viewer3D.cs` | 모델 보기: tga/dds 읽기(`TexLoad`), 부품을 3D 모델로(`ModelScene`, 다른 스레드에서 만들어 Freeze), 3D 화면(`ModelView`: 돌리기/옮기기/확대) |
| `Video.cs` | 개발용 `--video-frames`: 소개 영상 화면(장면별 PNG). 대본/음성/자막/합치기는 `tools/video/make_video.py` |
| `Effects.cs` | XAML 트리거용 애니메이션(`Anim`), 누름 물결 효과(`Ripple`, `RippleFx`) |
| `Theme.cs` | 라이트/다크 × 인디고/틸/로즈 팔레트 → 앱 리소스 브러시, 브러시 캐시, 테마 전환 크로스페이드 |
| `Dialogs.cs` | 창 안에 어둡게 깔리는 대화 상자(메시지, 면책 조항, 선택 목록) |
| `Mvvm.cs` | `Observable`, `Cmd`, `Loc`/`Tr`(문구 바인딩), `Xaml`(리소스 XAML 읽기), `UiTree`, `Pump` |
| `SelfTest.cs`, `Shots.cs` | `--selftest [결과 파일]`, `--shots`, `--readme-shots` |
| `AssemblyInfo.cs` | exe 파일 정보(이름/버전/회사/설명). 버전은 `AppInfo.Version`을 따른다 |

## 릴리스

- 버전은 `src/Core/AppInfo.cs`의 `Version`에서 바꾸고, README의 변경 내역에도 적는다.
- 자동 업데이트는 최신 릴리스의 `.zip` 첨부 파일을 받아 exe를 교체한다. 릴리스 zip `BusDepot.zip`에는 `BusDepot.exe`, `BusDepot.bat`, `BusDepot.py`, `obj2x.exe`(런처), `obj2x.bat`(BusDepot.bat을 부름), `obj2x_gui.ps1`(이 둘은 저장소의 `compat\`에 있고 zip에는 최상위에 넣는다), `LICENSE`, `fonts\`(나눔스퀘어 네오, Noto Sans KR, OFL.txt만. **MiSans는 절대 넣지 않는다** — 라이선스가 재배포를 막는다. 저장소에서도 `.gitignore`로 뺀다)를 같은 폴더(최상위)에 넣는다. zip 안 경로는 `/`로 쓴다. `lang` 폴더는 넣지 않는다(사용자가 만든 언어 파일을 덮어쓰지 않게). GPL-3.0이라 바이너리와 함께 라이선스 원문을 배포해야 한다.
  - 지금 업데이터는 zip 안의 `BusDepot.exe`(없으면 `obj2x.exe`)를 찾는다. v2.7.2 이하의 C# 업데이터는 `obj2x.exe`를, v2.0 이하의 PowerShell 업데이터는 `obj2x_gui.ps1`을 찾아 그 폴더를 덮어쓴 뒤 `obj2x.exe`/`obj2x.bat`을 실행한다. 그래서 `obj2x.exe` 런처, `obj2x.bat`, `obj2x_gui.ps1`을 빼면 예전 사용자의 업데이트가 실패한다. 저장소 이름(`AppInfo.Repo`)을 바꿔도 GitHub가 예전 주소를 새 주소로 넘겨 주므로 예전 버전의 업데이트 확인도 계속 된다.
- `BusDepot.exe`, `obj2x.exe`, `BusDepot.ini`, `obj2x.ini`는 저장소에 넣지 않는다(`.gitignore`). exe는 릴리스 zip으로만 배포한다.
