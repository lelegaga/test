class_name MechTankBoss
extends BossBase
## Mission 1 boss: "IRON BEHEMOTH", a huge mechanized tank.
##
## Phase 1  machine-gun sweeps + aimed cannon shells.
## Phase 2  armor plate blows off the missile pod: marked missile rain.
## Phase 3  berserk: faster everything, artillery barrage across the arena
##          and a ramming charge.
## The reactor core on the front armor is the weak point; it opens (glowing,
## venting steam) for a few seconds after every big attack. Every attack is
## telegraphed with laser sights, ground markers or a flashing "!".

var arena_left := 0.0
var arena_right := 640.0
var home_x := 0.0
var _goal_x := 0.0
var _speed := 70.0
var _cd := 1.5
var _core_open_t := 0.0
var _attack_i := 0
var _tread_t := 0.0

var _hull: Sprite2D
var _tread: Sprite2D
var _turret: Sprite2D
var _cannon: Sprite2D
var _mg: Sprite2D
var _pod: Sprite2D
var _pod_cover: Sprite2D
var _core: Node2D
var _warn: Node2D
var _wreck: Sprite2D

var cannon_aim := 0.0
var mg_aim := 0.0
var _mg_shots := 0
var _mg_t := 0.0
var _sweep := Vector2.ZERO
var _cannon_shots := 0
var _markers: Array = []       # [world_pos, time_left, fired]
var _laser_from := Vector2.ZERO
var _laser_to := Vector2.ZERO
var _laser_on := false
var _bang := false
var _ram_dir := -1.0


func _init() -> void:
	boss_name = "IRON BEHEMOTH"
	hp = 420.0
	max_hp = 420.0
	body_extent = Vector2(105, 55)
	score_value = 30000


func _build() -> void:
	_hull = _sprite("boss_hull", Vector2(-110, -86))
	_tread = Sprite2D.new()
	_tread.centered = false
	_tread.position = Vector2(-110, -34)
	_tread.use_parent_material = true
	visual.add_child(_tread)
	_pod = _sprite("boss_pod", Vector2(-104, -118))
	_pod_cover = _sprite("boss_pod_cover", Vector2(-105, -118))
	_turret = _sprite("boss_turret", Vector2(-40, -126))
	_cannon = _sprite("boss_cannon", Vector2.ZERO)
	_cannon.offset = Vector2(0, -9)
	_cannon.position = Vector2(70, -110)
	_mg = _sprite("boss_mg", Vector2.ZERO)
	_mg.offset = Vector2(-4, -7)
	_mg.position = Vector2(98, -58)
	_core = Node2D.new()
	_core.position = Vector2(88, -40)
	_core.use_parent_material = false
	_core.draw.connect(_draw_core)
	visual.add_child(_core)
	_warn = Node2D.new()
	_warn.top_level = true
	_warn.z_index = 50
	_warn.draw.connect(_draw_warnings)
	add_child(_warn)
	add_part(Rect2(-110, -84, 220, 84), 0.3)
	add_part(Rect2(-40, -126, 120, 44), 0.3)
	add_part(Rect2(78, -50, 20, 20), 2.0, true)
	_goal_x = global_position.x


func _sprite(key: String, off: Vector2) -> Sprite2D:
	var s := Sprite2D.new()
	s.texture = Art.tex(key)
	s.centered = false
	s.offset = off
	s.use_parent_material = true
	visual.add_child(s)
	return s


func lp(v: Vector2) -> Vector2:
	return global_position + Vector2(v.x * facing, v.y)


func wa(local_a: float) -> float:
	return local_a if facing > 0.0 else PI - local_a


func weak_open() -> bool:
	return _core_open_t > 0.0


func _rage() -> bool:
	return phase >= 3


