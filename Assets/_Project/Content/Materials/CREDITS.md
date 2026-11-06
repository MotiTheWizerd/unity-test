# Material textures

All from ambientCG (https://ambientcg.com), released under CC0 1.0 (public domain): free to use, change and
redistribute, no attribution required. Credited here anyway, so the source of each set is known.

| Material | ambientCG asset | Resolution |
|----------|-----------------|------------|
| OakPlanks | Planks037A | 1K JPG |
| WeatheredPlanks | Planks023A | 1K JPG |
| Thatch | ThatchedRoof001A | 1K JPG |
| Plaster | Plaster007 | 1K JPG |
| CutStone | Bricks066 | 1K JPG |
| RedBrick | Bricks085 | 1K JPG |
| RoofTiles | RoofingTiles012A | 1K JPG |
| DarkIron | Metal046B | 1K JPG |

Each folder keeps the colour, OpenGL normal, roughness and, where the set has them, ambient occlusion and metalness
maps. Forge → Materials → Build Materials packs the mask URP Lit reads (metallic, occlusion, smoothness) from them,
makes each folder's material and lists it in Content/MaterialCatalog.
