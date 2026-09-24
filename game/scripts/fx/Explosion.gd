class_name Explosion
extends Node2D
## Multi-stage pooled explosion:
##   white flash -> fireball blobs -> shockwave ring -> black smoke
##   -> metal debris -> lingering ground flames.
## Damage is applied separately by FX.boom() through Combat.explode().

var radius := 24.0
var t := 0.0
var duration := 0.6
var _blobs: Array = []   # [offset: Vector2, r: float, delay: float]
var _smoke_done := false
var _flames_done := false
var _ground_y := INF
var _big := false


func activate(pos: Vector2, r: float, big: bool) -> void:
	global_position = pos
	radius = r
	t = 0.0
	_big = big
	duration = 0.5 + r * 0.004
	_smoke_done = false
	_flames_done = false
	z_index = 20
	_blobs.clear()
	var n := 6 + int(r / 8.0)
	for i in n:
		var off := Vector2(randf_range(-1, 1), randf_range(-1, 0.6)) * r * 0.45
		_blobs.append([off, r * randf_range(0.28, 0.5), randf_range(0.0, 0.07)])
	_ground_y = Combat.ground_below(pos + Vector2(0, -4), r + 24.0)
	var layer: ParticleLayer = FX.front
	# metal debris and sparks right away
	var nd := 6 + int(r / 4.0)
	for i in nd:
		var a := randf_range(-PI, 0.0)
		var sp := randf_range(120, 320) * (0.6 + r / 60.0)
		var col := Color("3a3c44") if i % 3 else Color("8a8f98")
		layer.spawn(ParticleLayer.SQUARE, pos, Vector2(cos(a), sin(a)) * sp, randf_range(0.6, 1.2), randf_range(2, 4), col, 700, 0.5, -1, Color(0, 0, 0, 0), _ground_y)
	for i in 8 + int(r / 5.0):
		var a := randf() * TAU
		layer.spawn(ParticleLayer.SPARK, pos, Vector2(cos(a), sin(a)) * randf_range(150, 380), randf_range(0.15, 0.35), 1, Color("fff4a0"), 300, 2.0, -1, Color("ff6020"))
	queue_redraw()


func _process(delta: float) -> void:
	t += delta
	if not _smoke_done and t > 0.1:
		_smoke_done = true
		for i in 4 + int(radius / 6.0):
			var off := Vector2(randf_range(-1, 1), randf_range(-1, 0.3)) * radius * 0.5
			FX.back.spawn(ParticleLayer.SMOKE, global_position + off, Vector2(randf_range(-15, 15), randf_range(-45, -15)),
				randf_range(0.9, 1.8), radius * 0.2, Color(0.12, 0.1, 0.1, 0.9), -10, 0.8, radius * 0.45, Color(0.35, 0.33, 0.33, 0.6))
	if not _flames_done and t > 0.18:
		_flames_done = true
		if _ground_y != INF:
			for i in 2 + int(radius / 12.0):
				var x := global_position.x + randf_range(-radius, radius) * 0.7
				FX.back.spawn(ParticleLayer.FLAME, Vector2(x, _ground_y - 3), Vector2.ZERO, randf_range(0.8, 2.0), randf_range(3, 5), Color.WHITE)
	if t >= duration:
		ObjectPool.release(self)
		return
	queue_redraw()


func _draw() -> void:
	# 1. initial white flash
	if t < 0.06:
		draw_circle(Vector2.ZERO, radius * (0.5 + t * 6.0), Color(1, 1, 0.9))
	# 2. shockwave ring
	if t < 0.28:
		var k := t / 0.28
		var rr := lerpf(radius * 0.3, radius * 1.7, k)
		draw_arc(Vector2.ZERO, rr, 0, TAU, 32, Color(1, 0.95, 0.8, 1.0 - k), 3.0 - k * 2.0)
	# 3. fireball blobs grow then shrink while cooling down the fire ramp
	for b in _blobs:
		var bt: float = t - b[2]
		if bt < 0.0:
			continue
		var life := duration * 0.85
		var k := bt / life
		if k >= 1.0:
			continue
		var grow := minf(bt / 0.07, 1.0)
		var r: float = b[1] * grow * (1.0 - k * k * 0.8)
		var off: Vector2 = b[0] + Vector2(0, -bt * radius * 0.6)
		var col := ParticleLayer.fire_color(k * 1.1)
		draw_circle(off.round(), r, col)
		if k < 0.45:
			draw_circle((off + Vector2(0, -r * 0.2)).round(), r * 0.55, ParticleLayer.fire_color(k * 0.6))
