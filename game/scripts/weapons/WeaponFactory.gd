class_name WeaponFactory
extends RefCounted
## Creates weapons from ids used by pickups and supply crates.

const IDS := ["hmg", "shotgun", "rocket", "flame"]


static func create(id: String) -> WeaponBase:
	match id:
		"hmg":
			return MachineGun.new()
		"shotgun":
			return Shotgun.new()
		"rocket":
			return RocketLauncher.new()
		"flame":
			return FlameThrower.new()
	return Pistol.new()
