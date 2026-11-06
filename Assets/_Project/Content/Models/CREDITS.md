# Models

Each folder is one model, built into its material and prefab by *Forge → Models → Build Models*.

| Model | Source | Made by |
|-------|--------|---------|
| VikingSword | `blender-tests/viking_game.blend` (scene `viking_game`), exported as `game_viking.py` exports `export/viking/Viking.fbx` but with the textures beside it rather than packed inside; `export/viking/Viking_BaseColor.png`, `Viking_MetallicSmoothness.png` and `Viking_Normal.png` | Moti and studio-raven: a sword built in Blender by script, its guard and pommel modelled by Hunyuan 3D from reference crops, everything baked onto one low mesh. LOD0/1/2 of 8,781 / 4,389 / 1,755 triangles, three convex colliders, 4K textures (imported at 2K). |

The source files stay in the `blender-tests` art repo; only the finished model and textures are copied here.
