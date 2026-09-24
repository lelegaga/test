class_name EnemyBase
extends CharacterBody2D
## Shared enemy behaviour: physics, hurt flash, knockback, entry modes
## (walk-in / parachute drop / leap), exaggerated cartoon deaths, target
## helpers and shooting. Subclasses implement `_think()` and may override
## `_build_visual()`, `_modify_hit()` and `_on_death()`.

var alive := true
var hp := 3.0
var max_hp := 3.0
var score_value := 100
var facing := -1.0
var speed := 60.0
var gravity := 1100.0
var flying := false
var meleeable := true
var humanoid := true
var enter_mode := "walk"          # walk | drop | jump | none
var hurt_size := Vector2(16, 36)
var hurt_offset := Vector2(0, -19)
var body_style := "soldier"
var gun_id := "rifle"
var knock_resist := 0.0
var corpse_time := 1.4
var home_x := 0.0

var state := "enter"
var state_t := 0.0
var aim := 0.0                    # facing-local aim angle
var visual: Node2D
var body: Sprite2D
var arm: Sprite2D
var frames: Dictionary
var gun: Dictionary

var _mat: ShaderMaterial
var _flash := 0.0
var _anim_t := 0.0
var _dead_t := 0.0
var _death_kind := ""
var _landed_dead := false
var _parachute: Sprite2D
var _knock := 0.0
var _shape: CollisionShape2D

static var _sep_frame := -1
static var _sep_list: Array = []


func _ready() -> void:
	add_to_group("enemies")
	collision_layer = Combat.L_ENEMY
	collision_mask = Combat.MASK_ACTOR
	floor_snap_length = 6.0
	home_x = global_position.x
	_shape = CollisionShape2D.new()
	var r := RectangleShape2D.new()
	r.size = hurt_size - Vector2(2, 0)
	_shape.shape = r
	_shape.position = hurt_offset
	add_child(_shape)
	visual = Node2D.new()
	_mat = FX.make_flash_material()
	visual.material = _mat
	add_child(visual)
	_build_visual()
	Combat.register(self, Combat.Team.ENEMY)
	z_index = 5
	if enter_mode == "drop":
		_parachute = Sprite2D.new()
		_parachute.texture = Art.tex("parachute")
		_parachute.position = Vector2(0, -62)
		visual.add_child(_parachute)
		state = "drop"
	elif enter_mode == "jump":
		velocity = Vector2(facing * 90.0, -380.0)
		state = "enter"
	_on_ready()


func _exit_tree() -> void:
	Combat.unregister(self)


## Override for extra setup after visuals exist.
func _on_ready() -> void:
	pass


func _build_visual() -> void:
	frames = Art.frames(body_style)
	body = Sprite2D.new()
	body.centered = false
	body.offset = Vector2(-16, -48)
	body.use_parent_material = true
	body.texture = frames["idle0"]
	visual.add_child(body)
	if gun_id != "":
		gun = Art.gun(body_style, gun_id)
		arm = Sprite2D.new()
		arm.centered = false
		arm.use_parent_material = true
		arm.texture = gun.tex
		arm.offset = -gun.pivot
		arm.position = Vector2(2, -28)
		visual.add_child(arm)


# =============================================================== loop
func _physics_process(delta: float) -> void:
	_flash = maxf(_flash - delta * 7.0, 0.0)
	_mat.set_shader_parameter("flash", _flash)
	if not alive:
		_update_dead(delta)
		return
	state_t += delta
	if state == "drop":
		velocity = Vector2(sin(state_t * 2.0) * 20.0, 70.0)
		move_and_slide()
		visual.rotation = sin(state_t * 2.0) * 0.1
		if is_on_floor():
			visual.rotation = 0.0
			if _parachute:
				_parachute.queue_free()
				_parachute = null
			FX.dust(global_position, 4)
			set_state("alert")
		_animate(delta)
		return
	_think(delta)
	if not flying:
		velocity.y = minf(velocity.y + gravity * delta, 600.0)
	if _knock != 0.0:
		velocity.x += _knock
		_knock = move_toward(_knock, 0.0, 900.0 * delta)
	move_and_slide()
	if humanoid and not flying:
		global_position.x += _separation() * delta
	if not flying:
		if global_position.y > CameraManager.level_bottom + 80.0:
			_remove()
			return
		var lvl: Level = GameManager.level
		if lvl != null and lvl.is_water_at(global_position):
			FX.dust(global_position, 3)
			die({"kind": "water", "dir": Vector2.ZERO})
			return
	if global_position.x < CameraManager.left() - 220.0 and state != "enter":
		_remove()
		return
	_animate(delta)


