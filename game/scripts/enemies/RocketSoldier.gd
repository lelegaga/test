class_name RocketSoldier
extends EnemyBase
## Kneels at long range and fires slow, slightly homing rockets after a
## visible warning. Rockets can be shot down.

var cooldown := 1.2


func _init() -> void:
	hp = 4.0
	max_hp = 4.0
	score_value = 200
	speed = 50.0
	body_style = "rocket"
	gun_id = "bazooka"


func _on_ready() -> void:
	if state != "drop":
		set_state("enter" if enter_mode == "walk" else "idle")


func _think(delta: float) -> void:
	var dist := absf(dx_to_target())
	match state:
		"enter":
			walk(facing, speed * 1.4, delta)
			if on_screen(-50.0) or state_t > 4.0:
				set_state("idle")
		"idle", "alert":
			stop(delta)
			face_target()
			aim_at_target(0.5, 0.35)
			cooldown -= delta
			if cooldown <= 0.0 and on_screen() and dist < 520.0 and GameManager.target_alive():
				set_state("aim")
				AudioManager.play("alert", 0.0, -4.0)
			elif dist < 70.0 and state_t > 0.5:
				set_state("retreat")
		"aim":
			stop(delta)
			face_target()
			aim_at_target(0.5, 0.35)
			_flash = 0.6 if int(state_t * 16.0) % 2 == 0 else 0.0
			if state_t > 0.55:
				var p := shoot(Projectile.Kind.ENEMY_ROCKET, 110.0, 1.0)
				p.max_speed = 260.0
				FX.smoke(muzzle_pos() - Vector2(facing * 34.0, 0), 4, 3.0, false)
				AudioManager.play("rocket", 0.1, -2.0)
				cooldown = randf_range(2.0, 3.0)
				set_state("idle")
		"retreat":
			walk(-signf(dx_to_target()), speed * 1.5, delta)
			if edge_ahead() or state_t > 0.8:
				set_state("idle")


func _crouching() -> bool:
	return state == "idle" or state == "aim" or state == "alert"
