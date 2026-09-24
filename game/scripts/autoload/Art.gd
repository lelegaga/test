extends Node
## Procedural placeholder pixel art.
##
## Every texture in the demo is painted here at runtime with PixelCanvas and
## cached by key, so the game runs with zero external art assets. Swapping in
## real sprites later only requires returning loaded textures from `tex()` /
## `frames()`.

const OUT := Color("1a1420")

var _cache: Dictionary = {}
var _rng := RandomNumberGenerator.new()

# ---------------------------------------------------------------- palettes
var PAL := {
	"player": {
		"skin": Color("f1b98a"), "skin_d": Color("c47f55"),
		"shirt": Color("d9b27a"), "shirt_d": Color("a8834e"),
		"pants": Color("5d6b3a"), "pants_d": Color("434d28"),
		"boots": Color("5a3a22"), "belt": Color("4a3020"),
		"head": "bandana", "band": Color("d8322e"), "hair": Color("6a3a1a"),
		"pack": Color("7a6440"), "sleeve": false,
	},
	"soldier": {
		"skin": Color("e8a878"), "skin_d": Color("b87550"),
		"shirt": Color("8b9a55"), "shirt_d": Color("65733a"),
		"pants": Color("717e46"), "pants_d": Color("505a30"),
		"boots": Color("33261a"), "belt": Color("3a2a1a"),
		"head": "helmet", "helmet": Color("5e6b3a"), "band": Color("c83030"), "hair": Color("2a1a10"),
		"pack": Color("5a5030"), "sleeve": true,
	},
	"charger": {
		"skin": Color("e0a070"), "skin_d": Color("b07048"),
		"shirt": Color("a83a2a"), "shirt_d": Color("7a2a1e"),
		"pants": Color("4a4a3a"), "pants_d": Color("33332a"),
		"boots": Color("222222"), "belt": Color("222222"),
		"head": "bandana", "band": Color("222222"), "hair": Color("222222"),
		"pack": null, "sleeve": false,
	},
	"rocket": {
		"skin": Color("e8a878"), "skin_d": Color("b87550"),
		"shirt": Color("9a8a55"), "shirt_d": Color("736540"),
		"pants": Color("6a6040"), "pants_d": Color("4a4430"),
		"boots": Color("33261a"), "belt": Color("3a2a1a"),
		"head": "cap", "helmet": Color("8a3030"), "band": Color("c83030"), "hair": Color("2a1a10"),
		"pack": Color("6a5a30"), "sleeve": true,
	},
	"shield": {
		"skin": Color("e8a878"), "skin_d": Color("b87550"),
		"shirt": Color("5a6a78"), "shirt_d": Color("3e4a56"),
		"pants": Color("4a5560"), "pants_d": Color("333c44"),
		"boots": Color("222222"), "belt": Color("222222"),
		"head": "riot", "helmet": Color("3e4a56"), "band": Color("c83030"), "hair": Color("2a1a10"),
		"pack": null, "sleeve": true,
	},
	"hostage": {
		"skin": Color("f0c098"), "skin_d": Color("c08860"),
		"shirt": Color("ece4d4"), "shirt_d": Color("bdb3a0"),
		"pants": Color("8a6a4a"), "pants_d": Color("664c34"),
		"boots": Color("4a3020"), "belt": Color("4a3020"),
		"head": "beard", "band": Color("c83030"), "hair": Color("6a4a2a"),
		"pack": null, "sleeve": false,
	},
}


func _ready() -> void:
	_rng.seed = 42


func tex(key: String) -> Texture2D:
	if _cache.has(key):
		return _cache[key]
	var t: Texture2D = call("_gen_" + key)
	_cache[key] = t
	return t


func cached(key: String, maker: Callable) -> Variant:
	if not _cache.has(key):
		_cache[key] = maker.call()
	return _cache[key]


# ================================================================ HUMANOIDS
## Returns a dictionary of body frames for a character style.
func frames(style: String) -> Dictionary:
	return cached("frames_" + style, _make_frames.bind(style))


func _make_frames(style: String) -> Dictionary:
	var pal: Dictionary = PAL[style]
	var f := {}
	f["idle0"] = _humanoid(pal, "stand", 0, "none", 0)
	f["idle1"] = _humanoid(pal, "stand", 0, "none", 1)
	for i in 6:
		f["run%d" % i] = _humanoid(pal, "run", i, "none", 1 if i % 3 == 0 else 0)
	f["jump"] = _humanoid(pal, "jump", 0, "none", 0)
	f["fall"] = _humanoid(pal, "fall", 0, "none", 0)
	f["crouch"] = _humanoid(pal, "crouch", 0, "none", 0)
	f["crouch1"] = _humanoid(pal, "crouch", 1, "none", 0)
	f["fly"] = _humanoid(pal, "fly", 0, "up", 0)
	f["dead"] = _corpse(pal)
	f["burnt"] = _burnt(_humanoid(pal, "fly", 0, "up", 0))
	f["knife"] = _humanoid(pal, "stand", 0, "knife", 0)
	f["cheer"] = _humanoid(pal, "stand", 0, "up", 0)
	f["salute"] = _humanoid(pal, "stand", 0, "salute", 0)
	f["tied"] = _humanoid(pal, "sit", 0, "tied", 0)
	f["tied1"] = _humanoid(pal, "sit", 0, "tied", 1)
	f["sit"] = _humanoid(pal, "sit", 0, "down", 0)
	f["walk_idle"] = _humanoid(pal, "stand", 0, "down", 0)
	for i in 6:
		f["walk%d" % i] = _humanoid(pal, "run", i, "down", 1 if i % 3 == 0 else 0)
	f["parachute"] = _humanoid(pal, "fall", 0, "up", 0)
	return f


func _leg_pose(legs: String, phase: int) -> Array:
	# Returns [back_knee, back_foot, front_knee, front_foot] in frame coords.
	match legs:
		"run":
			var a := phase * TAU / 6.0
			var res: Array = []
			for k in 2:
				var ang := a + PI * k
				var fx := 15.0 + 8.0 * cos(ang)
				var fy := 46.0 - maxf(0.0, sin(ang)) * 5.0
				var knee := Vector2((15.0 + fx) * 0.5 + 3.0, 38.0 - maxf(0.0, sin(ang)) * 3.0)
				res.append_array([knee, Vector2(fx, fy)])
			return res
		"jump":
			return [Vector2(12, 35), Vector2(9, 41), Vector2(21, 34), Vector2(19, 42)]
		"fall":
			return [Vector2(12, 37), Vector2(10, 45), Vector2(19, 37), Vector2(21, 45)]
		"crouch":
			return [Vector2(10, 45), Vector2(5, 46), Vector2(22, 39), Vector2(22, 46)]
		"sit":
			return [Vector2(22, 43), Vector2(24, 46), Vector2(24, 41), Vector2(27, 46)]
		"fly":
			return [Vector2(9, 36), Vector2(4, 40), Vector2(22, 35), Vector2(27, 38)]
		_:
			return [Vector2(13, 38), Vector2(12, 46), Vector2(18, 38), Vector2(19, 46)]


