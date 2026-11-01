# -*- coding: utf-8 -*-
# Bus Depot 빌드 (Windows와 맥 모두 이 PC에서): .NET 8 SDK + Python 3
#   python build.py            → Windows 실행 폴더 (dist/win)  + 맥 앱 zip 2개
#   python build.py win        → Windows만 (dist/win, dist/BusDepot.zip)
#   python build.py mac        → 맥만 (dist/BusDepot-mac-arm64.zip, dist/BusDepot-mac-x64.zip)
#   python build.py release    → 릴리스용 zip 3개 (dist/BusDepot.zip, BusDepot-mac-arm64.zip, BusDepot-mac-x64.zip)
#   python build.py test       → 빌드 결과 점검 (Windows exe 자체 점검 + 맥 zip 구성). build_test.bat이 release 다음에 부른다
# 앱은 app/ (Avalonia, Windows·맥 공용), 로직은 shared/Core.
import os, sys, re, shutil, subprocess, zipfile, time

ROOT = os.path.dirname(os.path.abspath(__file__))
APP = os.path.join(ROOT, 'app', 'BusDepot.csproj')
DIST = os.path.join(ROOT, 'dist')
VER = re.search(r'Version\s*=\s*"([^"]+)"', open(os.path.join(ROOT, 'shared', 'Core', 'AppInfo.cs'), encoding='utf-8-sig').read()).group(1)

def run(cmd, **kw):
    print('>', ' '.join(cmd)); sys.stdout.flush()
    r = subprocess.run(cmd, **kw)
    if r.returncode != 0: sys.exit('failed: ' + ' '.join(cmd))

def publish(rid, out):
    if os.path.isdir(out): shutil.rmtree(out)
    run(['dotnet', 'publish', APP, '-c', 'Release', '-r', rid, '-o', out, '-nologo', '-v', 'q'])

# Windows: obj2x.exe(예전 이름의 바로 가기·v2.7.2 이하 자동 업데이트가 찾는 작은 런처)는 Windows에 들어 있는 csc로 만든다
def launcher(out):
    fw = os.path.join(os.environ.get('WINDIR', r'C:\Windows'), 'Microsoft.NET', 'Framework64', 'v4.0.30319', 'csc.exe')
    if not os.path.exists(fw): fw = fw.replace('Framework64', 'Framework')
    if not os.path.exists(fw): print('csc.exe 없음: obj2x.exe 런처를 건너뜀'); return
    run([fw, '/nologo', '/target:winexe', '/optimize+', '/out:' + os.path.join(out, 'obj2x.exe'), '/win32icon:' + os.path.join(ROOT, 'windows', 'BusDepot.ico'),
         os.path.join(ROOT, 'windows', 'Launcher', 'Launcher.cs')])

def win():
    out = os.path.join(DIST, 'win')
    publish('win-x64', out)
    launcher(out)
    shutil.copy(os.path.join(ROOT, 'windows', 'BusDepot.bat'), out)
    shutil.copy(os.path.join(ROOT, 'LICENSE'), out)
    print('Windows:', out)
    return out