## Soft push so crowds of soldiers spread out instead of stacking.
func _separation() -> float:
	var f := Engine.get_physics_frames()
	if f != _sep_frame:
		_sep_frame = f
		_sep_list = get_tree().get_nodes_in_group("enemies")
	var push := 0.0
	for e in _sep_list:
		if e == self or not is_instance_valid(e) or not e.humanoid:
			continue
		var d: float = global_position.x - e.global_position.x
		if absf(d) < 16.0 and absf(global_position.y - e.global_position.y) < 24.0:
			var s := signf(d) if d != 0.0 else (1.0 if get_instance_id() > e.get_instance_id() else -1.0)
			push += s * (16.0 - absf(d)) * 8.0
	return push


## Override: AI.
func _think(_delta: float) -> void:
	pass


func set_state(s: String) -> void:
	state = s
	state_t = 0.0


func _remove() -> void:
	if alive:
		alive = false
		Combat.unregister(self)
	queue_free()


# =============================================================== helpers
func target_pos() -> Vector2:
	return GameManager.target_pos()


func dx_to_target() -> float:
	return target_pos().x - global_position.x


func dist_to_target() -> float:
	return target_pos().distance_to(global_position + hurt_offset)


func face_target() -> void:
	var d := dx_to_target()
	if absf(d) > 4.0:
		facing = signf(d)


func on_screen(margin: float = -12.0) -> bool:
	return CameraManager.is_visible_point(global_position + hurt_offset, margin)


func can_see_target() -> bool:
	if not GameManager.target_alive():
		return false
	var eye := global_position + Vector2(0, -30)
	return Combat.ray_world(eye, target_pos()).is_empty()


func edge_ahead() -> bool:
	var from := global_position + Vector2(facing * 12.0, -4.0)
	return Combat.ray_world(from, from + Vector2(0, 40), Combat.MASK_ACTOR).is_empty()


func wall_ahead() -> bool:
	var from := global_position + Vector2(0, -12)
	return not Combat.ray_world(from, from + Vector2(facing * 16.0, 0), Combat.L_WORLD | Combat.L_PROPS).is_empty()


## Facing-local aim toward the target, clamped to a cone.
func aim_at_target(max_up: float = 0.7, max_down: float = 0.5) -> float:
	var shoulder := global_position + Vector2(2 * facing, -28)
	var d := target_pos() - shoulder
	var a := atan2(d.y, absf(d.x))
	aim = clampf(a, -max_up, max_down)
	return aim


func world_angle(local_a: float) -> float:
	return local_a if facing > 0.0 else PI - local_a


func muzzle_pos() -> Vector2:
	if gun.is_empty():
		return global_position + Vector2(12 * facing, -28)
	var m: Vector2 = arm.position + (gun.muzzle as Vector2).rotated(aim)
	return global_position + Vector2(m.x * facing, m.y)


func shoot(kind: int = Projectile.Kind.ENEMY_BULLET, spd: float = 180.0, dmg: float = 1.0, spread: float = 0.0) -> Projectile:
	var o := muzzle_pos()
	var a := world_angle(aim) + randf_range(-spread, spread)
	FX.muzzle(o, a, 7.0)
	AudioManager.play("enemy_shot", 0.1, -3.0)
	return Projectile.shoot(kind, o, Vector2.from_angle(a) * spd, Combat.Team.ENEMY, dmg)


func throw_grenade() -> void:
	var d := dx_to_target()
	var t := 0.9
	var vx := clampf(d / t, -220.0, 220.0)
	Projectile.shoot(Projectile.Kind.ENEMY_GRENADE, global_position + Vector2(6 * facing, -34), Vector2(vx, -300.0), Combat.Team.ENEMY, 1.0)
	AudioManager.play("throw", 0.1)


func alert_popup() -> void:
	FX.popup(global_position + Vector2(0, -56), "!", Color("ff5a3a"), 2)
	AudioManager.play("alert", 0.1, -6.0)


func walk(dir: float, spd: float, delta: float) -> void:
	velocity.x = move_toward(velocity.x, dir * spd, 900.0 * delta)
	if dir != 0.0:
		facing = signf(dir)


func stop(delta: float) -> void:
	velocity.x = move_toward(velocity.x, 0.0, 1200.0 * delta)


# =============================================================== damage
func get_hurt_rect() -> Rect2:
	return Rect2(global_position + hurt_offset - hurt_size * 0.5, hurt_size)


func is_hittable() -> bool:
	return alive


## Override to block hits (shields). Return Combat.HIT_BLOCKED to deflect.
func _modify_hit(_info: Dictionary) -> int:
	return Combat.HIT_OK


