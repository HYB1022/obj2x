# 할 일 (v2.8.0 이후)

v2.8.0부터 Windows와 맥이 같은 앱(`app/`, Avalonia 11 + .NET 8)을 쓴다. 구조와 규칙은 [CLAUDE.md](CLAUDE.md) 참고.
빌드: `python build.py win|mac|release` (맥 zip도 Windows에서 만들 수 있다). 고친 뒤에는 꼭 `BusDepot --selftest`.

## Windows

- [x] v2.8.1 (2026-11-01 릴리스): 업데이터 고침, obj2x 파일 모두 지우기, 버스 모델 열기 빠르게(부품 병렬 + 텍스처는 뒤에서, ReadyToRun)
  - [x] v2.8.0 → v2.8.1 업데이트 확인 (v2.8.0 업데이터의 파일 단계를 GitHub zip으로 따라 한 뒤 v2.8.1을 한 번 실행 → obj2x 파일·.old 모두 지워지고 설정은 BusDepot.ini로). v2.7.8 → v2.8.0도 실제 업데이터로 확인함
  - [ ] 텍스처를 뒤에서 푸는 동안 화면을 계속 다시 그리면 DispatcherTimer(WM_TIMER)가 밀린다. 0.5~1초 정도라 그대로 뒀음 (스크린샷 도구의 '모델 보임' 시간이 늦게 찍히는 이유)
  - [x] 맵이 느렸던 원인: v2.8.0 exe에 미리 컴파일(ReadyToRun)이 없어서 처음 도는 읽기 코드가 느렸음 (안락문산: v2.8.0 2.9~5.4초·화면 멈춤 최대 2.2초 → v2.8.1 0.7~1.0초). `--map-shot` txt에 한 번 그리는 시간(frame full/zoom8)도 남긴다
  - ReadyToRun으로 zip이 22 MB → 33 MB. 속도의 주된 원인이라 끄지 말 것
- [ ] 3D에서 실제 마우스로 확인: 돌리기/옮기기/휠, 부품 누르기, 사람 길 점·좌석 끌기(Shift 1 m), 축 화살표, 손잡이 위 커서, `Ctrl+Z`/`Ctrl+Y`
- [ ] 작업 표시줄 단추의 변환 진행률 (WPF의 TaskbarItemInfo가 Avalonia에는 없음 → `ITaskbarList3` COM으로 직접)
- [ ] 화면 배율이 125%/150%인 모니터에서 글자·3D 선명도, 창 크기 배율(1.0~1.3배) 확인
- [ ] 그래픽카드가 오래돼 OpenGL(ANGLE)이 안 될 때: 3D 칸에 안내 문구 띄우기 (지금은 비어 보이기만 함. `ModelView.RendererInfo`가 `GL:`로 시작하면 실패)

- [x] **업데이트 후 다시 띄우기가 실패함** (v2.8.2에서 고침) (v2.8.0 → v2.8.1에서 사용자가 겪음: 설치는 되지만 `System.Diagnostics.Process` FileNotFoundException 창이 뜨고, 직접 다시 켜야 함)
  - 원인: 한 파일 exe는 dll을 처음 쓸 때 exe 안에서 꺼내는데, 설치하며 exe가 새 버전으로 바뀌어 꺼낼 수 없게 됨
  - 고침(v2.8.2): `Updater.PreloadAssemblies`가 설치 전에 어셈블리를 모두 메모리에 올림. `--update-test <로그>`(복사한 폴더에서)로 실제 릴리스를 깔고 다시 띄우기까지 확인함
  - v2.8.1도 이 문제가 있으므로 v2.8.1 → 다음 버전 업데이트 때 같은 창이 한 번 뜬다 (설치는 됨). v2.8.2 릴리스 노트에 "오류 창이 뜨면 닫고 다시 실행" 안내를 넣음

## MacOS

- [ ] 맥에서 빌드·실행: `python build.py mac` → `dist/BusDepot-mac-arm64.zip`(Apple 실리콘) / `-x64.zip`(Intel)
  - 처음 한 번: `xattr -dr com.apple.quarantine /Applications/BusDepot.app` → `codesign --force --deep --sign - /Applications/BusDepot.app`
  - 맥에서 직접 빌드하려면 .NET 8 SDK 설치 후 `dotnet run --project app/BusDepot.csproj`
- [x] v2.8.3: 맥 첫 릴리스 (zip 2개, README 5개 언어에 설치 안내)
- [x] `/Applications/BusDepot.app/Contents/MacOS/BusDepot --selftest ~/Desktop/st.txt` → 실패 6개 고침 (v2.8.3: HOF 폴더 순서, obj 줄바꿈, 맥 기준 기대값). v2.8.3으로 다시 돌려 볼 것
- [ ] 버스 3D(OpenGL 3.2 core)가 보이는지, 텍스처·투명 유리·애니메이션·기즈모 끌기
- [ ] 설정 'OMSI 2 / openOMSI': OMSI 2 폴더 직접 고르기, `~/.openomsi/content` 자동 인식, 인코딩(949/1252 …)으로 한글/독일어 파일 이름이 맞게 나오는지
- [ ] 만든 오브젝트/스플라인이 `content/Sceneryobjects`, `content/Splines`에 들어가고 openOMSI에서 보이는지
- [ ] 맵 탭의 없는 파일 검사가 content 폴더까지 보는지, HOF 목록
- [ ] Blender(맥용 `/Applications/Blender.app`) 찾기와 .blend 저장, `.x` 단독 변환
- [ ] 단축키가 맥답게 동작하는지 (되돌리기 `⌘Z`, 다시 실행 `⌘⇧Z`)
- [ ] 정식 서명/공증(Apple Developer 계정)을 할지, 아니면 지금처럼 직접 서명 안내로 갈지
- [x] 맥 릴리스 (v2.8.3부터, GitHub 릴리스에 Windows `BusDepot.zip`을 먼저 올리고 맥 zip 2개)
- [ ] 맥 메뉴 막대의 표준 항목(숨기기/종료 ⌘Q)이 앱 메뉴에 남아 있는지 확인
