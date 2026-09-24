extends Node
## Automated play-through used for smoke testing (not part of the game).
##
##   godot --headless --fixed-fps 60 --path game -- --autotest [--god]
##          [--shotdir=/abs/dir] [--shots=10,20,..] [--hpmode]
##
## A simple bot runs right, faces and shoots the nearest enemy, jumps when
## stuck, throws grenades, boards the tank and fights the boss. If a locked
## encounter stalls for too long it force-clears it (logged) so the whole
## level flow — triggers, boss, results screen — is always exercised.

var t := 0.0
var phase := 0
var frames := 0
var shot_dir := ""
var shots: Array = [1.0, 30.0, 60.0, 90.0, 120.0, 150.0, 180.0, 210.0, 240.0, 270.0]
var _shot_i := 0
var _last_x := 0.0
var _stuck_t := 0.0
var _lock_t := 0.0
var _log_t := 0.0
var _jump_hold := 0.0
var _max_enemies := 0
var _max_bullets := 0
var _max_particles := 0
var _forced_clears := 0
var _slow_frames := 0
var _frame_ms_total := 0.0
var _args: PackedStringArray
var _limit := 900.0
var _cont_frames := 0
var _exit_code := 0
var _lives_seen := 3
var _proc_ms := 0.0
var _prev_us := 0
var _start_us := Time.get_ticks_usec()
var _phys_ms := 0.0
var _score_t := 0.0
var _last_score := 0


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	process_priority = -1000   # run before Main so injected presses are "just pressed"
	print("[autotest] boot took %.2fs" % (Time.get_ticks_msec() / 1000.0))
	_args = OS.get_cmdline_user_args()
	for a in _args:
		if a.begins_with("--shotdir="):
			shot_dir = a.substr(10)
		elif a.begins_with("--shots="):
			shots = Array(a.substr(8).split(",")).map(func(s): return float(s))
		elif a.begins_with("--limit="):
			_limit = float(a.substr(8))
	print("[autotest] start")


func _snap(name: String) -> void:
	if shot_dir == "":
		return
	var img := get_viewport().get_texture().get_image()
	if img == null or img.is_empty():
		return
	img.save_png(shot_dir + "/" + name + ".png")
	var p = GameManager.player
	if p != null and is_instance_valid(p) and GameManager.state != GameManager.State.TITLE:
		var sp: Vector2 = p.get_global_transform_with_canvas().origin
		var r := Rect2i(int(sp.x) - 110, int(sp.y) - 90, 220, 110)
		r = r.intersection(Rect2i(0, 0, img.get_width(), img.get_height()))
		if r.size.x > 0 and r.size.y > 0:
			var crop := img.get_region(r)
			crop.resize(r.size.x * 3, r.size.y * 3, Image.INTERPOLATE_NEAREST)
			crop.save_png(shot_dir + "/" + name + "_zoom.png")
	print("[autotest] saved ", name)


func _press(action: String, on: bool) -> void:
	if on:
		Input.action_press(action)
	else:
		Input.action_release(action)


func _release_all() -> void:
	for a in ["left", "right", "up", "down", "jump", "fire", "grenade", "interact"]:
		Input.action_release(a)


func _process(delta: float) -> void:
	t += delta
	frames += 1
	var main := get_parent()
	if _shot_i < shots.size() and t >= float(shots[_shot_i]):
		_snap("shot_%02d" % _shot_i)
		_shot_i += 1
	match phase:
		0:
			if t > 0.5:
				phase = 1
				GameManager.debug_hp_mode = "--hpmode" in _args
				main.start_game()
				GameManager.god_mode = "--god" in _args
				print("[autotest] game started god=%s" % GameManager.god_mode)
		1:
			if GameManager.player and GameManager.player.last_death != "":
				print("[autotest] died at x=%d: %s" % [GameManager.player.global_position.x, GameManager.player.last_death])
				GameManager.player.last_death = ""
			if "--stress" in _args:
				_stress(delta)
				return
			_bot(delta)
			_stats()
			if GameManager.state == GameManager.State.CLEAR:
				_report("LEVEL CLEAR")
				phase = 2
				_release_all()
			elif GameManager.state == GameManager.State.GAME_OVER:
				_report("GAME OVER")
				_release_all()
				phase = 3
			elif t > _limit:
				_report("TIMEOUT")
				_exit_code = 1
				phase = 2
		2:
			if t > 0.0:
				get_tree().quit(_exit_code)
		3:
			# hold "continue" for a few frames, then keep playing
			_cont_frames += 1
			Input.action_press("start")
			if _cont_frames > 3:
				Input.action_release("start")
				_cont_frames = 0
				if GameManager.state == GameManager.State.PLAYING:
					phase = 1


