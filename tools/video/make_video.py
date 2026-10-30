"""Bus Depot 소개 영상 만들기: 언어마다 오디오 트랙과 자막 트랙이 따로 들어간 MP4 하나.

필요한 것: ffmpeg/ffprobe (PATH), Python 패키지 edge-tts (Microsoft 온라인 음성, 인터넷 필요), 빌드한 BusDepot.exe

  python tools/video/make_video.py --exe <BusDepot.exe> --out <폴더> [--model <model.cfg|.sco>] [--split]

1. narration.json의 대본을 장면/언어마다 음성(mp3)으로 만든다
2. 장면 길이 = 가장 긴 언어의 음성 + 앞뒤 여유 (최소 min초)
3. BusDepot.exe --video-frames로 장면마다 화면(1920x1080, 30fps, 영어 UI)을 만든다
4. 화면 → 영상, 언어마다 음성을 장면 시작에 맞춰 이어 붙인 오디오, 자막(srt)을 만들어 MP4 하나로 묶는다
   --split이면 언어마다 오디오 하나짜리 MP4도 만든다
"""
import argparse, asyncio, io, json, os, shutil, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
LEAD, TAIL, FPS = 0.6, 0.8, 30


def run(cmd):
    print(">", " ".join(c if " " not in c else '"%s"' % c for c in cmd[:6]), "...")
    subprocess.run(cmd, check=True)


def duration(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", path],
                         check=True, capture_output=True, text=True).stdout
    return float(out.strip())


async def tts(text, voice, path):
    import edge_tts
    await edge_tts.Communicate(text, voice).save(path)


