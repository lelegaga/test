class_name RocketLauncher
extends WeaponBase
## Accelerating rockets with splash damage.


func _init() -> void:
	id = "rocket"
	display_name = "ROCKET LAUNCHER"
	ammo = 20
	max_ammo = 40
	tap_cooldown = 0.32
	hold_cooldown = 0.4
	sfx = "rocket"
	muzzle_size = 12.0
	shake = 0.12
	shells = false


func _emit(_shooter: Node2D, origin: Vector2, angle: float) -> void:
	Projectile.shoot(Projectile.Kind.ROCKET, origin, Vector2.from_angle(angle) * 160.0, team, 8.0)
