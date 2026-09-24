class_name Level1
extends Level
## MISSION 1 - "Coastal Assault": tropical coast military base.
##
##   Beach landing -> enemy outpost -> jungle (secret canopy) -> wooden bridge
##   ambush -> military base (tank pickup) -> airfield tank battle -> boss.
##
## Enemies are never all pre-spawned: every group is created by a SpawnTrigger
## when the player reaches it, most of them staged as small scripted events.

const BOSS_ARENA_CENTER := 10560.0

var _grounds: Array = []     # [x0, x1, top]
var bridge: Bridge
var boss: MechTankBoss
var _gate_wall: DestructibleObject


func build() -> void:
	length = 10880.0
	start_pos = Vector2(120, 296)
	zones = [[0, "palms"], [3150, "jungle"], [6250, "base"]]
	_build_beach()
	_build_outpost()
	_build_jungle()
	_build_bridge()
	_build_base()
	_build_airfield()
	_build_boss_arena()


func on_start() -> void:
	AudioManager.play_music("stage")
	GameManager.show_message("MISSION 1  START!", 2.5)


# ------------------------------------------------------------------ helpers
func g(x0: float, x1: float, top: float, style: String) -> void:
	ground(x0, x1, top, style)
	_grounds.append([x0, x1, top])


func top_at(x: float) -> float:
	for e in _grounds:
		if x >= e[0] and x < e[1]:
			return e[2]
	return 300.0


func prop(kind: String, x: float, drop: String = "", y: float = INF) -> DestructibleObject:
	var d := DestructibleObject.new().setup(kind, drop)
	add_entity(d, Vector2(x, top_at(x) if y == INF else y))
	return d


func hostage(x: float, reward: String = "", y: float = INF) -> Hostage:
	var h := Hostage.new()
	h.reward = reward
	add_entity(h, Vector2(x, top_at(x) if y == INF else y))
	return h


func item(kind: String, x: float, y: float = INF) -> Pickup:
	var p := Pickup.new().setup(kind)
	add_entity(p, Vector2(x, top_at(x) if y == INF else y))
	p.landed = true
	return p


func deco(key: String, x: float, front: bool = false, y: float = INF, flip: bool = false) -> Sprite2D:
	return decor(key, Vector2(x, top_at(x) if y == INF else y), front, flip)


# ------------------------------------------------------------------ A. beach
func _build_beach() -> void:
	water(-200, 60, 318)
	deco("boat", 40, false, 322)
	g(60, 700, 300, "sand")
	g(700, 980, 280, "sand")
	g(980, 1520, 300, "sand")
	for x in [180, 470, 830, 1180, 1420]:
		deco("palm", x, false, INF, x % 2 == 0)
	deco("rock", 300)
	deco("rock", 1100)
	deco("bush", 640, true)
	prop("crate", 360)
	prop("crate", 384)
	prop("crate", 372, "", 276)
	prop("supply", 520, "hmg")
	prop("barrel", 760)
	prop("barrel", 780)
	prop("barrel", 770, "", 256)
	hostage(930, "bombs")
	prop("sandbags", 1120)
	deco("flag", 1300)
	# first contact: a few soldiers stroll in
	trigger(240, [
		{"spawn": "soldier", "count": 2, "gap": 0.6},
		{"wait": 2.0},
		{"spawn": "soldier", "count": 1},
	])
	# more troops gather right next to the oil barrels
	trigger(560, [
		{"spawn": "soldier", "count": 3, "gap": 0.35, "space": 30},
		{"spawn": "soldier", "at": "top", "fx": 0.75},
		{"wait": 1.5},
		{"spawn": "charger", "at": "left"},
	])
	# dug-in machine gun nest behind the dune
	trigger(1000, [
		{"spawn": "gunner", "at": "abs", "x": 1390, "enter": "none"},
		{"spawn": "soldier", "count": 2, "gap": 0.5},
		{"wait": 1.2},
		{"spawn": "shield", "dx": 20},
		{"spawn": "soldier", "at": "top", "fx": 0.5},
	])


