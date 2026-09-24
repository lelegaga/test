extends Node
## Visual feedback façade: muzzle flashes, shells, sparks, smoke, debris,
## explosions, score popups and hit-flash materials.

var back: ParticleLayer      # behind actors (smoke, ground flames)
var front: ParticleLayer     # in front of actors (sparks, debris)
var flash_shader: Shader

signal screen_flash(amount: float)


func _ready() -> void:
	flash_shader = Shader.new()
	flash_shader.code = """
shader_type canvas_item;
uniform float flash : hint_range(0.0, 1.0) = 0.0;
uniform vec4 flash_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	c.rgb = mix(c.rgb, flash_color.rgb, flash);
	COLOR = c;
}
"""
	ObjectPool.register(&"explosion", func(): return Explosion.new())
	ObjectPool.register(&"popup", func(): return TextPopup.new())


func setup(back_layer: ParticleLayer, front_layer: ParticleLayer) -> void:
	back = back_layer
	front = front_layer


func make_flash_material() -> ShaderMaterial:
	var m := ShaderMaterial.new()
	m.shader = flash_shader
	return m


func ready() -> bool:
	return front != null and is_instance_valid(front)


# ------------------------------------------------------------------ small fx
func muzzle(pos: Vector2, angle: float, size: float = 8.0) -> void:
	if not ready():
		return
	front.spawn(ParticleLayer.MUZZLE, pos, Vector2.from_angle(angle), 0.06, size, Color.WHITE)
	front.spawn(ParticleLayer.SMOKE, pos, Vector2.from_angle(angle) * 30.0 + Vector2(0, -10), 0.35, 2.0, Color(0.9, 0.9, 0.9, 0.5), 0, 1.0, 5.0, Color(0.7, 0.7, 0.7, 0.0))


func shell(pos: Vector2, dir_x: float, col: Color = Color("e0b040")) -> void:
	if not ready():
		return
	var floor_y := Combat.ground_below(pos, 200.0)
	front.spawn(ParticleLayer.SHELL, pos, Vector2(-dir_x * randf_range(40, 90), randf_range(-160, -100)), 1.2, 1, col, 800, 0.0, -1, Color(0, 0, 0, 0), floor_y - 2.0)


func spark(pos: Vector2, dir: Vector2, n: int = 4, col: Color = Color("fff4a0")) -> void:
	if not ready():
		return
	for i in n:
		var d := dir.rotated(randf_range(-0.9, 0.9)) * randf_range(80, 220)
		front.spawn(ParticleLayer.SPARK, pos, d, randf_range(0.08, 0.2), 1, col, 200, 3.0, -1, Color("ff8030"))


func hit_puff(pos: Vector2, dir: Vector2) -> void:
	if not ready():
		return
	front.spawn(ParticleLayer.SMOKE, pos, dir * 20.0, 0.18, 3.0, Color(1, 1, 0.8, 0.9), 0, 1.0, 6.0, Color(1, 0.8, 0.4, 0.0))
	spark(pos, dir, 3)


## Cartoon "ouch" feedback instead of gore: stars and sweat drops.
func cartoon_hit(pos: Vector2) -> void:
	if not ready():
		return
	for i in 4:
		var a := randf_range(-PI, 0)
		front.spawn(ParticleLayer.STAR, pos, Vector2(cos(a), sin(a)) * randf_range(40, 110), 0.5, 1, Color("fff27a"), 200, 1.0)
	for i in 3:
		front.spawn(ParticleLayer.DROP, pos + Vector2(0, -6), Vector2(randf_range(-60, 60), randf_range(-120, -60)), 0.6, 1, Color("8ad0ff"), 500)


func dust(pos: Vector2, n: int = 4, spread: float = 30.0) -> void:
	if not ready():
		return
	for i in n:
		back.spawn(ParticleLayer.SMOKE, pos + Vector2(randf_range(-4, 4), 0), Vector2(randf_range(-spread, spread), randf_range(-25, -5)), randf_range(0.3, 0.6), 2.0, Color(0.85, 0.78, 0.6, 0.8), 0, 2.0, 5.0, Color(0.85, 0.78, 0.6, 0.0))


