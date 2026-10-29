# v2.0 이하(PowerShell 버전)와의 호환용 런처.
# 예전 자동 업데이트는 zip 안에서 이 파일을 찾아 그 폴더를 덮어쓰므로 릴리스 zip에 반드시 넣는다.
# 직접 실행해도 obj2x.exe를 띄운다.
$exe = Join-Path $PSScriptRoot 'obj2x.exe'
if (Test-Path $exe) { Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot }
else { Start-Process -FilePath (Join-Path $PSScriptRoot 'obj2x.bat') -WorkingDirectory $PSScriptRoot }