# ------------------------------------------------------------------ B. outpost
func _build_outpost() -> void:
	g(1520, 3200, 300, "sand")
	block(1880, 252, 170, 48, "concrete")
	_grounds.push_front([1880, 2050, 252])
	block(2760, 252, 110, 48, "concrete")
	_grounds.push_front([2760, 2870, 252])
	deco("bunker", 1965, false, 252)
	deco("flag", 1700)
	deco("palm", 1600)
	deco("palm", 2600, false, INF, true)
	prop("tower", 2160)
	prop("hut", 2480)
	prop("barrel", 2290)
	prop("barrel", 2308)
	prop("barrel", 2326)
	prop("barrel", 2308, "", 276)
	hostage(2400, "")
	prop("supply", 2560, "shotgun")
	prop("crate", 2600)
	prop("hedgehog", 2690)
	prop("barricade", 2720)
	deco("flag", 3000)
	item("fruit", 1960, 252)

	trigger(1620, [
		{"spawn": "soldier", "count": 3, "gap": 0.4},
		{"spawn": "rocket", "at": "abs", "x": 1990, "y": 252, "enter": "none"},
	])
	trigger(1950, [
		{"spawn": "soldier", "at": "abs", "x": 2146, "y": 220, "enter": "none", "params": {"patrol_range": 8.0, "grenadier": false}},
		{"spawn": "soldier", "at": "abs", "x": 2174, "y": 220, "enter": "none", "params": {"patrol_range": 8.0, "grenadier": false}},
		{"spawn": "soldier", "count": 2, "gap": 0.8},
	])
	# the barrel trap: a squad forms up right beside the fuel drums
	trigger(2150, [
		{"spawn": "soldier", "count": 4, "gap": 0.3, "space": 22},
		{"spawn": "shield", "dx": 60},
		{"wait": 2.0},
		{"spawn": "soldier", "at": "top", "fx": 0.4},
		{"spawn": "soldier", "at": "top", "fx": 0.8},
	])
	# first gunship: screen locks until it's down
	trigger(2560, [
		{"message": "ENEMY GUNSHIP!", "dur": 1.5, "big": false},
		{"spawn": "heli", "at": "sky", "y": 70},
		{"wait": 2.0},
		{"spawn": "soldier", "count": 2, "gap": 0.6},
		{"spawn": "turret", "at": "abs", "x": 2815, "y": 252, "enter": "none"},
	], {"lock_camera": true, "lock_x": 2600})
	trigger(2950, [
		{"spawn": "charger", "count": 2, "gap": 0.5},
		{"spawn": "soldier", "at": "left", "count": 2, "gap": 0.6},
	])


# ------------------------------------------------------------------ C. jungle
func _build_jungle() -> void:
	g(3200, 3600, 300, "jungle")
	g(3600, 3900, 268, "jungle")
	g(3900, 4170, 300, "jungle")
	water(4170, 4230, 318)
	g(4230, 4720, 290, "jungle")
	g(4720, 5200, 300, "jungle")
	for x in [3260, 3520, 3800, 4080, 4380, 4640, 4900, 5120]:
		deco("jungle_tree", x, false, INF, x % 3 == 0)
	for x in [3350, 3700, 4000, 4450, 4800, 5050]:
		deco("bush", x, true)
	# climbable branches leading up into a hidden canopy nook
	platform(3420, 236, 90, "branch")
	platform(3540, 196, 80, "branch")
	platform(3660, 158, 90, "branch")
	platform(3790, 122, 180, "branch")
	add_child(SecretArea.new().setup(Rect2(3780, 40, 200, 96)))
	hostage(3840, "life", 122)
	prop("supply", 3900, "flame", 122)
	item("medal", 3950, 122)
	# jungle sniper perch
	platform(4380, 206, 110, "branch")
	platform(4560, 236, 80, "branch")
	prop("crate", 3980)
	prop("barrel", 4300)
	prop("barrel", 4318)
	hostage(4600, "")
	prop("supply", 5000, "random")

	# ambush from the undergrowth
	trigger(3330, [
		{"spawn": "charger", "enter": "jump", "count": 2, "gap": 0.4},
		{"spawn": "soldier", "at": "left"},
		{"wait": 1.5},
		{"spawn": "soldier", "count": 2, "gap": 0.5},
	])
	trigger(3720, [
		{"spawn": "rocket", "at": "abs", "x": 4430, "y": 206, "enter": "none"},
		{"spawn": "soldier", "count": 2, "gap": 0.6},
		{"spawn": "charger", "enter": "jump"},
	])
	trigger(4180, [
		{"spawn": "soldier", "at": "top", "fx": 0.55},
		{"spawn": "soldier", "at": "top", "fx": 0.7},
		{"spawn": "soldier", "at": "top", "fx": 0.85},
		{"spawn": "rocket", "at": "abs", "x": 4590, "y": 236, "enter": "none"},
	])
	# jungle clearing: hold the line
	trigger(4700, [
		{"message": "HOLD THE LINE!", "dur": 1.5, "big": false},
		{"spawn": "charger", "enter": "jump", "count": 2, "gap": 0.3},
		{"spawn": "soldier", "count": 3, "gap": 0.4},
		{"wait": 2.5},
		{"spawn": "charger", "at": "left", "count": 2, "gap": 0.5},
		{"spawn": "shield"},
		{"spawn": "rocket", "dx": 40},
		{"wait_clear": true},
		{"spawn": "heli", "at": "sky", "y": 60},
		{"spawn": "soldier", "count": 2, "gap": 0.5},
	], {"lock_camera": true, "lock_x": 4860})


