class_name Level
extends Node2D
## Base class for stages: owns the layer stack, camera, player, water, and
## provides builder helpers (ground, platforms, props, triggers) used by
## concrete levels such as Level1.

signal completed
signal game_over

var length := 12000.0
var start_pos := Vector2(80, 280)
var water_rects: Array[Rect2] = []
var zones: Array = []

var bg: Background
var decor_back: Node2D
var terrain: Node2D
var entities: Node2D
var projectiles: Node2D
var decor_front: Node2D
var fx_back: ParticleLayer
var fx_front: ParticleLayer
var camera: Camera2D
var player: Player
var finished := false

const ENEMY_SCRIPTS := {
	"soldier": "res://scripts/enemies/Soldier.gd",
	"charger": "res://scripts/enemies/ChargerSoldier.gd",
	"rocket": "res://scripts/enemies/RocketSoldier.gd",
	"gunner": "res://scripts/enemies/MachineGunner.gd",
	"shield": "res://scripts/enemies/ShieldSoldier.gd",
	"turret": "res://scripts/enemies/Turret.gd",
	"car": "res://scripts/enemies/ArmoredCar.gd",
	"heli": "res://scripts/enemies/Helicopter.gd",
}


func _ready() -> void:
	GameManager.level = self
	GameManager.world = self
	Combat.clear()
	_make_layers()
	ObjectPool.reset(projectiles)
	ObjectPool.register(Projectile.POOL_KEY, func(): return Projectile.new())
	ObjectPool.prewarm(Projectile.POOL_KEY, 160)
	ObjectPool.prewarm(&"explosion", 12)
	ObjectPool.prewarm(&"popup", 8)
	FX.setup(fx_back, fx_front)
	build()
	bg = Background.new().setup(zones, length)
	add_child(bg)
	player = Player.new()
	player.position = start_pos
	entities.add_child(player)
	CameraManager.level_bottom = 360.0
	CameraManager.level_top = -300.0
	CameraManager.setup(camera, player, 0.0, length)
	on_start()


func _exit_tree() -> void:
	if GameManager.level == self:
		GameManager.level = null
	AudioManager.stop_all_loops()


func _make_layers() -> void:
	decor_back = _node("DecorBack", -8)
	terrain = _node("Terrain", -5)
	fx_back = ParticleLayer.new()
	fx_back.name = "FxBack"
	fx_back.z_index = 5
	add_child(fx_back)
	entities = _node("Entities", 0)
	projectiles = _node("Projectiles", 15)
	fx_front = ParticleLayer.new()
	fx_front.name = "FxFront"
	fx_front.z_index = 30
	add_child(fx_front)
	decor_front = _node("DecorFront", 35)
	camera = Camera2D.new()
	camera.name = "Camera"
	add_child(camera)
	camera.make_current()


func _node(n: String, z: int) -> Node2D:
	var node := Node2D.new()
	node.name = n
	node.z_index = z
	add_child(node)
	return node


## Override in concrete levels.
func build() -> void:
	pass


func on_start() -> void:
	pass


# =============================================================== builder helpers
func ground(x0: float, x1: float, top: float, style: String = "sand") -> TerrainBlock:
	var b := TerrainBlock.new().setup(Rect2(x0, top, x1 - x0, 520.0 - top), style)
	terrain.add_child(b)
	return b


func block(x: float, y: float, w: float, h: float, style: String = "concrete") -> TerrainBlock:
	var b := TerrainBlock.new().setup(Rect2(x, y, w, h), style)
	terrain.add_child(b)
	return b


func platform(x: float, y: float, w: float, style: String = "wood") -> TerrainBlock:
	var b := TerrainBlock.new().setup(Rect2(x, y, w, 8), style, true)
	terrain.add_child(b)
	return b


func water(x0: float, x1: float, y: float) -> void:
	var r := Rect2(x0, y, x1 - x0, 520.0 - y)
	water_rects.append(r)
	decor_front.add_child(WaterZone.new().setup(r))


func decor(tex_key: String, pos: Vector2, front: bool = false, flip: bool = false, tint: Color = Color.WHITE) -> Sprite2D:
	var s := Sprite2D.new()
	s.texture = Art.tex(tex_key)
	s.centered = false
	s.offset = Vector2(-s.texture.get_width() * 0.5, -s.texture.get_height())
	s.position = pos
	s.flip_h = flip
	s.modulate = tint
	(decor_front if front else decor_back).add_child(s)
	return s


func add_entity(n: Node2D, pos: Vector2) -> Node2D:
	n.position = pos
	entities.add_child(n)
	return n


func spawn_enemy(type: String, pos: Vector2, params: Dictionary = {}) -> Node2D:
	var e: Node2D = load(ENEMY_SCRIPTS[type]).new()
	for k in params:
		e.set(k, params[k])
	e.position = pos
	entities.add_child(e)
	return e


func trigger(x: float, actions: Array, opts: Dictionary = {}) -> SpawnTrigger:
	var t := SpawnTrigger.new()
	t.trigger_x = x
	t.actions = actions
	for k in opts:
		t.set(k, opts[k])
	add_child(t)
	return t


# =============================================================== queries
func is_water_at(p: Vector2) -> bool:
	for r in water_rects:
		if r.grow_individual(0, -10, 0, 0).has_point(p):
			return true
	return false


## Highest walkable surface under x (ignores one-way when `solid_only`).
func ground_y(x: float, from_y: float = -400.0) -> float:
	var y := Combat.ground_below(Vector2(x, from_y), 1000.0)
	return y if y != INF else 300.0


func safe_spawn_point(x: float) -> Vector2:
	var tries := 0
	while tries < 20:
		var gx := x + tries * 24.0
		var gy := Combat.ground_below(Vector2(gx, CameraManager.top() - 40.0), 900.0)
		if gy != INF and not is_water_at(Vector2(gx, gy + 12.0)):
			return Vector2(gx, gy)
		tries += 1
	return Vector2(x, 200)


func on_game_over() -> void:
	game_over.emit()


func complete() -> void:
	if finished:
		return
	finished = true
	completed.emit()