func _humanoid(pal: Dictionary, legs: String, phase: int, arms: String, bob: int) -> ImageTexture:
	var c := PixelCanvas.new(32, 48)
	var dy := 0
	if legs == "crouch":
		dy = 9
	elif legs == "sit":
		dy = 10
	dy += bob
	if legs == "crouch" and phase == 1:
		dy += 1
	var hip := Vector2(15, 30 + dy)
	var lp := _leg_pose(legs, phase)
	# back leg
	_leg(c, hip + Vector2(-1, 0), lp[0], lp[1], pal.pants_d, Color(pal.boots).darkened(0.25))
	# backpack
	if pal.pack != null:
		c.rect(7, 18 + dy, 5, 9, pal.pack)
		c.rect(7, 18 + dy, 1, 9, Color(pal.pack).darkened(0.3))
	# back arm for poses that show both arms
	if arms == "up":
		c.thick(Vector2(13, 19 + dy), Vector2(9, 8 + dy), 3, pal.skin_d)
		c.rect(7, 5 + dy, 4, 4, pal.skin_d)
	# torso
	c.rect(11, 17 + dy, 10, 13, pal.shirt)
	c.rect(11, 17 + dy, 2, 13, pal.shirt_d)
	if pal.sleeve:
		c.line(13, 17 + dy, 19, 28 + dy, Color(pal.shirt_d))
	c.rect(11, 28 + dy, 10, 2, pal.belt)
	c.px(18, 28 + dy, Color("e0c040"))
	# front leg
	_leg(c, hip + Vector2(1, 0), lp[2], lp[3], pal.pants, pal.boots)
	# neck + head
	c.rect(14, 15 + dy, 5, 2, pal.skin_d)
	c.rect(11, 6 + dy, 10, 10, pal.skin)
	c.rect(11, 6 + dy, 2, 10, pal.skin_d)
	c.px(18, 9 + dy, OUT)
	c.px(18, 10 + dy, OUT)
	c.px(19, 13 + dy, pal.skin_d)
	c.px(20, 13 + dy, pal.skin_d)
	match pal.head:
		"bandana":
			c.rect(11, 4 + dy, 10, 3, pal.hair)
			c.rect(11, 6 + dy, 10, 2, pal.band)
			c.px(10, 7 + dy, pal.band)
			c.px(9, 7 + dy, pal.band)
			c.px(8, 8 + dy, pal.band)
			c.px(9, 8 + dy, pal.band)
			c.px(7, 9 + dy, pal.band)
		"helmet":
			c.ellipse(16, 7 + dy, 7, 4, pal.helmet)
			c.rect(9, 7 + dy, 14, 2, Color(pal.helmet).darkened(0.25))
			c.px(12, 5 + dy, Color(pal.helmet).lightened(0.3))
			c.px(13, 4 + dy, Color(pal.helmet).lightened(0.3))
		"cap":
			c.rect(11, 4 + dy, 10, 4, pal.helmet)
			c.rect(19, 7 + dy, 4, 1, Color(pal.helmet).darkened(0.3))
		"riot":
			c.ellipse(16, 8 + dy, 7, 6, pal.helmet)
			c.rect(16, 8 + dy, 6, 4, Color("9ab0c0"))
			c.px(20, 9 + dy, Color("e0f0ff"))
		"beard":
			c.rect(11, 4 + dy, 10, 3, pal.hair)
			c.rect(15, 12 + dy, 6, 4, pal.hair)
			c.px(18, 13 + dy, pal.skin_d)
	# front arm poses (gun arm is a separate sprite)
	match arms:
		"up":
			c.thick(Vector2(18, 19 + dy), Vector2(22, 7 + dy), 3, pal.skin)
			c.rect(21, 4 + dy, 4, 4, pal.skin)
		"down":
			c.thick(Vector2(17, 19 + dy), Vector2(18, 26 + dy), 3, pal.skin)
			c.rect(17, 26 + dy, 3, 3, pal.skin_d)
		"salute":
			c.thick(Vector2(18, 19 + dy), Vector2(22, 14 + dy), 3, pal.skin)
			c.thick(Vector2(22, 14 + dy), Vector2(20, 8 + dy), 3, pal.skin)
		"knife":
			c.thick(Vector2(18, 20 + dy), Vector2(25, 21 + dy), 3, pal.skin)
			c.rect(25, 19 + dy, 2, 4, Color("5a3a22"))
			c.rect(27, 20 + dy, 5, 2, Color("e0e8f0"))
		"tied":
			c.hline(10, 21, 21 + dy, Color("c8a060"))
			c.hline(10, 21, 24 + dy, Color("c8a060"))
			c.px(9, 22 + dy, Color("c8a060"))
			c.px(8, 23 + dy, Color("c8a060"))
	c.outline(OUT)
	return c.texture()


func _leg(c: PixelCanvas, hip: Vector2, knee: Vector2, foot: Vector2, col: Color, boot: Color) -> void:
	c.thick(hip, knee, 4, col)
	c.thick(knee, foot - Vector2(0, 2), 4, col)
	c.rect(int(foot.x) - 2, int(foot.y) - 2, 6, 3, boot)


func _corpse(pal: Dictionary) -> ImageTexture:
	var c := PixelCanvas.new(48, 16)
	c.rect(4, 9, 14, 4, pal.pants)
	c.rect(2, 10, 4, 3, pal.boots)
	c.rect(18, 8, 14, 6, pal.shirt)
	c.rect(32, 7, 9, 8, pal.skin)
	c.px(37, 9, OUT)
	c.px(39, 9, OUT)
	if pal.head == "helmet":
		c.rect(40, 6, 4, 9, pal.helmet)
	c.outline(OUT)
	return c.texture()


func _burnt(t: ImageTexture) -> ImageTexture:
	var img := t.get_image()
	for y in img.get_height():
		for x in img.get_width():
			var p := img.get_pixel(x, y)
			if p.a > 0.5:
				var v := p.get_luminance() * 0.25
				img.set_pixel(x, y, Color(v, v * 0.9, v * 0.8))
	# glowing embers
	img.set_pixel(18, 10, Color("ff8a20"))
	img.set_pixel(14, 22, Color("ff8a20"))
	return ImageTexture.create_from_image(img)


# ================================================================ GUNS
## Arm + weapon sprite. Returns {tex, pivot, muzzle}; the sprite points right.
func gun(style: String, weapon: String) -> Dictionary:
	return cached("gun_%s_%s" % [style, weapon], _make_gun.bind(style, weapon))