# =============================================================== update
func _update_boss(delta: float) -> void:
	_core_open_t -= delta
	_update_markers(delta)
	var tp := GameManager.target_pos()
	var spd := _speed * (1.5 if _rage() else 1.0)
	match state:
		"intro":
			AudioManager.set_loop("engine", true, -4.0)
			global_position.x = move_toward(global_position.x, home_x, 95.0 * delta)
			_tread_t += delta * 95.0
			if absf(global_position.x - home_x) < 1.0:
				AudioManager.play("roar", 0.0)
				CameraShakeManager.shake(0.7)
				FX.smoke(lp(Vector2(-60, -130)), 8, 6.0)
				activate()
				_goal_x = home_x
				set_state("idle")
				_cd = 1.2
		"idle":
			if absf(global_position.x - _goal_x) < 2.0:
				_goal_x = clampf(home_x + randf_range(-110.0, 40.0), arena_left + 160.0, arena_right - 100.0)
			var nx := move_toward(global_position.x, _goal_x, spd * delta)
			_tread_t += (nx - global_position.x)
			global_position.x = nx
			var d := tp.x - global_position.x
			if absf(d) > 30.0:
				facing = signf(d)
			cannon_aim = move_toward(cannon_aim, _aim_local(lp(Vector2(70, -110)), tp, -0.35, 0.2), delta)
			_cd -= delta
			if _cd <= 0.0 and GameManager.target_alive():
				_pick_attack()
		"mg_warn":
			mg_aim = _sweep.x
			_laser_on = int(state_t * 16.0) % 2 == 0
			_laser_from = lp(Vector2(98, -58) + Vector2(38, 0).rotated(mg_aim))
			_laser_to = _laser_from + Vector2.from_angle(wa(mg_aim)) * 700.0
			if state_t > 0.6:
				_laser_on = false
				_mg_shots = 26 if _rage() else 20
				_mg_t = 0.0
				set_state("mg_fire")
		"mg_fire":
			var dur := 1.2 if _rage() else 1.5
			mg_aim = lerpf(_sweep.x, _sweep.y, clampf(state_t / dur, 0.0, 1.0))
			_mg_t -= delta
			if _mg_t <= 0.0 and _mg_shots > 0:
				_mg_shots -= 1
				_mg_t = dur / 22.0
				var o := lp(Vector2(98, -58) + Vector2(38, 0).rotated(mg_aim))
				FX.muzzle(o, wa(mg_aim), 10.0)
				FX.shell(o, -facing)
				AudioManager.play("enemy_shot", 0.1)
				Projectile.shoot(Projectile.Kind.ENEMY_BULLET, o, Vector2.from_angle(wa(mg_aim)) * 230.0, Combat.Team.ENEMY, 1.0)
			if _mg_shots <= 0:
				_end_attack(0.9)
		"cannon_warn":
			cannon_aim = move_toward(cannon_aim, _aim_local(lp(Vector2(70, -110)), tp, -0.35, 0.2), 1.5 * delta)
			_laser_on = int(state_t * 16.0) % 2 == 0
			_laser_from = lp(Vector2(70, -110) + Vector2(96, 0).rotated(cannon_aim))
			_laser_to = _laser_from + Vector2.from_angle(wa(cannon_aim)) * 700.0
			if state_t > (0.55 if _rage() else 0.8):
				_laser_on = false
				_fire_cannon()
				_cannon_shots -= 1
				if _cannon_shots > 0:
					set_state("cannon_warn")
					state_t = 0.35
				else:
					_core_open_t = 2.6
					_end_attack(1.2)
		"missile_mark":
			if state_t > 0.3 and not _bang:
				_bang = true
				var n := 6 if _rage() else 4
				for i in n:
					var x := clampf(tp.x + randf_range(-130.0, 130.0), arena_left + 20.0, arena_right - 20.0)
					if i == 0:
						x = tp.x
					var gy := Combat.ground_below(Vector2(x, CameraManager.top()), 600.0)
					_markers.append([Vector2(x, gy if gy != INF else 300.0), 1.3 + i * 0.15, false])
					var m := Projectile.shoot(Projectile.Kind.MISSILE, lp(Vector2(-80 + i * 10, -118)), Vector2(randf_range(-30, 30), -260), Combat.Team.ENEMY, 1.0)
					m.homing = 0.0
				AudioManager.play("missile", 0.0)
				FX.smoke(lp(Vector2(-80, -120)), 6, 5.0, false)
			if state_t > 1.0:
				_core_open_t = 2.4
				_end_attack(1.8)
		"barrage":
			if state_t > 0.2 and not _bang:
				_bang = true
				AudioManager.play("warning", 0.0)
				GameManager.show_message("ARTILLERY!", 1.2, false)
				var n := 8
				for i in n:
					var x := lerpf(arena_left + 30.0, global_position.x - 130.0, float(i) / (n - 1)) + randf_range(-15, 15)
					var gy := Combat.ground_below(Vector2(x, CameraManager.top()), 600.0)
					_markers.append([Vector2(x, gy if gy != INF else 300.0), 1.0 + i * 0.2, false])
			if state_t > 2.6:
				_core_open_t = 2.4
				_end_attack(1.0)
		"ram_warn":
			_bang = int(state_t * 10.0) % 2 == 0
			global_position.x += facing * -25.0 * delta
			_tread_t -= 60.0 * delta
			if randf() < 0.4:
				FX.dust(lp(Vector2(-100, 0)), 2, 40)
			if state_t > 1.0:
				_bang = false
				_ram_dir = facing
				AudioManager.play("roar", 0.0)
				set_state("ram")
		"ram":
			var target_x := arena_left + 190.0 if _ram_dir < 0.0 else arena_right - 190.0
			var nx := move_toward(global_position.x, target_x, 380.0 * delta)
			_tread_t += (nx - global_position.x)
			global_position.x = nx
			CameraShakeManager.shake(0.08)
			FX.dust(lp(Vector2(-100, 0)), 1, 40)
			contact_damage(_hull_rect().grow(-6))
			if absf(global_position.x - target_x) < 1.0:
				CameraShakeManager.shake(0.6)
				AudioManager.play("cannon", 0.0)
				FX.dust(lp(Vector2(110, 0)), 12, 90)
				_core_open_t = 3.0
				set_state("ram_back")
		"ram_back":
			var nx := move_toward(global_position.x, home_x, 110.0 * delta)
			_tread_t += (nx - global_position.x)
			global_position.x = nx
			if state_t > 0.6 and absf(global_position.x - home_x) < 1.0:
				_end_attack(0.8)
		"phase_change":
			visual.position = Vector2(randf_range(-2, 2), 0)
			if randf() < 0.4:
				FX.smoke(lp(Vector2(randf_range(-100, 100), -90)), 1, 6.0)
			if state_t > 0.6 and _pod_cover.visible and phase >= 2:
				_pod_cover.visible = false
				FX.boom(lp(Vector2(-80, -110)), 40.0, 0.0, Combat.Team.NEUTRAL, self)
				FX.debris(lp(Vector2(-80, -110)), 16, Color("6a6e74"), 360, 6)
			if state_t > 1.6:
				visual.position = Vector2.ZERO
				_end_attack(0.6)
	# continuous feedback
	if not _rage():
		visual.modulate = Color.WHITE
	else:
		var pulse := 0.5 + 0.5 * sin(state_t * 10.0)
		visual.modulate = Color(1.0, 0.75 + 0.25 * (1.0 - pulse), 0.75 + 0.25 * (1.0 - pulse))
		if randf() < 0.15:
			FX.smoke(lp(Vector2(randf_range(-60, 60), -126)), 1, 4.0, false)
	if weak_open() and randf() < 0.4:
		FX.smoke(lp(Vector2(88, -46)), 1, 3.0, false)
	if state != "intro" and state != "ram":
		contact_damage(_hull_rect().grow(-16))
	_animate()
	_warn.queue_redraw()
	_core.queue_redraw()


