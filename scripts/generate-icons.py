"""Regenerate the MSIX tile and icon PNGs from logo.png.

The llama mark is cut out of logo.png (the "InControl" wordmark below it is left out),
the white page around it is made transparent, and every file already in
src/InControl.App/Assets is redrawn at its current pixel size.

Run with: py -3 scripts/generate-icons.py
"""

from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "src" / "InControl.App" / "Assets"

# Share of the canvas the mark fills. Tiles get padding so the mark sits inside the plate.
FILL = {
    "Square44x44Logo": 1.0,
    "StoreLogo": 1.0,
    "Square71x71Logo": 0.72,
    "SmallTile": 0.72,
    "Square150x150Logo": 0.66,
    "Square310x310Logo": 0.66,
    "LargeTile": 0.66,
    "Wide310x150Logo": 0.72,
    "SplashScreen": 0.72,
}


def cut_mark() -> Image.Image:
    logo = Image.open(ROOT / "logo.png").convert("RGBA")
    w, h = logo.size
    px = logo.load()

    # The wordmark starts well below the octagon. Find the octagon's box above it.
    limit = int(h * 0.77)
    xs, ys = [], []
    for y in range(limit):
        for x in range(w):
            r, g, b, _ = px[x, y]
            if min(r, g, b) < 200:
                xs.append(x)
                ys.append(y)
    box = (min(xs) - 2, min(ys) - 2, max(xs) + 3, max(ys) + 3)
    mark = logo.crop(box)

    # Background is the near-white region connected to the crop's border.
    mw, mh = mark.size
    mp = mark.load()
    bg = [[False] * mw for _ in range(mh)]
    stack = [(x, y) for x in range(mw) for y in (0, mh - 1)] + [(x, y) for y in range(mh) for x in (0, mw - 1)]
    while stack:
        x, y = stack.pop()
        if x < 0 or y < 0 or x >= mw or y >= mh or bg[y][x]:
            continue
        r, g, b, _ = mp[x, y]
        if min(r, g, b) < 235:
            continue
        bg[y][x] = True
        stack.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    # Clear the background, and un-blend white from a thin band along its edge so
    # the outline has no light fringe on a dark taskbar.
    band = 3
    for y in range(mh):
        for x in range(mw):
            if bg[y][x]:
                mp[x, y] = (0, 0, 0, 0)
                continue
            near = any(
                bg[yy][xx]
                for yy in range(max(0, y - band), min(mh, y + band + 1))
                for xx in range(max(0, x - band), min(mw, x + band + 1))
            )
            if not near:
                continue
            r, g, b, _ = mp[x, y]
            a = max(255 - r, 255 - g, 255 - b) / 255
            if a <= 0:
                mp[x, y] = (0, 0, 0, 0)
                continue
            un = lambda c: max(0, min(255, round((c - 255 * (1 - a)) / a)))
            mp[x, y] = (un(r), un(g), un(b), round(a * 255))

    side = max(mw, mh)
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.paste(mark, ((side - mw) // 2, (side - mh) // 2), mark)
    return square


def main() -> None:
    mark = cut_mark()
    mark.save(ROOT / "assets" / "icon-mark.png")
    for path in sorted(ASSETS.glob("*.png")):
        stem = path.name.split(".")[0]
        fill = FILL.get(stem)
        if fill is None:
            raise SystemExit(f"No fill rule for {path.name}")
        w, h = Image.open(path).size
        size = max(1, round(min(w, h) * fill))
        icon = mark.resize((size, size), Image.LANCZOS)
        canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        canvas.paste(icon, ((w - size) // 2, (h - size) // 2), icon)
        canvas.save(path, optimize=True)
        print(f"{path.name} {w}x{h}")


if __name__ == "__main__":
    main()