func _make_gun(style: String, weapon: String) -> Dictionary:
	var pal: Dictionary = PAL[style]
	var widths := {"pistol": 22, "hmg": 36, "shotgun": 32, "rocket": 36, "flame": 32, "rifle": 28, "bazooka": 36, "smg": 26}
	var w: int = widths.get(weapon, 26)
	var c := PixelCanvas.new(w, 14)
	var pv := Vector2i(3, 6)
	var muzzle := Vector2(w - 2, 6)
	var sleeve: Color = pal.shirt if pal.sleeve else pal.skin
	# arm
	c.thick(Vector2(pv.x, pv.y), Vector2(pv.x + 6, pv.y + 1), 4, sleeve)
	c.rect(pv.x + 6, pv.y - 1, 4, 4, pal.skin)
	var grey := Color("7a808a")
	var dark := Color("3a3c44")
	var brown := Color("7a5230")
	match weapon:
		"pistol":
			c.rect(10, 4, 9, 3, grey)
			c.rect(10, 4, 9, 1, grey.lightened(0.3))
			c.rect(11, 7, 3, 4, dark)
			muzzle = Vector2(20, 5)
		"hmg":
			c.rect(8, 3, 16, 6, Color("4a505a"))
			c.rect(9, 2, 11, 1, Color("6a707a"))
			c.rect(24, 4, 10, 3, dark)
			for i in 3:
				c.px(25 + i * 3, 5, Color("15151a"))
			c.rect(12, 9, 7, 4, Color("6b5a3a"))
			c.rect(20, 9, 2, 3, dark)
			muzzle = Vector2(35, 5)
		"shotgun":
			c.rect(8, 4, 9, 4, grey)
			c.rect(17, 4, 13, 2, dark)
			c.rect(18, 6, 7, 2, brown)
			c.rect(9, 8, 3, 3, brown)
			muzzle = Vector2(31, 5)
		"rocket", "bazooka":
			var tube := Color("5d6b3a") if weapon == "rocket" else Color("6a5a3a")
			c.rect(2, 2, 26, 6, tube)
			c.rect(2, 2, 26, 1, tube.lightened(0.25))
			c.rect(6, 2, 2, 6, tube.darkened(0.3))
			c.rect(20, 2, 2, 6, tube.darkened(0.3))
			c.rect(28, 1, 4, 8, dark)
			c.rect(32, 3, 3, 4, Color("c83030"))
			c.rect(11, 8, 3, 4, dark)
			muzzle = Vector2(34, 5)
		"flame":
			c.rect(8, 3, 14, 5, Color("8a6a3a"))
			c.rect(8, 3, 14, 1, Color("b08a50"))
			c.rect(22, 4, 7, 3, grey)
			c.px(30, 5, Color("ffb030"))
			c.px(30, 4, Color("ff6020"))
			c.rect(12, 8, 3, 4, dark)
			muzzle = Vector2(30, 5)
		"rifle":
			c.rect(6, 5, 5, 3, brown)
			c.rect(11, 4, 11, 3, dark)
			c.rect(22, 5, 5, 1, dark)
			c.rect(14, 7, 2, 3, dark)
			muzzle = Vector2(27, 5)
		"smg":
			c.rect(9, 4, 10, 4, dark)
			c.rect(19, 5, 5, 1, dark)
			c.rect(13, 8, 2, 4, dark)
			muzzle = Vector2(24, 5)
	c.outline(OUT)
	return {"tex": c.texture(), "pivot": Vector2(pv), "muzzle": muzzle - Vector2(pv)}


# ================================================================ PROJECTILES
func _gen_bullet() -> Texture2D:
	var c := PixelCanvas.new(8, 4)
	c.rect(0, 1, 8, 2, Color("ffe070"))
	c.rect(5, 0, 3, 4, Color("fff8d0"))
	c.rect(0, 1, 2, 2, Color("ff9a30"))
	return c.texture()


func _gen_bullet_hmg() -> Texture2D:
	var c := PixelCanvas.new(12, 4)
	c.rect(0, 1, 12, 2, Color("ffb040"))
	c.rect(6, 0, 6, 4, Color("fff0a0"))
	c.rect(9, 1, 3, 2, Color("ffffff"))
	return c.texture()


func _gen_pellet() -> Texture2D:
	var c := PixelCanvas.new(6, 4)
	c.rect(0, 1, 6, 2, Color("ffd070"))
	c.rect(3, 0, 3, 4, Color("ffffff"))
	return c.texture()


func _gen_enemy_bullet() -> Texture2D:
	var c := PixelCanvas.new(6, 6)
	c.circle(3, 3, 3, Color("ff5030"))
	c.circle(3, 3, 1.6, Color("fff0c0"))
	return c.texture()


func _gen_rocket() -> Texture2D:
	var c := PixelCanvas.new(16, 6)
	c.rect(2, 1, 10, 4, Color("6b7a3a"))
	c.rect(12, 1, 3, 4, Color("c83030"))
	c.px(15, 2, Color("c83030"))
	c.px(15, 3, Color("c83030"))
	c.rect(0, 0, 3, 6, Color("4a4f58"))
	c.outline(OUT)
	return c.texture()


func _gen_missile() -> Texture2D:
	var c := PixelCanvas.new(16, 6)
	c.rect(2, 1, 11, 4, Color("c0c4cc"))
	c.rect(13, 2, 2, 2, Color("ff4030"))
	c.rect(0, 0, 3, 6, Color("7a2020"))
	c.rect(6, 1, 1, 4, Color("7a2020"))
	c.outline(OUT)
	return c.texture()


func _gen_shell() -> Texture2D:
	var c := PixelCanvas.new(10, 6)
	c.rect(0, 1, 7, 4, Color("8a8f98"))
	c.rect(7, 1, 3, 4, Color("e0b040"))
	c.outline(OUT)
	return c.texture()


func _gen_grenade() -> Texture2D:
	var c := PixelCanvas.new(8, 8)
	c.circle(4, 4.5, 3.2, Color("4d5a2a"))
	c.rect(3, 0, 2, 2, Color("8a8f98"))
	c.px(3, 3, Color("7a8a4a"))
	c.outline(OUT)
	return c.texture()


func _gen_bomb() -> Texture2D:
	var c := PixelCanvas.new(10, 14)
	c.ellipse(5, 8, 4, 5, Color("3a3c44"))
	c.rect(3, 1, 4, 3, Color("7a2020"))
	c.px(4, 6, Color("8a8f98"))
	c.outline(OUT)
	return c.texture()


# ================================================================ PICKUPS
func pickup(kind: String) -> Texture2D:
	return cached("pickup_" + kind, _make_pickup.bind(kind))


func _make_pickup(kind: String) -> Texture2D:
	var c := PixelCanvas.new(18, 18)
	var letters := {"hmg": "H", "shotgun": "S", "rocket": "R", "flame": "F", "bombs": "B", "ammo": "A", "life": "1", "medal": "*", "fruit": "o"}
	var cols := {"hmg": Color("e03a2a"), "shotgun": Color("e0a020"), "rocket": Color("3a8ae0"), "flame": Color("e06a20"),
		"bombs": Color("3aa040"), "ammo": Color("a0a0a0"), "life": Color("30c060"), "medal": Color("f0c020"), "fruit": Color("e04060")}
	var col: Color = cols.get(kind, Color.WHITE)
	if kind == "medal":
		c.circle(9, 10, 6, col)
		c.circle(9, 10, 3.5, col.lightened(0.4))
		c.rect(6, 0, 6, 5, Color("3a6ae0"))
	elif kind == "fruit":
		c.circle(9, 10, 6, col)
		c.px(7, 7, Color.WHITE)
		c.rect(9, 2, 2, 3, Color("3a8a3a"))
	else:
		c.rect(1, 1, 16, 16, col.darkened(0.35))
		c.rect(2, 2, 14, 14, col)
		c.rect(2, 2, 14, 2, col.lightened(0.35))
		PixelFont.paint_glyph(c, letters.get(kind, "?"), 6, 5, Color.WHITE)
	c.outline(OUT)
	return c.texture()


