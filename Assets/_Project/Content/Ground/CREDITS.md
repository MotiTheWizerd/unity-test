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

Each folder keeps the colour, OpenGL normal, roughness, ambient occlusion and displacement maps. The mask map
Unity's terrain layers read (metallic, occlusion, height, smoothness) is packed from them by
Forge → Ground → Build Ground Layers, which also makes each folder's terrain layer and lists it in Content/GroundLayerCatalog.
