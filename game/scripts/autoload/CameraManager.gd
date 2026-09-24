extends Node
## Side-scroller camera: follows the player (or their vehicle), never scrolls
## back left, can be locked to arenas for encounters and the boss fight.

const VIEW := Vector2(640, 360)
const LOOK_AHEAD := 56.0

var camera: Camera2D
var target: Node2D
var level_min_x := 0.0
var level_max_x := 100000.0
var level_top := -400.0
var level_bottom := 360.0

var locked := false
var _lock_center := Vector2.ZERO
var _min_center_x := 0.0      # the camera never scrolls left of this
var _look := 0.0
var _center := Vector2.ZERO


func setup(cam: Camera2D, follow: Node2D, min_x: float, max_x: float) -> void:
	camera = cam
	target = follow
	level_min_x = min_x
	level_max_x = max_x
	locked = false
	_look = LOOK_AHEAD
	_center = Vector2(maxf(follow.global_position.x, min_x + VIEW.x * 0.5), level_bottom - VIEW.y * 0.5)
	_min_center_x = _center.x
	camera.global_position = _center
	CameraShakeManager.reset()


func set_target(t: Node2D) -> void:
	target = t


func lock_at(center_x: float) -> void:
	locked = true
	_lock_center = Vector2(center_x, level_bottom - VIEW.y * 0.5)


func unlock() -> void:
	locked = false
	_min_center_x = _center.x


## Left edge of the visible area in world coordinates.
func left() -> float:
	return _center.x - VIEW.x * 0.5


func right() -> float:
	return _center.x + VIEW.x * 0.5


func top() -> float:
	return _center.y - VIEW.y * 0.5


func bottom() -> float:
	return _center.y + VIEW.y * 0.5


func view_rect() -> Rect2:
	return Rect2(_center - VIEW * 0.5, VIEW)


func center() -> Vector2:
	return _center


func is_visible_point(p: Vector2, margin: float = 0.0) -> bool:
	return view_rect().grow(margin).has_point(p)


func _physics_process(delta: float) -> void:
	if camera == null or not is_instance_valid(camera):
		return
	if locked:
		_center = _center.lerp(_lock_center, 1.0 - exp(-4.0 * delta))
	elif target != null and is_instance_valid(target):
		var facing: float = target.get("facing") if target.get("facing") != null else 1.0
		_look = move_toward(_look, LOOK_AHEAD * signf(facing) * 0.6 + LOOK_AHEAD * 0.4, 160.0 * delta)
		var want_x := target.global_position.x + _look
		want_x = maxf(want_x, _min_center_x)
		var x := lerpf(_center.x, want_x, 1.0 - exp(-8.0 * delta))
		x = maxf(x, _min_center_x)
		_min_center_x = x
		# Vertical: rest on the level floor, rise when the player climbs high.
		var want_y := minf(level_bottom - VIEW.y * 0.5, target.global_position.y - 30.0)
		want_y = maxf(want_y, level_top + VIEW.y * 0.5)
		var y := lerpf(_center.y, want_y, 1.0 - exp(-4.0 * delta))
		_center = Vector2(x, y)
	_center.x = clampf(_center.x, level_min_x + VIEW.x * 0.5, level_max_x - VIEW.x * 0.5)
	camera.global_position = _center.round()
	camera.offset = CameraShakeManager.offset