# ================================================================ VEHICLES
func _gen_tank_hull() -> Texture2D:
	var c := PixelCanvas.new(96, 44)
	var olive := Color("6b7a3a")
	# hull
	c.poly(PackedVector2Array([Vector2(6, 14), Vector2(82, 14), Vector2(93, 28), Vector2(2, 28)]), olive)
	c.rect(6, 14, 76, 2, olive.lightened(0.25))
	c.rect(2, 26, 91, 2, olive.darkened(0.3))
	c.rect(60, 18, 14, 5, olive.darkened(0.2))
	c.rect(16, 18, 10, 6, Color("4a4f58"))
	c.rect(86, 20, 4, 3, Color("fff0a0"))
	PixelFont.paint_glyph(c, "1", 38, 17, Color("f0f0e0"))
	# turret dome
	c.ellipse(46, 11, 16, 9, olive.darkened(0.05))
	c.rect(30, 11, 32, 4, olive)
	c.rect(40, 3, 10, 2, olive.lightened(0.3))
	c.rect(44, 0, 3, 4, Color("4a4f58"))
	# main cannon stub
	c.rect(60, 8, 18, 5, Color("4a4f58"))
	c.rect(76, 7, 4, 7, Color("3a3c44"))
	c.outline(OUT)
	return c.texture()


func tank_tread(frame: int) -> Texture2D:
	return cached("tank_tread%d" % frame, _make_tread.bind(96, 18, frame, 6))


func _make_tread(w: int, h: int, frame: int, wheels: int) -> Texture2D:
	var c := PixelCanvas.new(w, h)
	c.rect(3, 0, w - 6, h, Color("2a2a30"))
	c.rect(0, 3, w, h - 6, Color("2a2a30"))
	var step := 4
	for x in range((frame % step), w, step):
		c.px(x, 0, Color("5a5f68"))
		c.px(x, h - 1, Color("5a5f68"))
	var r := (h - 6) * 0.5
	for i in wheels:
		var cx := 8.0 + i * (w - 16.0) / (wheels - 1)
		c.circle(cx, h * 0.5, r, Color("5a5f68"))
		c.circle(cx, h * 0.5, r * 0.45, Color("8a8f98"))
		var ang := frame * 0.8 + i
		c.px(int(cx + cos(ang) * r * 0.7), int(h * 0.5 + sin(ang) * r * 0.7), Color("2a2a30"))
	c.outline(OUT)
	return c.texture()


func _gen_tank_vulcan() -> Texture2D:
	var c := PixelCanvas.new(30, 8)
	c.rect(0, 1, 10, 6, Color("5d6b3a"))
	c.rect(10, 2, 18, 2, Color("3a3c44"))
	c.rect(10, 4, 18, 2, Color("4a4f58"))
	c.rect(26, 1, 3, 6, Color("3a3c44"))
	c.outline(OUT)
	return c.texture()


func _gen_armored_car() -> Texture2D:
	var c := PixelCanvas.new(100, 52)
	var body := Color("7a7a5a")
	c.poly(PackedVector2Array([Vector2(4, 18), Vector2(80, 14), Vector2(96, 26), Vector2(96, 38), Vector2(2, 38)]), body)
	c.rect(2, 34, 94, 4, body.darkened(0.3))
	c.rect(6, 20, 20, 8, Color("3a4a5a"))
	c.rect(8, 21, 16, 3, Color("7ab0d0"))
	for i in 5:
		c.rect(30 + i * 10, 24, 6, 2, body.darkened(0.25))
	# emblem
	c.circle(62, 28, 5, Color("c83030"))
	c.circle(62, 28, 2, Color("f0f0e0"))
	# turret
	c.rect(40, 6, 26, 10, body.lightened(0.1))
	c.rect(40, 6, 26, 2, body.lightened(0.3))
	# wheels
	for x in [16, 40, 64, 86]:
		c.circle(x, 42, 8, Color("2a2a30"))
		c.circle(x, 42, 3.5, Color("8a8f98"))
	c.outline(OUT)
	return c.texture()


func _gen_car_cannon() -> Texture2D:
	var c := PixelCanvas.new(34, 8)
	c.rect(0, 2, 30, 4, Color("4a4f58"))
	c.rect(28, 1, 6, 6, Color("3a3c44"))
	c.outline(OUT)
	return c.texture()


func _gen_heli_body() -> Texture2D:
	var c := PixelCanvas.new(116, 46)
	var body := Color("5e6b3a")
	# tail boom (left) and fin
	c.rect(4, 16, 46, 6, body.darkened(0.1))
	c.poly(PackedVector2Array([Vector2(0, 4), Vector2(10, 4), Vector2(16, 18), Vector2(4, 20)]), body)
	c.circle(6, 18, 5, Color("3a3c44"))
	# main body
	c.ellipse(76, 24, 32, 14, body)
	c.rect(50, 14, 40, 3, body.lightened(0.25))
	c.ellipse(96, 22, 12, 9, Color("3a5a78"))
	c.ellipse(98, 20, 7, 5, Color("7ab0d0"))
	c.rect(56, 22, 16, 8, body.darkened(0.3))
	c.circle(64, 26, 3, Color("c83030"))
	# rocket pod + skids
	c.rect(70, 34, 22, 5, Color("3a3c44"))
	c.rect(56, 42, 44, 2, Color("3a3c44"))
	c.rect(62, 38, 2, 5, Color("3a3c44"))
	c.rect(90, 38, 2, 5, Color("3a3c44"))
	c.rect(74, 6, 6, 5, Color("3a3c44"))
	c.outline(OUT)
	return c.texture()


func _gen_truck() -> Texture2D:
	var c := PixelCanvas.new(112, 56)
	var body := Color("5e6b3a")
	c.rect(4, 8, 70, 34, Color("6f7a4a"))
	c.rect(4, 8, 70, 3, Color("8a9660"))
	for i in 6:
		c.rect(10 + i * 11, 12, 2, 28, Color("5a6440"))
	c.rect(74, 18, 30, 24, body)
	c.rect(82, 21, 14, 9, Color("7ab0d0"))
	c.rect(100, 32, 8, 8, body.darkened(0.2))
	c.rect(4, 40, 104, 4, Color("3a3c44"))
	for x in [18, 40, 90]:
		c.circle(x, 46, 8, Color("2a2a30"))
		c.circle(x, 46, 3, Color("8a8f98"))
	c.outline(OUT)
	return c.texture()


# ================================================================ BOSS
func _gen_boss_hull() -> Texture2D:
	var c := PixelCanvas.new(220, 86)
	var steel := Color("6a6e74")
	var dark := Color("3a3c44")
	c.poly(PackedVector2Array([Vector2(12, 20), Vector2(196, 20), Vector2(216, 52), Vector2(4, 52)]), steel)
	c.rect(12, 20, 184, 3, steel.lightened(0.3))
	c.rect(4, 48, 212, 4, steel.darkened(0.35))
	for i in 9:
		c.rect(20 + i * 20, 28, 12, 3, steel.darkened(0.2))
		c.px(24 + i * 20, 36, dark)
	# hazard stripes
	for i in 12:
		c.rect(30 + i * 12, 40, 6, 5, Color("e0b020") if i % 2 == 0 else dark)
	# red faction plate
	c.rect(120, 24, 26, 14, Color("a82828"))
	c.circle(133, 31, 4, Color("f0e0d0"))
	# treads
	c.rect(0, 52, 220, 32, Color("24242a"))
	for i in 10:
		c.circle(14 + i * 21.5, 68, 12, Color("4a4f58"))
		c.circle(14 + i * 21.5, 68, 5, Color("8a8f98"))
	c.outline(OUT)
	return c.texture()


func boss_tread(frame: int) -> Texture2D:
	return cached("boss_tread%d" % frame, _make_boss_tread.bind(frame))


func _make_boss_tread(frame: int) -> Texture2D:
	var c := PixelCanvas.new(220, 6)
	for x in range(frame * 3 % 12, 220, 12):
		c.rect(x, 0, 6, 6, Color("4a4f58"))
	return c.texture()


