class_name DestructibleObject
extends StaticBody2D
## Breakable scenery: crates, supply crates, oil barrels, sandbags,
## barricades, tank traps, watchtowers, wall segments and huts.
##
## Registered on the NEUTRAL team so player bullets and any explosion damage
## it. Explosive props (oil barrels) detonate with a short delay which gives
## satisfying chain reactions through Combat.explode().

signal destroyed(obj: DestructibleObject)

const KINDS := {
	"crate": {"tex": "crate", "hp": 3.0, "solid": true, "mat": "wood", "score": 50},
	"supply": {"tex": "", "hp": 3.0, "solid": true, "mat": "wood", "score": 50},
	"barrel": {"tex": "barrel", "hp": 2.0, "solid": true, "mat": "metal", "explosive": true, "score": 100},
	"sandbags": {"tex": "sandbags", "hp": 12.0, "solid": true, "mat": "sand", "score": 50},
	"barricade": {"tex": "barricade", "hp": 10.0, "solid": true, "mat": "wood", "score": 100},
	"hedgehog": {"tex": "hedgehog", "hp": 18.0, "solid": true, "mat": "metal", "armor": 0.5, "score": 100},
	"tower": {"tex": "tower", "hp": 26.0, "solid": false, "mat": "wood", "score": 500, "rubble": "tower_broken"},
	"wall": {"tex": "wall_segment", "hp": 30.0, "solid": true, "mat": "concrete", "armor": 0.35, "score": 300},
	"hut": {"tex": "hut", "hp": 24.0, "solid": false, "mat": "wood", "score": 300},
	"bunker": {"tex": "bunker", "hp": 40.0, "solid": false, "mat": "concrete", "armor": 0.4, "score": 500},
}

const MAT_COLORS := {
	"wood": Color("a8743a"), "metal": Color("5a5e64"), "sand": Color("c8b078"), "concrete": Color("8a8e92"),
}

var kind := "crate"
var hp := 3.0
var drop := ""           # pickup kind spawned when destroyed ("random" allowed)
var alive := true
var player_only := false
var _cfg: Dictionary
var _sprite: Sprite2D
var _shape: CollisionShape2D
var _platform: TerrainBlock
var _jiggle := 0.0
var _mat: ShaderMaterial
var _flash := 0.0
var _size := Vector2.ZERO


func setup(k: String, drop_kind: String = "") -> DestructibleObject:
	kind = k
	drop = drop_kind
	return self


func _ready() -> void:
	_cfg = KINDS[kind]
	hp = _cfg.hp
	collision_layer = Combat.L_PROPS if _cfg.solid else 0
	collision_mask = 0
	_sprite = Sprite2D.new()
	_sprite.texture = Art.supply_crate(drop if drop != "" else "random") if kind == "supply" else Art.tex(_cfg.tex)
	_sprite.centered = false
	_size = _sprite.texture.get_size()
	_sprite.offset = Vector2(-_size.x * 0.5, -_size.y)
	_mat = FX.make_flash_material()
	_sprite.material = _mat
	add_child(_sprite)
	z_index = -2 if not _cfg.solid else 2
	if _cfg.solid:
		_shape = CollisionShape2D.new()
		var r := RectangleShape2D.new()
		r.size = Vector2(_size.x - 2, _size.y - 2)
		_shape.shape = r
		_shape.position = Vector2(0, -_size.y * 0.5 + 1)
		add_child(_shape)
	if kind == "tower":
		# guard platform that enemies stand on
		_platform = TerrainBlock.new().setup(Rect2(global_position.x - 28, global_position.y - 80, 56, 8), "wood", true)
		_platform.visible = false
		get_parent().add_child.call_deferred(_platform)
	Combat.register(self, Combat.Team.NEUTRAL)


func _exit_tree() -> void:
	Combat.unregister(self)


func get_hurt_rect() -> Rect2:
	if kind == "tower":
		return Rect2(global_position + Vector2(-26, -120), Vector2(52, 120))
	if kind == "hut" or kind == "bunker":
		return Rect2(global_position + Vector2(-_size.x * 0.4, -_size.y * 0.8), Vector2(_size.x * 0.8, _size.y * 0.8))
	return Rect2(global_position - Vector2(_size.x * 0.5, _size.y), _size)


