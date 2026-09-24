class_name Player
extends CharacterBody2D
## Arcade soldier: snappy run, slightly floaty jump with air control,
## 8-way aiming (keys or mouse), knife when enemies are close, grenades,
## vehicle boarding. Weapons live in WeaponBase objects.

signal weapon_changed(weapon: WeaponBase)

enum St { NORMAL, DEAD, IN_VEHICLE }

const RUN_SPEED := 138.0
const CRAWL_SPEED := 48.0
const ACCEL := 2600.0
const DECEL := 3000.0
const AIR_ACCEL := 1000.0
const GRAVITY := 1150.0
const JUMP_V := -390.0
const MAX_FALL := 540.0
const COYOTE := 0.09
const JUMP_BUFFER := 0.12
const MAX_GRENADES := 10

var facing := 1.0
var state: int = St.NORMAL
var hp := GameManager.PLAYER_MAX_HP
var invuln := 0.0
var weapon: WeaponBase
var grenades := MAX_GRENADES
var crouching := false
var aim_target := 0.0    # facing-local aim angle: 0 forward, -PI/2 up, PI/2 down
var aim_cur := 0.0
var vehicle: Node2D = null

var _coyote := 0.0
var _jump_buf := 0.0
var _fire_buf := 0.0
var _drop_timer := 0.0
var _anim_t := 0.0
var _knife_t := -1.0
var _grenade_cd := 0.0
var _dead_t := 0.0
var _recoil := 0.0
var _mouse_aim_t := 0.0
var _was_on_floor := true
var _flash := 0.0

var visual: Node2D
var body: Sprite2D
var arm: Sprite2D
var _mat: ShaderMaterial
var _shape: CollisionShape2D
var _stand_shape := RectangleShape2D.new()
var _crouch_shape := RectangleShape2D.new()
var _frames: Dictionary
var _gun: Dictionary


func _ready() -> void:
	add_to_group("player")
	collision_layer = Combat.L_PLAYER
	collision_mask = Combat.MASK_ACTOR
	floor_snap_length = 6.0
	floor_max_angle = deg_to_rad(50)
	_stand_shape.size = Vector2(12, 38)
	_crouch_shape.size = Vector2(14, 24)
	_shape = CollisionShape2D.new()
	_shape.shape = _stand_shape
	_shape.position = Vector2(0, -19)
	add_child(_shape)

	visual = Node2D.new()
	_mat = FX.make_flash_material()
	visual.material = _mat
	add_child(visual)
	_frames = Art.frames("player")
	body = Sprite2D.new()
	body.centered = false
	body.offset = Vector2(-16, -48)
	body.use_parent_material = true
	visual.add_child(body)
	arm = Sprite2D.new()
	arm.centered = false
	arm.use_parent_material = true
	visual.add_child(arm)
	z_index = 10

	set_weapon(Pistol.new())
	body.texture = _frames["idle0"]
	arm.position = _shoulder_local()
	Combat.register(self, Combat.Team.PLAYER)
	GameManager.player = self


func _exit_tree() -> void:
	Combat.unregister(self)


# =============================================================== weapons
func set_weapon(w: WeaponBase) -> void:
	weapon = w
	_gun = Art.gun("player", w.id)
	arm.texture = _gun.tex
	arm.offset = -_gun.pivot
	weapon_changed.emit(w)


func give_weapon(id: String) -> void:
	if weapon.id == id:
		weapon.add_ammo(WeaponFactory.create(id).ammo)
	else:
		set_weapon(WeaponFactory.create(id))
	AudioManager.play("weapon", 0.0)
	FX.popup(global_position + Vector2(0, -60), weapon.display_name + "!", Color("fff27a"))


func add_grenades(n: int) -> void:
	grenades = mini(grenades + n, 99)


# =============================================================== main loop
func _physics_process(delta: float) -> void:
	_flash = maxf(_flash - delta * 6.0, 0.0)
	_mat.set_shader_parameter("flash", _flash)
	match state:
		St.NORMAL:
			_update_normal(delta)
		St.DEAD:
			_update_dead(delta)
		St.IN_VEHICLE:
			if vehicle != null and is_instance_valid(vehicle):
				global_position = vehicle.global_position
			weapon.update(delta)


