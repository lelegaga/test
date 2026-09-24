class_name Shotgun
extends WeaponBase
## Short range, huge damage cone.


func _init() -> void:
	id = "shotgun"
	display_name = "SHOTGUN"
	ammo = 20
	max_ammo = 40
	tap_cooldown = 0.42
	hold_cooldown = 0.5
	sfx = "shotgun"
	muzzle_size = 18.0
	shake = 0.3


func _emit(_shooter: Node2D, origin: Vector2, angle: float) -> void:
	for i in 8:
		var a := angle + randf_range(-0.26, 0.26)
		var p := Projectile.shoot(Projectile.Kind.PELLET, origin, Vector2.from_angle(a) * randf_range(420.0, 560.0), team, 3.0)
		p.knockback = 3.0
	FX.smoke(origin + Vector2.from_angle(angle) * 10.0, 3, 3.0, false)
