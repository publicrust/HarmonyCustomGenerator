# Custom Rust Map Generator (Harmony 2.3)
![Static Badge](https://img.shields.io/badge/Harmony-2.3-blue?style=for-the-badge)
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/hammzat/HarmonyCustomGenerator/total?style=for-the-badge&color=blue)
[![Boosty](https://img.shields.io/badge/Support%20on-Boosty-orange?style=for-the-badge)](https://boosty.to/aristocratos)

Allows you to generate semi-custom maps on default Rust generator.

For installation, full config reference and monument swap instructions, see [USAGE.md](USAGE.md).

> ⚠️ The mod is for **map generation only**: once the map is saved it shuts the server down. Don't keep it on a live server.

For help, report issue and etc. join [our discord](https://discord.gg/xUdpkm8RUS).
 
### Features

1. QoL (Quality of Life)
- [x] Disable AI navmesh generation on start (faster startup)

2. Map Settings
- [x] Generate map over default limits
> Don't generate maps larger than 6000!  
> Rust client won't be able to process them and will just display as a 6000 map with flying prefabs and monuments

- [x] Generate new map every time
- [x] Save map in specific folder
- [x] Save map with specific name
  
3. Generator
- [x] Try to generate Road Ring on any map
- [x] Enable/Disable roadside monuments
- [x] Enable/Disable roadside objects
- [x] Try to generate Rail Ring on any map
- [x] Enable/Disable railside monuments
- [ ] Enable/Disable railside objects (option exists in the config but has no effect yet)
- [x] Remove car wrecks near roads
- [x] Remove rivers
- [x] Remove tunnel entrances
- [x] Remove underground tunnels
- [x] Remove large powerlines
- [x] Scale river width
- [x] Configure tier percentages (Tier0, Tier1, Tier2)
- [x] Configure biome percentages (Arid, Temperate, Tundra, Arctic + Jungle separately)
- [x] Generate unique environment (oasis, canyons, lakes)

4. Monuments
- [ ] Full monument placement configuration
- [x] Configure distances between monuments
- [x] Configure specific monument counts
- [x] Monument placement filters (biome, splat, topology)
- [x] Choose prefabs inside a monument group (include / exclude / number of copies)
- [x] Override a group's prefab path, ignore the vanilla world-size count multiplier

5. Map Image Generator
- [x] Generate splat/height map
- [x] Generate monument names
- [x] Generate map grid

6. Additional Features
- [x] Monument Swapping
  - Replace vanilla monuments with custom ones
  - Swap specific monument types (e.g., replace Outpost with custom version)
  - Keep original monument positions and connections
- [x] Save both map versions (with and without swaps)
- [x] Config in English or Russian, switchable in the config itself
- [x] Config validation with warnings in the log, automatic backup on update

### Configuration
All features are configured in `HarmonyConfig/CustomGenerator.json` (created on the first run). Every option is described in [USAGE.md](USAGE.md), and monument swapping has its own section: [Monument swap](USAGE.md#monument-swap).

------
### Authors and Credits
- [@aristocratos](https://github.com/hammzat)
  - For support join to [discord server](https://discord.gg/xUdpkm8RUS)
 
Thanks to:
- bmgjet
- I4IgO Kurasaki
- FlySelf (rustmaps custom prefabs)
