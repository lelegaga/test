class_name WaterZone
extends Node2D
## Animated water surface. Falling in is deadly (see Level.is_water_at).

var rect := Rect2()
var _t := 0.0


func setup(r: Rect2) -> WaterZone:
	rect = r
	return self


func _ready() -> void:
	z_index = 12


func _process(delta: float) -> void:
	_t += delta
	if CameraManager.view_rect().grow(32).intersects(rect):
		queue_redraw()


func _draw() -> void:
	var deep := Color(0.12, 0.35, 0.55, 0.82)
	var mid := Color(0.2, 0.52, 0.72, 0.75)
	draw_rect(Rect2(rect.position + Vector2(0, 6), rect.size - Vector2(0, 6)), deep)
	var x := rect.position.x
	while x < rect.end.x:
		var h := 3.0 + 2.0 * sin(x * 0.08 + _t * 3.0)
		draw_rect(Rect2(x, rect.position.y + 6 - h, 4, h + 2), mid)
		if int(x / 4.0 + _t * 6.0) % 11 == 0:
			draw_rect(Rect2(x, rect.position.y + 6 - h, 4, 1), Color(0.85, 0.95, 1.0, 0.9))
		x += 4.0
	for i in 6:
		var yy := rect.position.y + 16 + i * 10
		var xx := rect.position.x + fmod(i * 97.0 + _t * (12 + i * 3), maxf(rect.size.x - 20, 1.0))
		draw_rect(Rect2(xx, yy, 10, 1), Color(0.6, 0.85, 1.0, 0.5))