func smoke(pos: Vector2, n: int = 3, size: float = 4.0, dark: bool = true) -> void:
	if not ready():
		return
	var c := Color(0.15, 0.13, 0.13, 0.8) if dark else Color(0.8, 0.8, 0.8, 0.6)
	for i in n:
		back.spawn(ParticleLayer.SMOKE, pos + Vector2(randf_range(-4, 4), randf_range(-4, 4)), Vector2(randf_range(-10, 10), randf_range(-40, -15)), randf_range(0.6, 1.2), size, c, -5, 0.5, size * 2.2, Color(c.r + 0.2, c.g + 0.2, c.b + 0.2, 0.0))


func debris(pos: Vector2, n: int, col: Color, speed: float = 200.0, size: float = 3.0) -> void:
	if not ready():
		return
	var floor_y := Combat.ground_below(pos + Vector2(0, -4), 300.0)
	for i in n:
		var a := randf_range(-PI * 0.95, -PI * 0.05)
		front.spawn(ParticleLayer.SQUARE, pos, Vector2(cos(a), sin(a)) * randf_range(speed * 0.4, speed), randf_range(0.6, 1.3), randf_range(size * 0.6, size * 1.3), col, 700, 0.3, -1, Color(0, 0, 0, 0), floor_y)


func fire(pos: Vector2, n: int = 3, spread: float = 8.0) -> void:
	if not ready():
		return
	for i in n:
		front.spawn(ParticleLayer.FIRE, pos + Vector2(randf_range(-spread, spread), randf_range(-spread, spread) * 0.5), Vector2(randf_range(-10, 10), randf_range(-60, -20)), randf_range(0.2, 0.45), randf_range(2, 4), Color.WHITE, 0, 0.5, 0.5)


func ground_flames(pos: Vector2, n: int = 3, spread: float = 16.0) -> void:
	if not ready():
		return
	for i in n:
		back.spawn(ParticleLayer.FLAME, pos + Vector2(randf_range(-spread, spread), 0), Vector2.ZERO, randf_range(0.8, 1.8), randf_range(3, 5), Color.WHITE)


# ------------------------------------------------------------------ explosions
func explosion(pos: Vector2, radius: float, big: bool = false) -> void:
	if not ready():
		return
	var e: Explosion = ObjectPool.acquire(&"explosion")
	e.activate(pos, radius, big)


## Visual + damage + sound + shake in one call.
func boom(pos: Vector2, radius: float, damage: float, team: int, source: Node = null, shake: float = -1.0) -> void:
	explosion(pos, radius, radius >= 48.0)
	if damage > 0.0:
		Combat.explode(pos, radius, damage, team, source)
	if shake < 0.0:
		shake = clampf(radius / 90.0, 0.12, 0.8)
	CameraShakeManager.shake(shake)
	AudioManager.play("explode_big" if radius >= 48.0 else "explode", 0.12)
	if radius >= 64.0:
		CameraShakeManager.hit_stop(0.06)
		screen_flash.emit(0.25)


## Staggered chain of explosions over an area (vehicle / boss deaths).
func chain(center: Vector2, extent: Vector2, count: int, interval: float, radius: float) -> void:
	for i in count:
		var p := center + Vector2(randf_range(-extent.x, extent.x), randf_range(-extent.y, extent.y))
		get_tree().create_timer(i * interval, false).timeout.connect(_chain_boom.bind(p, radius * randf_range(0.7, 1.2)))


func _chain_boom(p: Vector2, r: float) -> void:
	if ready():
		boom(p, r, 0.0, Combat.Team.NEUTRAL)


func popup(pos: Vector2, text: String, col: Color = Color.WHITE, scale: int = 1) -> void:
	if not ready():
		return
	var p: TextPopup = ObjectPool.acquire(&"popup")
	p.activate(pos, text, col, scale)
