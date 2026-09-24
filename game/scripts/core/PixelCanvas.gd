class_name PixelCanvas
extends RefCounted
## Tiny immediate-mode pixel painter used to generate placeholder art at startup.

var img: Image
var w: int
var h: int


func _init(width: int, height: int) -> void:
	w = width
	h = height
	img = Image.create(w, h, false, Image.FORMAT_RGBA8)


func px(x: int, y: int, c: Color) -> void:
	if x >= 0 and y >= 0 and x < w and y < h:
		img.set_pixel(x, y, c)


func rect(x: int, y: int, rw: int, rh: int, c: Color) -> void:
	var r := Rect2i(x, y, rw, rh).intersection(Rect2i(0, 0, w, h))
	if r.size.x > 0 and r.size.y > 0:
		img.fill_rect(r, c)


func hline(x0: int, x1: int, y: int, c: Color) -> void:
	for x in range(mini(x0, x1), maxi(x0, x1) + 1):
		px(x, y, c)


func vline(x: int, y0: int, y1: int, c: Color) -> void:
	for y in range(mini(y0, y1), maxi(y0, y1) + 1):
		px(x, y, c)


func line(x0: int, y0: int, x1: int, y1: int, c: Color) -> void:
	var dx := absi(x1 - x0)
	var dy := -absi(y1 - y0)
	var sx := 1 if x0 < x1 else -1
	var sy := 1 if y0 < y1 else -1
	var err := dx + dy
	while true:
		px(x0, y0, c)
		if x0 == x1 and y0 == y1:
			break
		var e2 := 2 * err
		if e2 >= dy:
			err += dy
			x0 += sx
		if e2 <= dx:
			err += dx
			y0 += sy


## Line with a square brush of size `t`.
func thick(a: Vector2, b: Vector2, t: int, c: Color) -> void:
	var steps := int(maxf(a.distance_to(b), 1.0))
	var half := t / 2
	for i in steps + 1:
		var p := a.lerp(b, float(i) / steps)
		rect(int(round(p.x)) - half, int(round(p.y)) - half, t, t, c)


func circle(cx: float, cy: float, r: float, c: Color) -> void:
	for y in range(int(cy - r - 1), int(cy + r + 2)):
		for x in range(int(cx - r - 1), int(cx + r + 2)):
			if Vector2(x + 0.5 - cx, y + 0.5 - cy).length() <= r:
				px(x, y, c)


func ellipse(cx: float, cy: float, rx: float, ry: float, c: Color) -> void:
	for y in range(int(cy - ry - 1), int(cy + ry + 2)):
		for x in range(int(cx - rx - 1), int(cx + rx + 2)):
			var dx := (x + 0.5 - cx) / rx
			var dy := (y + 0.5 - cy) / ry
			if dx * dx + dy * dy <= 1.0:
				px(x, y, c)


func poly(points: PackedVector2Array, c: Color) -> void:
	var minx := 99999.0
	var maxx := -99999.0
	var miny := 99999.0
	var maxy := -99999.0
	for p in points:
		minx = minf(minx, p.x)
		maxx = maxf(maxx, p.x)
		miny = minf(miny, p.y)
		maxy = maxf(maxy, p.y)
	for y in range(int(miny), int(maxy) + 1):
		for x in range(int(minx), int(maxx) + 1):
			if Geometry2D.is_point_in_polygon(Vector2(x + 0.5, y + 0.5), points):
				px(x, y, c)


## Random speckle texture inside a rect.
func noise(x: int, y: int, rw: int, rh: int, c: Color, density: float, rng: RandomNumberGenerator) -> void:
	for yy in range(y, y + rh):
		for xx in range(x, x + rw):
			if rng.randf() < density:
				px(xx, yy, c)


## Darkens the lower-right edge of opaque pixels for a cheap bevel.
func shade_edges(amount: float = 0.25) -> void:
	var src: Image = img.duplicate()
	for y in h:
		for x in w:
			var c := src.get_pixel(x, y)
			if c.a < 0.5:
				continue
			var below := src.get_pixel(x, y + 1) if y + 1 < h else Color(0, 0, 0, 0)
			if below.a < 0.5:
				img.set_pixel(x, y, c.darkened(amount))


## Adds a 1px outline around every opaque pixel.
func outline(c: Color = Color("1a1420")) -> void:
	var src: Image = img.duplicate()
	for y in h:
		for x in w:
			if src.get_pixel(x, y).a > 0.5:
				continue
			var solid := false
			for o in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
				var nx: int = x + o.x
				var ny: int = y + o.y
				if nx >= 0 and ny >= 0 and nx < w and ny < h and src.get_pixel(nx, ny).a > 0.5:
					solid = true
					break
			if solid:
				img.set_pixel(x, y, c)


func tint_all(c: Color, amount: float) -> void:
	for y in h:
		for x in w:
			var p := img.get_pixel(x, y)
			if p.a > 0.0:
				img.set_pixel(x, y, Color(p.lerp(c, amount), p.a))


func flip_x() -> void:
	img.flip_x()


func texture() -> ImageTexture:
	return ImageTexture.create_from_image(img)
