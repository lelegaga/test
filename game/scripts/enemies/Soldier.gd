class_name Soldier
extends EnemyBase
## Basic infantry: patrols, spots the player ("!"), then mixes aimed bursts,
## grenade lobs and repositioning. Occasionally panics and hops back.

var patrol_range := 60.0
var burst := 2
var _shots_left := 0
var _shot_t := 0.0
var _crouch := false
var grenadier := true
var _enter_extra := 0.0


func _init() -> void:
	hp = 3.0
	max_hp = 3.0
	score_value = 100
	speed = 70.0
	body_style = "soldier"
	gun_id = "rifle"


func _on_ready() -> void:
	if state != "drop":
		set_state("enter" if enter_mode == "walk" else "patrol")
	burst = randi_range(1, 3)
	_enter_extra = randf_range(0.0, 120.0)
	speed *= randf_range(0.9, 1.15)


func _think(delta: float) -> void:
	var dx := dx_to_target()
	var dist := absf(dx)
	match state:
		"enter":
			# walk in from off-screen, then engage
			walk(facing, speed * 1.3, delta)
			if on_screen(-40.0 - _enter_extra) or state_t > 4.0:
				set_state("alert")
		"patrol":
			var dir := facing
			if global_position.x > home_x + patrol_range:
				dir = -1.0
			elif global_position.x < home_x - patrol_range:
				dir = 1.0
			if edge_ahead() or wall_ahead():
				dir = -facing
			if int(state_t) % 4 == 3:
				stop(delta)
			else:
				walk(dir, speed * 0.5, delta)
			if on_screen() and dist < 330.0 and GameManager.target_alive():
				alert_popup()
				set_state("alert")
		"alert":
			stop(delta)
			face_target()
			aim_at_target()
			if state_t > 0.35:
				_choose()
		"shoot":
			stop(delta)
			face_target()
			aim_at_target()
			_shot_t -= delta
			if _shot_t <= 0.0 and _shots_left > 0 and on_screen() and GameManager.target_alive():
				_shots_left -= 1
				_shot_t = 0.22
				shoot(Projectile.Kind.ENEMY_BULLET, 175.0, 1.0, 0.04)
			if _shots_left <= 0 and _shot_t <= -0.35:
				_choose()
		"grenade":
			stop(delta)
			face_target()
			if state_t > 0.3 and _shots_left > 0:
				_shots_left = 0
				throw_grenade()
			if state_t > 0.7:
				_choose()
		"move":
			var want := 1.0 if dist > 180.0 else -1.0
			var dir := signf(dx) * want
			if edge_ahead():
				dir = 0.0
			walk(dir, speed, delta)
			if wall_ahead() and is_on_floor():
				velocity.y = -330.0
			if dir == 0.0 or state_t > randf_range(0.6, 1.1):
				face_target()
				_choose()
		"panic":
			if state_t < 0.05 and is_on_floor():
				velocity = Vector2(-signf(dx) * 120.0, -260.0)
				FX.popup(global_position + Vector2(0, -56), "!?", Color("8ad0ff"))
			if state_t > 0.5 and is_on_floor():
				_choose()


func _choose() -> void:
	_crouch = false
	if not GameManager.target_alive():
		set_state("patrol")
		return
	var dist := absf(dx_to_target())
	if dist < 36.0 and randf() < 0.5:
		set_state("panic")
		return
	if dist > 330.0 or not on_screen(0.0):
		set_state("move")
		return
	var r := randf()
	if grenadier and r < 0.18 and dist > 80.0 and dist < 260.0:
		_shots_left = 1
		set_state("grenade")
	elif r < 0.75:
		_shots_left = burst
		_shot_t = randf_range(0.15, 0.35)
		_crouch = randf() < 0.35
		set_state("shoot")
	else:
		set_state("move")


func _crouching() -> bool:
	return _crouch and state == "shoot"


func _override_frame(key: String) -> String:
	if state == "grenade" and state_t < 0.4:
		return "cheer"
	return key


func _show_arm() -> bool:
	return not (state == "grenade" and state_t < 0.4)