# ------------------------------------------------------------------ D. bridge
func _build_bridge() -> void:
	water(5200, 6200, 330)
	bridge = Bridge.new().setup(5200, 6200, 292)
	add_child(bridge)
	# guard post on the bridge
	platform(5720, 244, 80, "wood")
	deco("sandbags", 5760, false, 244)
	g(6200, 6260, 300, "jungle")
	deco("jungle_tree", 5160)
	deco("jungle_tree", 6230, false, INF, true)

	# The ambush the design doc asked for:
	# left squad -> MG on the right -> gunship -> bridge blows up behind you.
	trigger(5330, [
		{"message": "AMBUSH!", "dur": 1.5},
		{"spawn": "soldier", "at": "left", "count": 3, "gap": 0.4},
		{"wait": 0.8},
		{"spawn": "gunner", "at": "abs", "x": 5760, "y": 244, "enter": "none"},
		{"wait": 1.5},
		{"spawn": "heli", "at": "sky", "y": 60},
		{"wait": 1.5},
		{"shake": 0.6},
		{"call": func(): bridge.collapse_chase(5200.0, 6000.0, 72.0)},
		{"wait": 3.0},
		{"spawn": "soldier", "count": 2, "gap": 0.6},
	])


# ------------------------------------------------------------------ E. base
func _build_base() -> void:
	g(6260, 8700, 300, "concrete")
	deco("building", 6700)
	deco("radar", 6560)
	deco("building", 7420)
	deco("building", 8100)
	deco("flag", 6860)
	deco("flag", 7980)
	# player tank waiting at the base entrance
	var tank := Tank.new()
	add_entity(tank, Vector2(6420, 300))
	trigger(6300, [
		{"message": "TANK AVAILABLE!  PRESS E", "dur": 2.5, "big": false},
	])
	prop("hedgehog", 6820)
	_gate_wall = prop("wall", 6960)
	prop("sandbags", 7060)
	prop("supply", 7120, "rocket")
	# containers to fight on
	block(7280, 252, 96, 48, "metal")
	_grounds.push_front([7280, 7376, 252])
	block(7376, 204, 64, 96, "metal")
	_grounds.push_front([7376, 7440, 204])
	platform(7520, 236, 96, "metal")
	platform(7640, 196, 96, "metal")
	prop("barrel", 7470)
	prop("barrel", 7488)
	prop("barrel", 7479, "", 276)
	prop("bunker", 7800)
	hostage(7900, "rocket")
	prop("crate", 8060, "")
	prop("barrel", 8200)
	prop("barrel", 8218)
	prop("barricade", 8420)
	prop("hedgehog", 8520)

	trigger(6460, [
		{"spawn": "soldier", "count": 3, "gap": 0.4},
		{"spawn": "shield", "dx": 40},
	])
	trigger(6900, [
		{"message": "BLAST THROUGH THE GATE!", "dur": 1.5, "big": false},
		{"spawn": "turret", "at": "abs", "x": 7408, "y": 204, "enter": "none"},
		{"spawn": "soldier", "at": "abs", "x": 7320, "y": 252, "enter": "none"},
	])
	trigger(7150, [
		{"spawn": "soldier", "at": "top", "fx": 0.5},
		{"spawn": "soldier", "at": "top", "fx": 0.65},
		{"spawn": "soldier", "at": "top", "fx": 0.8},
		{"spawn": "charger", "count": 2, "gap": 0.4},
		{"wait": 2.0},
		{"spawn": "rocket", "at": "abs", "x": 7680, "y": 196, "enter": "none"},
		{"spawn": "car"},
		{"wait_clear": true},
		{"spawn": "soldier", "at": "left", "count": 2, "gap": 0.5},
		{"spawn": "shield", "count": 2, "gap": 1.0},
	], {"lock_camera": true, "lock_x": 7440})
	trigger(7900, [
		{"spawn": "heli", "at": "sky", "y": 65},
		{"spawn": "soldier", "count": 3, "gap": 0.4},
		{"spawn": "turret", "at": "abs", "x": 8320, "y": 300, "enter": "none"},
	])


