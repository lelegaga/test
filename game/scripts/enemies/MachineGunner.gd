class_name MachineGunner
extends EnemyBase
## Dug in behind sandbags. Paints the player with a laser, then sweeps a long
## burst across them. Tough to approach head-on — grenades work well.

var _nest: Sprite2D
var _laser: Line2D
var _burst_left := 0
var _shot_t := 0.0
var _sweep_from := 0.0
var _sweep_to := 0.0


func _init() -> void:
	hp = 7.0
	max_hp = 7.0
	score_value = 300
	speed = 0.0
	body_style = "soldier"
	gun_id = "smg"
	enter_mode = "none"
	hurt_size = Vector2(18, 24)
	hurt_offset = Vector2(0, -22)


func _on_ready() -> void:
	set_state("idle")
	_nest = Sprite2D.new()
	_nest.texture = Art.tex("mg_nest")
	_nest.position = Vector2(10, -11)
	visual.add_child(_nest)
	_laser = Line2D.new()
	_laser.width = 1.0
	_laser.default_color = Color(1, 0.1, 0.1, 0.7)
	_laser.visible = false
	_laser.top_level = true
	_laser.z_index = 25
	add_child(_laser)


func _think(delta: float) -> void:
	velocity.x = 0.0
	match state:
		"idle":
			face_target()
			aim_at_target(0.6, 0.3)
			if on_screen() and state_t > 1.0 and GameManager.target_alive():
				set_state("warn")
		"warn":
			face_target()
			aim_at_target(0.6, 0.3)
			_laser.visible = int(state_t * 14.0) % 2 == 0
			_laser.points = PackedVector2Array([muzzle_pos(), muzzle_pos() + Vector2.from_angle(world_angle(aim)) * 500.0])
			if state_t > 0.6:
				_laser.visible = false
				_burst_left = 10
				_sweep_from = aim - 0.25
				_sweep_to = aim + 0.2
				set_state("burst")
		"burst":
			aim = lerpf(_sweep_from, _sweep_to, state_t / 1.1)
			_shot_t -= delta
			if _shot_t <= 0.0 and _burst_left > 0:
				_burst_left -= 1
				_shot_t = 0.09
				shoot(Projectile.Kind.ENEMY_BULLET, 185.0, 1.0, 0.02)
				FX.shell(muzzle_pos(), facing)
			if _burst_left <= 0:
				set_state("idle")
				state_t = -0.4


func _crouching() -> bool:
	return true


func _on_death(_info: Dictionary) -> void:
	if _laser:
		_laser.visible = false
