extends Node
## Global game state: score, lives, mode flags, input map and game flow.

enum State { TITLE, PLAYING, PAUSED, GAME_OVER, CLEAR }

signal score_changed(score: int)
signal lives_changed(lives: int)
signal message(text: String, duration: float, big: bool)
signal boss_bar(ratio: float, visible: bool, name: String)
signal go_arrow(visible: bool)
signal state_changed(state: int)

const START_LIVES := 3
const PLAYER_MAX_HP := 100.0

var state: int = State.TITLE
var score := 0
var lives := START_LIVES
var hostages_rescued := 0
var enemies_killed := 0
var continues_used := 0
var play_time := 0.0

## Debug mode: the player has an HP bar instead of dying in one hit.
var debug_hp_mode := false
var god_mode := false
var show_debug := false

var player: Node2D = null
var world: Node2D = null
var level: Node = null


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_setup_input()


func _process(delta: float) -> void:
	if state == State.PLAYING:
		play_time += delta


func reset_run() -> void:
	score = 0
	lives = START_LIVES
	hostages_rescued = 0
	enemies_killed = 0
	continues_used = 0
	play_time = 0.0
	score_changed.emit(score)
	lives_changed.emit(lives)


func set_state(s: int) -> void:
	state = s
	state_changed.emit(s)


func add_score(n: int, at: Vector2 = Vector2.INF) -> void:
	score += n
	score_changed.emit(score)
	if at != Vector2.INF and n >= 100:
		FX.popup(at, str(n), Color("fff27a"))


func add_life() -> void:
	lives += 1
	lives_changed.emit(lives)
	AudioManager.play("oneup", 0.0)


func lose_life() -> bool:
	if god_mode:
		return true
	lives -= 1
	lives_changed.emit(lives)
	return lives > 0


func continue_game() -> void:
	continues_used += 1
	lives = START_LIVES
	score = 0
	lives_changed.emit(lives)
	score_changed.emit(score)


func show_message(text: String, duration: float = 2.0, big: bool = true) -> void:
	message.emit(text, duration, big)


## The thing enemies should aim at: the player or the vehicle they drive.
func get_target() -> Node2D:
	if player == null or not is_instance_valid(player):
		return null
	if player.has_method("get_target_node"):
		return player.get_target_node()
	return player


func target_pos() -> Vector2:
	var t := get_target()
	if t == null:
		return Vector2(-99999, -99999)
	if t.has_method("get_aim_point"):
		return t.get_aim_point()
	return t.global_position + Vector2(0, -20)


func target_alive() -> bool:
	var t := get_target()
	return t != null and t.has_method("is_hittable") and t.is_hittable()


## Cycles the window through whole-number multiples of 640x360
## (1x ... 6x = 3840x2160 / 4K), limited to what fits on the screen.
func _cycle_window_scale() -> void:
	if DisplayServer.window_get_mode() != DisplayServer.WINDOW_MODE_WINDOWED:
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
	var screen := DisplayServer.screen_get_usable_rect().size
	var max_k := clampi(mini(screen.x / 640, screen.y / 360), 1, 6)
	var cur := DisplayServer.window_get_size().x / 640
	var k := cur + 1 if cur < max_k else 1
	var size := Vector2i(640 * k, 360 * k)
	DisplayServer.window_set_size(size)
	DisplayServer.window_set_position(DisplayServer.screen_get_position() + (screen - size) / 2)
	show_message("WINDOW %dx  (%dx%d)" % [k, size.x, size.y], 1.2, false)


# ----------------------------------------------------------------- input map
func _add_action(action: String, keys: Array, mouse: int = 0, joy_buttons: Array = [], joy_axis: Array = []) -> void:
	if not InputMap.has_action(action):
		InputMap.add_action(action, 0.3)
	for k in keys:
		var ev := InputEventKey.new()
		ev.physical_keycode = k
		InputMap.action_add_event(action, ev)
	if mouse != 0:
		var mb := InputEventMouseButton.new()
		mb.button_index = mouse
		InputMap.action_add_event(action, mb)
	for b in joy_buttons:
		var jb := InputEventJoypadButton.new()
		jb.button_index = b
		InputMap.action_add_event(action, jb)
	if joy_axis.size() == 2:
		var ja := InputEventJoypadMotion.new()
		ja.axis = joy_axis[0]
		ja.axis_value = joy_axis[1]
		InputMap.action_add_event(action, ja)


func _setup_input() -> void:
	_add_action("left", [KEY_A, KEY_LEFT], 0, [JOY_BUTTON_DPAD_LEFT], [JOY_AXIS_LEFT_X, -1.0])
	_add_action("right", [KEY_D, KEY_RIGHT], 0, [JOY_BUTTON_DPAD_RIGHT], [JOY_AXIS_LEFT_X, 1.0])
	_add_action("up", [KEY_W, KEY_UP], 0, [JOY_BUTTON_DPAD_UP], [JOY_AXIS_LEFT_Y, -1.0])
	_add_action("down", [KEY_S, KEY_DOWN], 0, [JOY_BUTTON_DPAD_DOWN], [JOY_AXIS_LEFT_Y, 1.0])
	_add_action("jump", [KEY_SPACE], 0, [JOY_BUTTON_A])
	_add_action("fire", [KEY_J], MOUSE_BUTTON_LEFT, [JOY_BUTTON_X])
	_add_action("grenade", [KEY_K], MOUSE_BUTTON_RIGHT, [JOY_BUTTON_B])
	_add_action("interact", [KEY_E], 0, [JOY_BUTTON_Y])
	_add_action("pause", [KEY_ESCAPE, KEY_P], 0, [JOY_BUTTON_START])
	_add_action("start", [KEY_ENTER, KEY_KP_ENTER], 0, [JOY_BUTTON_START])
	_add_action("debug_hp", [KEY_F1])
	_add_action("debug_god", [KEY_F2])
	_add_action("debug_info", [KEY_F3])
	_add_action("fullscreen", [KEY_F11])
	_add_action("window_scale", [KEY_F10])


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("fullscreen"):
		var fs := DisplayServer.window_get_mode() == DisplayServer.WINDOW_MODE_FULLSCREEN
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED if fs else DisplayServer.WINDOW_MODE_FULLSCREEN)
	elif event.is_action_pressed("window_scale"):
		_cycle_window_scale()
	elif event.is_action_pressed("debug_hp"):
		debug_hp_mode = not debug_hp_mode
		show_message("DEBUG HP MODE " + ("ON" if debug_hp_mode else "OFF"), 1.5, false)
		if player != null and is_instance_valid(player):
			player.set("hp", PLAYER_MAX_HP)
	elif event.is_action_pressed("debug_god"):
		god_mode = not god_mode
		show_message("GOD MODE " + ("ON" if god_mode else "OFF"), 1.5, false)
	elif event.is_action_pressed("debug_info"):
		show_debug = not show_debug
