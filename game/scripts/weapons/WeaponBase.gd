class_name WeaponBase
extends RefCounted
## Base class for all player weapons. Weapons are plain objects owned by the
## shooter; they spawn pooled projectiles and handle ammo / fire rate.

var id := "pistol"
var display_name := "PISTOL"
var ammo := -1               # -1 = infinite
var max_ammo := -1
var tap_cooldown := 0.1      # fire rate when tapping
var hold_cooldown := 0.2     # fire rate when holding the trigger
var sfx := "pistol"
var shake := 0.0
var muzzle_size := 8.0
var shells := true
var aim_sweep_speed := 0.0   # >0: aim rotates smoothly (heavy MG spray)
var team: int = Combat.Team.PLAYER

var _cooldown := 0.0


func update(delta: float) -> void:
	_cooldown -= delta


func is_empty() -> bool:
	return ammo == 0


func add_ammo(n: int) -> void:
	if ammo >= 0:
		ammo = mini(ammo + n, max_ammo)


## Returns true if a shot was fired.
func fire(shooter: Node2D, origin: Vector2, angle: float, fresh_press: bool) -> bool:
	if _cooldown > 0.0 or ammo == 0:
		return false
	_cooldown = tap_cooldown if fresh_press else hold_cooldown
	_emit(shooter, origin, angle)
	if ammo > 0:
		ammo -= 1
	FX.muzzle(origin, angle, muzzle_size)
	AudioManager.play(sfx, 0.06)
	if shake > 0.0:
		CameraShakeManager.shake(shake)
	return true


## Override: spawn projectiles.
func _emit(_shooter: Node2D, origin: Vector2, angle: float) -> void:
	Projectile.shoot(Projectile.Kind.BULLET, origin, Vector2.from_angle(angle) * 480.0, team, 1.0)