## Performance scenario: 40+ soldiers, gunships, constant fire, explosions.
var _stress_t := 0.0
var _stress_frames := 0
var _stress_start := 0


func _stress(delta: float) -> void:
	_stress_t += delta
	var lvl: Level = GameManager.level
	var p: Player = GameManager.player
	if _stress_t < 0.1:
		return
	if _stress_frames == 0:
		p.set_weapon(MachineGun.new())
		p.weapon.ammo = 99999
		for i in 40:
			var x := p.global_position.x + 120.0 + (i % 20) * 24.0
			var e := lvl.spawn_enemy("soldier" if i % 4 else "shield", Vector2(x, 280.0 - (i / 20) * 40.0), {"enter_mode": "none", "facing": -1.0})
		lvl.spawn_enemy("heli", Vector2(p.global_position.x + 300.0, 60.0), {})
		lvl.spawn_enemy("heli", Vector2(p.global_position.x + 100.0, 50.0), {})
		_stress_start = Time.get_ticks_usec()
	_stress_frames += 1
	# saturate the screen with bullets from both sides
	for i in 4:
		var y := randf_range(150.0, 290.0)
		Projectile.shoot(Projectile.Kind.ENEMY_BULLET, Vector2(CameraManager.right() - 10.0, y), Vector2(-180.0, randf_range(-20, 20)), Combat.Team.ENEMY, 1.0)
	_press("fire", true)
	_press("up", int(_stress_t) % 3 == 1)
	if _stress_frames % 40 == 0:
		FX.boom(p.global_position + Vector2(randf_range(80, 400), -20), 50.0, 3.0, Combat.Team.PLAYER)
	# keep the crowd topped up at 40
	var n := get_tree().get_nodes_in_group("enemies").size()
	if n < 40 and _stress_frames % 10 == 0:
		lvl.spawn_enemy("soldier", Vector2(CameraManager.right() - 40.0, 200.0), {"enter_mode": "drop", "facing": -1.0})
	_stats()
	if _stress_frames % 120 == 0:
		var us := Time.get_ticks_usec() - _stress_start
		print("[stress] frames=%d enemies=%d bullets=%d particles=%d avg %.2f ms/frame" % [_stress_frames, n,
			ObjectPool.active(Projectile.POOL_KEY), FX.front.count() + FX.back.count(), us / 1000.0 / _stress_frames])
	if _stress_frames >= 1200:
		_report("STRESS DONE")
		get_tree().quit()


func _stats() -> void:
	_max_enemies = maxi(_max_enemies, get_tree().get_nodes_in_group("enemies").size())
	_max_bullets = maxi(_max_bullets, ObjectPool.active(Projectile.POOL_KEY))
	if FX.ready():
		_max_particles = maxi(_max_particles, FX.front.count() + FX.back.count())
	var ms := Performance.get_monitor(Performance.TIME_PROCESS) * 1000.0 + Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS) * 1000.0
	_frame_ms_total += ms
	_proc_ms += Performance.get_monitor(Performance.TIME_PROCESS) * 1000.0
	_phys_ms += Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS) * 1000.0
	if ms > 16.6:
		_slow_frames += 1


func _report(result: String) -> void:
	var p = GameManager.player
	print("[autotest] RESULT: %s at t=%.1f" % [result, t])
	print("[autotest]   x=%d lives=%d score=%d killed=%d pows=%d continues=%d forced_clears=%d" % [
		int(p.global_position.x) if p else -1, GameManager.lives, GameManager.score,
		GameManager.enemies_killed, GameManager.hostages_rescued, GameManager.continues_used, _forced_clears])
	print("[autotest]   wall clock %.1fs for %d frames" % [(Time.get_ticks_usec() - _start_us) / 1e6, frames])
	print("[autotest]   peak enemies=%d bullets=%d particles=%d" % [_max_enemies, _max_bullets, _max_particles])