# 릴리스 zip (Windows): 모두 최상위. 예전 업데이터용 obj2x.bat, obj2x_gui.ps1도 넣는다 (v2.0 이하 PowerShell 업데이터가 찾는다)
def win_zip(out):
    z = os.path.join(DIST, 'BusDepot.zip')
    if os.path.exists(z): os.remove(z)
    with zipfile.ZipFile(z, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as f:
        for dp, dn, fn in os.walk(out):
            for n in fn:
                if n.lower().startswith('misans'): continue            # MiSans는 라이선스상 절대 넣지 않는다
                if n.lower().endswith(('.ini', '.txt')) and n not in ('OFL.txt',): continue
                full = os.path.join(dp, n)
                f.write(full, os.path.relpath(full, out).replace(os.sep, '/'))
        for n in ('obj2x.bat', 'obj2x_gui.ps1'):
            f.write(os.path.join(ROOT, 'windows', 'compat', n), n)
    print('zip:', z, '%.1f MB' % (os.path.getsize(z) / 1048576))
    return z

def mac():
    zips = []
    for rid, arch in (('osx-arm64', 'arm64'), ('osx-x64', 'x64')):
        out = os.path.join(DIST, rid)
        publish(rid, out)
        z = os.path.join(DIST, 'BusDepot-mac-%s.zip' % arch)
        run([sys.executable, os.path.join(ROOT, 'mac', 'make_app.py'), out, z, VER])
        zips.append(z)
    return zips

# 빌드 결과 점검: Windows exe(트리밍한 배포본)의 --selftest, 맥 zip의 구성·실행 권한·CPU 종류·버전.
# 맥 앱을 실제로 돌려 보는 것은 맥에서만 된다 (마지막에 명령을 알려 준다). 실패가 있으면 False
def test():
    ok = True
    exe = os.path.join(DIST, 'win', 'BusDepot.exe')
    if not os.path.exists(exe):
        print('[FAIL] Windows exe 없음:', exe); ok = False
    else:
        res = os.path.join(DIST, 'selftest_win.txt')
        if os.path.exists(res): os.remove(res)
        t = time.time()
        subprocess.run([exe, '--selftest', res], timeout=600)
        lines = open(res, encoding='utf-8-sig', errors='replace').read().splitlines() if os.path.exists(res) else []
        p = sum(1 for l in lines if l.startswith('PASS')); f = [l for l in lines if l.startswith('FAIL')]
        for l in f: print('   ', l)
        good = p > 0 and not f
        print('[%s] Windows 자체 점검: PASS %d, FAIL %d (%.0f초) → %s' % ('OK' if good else 'FAIL', p, len(f), time.time() - t, res))
        ok = ok and good
    # 맥 실행 파일의 CPU 종류 (Mach-O 64비트 머리말의 cputype)
    cpu = {'arm64': 0x0100000C, 'x64': 0x01000007}
    for arch in ('arm64', 'x64'):
        z = os.path.join(DIST, 'BusDepot-mac-%s.zip' % arch)
        if not os.path.exists(z): print('[FAIL] 맥 zip 없음:', z); ok = False; continue
        bad = []
        with zipfile.ZipFile(z) as f:
            info = {i.filename: i for i in f.infolist()}
            c = 'BusDepot.app/Contents/'
            for n, exe755 in ((c + 'MacOS/BusDepot', True), (c + 'MacOS/libAvaloniaNative.dylib', True), (c + 'MacOS/libSkiaSharp.dylib', True),
                              (c + 'MacOS/libHarfBuzzSharp.dylib', True), (c + 'MacOS/BusDepot.py', False), (c + 'Info.plist', False),
                              (c + 'Resources/BusDepot.icns', False), ('LICENSE', False)):
                if n not in info: bad.append('없음 ' + n); continue
                if exe755 and (info[n].external_attr >> 16) & 0o111 == 0: bad.append('실행 권한 없음 ' + n)
            if any(n.lower().split('/')[-1].startswith('misans') for n in info): bad.append('MiSans가 들어 있음')
            if c + 'MacOS/BusDepot' in info:
                head = f.open(c + 'MacOS/BusDepot').read(8)
                magic, ct = int.from_bytes(head[:4], 'little'), int.from_bytes(head[4:8], 'little')
                if magic != 0xFEEDFACF or ct != cpu[arch]: bad.append('실행 파일 CPU가 %s가 아님 (%08x %08x)' % (arch, magic, ct))
            if c + 'Info.plist' in info and VER not in f.read(c + 'Info.plist').decode('utf-8', 'replace'): bad.append('Info.plist 버전이 %s가 아님' % VER)
        for b in bad: print('   ', b)
        print('[%s] 맥 %s zip (%.1f MB)' % ('FAIL' if bad else 'OK', arch, os.path.getsize(z) / 1048576))
        ok = ok and not bad
    print('맥에서 실행 점검: /Applications/BusDepot.app/Contents/MacOS/BusDepot --selftest ~/Desktop/st.txt')
    return ok

if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'test':
        sys.exit(0 if test() else 1)
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    os.makedirs(DIST, exist_ok=True)
    t0 = time.time()
    if what in ('win', 'all', 'release'):
        o = win()
        if what in ('win', 'release'): win_zip(o)
    if what in ('mac', 'all', 'release'): mac()
    print('done (v%s, %.0fs)' % (VER, time.time() - t0))
