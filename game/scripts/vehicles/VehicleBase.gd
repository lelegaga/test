class_name VehicleBase
extends CharacterBody2D
## Rideable vehicle base.
##
## Handles boarding (E), exiting (E), independent HP, damage scaling, the
## critical-damage warning, destruction with ejection, camera handoff and the
## boarding prompt. Subclasses implement `_build_visual()`, `_drive()` and
## `_animate()`. Designed to be extended by Tank, Mech, Helicopter...

var vehicle_name := "VEHICLE"
var hp := 100.0
var max_hp := 100.0
var driver: Player = null
var destroyed := false
var facing := 1.0
var gravity := 1100.0
var hurt_size := Vector2(80, 50)
var hurt_offset := Vector2(0, -28)
## Multipliers applied to incoming damage by hit kind.
var damage_scale := {"bullet": 4.0, "explosion": 14.0, "melee": 8.0, "flame": 2.0}

var visual: Node2D
var _mat: ShaderMaterial
var _flash := 0.0
var _enter_cd := 0.0
var _invuln := 0.0
var _warn_t := 0.0
var _prompt: Node2D
var _t := 0.0


func _ready() -> void:
	add_to_group("vehicles")
	collision_layer = Combat.L_VEHICLE
	collision_mask = Combat.MASK_ACTOR
	floor_snap_length = 8.0
	var shape := CollisionShape2D.new()
	var r := RectangleShape2D.new()
	r.size = hurt_size
	shape.shape = r
	shape.position = hurt_offset
	add_child(shape)
	visual = Node2D.new()
	_mat = FX.make_flash_material()
	visual.material = _mat
	add_child(visual)
	_build_visual()
	_prompt = Node2D.new()
	_prompt.z_index = 40
	_prompt.draw.connect(_draw_prompt)
	add_child(_prompt)
	z_index = 6


func _build_visual() -> void:
	pass


func can_enter() -> bool:
	return not destroyed and driver == null


func enter(p: Player) -> void:
	driver = p
	_enter_cd = 0.3
	_invuln = 0.5
	Combat.register(self, Combat.Team.PLAYER)
	AudioManager.play("weapon", 0.0)
	AudioManager.set_loop("engine", true, -10.0)
	FX.popup(global_position + Vector2(0, -80), vehicle_name + "!", Color("5ad0ff"), 2)
	_on_enter()


func exit(ejected: bool = false) -> void:
	if driver == null:
		return
	var p := driver
	driver = null
	Combat.unregister(self)
	AudioManager.set_loop("engine", false)
	p.exit_vehicle(ejected)
	_on_exit()


func _on_enter() -> void:
	pass


func _on_exit() -> void:
	pass


func _physics_process(delta: float) -> void:
	_t += delta
	_flash = maxf(_flash - delta * 6.0, 0.0)
	_mat.set_shader_parameter("flash", _flash)
	_enter_cd -= delta
	_invuln -= delta
	_prompt.queue_redraw()
	if destroyed:
		velocity.y = minf(velocity.y + gravity * delta, 600.0)
		velocity.x = move_toward(velocity.x, 0.0, 300.0 * delta)
		move_and_slide()
		if randf() < 0.25:
			FX.smoke(global_position + Vector2(randf_range(-30, 30), -40), 1, 5.0)
		return
	if driver != null:
		_drive(delta)
		if Input.is_action_just_pressed("interact") and _enter_cd <= 0.0:
			exit(false)
			return
		if hp < max_hp * 0.3:
			_warn_t -= delta
			if _warn_t <= 0.0:
				_warn_t = 0.8
				AudioManager.play("alert", 0.0, -4.0)
			if randf() < 0.3:
				FX.smoke(global_position + Vector2(randf_range(-20, 20), -50), 1, 4.0)
			if randf() < 0.05:
				FX.spark(global_position + Vector2(randf_range(-30, 30), -30), Vector2.UP, 3)
	else:
		velocity.x = move_toward(velocity.x, 0.0, 600.0 * delta)
	velocity.y = minf(velocity.y + gravity * delta, 600.0)
	move_and_slide()
	if driver != null:
		var min_x := CameraManager.left() + hurt_size.x * 0.5
		var max_x := CameraManager.right() - hurt_size.x * 0.5
		if global_position.x < min_x:
			global_position.x = min_x
			velocity.x = maxf(velocity.x, 0.0)
		elif CameraManager.locked and global_position.x > max_x:
			global_position.x = max_x
			velocity.x = minf(velocity.x, 0.0)
		var lvl: Level = GameManager.level
		if global_position.y > CameraManager.level_bottom + 40.0 or (lvl != null and lvl.is_water_at(global_position + Vector2(0, -4))):
			hp = 0.0
			_destroy()
			return
	_animate(delta)


func _drive(_delta: float) -> void:
	pass


func _animate(_delta: float) -> void:
	pass


func _draw_prompt() -> void:
	if driver != null or destroyed:
		return
	var p: Player = GameManager.player
	if p == null or p.state != Player.St.NORMAL:
		return
	var near := p.global_position.distance_to(global_position) < 90.0
	if int(_t * 3.0) % 2 == 0 or near:
		PixelFont.draw_centered(_prompt, "PRESS E" if near else "IN", Vector2(0, -86), 1, Color("5ad0ff"))
		_prompt.draw_colored_polygon(PackedVector2Array([Vector2(-5, -76), Vector2(5, -76), Vector2(0, -70)]), Color("fff27a"))


# =============================================================== damage
func get_hurt_rect() -> Rect2:
	return Rect2(global_position + hurt_offset - hurt_size * 0.5, hurt_size)


func is_hittable() -> bool:
	return driver != null and not destroyed and _invuln <= 0.0 and not GameManager.god_mode


func get_aim_point() -> Vector2:
	return global_position + hurt_offset


func take_hit(info: Dictionary) -> int:
	if not is_hittable():
		return Combat.HIT_NONE
	var k: String = info.get("kind", "bullet")
	hp -= float(info.get("damage", 1.0)) * float(damage_scale.get(k, 4.0))
	_flash = 0.8
	CameraShakeManager.shake(0.12 if k == "bullet" else 0.35)
	AudioManager.play("tink" if k == "bullet" else "hit", 0.2, -4.0)
	FX.spark(info.get("pos", global_position), -Vector2(info.get("dir", Vector2.RIGHT)), 3)
	if hp <= 0.0:
		_destroy()
	return Combat.HIT_OK


func _destroy() -> void:
	if destroyed:
		return
	destroyed = true
	hp = 0.0
	Combat.unregister(self)
	collision_layer = 0
	remove_from_group("vehicles")
	FX.boom(global_position + hurt_offset, 70.0, 10.0, Combat.Team.PLAYER, self, 0.8)
	FX.chain(global_position + hurt_offset, hurt_size * 0.4, 5, 0.12, 30.0)
	FX.debris(global_position + hurt_offset, 20, Color("5d6b3a"), 340, 5)
	visual.modulate = Color(0.3, 0.27, 0.25)
	GameManager.show_message("VEHICLE LOST!", 1.2, false)
	exit(true)
	_on_destroyed()


func _on_destroyed() -> void:
	pass