func _hull_rect() -> Rect2:
	var r := Rect2(-110, -84, 220, 84)
	if facing < 0.0:
		r.position.x = -r.position.x - r.size.x
	return Rect2(global_position + r.position, r.size)


func _aim_local(from: Vector2, to: Vector2, lo: float, hi: float) -> float:
	var d := to - from
	return clampf(atan2(d.y, absf(d.x)), lo, hi)


func _pick_attack() -> void:
	_bang = false
	var list: Array = ["mg", "cannon"]
	if phase >= 2:
		list = ["missiles", "mg", "cannon", "missiles"]
	if phase >= 3:
		list = ["barrage", "ram", "mg", "missiles", "cannon", "ram"]
	var a: String = list[_attack_i % list.size()]
	_attack_i += 1
	match a:
		"mg":
			_sweep = Vector2(-0.45, 0.4)
			AudioManager.play("alert", 0.0)
			set_state("mg_warn")
		"cannon":
			_cannon_shots = 2 if _rage() else 1
			AudioManager.play("alert", 0.0)
			set_state("cannon_warn")
		"missiles":
			AudioManager.play("warning", 0.0)
			set_state("missile_mark")
		"barrage":
			set_state("barrage")
		"ram":
			AudioManager.play("warning", 0.0)
			set_state("ram_warn")


func _end_attack(cooldown: float) -> void:
	_bang = false
	_laser_on = false
	_cd = cooldown * (0.6 if _rage() else 1.0)
	set_state("idle")


