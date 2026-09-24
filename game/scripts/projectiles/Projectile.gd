class_name Projectile
extends Node2D
## Pooled projectile covering every bullet type in the game.
##
## Movement is a swept segment each frame: first a ray against solid world
## geometry, then a segment test against the opposing team's hurt boxes via
## Combat. Explosive kinds call FX.boom on impact. Some enemy projectiles
## (rockets, missiles, bombs) can be shot down by the player.

enum Kind {
	BULLET, HMG, PELLET, ROCKET, FLAME, GRENADE, TANK_MG, TANK_SHELL,
	ENEMY_BULLET, ENEMY_ROCKET, MISSILE, ENEMY_SHELL, ENEMY_GRENADE, BOMB,
}

const POOL_KEY := &"projectile"

# kind -> defaults
const DEF := {
	Kind.BULLET: {"tex": "bullet", "life": 1.0},
	Kind.HMG: {"tex": "bullet_hmg", "life": 1.0},
	Kind.PELLET: {"tex": "pellet", "life": 0.2, "drag": 3.0},
	Kind.ROCKET: {"tex": "rocket", "life": 2.0, "accel": 700.0, "boom": 36.0, "trail": true},
	Kind.FLAME: {"life": 0.42, "drag": 2.4, "pierce": true, "flame": true, "radius": 5.0},
	Kind.GRENADE: {"tex": "grenade", "life": 2.2, "gravity": 750.0, "bounce": 1, "boom": 44.0, "spin": true},
	Kind.TANK_MG: {"tex": "bullet_hmg", "life": 1.0},
	Kind.TANK_SHELL: {"tex": "shell", "life": 2.0, "gravity": 160.0, "boom": 46.0, "trail": true},
	Kind.ENEMY_BULLET: {"tex": "enemy_bullet", "life": 3.0},
	Kind.ENEMY_ROCKET: {"tex": "rocket", "life": 4.0, "accel": 90.0, "boom": 26.0, "trail": true, "shootable": true, "homing": 0.9},
	Kind.MISSILE: {"tex": "missile", "life": 5.0, "accel": 120.0, "boom": 30.0, "trail": true, "shootable": true, "homing": 1.6},
	Kind.ENEMY_SHELL: {"tex": "shell", "life": 3.0, "gravity": 0.0, "boom": 34.0, "trail": true},
	Kind.ENEMY_GRENADE: {"tex": "grenade", "life": 2.5, "gravity": 650.0, "bounce": 0, "boom": 30.0, "spin": true, "shootable": true},
	Kind.BOMB: {"tex": "bomb", "life": 4.0, "gravity": 500.0, "boom": 40.0, "shootable": true},
}

var kind: int = Kind.BULLET
var team: int = Combat.Team.PLAYER
var vel := Vector2.ZERO
var damage := 1.0
var life := 1.0
var max_life := 1.0
var accel := 0.0
var drag := 0.0
var gravity := 0.0
var boom_radius := 0.0
var bounces := 0
var pierce := false
var flame := false
var radius := 0.0
var trail := false
var spin := false
var shootable := false
var homing := 0.0
var max_speed := 900.0
var knockback := 1.0
var _hit_set: Dictionary = {}
var _tex: Texture2D
var _trail_t := 0.0
var _registered := false
var player_only := false
var _stopped := false


static func shoot(k: int, pos: Vector2, velocity: Vector2, t: int, dmg: float) -> Projectile:
	var p: Projectile = ObjectPool.acquire(POOL_KEY)
	p._setup(k, pos, velocity, t, dmg)
	return p


func _setup(k: int, pos: Vector2, velocity: Vector2, t: int, dmg: float) -> void:
	kind = k
	team = t
	vel = velocity
	damage = dmg
	global_position = pos
	var d: Dictionary = DEF[k]
	life = d.get("life", 1.0)
	max_life = life
	accel = d.get("accel", 0.0)
	drag = d.get("drag", 0.0)
	gravity = d.get("gravity", 0.0)
	boom_radius = d.get("boom", 0.0)
	bounces = d.get("bounce", 0)
	pierce = d.get("pierce", false)
	flame = d.get("flame", false)
	radius = d.get("radius", 0.0)
	trail = d.get("trail", false)
	spin = d.get("spin", false)
	shootable = d.get("shootable", false)
	homing = d.get("homing", 0.0)
	knockback = 1.0
	max_speed = maxf(velocity.length() * 2.5, 200.0)
	_tex = Art.tex(d["tex"]) if d.has("tex") else null
	_hit_set.clear()
	_trail_t = 0.0
	_stopped = false
	z_index = 15
	rotation = vel.angle() if not spin else 0.0
	if shootable and team == Combat.Team.ENEMY:
		Combat.register(self, Combat.Team.ENEMY)
		_registered = true
	queue_redraw()


func _on_pool_release() -> void:
	if _registered:
		Combat.unregister(self, Combat.Team.ENEMY)
		_registered = false


