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
| GrassyCobblestone | [grassy_cobblestone](https://polyhaven.com/a/grassy_cobblestone) | Poly Haven | 2 m (the street variants: 2K maps scaled to 1K, occlusion from the AO map) |
| WornCobblestone | [cobblestone_floor_08](https://polyhaven.com/a/cobblestone_floor_08) | Poly Haven | 2 m |
| GrassPath | [grass_path_3](https://polyhaven.com/a/grass_path_3) | Poly Haven | 1 m |
| StonyMud | [brown_mud_rocks_01](https://polyhaven.com/a/brown_mud_rocks_01) | Poly Haven | 1.3 m |
| DirtRoad | [rocky_trail](https://polyhaven.com/a/rocky_trail) | Amal Kumar | 2 m (2K) |
| ForestTrail | [forest_ground_04](https://polyhaven.com/a/forest_ground_04) | Rob Tuytel, Rico Cilliers | 3.15 m (2K) |
| MountainRoad | [rocks_ground_01](https://polyhaven.com/a/rocks_ground_01) | Rob Tuytel | 2 m (2K) |
| SnowyRoad | [snow_03](https://polyhaven.com/a/snow_03) | Rob Tuytel | 2 m (2K) |
| StoneRoad | [grey_stone_path](https://polyhaven.com/a/grey_stone_path) | Poly Haven | 2.7 m, not its real 1.8 m: Skyrim-sized stones read as a road, real ones as pavement (2K maps, imported at 2K: `"textureSize": 2048`) |

Each folder keeps the colour, OpenGL normal, roughness, ambient occlusion and displacement maps. The mask map
Unity's terrain layers read (metallic, occlusion, height, smoothness) is packed from them by
Forge → Ground → Build Ground Layers, which also makes each folder's terrain layer and lists it in Content/GroundLayerCatalog.
