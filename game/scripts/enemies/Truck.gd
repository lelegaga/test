class_name Truck
extends EnemyBase
## Troop transport: rolls in, drops its tailgate and unloads a squad that
## leaps out one by one, then drives off. Blow it up early to stop the drop.

var troops := 4
var troop_type := "soldier"
var _hull: Sprite2D
var _unload_t := 0.0
var _stop_x := 0.0


func _init() -> void:
	hp = 30.0
	max_hp = 30.0
	score_value = 1000
	humanoid = false
	meleeable = false
	knock_resist = 1.0
	speed = 150.0
	hurt_size = Vector2(100, 40)
	hurt_offset = Vector2(0, -22)
	gun_id = ""


func _build_visual() -> void:
	_hull = Sprite2D.new()
	_hull.texture = Art.tex("truck")
	_hull.centered = false
	_hull.offset = Vector2(-56, -54)
	_hull.use_parent_material = true
	_hull.flip_h = true
	visual.add_child(_hull)


func _on_ready() -> void:
	set_state("drive_in")
	_stop_x = CameraManager.right() - 150.0


func _think(delta: float) -> void:
	match state:
		"drive_in":
			walk(-1.0, speed, delta)
			facing = 1.0
			if global_position.x <= _stop_x:
				set_state("unload")
				AudioManager.play("land", 0.0)
		"unload":
			stop(delta)
			_unload_t -= delta
			if _unload_t <= 0.0 and troops > 0 and state_t > 0.4:
				troops -= 1
				_unload_t = 0.35
				var lvl: Level = GameManager.level
				var e := lvl.spawn_enemy(troop_type, global_position + Vector2(46, -30), {"enter_mode": "jump", "facing": -1.0})
				e.velocity = Vector2(-randf_range(60, 120), -300.0)
				_register_spawn(e)
			if troops <= 0 and state_t > 2.0:
				set_state("drive_out")
		"drive_out":
			walk(1.0, speed * 1.2, delta)
			facing = 1.0
			if global_position.x > CameraManager.right() + 90.0:
				alive = false
				queue_free()
	if randf() < 0.2:
		FX.smoke(global_position + Vector2(58, -8), 1, 2.0, false)


## Troops count as part of the trigger that spawned the truck, so locked
## encounters wait for them too.
func _register_spawn(e: Node) -> void:
	for t in get_tree().get_nodes_in_group("triggers"):
		if self in t.spawned:
			t.spawned.append(e)


func _animate(_delta: float) -> void:
	visual.scale.x = 1.0


func _on_death(_info: Dictionary) -> void:
	FX.boom(global_position + Vector2(0, -28), 56.0, 8.0, Combat.Team.NEUTRAL, self, 0.6)
	FX.chain(global_position + Vector2(0, -28), Vector2(40, 14), 3, 0.12, 26.0)
	FX.debris(global_position + Vector2(0, -30), 14, Color("5e6b3a"), 300, 5)
	visual.modulate = Color(0.3, 0.27, 0.25)
	troops = 0


func _update_dead(delta: float) -> void:
	_dead_t += delta
	velocity.y = minf(velocity.y + gravity * delta, 600.0)
	velocity.x = move_toward(velocity.x, 0.0, 300.0 * delta)
	move_and_slide()
	if randf() < 0.25:
		FX.smoke(global_position + Vector2(randf_range(-40, 40), -40), 1, 5.0)
	if _dead_t > 7.0:
		visual.visible = int(_dead_t * 12.0) % 2 == 0
	if _dead_t > 8.0 or global_position.x < CameraManager.left() - 150.0:
		queue_free()
