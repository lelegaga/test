class_name MachineGun
extends WeaponBase
## Heavy machine gun: very fast fire, 100 rounds, aim sweeps between
## directions so the stream of bullets sprays across the screen.


func _init() -> void:
	id = "hmg"
	display_name = "HEAVY MACHINE GUN"
	ammo = 100
	max_ammo = 200
	tap_cooldown = 0.055
	hold_cooldown = 0.055
	sfx = "mg"
	muzzle_size = 11.0
	shake = 0.04
	aim_sweep_speed = 11.0


func _emit(_shooter: Node2D, origin: Vector2, angle: float) -> void:
	var a := angle + randf_range(-0.05, 0.05)
	var off := Vector2.from_angle(a + PI * 0.5) * randf_range(-2.0, 2.0)
	Projectile.shoot(Projectile.Kind.HMG, origin + off, Vector2.from_angle(a) * 620.0, team, 1.25)