func _update_normal(delta: float) -> void:
	invuln = maxf(invuln - delta, 0.0)
	_grenade_cd -= delta
	_knife_t -= delta
	_mouse_aim_t -= delta
	weapon.update(delta)
	if _drop_timer > 0.0:
		_drop_timer -= delta
		if _drop_timer <= 0.0:
			set_collision_mask_value(5, true)

	var dir := Input.get_axis("left", "right")
	var on_floor := is_on_floor()
	_coyote = COYOTE if on_floor else _coyote - delta

	_set_crouch(on_floor and Input.is_action_pressed("down"))
	if dir != 0.0 and _mouse_aim_t <= 0.0:
		facing = signf(dir)

	# horizontal: instant turn-around on the ground, softer control in the air
	var speed := CRAWL_SPEED if crouching else RUN_SPEED
	var target := dir * speed
	if on_floor and dir != 0.0 and signf(velocity.x) != signf(dir):
		velocity.x = 0.0
	var accel := ACCEL if on_floor else AIR_ACCEL
	if dir == 0.0 and on_floor:
		accel = DECEL
	velocity.x = move_toward(velocity.x, target, accel * delta)

	# jumping with buffer + coyote time; down+jump drops through platforms
	if Input.is_action_just_pressed("jump"):
		_jump_buf = JUMP_BUFFER
	else:
		_jump_buf -= delta
	if _jump_buf > 0.0 and _coyote > 0.0:
		_jump_buf = 0.0
		_coyote = 0.0
		if Input.is_action_pressed("down") and _standing_on_oneway():
			set_collision_mask_value(5, false)
			_drop_timer = 0.25
			position.y += 2.0
		else:
			velocity.y = JUMP_V
			_set_crouch(false)
			AudioManager.play("jump", 0.05, -8.0)
			FX.dust(global_position, 3)
	if velocity.y < 0.0 and not Input.is_action_pressed("jump"):
		velocity.y += GRAVITY * 1.3 * delta   # short hop when released early
	velocity.y = minf(velocity.y + GRAVITY * delta, MAX_FALL)

	move_and_slide()

	if is_on_floor() and not _was_on_floor:
		FX.dust(global_position, 4)
		AudioManager.play("land", 0.1, -10.0)
	_was_on_floor = is_on_floor()

	_clamp_to_camera()
	if global_position.y > CameraManager.level_bottom + 40.0:
		die({"kind": "fall"})
		return
	if GameManager.level != null and GameManager.level.is_water_at(global_position):
		die({"kind": "water"})
		return

	_update_aim(dir, is_on_floor(), delta)
	_update_fire(delta)
	if Input.is_action_just_pressed("grenade"):
		_throw_grenade()
	if Input.is_action_just_pressed("interact"):
		_try_enter_vehicle()
	_animate(delta)


func _clamp_to_camera() -> void:
	var min_x := CameraManager.left() + 8.0
	var max_x := CameraManager.right() - 8.0
	if global_position.x < min_x:
		global_position.x = min_x
		velocity.x = maxf(velocity.x, 0.0)
	elif global_position.x > max_x and CameraManager.locked:
		global_position.x = max_x
		velocity.x = minf(velocity.x, 0.0)


func _standing_on_oneway() -> bool:
	for i in get_slide_collision_count():
		var c := get_slide_collision(i)
		var col := c.get_collider()
		if c.get_normal().y < -0.5 and col is CollisionObject2D and (col.collision_layer & Combat.L_ONEWAY) != 0:
			return true
	return false


func _set_crouch(on: bool) -> void:
	if on == crouching:
		return
	crouching = on
	_shape.shape = _crouch_shape if on else _stand_shape
	_shape.position = Vector2(0, -12) if on else Vector2(0, -19)


# =============================================================== aiming / firing
func _update_aim(dir: float, on_floor: bool, delta: float) -> void:
	var mouse_firing := Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT)
	if mouse_firing:
		_mouse_aim_t = 0.25
		var d := get_global_mouse_position() - (global_position + Vector2(0, -26))
		if absf(d.x) > 2.0:
			facing = signf(d.x)
		var a := atan2(d.y, absf(d.x))
		a = snappedf(a, PI / 4.0)
		aim_target = clampf(a, -PI / 2.0, 0.0 if on_floor else PI / 2.0)
	elif _mouse_aim_t <= 0.0:
		if Input.is_action_pressed("up"):
			aim_target = -PI / 4.0 if dir != 0.0 else -PI / 2.0
		elif Input.is_action_pressed("down") and not on_floor:
			aim_target = PI / 2.0
		else:
			aim_target = 0.0
	if crouching and aim_target > 0.0:
		aim_target = 0.0
	if weapon.aim_sweep_speed > 0.0:
		aim_cur = move_toward(aim_cur, aim_target, weapon.aim_sweep_speed * delta)
	else:
		aim_cur = aim_target