func _gen_boss_turret() -> Texture2D:
	var c := PixelCanvas.new(120, 46)
	var steel := Color("7a7e84")
	c.poly(PackedVector2Array([Vector2(10, 8), Vector2(100, 8), Vector2(116, 44), Vector2(0, 44)]), steel)
	c.rect(10, 8, 90, 3, steel.lightened(0.3))
	c.rect(20, 18, 70, 3, steel.darkened(0.2))
	c.rect(30, 0, 18, 10, steel.darkened(0.1))
	c.rect(34, 2, 10, 3, Color("ff4030"))
	c.outline(OUT)
	return c.texture()


func _gen_boss_cannon() -> Texture2D:
	var c := PixelCanvas.new(96, 18)
	c.rect(0, 2, 84, 14, Color("4a4f58"))
	c.rect(0, 2, 84, 3, Color("6a6f78"))
	c.rect(84, 0, 12, 18, Color("3a3c44"))
	for i in 5:
		c.rect(10 + i * 14, 2, 2, 14, Color("3a3c44"))
	c.outline(OUT)
	return c.texture()


func _gen_boss_pod() -> Texture2D:
	var c := PixelCanvas.new(48, 34)
	c.rect(0, 4, 48, 30, Color("5a5e64"))
	c.rect(0, 4, 48, 3, Color("7a7e84"))
	for i in 3:
		for j in 2:
			c.circle(9 + i * 15, 15 + j * 11, 4, Color("24242a"))
			c.circle(9 + i * 15, 15 + j * 11, 2, Color("c83030"))
	c.outline(OUT)
	return c.texture()


func _gen_boss_pod_cover() -> Texture2D:
	var c := PixelCanvas.new(50, 34)
	c.rect(0, 2, 50, 32, Color("6a6e74"))
	c.rect(0, 2, 50, 3, Color("8a8e94"))
	for i in 4:
		c.rect(5 + i * 12, 12, 6, 16, Color("5a5e64"))
	c.outline(OUT)
	return c.texture()


func _gen_boss_mg() -> Texture2D:
	var c := PixelCanvas.new(40, 14)
	c.rect(0, 2, 16, 10, Color("5a5e64"))
	c.rect(16, 3, 22, 3, Color("2a2a30"))
	c.rect(16, 7, 22, 3, Color("2a2a30"))
	c.rect(36, 2, 4, 10, Color("3a3c44"))
	c.outline(OUT)
	return c.texture()


func _gen_boss_wreck() -> Texture2D:
	var c := PixelCanvas.new(220, 100)
	var dark := Color("2a2626")
	c.poly(PackedVector2Array([Vector2(12, 40), Vector2(80, 26), Vector2(120, 34), Vector2(196, 36), Vector2(216, 66), Vector2(4, 66)]), Color("3a3634"))
	c.rect(0, 66, 220, 32, dark)
	for i in 10:
		c.circle(14 + i * 21.5, 82, 11, Color("3a3634"))
	_rng.seed = 7
	c.noise(4, 30, 212, 60, Color("1a1616"), 0.2, _rng)
	c.noise(4, 30, 212, 60, Color("ff7020"), 0.01, _rng)
	c.outline(OUT)
	return c.texture()


# ================================================================ PROPS
func _gen_crate() -> Texture2D:
	var c := PixelCanvas.new(24, 24)
	var wood := Color("a8743a")
	c.rect(0, 0, 24, 24, wood)
	for y in [0, 8, 16]:
		c.hline(0, 23, y, wood.darkened(0.3))
	c.rect(0, 0, 24, 2, wood.lightened(0.2))
	c.line(2, 2, 21, 21, wood.darkened(0.25))
	c.rect(0, 0, 2, 24, wood.darkened(0.35))
	c.rect(22, 0, 2, 24, wood.darkened(0.35))
	c.outline(OUT)
	return c.texture()


func supply_crate(kind: String) -> Texture2D:
	return cached("supply_" + kind, _make_supply_crate.bind(kind))


func _make_supply_crate(kind: String) -> Texture2D:
	var c := PixelCanvas.new(26, 24)
	var green := Color("5d6b3a")
	c.rect(0, 0, 26, 24, green)
	c.rect(0, 0, 26, 3, green.lightened(0.3))
	c.rect(0, 21, 26, 3, green.darkened(0.3))
	c.rect(7, 6, 12, 12, Color("f0e8d0"))
	var letters := {"hmg": "H", "shotgun": "S", "rocket": "R", "flame": "F", "bombs": "B", "random": "?"}
	PixelFont.paint_glyph(c, letters.get(kind, "?"), 11, 8, Color("c83030"))
	c.outline(OUT)
	return c.texture()


func _gen_barrel() -> Texture2D:
	var c := PixelCanvas.new(18, 24)
	var red := Color("c83a2a")
	c.rect(1, 1, 16, 22, red)
	c.rect(1, 1, 4, 22, red.lightened(0.2))
	c.rect(13, 1, 4, 22, red.darkened(0.3))
	c.rect(1, 6, 16, 2, red.darkened(0.4))
	c.rect(1, 16, 16, 2, red.darkened(0.4))
	c.rect(6, 10, 6, 4, Color("f0d020"))
	c.px(8, 11, OUT)
	c.px(9, 12, OUT)
	c.outline(OUT)
	return c.texture()


func _gen_sandbags() -> Texture2D:
	var c := PixelCanvas.new(48, 20)
	var sand := Color("c8b078")
	for row in 3:
		var y := 14 - row * 6
		var off := (row % 2) * 6
		var n := 3 if row == 2 else 4
		for i in n:
			c.ellipse(off + 7 + i * 12 + row * 3, y + 2, 6.5, 3.5, sand.darkened(0.08 * row))
			c.px(off + 5 + i * 12 + row * 3, y + 1, sand.lightened(0.3))
	c.outline(OUT)
	return c.texture()


func _gen_barricade() -> Texture2D:
	var c := PixelCanvas.new(36, 30)
	var wood := Color("8a5a2a")
	c.thick(Vector2(3, 28), Vector2(30, 4), 4, wood)
	c.thick(Vector2(6, 4), Vector2(33, 28), 4, wood.darkened(0.15))
	c.rect(0, 16, 36, 5, wood.lightened(0.1))
	c.rect(0, 23, 36, 3, Color("7a808a"))
	c.outline(OUT)
	return c.texture()


func _gen_hedgehog() -> Texture2D:
	var c := PixelCanvas.new(30, 26)
	var m := Color("5a5e64")
	c.thick(Vector2(2, 25), Vector2(26, 2), 3, m)
	c.thick(Vector2(4, 2), Vector2(28, 25), 3, m.darkened(0.2))
	c.thick(Vector2(15, 1), Vector2(15, 25), 3, m.lightened(0.2))
	c.outline(OUT)
	return c.texture()