# -- target protocol for shoot-down-able projectiles
func get_hurt_rect() -> Rect2:
	return Rect2(global_position - Vector2(6, 6), Vector2(12, 12))


func is_hittable() -> bool:
	return _registered and life > 0.0


func take_hit(info: Dictionary) -> int:
	if info.get("kind", "") == "explosion" and info.get("source") == self:
		return Combat.HIT_NONE
	GameManager.add_score(50)
	# shot down: detonates harmlessly for the player
	team = Combat.Team.NEUTRAL
	damage = 2.0
	_impact(global_position, Vector2.ZERO)
	return Combat.HIT_OK


func _physics_process(delta: float) -> void:
	life -= delta
	if life <= 0.0:
		if boom_radius > 0.0:
			_impact(global_position, Vector2.ZERO)
		else:
			ObjectPool.release(self)
		return
	if _stopped:
		queue_redraw()
		return
	if homing > 0.0 and life < max_life - 0.25 and GameManager.target_alive():
		var want := (GameManager.target_pos() - global_position).angle()
		var cur := vel.angle()
		var diff := wrapf(want - cur, -PI, PI)
		var turn := clampf(diff, -homing * delta, homing * delta)
		vel = vel.rotated(turn)
		# homing fades so missiles stay dodgeable
		homing = maxf(homing - delta * 0.35, 0.0)
	if accel != 0.0:
		vel += vel.normalized() * accel * delta
		vel = vel.limit_length(max_speed)
	if drag > 0.0:
		vel *= maxf(0.0, 1.0 - drag * delta)
	vel.y += gravity * delta
	var from := global_position
	var to := from + vel * delta
	var hit_world := Combat.ray_world(from, to, Combat.L_WORLD)
	var seg_end: Vector2 = hit_world.position if not hit_world.is_empty() else to
	var hit := Combat.segment_query(from, seg_end, team, radius, _hit_set)
	if not hit.is_empty():
		var target: Node = hit.target
		var res: int = target.take_hit({"damage": damage, "dir": vel.normalized(), "pos": hit.pos, "kind": "flame" if flame else "bullet", "source": self, "knockback": knockback})
		if res != Combat.HIT_NONE:
			if pierce:
				_hit_set[target] = true
			else:
				if res == Combat.HIT_BLOCKED:
					_ricochet(hit.pos)
				else:
					_impact(hit.pos, -vel.normalized())
				return
	if not hit_world.is_empty():
		if bounces > 0:
			bounces -= 1
			var n: Vector2 = hit_world.normal
			vel = vel.bounce(n) * 0.45
			global_position = hit_world.position + n * 2.0
			AudioManager.play("land", 0.2, -6.0)
			return
		if flame:
			global_position = hit_world.position
			_stopped = true
			return
		_impact(hit_world.position, hit_world.normal)
		return
	global_position = to
	if spin:
		rotation += delta * 14.0 * signf(vel.x)
	elif gravity != 0.0 or homing > 0.0:
		rotation = vel.angle()
	if trail:
		_trail_t -= delta
		if _trail_t <= 0.0:
			_trail_t = 0.025
			var back := global_position - vel.normalized() * 8.0
			FX.back.spawn(ParticleLayer.SMOKE, back, Vector2(randf_range(-8, 8), randf_range(-12, 0)), 0.5, 2.0, Color(0.9, 0.9, 0.85, 0.7), 0, 1.0, 5.0, Color(0.6, 0.6, 0.6, 0.0))
			FX.front.spawn(ParticleLayer.FIRE, back, Vector2.ZERO, 0.08, 2.0, Color.WHITE)
	if flame:
		queue_redraw()
	# cull far outside the screen
	if not CameraManager.is_visible_point(global_position, 96.0):
		ObjectPool.release(self)


func _impact(pos: Vector2, normal: Vector2) -> void:
	if boom_radius > 0.0:
		var dmg := damage if team != Combat.Team.PLAYER else damage * 0.8
		var r := boom_radius
		var t := team
		var src: Node = self
		ObjectPool.release(self)
		FX.boom(pos, r, dmg, t, src)
		return
	if not flame:
		FX.hit_puff(pos, normal if normal != Vector2.ZERO else -vel.normalized())
	ObjectPool.release(self)


func _ricochet(pos: Vector2) -> void:
	FX.spark(pos, Vector2(-signf(vel.x), -0.6).normalized(), 5, Color("ffffff"))
	AudioManager.play("tink", 0.15, -4.0)
	ObjectPool.release(self)


func _draw() -> void:
	if flame:
		var k := 1.0 - life / max_life
		var r := lerpf(3.0, 11.0, k)
		draw_circle(Vector2.ZERO, r, ParticleLayer.fire_color(k * 0.85 + 0.05))
		draw_circle(Vector2(0, -r * 0.3), r * 0.55, ParticleLayer.fire_color(k * 0.5))
		return
	if _tex != null:
		draw_texture(_tex, -_tex.get_size() * 0.5)
