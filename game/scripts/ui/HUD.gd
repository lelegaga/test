class_name HUD
extends Control
## All UI is drawn with the bitmap PixelFont: score / arms / bombs / lives,
## vehicle and debug HP bars, boss bar, messages, GO arrow, title screen,
## pause, continue and results screens.

var msg := ""
var msg_t := 0.0
var msg_big := true
var boss_ratio := 0.0
var boss_shown := 0.0
var boss_visible := false
var boss_name := ""
var go_t := 0.0
var flash := 0.0
var t := 0.0
var continue_t := 0.0
var fps_smooth := 60.0
var title_sel := 0


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	process_mode = Node.PROCESS_MODE_ALWAYS
	GameManager.message.connect(_on_message)
	GameManager.boss_bar.connect(_on_boss_bar)
	GameManager.go_arrow.connect(_on_go_arrow)
	FX.screen_flash.connect(_on_flash)


func _on_message(text: String, dur: float, big: bool) -> void:
	msg = text
	msg_t = dur
	msg_big = big


func _on_boss_bar(ratio: float, vis: bool, n: String) -> void:
	boss_ratio = ratio
	boss_visible = vis
	boss_name = n


func _on_go_arrow(v: bool) -> void:
	go_t = 3.0 if v else 0.0


func _on_flash(a: float) -> void:
	flash = maxf(flash, a)


func _process(delta: float) -> void:
	t += delta
	msg_t -= delta
	go_t -= delta
	flash = maxf(flash - delta * 2.5, 0.0)
	boss_shown = move_toward(boss_shown, boss_ratio, delta * (0.8 if boss_shown < boss_ratio else 2.0))
	if delta > 0.0:
		fps_smooth = lerpf(fps_smooth, 1.0 / delta * Engine.time_scale, 0.05)
	queue_redraw()


func _draw() -> void:
	var st := GameManager.state
	if st == GameManager.State.TITLE:
		_draw_title()
		return
	_draw_status()
	if boss_visible:
		_draw_boss_bar()
	if msg_t > 0.0:
		var blink := msg_big and int(t * 8.0) % 2 == 0 and msg_t < 0.6
		if not blink:
			if msg_big:
				PixelFont.draw_centered(self, msg, Vector2(320, 120), 3, Color("fff27a"))
			else:
				PixelFont.draw_centered(self, msg, Vector2(320, 64), 1, Color.WHITE)
	if go_t > 0.0 and int(t * 4.0) % 2 == 0:
		PixelFont.draw_text(self, "GO", Vector2(560, 150), 3, Color("fff27a"))
		PixelFont.draw_text(self, ">", Vector2(600, 150), 3, Color("ff5a3a"))
	if flash > 0.0:
		draw_rect(Rect2(0, 0, 640, 360), Color(1, 1, 1, flash))
	match st:
		GameManager.State.PAUSED:
			draw_rect(Rect2(0, 0, 640, 360), Color(0, 0, 0, 0.55))
			PixelFont.draw_centered(self, "PAUSE", Vector2(320, 130), 4, Color.WHITE)
			PixelFont.draw_centered(self, "ESC  RESUME      Q  QUIT TO TITLE", Vector2(320, 190), 1, Color("c0c0c0"))
			_draw_controls(220)
		GameManager.State.GAME_OVER:
			draw_rect(Rect2(0, 0, 640, 360), Color(0, 0, 0, 0.6))
			PixelFont.draw_centered(self, "CONTINUE?", Vector2(320, 130), 4, Color("ff6a4a"))
			PixelFont.draw_centered(self, str(maxi(int(ceil(continue_t)), 0)), Vector2(320, 185), 4, Color.WHITE)
			PixelFont.draw_centered(self, "PRESS ENTER", Vector2(320, 230), 1, Color("fff27a"))
		GameManager.State.CLEAR:
			_draw_results()
	if GameManager.show_debug:
		_draw_debug()


func _draw_status() -> void:
	var gm := GameManager
	PixelFont.draw_text(self, "1UP", Vector2(8, 6), 1, Color("5ad0ff"))
	PixelFont.draw_text(self, "%08d" % gm.score, Vector2(32, 6), 1, Color.WHITE)
	# lives as little heads
	for i in mini(gm.lives, 9):
		draw_rect(Rect2(8 + i * 9, 18, 7, 6), Color("f1b98a"))
		draw_rect(Rect2(8 + i * 9, 18, 7, 2), Color("d8322e"))
	var p := gm.player as Player
	if p == null or not is_instance_valid(p):
		return
	var w := p.weapon
	# arms & bombs box
	draw_rect(Rect2(110, 4, 118, 22), Color(0, 0, 0, 0.45))
	PixelFont.draw_text(self, "ARMS", Vector2(114, 7), 1, Color("fff27a"))
	PixelFont.draw_text(self, "BOMB", Vector2(172, 7), 1, Color("fff27a"))
	var ammo_text := "~" if w.ammo < 0 else str(w.ammo)
	PixelFont.draw_text(self, ammo_text, Vector2(118, 16), 1, Color.WHITE)
	PixelFont.draw_text(self, str(p.grenades), Vector2(176, 16), 1, Color.WHITE)
	PixelFont.draw_text(self, w.display_name, Vector2(236, 7), 1, Color("c0c0c0"))
	PixelFont.draw_text(self, "POW %d" % gm.hostages_rescued, Vector2(580, 6), 1, Color("a0f0a0"))
	if gm.debug_hp_mode:
		_bar(Vector2(236, 17), 80, p.hp / gm.PLAYER_MAX_HP, Color("50e050"), "HP")
	if p.state == Player.St.IN_VEHICLE and p.vehicle != null and is_instance_valid(p.vehicle):
		var v = p.vehicle
		_bar(Vector2(8, 32), 90, v.hp / v.max_hp, Color("5ad0ff") if v.hp > v.max_hp * 0.3 else Color("ff4a3a"), v.vehicle_name)
	if gm.god_mode:
		PixelFont.draw_text(self, "GOD", Vector2(600, 18), 1, Color("ff8080"))