func aim_world() -> float:
	return aim_cur if facing > 0.0 else PI - aim_cur


func _shoulder_local() -> Vector2:
	return Vector2(2, -19) if crouching else Vector2(2, -28)


func muzzle_world() -> Vector2:
	var m: Vector2 = _shoulder_local() + (_gun.muzzle as Vector2).rotated(aim_cur)
	return global_position + Vector2(m.x * facing, m.y)


func _update_fire(delta: float) -> void:
	_recoil = maxf(_recoil - delta * 30.0, 0.0)
	if Input.is_action_just_pressed("fire"):
		_fire_buf = 0.12
	else:
		_fire_buf -= delta
	var fresh := _fire_buf > 0.0
	if not fresh and not Input.is_action_pressed("fire"):
		return
	if _knife_t > -0.1 and not fresh:
		return
	if fresh and _try_knife():
		_fire_buf = 0.0
		return
	var origin := muzzle_world()
	# don't shoot through a wall we're pressed against
	var wall := Combat.ray_world(global_position + Vector2(0, -26 if not crouching else -16), origin)
	if not wall.is_empty():
		origin = wall.position - Vector2.from_angle(aim_world()) * 2.0
	if weapon.fire(self, origin, aim_world(), fresh):
		_fire_buf = 0.0
		_recoil = 2.0
		if weapon.shells:
			FX.shell(global_position + Vector2(4 * facing, _shoulder_local().y), facing)
		if weapon.is_empty():
			set_weapon(Pistol.new())
			FX.popup(global_position + Vector2(0, -60), "OUT OF AMMO", Color("ff8080"))


func _try_knife() -> bool:
	var r := Rect2(global_position.x + (4.0 if facing > 0 else -30.0), global_position.y - 40.0, 26.0, 40.0)
	for t in Combat.rect_query(r, Combat.Team.PLAYER):
		if t.get("meleeable") == true:
			_knife_t = 0.2
			AudioManager.play("knife", 0.1)
			t.take_hit({"damage": 6.0, "dir": Vector2(facing, -0.3), "pos": t.get_hurt_rect().get_center(), "kind": "melee", "source": self})
			return true
	return false


func _throw_grenade() -> void:
	if grenades <= 0 or _grenade_cd > 0.0:
		return
	grenades -= 1
	_grenade_cd = 0.3
	var up := Input.is_action_pressed("up")
	var v := Vector2(facing * (110.0 if up else 190.0), -330.0 if up else -250.0)
	v.x += velocity.x * 0.4
	Projectile.shoot(Projectile.Kind.GRENADE, global_position + Vector2(6 * facing, _shoulder_local().y - 4), v, Combat.Team.PLAYER, 12.0)
	AudioManager.play("throw", 0.1)


# =============================================================== vehicles
func _try_enter_vehicle() -> void:
	for v in get_tree().get_nodes_in_group("vehicles"):
		if v.has_method("can_enter") and v.can_enter() and v.global_position.distance_to(global_position) < 56.0:
			v.enter(self)
			state = St.IN_VEHICLE
			vehicle = v
			visible = false
			collision_layer = 0
			collision_mask = 0
			_set_crouch(false)
			CameraManager.set_target(v)
			return


func exit_vehicle(ejected: bool) -> void:
	if state != St.IN_VEHICLE:
		return
	var v := vehicle
	vehicle = null
	state = St.NORMAL
	visible = true
	collision_layer = Combat.L_PLAYER
	collision_mask = Combat.MASK_ACTOR
	global_position = v.global_position + Vector2(0, -50)
	velocity = Vector2(-facing * 60.0, -420.0 if ejected else -300.0)
	invuln = 2.0 if ejected else 0.6
	CameraManager.set_target(self)


func get_target_node() -> Node2D:
	if state == St.IN_VEHICLE and vehicle != null and is_instance_valid(vehicle):
		return vehicle
	return self


func get_aim_point() -> Vector2:
	return global_position + (Vector2(0, -12) if crouching else Vector2(0, -22))


# =============================================================== damage
func get_hurt_rect() -> Rect2:
	if crouching:
		return Rect2(global_position.x - 7, global_position.y - 24, 14, 23)
	return Rect2(global_position.x - 6, global_position.y - 38, 12, 36)