func _fire_cannon() -> void:
	var o := lp(Vector2(70, -110) + Vector2(96, 0).rotated(cannon_aim))
	var a := wa(cannon_aim)
	FX.muzzle(o, a, 26.0)
	FX.smoke(o, 8, 5.0, false)
	AudioManager.play("cannon", 0.0)
	CameraShakeManager.shake(0.4)
	Projectile.shoot(Projectile.Kind.ENEMY_SHELL, o, Vector2.from_angle(a) * 300.0, Combat.Team.ENEMY, 1.0)
	_cannon.position.x = 60


func _update_markers(delta: float) -> void:
	for m in _markers:
		m[1] -= delta
		if m[1] <= 0.35 and not m[2]:
			m[2] = true
			var p: Vector2 = m[0]
			Projectile.shoot(Projectile.Kind.BOMB, Vector2(p.x, CameraManager.top() - 10.0), Vector2(0, 520), Combat.Team.ENEMY, 1.0)
	_markers = _markers.filter(func(m): return m[1] > 0.0)


func _on_phase(n: int) -> void:
	_laser_on = false
	_markers.clear()
	if n == 2:
		GameManager.show_message("WARNING!", 1.6)
		AudioManager.play("warning", 0.0)
	elif n == 3:
		GameManager.show_message("BERSERK MODE!", 1.8)
		_speed = 95.0
	set_state("phase_change")


# =============================================================== visuals
func _animate() -> void:
	visual.scale.x = facing
	_tread.texture = Art.boss_tread(int(absf(_tread_t) * 0.3) % 4)
	_cannon.rotation = cannon_aim
	_cannon.position.x = move_toward(_cannon.position.x, 70.0, 1.5)
	_mg.rotation = mg_aim
	_mg.modulate = Color(1.6, 0.6, 0.6) if state == "mg_warn" and int(state_t * 12.0) % 2 == 0 else Color.WHITE


func _draw_core() -> void:
	var open := weak_open()
	var pulse := 0.5 + 0.5 * sin(state_t * (14.0 if open else 4.0))
	if open:
		_core.draw_circle(Vector2.ZERO, 9, Color("1a1420"))
		_core.draw_circle(Vector2.ZERO, 7.5, Color(0.3 + 0.7 * pulse, 1.0, 1.0))
		_core.draw_circle(Vector2.ZERO, 4, Color.WHITE)
	else:
		_core.draw_circle(Vector2.ZERO, 9, Color("1a1420"))
		_core.draw_circle(Vector2.ZERO, 7.5, Color(0.2, 0.5 + 0.3 * pulse, 0.6))
		# armored shutters
		_core.draw_rect(Rect2(-8, -8, 16, 7), Color("5a5e64"))
		_core.draw_rect(Rect2(-8, 1, 16, 7), Color("4a4f58"))
		_core.draw_rect(Rect2(-8, -1, 16, 2), Color("1a1420"))


func _draw_warnings() -> void:
	if _laser_on:
		_warn.draw_line(_laser_from, _laser_to, Color(1, 0.15, 0.1, 0.85), 1.0)
		_warn.draw_circle(_laser_from, 3, Color(1, 0.3, 0.2, 0.9))
	for m in _markers:
		var p: Vector2 = m[0]
		var tl: float = m[1]
		if int(tl * 12.0) % 2 == 0 or tl < 0.5:
			var r := 10.0 + tl * 6.0
			_warn.draw_arc(p + Vector2(0, -2), r, 0, TAU, 20, Color(1, 0.15, 0.1, 0.9), 1.0)
			_warn.draw_line(p + Vector2(-r - 3, -2), p + Vector2(r + 3, -2), Color(1, 0.15, 0.1, 0.9), 1.0)
			_warn.draw_line(p + Vector2(0, -r - 5), p + Vector2(0, r + 1), Color(1, 0.15, 0.1, 0.9), 1.0)
			PixelFont.draw_centered(_warn, "!", p + Vector2(0, -r - 12), 1, Color("ff5a3a"))
	if state == "ram_warn" and _bang:
		PixelFont.draw_centered(_warn, "!!", lp(Vector2(0, -150)), 3, Color("ff5a3a"))


func _on_death_start() -> void:
	_laser_on = false
	_markers.clear()
	_warn.queue_redraw()
	GameManager.show_message("", 0.0)


func _on_final_explosion() -> void:
	for c in visual.get_children():
		c.visible = false
	_wreck = Sprite2D.new()
	_wreck.texture = Art.tex("boss_wreck")
	_wreck.centered = false
	_wreck.offset = Vector2(-110, -98)
	visual.add_child(_wreck)
	visual.modulate = Color.WHITE
	_mat.set_shader_parameter("flash", 0.0)
