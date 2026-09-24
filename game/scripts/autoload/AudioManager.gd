extends Node
## Synthesized retro sound effects and music.
##
## All sounds are generated into AudioStreamWAV buffers at startup so the demo
## needs no audio files. Replace entries in `_streams` with loaded .wav/.ogg to
## upgrade later. Rapid-fire sounds are throttled per name.

const RATE := 22050
const VOICES := 24

var _streams: Dictionary = {}
var _players: Array[AudioStreamPlayer] = []
var _last_play: Dictionary = {}
var _next := 0
var _music: AudioStreamPlayer
var _music_name := ""
var _loops: Dictionary = {}   # name -> AudioStreamPlayer for looping sfx
var sfx_volume_db := -6.0
var music_volume_db := -12.0

const THROTTLE := {
	"mg": 0.045, "pistol": 0.05, "flame": 0.09, "hit": 0.035, "tink": 0.06,
	"enemy_shot": 0.06, "explode": 0.05, "scream": 0.08, "shell": 0.05,
}


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	for i in VOICES:
		var p := AudioStreamPlayer.new()
		p.bus = "Master"
		add_child(p)
		_players.append(p)
	_music = AudioStreamPlayer.new()
	_music.volume_db = music_volume_db
	add_child(_music)
	_build_sfx()


func _exit_tree() -> void:
	# release generated streams so nothing is left alive at shutdown
	for p in _players:
		p.stop()
		p.stream = null
	for k in _loops:
		_loops[k].stop()
		_loops[k].stream = null
	_music.stop()
	_music.stream = null
	_streams.clear()


func play(name: String, pitch_var: float = 0.05, vol_db: float = 0.0) -> void:
	if not _streams.has(name):
		return
	var now := Time.get_ticks_msec() / 1000.0
	var th: float = THROTTLE.get(name, 0.0)
	if th > 0.0 and now - _last_play.get(name, -10.0) < th:
		return
	_last_play[name] = now
	var p := _players[_next]
	_next = (_next + 1) % VOICES
	p.stream = _streams[name]
	p.pitch_scale = 1.0 + randf_range(-pitch_var, pitch_var)
	p.volume_db = sfx_volume_db + vol_db
	p.play()


## Looping sound that stays on while `on` is true (helicopter rotor, engine).
func set_loop(name: String, on: bool, vol_db: float = 0.0) -> void:
	if on:
		if not _loops.has(name):
			var p := AudioStreamPlayer.new()
			p.stream = _streams.get(name)
			add_child(p)
			_loops[name] = p
		var lp: AudioStreamPlayer = _loops[name]
		lp.volume_db = sfx_volume_db + vol_db
		if not lp.playing:
			lp.play()
	elif _loops.has(name):
		_loops[name].stop()


func stop_all_loops() -> void:
	for k in _loops:
		_loops[k].stop()


func play_music(name: String) -> void:
	if _music_name == name and _music.playing:
		return
	_music_name = name
	if not _streams.has("music_" + name):
		_streams["music_" + name] = _make_music(name)
	_music.stream = _streams["music_" + name]
	_music.volume_db = music_volume_db
	_music.play()


func stop_music() -> void:
	_music.stop()
	_music_name = ""


# =================================================================== synthesis
func _to_stream(samples: PackedFloat32Array, loop: bool = false) -> AudioStreamWAV:
	var bytes := PackedByteArray()
	bytes.resize(samples.size() * 2)
	for i in samples.size():
		bytes.encode_s16(i * 2, int(clampf(samples[i], -1.0, 1.0) * 32000.0))
	var s := AudioStreamWAV.new()
	s.format = AudioStreamWAV.FORMAT_16_BITS
	s.mix_rate = RATE
	s.stereo = false
	s.data = bytes
	if loop:
		s.loop_mode = AudioStreamWAV.LOOP_FORWARD
		s.loop_begin = 0
		s.loop_end = samples.size()
	return s


func _buf(seconds: float) -> PackedFloat32Array:
	var b := PackedFloat32Array()
	b.resize(int(seconds * RATE))
	return b


## Noise burst with exponential decay and a one-pole lowpass sweep.
func _noise(dur: float, decay: float, lp_start: float, lp_end: float, vol: float = 0.8) -> PackedFloat32Array:
	var b := _buf(dur)
	var y := 0.0
	var n := b.size()
	for i in n:
		var t := float(i) / n
		var a := lerpf(lp_start, lp_end, t)
		y += a * (randf_range(-1, 1) - y)
		b[i] = y * vol * exp(-t * decay)
	return b


