---
when: before shaping land (hills, mountains, a range, valleys, ravines, a crater, rougher or gentler ground), digging a pond or a lake, or painting and planting land you shaped
---
# Shaping land

## Read the site

- `inspect_area` over the site and a margin around it: the lowest and highest ground and the steepest slope say what you work with. The land reaches only as far as the `<editor>` note says; grow it with `extend_land` only when the user asks for more land or the work won't fit.
- Heights run from 0 to 200 m and new land lies at 50 m, so a valley can go about 50 m down into new land and a mountain about 150 m up. A sculpt's reply says when the ground hit that range.

## Big forms first, then the detail

- `sculpt_ridge` lays a range's spine or, with a negative height, a valley's floor; `sculpt_hill` puts peaks on it and makes single hills and hollows. Heights add up, so a peak on a ridge stands at both heights, and several overlapping peaks of different sizes look more natural than one.
- For a range that reads as several mountains and not one cone: keep the ridge about half as high as the peaks, set the peaks at least one peak's width apart along it, and dig a saddle between each pair (a short `sculpt_ridge` across the ridge with a negative height). A spur or two running off the sides helps.
- One landform is at most 256 m across, so build a taller or wider mountain from a ridge with peaks on it rather than one huge hill.
- Don't stack `flatten_area` levels to make a mountain: they make terraces. `flatten_area` is for sites that must be level.

## Width sets the slope

How steep a landform gets comes from its height and its width (before roughness):

- `round` (hills, mounds) is steepest halfway down its side, at about atan(3 × height ÷ width). A width of 12 times the height stays under 15°, gentle enough to build on; 9 times under 20°, easy walking; 5 times about 30°, a steep hill.
- `peak` (mountains) is steepest at the summit, about atan(4 × height ÷ width), easing toward its foot. For snow to lie on it, make the width about four times the height; at three times its upper faces are too steep for snow and stay bare rock; narrower makes cliffs.
- `plateau` (a mesa) is flat over its inner half, with sides of about atan(6 × height ÷ width).
- A ridge's sides follow the same rule across its width.

Roughness: 0.4 to 0.7 for mountains, 0.2 to 0.4 for hills, 0 for made things (a barrow, an embankment, a dam).

## Level ground and smooth ground

- `flatten_area` levels to the average height inside it by default, so as much is dug away as filled in, and eases back into the land over its `blend`. Make the blend at least twice the height range the site had (its highest minus its lowest ground), or the bank comes out steeper than about 20° and reads as a cut. The blend goes up to 32 m; a site that needs more is too steep to level in one piece: level it in terraces, or pick gentler ground.
- Where shapes meet badly, or the user wants gentler land, use `smooth_ground`: it softens bumps, steps and banks and keeps the larger shape. Give a `size` about that of the bumps that should go, and smooth again to soften more.

## Water

- A sea is one level for the whole worldspace, set with `set_sea_level` only when the user asks for a sea, a coast or another water level. All ground below it is under water, so a coast is shaped by bringing land down below the level or lifting it above.
- A lake or pond fills a hollow up to a level (`add_lake`), and its shore follows the land. On level ground, dig the hollow first: `sculpt_hill` with a negative height for a natural bowl (a width about ten times the depth gives gentle banks), or `flatten_area` a few meters below the ground for a level bottom; then `add_lake` in it. A refusal says how high the water can go: the lowest point of the hollow's rim.

## Paint by the shape

- `auto_paint` over the whole landform and a margin around it: rock comes by itself on steep ground (from about 34°), dirt on slopes (from about 20°), sand at the sea's shore. A wide `edge` (16 to 32 m) lets the painted area fade slowly into the land around it.
- Snow: set `snow_line` from the heights the sculpting replies gave, about a third of the way down from the top. By default the steepest faces stay bare rock; `snow_holds_to` 90 whitens everything above the line.
- For a colder or drier land, swap the flat ground (`flat`, for example Dry Grass or Gravel).
- Then paint the details by hand with `paint_area` and `paint_line`: scree below cliffs, a path up, a muddy valley floor. A `strength` of 0.4 to 0.7 with an `edge` of 2 to 4 m makes worn, natural transitions; full strength with a hard edge looks laid by hand.

## Plant and strew

- Use `scatter_objects`, not one by one: trees with a `max_slope` of about 25° and a `max_height` below the snow line, rocks with a `min_slope` on the steep ground, a few kinds mixed.
- Keep the default clustering for forests (groves with clearings); 0 spreads them evenly.
- A forest that reads as one from afar needs about one tree per 100 to 150 m² of wooded ground. A call places up to 200, so call it several times over parts of a large area. When fewer fit than asked, the reply names the rule that held them back.
- Under a forest, the floor: a second, denser `scatter_objects` of the small ground cover (Ferns, Fallen Branches, Roots, Mossy Rocks, a Tree Stump here and there), `clustering` about 0.7, so the ground between the trees isn't bare. On open slopes, Small Stones and Mossy Rocks in clumps, more on the steeper ground.
- Check single spots with `ground_at`.

## Check

- `look` from far enough to see the whole form (a distance of about one and a half times its width), then from the side at a low pitch (10° to 20°), the way someone walking up to it would see it. Straight down (`pitch` 90) shows the plan: where the ridges, valleys and water run.
- When a step went wrong (a sculpt too tall, a paint too strong), take it back with `undo_last` and do it again, rather than sculpting over it.
