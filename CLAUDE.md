# Bus Depot (v2.7.2까지 obj2x)

OMSI 2용 변환·제작 도구 (v2.7.2까지 이름은 obj2x). Blender에서 내보낸 `.obj`를 `.x`(Blender 2.79b) → `.o3d`(OmsiXConv)로 폴더 단위 변환한다.
사용자용 설명은 [README.md](README.md) 참고.

Windows와 macOS가 **같은 앱 하나**(`app/`, Avalonia 11 + .NET 8)를 쓴다. 화면·동작·3D(OpenGL)가 같고, OmsiXConv 변환과 자동 업데이트만 Windows 전용이다.
(v2.7.8까지 Windows는 WPF, 맥은 따로 만든 간이 버전이었다.)

## 폴더

- `app/` — 앱 (네임스페이스 `Obj2x.UI`): 화면(`Views/*.axaml`), ViewModel(`ViewModels/`), 직접 그리는 컨트롤, 3D(`Gl/`), 개발용 도구
- `shared/` — `Core/`(로직, 네임스페이스 `Obj2x`), `lang/`(문구), `fonts/`(글꼴, exe 안에 들어감), `BusDepot.py`(Blender 스크립트)
- `windows/` — `Launcher/`(obj2x.exe 런처), `compat/`(예전 업데이터용 obj2x.bat, obj2x_gui.ps1), `tools/`(소개 영상, README 움직이는 그림), `BusDepot.bat`, `BusDepot.ico`
- `mac/` — `make_app.py`(publish 결과를 BusDepot.app으로 묶어 zip), `BusDepot.icns`
- `build.py` — 빌드와 릴리스 zip
- `docs/images/` — README 그림 (한국어는 바로 아래, 다른 언어는 `<언어>/`)
- `useless/` — 지울 것을 모아 두는 곳 (사용자가 직접 확인하고 지운다, git에 안 들어감)

## 빌드 / 실행

- 필요한 것: .NET 8 SDK(이 PC는 `C:\Program Files\dotnet`), Python 3. NuGet 패키지는 Avalonia 11.2.3(MIT)뿐.
- 개발 중: `dotnet build app/BusDepot.csproj` → `app/bin/Debug/net8.0/BusDepot.exe`. 설정 파일은 exe 옆 `BusDepot.ini`(Windows) / `~/Library/Application Support/BusDepot/BusDepot.ini`(맥).
- 배포 빌드: `python build.py release` → `dist/BusDepot.zip`(Windows), `dist/BusDepot-mac-arm64.zip`, `dist/BusDepot-mac-x64.zip`. `python build.py win|mac`으로 한쪽만. 맨 위의 `build_test.bat`은 release 빌드 후 `python build.py test`(배포 exe `--selftest` + 맥 zip 구성·실행 권한·CPU 종류·버전 점검)까지 한다.
  - Release 설정(csproj): .NET을 함께 넣은 한 파일 exe, 미리 컴파일(ReadyToRun: exe가 약 11 MB 커지지만 끄지 말 것 — 없으면 처음 도는 맵·모델 읽기 코드가 최적화 안 된 채로 돌아 v2.8.0처럼 몇 배 느려진다), 트리밍(partial: 우리 코드는 그대로), 압축. 네이티브 dll(Skia, HarfBuzz, ANGLE / 맥은 dylib)은 exe 옆에 둔다. 실행할 때 임시 폴더에 풀어 쓰는 방식은 백신 오진 위험 때문에 쓰지 않는다.
  - obj2x.exe 런처는 Windows에 들어 있는 .NET Framework `csc.exe`로 `windows/Launcher/Launcher.cs`를 컴파일한다 (build.py가 한다). 릴리스 zip에만 넣고(예전 업데이터용), 앱은 시작할 때 지운다.
  - 맥 zip은 Windows에서도 만든다 (`mac/make_app.py`: Info.plist, 아이콘, 실행 권한 755, 처음 실행 안내). 서명이 없어서 맥에서 처음 한 번 `xattr -dr com.apple.quarantine`와 `codesign --force --deep --sign -`가 필요하다(Apple 실리콘은 서명 없이는 실행되지 않음).
