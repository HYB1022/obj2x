# obj2x.py  (Blender 2.79b)
# obj 파일을 불러와서 .x 로 내보낸다.
# 사용: blender -b -P obj2x.py -- <obj 폴더> <x 출력 폴더> [작업 파일]
#   작업 파일(선택): 'obj:이름' 줄이 있으면 그 obj 만 변환, 'opt:이름=값' 줄은 내보내기 옵션으로 추가
# 모델 보기의 .blend 저장: blender -b -P obj2x.py -- --blend <obj 파일> <.blend 파일>
#   obj2x가 OMSI 모델(cfg/sco)을 obj + mtl로 풀어 둔 것을 불러와 텍스처를 파일 안에 넣고 저장한다 (2.8 이상에서도 동작)
import bpy, sys, os, glob, io

args = sys.argv[sys.argv.index("--") + 1:]

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


def to_blend(obj, out):
    # 빈 장면에서 시작 (기본 상자/조명/카메라 지우기)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    # 3.2부터 새 obj 불러오기(wm.obj_import), 4.0부터는 그것만 있다
    if bpy.app.version >= (3, 2, 0):
        bpy.ops.wm.obj_import(filepath=obj)
    else:
        bpy.ops.import_scene.obj(filepath=obj, split_mode="ON")
    n = len(bpy.data.objects)
    print("imported objects:", n)
    # OMSI는 뒷면을 그리지 않는다: 재질마다 뒷면 감추기를 켜서 노멀이 뒤집힌 면이 게임처럼 안 보이게
    for m in bpy.data.materials:
        try:
            m.use_backface_culling = True          # 2.8 이상
        except Exception:
            try:
                m.game_settings.use_backface_culling = True   # 2.79
            except Exception:
                pass
    # OMSI는 [matl_alpha]가 있을 때만 알파를 투명으로 쓴다 (obj2x가 그때만 map_d를 넣음).
    # 그 밖의 재질은 텍스처의 알파(반사 마스크 등)를 무시해야 색이 하얗게 바래지 않는다 (2.79 Blender Internal)
    for m in bpy.data.materials:
        if getattr(m, "use_transparency", True):
            continue
        for s in getattr(m, "texture_slots", []):
            if s and s.texture and hasattr(s.texture, "use_alpha"):
                s.texture.use_alpha = False
    # OMSI는 삼각형만 쓰므로 o3d의 삼각형을 그대로 둔다 (사각형으로 합치지 않음)
    try:
        bpy.ops.file.pack_all()
        print("packed images:", len(bpy.data.images))
    except Exception as ex:
        print("pack failed:", ex)
    # 열었을 때 바로 텍스처가 보이게
    for scr in bpy.data.screens:
        for area in scr.areas:
            if area.type != "VIEW_3D":
                continue
            for sp in area.spaces:
                if sp.type != "VIEW_3D":
                    continue
                try:
                    sp.viewport_shade = "TEXTURED"
                except Exception:
                    try:
                        sp.shading.type = "MATERIAL"
                    except Exception:
                        pass
    d = os.path.dirname(out)
    if d and not os.path.isdir(d):
        os.makedirs(d)
    bpy.ops.wm.save_as_mainfile(filepath=out, check_existing=False)
    print("OK", out)


def to_x():
    src, dst = args[0], args[1]
    jobfile = args[2] if len(args) > 2 else None
    os.makedirs(dst, exist_ok=True)
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


if args and args[0] == "--blend":
    try:
        to_blend(args[1], args[2])
    except Exception as ex:
        print("Error:", ex)
    print("DONE")
else:
    to_x()
