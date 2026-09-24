class_name SecretArea
extends Sprite2D
## Foreground foliage hiding a secret nook. Fades out while the player is
## inside and awards a bonus the first time it's discovered.

var area := Rect2()
var _found := false


func setup(r: Rect2) -> SecretArea:
	area = r
	return self


func _ready() -> void:
	texture = Art.tex("foliage")
	centered = false
	scale = Vector2(area.size.x / texture.get_width(), area.size.y / texture.get_height())
	position = area.position
	z_index = 36


func _process(delta: float) -> void:
	var p: Player = GameManager.player
	var inside := p != null and area.grow(8).has_point(p.global_position + Vector2(0, -10))
	modulate.a = move_toward(modulate.a, 0.2 if inside else 1.0, delta * 3.0)
	if inside and not _found:
		_found = true
		GameManager.add_score(2000)
		GameManager.show_message("SECRET AREA!", 1.5, false)
		AudioManager.play("oneup", 0.0)
