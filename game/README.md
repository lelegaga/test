# Iron Tide: Coastal Assault

A fast, explosive 2D side-scrolling **run-and-gun** arcade shooter demo for
**Godot 4.3+**, written entirely in GDScript. It needs no external assets:
all pixel art, the bitmap font, sound effects and music are generated
procedurally at startup, so the project runs straight from a clean checkout.

The game plays like a classic arcade run-and-gun: you die in one hit, enemies
come in large numbers, and most things on screen can be blown up.

## Running

1. Install [Godot 4.3](https://godotengine.org/download) (standard build, not .NET).
2. Open `game/project.godot` in the editor and press **F5**, or run:

   ```bash
   godot --path game
   ```

The internal resolution is 640×360. The window scales it up by whole numbers
(integer scaling), so pixels stay sharp up to 4K (6×). Press **F11** for
fullscreen.

## Controls

| Action | Keyboard / Mouse | Gamepad |
| --- | --- | --- |
| Move | A / D | D-pad / left stick |
| Crouch | S | Down |
| Aim up / diagonal | W (+ direction) | Up |
| Aim down (in the air) | S | Down |
| Jump | Space | A |
| Drop through a platform | S + Space | Down + A |
| Fire | J / left mouse button (mouse aims in 8 directions) | X |
| Grenade | K / right mouse button | B |
| Enter or exit the tank | E | Y |
| Pause | Esc / P | Start |

**In the tank:** A/D move, W aims the vulcan cannon, J fires the vulcan,
K fires the main cannon, Space hops, E exits.

**Debug keys:** F1 toggles HP mode (a health bar instead of one-hit deaths),
F2 toggles god mode, F3 shows FPS, entity and pool counters.

## Mission 1 walkthrough

Beach landing → enemy outpost → jungle → wooden bridge → military base →
tank battle on the airfield → boss.

- **Beach.** The first supply crate holds the heavy machine gun. Troops gather
  next to oil barrels, so shoot a barrel to set off a chain explosion.
- **Outpost.** Knock down the watchtower to drop its guards, and set off the
  fuel drums under the squad. A gunship locks the screen until you shoot it down.
- **Jungle.** Chargers leap out of the bushes. Climb the branches to reach a
  hidden spot in the treetops with a prisoner who gives an extra life, a
  flamethrower and a medal.
- **Bridge ambush.** A squad attacks from behind, a machine-gun post opens up
  ahead and a gunship arrives. Then the bridge starts exploding behind you,
  so keep running.
- **Base.** Your SV-1 tank is parked at the entrance. Blast through the gate
  wall with explosives, then hold off waves of infantry, turrets and an
  armored car.
- **Airfield.** Two locked tank battles against armored cars, gunships and
  mixed infantry.
- **Boss: Iron Behemoth.** A huge mech tank with three phases:
  1. Machine-gun sweeps and aimed cannon shells, with laser-sight warnings.
  2. Its armor plate blows off to reveal a missile pod. Missiles fall on
     red target markers on the ground.
  3. Berserk mode: every attack gets faster, plus an artillery barrage across
     the arena and a ramming charge with a flashing "!!" warning.

  The reactor core on its front armor is the weak point. It opens and glows
  for a few seconds after each big attack. When it dies, parts of it explode
  one after another, the screen shakes, and it ends in one huge explosion.

Rescued prisoners run over, salute, and drop a random reward: a weapon, ammo,
grenades, points or an extra life.

## Architecture

```
game/
├── project.godot            autoloads, 640x360 integer-scaled viewport
├── scenes/Main.tscn         single entry scene (everything else is built in code)
└── scripts/
    ├── autoload/
    │   ├── GameManager.gd        state, score, lives, input map, debug modes
    │   ├── Combat.gd             hit registry + segment/rect/explosion queries
    │   ├── ObjectPool.gd         generic node pool (projectiles, explosions, popups)
    │   ├── CameraManager.gd      follow, no-backtracking, arena locks
    │   ├── CameraShakeManager.gd trauma-based shake + hit-stop
    │   ├── FX.gd                 muzzle flash, shells, sparks, smoke, debris, booms
    │   ├── AudioManager.gd       synthesized SFX + chiptune music
    │   └── Art.gd                procedural placeholder pixel art
    ├── core/        Main, Level (builder helpers), PixelCanvas, AutoTest bot
    ├── player/      Player.gd
    ├── weapons/     WeaponBase, Pistol, MachineGun, Shotgun, RocketLauncher,
    │                FlameThrower, WeaponFactory
    ├── projectiles/ Projectile.gd (all bullet kinds, grenades, rockets, missiles)
    ├── enemies/     EnemyBase, Soldier, ChargerSoldier, RocketSoldier,
    │                MachineGunner, ShieldSoldier, Turret, ArmoredCar, Helicopter
    ├── vehicles/    VehicleBase, Tank
    ├── boss/        BossBase, BossPart, MechTankBoss
    ├── world/       DestructibleObject, SpawnTrigger, Pickup, Hostage, Bridge,
    │                SecretArea, TerrainBlock, WaterZone, Background
    ├── fx/          ParticleLayer, Explosion, TextPopup
    ├── ui/          HUD, PixelFont
    └── levels/      Level1.gd (the whole mission layout + scripted events)
```

Key design points:

- **Combat without hundreds of Area2Ds.** Everything that can be damaged
  registers with `Combat` under a team (PLAYER / ENEMY / NEUTRAL).
  Each frame a projectile checks the line it just travelled against the
  opposing team's hurt boxes, and raycasts against solid geometry.
  Explosions apply radial damage with falloff. As a result, oil barrels can
  set off other barrels, and the player can shoot down enemy rockets.
- **Pooling.** Projectiles, explosions and popups are pooled nodes. Shells,
  sparks, smoke, debris and flames live in one `ParticleLayer` node that
  stores particles in fixed-size arrays and draws them itself, with no
  per-particle nodes.
- **Encounters.** A `SpawnTrigger` runs a script of actions (`spawn`, `wait`,
  `wait_clear`, `message`, `shake`, `sfx`, `music`, `call`) when the player
  reaches its x position, optionally locking the camera until the wave is
  dead. Enemies can walk in, parachute in or leap in.
- **Extending vehicles.** Subclass `VehicleBase` and implement
  `_build_visual()`, `_drive()` and `_animate()`. Boarding and exiting, HP,
  damage scaling, ejection, the camera handoff and the boarding prompt are
  already handled. A `Mech` or a player `Helicopter` would follow the same
  pattern as `Tank`.
- **Adding enemies.** Subclass `EnemyBase`, set stats in `_init()`, write the
  AI in `_think()`, then add the script to `Level.ENEMY_SCRIPTS`.
- **Replacing art.** `Art.tex()`, `Art.frames()` and `Art.gun()` are the only
  places sprites come from, so real sprite sheets can replace the generated
  art without touching gameplay code.

## Automated smoke test

`scripts/core/AutoTest.gd` contains a bot that plays the whole mission:
it runs, shoots, jumps pits, throws grenades, boards the tank and fights the
boss. It exits after reporting the result.

```bash
# play the whole mission, faster than real time, in god mode
godot --headless --fixed-fps 60 --path game -- --autotest --god

# performance stress scene: 40+ enemies, gunships, constant fire and explosions
godot --headless --fixed-fps 60 --path game -- --autotest --stress --god
```
