class_name ParticleLayer
extends Node2D
## Lightweight pooled particle system.
##
## Thousands of particles live in fixed-size packed arrays (struct-of-arrays)
## and are drawn by this one node, so shells, sparks, smoke and debris never
## allocate nodes at runtime.

enum { SQUARE, SMOKE, FIRE, SHELL, SPARK, MUZZLE, FLAME, STAR, DROP }

const CAP := 3000

var _pos := PackedVector2Array()
var _vel := PackedVector2Array()
var _life := PackedFloat32Array()
var _max := PackedFloat32Array()
var _size := PackedFloat32Array()
var _size2 := PackedFloat32Array()
var _grav := PackedFloat32Array()
var _drag := PackedFloat32Array()
var _floor := PackedFloat32Array()
var _col := PackedColorArray()
var _col2 := PackedColorArray()
var _type := PackedInt32Array()
var _free := PackedInt32Array()
var _active := PackedInt32Array()

const FIRE_RAMP := [Color("ffffff"), Color("fff27a"), Color("ffb62e"), Color("f2641e"), Color("b8301c"), Color("402020")]


func _init() -> void:
	_pos.resize(CAP)
	_vel.resize(CAP)
	_life.resize(CAP)
	_max.resize(CAP)
	_size.resize(CAP)
	_size2.resize(CAP)
	_grav.resize(CAP)
	_drag.resize(CAP)
	_floor.resize(CAP)
	_col.resize(CAP)
	_col2.resize(CAP)
	_type.resize(CAP)
	_free.resize(CAP)
	for i in CAP:
		_free[i] = CAP - 1 - i


func count() -> int:
	return _active.size()


func spawn(type: int, p: Vector2, v: Vector2, life: float, size: float, col: Color,
		grav: float = 0.0, drag: float = 0.0, size2: float = -1.0, col2: Color = Color(0, 0, 0, 0),
		floor_y: float = INF) -> void:
	var n := _free.size()
	if n == 0:
		return
	var i := _free[n - 1]
	_free.resize(n - 1)
	_type[i] = type
	_pos[i] = p
	_vel[i] = v
	_life[i] = life
	_max[i] = life
	_size[i] = size
	_size2[i] = size if size2 < 0.0 else size2
	_col[i] = col
	_col2[i] = col if col2.a == 0.0 and col2.r == 0.0 else col2
	_grav[i] = grav
	_drag[i] = drag
	_floor[i] = floor_y
	_active.append(i)


func clear() -> void:
	for i in _active:
		_free.append(i)
	_active.resize(0)


func _process(delta: float) -> void:
	var n := _active.size()
	var k := 0
	while k < n:
		var i := _active[k]
		var l := _life[i] - delta
		if l <= 0.0:
			_free.append(i)
			n -= 1
			_active[k] = _active[n]
			continue
		_life[i] = l
		var v := _vel[i]
		v.y += _grav[i] * delta
		if _drag[i] > 0.0:
			v *= maxf(0.0, 1.0 - _drag[i] * delta)
		var p := _pos[i] + v * delta
		if p.y > _floor[i]:
			p.y = _floor[i]
			if absf(v.y) > 40.0:
				v.y = -v.y * 0.4
				v.x *= 0.6
			else:
				v = Vector2.ZERO
		_pos[i] = p
		_vel[i] = v
		k += 1
	_active.resize(n)
	queue_redraw()


static func fire_color(t: float) -> Color:
	var f := clampf(t, 0.0, 0.999) * (FIRE_RAMP.size() - 1)
	var i := int(f)
	return FIRE_RAMP[i].lerp(FIRE_RAMP[i + 1], f - i)


func _draw() -> void:
	var view := CameraManager.view_rect().grow(48)
	for i in _active:
		var p := _pos[i]
		if not view.has_point(p):
			continue
		var t := 1.0 - _life[i] / _max[i]
		var s := lerpf(_size[i], _size2[i], t)
		match _type[i]:
			SQUARE:
				var c := _col[i].lerp(_col2[i], t)
				draw_rect(Rect2(p.round() - Vector2(s, s) * 0.5, Vector2(s, s)), c)
			SMOKE:
				var c := _col[i].lerp(_col2[i], t)
				c.a *= 1.0 - t * t
				draw_circle(p.round(), s, c)
			FIRE:
				draw_circle(p.round(), s, fire_color(t * 0.9 + 0.05))
			FLAME:
				var flick := 0.7 + 0.3 * sin(_life[i] * 40.0 + i)
				var fs := s * flick * (1.0 - t * 0.6)
				draw_circle(p.round(), fs, fire_color(0.25 + t * 0.5))
				draw_circle((p + Vector2(0, -fs * 0.5)).round(), fs * 0.55, fire_color(0.1))
			SHELL:
				var horizontal := int(_life[i] * 30.0) % 2 == 0
				draw_rect(Rect2(p.round(), Vector2(3, 2) if horizontal else Vector2(2, 3)), _col[i])
			SPARK:
				var v := _vel[i]
				var c := _col[i].lerp(_col2[i], t)
				draw_line(p.round(), (p - v * 0.025).round(), c, 1.0)
			MUZZLE:
				var d := _vel[i].normalized()
				var nrm := Vector2(-d.y, d.x)
				var ss := s * (1.0 - t * 0.5)
				var pts := PackedVector2Array([p - d * ss * 0.3, p + nrm * ss * 0.45, p + d * ss * 1.4, p - nrm * ss * 0.45])
				draw_colored_polygon(pts, Color("ffd24a"))
				draw_circle(p + d * ss * 0.2, ss * 0.35, Color("fffbe0"))
			STAR:
				var c := _col[i]
				c.a = 1.0 - t
				draw_rect(Rect2(p.round() - Vector2(1, 0), Vector2(3, 1)), c)
				draw_rect(Rect2(p.round() - Vector2(0, 1), Vector2(1, 3)), c)
			DROP:
				draw_rect(Rect2(p.round(), Vector2(2, 2)), _col[i])
