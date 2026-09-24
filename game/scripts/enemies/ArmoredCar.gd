class_name ArmoredCar
extends EnemyBase
## Armored car: lots of HP, drives into range, paints the player with a laser
## sight and fires explosive shells from its turret cannon.

var _cannon: Sprite2D
var _laser: Line2D
var cannon_angle := PI
var _cd := 1.5
var _mg_cd := 2.5
var _mg_left := 0
var _mg_t := 0.0
var _wheel_t := 0.0
var stop_dist := 250.0


func _init() -> void:
	hp = 70.0
	max_hp = 70.0
	score_value = 2000
	humanoid = false
	meleeable = false
	knock_resist = 1.0
	speed = 70.0
	hurt_size = Vector2(92, 40)
	hurt_offset = Vector2(0, -24)
	gun_id = ""


func _build_visual() -> void:
	var hull := Sprite2D.new()
	hull.texture = Art.tex("armored_car")
	hull.centered = false
	hull.offset = Vector2(-50, -52)
	hull.use_parent_material = true
	visual.add_child(hull)
	_cannon = Sprite2D.new()
	_cannon.texture = Art.tex("car_cannon")
	_cannon.centered = false
	_cannon.offset = Vector2(-2, -4)
	_cannon.position = Vector2(-3, -40)
	_cannon.use_parent_material = true
	visual.add_child(_cannon)


func _on_ready() -> void:
	set_state("drive")
	_laser = Line2D.new()
	_laser.width = 1.0
	_laser.default_color = Color(1, 0.1, 0.1, 0.8)
	_laser.top_level = true
	_laser.visible = false
	_laser.z_index = 25
	add_child(_laser)
	AudioManager.play("warning", 0.0)
	GameManager.show_message("ARMORED CAR!", 1.5, false)


func _cannon_origin() -> Vector2:
	return global_position + Vector2(-3, -40)


func _think(delta: float) -> void:
	var dx := dx_to_target()
	_wheel_t += delta * velocity.x
	# cannon always tracks, clamped to the front arc
	var want := (target_pos() - _cannon_origin()).angle()
	cannon_angle = rotate_toward(cannon_angle, want, 1.2 * delta)
	match state:
		"drive":
			var dir := signf(dx) if absf(dx) > stop_dist else 0.0
			walk(dir, speed, delta)
			facing = -1.0 if dx < 0.0 else 1.0
			if dir == 0.0 and on_screen(-30.0):
				set_state("idle")
		"idle":
			stop(delta)
			_cd -= delta
			_mg_cd -= delta
			if absf(dx) > stop_dist + 120.0 or absf(dx) < 110.0:
				set_state("reposition")
			elif _cd <= 0.0 and on_screen() and GameManager.target_alive():
				set_state("aim")
				AudioManager.play("alert", 0.0)
			elif _mg_cd <= 0.0 and on_screen():
				_mg_left = 8
				_mg_cd = 3.0
		"reposition":
			var dir := signf(dx) if absf(dx) > stop_dist else -signf(dx)
			walk(dir, speed * 1.2, delta)
			facing = signf(dx)
			if state_t > 1.2:
				set_state("idle")
		"aim":
			stop(delta)
			_laser.visible = int(state_t * 16.0) % 2 == 0
			var o := _cannon_origin() + Vector2.from_angle(cannon_angle) * 34.0
			_laser.points = PackedVector2Array([o, o + Vector2.from_angle(cannon_angle) * 600.0])
			if state_t > 0.75:
				_laser.visible = false
				FX.muzzle(o, cannon_angle, 16.0)
				FX.smoke(o, 5, 4.0, false)
				AudioManager.play("cannon", 0.1)
				CameraShakeManager.shake(0.25)
				Projectile.shoot(Projectile.Kind.ENEMY_SHELL, o, Vector2.from_angle(cannon_angle) * 240.0, Combat.Team.ENEMY, 1.0)
				velocity.x = -facing * 60.0
				_cd = randf_range(2.2, 3.2)
				set_state("idle")
	# coaxial machine gun
	_mg_t -= delta
	if _mg_left > 0 and _mg_t <= 0.0:
		_mg_left -= 1
		_mg_t = 0.1
		var o2 := _cannon_origin() + Vector2(facing * 10.0, 4.0)
		var a := (target_pos() - o2).angle() + randf_range(-0.08, 0.08)
		FX.muzzle(o2, a, 6.0)
		AudioManager.play("enemy_shot", 0.1, -4.0)
		Projectile.shoot(Projectile.Kind.ENEMY_BULLET, o2, Vector2.from_angle(a) * 200.0, Combat.Team.ENEMY, 1.0)
	if hp < max_hp * 0.4 and randf() < 0.2:
		FX.smoke(global_position + Vector2(randf_range(-30, 30), -40), 1, 4.0)


func _animate(_delta: float) -> void:
	visual.scale.x = 1.0
	_cannon.rotation = cannon_angle


func _on_death(_info: Dictionary) -> void:
	_laser.visible = false
	FX.boom(global_position + Vector2(0, -26), 64.0, 10.0, Combat.Team.NEUTRAL, self, 0.7)
	FX.chain(global_position + Vector2(0, -26), Vector2(40, 16), 5, 0.12, 30.0)
	FX.debris(global_position + Vector2(0, -30), 18, Color("5a5e64"), 320, 5)
	_cannon.visible = false
	visual.modulate = Color(0.3, 0.26, 0.24)
	velocity = Vector2(0, -150)


func _update_dead(delta: float) -> void:
	_dead_t += delta
	velocity.y = minf(velocity.y + gravity * delta, 600.0)
	velocity.x = move_toward(velocity.x, 0.0, 300.0 * delta)
	move_and_slide()
	if randf() < 0.3:
		FX.smoke(global_position + Vector2(randf_range(-30, 30), -36), 1, 5.0)
	if randf() < 0.1:
		FX.fire(global_position + Vector2(randf_range(-30, 30), -30), 1, 4.0)