func _gen_tower() -> Texture2D:
	var c := PixelCanvas.new(64, 120)
	var wood := Color("7a5230")
	c.thick(Vector2(8, 119), Vector2(14, 44), 4, wood)
	c.thick(Vector2(56, 119), Vector2(50, 44), 4, wood)
	c.thick(Vector2(12, 60), Vector2(52, 100), 2, wood.darkened(0.25))
	c.thick(Vector2(52, 60), Vector2(12, 100), 2, wood.darkened(0.25))
	c.rect(10, 78, 44, 3, wood.darkened(0.15))
	c.rect(4, 40, 56, 5, wood.lightened(0.1))
	c.rect(6, 26, 3, 14, wood)
	c.rect(55, 26, 3, 14, wood)
	c.rect(8, 32, 48, 2, wood.darkened(0.1))
	c.poly(PackedVector2Array([Vector2(0, 26), Vector2(32, 10), Vector2(64, 26)]), Color("6a6a3a"))
	c.rect(20, 14, 24, 4, Color("7a7a4a"))
	# red flag
	c.rect(31, 0, 2, 12, Color("3a3c44"))
	c.rect(33, 0, 10, 6, Color("c83030"))
	c.outline(OUT)
	return c.texture()


func _gen_tower_broken() -> Texture2D:
	var c := PixelCanvas.new(64, 40)
	var wood := Color("4a3220")
	c.thick(Vector2(8, 39), Vector2(12, 16), 4, wood)
	c.thick(Vector2(56, 39), Vector2(50, 22), 4, wood)
	c.thick(Vector2(14, 30), Vector2(40, 36), 3, wood.darkened(0.3))
	c.outline(OUT)
	return c.texture()


func _gen_hut() -> Texture2D:
	var c := PixelCanvas.new(96, 72)
	var bamboo := Color("b8964a")
	c.rect(8, 26, 80, 46, bamboo)
	for x in range(8, 88, 5):
		c.vline(x, 26, 71, bamboo.darkened(0.25))
	c.rect(34, 42, 22, 30, Color("2a1a10"))
	c.rect(62, 36, 16, 12, Color("2a1a10"))
	c.poly(PackedVector2Array([Vector2(0, 30), Vector2(48, 0), Vector2(96, 30)]), Color("a08a4a"))
	for i in 12:
		c.line(i * 8, 30, 48, 2, Color("8a7440"))
	c.outline(OUT)
	return c.texture()


func _gen_bunker() -> Texture2D:
	var c := PixelCanvas.new(112, 56)
	var con := Color("8a8a84")
	c.poly(PackedVector2Array([Vector2(10, 8), Vector2(102, 8), Vector2(112, 56), Vector2(0, 56)]), con)
	c.rect(10, 8, 92, 4, con.lightened(0.2))
	c.rect(30, 22, 52, 8, Color("1a1a1a"))
	_rng.seed = 11
	c.noise(4, 12, 104, 44, con.darkened(0.2), 0.08, _rng)
	c.outline(OUT)
	return c.texture()


func _gen_building() -> Texture2D:
	var c := PixelCanvas.new(160, 128)
	var con := Color("7e8286")
	c.rect(0, 12, 160, 116, con)
	c.rect(0, 12, 160, 4, con.lightened(0.25))
	for row in 3:
		for col in 5:
			var x := 12 + col * 30
			var y := 26 + row * 32
			c.rect(x, y, 18, 16, Color("2a3440"))
			c.rect(x, y, 18, 3, Color("4a5a6a"))
			c.rect(x + 2, y + 5, 5, 8, Color("5a7a9a"))
	c.rect(64, 96, 32, 32, Color("3a3c44"))
	c.rect(0, 0, 160, 12, con.darkened(0.2))
	for i in 16:
		c.rect(i * 10, 0, 5, 4, con.darkened(0.4))
	c.rect(120, 40, 26, 40, Color("a82828"))
	c.circle(133, 58, 7, Color("f0e0d0"))
	c.circle(133, 58, 3, Color("a82828"))
	c.outline(OUT)
	return c.texture()


func _gen_wall_segment() -> Texture2D:
	var c := PixelCanvas.new(32, 96)
	var con := Color("8a8e92")
	c.rect(0, 0, 32, 96, con)
	for y in range(0, 96, 12):
		c.hline(0, 31, y, con.darkened(0.25))
		var off := 0 if (y / 12) % 2 == 0 else 16
		c.vline(off, y, y + 11, con.darkened(0.25))
	c.rect(0, 0, 32, 3, con.lightened(0.2))
	c.rect(4, 40, 24, 16, Color("a82828"))
	c.outline(OUT)
	return c.texture()


func _gen_palm() -> Texture2D:
	var c := PixelCanvas.new(80, 128)
	var trunk := Color("9a7a4a")
	for i in 22:
		var t := i / 21.0
		var x := 40.0 + sin(t * 2.2) * 10.0 - t * 4.0
		var y := 127.0 - t * 100.0
		c.rect(int(x) - 3, int(y) - 3, 7, 6, trunk if i % 2 == 0 else trunk.darkened(0.2))
	var top := Vector2(36, 26)
	var leaf := Color("3f8a34")
	for a in [-2.8, -2.2, -1.5, -0.9, -0.3, 0.2]:
		for k in 16:
			var p := top + Vector2(cos(a), sin(a) * 0.5) * k * 2.2 + Vector2(0, k * k * 0.06)
			c.rect(int(p.x) - 2, int(p.y) - 1, 5, 3, leaf if k % 3 != 0 else leaf.lightened(0.2))
	c.circle(38, 30, 4, Color("6a4a2a"))
	c.circle(33, 31, 3, Color("6a4a2a"))
	c.outline(OUT)
	return c.texture()


func _gen_jungle_tree() -> Texture2D:
	var c := PixelCanvas.new(120, 180)
	var trunk := Color("5a3e26")
	c.rect(52, 70, 16, 110, trunk)
	c.rect(52, 70, 4, 110, trunk.lightened(0.15))
	c.thick(Vector2(60, 120), Vector2(90, 90), 5, trunk)
	c.thick(Vector2(60, 110), Vector2(28, 84), 5, trunk)
	var leaf := Color("2f6e2a")
	_rng.seed = 5
	for i in 26:
		var cx := _rng.randf_range(10, 110)
		var cy := _rng.randf_range(8, 90)
		c.circle(cx, cy, _rng.randf_range(10, 18), leaf.darkened(_rng.randf() * 0.25))
	for i in 20:
		c.circle(_rng.randf_range(15, 105), _rng.randf_range(10, 80), 3, leaf.lightened(0.25))
	c.vline(20, 60, 130, Color("3f7a2a"))
	c.vline(96, 70, 150, Color("3f7a2a"))
	c.outline(OUT)
	return c.texture()


func _gen_bush() -> Texture2D:
	var c := PixelCanvas.new(56, 28)
	var leaf := Color("3a7a30")
	for i in 7:
		c.circle(6 + i * 7.5, 18 - (i % 2) * 4, 9, leaf.darkened((i % 3) * 0.08))
	c.rect(0, 22, 56, 6, leaf.darkened(0.2))
	c.px(12, 8, leaf.lightened(0.3))
	c.px(30, 6, leaf.lightened(0.3))
	c.px(44, 9, leaf.lightened(0.3))
	c.outline(OUT)
	return c.texture()


func _gen_foliage() -> Texture2D:
	# big overlapping canopy that hides secret areas
	var c := PixelCanvas.new(160, 96)
	_rng.seed = 9
	var leaf := Color("2a6026")
	for i in 40:
		c.circle(_rng.randf_range(10, 150), _rng.randf_range(10, 86), _rng.randf_range(10, 20), leaf.darkened(_rng.randf() * 0.2))
	for i in 30:
		c.circle(_rng.randf_range(10, 150), _rng.randf_range(10, 86), 3, leaf.lightened(0.25))
	c.outline(OUT)
	return c.texture()


