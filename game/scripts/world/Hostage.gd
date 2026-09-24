class_name Hostage
extends Node2D
## Tied-up prisoner. Shoot the rope (or touch them) to free them; they cheer,
## run to the player, salute and hand over a random reward, then flee.

enum St { TIED, FREED, WALK, GIVE, RUN }

var state: int = St.TIED
var reward := ""            # fixed reward kind, empty = random
var player_only := true     # enemy fire never frees hostages
var facing := 1.0
var _t := 0.0
var _anim := 0.0
var _body: Sprite2D
var _rope: Sprite2D
var _frames: Dictionary
var _gave := false


func _ready() -> void:
	_frames = Art.frames("hostage")
	_body = Sprite2D.new()
	_body.centered = false
	_body.offset = Vector2(-16, -48)
	_body.texture = _frames["tied"]
	add_child(_body)
	_rope = Sprite2D.new()
	_rope.texture = Art.tex("hostage_rope")
	_rope.position = Vector2(-12, -40)
	add_child(_rope)
	z_index = 4
	Combat.register(self, Combat.Team.NEUTRAL)


func _exit_tree() -> void:
	Combat.unregister(self)


func get_hurt_rect() -> Rect2:
	return Rect2(global_position + Vector2(-14, -40), Vector2(26, 40))


func is_hittable() -> bool:
	return state == St.TIED


func take_hit(info: Dictionary) -> int:
	if state != St.TIED:
		return Combat.HIT_NONE
	if info.get("kind", "") == "explosion":
		return Combat.HIT_NONE
	free_hostage()
	return Combat.HIT_OK


func free_hostage() -> void:
	if state != St.TIED:
		return
	state = St.FREED
	_t = 0.0
	Combat.unregister(self)
	_rope.queue_free()
	for i in 6:
		FX.front.spawn(ParticleLayer.SQUARE, global_position + Vector2(-10, -24), Vector2(randf_range(-80, 40), randf_range(-160, -60)), 0.8, 2, Color("c8a060"), 600)
	FX.popup(global_position + Vector2(0, -58), "THANK YOU!", Color("a0f0ff"))
	AudioManager.play("thanks", 0.0)
	GameManager.hostages_rescued += 1
	GameManager.add_score(500, global_position + Vector2(0, -40))


func _physics_process(delta: float) -> void:
	_t += delta
	var p: Player = GameManager.player
	match state:
		St.TIED:
			_anim += delta
			_body.texture = _frames["tied1" if int(_anim * 3.0) % 4 == 0 else "tied"]
			if p != null and p.state == Player.St.NORMAL and p.global_position.distance_to(global_position) < 22.0:
				free_hostage()
		St.FREED:
			_body.texture = _frames["cheer"] if int(_t * 6.0) % 2 == 0 else _frames["walk_idle"]
			_body.position.y = -absf(sin(_t * 10.0)) * 6.0
			if _t > 0.8:
				_body.position.y = 0
				state = St.WALK
				_t = 0.0
		St.WALK:
			var tx := p.get_target_node().global_position.x if p != null else global_position.x
			var d := tx - global_position.x
			facing = signf(d) if absf(d) > 2.0 else facing
			if absf(d) > 30.0 and _t < 2.0:
				_move(facing * 80.0, delta)
			else:
				state = St.GIVE
				_t = 0.0
		St.GIVE:
			_body.texture = _frames["salute"]
			if _t > 0.25 and not _gave:
				_gave = true
				var k := reward if reward != "" else Pickup.random_kind()
				var pk := Pickup.new().setup(k)
				get_parent().add_child(pk)
				pk.global_position = global_position + Vector2(facing * 10.0, -20.0)
				pk.pop(Vector2(facing * 50.0, -200.0))
				AudioManager.play("pickup", 0.0)
			if _t > 0.9:
				state = St.RUN
				_t = 0.0
				facing = -1.0
		St.RUN:
			_move(-170.0, delta)
			if global_position.x < CameraManager.left() - 40.0 or _t > 5.0:
				queue_free()
	_body.flip_h = facing < 0.0
	_body.offset.x = -16.0 if facing > 0.0 else -16.0


func _move(vx: float, delta: float) -> void:
	global_position.x += vx * delta
	_anim += delta * absf(vx) / 12.0
	_body.texture = _frames["walk%d" % (int(_anim) % 6)]
	var gy := Combat.ground_below(global_position + Vector2(0, -20), 60.0)
	if gy != INF:
		global_position.y = gy
