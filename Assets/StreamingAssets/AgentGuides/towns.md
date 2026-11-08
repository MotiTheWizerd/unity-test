---
when: before laying out a town, a village or a hamlet, a street, a square or a market, or adding houses to one (read `terrain` too when it stands on hilly ground)
---
# Towns and villages

A place reads as a town because of its streets: the buildings front them, the ground under them is worn, and the open spaces are used. Build in this order; each step stands on the one before.

## 1. Read the site and size the place

- `inspect_area` over the whole site (up to 256 m a side; several calls for a bigger town). Streets follow the land: run the main street along the gentlest line (across a slope, or along a valley's floor), never straight up ground steeper than about 8°.
- Size it to the request: a hamlet is 3 to 6 buildings around a lane or a yard; a village 8 to 20 along a main street, with a green or a small square; a town 25 or more on several streets around a market square.
- Take the buildings' sizes from `list_objects` and plan from their footprints: a street side 100 m long holds about six houses 14 m across with 2 m between them.

## 2. Plan in meters before placing anything

Write the plan down first: each street as its centre line of points, the square's rectangle, and each building's spot and heading.

- Widths: a main street 6 to 8 m, a side street 4 to 5 m, a lane or an alley 2 to 3 m, a footpath to a door 1.5 m.
- A village's main street bends gently and widens where it meets the square. A dead straight street with mirrored rows of houses looks planned: right for a fort or a new town, wrong for a village. Streets meet at roughly right angles, not in sharp wedges.
- The square: 20 to 40 m across for a village green or market, 30 to 60 m for a town's. The street runs along one side of it or into it, not through its middle. The well, a market cross or a big tree stands in it with 4 to 6 m clear around it, and the biggest building (the hall, the inn, a temple) fronts it.
- Buildings front the street, set back 0 to 1 m from its edge in a town and 1 to 4 m in a village, the setback varying a little from house to house. Gaps between neighbours: 0 to 2 m in a town's centre, 3 to 10 m in a village. An alley every three to five houses leads to the back yards.
- Behind each house lies a yard or a garden at least as deep as the house, and the plots' back fences line up roughly.

## 3. Level, then paint the ground

- On ground steeper than about 5°, level each building's plot with `flatten_area`: its footprint plus 2 m each way, turned to the building's heading (the `terrain` guide says how wide a blend it needs). Streets may follow gentle slopes as they are.
- Paint before placing anything; painting never moves what stands. Paint each street with `paint_line` along the centre points you planned, its `width` the street's width.
  - Grounds (`list_grounds`): Cobblestone for a town's streets and square, Flagstone for the square before a hall or a temple, Packed Earth for village lanes, yards and the trodden ground around a well, Wet Mud by troughs and stables and where the well spills, Gravel for paths and courtyards, Dirt for tracks into the country.
  - Where a street leaves the place it becomes a track: paint its last 15 to 30 m narrower, with a `strength` of 0.5 to 0.7, so it wears out into the grass.
  - The square: `paint_area` lined up with it. A footpath from each door to the street: `paint_line`, 1.5 m wide, strength 0.6. Yards: `paint_area` turned to the house's heading, strength 0.5 to 0.7.

## 4. Buildings

- Along a street, `place_line` lays one kind of building in a row: give the street's centre points as the path, an `offset` of half the street's width plus the setback plus half the building's depth, `both_sides` for a row on each side, and `face_path` so every door faces the street. The offset is to the right of the path as it is walked (negative: to the left).
- Set the `gap`: buildings in a row touch end to end without one (their default gap is 0). Use 1 to 2 m in a town and 4 to 8 m in a village, with a `random_gap` of 2 to 4 m for an unplanned look.
- One kind in a long row looks made by a machine. Mix the kinds: short rows of different kinds along different stretches, or a mixed row with `place_objects`. A building fronting a street faces across it: on the right of the street as walked, its heading is the street's heading minus 90°; on the left, plus 90°.
- Lay each row only along the stretches where buildings belong: a row laid along the whole street also fills the square and the junctions. At a bend the row on the inside bunches up; the check in step 7 finds it.
- Keep the biggest buildings for the square and the corners, and turn a corner house to face the more important street.

## 5. The square and the street's life

- In the square: the well or a fountain in the middle; market stalls in rows facing each other across a 3 to 4 m aisle, or around the square's edge facing in; benches at its edge, facing in.
- Lamps along a main street every 12 to 20 m, with `place_line`: an `offset` of half the street's width plus 0.5 m, `both_sides`, `face_path`, and a `gap` that spaces them. Lamps also at the square's corners and by the inn's door, never in the middle of a square.
- Props where people would use them, in twos and threes rather than spread evenly: barrels and crates by the inn, the shops and the market; a cart and hay by a barn or a stable; a water trough and a hitching post by the inn; firewood and a chopping block at a house's side or back.
- Fences enclose the yards and gardens behind the houses, not the street side, each with a gate facing its house.

## 6. Trees and greenery

- Trees and bushes grow in yards, gardens and around the place's edge, and a tree or two on the green; never in a street or the square. Use `scatter_objects` with `avoid_grounds` naming the grounds you painted (e.g. ["Cobblestone", "Packed Earth"]), over the yards and the edges rather than the whole site.
- Fields, pasture and orchards lie outside, fenced, reached by tracks.

## 7. Check before you finish

- `inspect_area` over the whole place: its `overlaps` list every pair whose footprints cut into each other (a tree into a house, a lamp into a stall, two houses on one spot). Fix each with `adjust_objects`, or `delete_objects` for what shouldn't be there. Tell the user nothing overlaps only after `inspect_area` said so; a picture can't show it.
- `look` straight down (`pitch` 90) from a distance about the place's width: the streets should read clearly, the plots evenly, and no building stand in a street. Then look along the main street from one end at walking height (distance 25 to 40 m, `pitch` 10 to 20, heading along the street).

## Pitfalls

- Houses touching end to end in a row: the gap was left at 0.
- Houses with their backs to the street: `face_path` was left out, or the offset went to the wrong side of the path.
- Everything on a grid, every house the same distance from the street, the two sides mirrored: vary the setback and the gaps, bend the street, mix the kinds.
- A street painted after the houses stand: they sit on its edge. Paint first.
- Many props spread evenly: a few in the right places say more.
