class_name ChargerSoldier
extends EnemyBase
## Knife-wielding rusher: sprints straight at the player, hops obstacles and
## slashes at close range after a short, readable wind-up.

var _slashed := false


func _init() -> void:
	hp = 3.0
	max_hp = 3.0
	score_value = 150
	speed = 175.0
	body_style = "charger"
	gun_id = ""


func _on_ready() -> void:
	if state != "drop":
		set_state("charge")
	alert_popup()


func _think(delta: float) -> void:
	var tp := target_pos()
	var dx := tp.x - global_position.x
	var dy := tp.y - (global_position.y - 20.0)
	match state:
		"alert":
			stop(delta)
			face_target()
			if state_t > 0.25:
				set_state("charge")
		"charge":
			if not GameManager.target_alive():
				walk(facing, speed * 0.4, delta)
				return
			walk(signf(dx), speed, delta)
			if is_on_floor() and (wall_ahead() or (dy < -40.0 and absf(dx) < 90.0)):
				velocity.y = -380.0
			if absf(dx) < 28.0 and absf(dy) < 34.0:
				set_state("windup")
				_slashed = false
		"windup":
			stop(delta)
			face_target()
			_flash = 0.5 if int(state_t * 20.0) % 2 == 0 else 0.0
			if state_t > 0.2:
				set_state("slash")
		"slash":
			if not _slashed:
				_slashed = true
				AudioManager.play("knife", 0.1)
				velocity.x = facing * 120.0
				var r := Rect2(global_position.x + (0.0 if facing > 0 else -34.0), global_position.y - 42.0, 34.0, 42.0)
				for t in Combat.rect_query(r, Combat.Team.ENEMY):
					t.take_hit({"damage": 1.0, "dir": Vector2(facing, -0.4), "pos": r.get_center(), "kind": "melee", "source": self})
			stop(delta)
			if state_t > 0.45:
				set_state("charge")


func _override_frame(key: String) -> String:
	if state == "windup" or state == "slash":
		return "knife"
	return key
