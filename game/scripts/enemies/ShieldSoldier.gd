class_name ShieldSoldier
extends EnemyBase
## Riot shield: deflects bullets from the front, advances slowly and pops
## off pistol shots. Attack from behind, jump over, or use explosives/flame.
## Enough bullets shatter the shield.

var shield_hp := 14.0
var has_shield := true
var _shield: Sprite2D
var _shots := 0
var _shot_t := 0.0


func _init() -> void:
	hp = 4.0
	max_hp = 4.0
	score_value = 300
	speed = 34.0
	body_style = "shield"
	gun_id = "pistol"


func _on_ready() -> void:
	if state != "drop":
		set_state("enter" if enter_mode == "walk" else "advance")
	_shield = Sprite2D.new()
	_shield.texture = Art.tex("shield")
	_shield.position = Vector2(13, -24)
	_shield.use_parent_material = true
	visual.add_child(_shield)


func _think(delta: float) -> void:
	var dx := dx_to_target()
	var dist := absf(dx)
	match state:
		"enter":
			walk(facing, speed * 2.0, delta)
			if on_screen(-40.0) or state_t > 5.0:
				set_state("advance")
		"advance", "alert":
			face_target()
			aim_at_target(0.5, 0.3)
			if dist > 90.0 and not edge_ahead():
				walk(signf(dx), speed, delta)
			else:
				stop(delta)
			if state_t > 1.4 and on_screen() and GameManager.target_alive():
				_shots = 2
				_shot_t = 0.2
				set_state("fire")
			elif dist < 26.0 and has_shield:
				set_state("bash")
		"fire":
			stop(delta)
			face_target()
			aim_at_target(0.5, 0.3)
			_shot_t -= delta
			if _shot_t <= 0.0 and _shots > 0:
				_shots -= 1
				_shot_t = 0.3
				shoot(Projectile.Kind.ENEMY_BULLET, 170.0, 1.0)
			if _shots <= 0 and _shot_t < -0.3:
				set_state("advance")
		"bash":
			if state_t < 0.02:
				velocity.x = facing * 160.0
				var r := Rect2(global_position.x + (4.0 if facing > 0 else -28.0), global_position.y - 40.0, 24.0, 40.0)
				for t in Combat.rect_query(r, Combat.Team.ENEMY):
					t.take_hit({"damage": 1.0, "dir": Vector2(facing, 0), "pos": r.get_center(), "kind": "melee", "source": self})
			stop(delta)
			if state_t > 0.6:
				set_state("advance")


func _modify_hit(info: Dictionary) -> int:
	if not has_shield or info.get("kind", "") != "bullet":
		return Combat.HIT_OK
	var dir: Vector2 = info.get("dir", Vector2.ZERO)
	# bullets travelling against our facing hit the shield
	if signf(dir.x) == -facing:
		shield_hp -= float(info.get("damage", 1.0))
		_flash = 0.3
		if shield_hp <= 0.0:
			_break_shield()
		return Combat.HIT_BLOCKED
	return Combat.HIT_OK


func _break_shield() -> void:
	has_shield = false
	if _shield:
		_shield.queue_free()
		_shield = null
	FX.debris(global_position + Vector2(facing * 12.0, -24.0), 8, Color("7a8a9a"), 220, 4)
	FX.popup(global_position + Vector2(0, -56), "SHIELD BROKEN", Color("8ad0ff"))
	AudioManager.play("crate", 0.1)
	speed = 60.0


func _on_death(_info: Dictionary) -> void:
	if _shield:
		_break_shield()
