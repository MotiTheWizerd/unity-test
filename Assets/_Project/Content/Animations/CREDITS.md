# Animations

Each top folder is a move set; each file in it is one move, named after it (a name of its own in the set), in the
folder that says what it is:

```
Humanoid/
  Locomotion/        Idle Walk Run Sprint, and with weapons drawn DrawnIdle DrawnWalk DrawnRun DrawnSprint,
                     crouched CrouchIdle CrouchWalk CrouchRun CrouchSprint, crouched with weapons drawn
                     DrawnCrouchIdle DrawnCrouchWalk ..., CrouchDown StandUp between, and a sidestep to the right
                     Strafe / DrawnStrafe (mirrored for the left, sped up for the run; with one, the drawn moves
                     blend by the way the body goes: a fighter facing its foe circles it and backs away)
  Air/               Jump Fall Land, and with weapons drawn DrawnJump
  Styles/<Style>/    a combat style's moves: its stance <Style>Idle, its draw and sheathe (on the arms), its blows,
                     its deaths (a name with Death in it: the whole body, stayed at its end)
  <Other>/           more actions (Interact, Emotes, ...)
  <Move>.json        beside a clip, optional: how the move holds ({ "rightHand": { "points": [0, 0, 1] } }: what the
                     right hand holds points straight ahead; x right, y up, z forward); the hand is turned the least
                     that does it, on top of the clip, since Mixamo animates with nothing in the hand
  _Built/            made by the builder: the controller and its masks; don't put clips here
_Reference/
  TPose.fbx          the skeleton every clip is read against: a Mixamo-rigged character downloaded from Mixamo
                     standing in its T-pose (Characters → the character → Download, Pose "T-pose"); not a move
```

`Humanoid/` is the base set, played by every character unless its `<Name>.json` names another;
*Forge → Characters → Build Characters* builds each set's controller. A folder beside the sets whose name starts
with `_` is not a set.

| Move | Source | Made by |
|------|--------|---------|
| Idle | Mixamo "Neutral Idle" | Adobe Mixamo |
| Walk | Mixamo "Walking" | Adobe Mixamo |
| Run | Mixamo "Standard Run" | Adobe Mixamo |
| Sprint | Mixamo "Fast Run" | Adobe Mixamo |
| Jump | Mixamo "Running Jump" | Adobe Mixamo |
| Draw | Mixamo "Withdrawing Sword" | Adobe Mixamo |
| Sheathe | Mixamo "Sheathing Sword" | Adobe Mixamo |
| DrawnRun | Mixamo "Sword And Shield Run" | Adobe Mixamo |
| DrawnJump | Mixamo "Sword And Shield Jump" | Adobe Mixamo |
| DrawnIdle | Mixamo "Sword And Shield Idle" | Adobe Mixamo |
| DrawnStrafe | Mixamo "Sword And Shield Strafe" (steps right; the left is its mirror) | Adobe Mixamo |
| SwordSlash | Mixamo "Sword And Shield Slash" | Adobe Mixamo |
| BowDraw | Mixamo "Standing Equip Bow" | Adobe Mixamo |
| BowSheathe | Mixamo "Standing Disarm Bow" | Adobe Mixamo |
| BowIdle | Mixamo "Standing Idle 01" (longbow) | Adobe Mixamo |
| BowShot | Mixamo "Standing Draw Arrow" | Adobe Mixamo |
| BowRelease | Mixamo "Standing Aim Recoil" | Adobe Mixamo |
| ShieldBlock | Mixamo "Sword And Shield Block" | Adobe Mixamo |
| ShieldImpact | Mixamo "Sword And Shield Impact" | Adobe Mixamo |
| CrouchIdle | Mixamo "Crouching Idle" | Adobe Mixamo |
| CrouchWalk | Mixamo "Crouched Walking" | Adobe Mixamo |
| DrawnCrouchIdle | Mixamo "Sword And Shield Crouch Idle" | Adobe Mixamo |
| CrouchDown | Mixamo "Standing To Crouch" | Adobe Mixamo |
| StandUp | Mixamo "Crouched To Standing" | Adobe Mixamo |
| SwordDeath, SwordDeath2 | Mixamo sword-and-shield deaths (Moti's picks, 2026-11-11; exact names to fill in) | Adobe Mixamo |
| BowDeath, BowDeath2 | Mixamo bow deaths (Moti's picks, 2026-11-11; exact names to fill in) | Adobe Mixamo |
| _Reference/TPose | VikingM1 as Mixamo rigged it, downloaded in Mixamo's T-pose with skin (the skeleton the clips are read against, not a move) | Moti and studio-raven's VikingM1, rigged and posed by Adobe Mixamo |

Downloaded by Moti from mixamo.com as FBX for Unity, 30 fps, without skin, travel left in (not "in place"). Mixamo
animations are free to use in games under Adobe's terms.
