# Ground textures

All from ambientCG (https://ambientcg.com), released under CC0 1.0 (public domain): free to use, change and
redistribute, no attribution required. Credited here anyway, so the source of each set is known.

| Layer | ambientCG asset | Resolution |
|-------|-----------------|------------|
| Grass | Grass004        | 1K JPG     |
| Dirt  | Ground048       | 1K JPG     |
| Rock  | Rock030         | 1K JPG     |
| Sand  | Ground054       | 1K JPG     |
| Snow  | Snow010A        | 1K JPG     |
| Gravel | Gravel040      | 1K JPG     |
| Mud   | Ground036       | 1K JPG     |
| ForestFloor | Ground023 | 1K JPG     |
| Moss  | Moss002         | 1K JPG     |
| DryGrass | Ground028    | 1K JPG     |

The town grounds are from Poly Haven (https://polyhaven.com), also CC0, renamed to the same naming; their real size
sets their repeat (`<Name>.json`, `tileSize` in meters), and their occlusion is the red of Poly Haven's ARM map.

| Layer | Poly Haven texture | Author | Repeat |
|-------|--------------------|--------|--------|
| Cobblestone | [cobblestone_floor_001](https://polyhaven.com/a/cobblestone_floor_001) | Rob Tuytel | 2.4 m (no occlusion map: its ARM has none) |
| Flagstone | [monastery_stone_floor](https://polyhaven.com/a/monastery_stone_floor) | Amal Kumar | 1.8 m |
| PackedEarth | [dry_mud_field_001](https://polyhaven.com/a/dry_mud_field_001) | Rob Tuytel, Rico Cilliers | 3 m |
| WetMud | [muddy_tracks](https://polyhaven.com/a/muddy_tracks) | Amal Kumar | 2.25 m |

Each folder keeps the colour, OpenGL normal, roughness, ambient occlusion and displacement maps. The mask map
Unity's terrain layers read (metallic, occlusion, height, smoothness) is packed from them by
Forge → Ground → Build Ground Layers, which also makes each folder's terrain layer and lists it in Content/GroundLayerCatalog.
