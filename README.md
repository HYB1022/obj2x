<p align="center">
  <img src="docs/images/logo.png" width="112" alt="obj2x 로고">
</p>

<h1 align="center">obj2x</h1>

<p align="center">
  Blender에서 내보낸 <b>obj → x → o3d</b> 변환을 폴더 단위로 한 번에 처리하는 OMSI용 도구
</p>

<p align="center">
  <a href="https://github.com/HYB1022/obj2x/releases/latest"><img src="https://img.shields.io/github/v/release/HYB1022/obj2x?style=flat-square&label=%EC%B5%9C%EC%8B%A0%20%EB%B2%84%EC%A0%84&color=5046e5" alt="최신 버전"></a>
  <a href="https://github.com/HYB1022/obj2x/releases"><img src="https://img.shields.io/github/downloads/HYB1022/obj2x/total?style=flat-square&label=%EB%8B%A4%EC%9A%B4%EB%A1%9C%EB%93%9C&color=0a84ff" alt="다운로드 수"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078d4?style=flat-square" alt="Windows 10 | 11">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0-34c759?style=flat-square" alt="GPL-3.0"></a>
</p>

<p align="center">
  <a href="https://github.com/HYB1022/obj2x/releases/latest/download/obj2x.zip"><b>⬇ obj2x.zip 내려받기</b></a>
  &nbsp;·&nbsp;
  <a href="#사용법">사용법</a>
  &nbsp;·&nbsp;
  <a href="#문제-해결">문제 해결</a>
</p>

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/images/dashboard-dark.png">
    <img src="docs/images/dashboard-light.png" width="860" alt="대시보드: 빠른 변환으로 9개 파일을 변환한 결과 표">
  </picture>
</p>

파일을 하나씩 열고 옵션을 맞추고 Convert를 누르던 작업을, **폴더만 지정하면** 자동으로 해 줍니다.

- **일괄 변환**: 폴더 안의 `.obj`를 Blender 2.79b로 `.x`로, 이어서 OmsiXConv로 `.o3d`까지 변환합니다. OmsiXConv의 체크박스(`X-File: z up` 등)는 정해 둔 상태로 알아서 맞춥니다.
- **병렬 처리**: OmsiXConv를 여러 개 동시에 띄우고, 그 창들은 화면 밖으로 치워 이 도구의 창만 보이게 합니다.
- **빠른 변환 (실험적)**: Blender와 OmsiXConv 없이 obj에서 바로 `.o3d`를 만듭니다. 파일당 0.1초 안팎입니다.
- **결과 표**: 파일별 정점·삼각형·재질 수, 걸린 시간, 상태(OK / 주의 / 실패)를 한눈에 보여 줍니다.

`.o3d`는 `.x`와 같은 폴더에 생기고, 파일 이름은 obj 이름과 같습니다 (`face.obj` → `face.x` → `face.o3d`).

## 화면

<table>
  <tr>
    <td width="50%"><img src="docs/images/wizard-light.png" alt="변환 마법사: 변환 방식 선택"></td>
    <td width="50%"><img src="docs/images/about-light.png" alt="정보 탭: 새 버전 업데이트 알림"></td>
  </tr>
  <tr>
    <td align="center"><b>변환 마법사</b><br><sub>종류 → 경로 → 옵션 → 완료, 4단계로 변환을 시작합니다</sub></td>
    <td align="center"><b>업데이트 알림</b><br><sub>새 버전이 있으면 <code>정보</code> 메뉴에 표시되고, 버튼 하나로 설치합니다</sub></td>
  </tr>
  <tr>
    <td width="50%"><img src="docs/images/dashboard-dark.png" alt="다크 테마 대시보드"></td>
    <td width="50%"><img src="docs/images/about-dark.png" alt="다크 테마 정보 탭"></td>
  </tr>
  <tr>
    <td align="center" colspan="2"><b>라이트 / 다크 테마</b><br><sub><code>설정</code>에서 테마(시스템 / 라이트 / 다크)와 강조 색(인디고 / 틸 / 로즈), 언어(한국어 / English)를 고를 수 있습니다</sub></td>
  </tr>
