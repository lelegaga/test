extends Node
## Entry point: title screen -> level -> continue / results -> title.

const LEVEL_SCRIPT := "res://scripts/levels/Level1.gd"

var level: Level
var hud: HUD
var ui: CanvasLayer
var _continue_left := 0.0


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	ui = CanvasLayer.new()
	ui.layer = 20
	add_child(ui)
	hud = HUD.new()
	ui.add_child(hud)
	_show_title()
	var args := OS.get_cmdline_user_args()
	if "--autotest" in args or "--screenshots" in args:
		var at: Node = load("res://scripts/core/AutoTest.gd").new()
		add_child(at)


func _load_level(frozen: bool) -> void:
	if level != null:
		level.queue_free()
		remove_child(level)
		level = null
	FX.front = null
	FX.back = null
	level = load(LEVEL_SCRIPT).new()
	level.name = "Level"
	if frozen:
		level.process_mode = Node.PROCESS_MODE_DISABLED
	else:
		level.process_mode = Node.PROCESS_MODE_PAUSABLE
	add_child(level)
	level.completed.connect(_on_level_completed)
	level.game_over.connect(_on_game_over)


func _show_title() -> void:
	get_tree().paused = false
	Engine.time_scale = 1.0
	GameManager.set_state(GameManager.State.TITLE)
	_load_level(true)
	AudioManager.stop_music()


func start_game() -> void:
	GameManager.reset_run()
	_load_level(false)
	GameManager.set_state(GameManager.State.PLAYING)


func _on_level_completed() -> void:
	GameManager.set_state(GameManager.State.CLEAR)
	AudioManager.stop_music()
	AudioManager.play("clear", 0.0)


func _on_game_over() -> void:
	GameManager.set_state(GameManager.State.GAME_OVER)
	_continue_left = 10.0
	get_tree().paused = true


func _process(delta: float) -> void:
	match GameManager.state:
		GameManager.State.TITLE:
			if Input.is_action_just_pressed("start") or Input.is_action_just_pressed("fire"):
				start_game()
		GameManager.State.PLAYING:
			if Input.is_action_just_pressed("pause"):
				get_tree().paused = true
				GameManager.set_state(GameManager.State.PAUSED)
		GameManager.State.PAUSED:
			if Input.is_action_just_pressed("pause"):
				get_tree().paused = false
				GameManager.set_state(GameManager.State.PLAYING)
			elif Input.is_physical_key_pressed(KEY_Q):
				_show_title()
		GameManager.State.GAME_OVER:
			_continue_left -= delta
			hud.continue_t = _continue_left
			if Input.is_action_just_pressed("start") or Input.is_action_just_pressed("fire"):
				GameManager.continue_game()
				get_tree().paused = false
				GameManager.set_state(GameManager.State.PLAYING)
				level.player.respawn()
			elif _continue_left <= 0.0:
				_show_title()
		GameManager.State.CLEAR:
			if Input.is_action_just_pressed("start"):
				_show_title()
