# dotnet publish 결과(osx-arm64 / osx-x64, app/BusDepot.csproj)를 BusDepot.app으로 묶어 zip으로 만든다 (Windows에서도 실행 가능). build.py가 부른다.
#   python mac/make_app.py <publish 폴더> <출력 zip> <버전>
# zip 안의 실행 파일에는 유닉스 실행 권한(755)을 넣는다. Windows에서 만든 zip은 기본으로 권한이 없어 맥에서 실행되지 않는다.
# 서명이 없으므로 맥에서 처음 한 번은 README의 안내대로 xattr / codesign을 해야 한다.
import os, sys, zipfile, time

pub, out = sys.argv[1], sys.argv[2]
ver = sys.argv[3] if len(sys.argv) > 3 and sys.argv[3] not in ("", "auto") else None
here = os.path.dirname(os.path.abspath(__file__))
root = os.path.dirname(here)
if ver is None:
    import re
    ver = re.search(r'Version\s*=\s*"([^"]+)"', open(os.path.join(root, "shared", "Core", "AppInfo.cs"), encoding="utf-8-sig").read()).group(1)
app = "BusDepot.app/Contents/"
plist = """<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Bus Depot</string>
  <key>CFBundleDisplayName</key><string>Bus Depot</string>
  <key>CFBundleIdentifier</key><string>io.github.hyb1022.busdepot</string>
  <key>CFBundleVersion</key><string>%s</string>
  <key>CFBundleShortVersionString</key><string>%s</string>
  <key>CFBundleExecutable</key><string>BusDepot</string>
  <key>CFBundleIconFile</key><string>BusDepot.icns</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSHumanReadableCopyright</key><string>Copyright (C) 2026 Hyunwoo Bang, Junyoung. GPL-3.0-or-later</string>
</dict>
</plist>
""" % (ver, ver)

FIRST = """Bus Depot %s (macOS)

[한국어]
1. BusDepot.app을 '응용 프로그램(Applications)' 폴더로 옮깁니다.
2. 서명이 없는 앱이라 처음 한 번은 터미널에서 아래 두 줄을 실행해야 열립니다.
   (Apple 실리콘 Mac은 두 번째 줄이 꼭 필요합니다)

   xattr -dr com.apple.quarantine /Applications/BusDepot.app
   codesign --force --deep --sign - /Applications/BusDepot.app

3. 설정에서 OMSI 2 폴더와 openOMSI의 Mods 폴더(openOMSI가 설치된 곳 옆)를 확인하세요.
   만든 오브젝트/스플라인은 Mods 폴더 안의 'Bus Depot' 폴더에 들어갑니다.

[English]
1. Move BusDepot.app to the Applications folder.
2. The app is not signed, so run these two lines in Terminal once before opening it
   (the second one is required on Apple silicon Macs):

   xattr -dr com.apple.quarantine /Applications/BusDepot.app
   codesign --force --deep --sign - /Applications/BusDepot.app

3. In Settings, check the OMSI 2 folder and openOMSI's Mods folder (next to where openOMSI is installed).
   Objects and splines you make go into a 'Bus Depot' folder inside Mods.

https://github.com/HYB1022/obj2x
""" % ver

now = time.localtime()[:6]

def add(z, name, data=None, path=None, exe=False):
    zi = zipfile.ZipInfo(name, now)
    zi.compress_type = zipfile.ZIP_DEFLATED
    zi.create_system = 3                      # unix: external_attr의 권한을 맥이 읽는다
    zi.external_attr = ((0o755 if exe else 0o644) | 0o100000) << 16
    if path is not None:
        with open(path, "rb") as f: data = f.read()
    z.writestr(zi, data)

with zipfile.ZipFile(out, "w") as z:
    add(z, app + "Info.plist", plist.encode("utf-8"))
    add(z, app + "Resources/BusDepot.icns", path=os.path.join(here, "BusDepot.icns"))   # 로고 (BusDepot --make-icns로 만든 것)
    for dp, dn, fn in os.walk(pub):
        for f in fn:
            full = os.path.join(dp, f)
            rel = os.path.relpath(full, pub).replace(os.sep, "/")
            if f.lower().startswith("misans"): continue
            exe = f == "BusDepot" or f.endswith(".dylib")
            add(z, app + "MacOS/" + rel, path=full, exe=exe)
    for f in ("LICENSE",):
        add(z, f, path=os.path.join(root, f))
    add(z, "처음 실행 - READ ME FIRST.txt", FIRST.encode("utf-8"))
    # 글꼴은 exe 안에 들어 있고(MiSans는 라이선스상 넣지 않는다), 글꼴 라이선스(fonts/OFL.txt)와 BusDepot.py는 publish 결과에 함께 있다
print("ok", out)