func is_hittable() -> bool:
	return state == St.NORMAL and invuln <= 0.0 and not GameManager.god_mode


func take_hit(info: Dictionary) -> int:
	if not is_hittable():
		return Combat.HIT_NONE
	if GameManager.debug_hp_mode:
		hp -= float(info.get("damage", 1.0)) * 12.0
		_flash = 1.0
		invuln = 0.4
		CameraShakeManager.shake(0.2)
		FX.cartoon_hit(global_position + Vector2(0, -30))
		AudioManager.play("hit", 0.1)
		if hp > 0.0:
			return Combat.HIT_OK
	die(info)
	return Combat.HIT_OK


func die(info: Dictionary) -> void:
	if state == St.DEAD:
		return
	if GameManager.god_mode and info.get("kind") != "fall" and info.get("kind") != "water":
		return
	state = St.DEAD
	_dead_t = 0.0
	_set_crouch(false)
	var kind: String = info.get("kind", "")
	var d: Vector2 = info.get("dir", Vector2(-facing, 0))
	velocity = Vector2(signf(d.x if d.x != 0.0 else -facing) * 110.0, -260.0)
	body.texture = _frames["fly"]
	arm.visible = false
	_flash = 1.0
	CameraShakeManager.shake(0.4)
	CameraShakeManager.hit_stop(0.08)
	FX.cartoon_hit(global_position + Vector2(0, -30))
	AudioManager.play("player_die", 0.0)
	if kind == "water":
		for i in 10:
			FX.front.spawn(ParticleLayer.DROP, global_position, Vector2(randf_range(-80, 80), randf_range(-240, -100)), 0.8, 2, Color("bfe8ff"), 600)
		visual.visible = false


func _update_dead(delta: float) -> void:
	_dead_t += delta
	velocity.y = minf(velocity.y + GRAVITY * delta, MAX_FALL)
	if is_on_floor():
		velocity.x = move_toward(velocity.x, 0.0, 600.0 * delta)
	move_and_slide()
	visual.rotation = lerpf(visual.rotation, -PI / 2.0 * facing if is_on_floor() else visual.rotation + 0.3, 0.2)
	if _dead_t > 1.4:
		visual.rotation = 0.0
		if GameManager.lose_life():
			respawn()
		else:
			set_physics_process(false)
			visible = false
			GameManager.level.on_game_over()


func respawn() -> void:
	var x := CameraManager.left() + 90.0
	var y := CameraManager.top() - 30.0
	if GameManager.level != null:
		var p: Vector2 = GameManager.level.safe_spawn_point(x)
		x = p.x
		y = minf(y, p.y - 60.0)
	global_position = Vector2(x, y)
	velocity = Vector2(0, 100)
	state = St.NORMAL
	visible = true
	visual.visible = true
	arm.visible = true
	hp = GameManager.PLAYER_MAX_HP
	invuln = 2.5
	grenades = MAX_GRENADES
	set_weapon(Pistol.new())
	set_collision_mask_value(5, true)
	set_physics_process(true)


# =============================================================== animation
func _animate(delta: float) -> void:
	visual.scale.x = facing
	var bob := 0
	var key := "idle0"
	if _knife_t > 0.0:
		key = "knife"
	elif not is_on_floor():
		key = "jump" if velocity.y < 0.0 else "fall"
	elif crouching:
		if absf(velocity.x) > 5.0:
			_anim_t += delta * 6.0
			key = "crouch1" if int(_anim_t) % 2 == 0 else "crouch"
		else:
			key = "crouch"
	elif absf(velocity.x) > 10.0:
		_anim_t += delta * absf(velocity.x) / RUN_SPEED * 13.0
		var f := int(_anim_t) % 6
		key = "run%d" % f
		bob = 1 if f % 3 == 0 else 0
	else:
		_anim_t += delta * 2.0
		var f := int(_anim_t) % 2
		key = "idle%d" % f
		bob = f
	body.texture = _frames[key]
	arm.visible = _knife_t <= 0.0
	arm.position = _shoulder_local() + Vector2(-_recoil, bob)
	if crouching and key == "crouch1":
		arm.position.y += 1
	arm.rotation = aim_cur
	# invulnerability blink
	visual.modulate.a = 0.35 if invuln > 0.0 and int(invuln * 20.0) % 2 == 0 else 1.0
