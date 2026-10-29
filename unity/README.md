# Ember Vale for Unity

Work-in-progress port of the single-file HTML game (`../ember-vale.html`) to Unity, for iPhone.
The HTML game stays the design reference; read [PORT_PLAN.md](PORT_PLAN.md) for how the two are kept in step.

## What exists

| Part | Where | State |
| --- | --- | --- |
| Sim core (no UnityEngine) | `Assets/Scripts/Sim` | World generation (identical to the HTML for the same seed), bands of settlers that pick a site, walk and found villages |
| View | `Assets/Scripts/View` | Ground texture, trees/rocks/tents/halls/settlers from the Blender models, portrait camera |
| Blender assets | `blender/generate_assets.py` → `Assets/Art/Models/*.fbx` | house, hut, hall, barracks, tree, oak, rock, soldier, settler, banner, tent (previews in `Art/Previews`) |
| Editor setup | `Assets/Editor/ProjectSetup.cs` | iOS player settings, URP asset, builds `Assets/Scenes/Demo.unity` |
| Checks | `Tools~/SimCheck` | Console runner for the sim: terrain parity with the HTML, village founding, determinism |

## Open it (Unity 6, 6000.0 LTS, with iOS Build Support)

1. Unity Hub > Add project from disk > this `unity` folder. First import takes a few minutes.
2. Menu **Ember Vale > Build Demo Scene**, then open `Assets/Scenes/Demo.unity` and press Play.
3. iPhone: File > Build Profiles > iOS > Switch Platform > Build, then open the Xcode project on a Mac and run it on the device.

The Unity-side code (`View`, `Editor`) has not been opened in a Unity editor yet; the sim core is the part that is tested.

## Run the sim checks (no Unity needed)

```
cd unity/Tools~/SimCheck
dotnet run          # needs the .NET 8 SDK; the terrain parity check also needs node
```

## Regenerate the models

```
pip install bpy==5.2.3        # Blender as a Python module, or use a desktop Blender with `blender -b -P`
python blender/generate_assets.py --out Assets/Art/Models --renders Art/Previews
```

Scale is 1 Blender unit = 1 map tile = 1 Unity unit. Every material is a flat palette colour (see `PALETTE`).