## Oscillator with linear pitch sweep. wave: 0 square, 1 sine, 2 triangle, 3 saw
func _tone(dur: float, f0: float, f1: float, wave: int, decay: float, vol: float = 0.5, vibrato: float = 0.0) -> PackedFloat32Array:
	var b := _buf(dur)
	var ph := 0.0
	var n := b.size()
	for i in n:
		var t := float(i) / n
		var f := lerpf(f0, f1, t) * (1.0 + vibrato * sin(t * dur * TAU * 9.0))
		ph += f / RATE
		var v := 0.0
		match wave:
			0:
				v = 1.0 if fmod(ph, 1.0) < 0.5 else -1.0
			1:
				v = sin(ph * TAU)
			2:
				v = 1.0 - 4.0 * absf(fmod(ph, 1.0) - 0.5)
			3:
				v = fmod(ph, 1.0) * 2.0 - 1.0
		b[i] = v * vol * exp(-t * decay)
	return b


func _mix(a: PackedFloat32Array, b: PackedFloat32Array, offset: float = 0.0) -> PackedFloat32Array:
	var off := int(offset * RATE)
	var n := maxi(a.size(), b.size() + off)
	var out := PackedFloat32Array()
	out.resize(n)
	for i in a.size():
		out[i] = a[i]
	for i in b.size():
		out[i + off] += b[i]
	return out


func _seq(notes: Array, note_len: float, wave: int, vol: float) -> PackedFloat32Array:
	var out := PackedFloat32Array()
	for f in notes:
		out.append_array(_tone(note_len, f, f, wave, 2.0, vol))
	return out


func _build_sfx() -> void:
	_streams["pistol"] = _to_stream(_mix(_noise(0.09, 9.0, 0.7, 0.2, 0.7), _tone(0.07, 900, 220, 0, 6.0, 0.25)))
	_streams["mg"] = _to_stream(_mix(_noise(0.08, 8.0, 0.8, 0.25, 0.8), _tone(0.06, 300, 90, 0, 5.0, 0.3)))
	_streams["shotgun"] = _to_stream(_mix(_noise(0.35, 7.0, 0.6, 0.05, 1.0), _tone(0.2, 180, 50, 1, 5.0, 0.6)))
	_streams["rocket"] = _to_stream(_mix(_noise(0.45, 3.0, 0.1, 0.5, 0.6), _tone(0.3, 200, 500, 3, 4.0, 0.15)))
	_streams["flame"] = _to_stream(_noise(0.14, 3.0, 0.15, 0.3, 0.45))
	_streams["explode"] = _to_stream(_mix(_noise(0.6, 5.0, 0.5, 0.03, 1.0), _tone(0.4, 90, 35, 1, 4.0, 0.7)))
	_streams["explode_big"] = _to_stream(_mix(_noise(1.3, 3.5, 0.45, 0.02, 1.0), _tone(0.9, 70, 25, 1, 3.0, 0.9)))
	_streams["hit"] = _to_stream(_tone(0.04, 1400, 700, 0, 6.0, 0.18))
	_streams["tink"] = _to_stream(_tone(0.18, 2600, 1900, 1, 7.0, 0.35))
	_streams["scream"] = _to_stream(_tone(0.45, 620, 220, 0, 2.0, 0.22, 0.05))
	_streams["player_die"] = _to_stream(_seq([660, 520, 400, 300, 200], 0.08, 0, 0.3))
	_streams["pickup"] = _to_stream(_seq([520, 660, 880, 1040], 0.05, 0, 0.25))
	_streams["weapon"] = _to_stream(_mix(_seq([392, 523, 659, 784, 1046], 0.06, 0, 0.25), _tone(0.3, 200, 1200, 2, 2.0, 0.2)))
	_streams["thanks"] = _to_stream(_seq([784, 988, 1175, 1568], 0.07, 2, 0.35))
	_streams["jump"] = _to_stream(_tone(0.1, 250, 520, 0, 4.0, 0.12))
	_streams["land"] = _to_stream(_noise(0.08, 8.0, 0.2, 0.05, 0.4))
	_streams["throw"] = _to_stream(_noise(0.15, 4.0, 0.05, 0.4, 0.35))
	_streams["cannon"] = _to_stream(_mix(_noise(0.5, 5.0, 0.5, 0.03, 1.0), _tone(0.3, 120, 40, 1, 4.0, 0.9)))
	_streams["enemy_shot"] = _to_stream(_mix(_noise(0.07, 9.0, 0.5, 0.2, 0.5), _tone(0.06, 500, 160, 0, 6.0, 0.18)))
	_streams["knife"] = _to_stream(_noise(0.12, 5.0, 0.6, 0.9, 0.5))
	_streams["warning"] = _to_stream(_mix(_tone(0.35, 880, 880, 0, 0.5, 0.25), _tone(0.35, 660, 660, 0, 0.5, 0.25), 0.35))
	_streams["oneup"] = _to_stream(_seq([659, 784, 1318, 1046, 1175, 1568], 0.07, 0, 0.3))
	_streams["clear"] = _to_stream(_seq([523, 523, 523, 523, 415, 466, 523, 466, 523], 0.12, 0, 0.3))
	_streams["shell"] = _to_stream(_tone(0.05, 3200, 2800, 1, 8.0, 0.08))
	_streams["alert"] = _to_stream(_tone(0.12, 900, 1300, 0, 3.0, 0.15))
	_streams["crate"] = _to_stream(_mix(_noise(0.2, 6.0, 0.3, 0.1, 0.8), _tone(0.12, 180, 90, 2, 5.0, 0.4)))
	_streams["roar"] = _to_stream(_mix(_noise(0.9, 2.0, 0.08, 0.02, 0.9), _tone(0.9, 80, 50, 3, 1.5, 0.5, 0.2)))
	_streams["missile"] = _to_stream(_noise(0.35, 3.0, 0.3, 0.6, 0.45))
	# looping ambience
	var heli := PackedFloat32Array()
	for k in 8:
		heli.append_array(_mix(_noise(0.06, 6.0, 0.2, 0.05, 0.7), _buf(0.02)))
	_streams["heli"] = _to_stream(heli, true)
	var eng := _tone(0.5, 55, 55, 3, 0.0, 0.25, 0.08)
	_streams["engine"] = _to_stream(eng, true)


