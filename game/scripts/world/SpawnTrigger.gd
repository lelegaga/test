class_name SpawnTrigger
extends Node
## Scripted encounter.
##
## When the player (or their vehicle) crosses `trigger_x`, the `actions` list
## runs in sequence. Each action is a Dictionary:
##   {"spawn": "soldier", "at": "right"|"left"|"top"|"abs"|"sky", "dx": 0,
##    "x": .., "y": .., "count": 1, "gap": 0.3, "space": 26,
##    "enter": "walk"|"drop"|"jump"|"none", "params": {...}}
##   {"wait": seconds}
##   {"wait_clear": true}          wait until everything spawned so far is dead
##   {"message": "TEXT", "dur": 2.0}
##   {"shake": 0.5}
##   {"sfx": "warning"}  {"music": "boss"}
##   {"call": Callable}
## With `lock_camera` the screen locks until all spawned enemies are dead,
## then a GO arrow tells the player to push on.

signal finished

var trigger_x := 0.0
var actions: Array = []
var lock_camera := false
var lock_x := -1.0
var fired := false
var done := false
var spawned: Array = []


func _ready() -> void:
	add_to_group("triggers")


func _physics_process(_delta: float) -> void:
	if fired:
		return
	var t := GameManager.get_target()
	if t == null or t.global_position.x < trigger_x:
		return
	fired = true
	_run()


func _run() -> void:
	if lock_camera:
		CameraManager.lock_at(lock_x if lock_x > 0.0 else CameraManager.center().x)
		GameManager.go_arrow.emit(false)
	for a in actions:
		if not is_inside_tree():
			return
		await _do(a)
	if lock_camera:
		await _wait_clear()
		if not is_inside_tree():
			return
		CameraManager.unlock()
		GameManager.go_arrow.emit(true)
	done = true
	finished.emit()


func _wait(t: float) -> void:
	await get_tree().create_timer(t, false).timeout


func alive_count() -> int:
	var n := 0
	for e in spawned:
		if is_instance_valid(e) and e.get("alive") == true:
			n += 1
	return n


func _wait_clear() -> void:
	while is_inside_tree() and alive_count() > 0:
		await _wait(0.25)


func _do(a: Dictionary) -> void:
	if a.has("spawn"):
		var count: int = a.get("count", 1)
		for i in count:
			_spawn_one(a, i)
			var gap: float = a.get("gap", 0.0)
			if gap > 0.0 and i < count - 1:
				await _wait(gap)
	elif a.has("wait"):
		await _wait(a.wait)
	elif a.has("wait_clear"):
		await _wait_clear()
	elif a.has("message"):
		GameManager.show_message(a.message, a.get("dur", 2.0), a.get("big", true))
	elif a.has("shake"):
		CameraShakeManager.shake(a.shake)
	elif a.has("sfx"):
		AudioManager.play(a.sfx, 0.0)
	elif a.has("music"):
		AudioManager.play_music(a.music)
	elif a.has("call"):
		(a.call as Callable).call()


func _spawn_one(a: Dictionary, i: int) -> void:
	var level: Level = GameManager.level
	if level == null:
		return
	var at: String = a.get("at", "right")
	var dx: float = a.get("dx", 0.0) + i * float(a.get("space", 26.0))
	var pos := Vector2.ZERO
	var enter: String = a.get("enter", "walk")
	var facing := -1.0
	match at:
		"right":
			pos.x = CameraManager.right() + 24.0 + dx
		"left":
			pos.x = CameraManager.left() - 24.0 - dx
			facing = 1.0
		"top":
			pos.x = CameraManager.left() + float(a.get("fx", 0.6)) * CameraManager.VIEW.x + dx
			enter = "drop"
		"sky":
			pos = Vector2(CameraManager.right() + 70.0 + dx, CameraManager.top() + float(a.get("y", 70.0)))
		_:
			pos.x = float(a.get("x", 0.0)) + dx
	if at == "top":
		pos.y = CameraManager.top() - 40.0 - i * 20.0
	elif at != "sky":
		if a.has("y"):
			pos.y = a.y
		else:
			pos.y = level.ground_y(pos.x, CameraManager.top() - 20.0 if not a.has("from_y") else float(a.from_y))
	var params: Dictionary = a.get("params", {}).duplicate()
	params["enter_mode"] = enter
	if not params.has("facing"):
		params["facing"] = facing
	var e := level.spawn_enemy(a.spawn, pos, params)
	spawned.append(e)
