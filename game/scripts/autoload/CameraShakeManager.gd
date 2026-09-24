extends Node
## Trauma based screen shake plus hit-stop.
##
## Callers add trauma (0..1); the offset is trauma^2 scaled noise so small hits
## give a light jiggle and big explosions really rattle the screen.

const MAX_OFFSET := Vector2(10, 8)
const DECAY := 1.6

var trauma := 0.0
var offset := Vector2.ZERO
var _t := 0.0
var _noise := FastNoiseLite.new()
var _hitstop_left := 0.0


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_noise.seed = 1337
	_noise.frequency = 0.9


func shake(amount: float) -> void:
	trauma = clampf(trauma + amount, 0.0, 1.0)


func reset() -> void:
	trauma = 0.0
	offset = Vector2.ZERO


## Freezes gameplay for a few frames to sell heavy impacts.
func hit_stop(duration: float) -> void:
	if get_tree().paused:
		return
	_hitstop_left = maxf(_hitstop_left, duration)
	Engine.time_scale = 0.05


func _process(delta: float) -> void:
	# Real (unscaled) delta for hit-stop bookkeeping.
	var real_delta := delta / maxf(Engine.time_scale, 0.001)
	if _hitstop_left > 0.0:
		_hitstop_left -= real_delta
		if _hitstop_left <= 0.0:
			Engine.time_scale = 1.0
	if get_tree().paused:
		return
	_t += real_delta * 60.0
	trauma = maxf(trauma - DECAY * real_delta, 0.0)
	var p := trauma * trauma
	offset = Vector2(MAX_OFFSET.x * p * _noise.get_noise_2d(_t, 0.0), MAX_OFFSET.y * p * _noise.get_noise_2d(0.0, _t))
	offset = offset.round()
