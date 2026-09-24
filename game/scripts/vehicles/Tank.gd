class_name Tank
extends VehicleBase
## Light battle tank ("SV-1").
##   A/D move, Space hop, W/S aim the vulcan turret,
##   J vulcan cannon (rapid fire), K main cannon (explosive shells).
## Runs over infantry and has its own HP; when destroyed the pilot is ejected.

const SPEED := 118.0
const ACCEL := 700.0
const HOP_V := -340.0

var turret_angle := 0.0     # facing-local
var _mg_cd := 0.0
var _cannon_cd := 0.0
var _tread_t := 0.0
var _recoil := 0.0
var _hull: Sprite2D
var _tread: Sprite2D
var _vulcan: Sprite2D
var _crush_cd := 0.0


func _init() -> void:
	vehicle_name = "SV-1 TANK"
	hp = 100.0
	max_hp = 100.0
	hurt_size = Vector2(84, 44)
	hurt_offset = Vector2(0, -22)


func _build_visual() -> void:
	_tread = Sprite2D.new()
	_tread.texture = Art.tank_tread(0)
	_tread.centered = false
	_tread.offset = Vector2(-48, -18)
	_tread.use_parent_material = true
	visual.add_child(_tread)
	_hull = Sprite2D.new()
	_hull.texture = Art.tex("tank_hull")
	_hull.centered = false
	_hull.offset = Vector2(-48, -46)
	_hull.use_parent_material = true
	visual.add_child(_hull)
	_vulcan = Sprite2D.new()
	_vulcan.texture = Art.tex("tank_vulcan")
	_vulcan.centered = false
	_vulcan.offset = Vector2(-4, -4)
	_vulcan.position = Vector2(-2, -37)
	_vulcan.use_parent_material = true
	visual.add_child(_vulcan)


func _drive(delta: float) -> void:
	_mg_cd -= delta
	_cannon_cd -= delta
	_crush_cd -= delta
	_recoil = maxf(_recoil - delta * 20.0, 0.0)
	var dir := Input.get_axis("left", "right")
	if dir != 0.0:
		facing = signf(dir)
	velocity.x = move_toward(velocity.x, dir * SPEED, ACCEL * delta)
	if Input.is_action_just_pressed("jump") and is_on_floor():
		velocity.y = HOP_V
		FX.dust(global_position + Vector2(-30, 0), 4)
		FX.dust(global_position + Vector2(30, 0), 4)
		AudioManager.play("jump", 0.0, -2.0)
	# vulcan aim
	var want := 0.0
	if Input.is_action_pressed("up"):
		want = -PI / 4.0 if dir != 0.0 else -PI / 2.0
	elif Input.is_action_pressed("down") and not is_on_floor():
		want = PI / 3.0
	turret_angle = move_toward(turret_angle, want, 7.0 * delta)
	if Input.is_action_pressed("fire") and _mg_cd <= 0.0:
		_mg_cd = 0.07
		var a := turret_angle if facing > 0.0 else PI - turret_angle
		var o := global_position + Vector2(-2 * facing, -37) + Vector2.from_angle(a) * 28.0
		FX.muzzle(o, a, 10.0)
		FX.shell(o - Vector2.from_angle(a) * 20.0, facing)
		AudioManager.play("mg", 0.08)
		Projectile.shoot(Projectile.Kind.TANK_MG, o, Vector2.from_angle(a + randf_range(-0.03, 0.03)) * 640.0, Combat.Team.PLAYER, 1.5)
	if Input.is_action_just_pressed("grenade") and _cannon_cd <= 0.0:
		_cannon_cd = 0.65
		var o := global_position + Vector2(36 * facing, -35)
		var a := 0.0 if facing > 0.0 else PI
		FX.muzzle(o, a, 20.0)
		FX.smoke(o, 5, 4.0, false)
		AudioManager.play("cannon", 0.05)
		CameraShakeManager.shake(0.3)
		_recoil = 4.0
		velocity.x -= facing * 50.0
		Projectile.shoot(Projectile.Kind.TANK_SHELL, o, Vector2(facing * 400.0, -40.0), Combat.Team.PLAYER, 12.0)
	# run over infantry
	if absf(velocity.x) > 50.0 and _crush_cd <= 0.0:
		var r := Rect2(global_position.x + (20.0 if facing > 0 else -48.0), global_position.y - 30.0, 28.0, 30.0)
		for t in Combat.rect_query(r, Combat.Team.PLAYER):
			if t.get("meleeable") == true:
				t.take_hit({"damage": 50.0, "dir": Vector2(facing, -1.0).normalized(), "pos": r.get_center(), "kind": "explosion", "source": self})
				_crush_cd = 0.1


func _animate(delta: float) -> void:
	visual.scale.x = facing
	_tread_t += delta * absf(velocity.x) * 0.25
	_tread.texture = Art.tank_tread(int(_tread_t) % 4)
	_hull.position.x = -_recoil
	_vulcan.rotation = turret_angle
	_vulcan.position.x = -2 - _recoil
	if driver != null and absf(velocity.x) > 30.0 and is_on_floor() and randf() < 0.3:
		FX.dust(global_position + Vector2(-facing * 40.0, 0), 1, 20)


func _on_destroyed() -> void:
	_vulcan.rotation = 0.4