def srt_time(t):
    ms = int(round(t * 1000))
    return "%02d:%02d:%02d,%03d" % (ms // 3600000, ms // 60000 % 60, ms // 1000 % 60, ms % 1000)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--model", default="")
    ap.add_argument("--split", action="store_true")
    ap.add_argument("--name", default="BusDepot_intro")
    a = ap.parse_args()
    spec = json.load(io.open(os.path.join(HERE, "narration.json"), encoding="utf-8"))
    langs = list(spec["voices"].keys())
    scenes = spec["scenes"]
    out = os.path.abspath(a.out)
    tdir = os.path.join(out, "tts"); os.makedirs(tdir, exist_ok=True)

    # 1. 음성
    dur = {}
    for sc in scenes:
        for lg in langs:
            p = os.path.join(tdir, "%s_%s.mp3" % (lg, sc["id"]))
            if not os.path.exists(p):
                asyncio.run(tts(sc["text"][lg], spec["voices"][lg][0], p))
            dur[(lg, sc["id"])] = duration(p)

    # 2. 장면 길이 (프레임 단위로 맞춘다)
    lens = []
    for sc in scenes:
        longest = max(dur[(lg, sc["id"])] for lg in langs)
        d = max(sc["min"], LEAD + longest + TAIL)
        lens.append(round(d * FPS) / FPS)
    sfile = os.path.join(out, "scenes.txt")
    with io.open(sfile, "w", encoding="utf-8") as fh:
        for sc, d in zip(scenes, lens):
            fh.write("%s %.4f\n" % (sc["id"], d))
    print("total %.1fs" % sum(lens))

    # 3. 화면
    fdir = os.path.join(out, "frames")
    cmd = [a.exe, "--video-frames", fdir, sfile]
    if a.model: cmd.append(a.model)
    run(cmd)

    # 4-1. 영상: 장면마다 인코딩해서 이어 붙이고, 처음/끝은 살짝 페이드
    parts = []
    for sc in scenes:
        p = os.path.join(out, "v_%s.mp4" % sc["id"])
        run(["ffmpeg", "-y", "-v", "error", "-framerate", str(FPS), "-i", os.path.join(fdir, sc["id"], "f%05d.png"),
             "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-r", str(FPS), p])
        parts.append(p)
    lst = os.path.join(out, "parts.txt")
    with io.open(lst, "w", encoding="utf-8") as fh:
        for p in parts: fh.write("file '%s'\n" % p.replace("\\", "/"))
    joined = os.path.join(out, "video_joined.mp4")
    run(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", lst, "-c", "copy", joined])
    total = sum(lens)
    video = os.path.join(out, "video.mp4")
    run(["ffmpeg", "-y", "-v", "error", "-i", joined, "-vf", "fade=in:st=0:d=0.5,fade=out:st=%.3f:d=0.6" % (total - 0.6),
         "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", video])

    # 4-2. 언어마다 오디오(장면 시작 + LEAD에 음성) + 자막
    audio, subs = {}, {}
    for lg in langs:
        inputs, filt = [], []
        for i, (sc, d) in enumerate(zip(scenes, lens)):
            inputs += ["-i", os.path.join(tdir, "%s_%s.mp3" % (lg, sc["id"]))]
            ms = int(LEAD * 1000)
            filt.append("[%d:a]aresample=48000,aformat=channel_layouts=stereo,adelay=%d|%d,apad,atrim=0:%.4f,asetpts=PTS-STARTPTS[a%d]" % (i, ms, ms, d, i))
        filt.append("".join("[a%d]" % i for i in range(len(scenes))) + "concat=n=%d:v=0:a=1,afade=t=out:st=%.3f:d=0.6[out]" % (len(scenes), total - 0.6))
        ap_ = os.path.join(out, "a_%s.m4a" % lg)
        run(["ffmpeg", "-y", "-v", "error"] + inputs + ["-filter_complex", ";".join(filt), "-map", "[out]", "-c:a", "aac", "-b:a", "160k", ap_])
        audio[lg] = ap_
        sp = os.path.join(out, "%s.srt" % lg)
        t = 0.0
        with io.open(sp, "w", encoding="utf-8") as fh:
            for i, (sc, d) in enumerate(zip(scenes, lens)):
                s0 = t + LEAD; s1 = s0 + dur[(lg, sc["id"])]
                fh.write("%d\n%s --> %s\n%s\n\n" % (i + 1, srt_time(s0), srt_time(s1), sc["text"][lg]))
                t += d
        subs[lg] = sp

    # 4-3. 묶기: 영상 1 + 오디오 N + 자막 N (첫 언어가 기본)
    final = os.path.join(out, a.name + ".mp4")
    cmd = ["ffmpeg", "-y", "-v", "error", "-i", video]
    for lg in langs: cmd += ["-i", audio[lg]]
    for lg in langs: cmd += ["-i", subs[lg]]
    cmd += ["-map", "0:v"]
    for i in range(len(langs)): cmd += ["-map", "%d:a" % (1 + i)]
    for i in range(len(langs)): cmd += ["-map", "%d:s" % (1 + len(langs) + i)]
    cmd += ["-c:v", "copy", "-c:a", "copy", "-c:s", "mov_text"]
    for i, lg in enumerate(langs):
        code, title = spec["voices"][lg][1], spec["voices"][lg][2]
        cmd += ["-metadata:s:a:%d" % i, "language=" + code, "-metadata:s:a:%d" % i, "title=" + title,
                "-metadata:s:s:%d" % i, "language=" + code, "-metadata:s:s:%d" % i, "title=" + title,
                "-disposition:a:%d" % i, "default" if i == 0 else "0", "-disposition:s:%d" % i, "0"]
    cmd += ["-movflags", "+faststart", final]
    run(cmd)
    print("OK", final)

    if a.split:
        for lg in langs:
            p = os.path.join(out, "%s_%s.mp4" % (a.name, lg))
            run(["ffmpeg", "-y", "-v", "error", "-i", video, "-i", audio[lg], "-i", subs[lg], "-map", "0:v", "-map", "1:a", "-map", "2:s",
                 "-c:v", "copy", "-c:a", "copy", "-c:s", "mov_text", "-metadata:s:a:0", "language=" + spec["voices"][lg][1],
                 "-metadata:s:s:0", "language=" + spec["voices"][lg][1], "-movflags", "+faststart", p])
            print("OK", p)


if __name__ == "__main__":
    main()