func _bar(pos: Vector2, width: float, ratio: float, col: Color, label: String) -> void:
	PixelFont.draw_text(self, label, pos, 1, Color.WHITE)
	var x := pos.x + PixelFont.text_width(label) + 4
	draw_rect(Rect2(x - 1, pos.y - 1, width + 2, 9), Color("1a1420"))
	draw_rect(Rect2(x, pos.y, width * clampf(ratio, 0, 1), 7), col)
	draw_rect(Rect2(x, pos.y, width * clampf(ratio, 0, 1), 2), col.lightened(0.4))


func _draw_boss_bar() -> void:
	var w := 300.0
	var x := 320.0 - w * 0.5
	var y := 340.0
	PixelFont.draw_centered(self, boss_name, Vector2(320, y - 10), 1, Color("ff8a6a"))
	draw_rect(Rect2(x - 2, y - 2, w + 4, 12), Color("1a1420"))
	draw_rect(Rect2(x, y, w, 8), Color("3a1010"))
	draw_rect(Rect2(x, y, w * boss_shown, 8), Color("ffb040"))
	draw_rect(Rect2(x, y, w * boss_ratio, 8), Color("e8302a"))
	draw_rect(Rect2(x, y, w * boss_ratio, 2), Color("ff8a6a"))
	for i in range(1, 10):
		draw_rect(Rect2(x + w * i / 10.0, y, 1, 8), Color(0, 0, 0, 0.4))


func _draw_title() -> void:
	draw_rect(Rect2(0, 0, 640, 360), Color(0.03, 0.04, 0.08, 0.55))
	var bounce := sin(t * 3.0) * 3.0
	PixelFont.draw_centered(self, "IRON TIDE", Vector2(320, 90 + bounce), 7, Color("ffb62e"))
	PixelFont.draw_centered(self, "COASTAL ASSAULT", Vector2(320, 140), 2, Color("f2641e"))
	if int(t * 2.0) % 2 == 0:
		PixelFont.draw_centered(self, "PRESS ENTER OR J TO START", Vector2(320, 190), 1, Color.WHITE)
	var mode := "ARCADE (1 HIT)" if not GameManager.debug_hp_mode else "DEBUG (HP BAR)"
	PixelFont.draw_centered(self, "MODE: " + mode + "   F1 TOGGLE", Vector2(320, 210), 1, Color("a0f0a0"))
	_draw_controls(250)


func _draw_controls(y: float) -> void:
	var lines := [
		"A/D MOVE   S CROUCH   SPACE JUMP   W/S AIM",
		"J / LMB FIRE   K / RMB GRENADE   E ENTER TANK",
		"TANK: J VULCAN  K CANNON  SPACE HOP  E EXIT",
		"F1 HP MODE  F2 GOD  F3 DEBUG  F11 FULLSCREEN",
	]
	for i in lines.size():
		PixelFont.draw_centered(self, lines[i], Vector2(320, y + i * 13), 1, Color("c0c0c0"))


func _draw_results() -> void:
	draw_rect(Rect2(0, 0, 640, 360), Color(0, 0, 0, 0.55))
	PixelFont.draw_centered(self, "MISSION COMPLETE!", Vector2(320, 80), 4, Color("fff27a"))
	var gm := GameManager
	var rows := [
		["SCORE", "%08d" % gm.score],
		["ENEMIES", str(gm.enemies_killed)],
		["POW RESCUED", str(gm.hostages_rescued)],
		["TIME", "%d:%02d" % [int(gm.play_time) / 60, int(gm.play_time) % 60]],
		["CONTINUES", str(gm.continues_used)],
	]
	for i in rows.size():
		PixelFont.draw_text(self, rows[i][0], Vector2(200, 140 + i * 18), 2, Color("c0c0c0"))
		PixelFont.draw_text(self, rows[i][1], Vector2(380, 140 + i * 18), 2, Color.WHITE)
	if int(t * 2.0) % 2 == 0:
		PixelFont.draw_centered(self, "PRESS ENTER", Vector2(320, 250), 2, Color("fff27a"))


func _draw_debug() -> void:
	var lines := [
		"FPS %d" % int(fps_smooth),
		"ENEMIES %d" % get_tree().get_nodes_in_group("enemies").size(),
		"BULLETS %d" % ObjectPool.active(Projectile.POOL_KEY),
		"PARTICLES %d" % ((FX.front.count() + FX.back.count()) if FX.ready() else 0),
		"TARGETS %d/%d/%d" % [Combat.count(0), Combat.count(1), Combat.count(2)],
		"X %d" % (int(GameManager.player.global_position.x) if GameManager.player else 0),
	]
	draw_rect(Rect2(4, 40, 120, lines.size() * 10 + 4), Color(0, 0, 0, 0.5))
	for i in lines.size():
		PixelFont.draw_text(self, lines[i], Vector2(8, 43 + i * 10), 1, Color("a0ffa0"))
