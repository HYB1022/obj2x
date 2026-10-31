# 할 일 (v2.8.0 이후)

v2.8.0부터 Windows와 맥이 같은 앱(`app/`, Avalonia 11 + .NET 8)을 쓴다. 구조와 규칙은 [CLAUDE.md](CLAUDE.md) 참고.
빌드: `python build.py win|mac|release` (맥 zip도 Windows에서 만들 수 있다). 고친 뒤에는 꼭 `BusDepot --selftest`.

## Windows

- [ ] **다음 릴리스(v2.8.1)에 꼭 들어갈 것: 업데이터 고침** (이미 main에 있음). v2.8.0의 업데이터는 다음 업데이트 때 obj2x.exe 런처를 `obj2x.exe.old`로 비켜 두고 새 것을 넣지 않는다 → 새 버전이 시작할 때 되살린다(`Updater.CleanupOld`). v2.8.0 → 새 버전 업데이트를 실제로 시험해 볼 것
- [ ] **버스 모델·맵 타일 불러오기가 v2.7.8(WPF)보다 많이 느림** (v2.8.0 알려진 문제로 README에 적음)
  - 먼저 잴 것: `--view-shot <bus> <png>`, `--map-shot <맵> <png>`(txt에 load 시간이 남는다)로 v2.7.8과 비교. Debug 빌드가 아니라 `dist/win` 배포 exe로 잰다
  - 의심 가는 곳
    - `Observable.Raise`가 바뀔 때마다 `Cmd.Requery()`를 부름 → 읽는 동안 상태 글자가 자주 바뀌면 버튼 CanExecute를 계속 다시 물음
    - 다른 스레드에서 `ui.Post`로 상태를 너무 자주 보냄 (`map_loading`, `view_loading`)
    - 텍스처 읽기 `TexLoad.Decode`(Avalonia Bitmap → WriteableBitmap 복사), `Half`(1024 넘으면 절반 줄이기)가 관리 코드라 느림
    - `ModelScene.Build`의 `List<float>` → 배열 복사, OpenGL로 처음 올릴 때(`GlSurface.Upload`) 한꺼번에
    - `Omsi.Ansi`(.NET 8의 코드 페이지 공급자)로 cfg/map 읽기
    - 트리밍한 한 파일 exe가 처음 쓰는 코드를 JIT하는 시간 (ReadyToRun을 켜 보면 비교 가능, 대신 exe가 커짐)
- [ ] 3D에서 실제 마우스로 확인: 돌리기/옮기기/휠, 부품 누르기, 사람 길 점·좌석 끌기(Shift 1 m), 축 화살표, 손잡이 위 커서, `Ctrl+Z`/`Ctrl+Y`
- [ ] 작업 표시줄 단추의 변환 진행률 (WPF의 TaskbarItemInfo가 Avalonia에는 없음 → `ITaskbarList3` COM으로 직접)
- [ ] 화면 배율이 125%/150%인 모니터에서 글자·3D 선명도, 창 크기 배율(1.0~1.3배) 확인
- [ ] 그래픽카드가 오래돼 OpenGL(ANGLE)이 안 될 때: 3D 칸에 안내 문구 띄우기 (지금은 비어 보이기만 함. `ModelView.RendererInfo`가 `GL:`로 시작하면 실패)

## MacOS (맥북 있는 친구)

- [ ] 맥에서 빌드·실행: `python build.py mac` → `dist/BusDepot-mac-arm64.zip`(Apple 실리콘) / `-x64.zip`(Intel)
  - 처음 한 번: `xattr -dr com.apple.quarantine /Applications/BusDepot.app` → `codesign --force --deep --sign - /Applications/BusDepot.app`
  - 맥에서 직접 빌드하려면 .NET 8 SDK 설치 후 `dotnet run --project app/BusDepot.csproj`
- [ ] `/Applications/BusDepot.app/Contents/MacOS/BusDepot --selftest ~/Desktop/st.txt` → 실패 줄 고치기 (백슬래시 경로 변환은 맥에서만 실제로 돈다)
- [ ] 버스 3D(OpenGL 3.2 core)가 보이는지, 텍스처·투명 유리·애니메이션·기즈모 끌기
- [ ] 설정 'OMSI 2 / openOMSI': OMSI 2 폴더 직접 고르기, `~/.openomsi/content` 자동 인식, 인코딩(949/1252 …)으로 한글/독일어 파일 이름이 맞게 나오는지
- [ ] 만든 오브젝트/스플라인이 `content/Sceneryobjects`, `content/Splines`에 들어가고 openOMSI에서 보이는지
- [ ] 맵 탭의 없는 파일 검사가 content 폴더까지 보는지, HOF 목록
- [ ] Blender(맥용 `/Applications/Blender.app`) 찾기와 .blend 저장, `.x` 단독 변환
- [ ] 단축키가 맥답게 동작하는지 (되돌리기 `⌘Z`, 다시 실행 `⌘⇧Z`)
- [ ] 정식 서명/공증(Apple Developer 계정)을 할지, 아니면 지금처럼 직접 서명 안내로 갈지
- [ ] 맥 릴리스 여부 결정 → 하게 되면 README 5개 언어에 맥 사용법(설치, 처음 실행 명령, content 폴더)을 넣고 GitHub 릴리스에 맥 zip 추가 (Windows `BusDepot.zip`을 먼저 올린다)
