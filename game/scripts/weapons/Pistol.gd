class_name Pistol
extends WeaponBase
## Infinite ammo sidearm. Tapping fires faster than holding (arcade feel).


func _init() -> void:
	id = "pistol"
	display_name = "PISTOL"
	ammo = -1
	tap_cooldown = 0.09
	hold_cooldown = 0.18
	sfx = "pistol"
	muzzle_size = 7.0


func _emit(_shooter: Node2D, origin: Vector2, angle: float) -> void:
	Projectile.shoot(Projectile.Kind.BULLET, origin, Vector2.from_angle(angle + randf_range(-0.02, 0.02)) * 500.0, team, 1.0)