func _bot(delta: float) -> void:
	var p: Player = GameManager.player
	if p == null or not is_instance_valid(p) or GameManager.state != GameManager.State.PLAYING:
		return
	var me: Node2D = p.get_target_node()
	var pos := me.global_position
	# nearest threat on screen
	var best: Node2D = null
	var best_d := 1e9
	for e in get_tree().get_nodes_in_group("enemies"):
		if not e.alive or not CameraManager.is_visible_point(e.global_position + Vector2(0, -20), 0.0):
			continue
		var d: float = e.global_position.distance_to(pos)
		if d < best_d:
			best_d = d
			best = e
	for b in get_tree().get_nodes_in_group("bosses"):
		if b.active and b.alive:
			best = b
			best_d = absf(b.global_position.x - pos.x)
	var move := 1.0
	var aim_up := false
	if best != null:
		var dx := best.global_position.x - pos.x
		var dy := best.global_position.y - pos.y
		aim_up = dy < -34.0
		if best.get("flying") == true and dy < -60.0:
			# get under aircraft and shoot straight up
			move = signf(dx) if absf(dx) > 16.0 else 0.0
			_press("left", false)
			_press("right", false)
		if best is BossBase:
			move = -1.0 if dx < 260.0 else 0.0
			if best.state == "ram_warn" or best.state == "ram":
				move = -1.0
		elif best.get("flying") == true and dy < -60.0:
			pass
		elif best_d < 150.0 or CameraManager.locked:
			# face the enemy, hold position-ish
			move = signf(dx) * (0.0 if absf(dx) < 120.0 else 1.0)
			if move == 0.0:
				_press("left", dx < 0.0)
				_press("right", dx > 0.0)
	# no progress for a while: push forward and jump
	if GameManager.score != _last_score:
		_last_score = GameManager.score
		_score_t = 0.0
	else:
		_score_t += delta
	if _score_t > 4.0 and not (best is BossBase):
		move = 1.0
		if _score_t > 4.5:
			_stuck_t = 1.0
		if _score_t > 6.0:
			_score_t = 0.0
	if move == 0.0 and best != null:
		# tap toward the target to turn around without walking far
		var want := signf(best.global_position.x - pos.x)
		var f: float = me.get("facing")
		if f != want:
			_press("right", want > 0.0)
			_press("left", want < 0.0)
		else:
			_press("right", false)
			_press("left", false)
	elif move != 0.0:
		_press("right", move > 0.0)
		_press("left", move < 0.0)
	elif best == null:
		_press("left", false)
		_press("right", false)
	_press("up", aim_up)
	_press("fire", frames % 3 != 0)
	_press("grenade", best != null and best_d < 200.0 and frames % 70 == 0)
	# jump when stuck against something
	if absf(pos.x - _last_x) < 0.5 and move != 0.0:
		_stuck_t += delta
	else:
		_stuck_t = 0.0
	_last_x = pos.x
	_jump_hold -= delta
	if _stuck_t > 0.35 and _jump_hold <= -0.3:
		_jump_hold = 0.35
		_stuck_t = 0.0
	if best is BossBase and best.state.begins_with("mg") and frames % 50 == 0:
		_jump_hold = 0.3
	# jump over pits / water ahead
	if move != 0.0 and _jump_hold <= -0.3 and me == p and p.is_on_floor():
		var ahead := pos + Vector2(move * 30.0, -6.0)
		var gy := Combat.ground_below(ahead, 60.0)
		if gy == INF or (GameManager.level and GameManager.level.is_water_at(Vector2(ahead.x, gy + 12.0))):
			_jump_hold = 0.4
	_press("jump", _jump_hold > 0.0)
	# board the tank
	var want_tank := false
	for v in get_tree().get_nodes_in_group("vehicles"):
		if v.can_enter() and v.global_position.distance_to(p.global_position) < 50.0:
			want_tank = true
	_press("interact", want_tank and frames % 2 == 0)
	# periodic log
	_log_t += delta
	if _log_t > 20.0:
		_log_t = 0.0
		print("[autotest] t=%.0f x=%d lives=%d score=%d enemies=%d locked=%s veh=%s" % [t, int(pos.x), GameManager.lives, GameManager.score,
			get_tree().get_nodes_in_group("enemies").size(), CameraManager.locked, p.state == Player.St.IN_VEHICLE])
		for b in get_tree().get_nodes_in_group("bosses"):
			print("[autotest]   boss x=%d state=%s hp=%d phase=%d active=%s" % [b.global_position.x, b.state, b.hp, b.phase, b.active])
	# stall breaker for locked encounters
	if CameraManager.locked:
		_lock_t += delta
		if _lock_t > 40.0:
			_lock_t = 0.0
			_forced_clears += 1
			print("[autotest] forcing clear at x=%d" % int(pos.x))
			for e in get_tree().get_nodes_in_group("enemies"):
				if e.alive:
					e.take_hit({"damage": 999.0, "dir": Vector2.RIGHT, "pos": e.global_position, "kind": "explosion"})
			for b in get_tree().get_nodes_in_group("bosses"):
				if b.active and b.alive:
					b.hp = 1.0
	else:
		_lock_t = 0.0