# ------------------------------------------------------------------ F. airfield
func _build_airfield() -> void:
	g(8700, 10240, 300, "concrete")
	deco("radar", 8800)
	deco("building", 9300)
	deco("flag", 9000)
	deco("flag", 9900)
	prop("sandbags", 8950)
	prop("barrel", 9120)
	prop("barrel", 9138)
	prop("hedgehog", 9500)
	prop("sandbags", 9700)
	prop("barrel", 9820)
	prop("barrel", 9838)
	prop("barrel", 9829, "", 276)
	hostage(9620, "hmg")
	prop("supply", 9960, "random")

	trigger(8760, [
		{"message": "TANK BATTLE!", "dur": 2.0},
		{"sfx": "warning"},
		{"spawn": "car"},
		{"spawn": "soldier", "count": 3, "gap": 0.4, "dx": 60},
		{"wait": 3.0},
		{"spawn": "soldier", "at": "left", "count": 3, "gap": 0.4},
		{"spawn": "rocket", "dx": 30},
		{"wait_clear": true},
		{"spawn": "car"},
		{"spawn": "heli", "at": "sky", "y": 55},
		{"spawn": "charger", "count": 3, "gap": 0.5},
	], {"lock_camera": true, "lock_x": 9000})
	trigger(9450, [
		{"message": "ENEMY REINFORCEMENTS!", "dur": 1.5, "big": false},
		{"spawn": "soldier", "count": 4, "gap": 0.25},
		{"spawn": "soldier", "at": "top", "fx": 0.3},
		{"spawn": "soldier", "at": "top", "fx": 0.5},
		{"spawn": "soldier", "at": "top", "fx": 0.7},
		{"wait": 1.5},
		{"spawn": "shield", "count": 2, "gap": 0.8},
		{"spawn": "rocket", "count": 2, "gap": 0.8, "dx": 40},
		{"spawn": "charger", "at": "left", "count": 2, "gap": 0.4},
		{"wait_clear": true},
		{"spawn": "car"},
		{"spawn": "soldier", "count": 3, "gap": 0.4, "dx": 80},
	], {"lock_camera": true, "lock_x": 9700})


# ------------------------------------------------------------------ G. boss
func _build_boss_arena() -> void:
	deco("wall_segment", 10870)
	deco("wall_segment", 10840)
	deco("building", 10990)
	item("bombs", 10300)
	trigger(10300, [
		{"call": func(): CameraManager.lock_at(BOSS_ARENA_CENTER)},
		{"call": func(): AudioManager.stop_music()},
		{"sfx": "warning"},
		{"message": "WARNING!!", "dur": 2.4},
		{"wait": 0.8},
		{"sfx": "warning"},
		{"wait": 0.8},
		{"call": _spawn_boss},
	])


func _spawn_boss() -> void:
	AudioManager.play_music("boss")
	boss = MechTankBoss.new()
	boss.arena_left = BOSS_ARENA_CENTER - 320.0
	boss.arena_right = BOSS_ARENA_CENTER + 320.0
	boss.home_x = BOSS_ARENA_CENTER + 170.0
	boss.position = Vector2(BOSS_ARENA_CENTER + 480.0, 300)
	entities.add_child(boss)
	boss.defeated.connect(_on_boss_defeated)
	# it smashes through the fortress wall
	get_tree().create_timer(1.4, false).timeout.connect(func(): FX.boom(Vector2(10860, 250), 60.0, 0.0, Combat.Team.NEUTRAL))
	for s in decor_back.get_children():
		if s is Sprite2D and s.position.x > 10830 and s.position.x < 10880:
			get_tree().create_timer(1.5, false).timeout.connect(s.queue_free)


func _on_boss_defeated() -> void:
	GameManager.show_message("MISSION COMPLETE!", 4.0)
	get_tree().create_timer(3.5, false).timeout.connect(complete)
