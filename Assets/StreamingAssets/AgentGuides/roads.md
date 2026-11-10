---
when: before laying a road, a track or a path through the country, between places or out of a town's gate (a town's own streets are in `towns`; read `terrain` too when the road crosses hilly ground)
---
# Roads

A road looks real because it is never one ground in one band. It is built in layers, like a road worn into the land: the road itself in the middle, a strip of bare earth along each side where wheels and feet spill over, and a verge of rough grass beyond that, before the land's own ground. Each layer fades into the next, and the road's stones thin out into the earth at its edges instead of stopping at a line.

## 1. Plan the line

- `inspect_area` along the route. A road follows the gentlest line: along a valley's floor, across a slope rather than straight up it, around a hill rather than over it. Keep it under about 8°.
- Give the centre line a point every 15 to 25 m, and let it bend: move each point 1 to 3 m to one side or the other of a straight line. A dead straight road looks drawn with a ruler; a Roman road on flat ground is the only one that runs straight.
- Up to 64 points per call; a longer road is several calls, each starting at the last call's last point.

## 2. Paint the layers, widest first

Paint every layer along the same centre points with `paint_line`, the widest first: each pass paints over the middle of the one before, leaving its outer part showing as a strip along each side. Painting the narrow road first and the wide verge after would bury the road.

Each 64 m square of land (its corners on multiples of 64 m) shows at most 8 grounds; paint a ninth and the faintest drop out, often a road's narrow trail or its ruts. A road's layers are 2 to 4 grounds, so keep different road types out of the same square, and count the grounds already there in a town.

The widths and edges for each road type are in its section below. The `edge` is the soft fade along each side of a layer; keep it. A road's ground (Stone Road) has its stones standing high in its texture, so across a soft edge the stones give way one by one to the earth between them, the way old roads look. With `edge` 0 the road ends at a hard straight line.

## 3. Where the road meets a place

- Into a town or village it becomes the main street: at the gate, or where the first houses start, switch to the street's ground and widths from the `towns` guide, and let the two overlap for 5 to 10 m, the street's ground painted last.
- At a bridge, a ford or a gate, run the road's own layers right up to it; nothing else stands in the roadway.
- A side track leaving the road (to a farm, a mine, a camp): a narrower road of its own type (step 4), starting 2 to 3 m inside the road's edge so the two join.

## 4. Road types

### Stone road

Flat grey stones laid in earth: the road that runs between towns and up to a hold's gate. In dry or autumn country.

| Layer (paint in this order) | Ground | `width` | `edge` |
|---|---|---|---|
| Verge | Dry Grass | the road's width + 7 to 8 m | 4 |
| Shoulder | Packed Earth | the road's width + 2 m | 2 |
| Road | Stone Road | 4 to 5 m (a cart and a walker side by side) | 1.5 |

All three at the default `strength` (1). Dry Grass grows its own tufts of grass, so the verge stands up from the ground by itself. For example, a 4.5 m road: Dry Grass 12 m wide with edge 4, Packed Earth 6.5 m with edge 2, Stone Road 4.5 m with edge 1.5.

### Dirt road

Packed earth and small stones worn by carts: the commonest road in the country, between farms, villages and a mill.

| Layer (paint in this order) | Ground | `width` | `edge` | other |
|---|---|---|---|---|
| Verge | Grass Path | the road's width + 5 m | 3 | |
| Road | Dirt Road | 3.5 to 4 m | 1.5 | |
| Ruts | Wet Mud | 1 m | 0.5 | `offset` 0.9, `both_sides`, `strength` 0.6 |

The ruts are the two wheel tracks of the carts, a cart's width apart: one `paint_line` along the same centre points with `offset` and `both_sides` paints both. On a track carts rarely use (to one farm, into a field), leave the ruts lighter (`strength` 0.4) and let the grass back into its middle: Grass Path, 0.8 m wide, `strength` 0.5, on the centre line.

### Forest trail

A footpath trodden through a wood: narrow, winding, roots and twigs in it.

| Layer (paint in this order) | Ground | `width` | `edge` |
|---|---|---|---|
| Verge | Forest Floor | the trail's width + 6 m | 4 |
| Trail | Forest Trail | 1.5 to 2.5 m | 1.5 |

It bends more than a road: a point every 8 to 12 m, moved 1 to 4 m to the sides, and around trees and rocks rather than through them. Dress its edges (step 5) with Fern, Lady Fern, Fallen Branches and Roots instead of stones, a Tree Stump now and then.

### Mountain road

Loose grey stones on a mountainside or a pass, where nothing grows well.

| Layer (paint in this order) | Ground | `width` | `edge` |
|---|---|---|---|
| Verge | Rock | the road's width + 5 m | 3 |
| Shoulder | Gravel | the road's width + 2 m | 2 |
| Road | Mountain Road | 3 to 4 m | 1.5 |

It climbs across the slope in long turns, never straight up it (step 1). Dress its edges (step 5) with Rock, Rock Cluster and a Boulder now and then, more of them on the downhill side.

### Snowy road

A road in the snow, trodden through to the earth in places. On snowy land (paint the land Snow first, or lay it where the land is already Snow).

| Layer (paint in this order) | Ground | `width` | `edge` | other |
|---|---|---|---|---|
| Road | Snowy Road | 4 to 5 m | 2.5 | |
| Ruts | Dirt Road | 1 m | 0.5 | `offset` 0.9, `both_sides`, `strength` 0.5 |

No verge: the snow itself is the verge, and the wide `edge` lets the trodden snow fade into it. Dress its edges (step 5) sparsely: a Rock or a Small Stone breaking through the snow every 5 to 10 m.

## 5. Dress the edges

After painting, scatter the small things along each side with `scatter_objects`: a narrow rectangle (1.5 to 2.5 m deep) along each shoulder, `avoid_grounds` naming the road's ground so nothing lands in the roadway, `clustering` 0.6 to 0.8, `spacing` 1 to 2 m. Small Stone and Weeds, about one every 2 to 3 m of road, a Mossy Rock now and then; ask `list_objects` with the search "Ground Cover" or "Rocks" for them. Keep the roadway itself clear.

## 6. Check before you finish

- `look` along the road from walking height: stand on it (distance 25 to 40 m, `pitch` 10 to 20, heading along the road). The stones should thin out into the earth at both sides, the earth into the grass, and no edge run as a straight line.
- `look` straight down (`pitch` 90) over its length: the road should bend, not run straight, and stay clear of everything placed.

## Pitfalls

- The road painted alone, without its shoulder and verge: a grey band laid on the grass. Paint all the layers.
- The layers painted narrowest first: the verge buries the road.
- `edge` 0 or a small edge on the road: it stops at a hard line instead of thinning out.
- A straight road with evenly spaced points: bend it.
- A road straight up a hillside: follow the land around it.
- A trail or ruts that don't show: more than 8 grounds in that 64 m square (step 2).
