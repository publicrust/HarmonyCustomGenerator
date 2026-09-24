# Monument swap

This guide has moved into the main documentation, which is kept up to date with the code:

- English: [USAGE.md → Monument swap](USAGE.md#monument-swap)
- Русский: [USAGE_RU.md → Замена монументов](USAGE_RU.md#замена-монументов-свап--swap-monuments)

Quick checklist:
1. Build the monument in RustEdit. The first object in the hierarchy is the original monument or a SpawnPoint at (0, 0, 0), without rotation.
2. Save it as `<vanilla prefab name>.prefab.map`, for example `harbor_1.prefab.map`.
3. Put it in `<server folder>/maps/prefabs/`.
4. In `HarmonyConfig/CustomGenerator.json` set `"Swap Monuments": { "Enabled": true }` and keep `Override Map Folder` and `Override Map Name` enabled.
5. Generate the map and check the result (`<name>.swapped.map` if `Save both maps` is on) in RustEdit.

Examples of fully custom monuments are in [Examples/prefabs/](Examples/prefabs/), templates in [CustomPrefabs/](CustomPrefabs/).