- 자체 점검: `BusDepot --selftest [결과 파일]`. 문구표, 설정, FastO3D, 엔진(중지 포함), 변환 준비, o3d/cfg/도색/스크립트/버스 cfg/맵/HOF, 텍스처, 글꼴, 인코딩, 3D 시선 검사, 모든 화면 읽기, 메인 창 + ViewModel(페이지/테마/언어/마법사/면책/개발자 옵션/업데이트 표시)을 점검하고 `PASS`/`FAIL` 줄로 남긴다. **코드를 바꾼 뒤 빌드하고 이것부터 돌린다.** 트리밍한 배포 exe(`dist/win/BusDepot.exe`)로도 돌려 본다.
- 스크린샷: `BusDepot --shots <폴더>` (개발용, 모든 화면을 PNG로). 창 내용을 직접 그려 저장하므로 가려져도 된다. 애니메이션은 꺼진 상태로 찍힌다. OpenGL 3D는 따로 읽어 겹친다(`ModelView.Snapshot`).
- 배포글 화면: `BusDepot --cafe-shots <폴더> <OMSI 폴더>`. 데모 버스(앞문 애니메이션·사람 길·좌석, `Video.CafeBus`), 직접 만든 정류장 오브젝트(`Video.CafeShelter`), OMSI 기본 맵 Grundorf와 거기 쓰인 기본 스플라인으로 화면별 PNG를 찍는다. 남의 모드는 쓰지 않고, OMSI 기본 오브젝트는 o3d가 암호화돼 있어 열 수 없다.
- 시작 페이지 지정: `BusDepot --page about` (개발용).
- 프레임 측정: `BusDepot --fps [--page about]` 또는 `--fps nav`(0.4초마다 페이지 전환)로 3초간 프레임 간격을 `BusDepot_fps.txt`에 남긴다.
- README 화면: `BusDepot --readme-shots <폴더> [ko|en|de|ja|zh]`로 찍어서 `docs/images/`(한국어)나 `docs/images/<언어>/`에 넣는다. 사용자 경로가 보이는 화면은 찍지 않는다.
- 버스/맵 확인: `BusDepot --view-shot <model.cfg|.bus|.sco|.sli> <png> [light|dark] [ext|int|all|pose|around|paths|obj]`(버스 탭 캡처, 3D 포함. 옆 txt에 부품 목록, OpenGL 정보, 읽기 시간과 `Prof`(o3d/텍스처 누적 시간)), `--map-shot <맵 폴더> <png> [hof]`(txt에 읽기 시간, 화면 멈춤, 지도 한 번 그리는 시간), `--save-model <model.cfg> <out.blend|.obj> [blender]`.
- 아이콘: `--make-icon <ico>`(windows/BusDepot.ico), `--make-icns <icns>`(mac/BusDepot.icns). 둘 다 `Art.Logo`로 그린다.
- 글꼴 확인: `--fonttest <txt>` (보통/굵게가 실제로 어떤 글꼴로 잡히는지).
- 소개 영상: `python windows/tools/video/make_video.py --exe <BusDepot.exe> --out <폴더> [--model <cfg>] [--split]`. 대본은 `windows/tools/video/narration.json`(장면 id = `--video-frames` 장면 이름). 음성은 edge-tts, 언어마다 오디오/자막 트랙이 따로 들어간 MP4 하나. 화면은 영어 UI, 작업 폴더는 드라이브 바로 아래 `BusDepot_demo`.
- README 맨 위 움직이는 그림: `BusDepot --welcome-frames <폴더> [light|dark] [언어]`(2배 해상도)로 환영 화면을 한 바퀴 초당 25장 찍고, `windows/tools/mkwebp.py`(Pillow)로 이음매 없이 반복되는 WebP로 만든다.

