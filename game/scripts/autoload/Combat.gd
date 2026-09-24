extends Node
## Hit registry and collision queries.
##
## Everything that can be damaged registers itself here with a team. Projectiles
## do cheap segment-vs-rect tests against the opposing lists instead of relying
## on hundreds of Area2D nodes, which keeps 100+ bullets on screen cheap.
##
## Target protocol (duck-typed):
##   get_hurt_rect() -> Rect2        global-space hurt box
##   take_hit(info: Dictionary) -> int   HIT_* result
##   is_hittable() -> bool
##   optional property `player_only` (e.g. hostage ropes)

enum Team { PLAYER, ENEMY, NEUTRAL }

const HIT_NONE := 0      # ignored, projectile keeps flying
const HIT_OK := 1        # damage applied
const HIT_BLOCKED := 2   # shield / armor deflection

# Physics layers (bit values)
const L_WORLD := 1
const L_PLAYER := 2
const L_ENEMY := 4
const L_VEHICLE := 8
const L_ONEWAY := 16
const L_PROPS := 32

const MASK_ACTOR := L_WORLD | L_ONEWAY | L_PROPS

var _lists: Array = [[], [], []]


func register(t: Node, team: int) -> void:
	var list: Array = _lists[team]
	if not list.has(t):
		list.append(t)


func unregister(t: Node, team: int = -1) -> void:
	if team >= 0:
		_lists[team].erase(t)
	else:
		for l in _lists:
			l.erase(t)


func clear() -> void:
	_lists = [[], [], []]


func count(team: int) -> int:
	return _lists[team].size()


## Which lists a given attacker team can damage.
func _victims(team: int, is_explosion: bool) -> Array:
	match team:
		Team.PLAYER:
			return [_lists[Team.ENEMY], _lists[Team.NEUTRAL]]
		Team.ENEMY:
			if is_explosion:
				return [_lists[Team.PLAYER], _lists[Team.NEUTRAL]]
			return [_lists[Team.PLAYER]]
		_:
			# Environmental explosions (oil barrels) hurt enemies and props.
			return [_lists[Team.ENEMY], _lists[Team.NEUTRAL]]


## Segment/rect slab test. Returns entry t in [0,1] or -1 when missing.
static func seg_rect(a: Vector2, b: Vector2, r: Rect2) -> float:
	var tmin := 0.0
	var tmax := 1.0
	var d := b - a
	for axis in 2:
		var p: float = a[axis]
		var dd: float = d[axis]
		var lo: float = r.position[axis]
		var hi: float = r.end[axis]
		if absf(dd) < 0.00001:
			if p < lo or p > hi:
				return -1.0
		else:
			var t1 := (lo - p) / dd
			var t2 := (hi - p) / dd
			if t1 > t2:
				var tmp := t1
				t1 = t2
				t2 = tmp
			tmin = maxf(tmin, t1)
			tmax = minf(tmax, t2)
			if tmin > tmax:
				return -1.0
	return tmin


## First target hit along a segment. `radius` grows the rects (fat projectiles).
## `exclude` is a Dictionary used as a set for piercing projectiles.
func segment_query(a: Vector2, b: Vector2, team: int, radius: float = 0.0, exclude: Dictionary = {}) -> Dictionary:
	var best_t := 2.0
	var best: Node = null
	for list in _victims(team, false):
		for t in list:
			if exclude.has(t):
				continue
			if not t.is_hittable():
				continue
			if team != Team.PLAYER and t.get("player_only") == true:
				continue
			var r: Rect2 = t.get_hurt_rect()
			if radius > 0.0:
				r = r.grow(radius)
			var tt := seg_rect(a, b, r)
			if tt >= 0.0 and tt < best_t:
				best_t = tt
				best = t
	if best == null:
		return {}
	return {"target": best, "t": best_t, "pos": a.lerp(b, best_t)}


## All targets overlapping a rect (melee, crushing).
func rect_query(r: Rect2, team: int) -> Array:
	var out: Array = []
	for list in _victims(team, false):
		for t in list:
			if t.is_hittable() and t.get_hurt_rect().intersects(r):
				out.append(t)
	return out


## Radial damage with falloff. Returns number of victims.
func explode(pos: Vector2, radius: float, damage: float, team: int, source: Node = null) -> int:
	var n := 0
	for list in _victims(team, true):
		for t in list.duplicate():
			if not is_instance_valid(t) or not t.is_hittable():
				continue
			if t == source:
				continue
			var r: Rect2 = t.get_hurt_rect()
			var closest := Vector2(clampf(pos.x, r.position.x, r.end.x), clampf(pos.y, r.position.y, r.end.y))
			var dist := closest.distance_to(pos)
			if dist > radius:
				continue
			var falloff := 1.0 if dist < radius * 0.5 else lerpf(1.0, 0.4, (dist - radius * 0.5) / (radius * 0.5))
			var dir := (r.get_center() - pos).normalized()
			if dir == Vector2.ZERO:
				dir = Vector2.UP
			t.take_hit({"damage": damage * falloff, "dir": dir, "pos": closest, "kind": "explosion", "source": source})
			n += 1
	return n


func ray_world(a: Vector2, b: Vector2, mask: int = L_WORLD) -> Dictionary:
	var space := get_viewport().world_2d.direct_space_state if get_viewport() else null
	if space == null:
		return {}
	var q := PhysicsRayQueryParameters2D.create(a, b, mask)
	q.hit_from_inside = false
	return space.intersect_ray(q)


## Finds the top of the first solid surface below `from`.
func ground_below(from: Vector2, max_dist: float = 600.0, mask: int = L_WORLD | L_ONEWAY | L_PROPS) -> float:
	var hit := ray_world(from, from + Vector2(0, max_dist), mask)
	if hit.is_empty():
		return INF
	return hit.position.y


func hit_info(damage: float, dir: Vector2, pos: Vector2, kind: String = "bullet", source: Node = null) -> Dictionary:
	return {"damage": damage, "dir": dir, "pos": pos, "kind": kind, "source": source}
