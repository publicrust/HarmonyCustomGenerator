# Custom Rust Map Generator Usage Guide

## Table of Contents
1. [Installation](#installation)
2. [Map Generation](#map-generation)
3. [Monument Swapping](#monument-swapping)

## Installation

1. Make sure your server have Harmony 2.3 installed (default installed)
2. Copy the generator dll file to the `HarmonyMods/CustomGenerator.dll`
3. Configure settings in `HarmonyConfig/CustomGeneratorCFG.json`

Logs will be available in `HarmonyConfig/logs`  
Generated map images in `mapimages/`


## Map Generation

1. Run the server with the installed generator at least once
2. Configure desired parameters in the configuration file `HarmonyConfig/CustomGeneratorCFG.json`
3. Run the server again
4. The generated map will be saved in the `maps/` folder or default folder with the chosen name


## Monument Swapping

Monument Swapping allows you to replace vanilla monuments with custom ones while maintaining the original map layout and connections. This feature enables:

- Direct replacement of vanilla monuments with custom versions
- Preservation of original monument positions and road/rail connections
- Multiple monument replacements in a single generation
- Automatic generation of two map versions (with and without custom monuments)


### Custom Monument Preparation
1. Enable "Swap Monuments" => "Enabled": true
2. Place your custom monument prefabs in the `maps/prefabs` folder
3. Prefab requirements:
   - File format: `.prefab`
   - Name format: `monument_original_path.prefab` (example: `fishing_village_c.prefab`)
   - Monument size must match the original
   - Proper terrain alignment in the prefab

> Note: Make sure your custom monuments and whole map are properly tested before using them on server!

For support, contact the author on Discord: aristocratos 