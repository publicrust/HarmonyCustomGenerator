# Custom Rust Map Generator Guide

How to install the mod, set up the config and swap monuments.

## Contents
1. [How it works](#how-it-works)
2. [Installation](#installation)
3. [First run and generation](#first-run-and-generation)
4. [Config basics](#config-basics)
5. [Map Settings](#map-settings)
6. [Main Generator](#main-generator)
7. [Monuments](#monuments)
8. [Monument swap](#monument-swap)
9. [Recipes](#recipes)
10. [Troubleshooting](#troubleshooting)

---

## How it works

The mod hooks into the vanilla Rust map generator through Harmony and changes its parameters on the fly. The map stays procedural, but you control roads, rivers, monuments, biomes and more.

> ⚠️ **The mod is for map generation only.** Once the map is generated and saved, the mod renders a preview image and **shuts the server down**. Don't leave `CustomGenerator.dll` in `HarmonyMods` on a live server: the server will generate a map and exit, and an auto-restart script will loop forever.
>
> The usual workflow: generate a `.map` on a separate server copy, then put it on your live server with `server.levelurl` or a local `server.level`.

---

## Installation

1. You need a Rust Dedicated Server. Harmony 2.3 is already included.
2. Copy `CustomGenerator.dll` to `<server folder>/HarmonyMods/`.
3. That's it. The config is created on the first run.

Where things are (all paths are relative to the server folder that contains `RustDedicated.exe`):

| Path | Contents |
|---|---|
| `HarmonyMods/CustomGenerator.dll` | The mod |
| `HarmonyConfig/CustomGenerator.json` | Config |
| `HarmonyConfig/CustomGenerator.schema.json` | Config schema for editor hints (regenerated, don't edit) |
| `HarmonyConfig/logs/cgen_*.log` | Mod logs, one file per run |
| `maps/` | Generated maps (`.map`) |
| `maps/prefabs/` | Your monuments for swapping |
| `mapimages/` | Map previews (`.png`) |
| `mapimages/resources/` | Fonts for previews (downloaded automatically) |

---

## First run and generation

1. Start the server with the `+server.worldsize` and `+server.seed` you want:
   ```bat
   RustDedicated.exe -batchmode -nographics +server.identity gen +server.worldsize 4000 +server.seed 12345 +server.level "Procedural Map"
   ```
2. The mod creates `HarmonyConfig/CustomGenerator.json`, generates a map with the default settings and shuts the server down.
3. Edit the config and start the server again. Every run generates a new map.
4. The map is saved to `maps/CustomGenerator<size>_<seed>.map` and the preview to `mapimages/`.

A 4000 map takes about 5–10 minutes to generate, plus about a minute to render the preview.

> Seed and size come from the server launch parameters, not from the mod config.

---

## Config basics

### Language
The first field of the config:
```json
"Language (en/ru)": "en"
```
A new config uses your system language. Keys are accepted in **both** languages: change the value to `en` or `ru`, restart the server, and the file is rewritten with keys in the selected language. All values are kept.

This guide uses English keys, with the Russian key in parentheses where it helps.

### Editor hints
Next to the config the mod writes `CustomGenerator.schema.json`, and the config's first line links to it (`"$schema"`). Open the config in **VS Code** (or another editor with JSON Schema support) and you get:
- a description of every option on hover, in the config's language;
- autocompletion of keys and allowed values (biomes, textures, topologies, `HeightMode`, `distanceSame`, etc.);
- underlined mistakes: a misspelled biome, a negative distance, text instead of a number.

The schema is rewritten on every run, so it always matches your mod version. Don't edit it and don't remove the `"$schema"` line.

### Version and updates
Don't edit the `"Version"` field. When you update the mod:
- the old config is copied to `CustomGenerator.json.<old version>.backup`;
- new options are added with default values, your values are kept.

### When the config has an error
- **Broken JSON** (extra comma, missing bracket): the file is saved as `CustomGenerator.json.broken-<date>` and the mod starts with the default config. Fix the copy and put it back.
- **Invalid value** (negative percentage, unknown biome in a filter, etc.): a `[WARN]` line explaining the problem goes to the log, and the value is replaced with a safe one **for this run only**. The file isn't changed. Always check the warnings in `HarmonyConfig/logs` after a run.

The mod reads and re-saves the config on every run, so the file formatting may change. That's expected.

---

## Map Settings

`Map Settings` (`Настройки Карты`)

| Key | Default | Description |
|---|---|---|
| `Generate new map everytime` | `true` | Don't load a previously generated map from disk, generate a new one every time |
| `Override Map Sizes (9000 not be changed to 6000)` | `true` | Allow sizes below 1000 and above 6000. Without it Rust clamps the size |
| `Override Map Folder (saves to <Server Root>/maps/)` | `true` | Save the map to `<server folder>/maps/` |
| `Override Map Name` | `true` | Name the map file using the template below |
| `Map Name ({0} - size, {1} - seed)` | `CustomGenerator{0}_{1}` | Name template: `{0}` is the size, `{1}` is the seed. `.map` is appended automatically |

> ⚠️ Don't generate maps **larger than 6000**. The Rust client doesn't support them: it shows a 6000 map with monuments and prefabs floating in the air.

Only `{0}` and `{1}` are allowed in the map name. If you use something like `{2}`, the mod falls back to the default template and logs a warning.

---

## Main Generator

`Main Generator` (`Основной Генератор`)

### Roads and rails: `Road`, `Rail`

```json
"Road": {
  "ShouldChange": true,
  "Enabled": true,
  "GenerateRing": true,
  "GenerateSideMonuments": true,
  "GenerateSideObjects": false
}
```

| Key | Description |
|---|---|
| `ShouldChange` | Master switch for the block. With `false` everything else is ignored and roads (rails) are vanilla |
| `Enabled` | The ring road (ring rail). `false` means no ring on any map size |
| `GenerateRing` | Generate the ring on **any** map size. In vanilla the ring only appears on large maps |
| `GenerateSideMonuments` | Roadside (railside) monuments: gas stations, supermarkets, stations, etc. |
| `GenerateSideObjects` | **`Road` only.** Roadside objects. With `false` they aren't generated at all. For `Rail` this field does nothing |

> `Enabled` and `GenerateRing` control the **ring** only. Regular roads between monuments are always generated.

### Unique environment: `UniqueEnviroment`

```json
"UniqueEnviroment": {
  "ShouldChange": true,
  "GenerateOasis": true,
  "GenerateCanyons": true,
  "GenerateLakes": true
}
```
With `ShouldChange: true`, oases, canyons and lakes are turned on (`true`) or off (`false`) **on any map size**. In vanilla they only appear on maps of 4000–4500 and up. Jungle swamps aren't controlled here: configure them in [Monuments](#monuments) (`unique_environment/jungle`).

### Rivers, powerlines, tunnels and more

| Key (EN) | Key (RU) | Default | Description |
|---|---|---|---|
| `Remove Rivers` | `Удалить реки` | `false` | No rivers |
| `River width scale (1 = default)` | `Множитель ширины рек (1 = по умолчанию)` | `1` | River width: `0.7` is narrower, `1.5` is wider. Must be above 0 |
| `Remove Car Wrecks around Road` | `Удалить разбитые префабы машин около дороги` | `false` | Remove wrecked cars along roads |
| `Allow building on road` | `Разрешить строительство на дорогах` | `false` | Allow building on roads: the mod lifts the building block on them |
| `Remove large powerlines` | `Удалить большие ЛЭП` | `false` | No large powerlines |
| `Remove tunnel entrances` | `Удалить входы в туннели` | `false` | Remove the underground railway entrances. The tunnels themselves stay |
| `Remove underground tunnels (also removes entrances)` | `Удалить подземные туннели (вместе со входами)` | `false` | Remove the underground railway entirely, entrances included |

### Tiers and biomes: `Change percentages`

```json
"Change percentages": true,
"Tier Percentages (100 in total)": { "Tier0": 30, "Tier1": 30, "Tier2": 40 },
"Biome Percentages (Arid+Temperate+Tundra+Arctic = 100, Jungle is separate)": {
  "Arid": 40, "Temperate": 15, "Tundra": 15, "Arctic": 30, "Jungle": 50
}
```

- Percentages apply only with `"Change percentages": true`.
- **Tiers** (loot difficulty zones, from Tier0 near the map edge to Tier2 inland) add up to 100. If they don't, they're scaled proportionally, so 1/1/2 becomes 25/25/50.
- **Biomes:** `Arid`, `Temperate`, `Tundra` and `Arctic` add up to 100. Otherwise they're scaled proportionally too.
- **`Jungle` isn't part of that sum.** It's a separate value from 0 to 100, which is 50 in vanilla.
- Negative values or a zero sum are errors. In that case the vanilla percentages are kept and a warning is logged.

The example above shows the vanilla values, which are a good starting point.

---

## Monuments

`Monuments` (`Монументы`)

### Getting the list
1. Set `"Enabled": true` and an empty list:
   ```json
   "Monuments": { "Enabled": true, "MonumentList": [] }
   ```
2. Run a generation. The mod finds every monument group in the game (18 at the moment) and writes them to `MonumentList` with vanilla settings.
3. Edit the groups you need. Leave the rest alone: they have `"ShouldChange": false` and behave as in vanilla.

To get a fresh list (for example, after a major Rust update), empty `MonumentList` again.

### Groups
Each entry is a **group** of monuments, not a single monument. A group corresponds to a prefab folder in the game.

> ⚠️ These "folders" live **inside the game's asset bundles** (`Bundles/`), not on disk. The full path looks like `assets/bundled/prefabs/autospawn/<Folder>/`, for example `assets/bundled/prefabs/autospawn/monument/harbor/harbor_1.prefab`. There's no such folder in the server directory: you can't open it or put your own prefabs in it. `Folder` and `OverrideFolder` are paths inside the bundles. The only way to add your own monuments is the [swap](#monument-swap).

| `Folder` | `Description` | What it is |
|---|---|---|
| `monument/xlarge,monument/large,monument/medium,monument/small` | Main Monuments | Main monuments: Launch Site, Airfield, Military Tunnel, Power Plant… |
| `monument/harbor` | Harbors | Harbors and the ferry terminal |
| `monument/fishing_village` | Fishing Villages | Fishing villages |
| `monument/lighthouse` | Lighthouses | Lighthouses |
| `monument/military_bases` | Desert Military | Desert military bases |
| `monument/arctic_bases` | Arctic Bases | Arctic research base |
| `monument/tiny` | Tiny Monuments | Tiny monuments |
| `monument/cave` | Caves | Caves |
| `monument/swamp` | Swamps | Swamps |
| `monument/ice_lakes` | Ice Lakes | Ice lakes |
| `monument/jungle_ruins` | Jungle Ruins | Jungle ruins |
| `monument/underwater_lab` | Underwater Labs | Underwater labs |
| `tunnel-entrance` | Tunnel Entrances | Underground railway entrances |
| `mountain` | Mountains | Mountains |
| `unique_environment/...` | Canyons, Lakes, Oasis, Jungle Swamps | Unique environment |

This list comes from the current Rust version and may change. The up-to-date list is always the one in your `MonumentList` after auto-detection.

### Group fields

```json
{
  "ShouldChange": true,
  "Generate": true,
  "Description": "Harbors",
  "Folder": "monument/harbor",
  "MinWorldSize": 0,
  "TargetCount": 5,
  "distanceSame": "Max",
  "MinDistanceSameType": 300,
  "distanceDifferent": "Any",
  "MinDistanceDifferentType": 50,
  "Filter": { "Enabled": true, "...": "..." },
  "OverrideFolder": "",
  "IncludePrefabs": ["harbor"],
  "ExcludePrefabs": [],
  "PrefabCopies": { "harbor": 5 },
  "IgnoreWorldSizeMultiplier": true
}
```

**Basics**

| Field | Description |
|---|---|
| `ShouldChange` | `true` applies the settings below. `false` keeps the group vanilla and ignores the other fields |
| `Generate` | `false` means the group isn't generated at all (for example, no lighthouses). Works only with `ShouldChange: true` |
| `Description`, `Folder` | The group's name and its path in the bundles. The mod finds the group by `Folder`, so **don't change it**. Use `OverrideFolder` to load a different path |
| `MinWorldSize` | Minimum map size for the group to appear. `0` means any size |
| `TargetCount` | How many monuments of the group to place. `0` means "every prefab of the group", as in vanilla. You may get fewer if there isn't enough room (see distances) |

**Distances**

| Field | Description |
|---|---|
| `MinDistanceSameType` | Minimum distance (m) to monuments of the **same** group |
| `MinDistanceDifferentType` | Minimum distance (m) to monuments of **other** groups |
| `distanceSame` / `distanceDifferent` | Placement preference: `Max` prefers far away, `Min` prefers close, `Any` has no preference |

If monuments don't fit, the game gradually relaxes the minimum distances, down to a quarter of the configured values. Large distances make closer placement less likely but don't rule it out.

**Placement filter: `Filter`**

Defines where a monument may stand.

```json
"Filter": {
  "Enabled": true,
  "SplatType": ["Grass", "Forest"],
  "BiomeType": ["Temperate"],
  "TopologyAny": [],
  "TopologyAll": [],
  "TopologyNot": ["River", "Road"]
}
```

| Field | Meaning | Empty list |
|---|---|---|
| `Enabled` | `true` replaces the group's vanilla filter with this one | — |
| `SplatType` | Allowed ground textures | any |
| `BiomeType` | Allowed biomes | any |
| `TopologyAny` | **At least one** of these topologies | any |
| `TopologyAll` | **All** of these topologies | no condition |
| `TopologyNot` | **None** of these topologies | no condition |

Valid values:
- **SplatType:** `Dirt`, `Snow`, `Sand`, `Rock`, `Grass`, `Forest`, `Stones`, `Gravel`
- **BiomeType:** `Arid`, `Temperate`, `Tundra`, `Arctic`, `Jungle` and others
- **Topology:** `Field`, `Cliff`, `Summit`, `Beachside`, `Beach`, `Forest`, `Forestside`, `Ocean`, `Oceanside`, `Decor`, `Monument`, `Road`, `Roadside`, `Swamp`, `River`, `Riverside`, `Lake`, `Lakeside`, `Offshore`, `Rail`, `Railside`, `Building`, `Cliffside`, `Mountain`, `Clutter`, `Alt`, `Tier0`, `Tier1`, `Tier2`, `Mainland`, `Hilltop`

Names are case-sensitive. The mod drops a misspelled value and logs the full list of valid ones. After auto-detection every group already has its vanilla filter filled in, which is a good starting point.

> A filter only restricts placement. If it's too strict (for example, `Arctic` + `Beach`), you may get no monuments at all.

**Prefab selection**

These fields control *which* monuments of the group end up on the map.

| Field | Description |
|---|---|
| `OverrideFolder` | A different path **inside the bundles** instead of the vanilla one, relative to `assets/bundled/prefabs/autospawn/`. Only paths that exist in the game work, for example another group's `Folder`. Separate several paths with commas: `"monument/harbor,monument/lighthouse"`. Empty means the vanilla path |
| `IncludePrefabs` | Keep only prefabs whose name contains one of these strings. Empty means all |
| `ExcludePrefabs` | Remove prefabs whose name contains one of these strings |
| `PrefabCopies` | `"part of name": N`: how many copies of the prefab go into the candidate pool. `0` means none |
| `IgnoreWorldSizeMultiplier` | Vanilla multiplies `TargetCount` by a map-size factor that is only defined up to 6000. `true` places exactly `TargetCount` |

How it works:
- Rules are matched against the **prefab name without its folder**, case-insensitive. `"harbor"` matches `harbor_1` and `harbor_2`, but not `ferry_terminal_1`, even though it lives in `monument/harbor`.
- Normally each prefab of a group appears on the map **once**. So to get 5 harbors out of 2 harbor variants you need **copies**: `PrefabCopies: {"harbor": 5}` plus `TargetCount: 5`.
- The log shows which prefab names you can use. For every group with rules the mod writes a line like
  ```
  Harbors: '.../monument/harbor' 3 -> 10 candidates (available: ferry_terminal_1, harbor_1, harbor_2)
  ```
  To see the names in a group, give it any rule (for example, `"ExcludePrefabs": ["nothing"]`) and run a generation.

> Groups are placed one after another. If you greatly increase one group (for example, harbors), later groups may run out of room and some main monuments may disappear. Lower the distances or use a bigger map.

---

## Monument swap

`Swap Monuments` (`Замена Монументов`)

The swap replaces vanilla monuments on the map with your own versions from `.map` files made in RustEdit. Position, rotation, roads and rails are kept from the original.

### Settings

```json
"Swap Monuments": {
  "Enabled": true,
  "Save both maps (with swap and without)": true
}
```

| Key | Description |
|---|---|
| `Enabled` (`Включить`) | Enable the swap |
| `Save both maps (with swap and without)` (`Сохранить обе карты (с заменой и без)`) | `true` keeps the original as `<name>.map` and saves the swapped version as `<name>.swapped.map`. `false` **overwrites** the original with the swapped version |

### How the swap works
The swap runs **after** generation, once the map is saved:
1. The mod opens the saved map `maps/<name>.map`.
2. For every file `maps/prefabs/<name>.map` it looks for monuments whose path contains `<name>`.
3. It removes each match and places every prefab from your file in its spot: position and rotation come from the original, and the other prefabs are placed relative to the first object in your file.
4. It saves the result (see `Save both maps`).

What is **not** transferred:
- terrain, textures and topology from your `.map`. Only **prefabs** are taken, the ground under the monument stays vanilla;
- roads and rails: they stay connected to the original monument's layout;
- the preview in `mapimages/`: it's rendered before the swap and shows the **original** monuments.

### Step 1. Build the monument in RustEdit

**Option A: based on the original** (you add objects around a vanilla monument):
1. Place the original monument **without rotation** (0, 0, 0). Point (0, 0, 0) is easiest, but the position doesn't matter: everything is calculated relative to it.
2. Add your objects around it.
3. The original monument must be the **first object in the hierarchy**.

**Option B: fully custom** (no original):
1. Place a **SpawnPoint** at **(0, 0, 0)** without rotation and make it **first in the hierarchy**. It marks the **center of the original monument** yours will replace.
2. Build your monument around that point.
3. The center of a vanilla monument isn't always its visual center. For example, a gas station's center sits below the building because of the cave under it, so a custom gas station has to be raised or it ends up underground. See the examples in `Examples/prefabs/full_custom_monuments/`.

During the swap the SpawnPoint is replaced by a zero-scale helper prefab and isn't visible on the map.

Ready-made templates are in `CustomPrefabs/` (thanks to FlySelf), examples are in `Examples/prefabs/`.

### Step 2. Name the file correctly

```
<vanilla prefab name>.prefab.map
```
Examples: `harbor_1.prefab.map`, `fishing_village_c.prefab.map`, `gas_station_1.prefab.map`.

- The mod strips `.map` and looks for **`harbor_1.prefab`** in monument paths.
- **Don't drop `.prefab`.** A file named `harbor_1.map` also works, but it matches just `harbor_1` and may catch things you didn't mean.
- You can find the vanilla prefab name in RustEdit or in the monument log (the `available` list, see [Group fields](#group-fields)).

### Step 3. Put the file in place

```
<server folder>/maps/prefabs/harbor_1.prefab.map
```
The folder is created automatically on the first swap. You can have any number of files, and each one replaces **every** matching monument on the map.

### Step 4. Generate the map
Run a generation. `[SWAP MN]` lines appear in the server log. Check the result by opening `.swapped.map` (or `.map`) in RustEdit.

### Combining swap with monument settings
They work together. For example, make 3 harbors with `PrefabCopies` and replace every `harbor_1` with your version: the swap replaces **each** copy.

---

## Recipes

**Small map with ring road and ring rail, no rivers**
```json
"Road": { "ShouldChange": true, "Enabled": true, "GenerateRing": true, "GenerateSideMonuments": true, "GenerateSideObjects": true },
"Rail": { "ShouldChange": true, "Enabled": true, "GenerateRing": true, "GenerateSideMonuments": true, "GenerateSideObjects": false },
"Remove Rivers": true
```

**More snow, less desert**
```json
"Change percentages": true,
"Biome Percentages (Arid+Temperate+Tundra+Arctic = 100, Jungle is separate)": { "Arid": 15, "Temperate": 25, "Tundra": 20, "Arctic": 40, "Jungle": 30 }
```

**5 harbors, no ferry terminal** (group `monument/harbor`)
```json
"ShouldChange": true, "Generate": true, "TargetCount": 5, "MinDistanceSameType": 300,
"IncludePrefabs": ["harbor"], "PrefabCopies": { "harbor": 5 }, "IgnoreWorldSizeMultiplier": true
```

**One fishing village of a specific type** (group `monument/fishing_village`)
```json
"ShouldChange": true, "Generate": true, "TargetCount": 1, "IncludePrefabs": ["fishing_village_c"]
```

**No lighthouses** (group `monument/lighthouse`)
```json
"ShouldChange": true, "Generate": false
```

**No underground railway and no powerlines**
```json
"Remove underground tunnels (also removes entrances)": true,
"Remove large powerlines": true
```

---

## Troubleshooting

| Problem | What to check |
|---|---|
| The server shuts down right after starting | That's by design: the mod generates a map and shuts the server down. Remove `CustomGenerator.dll` from `HarmonyMods` to run a normal server |
| A setting has no effect | Is `ShouldChange: true` set in that block or group? Any `[WARN]` lines in `HarmonyConfig/logs`? |
| The config "reset itself" | The JSON was probably broken. Look for `CustomGenerator.json.broken-*` next to it |
| Fewer monuments than `TargetCount` | Not enough room: lower `MinDistance*`, loosen `Filter`, use a bigger map. Duplicates need `PrefabCopies` |
| Other monuments disappeared | One group took their space. Shrink it or lower the distances |
| I don't know a prefab name | Give the group any rule and look for the `available:` line in the log |
| The swap didn't work | Is the file in `maps/prefabs/`? Is it named `<prefab>.prefab.map`? Is `Swap Monuments → Enabled` set to `true`? Any `Swap:` errors in the log? |
| A swapped monument is shifted, rotated or underground | Is the original or the SpawnPoint first in the hierarchy? Is it unrotated? Does the SpawnPoint match the original's center, height included? |
| The preview shows the old monuments | Expected: the preview is rendered before the swap. Check the result in RustEdit |
| No preview or a font error | No internet access: copy the fonts from the repository's `Resources/` folder to `mapimages/resources/` |
| A map above 6000 looks broken | The Rust client doesn't support such sizes, stay at 6000 or below |

For support, join [our Discord](https://discord.gg/xUdpkm8RUS).