func _gen_rock() -> Texture2D:
	var c := PixelCanvas.new(40, 24)
	var r := Color("7a7068")
	c.ellipse(20, 16, 19, 10, r)
	c.ellipse(16, 12, 10, 6, r.lightened(0.15))
	c.px(26, 16, r.darkened(0.3))
	c.outline(OUT)
	return c.texture()


func _gen_boat() -> Texture2D:
	var c := PixelCanvas.new(140, 48)
	var hull := Color("5a6a4a")
	c.poly(PackedVector2Array([Vector2(0, 14), Vector2(120, 14), Vector2(140, 30), Vector2(128, 46), Vector2(8, 46)]), hull)
	c.rect(0, 14, 120, 3, hull.lightened(0.25))
	c.rect(4, 26, 124, 3, hull.darkened(0.3))
	c.rect(118, 4, 18, 12, hull.darkened(0.1))
	PixelFont.paint_glyph(c, "7", 60, 30, Color("f0f0e0"))
	c.outline(OUT)
	return c.texture()


func _gen_flag() -> Texture2D:
	var c := PixelCanvas.new(28, 64)
	c.rect(1, 0, 2, 64, Color("5a5e64"))
	c.rect(3, 2, 22, 14, Color("c83030"))
	c.circle(13, 9, 4, Color("f0e0d0"))
	c.circle(13, 9, 2, Color("c83030"))
	c.outline(OUT)
	return c.texture()


func _gen_sign() -> Texture2D:
	var c := PixelCanvas.new(40, 36)
	c.rect(18, 14, 4, 22, Color("6a4a2a"))
	c.rect(0, 0, 40, 16, Color("b8964a"))
	PixelFont.paint_text(c, "GO!", 8, 4, Color("a82828"))
	c.outline(OUT)
	return c.texture()


func _gen_radar() -> Texture2D:
	var c := PixelCanvas.new(64, 72)
	c.thick(Vector2(32, 71), Vector2(32, 30), 4, Color("5a5e64"))
	c.thick(Vector2(16, 71), Vector2(32, 40), 2, Color("5a5e64"))
	c.thick(Vector2(48, 71), Vector2(32, 40), 2, Color("5a5e64"))
	c.ellipse(32, 20, 26, 16, Color("9a9ea4"))
	c.ellipse(34, 18, 18, 10, Color("6a6e74"))
	c.rect(32, 10, 2, 12, Color("3a3c44"))
	c.outline(OUT)
	return c.texture()


func _gen_hostage_rope() -> Texture2D:
	var c := PixelCanvas.new(6, 40)
	c.rect(2, 0, 2, 40, Color("c8a060"))
	c.px(2, 8, Color("8a6a30"))
	c.px(3, 20, Color("8a6a30"))
	c.px(2, 30, Color("8a6a30"))
	return c.texture()


func _gen_parachute() -> Texture2D:
	var c := PixelCanvas.new(40, 30)
	c.ellipse(20, 10, 19, 9, Color("e0e0d0"))
	c.rect(0, 10, 40, 10, Color(0, 0, 0, 0))
	for i in 5:
		c.rect(2 + i * 8, 2, 4, 8, Color("c8b890"))
	c.line(2, 10, 17, 29, Color("888070"))
	c.line(38, 10, 23, 29, Color("888070"))
	c.line(20, 10, 20, 29, Color("888070"))
	c.outline(OUT)
	return c.texture()


func _gen_shield() -> Texture2D:
	var c := PixelCanvas.new(10, 36)
	c.rect(2, 0, 7, 36, Color("7a8a9a"))
	c.rect(2, 0, 2, 36, Color("aab8c8"))
	c.rect(4, 6, 4, 6, Color("2a3440"))
	c.rect(3, 16, 5, 2, Color("e0c040"))
	c.outline(OUT)
	return c.texture()


func _gen_mg_nest() -> Texture2D:
	var c := PixelCanvas.new(48, 22)
	var sand := Color("c8b078")
	for i in 4:
		c.ellipse(6 + i * 12, 16, 7, 5, sand.darkened((i % 2) * 0.1))
	for i in 3:
		c.ellipse(12 + i * 12, 8, 7, 5, sand.darkened(((i + 1) % 2) * 0.1))
	c.outline(OUT)
	return c.texture()


func _gen_turret_base() -> Texture2D:
	var c := PixelCanvas.new(32, 22)
	var m := Color("5a5e64")
	c.rect(2, 10, 28, 12, m)
	c.rect(0, 18, 32, 4, m.darkened(0.3))
	c.ellipse(16, 10, 11, 8, m.lightened(0.15))
	c.circle(16, 9, 3, Color("ff4030"))
	c.outline(OUT)
	return c.texture()


func _gen_turret_gun() -> Texture2D:
	var c := PixelCanvas.new(26, 8)
	c.rect(0, 1, 8, 6, Color("4a4f58"))
	c.rect(8, 2, 16, 4, Color("2a2a30"))
	c.rect(22, 1, 4, 6, Color("3a3c44"))
	c.outline(OUT)
	return c.texture()


func _gen_debris_wood() -> Texture2D:
	var c := PixelCanvas.new(6, 3)
	c.rect(0, 0, 6, 3, Color("a8743a"))
	return c.texture()


# ================================================================ TERRAIN
func tile(style: String, part: String) -> Texture2D:
	return cached("tile_%s_%s" % [style, part], _make_tile.bind(style, part))


func _make_tile(style: String, part: String) -> Texture2D:
	var c := PixelCanvas.new(32, 32)
	_rng.seed = hash(style + part)
	match style:
		"sand":
			if part == "top":
				c.rect(0, 0, 32, 32, Color("dfbd70"))
				c.rect(0, 0, 32, 3, Color("f5dc98"))
				c.noise(0, 3, 32, 29, Color("c9a15a"), 0.12, _rng)
				c.noise(0, 3, 32, 29, Color("f5dc98"), 0.05, _rng)
			else:
				c.rect(0, 0, 32, 32, Color("c29a55"))
				c.noise(0, 0, 32, 32, Color("a8844a"), 0.15, _rng)
				c.noise(0, 0, 32, 32, Color("8a6a3a"), 0.04, _rng)
		"jungle":
			if part == "top":
				c.rect(0, 0, 32, 32, Color("6b4a2a"))
				c.rect(0, 0, 32, 7, Color("4f9a3a"))
				for x in 32:
					var hgt := _rng.randi_range(0, 4)
					c.vline(x, 7, 7 + hgt, Color("3f7a2a"))
				c.rect(0, 0, 32, 2, Color("7aca5a"))
				c.noise(0, 12, 32, 20, Color("5a3a20"), 0.15, _rng)
			else:
				c.rect(0, 0, 32, 32, Color("5e4026"))
				c.noise(0, 0, 32, 32, Color("4a321e"), 0.18, _rng)
				c.noise(0, 0, 32, 32, Color("7a5a3a"), 0.05, _rng)
		"concrete":
			c.rect(0, 0, 32, 32, Color("7c7e80"))
			c.hline(0, 31, 15, Color("64666a"))
			c.vline(0, 0, 31, Color("64666a"))
			c.vline(16, 16, 31, Color("64666a"))
			c.noise(0, 0, 32, 32, Color("6a6c70"), 0.08, _rng)
			if part == "top":
				c.rect(0, 0, 32, 4, Color("a0a2a4"))
				c.rect(0, 4, 32, 1, Color("54565a"))
				for i in 4:
					c.rect(i * 8, 5, 4, 2, Color("e0b020"))
		"metal":
			c.rect(0, 0, 32, 32, Color("5a5e64"))
			c.rect(0, 0, 32, 2, Color("7a7e84"))
			c.rect(0, 30, 32, 2, Color("3a3c44"))
			for p in [Vector2i(3, 4), Vector2i(28, 4), Vector2i(3, 27), Vector2i(28, 27)]:
				c.px(p.x, p.y, Color("9a9ea4"))
		"wood":
			c.rect(0, 0, 32, 32, Color("8a5a2a"))
			for y in [0, 8, 16, 24]:
				c.hline(0, 31, y, Color("5a3a1a"))
			c.rect(0, 1, 32, 1, Color("a8743a"))
			c.vline(10, 1, 7, Color("5a3a1a"))
			c.vline(24, 9, 15, Color("5a3a1a"))
		"rock":
			c.rect(0, 0, 32, 32, Color("6a625a"))
			c.noise(0, 0, 32, 32, Color("5a524a"), 0.2, _rng)
			c.noise(0, 0, 32, 32, Color("8a827a"), 0.06, _rng)
			if part == "top":
				c.rect(0, 0, 32, 4, Color("4f9a3a"))
				c.rect(0, 0, 32, 1, Color("7aca5a"))
	return c.texture()