## 맥 (openOMSI)

- 지원: 빠른 변환(FastO3D), Blender `.x` 단독(맥용 Blender가 있으면), 버스(3D 포함 전부), 맵, 오브젝트/스플라인 만들기, 설정, 정보, 개발자 옵션. OmsiXConv(Windows 프로그램, 창 자동 조작)가 필요한 DirectX → O3D 방식은 꺼진다(`Conversion.ConvOk`는 Windows에서만 참). 자동 업데이트는 릴리스 페이지를 연다.
- OMSI 2 폴더는 **자동으로 찾지 않는다**: Windows에서 설치한 폴더를 맥으로 그대로 가져와 쓰므로 위치가 정해져 있지 않다. 설정의 'OMSI 2 / openOMSI' 칸(맥에서만 보임)에서 고른다. `Omsi.exe`가 없어도 `Vehicles`나 `maps`가 있으면 OMSI 폴더로 본다(`Platform.IsOmsi`).
- openOMSI 모드 설치기는 모드를 `~/.openomsi/content`에 OMSI 폴더 구조 그대로(`Vehicles`, `Sceneryobjects`, `Splines`, `maps` …) 풀어 넣는다. 이것을 content 폴더(`Settings.ModsDir`)로 자동으로 잡고, 맵의 없는 파일 검사·HOF 목록에 함께 쓰며(`MapInfo.ExtraRoots`), 만든 오브젝트/스플라인도 여기의 `Sceneryobjects`/`Splines`에 넣는다(`Platform.MakeRoot`). openOMSI는 OMSI 2 폴더에 쓰지 않는다.
- OMSI 파일 속 `\` 경로는 `Omsi.Rel`로 바꾼다(Windows에서는 그대로). 글자 인코딩은 `Omsi.Ansi`: Windows는 시스템 ANSI 코드 페이지(`Program.Main`), 맥은 설정의 코드 페이지(`Settings.AnsiCp`, 0 = 언어에 맞춰 949/932/936/1252).
- 맥 전용 문구는 `mac_*` 키로 `shared/lang/*.json` 모두에 넣는다.
- 백슬래시 경로 변환은 맥에서만 실제로 일어나므로 맥에서도 `--selftest`를 돌려 본다 (`/Applications/BusDepot.app/Contents/MacOS/BusDepot --selftest ~/Desktop/st.txt`).

## 제약

- 라이선스: GPL-3.0-or-later (v2.2까지는 MIT). 외부 코드를 가져올 때는 GPL과 호환되는 라이선스인지 확인한다. Avalonia는 MIT.
- **Core(`shared/Core`)는 C# 5 문법을 유지한다** (간단한 로직 코드라 최신 문법이 필요 없고, 예전 버전과 비교하기 쉽게). `app/`은 최신 C#을 쓴다.
- 글꼴: 나눔스퀘어 네오, Noto Sans KR(둘 다 SIL OFL 1.1 — `fonts/OFL.txt`)을 exe 안에 넣고(AvaloniaResource) `avares://BusDepot/Assets/Fonts#이름`으로 쓴다. Avalonia는 avares 글꼴과 시스템 글꼴을 한 목록에 섞지 못하므로 함께 넣은 글꼴은 하나만 주고, 없는 글자는 Avalonia가 시스템 글꼴에서 찾는다. 중국어/일본어는 그 언어의 시스템 글꼴을 쓴다(`Look.MakeFont`). 설정의 글꼴 드롭다운(`Settings.Font` = `nanum`/`noto`/`misans`/`system`). **MiSans는 라이선스가 재배포를 막아서 절대 넣지 않는다** (사용자가 시스템에 설치한 것만 쓰고, 없으면 나눔스퀘어 네오).
- **XAML(`.axaml`)은 빌드할 때 컴파일된다** (x:Class 없이 `Xaml.Load("이름")` → `AvaloniaXamlLoader`). 그래도 바인딩은 대부분 리플렉션이라 이름이 틀리면 실행해야 드러난다 → `--selftest`, `--shots`로 확인.
  - WPF의 키 스타일 = Avalonia `ControlTheme`. 같은 키 이름으로 `Theme="{StaticResource BtnNormal}"` (`Views/Styles.axaml`).
  - 상태에 따른 모양은 스타일 선택자: `^:pointerover /template/ Border#hov`, `^[Tag=on]`, 데이터에 따른 것은 `Classes.이름="{Binding !Exists}"` + 화면의 `<X.Styles>`. 로컬 값은 스타일보다 세서, 스타일로 바꿀 속성은 로컬 값으로 주지 않는다.
  - 드롭다운의 고른 값은 `ui:Behave.Value="{Binding X}"`(항목의 Code와 맞춤, 목록을 다시 채워도 다시 고름), 표시 글자는 `DisplayMemberBinding="{Binding Name}"`.
  - 문구는 `{ui:Tr 키}`, 색은 `{DynamicResource 이름}`(`Look.cs`가 넣는 브러시).
  - 색 팔레트 정적 클래스 이름은 `Look` (Avalonia 컨트롤의 `Theme` 속성과 겹치지 않게).
- 파일/폴더 고르기는 비동기다: `IUi.BrowseFolder(start, done)`, `PickFile(title, filter, start, done)`, `SaveFile(name, filter, done)`. 클립보드는 `IUi.Copy`.
- .NET 8에서 `Encoding.Default`는 UTF-8이다. OMSI 파일은 항상 `Omsi.Ansi`로 읽고 쓴다. 실행 파일 경로는 `Environment.ProcessPath`(`Assembly.Location`은 한 파일 exe에서 비어 있다).
- **성능**: 직접 그리는 컨트롤(`Render`)에서는 `new SolidColorBrush`/`new Pen` 대신 `Look.Brush(색)`/`Art.Pen`/`Art.RoundPen`(캐시)을 쓴다. 프레임마다 도는 것은 `TopLevel.RequestAnimationFrame`, 끝나면 다시 요청하지 않는다. 계속 도는 것은 창이 비활성/최소화면 멈춘다(`HeroBackdrop`). 움직이는 요소는 `RenderTransform`/`Opacity`를 바꾼다.
- **애니메이션 시간은 모두 개발자 옵션의 배율(`Motion.Scale`)을 따라야 한다.** 값을 움직일 때는 `Tween.Run(주인, 이름, 시작, 끝, Motion.Sec(초), set)`, 프레임마다 도는 것은 경과 시간을 `Motion.Scale`로 나눈다. XAML에는 Avalonia 애니메이션/트랜지션을 쓰지 않는다. 스타일에서 `ui:Anim.Opacity`/`ui:Anim.X`/`ui:Anim.Angle`을 Setter로 바꾸면 `Effects.cs`가 배율대로 움직인다. 누름 효과는 템플릿에 `<ui:Ripple/>`, 직접 그리는 컨트롤은 `RippleFx`.
  - Avalonia `DispatcherTimer(TimeSpan, priority, callback)` 생성자는 바로 시작한다. 시계를 맞춰야 하는 타이머는 `new DispatcherTimer(priority) { Interval = … }` + `Tick +=`로 만든다.
- **3D(`app/Gl/`)**: `Scene3D.cs`가 WPF Media3D와 같은 이름의 자료(Point3D/Vector3D/Rect3D/Matrix3D(행 벡터), Model3DGroup/GeometryModel3D(Handle = 끌 수 있는 손잡이), Material, TexLoad(RGBA), AnimMath, Overlay3D)를 만들고, `ModelView.cs`(Avalonia `OpenGlControlBase`)가 그린다. Windows는 ANGLE(OpenGL ES 3.0), 맥은 OpenGL 3.2 core → 셰이더 머리말만 다르다. 4배 멀티샘플, WPF와 같은 조명(주변광 120 + 시선 방향 150 + (0.4,-1,0.3) 방향 70)과 가로 화각 40도. 투명 재질은 불투명한 것을 모두 그린 뒤에. 부품은 여러 코어로 함께 만들고(`Parallel.For`), 텍스처는 모델을 먼저 보여 준 뒤 `ModelScene.LoadTextures`가 뒤에서 풀어 채운다(`Material.TexPath` → `Texture`, `ModelScene.TexturesChanged`로 다시 그림, 스크린샷·점검은 `ModelScene.Pending`이 0이 될 때까지 기다린다). 큰 dds는 맞는 밉맵만, bmp/png/jpg는 Skia로 줄여서 푼다. 부품 고르기/손잡이 끌기는 CPU 시선 검사(`Hit`). GL 함수는 `GlApi.cs`가 GetProcAddress로 읽는다(두 쪽 모두에 있는 함수만).
- **백신 오진 주의**: 서명이 없는 exe라서 Windows Defender의 머신러닝 진단(`...!ml`)에 걸리기 쉽다. exe가 자기 자신을 다른 폴더(특히 `%TEMP%`)에 복사해서 실행하거나 창 없이 띄우는 코드를 넣지 않는다(v2.7.1이 `Trojan:Win32/Bearfoos.A!ml`로 오진됨). 한 파일 exe에서 네이티브 dll을 임시 폴더에 푸는 설정(`IncludeNativeLibrariesForSelfExtract`)도 쓰지 않는다. 릴리스 전에 `"C:\Program Files\Windows Defender\MpCmdRun.exe" -Scan -ScanType 3 -File <exe> -DisableRemediation`으로 검사해 본다.
- 변환 스레드에서 화면으로 보내는 일은 `IUi.Post`로 보낸다. `MainWindow`가 모아서 Background 우선순위로 한 번에 처리한다.
- `BusDepot.py`(v2.7.2까지 `obj2x.py`)는 **Blender 2.79b**(Python 3.5)에서 `blender -b -P BusDepot.py -- <obj 폴더> <x 폴더> [작업 파일]`로 실행된다. f-string을 쓰지 않는다.

## 코드 구조

### `shared/Core` (네임스페이스 `Obj2x`) — 로직. UI를 참조하지 않는다

| 파일 | 역할 |
| --- | --- |
| `AppInfo.cs` | 버전(`AppInfo.Version`, exe 파일 정보도 이것을 따른다), 저장소 |
| `Engine.cs` | 변환 엔진: Blender 실행, OmsiXConv 병렬 창 자동 조작(Windows), 중지/재시도 |
| `ObjList.cs` | obj 목록, 결과 줄, 실행 옵션 |
| `FastO3D.cs` | 내장 빠른 변환(실험적): obj → o3d 직접 생성 |
| `Conversion.cs` | 변환 전 검사와 준비: 도구 경로 확인(맥은 `.exe` 없는 Blender 허용, OmsiXConv는 Windows만), 옵션 만들기, 대상 obj 고르기 |
| `Updater.cs` | GitHub 최신 릴리스 확인(Windows용 `BusDepot.zip`을 고름), zip 받아서 교체(`Apply`). 실행 중인 exe/dll은 `.old`로 비켜 두고 새 파일을 넣는다(다음 시작 때 지움). 예전 업데이터용 obj2x 파일은 깔지 않고, `CleanupObj2x`가 obj2x 파일(런처 포함)을 모두 지운다 |
| `Settings.cs` | `BusDepot.ini` 읽기/쓰기 (처음 실행할 때 `obj2x.ini`가 있으면 복사해 온다 — `Platform.LoadSettings`). `ModsDir`, `AnsiCp`는 맥 전용 |
| `Strings.cs` | 문구표 `L.T("키")`와 작은 JSON 읽기/쓰기. 문구는 `shared/lang/<코드>.json`(ko/en/de/ja/zh)에 있고 빌드 때 exe에 들어간다. exe 옆 `lang` 폴더의 `<코드>.json`이 그 위에 덮어쓴다. 새 문구는 `shared/lang/*.json` 모두에 넣는다(`--selftest`가 빠진 키와 `{0}` 불일치를 잡는다) |
| `Win32.cs` | OmsiXConv 창 찾기/조작 P/Invoke (Windows) |
| `O3d.cs` | o3d 읽기(버전 1/3/5/7, 32비트 번호. 키가 `0xFFFFFFFF`가 아니면 암호화로 보고 거부 — 공식 차량은 암호화돼 있고 풀지 않는다), `ModelCfg`, `Repaint`(.cti), `ObjExport` |
| `BusFiles.cs` | 버스 탭 파일: `CfgDoc`(줄 단위로 값만 바꾸고 들여쓰기 유지, ANSI 저장, 처음 저장할 때 `.bak`), `AnimBlock`, `BusFile`, `PathsCfg`, `CabinCfg` |
| `OmsiScript.cs` | 버스 탭용 차량 스크립트 실행기([visible] 변수 계산) |
| `Omsi.cs` | OMSI 2: 설치 폴더 찾기(Windows Steam), `Ansi`/`IsWindows`/`Rel`, `Open`/`OpenText`(운영체제 기본 프로그램), Blender 찾기(Program Files, Steam, 맥 `/Applications`), 텍스처, `ScoMaker`, `SplineSpec`, `SliFile` |
| `MapData.cs` | 맵 탭: `MapInfo.Load`(global.cfg UTF-16, 타일, 없는 오브젝트/스플라인/AI/사람, `ExtraRoots`), `HofFile` |

### `app/` (네임스페이스 `Obj2x.UI`) — 화면 (Avalonia)

| 파일 | 역할 |
| --- | --- |
| `Program.cs` | 진입점(코드 페이지, OpenGL 렌더링 설정), `App`(FluentTheme + Styles.axaml), 개발용 명령, 첫 실행이면 환영 화면 |
| `Platform.cs` | 운영체제마다 다른 것: 설정 파일 위치, 인코딩, openOMSI content 폴더, 만들기 위치, OMSI 폴더 판별 |
| `ViewModels/MainViewModel*.cs` | 화면 상태와 동작(속성/명령). 화면 컨트롤은 모르고, 대화 상자·파일 선택·알림은 `IUi`로 부탁한다. `.Omsi`(오브젝트/스플라인 만들기), `.Viewer`(버스 탭 모델 읽기/도색/부품/저장), `.Bus`(애니메이션, 블록 편집, paths/cabin, 기즈모, 되돌리기), `.Preview`(.sco/.sli 열어 보기), `.Map`, `.Dev`(개발자 옵션, 정보 화면의 버전을 7번 누르면 켜짐), `.Mac`(content 폴더, 인코딩) |
| `MainWindow.cs` | Shell/페이지 XAML을 붙이고 `IUi` 구현: 페이지 전환, 창 크기 배율(`LayoutTransformControl`), 대화 상자, 토스트, 끌어다 놓기, 테마 크로스페이드 |
| `WelcomeWindow.cs` | 첫 실행 환영 화면 |
| `Views/*.axaml` | `Styles`(공통 ControlTheme), `Shell`(왼쪽 큰 제목 + 앱 카드 + 카드로 묶은 메뉴), `Dashboard`, `Wizard`, `Object`/`Spline`, `Bus`, `Map`, `Advanced`, `Settings`(맥에서만 'OMSI 2 / openOMSI' 칸), `Dev`, `About` |
| `Controls.cs`, `Hero.cs`, `Art.cs`, `MapView.cs` | 직접 그리는 컨트롤(메뉴, 단계 표시, 방식 카드, 세그먼트, 숫자 상자, 상태 점, 스플라인 미리보기), `Behave`(버튼 모양, 폴더 끌어 놓기, 드롭다운 값), 정보 화면(`HeroBackdrop` + `AboutHero`), 아이콘/로고(.ico/.icns), 맵 지도 `MapCanvas` |
| `Gl/` | 3D: `Scene3D.cs`(자료·텍스처·애니메이션 계산·덧그림), `ModelView.cs`(OpenGL 화면, 시선 검사), `GlApi.cs`(GL 함수) |
| `Effects.cs` | `Tween`(배율을 따르는 값 움직임), `Anim`(스타일용), `RoundClip`, 누름 물결(`Ripple`, `RippleFx`) |
| `Look.cs` | 라이트/다크 × 인디고/틸/로즈 팔레트 → 앱 리소스 브러시, 브러시 캐시, 글꼴, `Motion` |
| `Dialogs.cs` | 창 안에 어둡게 깔리는 대화 상자(메시지, 면책 조항, 선택 목록), `Res`(코드에서 ControlTheme/DynamicResource 붙이기) |
| `Mvvm.cs` | `Observable`, `Cmd`(+`Cmd.Requery`), `Loc`/`Tr`(문구 바인딩), `Xaml`, `UiTree`, `Pump`(메시지 루프 돌리기), `FileFonts` |
| `SelfTest.cs`, `Shots.cs`, `Video.cs`, `DevTools.cs` | `--selftest`, 스크린샷류, 소개 영상 프레임·데모 모델, 개발용 명령줄 |

## 릴리스

- 버전은 `shared/Core/AppInfo.cs`의 `Version`에서 바꾸고, README의 변경 내역에도 적는다.
- `python build.py release`로 zip 3개를 만든다. GitHub 릴리스에는 **`BusDepot.zip`(Windows)을 먼저 올린다** (예전 업데이터는 첫 번째 `.zip` 첨부를 받는다). 그다음 맥 zip 둘.
- Windows zip `BusDepot.zip`(모두 최상위, zip 안 경로는 `/`): `BusDepot.exe`, 네이티브 dll 3개, `BusDepot.py`, `fonts/OFL.txt`, `LICENSE`, `BusDepot.bat`, `obj2x.exe`(런처), `obj2x.bat`, `obj2x_gui.ps1`. **MiSans는 절대 넣지 않는다.** `lang` 폴더는 넣지 않는다(사용자가 만든 언어 파일을 덮어쓰지 않게). GPL-3.0이라 바이너리와 함께 라이선스 원문을 배포해야 한다.
  - 지금 업데이터는 zip 안의 `BusDepot.exe`(없으면 `obj2x.exe`)를 찾아 그 폴더를 통째로 복사한다(obj2x.exe·obj2x.bat·obj2x_gui.ps1은 빼고). 앱은 v2.8.1부터 시작할 때 obj2x 파일을 모두 지운다. v2.7.2 이하의 C# 업데이터는 `obj2x.exe`를, v2.0 이하의 PowerShell 업데이터는 `obj2x_gui.ps1`을 찾아 그 폴더를 덮어쓴 뒤 `obj2x.exe`/`obj2x.bat`을 실행한다. 그래서 `obj2x.exe` 런처, `obj2x.bat`, `obj2x_gui.ps1`을 빼면 예전 사용자의 업데이트가 실패한다. 저장소 이름(`AppInfo.Repo`)을 바꿔도 GitHub가 예전 주소를 새 주소로 넘겨 준다.
- 릴리스 전에 Defender로 `dist/win/BusDepot.exe`와 `obj2x.exe`를 검사한다.
- `BusDepot.exe`, `obj2x.exe`, `BusDepot.ini`, `obj2x.ini`, `dist/`는 저장소에 넣지 않는다(`.gitignore`).
