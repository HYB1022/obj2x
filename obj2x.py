# obj2x.py  (Blender 2.79b)
# obj 파일을 불러와서 .x 로 내보낸다.
# 사용: blender -b -P obj2x.py -- <obj 폴더> <x 출력 폴더> [작업 파일]
#   작업 파일(선택): 'obj:이름' 줄이 있으면 그 obj 만 변환, 'opt:이름=값' 줄은 내보내기 옵션으로 추가
import bpy, sys, os, glob, io

args = sys.argv[sys.argv.index("--") + 1:]
src, dst = args[0], args[1]
jobfile = args[2] if len(args) > 2 else None
os.makedirs(dst, exist_ok=True)

# 기본 내보내기 옵션. 작업 파일의 opt 줄이 이것을 덮어쓴다. 없는 옵션 이름은 자동으로 무시된다.
OPTS = {
    "SelectedOnly": True,
}


def conv(v):
    lv = v.lower()
    if lv == "true":
        return True
    if lv == "false":
        return False
    try:
        return int(v)
    except ValueError:
        pass
    try:
        return float(v)
    except ValueError:
        return v


names = None
if jobfile and os.path.exists(jobfile):
    names = []
    with io.open(jobfile, "r", encoding="utf-8-sig") as fh:
        for line in fh:
            line = line.strip()
            if line.startswith("obj:"):
                names.append(line[4:])
            elif line.startswith("opt:") and "=" in line[4:]:
                k, v = line[4:].split("=", 1)
                OPTS[k.strip()] = conv(v.strip())

props = bpy.ops.export_scene.x.get_rna().bl_rna.properties.keys()
print("x exporter options:", sorted(props))
opts = dict((k, v) for k, v in OPTS.items() if k in props)
ignored = [k for k in OPTS if k not in props]
if ignored:
    print("ignored options (unknown name):", ignored)

if names is None:
    files = sorted(glob.glob(os.path.join(src, "*.obj")))
    rels = [os.path.splitext(os.path.basename(f))[0] for f in files]
else:
    # 이름에 하위 폴더 경로(예: door\\left)가 들어 있을 수 있다. 출력도 같은 하위 구조로 만든다.
    files = [os.path.join(src, n + ".obj") for n in names]
    rels = list(names)
print("obj files:", len(files))
for f, rel in zip(files, rels):
    if not os.path.exists(f):
        print("Error: missing", f)
        continue
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    bpy.ops.import_scene.obj(filepath=f)
    bpy.ops.object.select_all(action="SELECT")
    out = os.path.join(dst, rel + ".x")
    if not os.path.isdir(os.path.dirname(out)):
        os.makedirs(os.path.dirname(out))
    bpy.ops.export_scene.x(filepath=out, **opts)
    print("OK", out)
print("DONE")
