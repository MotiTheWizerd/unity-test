# Characters

Each folder is one character, built into its material and prefab by *Forge → Characters → Build Characters*.

| Character | Source | Made by |
|-----------|--------|---------|
| VikingHero | `blender-tests/characters/viking_hero/anim/dancing.fbx` (the rigged model) and `viking_hero_basecolor.png` | Moti and studio-raven: an original face from a text prompt (Qwen Image), modelled by Hunyuan 3D, rigged by Adobe Mixamo. 1.85 m, about 50k triangles, one 4K colour texture. |
| VikingM1 (the player) | `blender-tests/ai_models/viking_m_1/` (the Tripo source and Mixamo download are private, kept out of git) | Moti and studio-raven: modelled by Tripo on Moti's Pro plan (8K colour), reduced and normal-baked in Blender (`ai_models/reduce.py`, `mixamo.py`), rigged by Adobe Mixamo. 1.85 m, 50k triangles, colour and normal at 4096. |
| VikingF1 (an NPC) | `blender-tests/ai_models/viking_f_1/` (the same) | Moti and studio-raven: modelled by Tripo on Moti's Pro plan, reduced and normal-baked in Blender, rigged by Adobe Mixamo. 1.70 m, 40k triangles, colour and normal at 2048. |
| ElfF1 (an NPC) | `blender-tests/ai_models/elf_f_1/` (the same) | Moti and studio-raven: modelled by Tripo on Moti's Pro plan, reduced and normal-baked in Blender, rigged by Adobe Mixamo. 1.75 m, 40k triangles, colour and normal at 2048. |

The source files stay in the `blender-tests` art repo; only the finished model and texture are copied here.