func is_hittable() -> bool:
	return alive


func take_hit(info: Dictionary) -> int:
	if not alive:
		return Combat.HIT_NONE
	var dmg: float = info.get("damage", 1.0)
	var k: String = info.get("kind", "bullet")
	if k == "bullet" and _cfg.has("armor"):
		dmg *= _cfg.armor
		FX.spark(info.get("pos", global_position), -Vector2(info.get("dir", Vector2.RIGHT)), 3, Color.WHITE)
		AudioManager.play("tink", 0.2, -8.0)
	elif k == "flame" and _cfg.mat == "wood":
		dmg *= 2.0
	elif k == "explosion":
		dmg *= 2.0
	hp -= dmg
	_jiggle = 0.12
	_flash = 0.8
	if _cfg.mat == "wood" and randf() < 0.5:
		FX.debris(info.get("pos", global_position), 1, MAT_COLORS.wood, 120, 2)
	if hp <= 0.0:
		if k == "explosion" and _cfg.get("explosive", false):
			# stagger chained barrels so the chain reads as a sequence
			alive = false
			get_tree().create_timer(randf_range(0.08, 0.18), false).timeout.connect(_destroy)
		else:
			_destroy()
	return Combat.HIT_OK


func _process(delta: float) -> void:
	if _jiggle > 0.0:
		_jiggle -= delta
		_sprite.position = Vector2(randi_range(-1, 1), 0) if _jiggle > 0.0 else Vector2.ZERO
	if _flash > 0.0:
		_flash = maxf(_flash - delta * 6.0, 0.0)
		_mat.set_shader_parameter("flash", _flash)


func _destroy() -> void:
	if not is_inside_tree():
		return
	alive = false
	Combat.unregister(self)
	var center := global_position + Vector2(0, -_size.y * 0.5)
	var col: Color = MAT_COLORS[_cfg.mat]
	FX.debris(center, 10 + int(_size.x / 6.0), col, 260, 4)
	FX.dust(global_position, 6, 60)
	GameManager.add_score(_cfg.score)
	if _shape:
		_shape.set_deferred("disabled", true)
	if _cfg.get("explosive", false):
		FX.boom(center, 52.0, 12.0, Combat.Team.NEUTRAL, self)
		FX.ground_flames(global_position, 4, 14)
		_sprite.visible = false
	elif kind == "tower":
		_collapse()
	elif kind == "hut" or kind == "bunker":
		FX.boom(center, 44.0, 0.0, Combat.Team.NEUTRAL, self, 0.4)
		FX.chain(center, Vector2(30, 20), 3, 0.15, 26.0)
		_sprite.modulate = Color(0.25, 0.22, 0.2)
		_sprite.scale.y = 0.35
		_sprite.position.y = 0
	else:
		AudioManager.play("crate", 0.15)
		_sprite.visible = false
	if drop != "":
		var d := drop
		if d == "random":
			d = Pickup.random_kind()
		var p := Pickup.new().setup(d)
		get_parent().add_child(p)
		p.global_position = center
		p.pop(Vector2(randf_range(-30, 30), -220))
	destroyed.emit(self)
	if not _sprite.visible:
		queue_free.call_deferred()


func _collapse() -> void:
	if _platform:
		_platform.remove_collision()
	AudioManager.play("explode", 0.1)
	CameraShakeManager.shake(0.4)
	var tw := create_tween()
	tw.tween_property(_sprite, "rotation", 0.6 * (1 if randf() < 0.5 else -1), 0.6).set_ease(Tween.EASE_IN)
	tw.parallel().tween_property(_sprite, "position:y", 50.0, 0.6).set_ease(Tween.EASE_IN)
	tw.tween_callback(_collapse_done)


func _collapse_done() -> void:
	_sprite.texture = Art.tex("tower_broken")
	_sprite.rotation = 0.0
	_sprite.position = Vector2.ZERO
	_sprite.offset = Vector2(-32, -40)
	FX.dust(global_position, 12, 90)
	FX.debris(global_position + Vector2(0, -20), 16, MAT_COLORS.wood, 240, 4)
	CameraShakeManager.shake(0.3)
