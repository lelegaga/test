class_name Helicopter
extends EnemyBase
## Attack helicopter: flies in, hovers around the player, fires missile
## salvos and drops bombs. When destroyed it spins down trailing smoke and
## crashes in a huge explosion.

var _rotor_t := 0.0
var _hull: Sprite2D
var _rotor: Node2D
var hover_y := 70.0
var _side := 1.0
var _cd := 1.6
var _salvo := 0
var _salvo_t := 0.0
var _bomb_cd := 3.0
var _crash_vx := 0.0


func _init() -> void:
	hp = 45.0
	max_hp = 45.0
	score_value = 3000
	humanoid = false
	flying = true
	meleeable = false
	knock_resist = 1.0
	speed = 120.0
	hurt_size = Vector2(88, 34)
	hurt_offset = Vector2(0, -2)
	gun_id = ""
	enter_mode = "none"


func _build_visual() -> void:
	_hull = Sprite2D.new()
	_hull.texture = Art.tex("heli_body")
	_hull.use_parent_material = true
	_hull.position = Vector2(8, 0)
	visual.add_child(_hull)
	_rotor = Node2D.new()
	_rotor.draw.connect(_draw_rotor)
	visual.add_child(_rotor)


func _draw_rotor() -> void:
	var w := 58.0 * absf(cos(_rotor_t * 30.0)) + 8.0
	_rotor.draw_rect(Rect2(27 - w, -20, w * 2.0, 2), Color(0.1, 0.1, 0.12, 0.85))
	_rotor.draw_rect(Rect2(24, -22, 6, 4), Color("3a3c44"))
	# tail rotor
	var tw := 8.0 * absf(sin(_rotor_t * 40.0)) + 1.0
	_rotor.draw_rect(Rect2(-46, -6 - tw, 2, tw * 2.0), Color(0.1, 0.1, 0.12, 0.8))


func _on_ready() -> void:
	set_state("enter")
	AudioManager.play("warning", 0.0)
	GameManager.show_message("ATTACK CHOPPER!", 1.5, false)


func _think(delta: float) -> void:
	_rotor_t += delta
	AudioManager.set_loop("heli", on_screen(60.0), -6.0)
	var tp := target_pos()
	var goal := Vector2(tp.x + 130.0 * _side, CameraManager.top() + hover_y + sin(state_t * 1.7) * 14.0)
	goal.x = clampf(goal.x, CameraManager.left() + 70.0, CameraManager.right() - 70.0)
	match state:
		"enter":
			velocity = (goal - global_position).limit_length(speed * 1.3)
			if global_position.distance_to(goal) < 30.0:
				set_state("hover")
		"hover":
			velocity = velocity.lerp((goal - global_position).limit_length(speed), 2.0 * delta)
			_cd -= delta
			_bomb_cd -= delta
			if _cd <= 0.0 and GameManager.target_alive():
				set_state("warn")
				AudioManager.play("alert", 0.0)
			elif _bomb_cd <= 0.0 and absf(tp.x - global_position.x) < 50.0:
				_bomb_cd = 2.5
				Projectile.shoot(Projectile.Kind.BOMB, global_position + Vector2(0, 16), Vector2(velocity.x * 0.5, 40), Combat.Team.ENEMY, 1.0)
			if state_t > 4.0:
				_side = -_side
				state_t = 0.0
		"warn":
			velocity = velocity.lerp(Vector2.ZERO, 4.0 * delta)
			_flash = 0.5 if int(state_t * 16.0) % 2 == 0 else 0.0
			if state_t > 0.5:
				_salvo = 3
				_salvo_t = 0.0
				set_state("salvo")
		"salvo":
			velocity = velocity.lerp(Vector2.ZERO, 4.0 * delta)
			_salvo_t -= delta
			if _salvo_t <= 0.0 and _salvo > 0:
				_salvo -= 1
				_salvo_t = 0.25
				var o := global_position + Vector2(-facing * -10.0, 16)
				var a := (tp - o).angle() + randf_range(-0.2, 0.2)
				Projectile.shoot(Projectile.Kind.MISSILE, o, Vector2.from_angle(a) * 90.0, Combat.Team.ENEMY, 1.0)
				AudioManager.play("missile", 0.1)
			if _salvo <= 0:
				_cd = randf_range(2.4, 3.4)
				set_state("hover")
	facing = -1.0 if tp.x < global_position.x else 1.0
	visual.rotation = clampf(velocity.x / speed, -1.0, 1.0) * 0.15
	_rotor.queue_redraw()
	if hp < max_hp * 0.35 and randf() < 0.3:
		FX.smoke(global_position + Vector2(0, -10), 1, 4.0)


func _animate(_delta: float) -> void:
	visual.scale.x = facing


func _on_death(_info: Dictionary) -> void:
	AudioManager.set_loop("heli", false)
	FX.boom(global_position, 40.0, 0.0, Combat.Team.NEUTRAL, self)
	_crash_vx = -facing * 60.0
	visual.modulate = Color(0.5, 0.45, 0.45)


func _update_dead(delta: float) -> void:
	_dead_t += delta
	_rotor_t += delta * 0.5
	velocity = Vector2(_crash_vx, minf(velocity.y + 300.0 * delta, 260.0))
	visual.rotation += delta * 5.0
	global_position += velocity * delta
	_rotor.queue_redraw()
	FX.smoke(global_position, 1, 5.0)
	if randf() < 0.3:
		FX.fire(global_position, 1, 10.0)
	var ground := Combat.ground_below(global_position, 30.0)
	if ground != INF or _dead_t > 3.0 or global_position.y > CameraManager.level_bottom:
		FX.boom(global_position, 72.0, 12.0, Combat.Team.NEUTRAL, self, 0.9)
		FX.chain(global_position, Vector2(40, 20), 4, 0.1, 34.0)
		FX.debris(global_position, 24, Color("4a4f58"), 380, 5)
		queue_free()
