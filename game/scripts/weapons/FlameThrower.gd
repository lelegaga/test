class_name FlameThrower
extends WeaponBase
## Continuous piercing stream of flames.


func _init() -> void:
	id = "flame"
	display_name = "FLAME SHOT"
	ammo = 160
	max_ammo = 300
	tap_cooldown = 0.03
	hold_cooldown = 0.03
	sfx = "flame"
	muzzle_size = 6.0
	shells = false


func _emit(shooter: Node2D, origin: Vector2, angle: float) -> void:
	var inherit := Vector2.ZERO
	if shooter is CharacterBody2D:
		inherit = (shooter as CharacterBody2D).velocity * 0.4
	Projectile.shoot(Projectile.Kind.FLAME, origin, Vector2.from_angle(angle + randf_range(-0.07, 0.07)) * randf_range(300.0, 360.0) + inherit, team, 0.6)
