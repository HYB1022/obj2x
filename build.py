# -*- coding: utf-8 -*-
# Bus Depot 빌드 (Windows와 맥 모두 이 PC에서): .NET 8 SDK + Python 3
#   python build.py            → Windows 실행 폴더 (dist/win)  + 맥 앱 zip 2개
#   python build.py win        → Windows만 (dist/win, dist/BusDepot.zip)
#   python build.py mac        → 맥만 (dist/BusDepot-mac-arm64.zip, dist/BusDepot-mac-x64.zip)
#   python build.py release    → 릴리스용 zip 3개 (dist/BusDepot.zip, BusDepot-mac-arm64.zip, BusDepot-mac-x64.zip)
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

if __name__ == '__main__':
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    os.makedirs(DIST, exist_ok=True)
    t0 = time.time()
    if what in ('win', 'all', 'release'):
        o = win()
        if what in ('win', 'release'): win_zip(o)
    if what in ('mac', 'all', 'release'): mac()
    print('done (v%s, %.0fs)' % (VER, time.time() - t0))
