# Ball Brawl

A 3D soccer/sumo ball game where you knock enemy balls into their goal while defending your own.

## Gameplay

**Combat:**

- Knock enemies into the Enemy Goal by ramming into them
- Enemies get stunned on hit and briefly drift toward their own goal
- Each enemy scored = +1 point, each enemy that reaches your goal = -1 life

**Movement:**

- WASD movement relative to camera direction
- Mouse-controlled orbiting camera
- Jump with spacebar
- Turbo dash on shift with smoke trail

**Powerups:**

Five powerup types cycle between waves, all stackable:

- **Knockback:** Boosted push force on contact for a duration
- **Smash:** Launch into the air, slow-mo aim, dive-bomb with AOE knockback
- **Shield:** AOE barrier that shrinks and destroys nearby enemies on contact
- **Giant:** Grow massive and squish enemies flat on contact
- **Haunt:** Touched enemies aggressively home toward their own goal and spread haunt to other enemies on collision

**Enemy Types:**

Waves start with normal enemies, then introduce new types as difficulty scales:

- **Normal:** Steady march toward your goal
- **Aggressive** (wave 3+): High acceleration, rams the player when close
- **Evasive** (wave 5+): Slow crawl with periodic sideways dodges
- **Tank** (wave 7+): Big, heavy, and slow

## Controls

- **WASD:** Move
- **Mouse:** Camera orbit
- **Space:** Jump
- **Shift:** Turbo dash
- **F:** Smash attack (when charged)
- **Escape:** Pause

## Features

- Wave-based spawning with countdown between waves
- Weighted enemy type distribution that scales with wave number
- Powerup stacking with visual indicator clones
- 5-phase targeted smash aiming system with slow-mo
- Anime speed lines that intensify at high speed
- Powerup color overlay shader with animated noise blending
- Pixelation post-processing with adjustable pixel size
- Skin selection with persistence across sessions
- Main menu with physics-based smash navigation
- Pause and game over screens with score/wave tracking

## Credits

Built for COSC 495 Introduction to Game Development, based on Unity's Create with Code "Challenge 4" template.
Modified and extended with custom powerup, combat, enemy AI, and camera systems.
