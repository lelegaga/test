class_name BossBase
extends Node2D
## Generic multi-phase boss.
##
## Provides: hit parts (with damage multipliers and a weak point), HP bar
## updates, phase changes at HP thresholds, intro/active gating, flash
## feedback and a long cinematic death (staggered part explosions, screen
## shake, then one huge final blast). Subclasses implement `_build()`,
## `_update_boss()` and `_on_phase()`.

signal defeated

var boss_name := "BOSS"
var hp := 400.0
var max_hp := 400.0
var phase := 1
var phase_thresholds: Array = [0.66, 0.33]   # ratios that start phase 2, 3...
var active := false
var dying := false
var alive := true
var facing := -1.0
var score_value := 20000
var body_extent := Vector2(110, 60)     # used for death explosions

var parts: Array = []
var weak_part: BossPart
var visual: Node2D
var _mat: ShaderMaterial
var _flash := 0.0
var state := "intro"
var state_t := 0.0
var _death_t := 0.0
var _final_done := false


func _ready() -> void:
	add_to_group("bosses")
	visual = Node2D.new()
	_mat = FX.make_flash_material()
	visual.material = _mat
	add_child(visual)
	z_index = 4
	_build()


func _build() -> void:
	pass


func add_part(rect: Rect2, mult: float, weak: bool = false) -> BossPart:
	var p := BossPart.new()
	p.boss = self
	p.local_rect = rect
	p.mult = mult
	p.weak = weak
	add_child(p)
	parts.append(p)
	if weak:
		weak_part = p
	return p


func activate() -> void:
	active = true
	GameManager.boss_bar.emit(hp / max_hp, true, boss_name)


func set_state(s: String) -> void:
	state = s
	state_t = 0.0


## Override for weak-point gating (e.g. the core only opens after attacks).
func weak_open() -> bool:
	return true


func on_part_hit(part: BossPart, info: Dictionary) -> int:
	if not active or dying:
		# armored during the intro
		FX.spark(info.get("pos", global_position), -Vector2(info.get("dir", Vector2.RIGHT)), 2, Color.WHITE)
		return Combat.HIT_BLOCKED if info.get("kind", "") == "bullet" else Combat.HIT_OK
	var weak := part.weak
	# bullets that strike the hull right on the weak point count as weak hits
	if not weak and weak_part != null and weak_part.get_hurt_rect().grow(4).has_point(info.get("pos", Vector2.INF)):
		weak = true
	var mult := part.mult
	if weak:
		mult = weak_part.mult if weak_open() else 0.35
	var dmg: float = info.get("damage", 1.0) * mult
	hp -= dmg
	_flash = 1.0 if weak else 0.45
	if weak and weak_open():
		FX.spark(info.get("pos", global_position), -Vector2(info.get("dir", Vector2.RIGHT)), 4, Color("80ffff"))
		AudioManager.play("hit", 0.1, -2.0)
	elif info.get("kind", "") == "bullet":
		FX.spark(info.get("pos", global_position), -Vector2(info.get("dir", Vector2.RIGHT)), 2, Color.WHITE)
		AudioManager.play("tink", 0.2, -10.0)
	GameManager.boss_bar.emit(maxf(hp, 0.0) / max_hp, true, boss_name)
	var ratio := hp / max_hp
	while phase - 1 < phase_thresholds.size() and ratio <= float(phase_thresholds[phase - 1]):
		phase += 1
		_change_phase()
	if hp <= 0.0:
		_die()
	return Combat.HIT_OK


func _change_phase() -> void:
	CameraShakeManager.shake(0.6)
	CameraShakeManager.hit_stop(0.12)
	FX.screen_flash.emit(0.35)
	AudioManager.play("roar", 0.0)
	_on_phase(phase)


func _on_phase(_n: int) -> void:
	pass


func _physics_process(delta: float) -> void:
	_flash = maxf(_flash - delta * 6.0, 0.0)
	_mat.set_shader_parameter("flash", _flash)
	state_t += delta
	if dying:
		_update_death(delta)
		return
	_update_boss(delta)


func _update_boss(_delta: float) -> void:
	pass


# =============================================================== death
func _die() -> void:
	if dying:
		return
	dying = true
	alive = false
	for p in parts:
		Combat.unregister(p)
	GameManager.boss_bar.emit(0.0, true, boss_name)
	GameManager.add_score(score_value, global_position + Vector2(0, -120))
	GameManager.enemies_killed += 1
	AudioManager.set_loop("engine", false)
	CameraShakeManager.hit_stop(0.2)
	FX.screen_flash.emit(0.6)
	_on_death_start()


func _on_death_start() -> void:
	pass


func _update_death(delta: float) -> void:
	_death_t += delta
	var center := global_position + Vector2(0, -body_extent.y)
	if _death_t < 3.0:
		# staggered part explosions all over the body
		if randf() < delta * 12.0:
			var p := center + Vector2(randf_range(-body_extent.x, body_extent.x), randf_range(-body_extent.y, body_extent.y) * 0.8)
			FX.boom(p, randf_range(26, 44), 0.0, Combat.Team.NEUTRAL, self, 0.35)
			FX.debris(p, 6, Color("5a5e64"), 280, 4)
		_flash = 0.6 if int(_death_t * 14.0) % 2 == 0 else 0.0
		visual.position = Vector2(randf_range(-2, 2), randf_range(-1, 1))
	elif not _final_done:
		_final_done = true
		visual.position = Vector2.ZERO
		FX.screen_flash.emit(1.0)
		CameraShakeManager.shake(1.0)
		CameraShakeManager.hit_stop(0.25)
		FX.explosion(center, 130.0, true)
		FX.chain(center, body_extent, 10, 0.07, 50.0)
		FX.debris(center, 50, Color("4a4f58"), 520, 6)
		FX.debris(center, 20, Color("ffb62e"), 420, 3)
		FX.ground_flames(global_position, 14, body_extent.x)
		AudioManager.play("explode_big", 0.0)
		_on_final_explosion()
	elif _death_t > 5.5:
		set_physics_process(false)
		GameManager.boss_bar.emit(0.0, false, boss_name)
		defeated.emit()
	if _final_done and randf() < 0.3:
		FX.smoke(center + Vector2(randf_range(-body_extent.x, body_extent.x), 0), 1, 7.0)


func _on_final_explosion() -> void:
	pass


## Kills the player on touch (e.g. ramming).
func contact_damage(r: Rect2) -> void:
	for t in Combat.rect_query(r, Combat.Team.ENEMY):
		t.take_hit({"damage": 5.0, "dir": Vector2(facing, -0.5).normalized(), "pos": r.get_center(), "kind": "melee", "source": self})
