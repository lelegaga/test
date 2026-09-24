class_name TextPopup
extends Node2D
## Pooled floating text ("+100", "THANK YOU!").

var text := ""
var col := Color.WHITE
var t := 0.0
var scale_i := 1
const LIFE := 0.9


func activate(pos: Vector2, s: String, c: Color, sc: int) -> void:
	global_position = pos
	text = s
	col = c
	scale_i = sc
	t = 0.0
	z_index = 40
	queue_redraw()


func _process(delta: float) -> void:
	t += delta
	position.y -= 30.0 * delta * (1.0 - t / LIFE)
	if t >= LIFE:
		ObjectPool.release(self)
		return
	queue_redraw()


func _draw() -> void:
	var c := col
	if t > LIFE * 0.6:
		c.a = 1.0 - (t - LIFE * 0.6) / (LIFE * 0.4)
	PixelFont.draw_centered(self, text, Vector2.ZERO, scale_i, c)
