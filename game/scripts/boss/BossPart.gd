class_name BossPart
extends Node2D
## A hurt box on a boss. Damage is forwarded to the owning BossBase with a
## per-part multiplier (armor vs. weak point). Rects mirror with the boss.

var boss: BossBase
var local_rect := Rect2()
var mult := 1.0
var weak := false
var meleeable := false


func _ready() -> void:
	Combat.register(self, Combat.Team.ENEMY)


func _exit_tree() -> void:
	Combat.unregister(self)


func get_hurt_rect() -> Rect2:
	var r := local_rect
	if boss.facing < 0.0:
		r.position.x = -r.position.x - r.size.x
	return Rect2(boss.global_position + r.position, r.size)


func is_hittable() -> bool:
	return boss != null and boss.alive


func take_hit(info: Dictionary) -> int:
	return boss.on_part_hit(self, info)
