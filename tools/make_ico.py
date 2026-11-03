"""Build a multi-size capoo.ico from btn_internet_os.png.

Sizes 16-128 are stored as 32-bit BMP entries (with an AND mask), 256 as PNG,
the classic layout Windows and rcedit handle everywhere.
Usage: python make_ico.py <source.png> <out.ico> <preview.png>
"""
import io
import struct
import sys

from PIL import Image

SIZES = [16, 24, 32, 48, 64, 128, 256]


def square(src):
	side = max(src.size)
	canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
	canvas.alpha_composite(src, ((side - src.size[0]) // 2, (side - src.size[1]) // 2))
	return canvas


def bmp_entry(img):
	w, h = img.size
	header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, 0, 0, 0, 0, 0)
	px = img.load()
	xor = bytearray()
	for y in range(h - 1, -1, -1):
		for x in range(w):
			r, g, b, a = px[x, y]
			xor += bytes((b, g, r, a))
	row_bytes = ((w + 31) // 32) * 4
	mask = bytearray()
	for y in range(h - 1, -1, -1):
		row = bytearray(row_bytes)
		for x in range(w):
			if px[x, y][3] == 0:
				row[x // 8] |= 0x80 >> (x % 8)
		mask += row
	return header + bytes(xor) + bytes(mask)


def png_entry(img):
	buf = io.BytesIO()
	img.save(buf, "PNG", optimize=True)
	return buf.getvalue()


def main(src_path, out_path, preview_path):
	src = square(Image.open(src_path).convert("RGBA"))
	images = [src.resize((s, s), Image.LANCZOS) for s in SIZES]
	blobs = [png_entry(im) if im.size[0] >= 256 else bmp_entry(im) for im in images]

	out = bytearray(struct.pack("<HHH", 0, 1, len(images)))
	offset = 6 + 16 * len(images)
	for im, blob in zip(images, blobs):
		w = im.size[0]
		out += struct.pack("<BBBBHHII", w % 256, w % 256, 0, 0, 1, 32, len(blob), offset)
		offset += len(blob)
	for blob in blobs:
		out += blob
	with open(out_path, "wb") as f:
		f.write(out)

	sheet = Image.new("RGBA", (sum(SIZES) + 16 * len(SIZES), 256), (255, 255, 255, 255))
	x = 0
	for s in SIZES:
		ico = Image.open(out_path)
		ico.size = (s, s)
		sheet.alpha_composite(ico.convert("RGBA"), (x, 256 - s))
		x += s + 16
	sheet.save(preview_path)


if __name__ == "__main__":
	main(*sys.argv[1:4])