# =================================================================== music
func _note(n: int) -> float:
	return 440.0 * pow(2.0, (n - 69) / 12.0)


func _make_music(name: String) -> AudioStreamWAV:
	var bpm := 150.0 if name == "stage" else 170.0
	var step := 60.0 / bpm / 4.0          # 16th note
	var steps := 64 * 2                    # 8 bars
	var total := int(steps * step * RATE)
	var out := PackedFloat32Array()
	out.resize(total)
	# chord roots per bar (MIDI)
	var roots: Array = [45, 45, 41, 43, 45, 45, 48, 43] if name == "stage" else [40, 40, 41, 41, 40, 40, 43, 42]
	var lead: Array = [0, 3, 7, 10, 7, 3, 12, 10, 7, 3, 5, 7, 3, 0, -2, 0]
	var ph_bass := 0.0
	var ph_lead := 0.0
	var y := 0.0
	var seed_state := 12345
	for i in total:
		var t := float(i) / RATE
		var s := int(t / step)
		var st := fmod(t, step) / step          # 0..1 within 16th
		var bar := (s / 16) % roots.size()
		var root: int = roots[bar]
		var v := 0.0
		# bass: driving 8ths with octave jumps
		var bn := root - 12 + (12 if (s % 4) == 2 else 0)
		ph_bass += _note(bn) / RATE
		var bass_env := exp(-st * 2.0) if s % 2 == 0 else exp(-(st + 1.0) * 2.0)
		v += (1.0 if fmod(ph_bass, 1.0) < 0.5 else -1.0) * 0.16 * bass_env
		# lead arpeggio (triangle), silent on some steps for groove
		var li: int = lead[s % 16]
		var ln := root + 12 + li
		if name == "boss":
			ln += 12 if (s / 32) % 2 == 1 else 0
		ph_lead += _note(ln) / RATE
		var lv := 1.0 - 4.0 * absf(fmod(ph_lead, 1.0) - 0.5)
		if (s % 16) not in [3, 11]:
			v += lv * 0.11 * exp(-st * 1.5)
		# drums
		var in_beat := s % 4
		if in_beat == 0:
			var kt := st * step
			v += sin(TAU * (120.0 - 300.0 * kt) * kt) * 0.5 * exp(-kt * 25.0)
		seed_state = (seed_state * 1103515245 + 12345) & 0x7fffffff
		var nz := (seed_state / 1073741823.5) - 1.0
		if (s % 8) == 4:
			y += 0.6 * (nz - y)
			v += y * 0.35 * exp(-st * 4.0)
		if s % 2 == 1:
			v += nz * 0.05 * exp(-st * 12.0)
		out[i] = v
	return _to_stream(out, true)
