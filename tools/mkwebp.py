# README 맨 위 움직이는 그림 만들기 (개발용, Pillow 필요)
#   obj2x.exe --welcome-frames <프레임 폴더> [light|dark]
#   python tools/mkwebp.py <프레임 폴더> docs/images/welcome-light.webp 960 80 1.2
# 인수: 프레임 폴더, 출력 파일, 가로 픽셀, 품질(0~100), 시작 시각(초, 첫 장에 인사말이 보이게)
import sys, os
from PIL import Image
src, out, width, q, start = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), float(sys.argv[5])
n, fps, period, blend = open(os.path.join(src, "times.txt")).read().split()
n, fps = int(n), int(fps); P = int(round(float(period) * fps)); B = int(round(float(blend) * fps))
def load(i):
    im = Image.open(os.path.join(src, "f_%04d.png" % i)).convert("RGB")
    h = round(im.height * width / im.width)
    return im.resize((width, h), Image.LANCZOS)
frames = []
for k in range(P):                       # t = k/fps, 한 바퀴
    im = load(k + B)
    if k >= P - B:                       # 끝부분을 '한 바퀴 전' 프레임과 섞어 처음과 이어지게
        w = (k - (P - B)) / B
        im = Image.blend(im, load(k + B - P), w)
    frames.append(im)
r = int(round(start * fps)) % P          # 이 시각부터 시작 (첫 장에 인사말이 보이게)
frames = frames[r:] + frames[:r]
frames[0].save(out, save_all=True, append_images=frames[1:], duration=int(1000 / fps), loop=0, quality=q, method=6)
print(len(frames), frames[0].size, os.path.getsize(out) // 1024, "KB")
