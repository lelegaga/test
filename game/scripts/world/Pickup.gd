class_name Pickup
extends Node2D
## Collectible item: weapons (H/S/R/F), bombs, ammo, 1UP and score items.
## Pops out with a little arc, lands, bobs, and is collected on touch.

const WEAPONS := ["hmg", "shotgun", "rocket", "flame"]

var kind := "hmg"
var vel := Vector2.ZERO
var landed := false
var _t := 0.0
var _floor_y := INF
var _sprite: Sprite2D


static func random_kind() -> String:
	var r := randf()
	if r < 0.38:
		return WEAPONS[randi() % WEAPONS.size()]
	elif r < 0.62:
		return "bombs"
	elif r < 0.72:
		return "ammo"
	elif r < 0.95:
		return "medal" if randf() < 0.5 else "fruit"
	return "life"


func setup(k: String) -> Pickup:
	kind = k
	return self


func _ready() -> void:
	_sprite = Sprite2D.new()
	_sprite.texture = Art.pickup(kind)
	_sprite.position = Vector2(0, -9)
	add_child(_sprite)
	z_index = 8


func pop(v: Vector2) -> void:
	vel = v
	landed = false


func _physics_process(delta: float) -> void:
	_t += delta
	if not landed:
		vel.y = minf(vel.y + 700.0 * delta, 400.0)
		var to := global_position + vel * delta
		var hit := Combat.ray_world(global_position, to + Vector2(0, 1), Combat.MASK_ACTOR)
		if not hit.is_empty() and vel.y > 0.0:
			global_position = hit.position
			landed = true
		else:
			global_position = to
		if global_position.y > CameraManager.level_bottom + 40.0:
			queue_free()
			return
	else:
		_sprite.position.y = -9.0 + sin(_t * 5.0) * 2.0
	# blink hint
	_sprite.modulate = Color(1.4, 1.4, 1.4) if int(_t * 6.0) % 6 == 0 else Color.WHITE
	var p: Player = GameManager.player
	if p == null or not is_instance_valid(p) or p.state == Player.St.DEAD:
		return
	var tgt: Node2D = p.get_target_node()
	var reach := 20.0 if tgt == p else 52.0
	if tgt.global_position.distance_to(global_position) < reach or (tgt == p and p.global_position.distance_to(global_position + Vector2(0, 12)) < 22.0):
		if tgt != p and kind in WEAPONS:
			return   # weapons can't be used inside the tank
		_collect(p)


func _collect(p: Player) -> void:
	match kind:
		"hmg", "shotgun", "rocket", "flame":
			p.give_weapon(kind)
		"bombs":
			p.add_grenades(10)
			AudioManager.play("pickup", 0.0)
			FX.popup(global_position + Vector2(0, -24), "BOMB +10", Color("a0f0a0"))
		"ammo":
			if p.weapon.ammo >= 0:
				p.weapon.add_ammo(p.weapon.max_ammo / 2)
			p.add_grenades(5)
			AudioManager.play("pickup", 0.0)
			FX.popup(global_position + Vector2(0, -24), "AMMO", Color("a0f0a0"))
		"life":
			GameManager.add_life()
			FX.popup(global_position + Vector2(0, -24), "1UP!", Color("50ff80"), 2)
		"medal":
			GameManager.add_score(1000, global_position + Vector2(0, -20))
			AudioManager.play("pickup", 0.0)
		"fruit":
			GameManager.add_score(500, global_position + Vector2(0, -20))
			AudioManager.play("pickup", 0.0)
	for i in 6:
		FX.front.spawn(ParticleLayer.STAR, global_position + Vector2(0, -9), Vector2.from_angle(i * TAU / 6.0) * 90.0, 0.35, 1, Color("fff27a"))
	queue_free()