</table>

## 필요한 것

| 필요한 것 | 설명 |
| --- | --- |
| Windows 10 / 11 | 실행에 필요한 .NET Framework 4.x는 Windows에 기본 포함되어 있습니다 |
| [Blender 2.79b](https://download.blender.org/release/Blender2.79/) | 2.79 기본 포함 `Import-Export: DirectX X Format` 애드온이 켜져 있어야 합니다. 다른 버전(2.8 이상)은 지원하지 않습니다 |
| OmsiXConv (Omsi X to O3D Converter 1.0) | **이 저장소에 포함되어 있지 않습니다.** 별도로 구해서 `OmsiXConv.exe` 위치를 지정하세요 |

obj를 만드는 Blender는 버전이 상관없습니다. 변환에만 2.79b가 필요합니다. 빠른 변환만 쓴다면 Blender와 OmsiXConv 모두 없어도 됩니다.

## 사용법

1. [Releases](https://github.com/HYB1022/obj2x/releases/latest)에서 최신 `obj2x.zip`을 받아 **쓰기 가능한 폴더**에 풉니다. 설정은 같은 폴더의 `obj2x.ini`에 저장됩니다.
2. `obj2x.exe`(또는 `obj2x.bat`)를 실행합니다. 처음 실행하면 면책 조항에 동의해야 합니다.
3. `고급` 탭에서 Blender 2.79의 `blender.exe`와 `OmsiXConv.exe` 위치를 지정합니다. 한 번 지정하면 저장됩니다.
4. `변환 마법사`에서 변환 방식을 고르고, OBJ 폴더(입력)와 출력 폴더를 지정한 뒤 변환을 시작합니다. OBJ 폴더 칸에는 폴더를 끌어다 놓아도 되고, 최근에 쓴 폴더를 눌러 바로 고를 수도 있습니다.
5. 진행 상황은 `대시보드`의 진행 막대와 `로그` / `결과 표` 탭에 표시됩니다.

| 변환 방식 | 하는 일 |
| --- | --- |
| **DirectX → O3D** (권장) | Blender 2.79b로 `.x`를 만든 뒤 OmsiXConv로 `.o3d`까지 변환하는 표준 방식 |
| **Blender .x 단독** | OmsiXConv 없이 `.x` 파일만 만듭니다 |
| **FastO3D** (실험적) | 내장 변환기로 obj에서 바로 `.o3d`를 만듭니다. 아래 [빠른 변환](#빠른-변환-실험적-기능)을 먼저 읽어 보세요 |

> [!NOTE]
> OmsiXConv로 변환하는 동안에는 보이지 않는 OmsiXConv 창이 입력을 받고 있으니 마우스와 키보드를 가급적 건드리지 마세요.

## 기능

| 기능 | 설명 |
| --- | --- |
| 하위 폴더 포함 | 입력 폴더 안의 하위 폴더까지 obj를 찾아 변환합니다. 출력 폴더에도 같은 하위 폴더 구조로 `.x`/`.o3d`가 생깁니다 |
| 변경된 obj만 변환 | `.o3d`보다 새로 수정된 obj만 변환합니다 (`.x`만 만들 때는 `.x`와 비교). 모델링하며 반복해서 내보낼 때 유용합니다 |
| 중지 | Blender와 OmsiXConv를 모두 종료하고 대기열을 비웁니다 |
| 실패한 것만 다시 | 끝난 뒤 실패한 파일만 O3D 단계부터 다시 돌립니다 |
| 동시 실행 개수 | OmsiXConv를 몇 개까지 동시에 띄울지 정합니다. 기본값은 PC 논리 코어 수 − 1입니다. OmsiXConv는 코어 하나만 쓰므로 코어 수보다 많이 띄우면 오히려 느려집니다 |
| 업데이트 확인 | 시작할 때 GitHub에서 최신 릴리스를 확인하고, 새 버전이 있으면 `정보` 메뉴의 `업데이트` 표시로 알려 줍니다 |

### 고급 / 설정 탭

- **OmsiXConv 세부 옵션** (`고급`): `z up`, `Separate Files`, `Show Details of X`, `Remove Specular`, `Opt. Materials`, `Opt. Vertices`, `All Normals Up`, `Use Submesh Local Pos. as Global Pos.` 변환할 때마다 여기서 정한 상태로 자동 설정됩니다. 기본값은 `z up` 해제, `Remove Specular` / `Opt. Materials` / `Opt. Vertices` 체크입니다.
- **Blender .x 내보내기 추가 옵션** (`고급`): 내보내기 애드온의 옵션을 `이름=값` 한 줄씩 적습니다 (예: `ApplyModifiers=True`). 실행 로그의 `x exporter options:` 목록에 있는 이름만 적용되고, 없는 이름은 `ignored options`로 표시됩니다.
- **성공 시 .x 파일 삭제** (`설정`): O3D 변환에 성공하면 중간 파일인 `.x`를 지웁니다.
- **글꼴** (`설정`): 나눔스퀘어 네오(기본), Noto Sans KR, Windows 시스템 글꼴 중에서 고릅니다. 앞의 두 글꼴은 `fonts` 폴더에 들어 있습니다.
- **언어 파일** (`설정`): 한국어, English, Deutsch, 日本語, 中文을 지원합니다. `언어 파일` 버튼을 누르면 exe 옆에 `lang` 폴더와 번역 틀(`_template.json`)이 생깁니다. 틀을 복사해 `<언어 코드>.json`(예: `fr.json`)으로 저장하고 번역하면 언어 목록에 나타납니다. 기본 언어와 같은 이름으로 저장하면 그 언어의 일부 문구만 바꿀 수도 있습니다.

### 개발자 옵션

`정보` 탭의 `버전` 줄을 일곱 번 누르면 사이드바의 `정보` 위에 `개발자 옵션`이 생깁니다. 맨 위 스위치를 끄면 다시 사라집니다.

- 업데이트: 버그 신고(GitHub 이슈), 지금 업데이트 확인, 최신 버전 다시 설치
- 디버깅: 자체 점검 실행, 언어 파일 다시 읽기, 설정 파일 열기, 환영 화면 다시 보기
- 그리기: 새로 고침 빈도(FPS) 표시, 레이아웃 경계 표시, 애니메이션 배율(꺼짐 ~ 10배)
- 변환: Blender 출력을 거르지 않고 모두 로그에 남기기
- 앱 정보: .NET 런타임, 렌더링 방식, 화면 배율, 설치 위치

### 빠른 변환 (실험적 기능)

> [!WARNING]
> **실험적 기능입니다.** OmsiXConv의 동작을 파일 형식에서 역으로 분석해 만든 것이라 모든 모델에서 같은 결과가 나온다고 보장할 수 없습니다. 중요한 모델은 처음에는 기존 방식과 결과를 비교해 보고, 문제가 있으면 기존 방식으로 변환하세요.

OmsiXConv의 `.o3d`와 대조해 검증한 범위에서는 삼각형·재질·UV가 같습니다(노멀은 0.001 이내 차이, 정점 수는 몇 개 달라질 수 있음). 다음은 아직 지원하지 않거나 검증하지 못했습니다.

- 오브젝트가 여러 개로 나뉜 obj: 하나의 메시로 합쳐지며 오브젝트별 기준점은 유지되지 않습니다. OmsiXConv와 결과가 같은지는 확인하지 못했습니다
- 정점 또는 삼각형이 65,535개를 넘는 모델 (실패로 표시됨)
- OmsiXConv 세부 옵션(`z up` 등)과 Blender 추가 옵션은 적용되지 않음

## 업데이트

시작할 때 새 버전이 있으면 사이드바의 `정보` 메뉴 오른쪽에 `업데이트` 표시가 생기고, `정보` 탭의 `업데이트` 줄에도 `업데이트` 표시가 올라옵니다. `업데이트`를 누르면 릴리스의 zip을 내려받아 같은 폴더에 덮어쓰고 다시 시작합니다.

- 설정(`obj2x.ini`)은 그대로 유지됩니다.
- 기존 `obj2x.exe`, `obj2x.bat`, `obj2x.py`, `obj2x_gui.ps1`은 `obj2x_backup` 폴더에 보관됩니다.
- v2.0(PowerShell 버전)에서도 그대로 최신 버전으로 업데이트됩니다. `obj2x_gui.ps1`은 이제 `obj2x.exe`를 실행하는 런처입니다.
- 시작할 때 GitHub에 접속하는 게 싫으면 `설정`에서 `시작 시 업데이트 확인`을 끄세요.
- 내려받은 파일의 무결성은 따로 검증하지 않습니다. 이 저장소의 릴리스를 신뢰하는 경우에만 사용하세요.

## 문제 해결

| 증상 | 확인할 것 |
| --- | --- |
| Windows가 실행을 막거나 경고함 | `obj2x.exe`는 코드 서명이 없어 SmartScreen 경고가 뜰 수 있습니다. `추가 정보` → `실행`을 누르거나, 소스(`src/`)를 확인하고 `build.bat`으로 직접 빌드하세요 |
| `blender.exe` 관련 오류 | 2.79b의 `blender.exe`를 지정했는지 확인하세요 (자동 감지는 하지 않습니다) |
| `E/A-Fehler 103`이 간혹 뜸 | OmsiXConv 쪽 오류입니다. 자동으로 최대 3번 재시도하고, 계속 나면 동시 실행 개수를 줄여 보세요 |
| 로그에 `OmsiXConv 창을 못 찾음` | OmsiXConv 경로가 맞는지, 다른 프로그램이 그 창을 가로막지 않는지 확인하세요 |
| 로그에 `파일 선택창을 못 찾음` | 괄호 안에 나온 창 목록을 이슈로 알려주세요 |
| 방향이 틀어진 모델 | `고급` 탭에서 OmsiXConv의 `z up` 상태를 확인하세요 (기본은 해제) |

문제가 있으면 로그 내용과 함께 [이슈](https://github.com/HYB1022/obj2x/issues)로 알려주세요. 제 환경에서 만든 도구라 다른 환경에서는 동작이 다를 수 있습니다. 파일이 큰 `.x`(수 MB 이상)는 변환이 오래 걸릴 수 있습니다.

## 직접 빌드하기

`build.bat`을 실행하면 Windows에 기본 포함된 C# 컴파일러(.NET Framework 4)로 `src\Core`(변환 로직)와 `src\App`(WPF 화면)을 `obj2x.exe`로 빌드합니다. 따로 설치할 것은 없습니다.

```bat
build.bat
obj2x.exe --selftest
```

`--selftest`는 설정·문구·변환기·화면 구성을 자동으로 점검하고, 결과를 같은 폴더의 `obj2x_selftest.txt`에 `PASS` / `FAIL` 줄로 남깁니다.

## 면책 조항

처음 실행하면 면책 조항이 표시되고, 내용에 동의해야 사용할 수 있습니다. 동의 여부는 `obj2x.ini`에 저장되며, `정보` 탭의 `면책 조항`을 누르면 다시 볼 수 있습니다. 이 도구는 개인이 만든 비공식 도구이며 Blender, OmsiXConv, OMSI와 관련이 없고, 변환 결과나 파일 손상 등에 대해 보증하거나 책임지지 않습니다. 중요한 파일은 백업하고 결과를 직접 확인하세요.

이 저장소에는 Blender와 OmsiXConv가 포함되어 있지 않습니다. 각 프로그램의 라이선스는 각자 따릅니다.

## 변경 내역

- **v2.6** — 언어를 파일(`lang/*.json`)로 분리해 사용자가 직접 언어를 추가할 수 있게, 일본어(日本語)·중국어(中文) 추가, 기본 글꼴을 나눔스퀘어 네오로 바꾸고 글꼴 선택(나눔스퀘어 네오 / Noto Sans KR / 시스템) 추가, 업데이트 확인 결과가 `업데이트` 표시로 올라오며 나타나게, 개발자 옵션(정보 탭의 버전을 7번 누르기) 추가
- **v2.5** — 독일어(Deutsch) 추가(Windows가 독일어면 처음부터 독일어로 시작), 애니메이션을 화면 주사율(60fps)에 맞춰 더 부드럽게, 환영 화면 인사말을 로고 아래 가운데로
- **v2.4** — 화면을 WPF로 새로 만들고 변환 로직과 화면 코드를 분리, 버전/상태 표시를 사이드바 아래로 옮겨 오른쪽 화면을 창 높이 전체로 사용, 동시 실행 개수 숫자 상자(직접 입력, 휠, 길게 눌러 반복), 언어 드롭다운, 테마 전환 애니메이션, 정보 화면 CPU 사용량 약 1/3로 감소(다른 창을 쓰는 중에는 멈춤), 글자·아이콘·입력 상자 세로 정렬 수정
- **v2.3** — 새 버전 알림을 빨간 점 대신 `정보` 메뉴 오른쪽의 `업데이트` 표시로 변경, 라이선스를 GPL-3.0으로 변경
- **v2.2** — 새 버전 알림을 전체 화면 창 대신 `정보` 탭의 업데이트 줄로 표시(`업데이트` 버튼으로 바로 설치), 사이드바 메뉴마다 아이콘 추가
- **v2.1** — PowerShell 스크립트에서 실행 파일(`obj2x.exe`, C#)로 전환. 기존 설정(`obj2x.ini`)과 면책 조항 동의는 그대로 이어지며, v2.0의 자동 업데이트로 설치할 수 있음

<details>
<summary>이전 버전 (v1.1 ~ v2.0)</summary>

v2.2 이하 버전의 실행 파일은 더 이상 배포하지 않습니다. 자동 업데이트로 최신 버전을 받아 주세요.

- **v2.0** — 화면 개편(카드형 영역, 진행 막대, 라이트/다크 테마: 옵션 창에서 선택), 빠른 변환 결과 표, 처음 실행 시 면책 조항 동의
- **v1.9** — 하위 폴더 포함 옵션, OBJ 폴더 칸에 폴더 끌어다 놓기, 빠른 변환 로그에 주의 표시(mtl 없음 / mtl에 없는 재질 / 오브젝트 여러 개 합침)
- **v1.8** — 빠른 변환(내장, 실험적) 추가: Blender/OmsiXConv 없이 obj에서 바로 `.o3d` 생성
- **v1.7** — 동시 실행 기본값을 PC의 코어 수에 맞춤(이전 10개 고정은 코어가 적은 PC에서 오히려 느려질 수 있음), 화면에 코어 수 표시
- **v1.6** — O3D 변환을 큰 파일부터 시작해서 전체 시간 단축
- **v1.5** — 변환 소요 시간 표시(Blender 단계, 파일별 준비/변환 시간, O3D 단계 총 시간)
- **v1.4** — 옵션 창에서 `확인`을 누르면 오류가 나고 설정이 저장되지 않던 문제 수정
- **v1.3** — 변경된 obj만 변환, 중지 / 실패한 것만 다시, 옵션 창(OmsiXConv 체크박스 전체 설정, .x 삭제, 내보내기 추가 옵션), 자동 업데이트 설치
- **v1.1** — 최초 공개: obj → x → o3d 일괄 변환, `z up` 자동 해제, 병렬 변환, 입력 폴더와 같게 옵션, 업데이트 확인

</details>

## 라이선스

Copyright (C) 2026 Hyunwoo Bang

[GNU General Public License v3.0 이상](LICENSE) (GPL-3.0-or-later). 자유롭게 사용·수정·재배포할 수 있지만, 수정한 버전을 배포할 때는 같은 GPL 라이선스로 소스 코드를 함께 공개해야 합니다.

v2.2 이전 버전은 MIT 라이선스로 배포되었습니다.