func take_hit(info: Dictionary) -> int:
	if not alive:
		return Combat.HIT_NONE
	var r := _modify_hit(info)
	if r != Combat.HIT_OK:
		return r
	var dmg: float = info.get("damage", 1.0)
	hp -= dmg
	_flash = 1.0
	AudioManager.play("hit", 0.2, -8.0)
	var dir: Vector2 = info.get("dir", Vector2.ZERO)
	if not flying and knock_resist < 1.0:
		_knock += dir.x * 30.0 * float(info.get("knockback", 1.0)) * (1.0 - knock_resist)
	_on_hurt(info)
	if hp <= 0.0:
		die(info)
	return Combat.HIT_OK


func _on_hurt(_info: Dictionary) -> void:
	pass


func die(info: Dictionary) -> void:
	if not alive:
		return
	alive = false
	remove_from_group("enemies")
	Combat.unregister(self)
	collision_layer = 0
	collision_mask = Combat.L_WORLD | Combat.L_PROPS
	GameManager.enemies_killed += 1
	GameManager.add_score(score_value, global_position + Vector2(0, -50))
	_death_kind = info.get("kind", "bullet")
	var dir: Vector2 = info.get("dir", Vector2(-facing, 0))
	_on_death(info)
	if not humanoid:
		return
	if _parachute:
		_parachute.queue_free()
		_parachute = null
	var sx := signf(dir.x) if dir.x != 0.0 else -facing
	match _death_kind:
		"explosion":
			velocity = Vector2(sx * randf_range(120, 200), randf_range(-380, -300))
		"flame":
			velocity = Vector2(sx * 30.0, -80.0)
			body.texture = frames["burnt"]
		"melee":
			velocity = Vector2(sx * 160.0, -160.0)
			CameraShakeManager.hit_stop(0.05)
			CameraShakeManager.shake(0.15)
		"water":
			velocity = Vector2(0, 60)
		_:
			velocity = Vector2(sx * randf_range(70, 110), -170.0)
	if _death_kind != "flame":
		body.texture = frames["fly"]
	if arm:
		arm.visible = false
	_flash = 1.0
	FX.cartoon_hit(global_position + Vector2(0, -30))
	if randf() < 0.7:
		AudioManager.play("scream", 0.15, -4.0)
	# the gun goes flying
	FX.debris(global_position + Vector2(0, -26), 1, Color("3a3c44"), 150, 4)


## Override for special deaths (vehicle explosions).
func _on_death(_info: Dictionary) -> void:
	pass


func _update_dead(delta: float) -> void:
	_dead_t += delta
	if not humanoid:
		return
	velocity.y = minf(velocity.y + gravity * delta, 600.0)
	var was_floor := is_on_floor()
	move_and_slide()
	if not is_on_floor():
		visual.rotation += delta * (10.0 if _death_kind == "explosion" else 4.0) * -facing
	elif not _landed_dead and _dead_t > 0.1:
		_landed_dead = true
		visual.rotation = 0.0
		if _death_kind != "flame":
			body.texture = frames["dead"]
			body.offset = Vector2(-24, -16)
		FX.dust(global_position, 3)
		velocity.x *= 0.3
	if is_on_floor() and was_floor:
		velocity.x = move_toward(velocity.x, 0.0, 500.0 * delta)
	if _death_kind == "flame" and int(_dead_t * 10) % 3 == 0:
		FX.fire(global_position + Vector2(0, -20), 1, 6)
	if _dead_t > corpse_time:
		visual.visible = int(_dead_t * 16.0) % 2 == 0
	if _dead_t > corpse_time + 0.6:
		queue_free()


# =============================================================== animation
func _animate(delta: float) -> void:
	visual.scale.x = facing
	if not humanoid:
		return
	var key := "idle0"
	var bob := 0
	if state == "drop":
		key = "parachute"
	elif not is_on_floor():
		key = "jump" if velocity.y < 0.0 else "fall"
	elif _crouching():
		key = "crouch"
	elif absf(velocity.x) > 8.0:
		_anim_t += delta * absf(velocity.x) / 90.0 * 10.0
		var f := int(_anim_t) % 6
		key = "run%d" % f
		bob = 1 if f % 3 == 0 else 0
	else:
		_anim_t += delta * 2.0
		var f := int(_anim_t) % 2
		key = "idle%d" % f
		bob = f
	key = _override_frame(key)
	body.texture = frames[key]
	if arm:
		arm.visible = key != "parachute" and key != "knife" and _show_arm()
		arm.position = (Vector2(2, -19) if key.begins_with("crouch") else Vector2(2, -28)) + Vector2(0, bob)
		arm.rotation = aim


func _crouching() -> bool:
	return false


func _show_arm() -> bool:
	return true


func _override_frame(key: String) -> String:
	return key
