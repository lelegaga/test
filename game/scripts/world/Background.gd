class_name Background
extends ParallaxBackground
## Multi-layer parallax: sky, sun, drifting clouds, distant islands & sea,
## and a mid layer whose content changes with the level zone
## (beach palms -> jungle -> military base).

var zones: Array = []   # [[start_x, "palms"|"jungle"|"base"], ...]
var level_length := 12000.0
var _clouds: ParallaxLayer


func setup(zone_list: Array, length: float) -> Background:
	zones = zone_list
	level_length = length
	return self


func _zone_at(x: float) -> String:
	var z := "palms"
	for e in zones:
		if x >= e[0]:
			z = e[1]
	return z


func _ready() -> void:
	layer = -10
	# sky: fixed to the screen
	var sky_layer := _layer(Vector2(0, 0))
	var sky := Sprite2D.new()
	sky.texture = Art.tex("sky")
	sky.centered = false
	sky.scale = Vector2(160, 1.2)
	sky.position = Vector2(0, -40)
	sky_layer.add_child(sky)
	var sun := Sprite2D.new()
	sun.texture = Art.tex("sun")
	sun.position = Vector2(470, 70)
	sky_layer.add_child(sun)

	_clouds = _layer(Vector2(0.08, 0.05))
	_clouds.motion_mirroring = Vector2(1280, 0)
	for i in 7:
		var c := Sprite2D.new()
		c.texture = Art.tex("cloud")
		c.position = Vector2(i * 190 + (i % 3) * 30, 40 + (i * 37) % 70)
		c.scale = Vector2.ONE * (1.0 if i % 2 == 0 else 0.7)
		c.modulate.a = 0.9
		_clouds.add_child(c)

	var far := _layer(Vector2(0.18, 0.08))
	far.motion_mirroring = Vector2(640, 0)
	var fm := Sprite2D.new()
	fm.texture = Art.tex("far_mountains")
	fm.centered = false
	fm.position = Vector2(0, 110)
	far.add_child(fm)

	var mid := _layer(Vector2(0.42, 0.15))
	var chunk_world := 640.0 / 0.42
	var n := int(level_length / chunk_world) + 3
	for i in n:
		var z := _zone_at(i * chunk_world + chunk_world * 0.5)
		var s := Sprite2D.new()
		s.texture = Art.tex("mid_" + z)
		s.centered = false
		s.position = Vector2(i * 640, 160)
		mid.add_child(s)
	# a solid strip below the mid layer so no sky shows through when the camera rises
	var fill := ColorRect.new()
	fill.color = Color("25402a")
	fill.position = Vector2(0, 358)
	fill.size = Vector2(n * 640, 400)
	mid.add_child(fill)


func _layer(scale: Vector2) -> ParallaxLayer:
	var l := ParallaxLayer.new()
	l.motion_scale = scale
	add_child(l)
	return l


func _process(delta: float) -> void:
	if _clouds:
		_clouds.motion_offset.x -= 6.0 * delta
