# Custom Rust Map Generator (Harmony 2.3)
![Static Badge](https://img.shields.io/badge/Harmony-2.3-blue?style=for-the-badge)
![Static Badge](https://img.shields.io/github/license/hammzat/HarmonyCustomGenerator?label=license&style=for-the-badge)
[![Boosty](https://img.shields.io/badge/Support%20on-Boosty-orange?style=for-the-badge)](https://boosty.to/aristocratos)

Allows you to generate semi-custom maps on default Rust generator.

For installation and usage instructions, see [USAGE.md](USAGE.md)

### Features

1. QoL (Quality of Life)
- [x] Skip Asset Warmup on start

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
- [x] Enable/Disable railside objects
- [x] Remove car wrecks near roads
- [x] Remove rivers
- [x] Remove tunnel entrances
- [x] Configure tier percentages (Tier0, Tier1, Tier2)
- [x] Configure biome percentages (Arid, Temperate, Tundra, Arctic)
- [x] Generate unique environment (oasis, canyons, lakes)

4. Monuments
- [ ] Full monument placement configuration
- [x] Configure distances between monuments
- [x] Configure specific monument counts
- [x] Monument placement filters (biome, splat, topology)

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
- [x] Russian and English language support in configuration

### Monument Swapping Setup  
Instructions for use can be found in the `USAGE.md` file

Configure all features at `HarmonyConfig/CustomGeneratorCFG.json`

------
### Authors and Credits
- [@aristocratos](https://github.com/hammzat)
  - For support DM me on Discord - aristocratos
 
Thanks to:
- bmgjet
- I4IgO Kurasaki
- FlySelf (rustmaps custom prefabs)