func _gen_plank() -> Texture2D:
	var c := PixelCanvas.new(16, 10)
	c.rect(0, 0, 16, 8, Color("8a5a2a"))
	c.rect(0, 0, 16, 2, Color("b07a40"))
	c.vline(0, 0, 7, Color("5a3a1a"))
	c.rect(0, 8, 16, 2, Color("5a3a1a"))
	c.px(7, 4, Color("3a2a1a"))
	return c.texture()


func _gen_bridge_post() -> Texture2D:
	var c := PixelCanvas.new(8, 80)
	c.rect(1, 0, 6, 80, Color("5a3a1a"))
	c.rect(1, 0, 2, 80, Color("7a5230"))
	c.outline(OUT)
	return c.texture()


# ================================================================ BACKGROUND
func _gen_sky() -> Texture2D:
	var img := Image.create(4, 360, false, Image.FORMAT_RGBA8)
	var top := Color("2a6ac8")
	var mid := Color("6aaee8")
	var bot := Color("cfe8f0")
	for y in 360:
		var t := y / 359.0
		var col := top.lerp(mid, t * 1.6) if t < 0.62 else mid.lerp(bot, (t - 0.62) / 0.38)
		# banding for a retro look
		col = Color(snappedf(col.r, 0.03), snappedf(col.g, 0.03), snappedf(col.b, 0.03))
		for x in 4:
			img.set_pixel(x, y, col)
	return ImageTexture.create_from_image(img)


func _gen_cloud() -> Texture2D:
	var c := PixelCanvas.new(96, 36)
	var wh := Color("f4f8ff")
	c.ellipse(30, 22, 22, 11, wh)
	c.ellipse(52, 16, 22, 14, wh)
	c.ellipse(72, 24, 20, 10, wh)
	c.rect(10, 26, 80, 8, wh)
	c.rect(10, 30, 80, 4, Color("c8d8ec"))
	return c.texture()


func _gen_sun() -> Texture2D:
	var c := PixelCanvas.new(40, 40)
	c.circle(20, 20, 18, Color("fff0b0"))
	c.circle(20, 20, 15, Color("fffae0"))
	return c.texture()


func _gen_far_mountains() -> Texture2D:
	var w := 640
	var c := PixelCanvas.new(w, 160)
	_rng.seed = 3
	# distant mountains / islands
	var base := Color("7a9ab8")
	var hts: Array = []
	for x in w:
		var t := x / float(w) * TAU
		var hgt := 60.0 + 30.0 * sin(t * 2.0) + 20.0 * sin(t * 5.0 + 1.3) + 8.0 * sin(t * 13.0)
		hts.append(hgt)
		c.vline(x, int(120 - hgt), 120, base)
	for x in w:
		var hgt: float = hts[x]
		if x % 2 == 0:
			c.px(x, int(121 - hgt), base.lightened(0.25))
	# sea
	c.rect(0, 120, w, 40, Color("3a8ac0"))
	for i in 60:
		var x := _rng.randi_range(0, w - 10)
		var y := _rng.randi_range(122, 158)
		c.hline(x, x + _rng.randi_range(3, 9), y, Color("8ac8e8"))
	return c.texture()


func _gen_mid_jungle() -> Texture2D:
	var w := 640
	var c := PixelCanvas.new(w, 200)
	_rng.seed = 21
	var dark := Color("2f5a3a")
	for i in 70:
		var x := _rng.randf_range(0, w)
		var y := _rng.randf_range(40, 140)
		var r := _rng.randf_range(16, 34)
		c.circle(x, y, r, dark.darkened(_rng.randf() * 0.2))
		# wrap
		if x < r:
			c.circle(x + w, y, r, dark)
		elif x > w - r:
			c.circle(x - w, y, r, dark)
	c.rect(0, 130, w, 70, dark.darkened(0.15))
	for i in 20:
		var x := _rng.randi_range(0, w)
		c.rect(x, 110, 4, 90, Color("25402a"))
	return c.texture()


func _gen_mid_palms() -> Texture2D:
	var w := 640
	var c := PixelCanvas.new(w, 200)
	var col := Color("4a7a6a")
	_rng.seed = 33
	for i in 9:
		var bx := 20.0 + i * 72.0 + _rng.randf_range(-12, 12)
		for k in 30:
			var t := k / 29.0
			c.rect(int(bx + sin(t * 2.0) * 8.0), int(198 - t * 120), 4, 5, col.darkened(0.1))
		var top := Vector2(bx + sin(2.0) * 8.0, 78)
		for a in [-2.8, -2.1, -1.4, -0.8, -0.2]:
			for k in 12:
				var p := top + Vector2(cos(a), sin(a) * 0.5) * k * 2.4 + Vector2(0, k * k * 0.08)
				c.rect(int(p.x), int(p.y), 4, 3, col)
	c.rect(0, 186, w, 14, col.darkened(0.2))
	return c.texture()


func _gen_mid_base() -> Texture2D:
	var w := 640
	var c := PixelCanvas.new(w, 200)
	var col := Color("6a7488")
	var d := col.darkened(0.2)
	# fortress blocks, towers, radar
	c.rect(0, 120, w, 80, d)
	for i in 6:
		var x := 10 + i * 108
		var hgt := 60 + (i * 37) % 50
		c.rect(x, 200 - hgt - 60, 70, hgt + 60, col)
		for j in 3:
			c.rect(x + 8 + j * 20, 200 - hgt - 50, 10, 6, Color("e0a040"))
	c.rect(300, 20, 8, 180, col.darkened(0.1))
	c.thick(Vector2(284, 200), Vector2(304, 30), 3, col.darkened(0.1))
	c.thick(Vector2(324, 200), Vector2(304, 30), 3, col.darkened(0.1))
	c.ellipse(560, 70, 30, 18, col.lightened(0.1))
	c.rect(556, 70, 6, 60, col)
	for i in 4:
		c.rect(40 + i * 150, 90, 3, 30, col)
		c.rect(43 + i * 150, 90, 14, 8, Color("a84040"))
	return c.texture()
