class_name Turret
extends EnemyBase
## Fixed gun emplacement. The barrel tracks the player at a limited turn rate
## and fires 3-round bursts once lined up. Explodes when destroyed.

var turn_rate := 1.6
var gun_angle := PI          # world angle
var _cd := 1.0
var _burst := 0
var _shot_t := 0.0
var _gun_sprite: Sprite2D
var ceiling := false


func _init() -> void:
	hp = 22.0
	max_hp = 22.0
	score_value = 500
	humanoid = false
	flying = true
	meleeable = false
	knock_resist = 1.0
	enter_mode = "none"
	hurt_size = Vector2(28, 22)
	hurt_offset = Vector2(0, -11)
	gun_id = ""


func _build_visual() -> void:
	var base := Sprite2D.new()
	base.texture = Art.tex("turret_base")
	base.centered = false
	base.offset = Vector2(-16, -22)
	base.use_parent_material = true
	visual.add_child(base)
	_gun_sprite = Sprite2D.new()
	_gun_sprite.texture = Art.tex("turret_gun")
	_gun_sprite.centered = false
	_gun_sprite.offset = Vector2(-4, -4)
	_gun_sprite.position = Vector2(0, -14)
	_gun_sprite.use_parent_material = true
	visual.add_child(_gun_sprite)
	if ceiling:
		visual.scale.y = -1


func _on_ready() -> void:
	set_state("track")


func _think(delta: float) -> void:
	velocity = Vector2.ZERO
	var pivot := global_position + Vector2(0, -14 if not ceiling else 14)
	var want := (target_pos() - pivot).angle()
	if not ceiling:
		# ground mounted: keep the barrel above the horizon
		if want > 0.0:
			want = 0.0 if want < PI * 0.5 else PI
	gun_angle = rotate_toward(gun_angle, want, turn_rate * delta)
	_cd -= delta
	_shot_t -= delta
	if _burst > 0 and _shot_t <= 0.0:
		_burst -= 1
		_shot_t = 0.14
		var o := pivot + Vector2.from_angle(gun_angle) * 24.0
		FX.muzzle(o, gun_angle, 8.0)
		AudioManager.play("enemy_shot", 0.1)
		Projectile.shoot(Projectile.Kind.ENEMY_BULLET, o, Vector2.from_angle(gun_angle) * 200.0, Combat.Team.ENEMY, 1.0)
	elif _cd <= 0.0 and on_screen() and absf(angle_difference(gun_angle, want)) < 0.15 and GameManager.target_alive():
		_burst = 3
		_cd = 1.7


func _animate(_delta: float) -> void:
	_gun_sprite.rotation = gun_angle if not ceiling else -gun_angle


func _on_death(_info: Dictionary) -> void:
	FX.boom(global_position + Vector2(0, -12), 40.0, 6.0, Combat.Team.NEUTRAL, self)
	FX.debris(global_position + Vector2(0, -12), 10, Color("5a5e64"), 260, 4)
	_gun_sprite.visible = false
	visual.modulate = Color(0.35, 0.3, 0.3)
	_dead_t = 0.0


func _update_dead(delta: float) -> void:
	_dead_t += delta
	if int(_dead_t * 8.0) % 4 == 0:
		FX.smoke(global_position + Vector2(0, -16), 1, 3.0)
