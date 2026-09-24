class_name Bridge
extends Node2D
## Wooden bridge made of one-way plank segments on posts. Scripted events can
## blow it up section by section (`collapse_chase`) for a cinematic escape.

var x0 := 0.0
var x1 := 0.0
var y := 292.0
var seg_w := 48.0
var segments: Array = []   # [TerrainBlock, alive]


func setup(from_x: float, to_x: float, top_y: float) -> Bridge:
	x0 = from_x
	x1 = to_x
	y = top_y
	return self


func _ready() -> void:
	var lvl: Level = GameManager.level
	var x := x0
	while x < x1:
		var w := minf(seg_w, x1 - x)
		var b := lvl.platform(x, y, w, "bridge")
		segments.append([b, true])
		x += w
	# posts and rope rail (decor)
	var px := x0
	while px <= x1:
		lvl.decor("bridge_post", Vector2(px, y + 80.0))
		px += 96.0
	z_index = -4


func _draw() -> void:
	# rope rail
	var px := x0
	while px < x1:
		var e := minf(px + 96.0, x1)
		var alive_here := _segment_alive_at(px + 10.0) and _segment_alive_at(e - 10.0)
		if alive_here:
			var mid := Vector2((px + e) * 0.5, y - 10.0)
			draw_polyline(PackedVector2Array([Vector2(px, y - 20), mid, Vector2(e, y - 20)]), Color("c8a060"), 1.0)
		px += 96.0


func _segment_alive_at(x: float) -> bool:
	for s in segments:
		var b: TerrainBlock = s[0]
		if x >= b.position.x and x <= b.position.x + b.size.x:
			return s[1]
	return false


## Explodes segments from `from_x` rightwards at `speed` px/s until `to_x`.
func collapse_chase(from_x: float, to_x: float, speed: float) -> void:
	GameManager.show_message("THE BRIDGE IS BLOWING! RUN!", 2.0, false)
	for s in segments:
		var b: TerrainBlock = s[0]
		if b.position.x < from_x or b.position.x > to_x:
			continue
		var delay := (b.position.x - from_x) / speed
		get_tree().create_timer(delay, false).timeout.connect(_blow.bind(s))


func _blow(s: Array) -> void:
	if not s[1]:
		return
	s[1] = false
	var b: TerrainBlock = s[0]
	b.remove_collision()
	var c := b.position + Vector2(b.size.x * 0.5, 4)
	FX.boom(c, 36.0, 0.0, Combat.Team.NEUTRAL, null, 0.3)
	FX.debris(c, 10, Color("8a5a2a"), 260, 4)
	var tw := b.create_tween()
	tw.tween_property(b, "position:y", b.position.y + 120.0, 0.7).set_ease(Tween.EASE_IN)
	tw.parallel().tween_property(b, "rotation", randf_range(-0.8, 0.8), 0.7)
	tw.tween_callback(b.hide)
	queue_redraw()
