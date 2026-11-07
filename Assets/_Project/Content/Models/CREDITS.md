# Models

Each folder is one model, built into its material and prefab by *Forge → Models → Build Models*.

| Model | Source | Made by |
|-------|--------|---------|
| VikingSword | `blender-tests/viking_game.blend` (scene `viking_game`), exported as `game_viking.py` exports `export/viking/Viking.fbx` but with the textures beside it rather than packed inside; `export/viking/Viking_BaseColor.png`, `Viking_MetallicSmoothness.png` and `Viking_Normal.png` | Moti and studio-raven: a sword built in Blender by script, its guard and pommel modelled by Hunyuan 3D from reference crops, everything baked onto one low mesh. LOD0/1/2 of 8,781 / 4,389 / 1,755 triangles, three convex colliders, 4K textures (imported at 2K). |
| Greatsword | [Heavy Greatsword](https://opengameart.org/content/heavy-greatsword) (OpenGameArt, CC0) | Lucian Pavel. Re-exported in Blender: scaled to 1.75 m, the grip on the pivot, three convex colliders (grip, guard, blade); 2,338 triangles, 2K textures with ambient occlusion. |
| RoundShield, RoundShieldPainted | [Round Shield 1](https://opengameart.org/content/round-shield-1) (OpenGameArt, CC0), its two texture sets (plain wood; yellow and black) | Lucian Pavel. Re-exported in Blender: scaled to 0.8 m across, the pivot on the hand grip behind the board, one convex collider; 800 triangles, 1K textures with ambient occlusion. |
| KiteShield, KiteShieldIron | [Kite Shield 1](https://opengameart.org/content/kite-shield-1) (OpenGameArt, CC0), its two texture sets (wood with a cross; engraved iron) | Lucian Pavel. Re-exported in Blender: scaled to 0.95 m tall, the pivot on the hand grip, one convex collider; 504 triangles, 1K textures with ambient occlusion. |
| MageStaff | [Stylized Wizard Staff](https://opengameart.org/content/stylized-wizard-staff) (OpenGameArt, CC0) | TCCDev. Re-exported in Blender from its glTF: scaled to 1.8 m, the grip on the pivot, two convex colliders (shaft, head); 636 triangles, 1K textures (glTF metal/roughness repacked as metal/smoothness). |

CC0 asks for no credit; it is given anyway. The source files stay in the `blender-tests` art repo; only the finished model and textures are copied here.
