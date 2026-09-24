class_name TerrainBlock
extends StaticBody2D
## Solid (or one-way) rectangle of ground drawn with tiling pixel textures.

var size := Vector2(64, 64)
var style := "sand"
var one_way := false
var _shape: CollisionShape2D


func setup(rect: Rect2, s: String, oneway: bool = false) -> TerrainBlock:
	position = rect.position
	size = rect.size
	style = s
	one_way = oneway
	return self


func _ready() -> void:
	collision_layer = Combat.L_ONEWAY if one_way else Combat.L_WORLD
	collision_mask = 0
	texture_repeat = CanvasItem.TEXTURE_REPEAT_ENABLED
	_shape = CollisionShape2D.new()
	var r := RectangleShape2D.new()
	r.size = Vector2(size.x, 8.0) if one_way else size
	_shape.shape = r
	_shape.position = Vector2(size.x * 0.5, 4.0) if one_way else size * 0.5
	_shape.one_way_collision = one_way
	add_child(_shape)
	z_index = -5


func remove_collision() -> void:
	if _shape:
		_shape.set_deferred("disabled", true)


func _draw() -> void:
	if one_way:
		_draw_platform()
		return
	var top := Art.tile(style, "top")
	var fill := Art.tile(style, "fill")
	var top_h := minf(32.0, size.y)
	draw_texture_rect(top, Rect2(0, 0, size.x, top_h), true)
	if size.y > 32.0:
		draw_texture_rect(fill, Rect2(0, 32, size.x, size.y - 32.0), true)
	# darker side edges to read as solid blocks
	draw_rect(Rect2(0, 0, 2, size.y), Color(0, 0, 0, 0.25))
	draw_rect(Rect2(size.x - 2, 0, 2, size.y), Color(0, 0, 0, 0.25))


func _draw_platform() -> void:
	match style:
		"wood", "bridge":
			var plank := Art.tex("plank")
			draw_texture_rect(plank, Rect2(0, 0, size.x, 10), true)
			for x in range(8, int(size.x), 48):
				draw_rect(Rect2(x, 10, 3, 14), Color("5a3a1a"))
		"branch":
			draw_rect(Rect2(0, 2, size.x, 6), Color("5a3e26"))
			draw_rect(Rect2(0, 2, size.x, 2), Color("7a5a3a"))
			for x in range(0, int(size.x), 12):
				draw_circle(Vector2(x + 6, 1), 5, Color("2f6e2a"))
				draw_circle(Vector2(x + 3, -1), 2, Color("4f9a3a"))
		"metal":
			draw_rect(Rect2(0, 0, size.x, 6), Color("5a5e64"))
			draw_rect(Rect2(0, 0, size.x, 2), Color("8a8e94"))
			for x in range(0, int(size.x), 16):
				draw_line(Vector2(x, 6), Vector2(x + 8, 14), Color("3a3c44"), 2.0)
				draw_line(Vector2(x + 16, 6), Vector2(x + 8, 14), Color("3a3c44"), 2.0)
		_:
			draw_rect(Rect2(0, 0, size.x, 8), Color("7c7e80"))
			draw_rect(Rect2(0, 0, size.x, 2), Color("a0a2a4"))
			draw_rect(Rect2(0, 7, size.x, 1), Color("1a1420"))
