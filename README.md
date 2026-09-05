# navy 1851

[![build](https://github.com/raya-ac/navy1851/actions/workflows/build.yml/badge.svg?branch=vs1.22.3)](https://github.com/raya-ac/navy1851/actions/workflows/build.yml)

i wanted a colt navy in vintage story, with a model that actually looked like one. this is my take on the 1851: a long octagonal barrel, engraved cylinder, brass frame and a curved walnut grip.

it holds six rounds. hold left mouse and it keeps firing until you let go or run out. hold right mouse to aim down the sights for a tighter shot. sneak and hold right mouse to load it again, using only as many charges as it needs. the ammo count stays with the gun when you move it or save the world.

this branch is for **vintage story 1.22.3**. download the zip ending in `vs1.22.3.zip`; the 1.22.7 build is a separate download. it builds against the real game API and has automated checks. i've started testing the held model in the 1.22.3 Windows client. this is still a prerelease: sight alignment, continuous input and multiplayer need more testing.

## get it

download the mod zip from [releases](https://github.com/raya-ac/navy1851/releases) and put it in your `VintagestoryData/Mods` folder. leave the zip intact.

builds from individual commits are also available under [actions](https://github.com/raya-ac/navy1851/actions/workflows/build.yml). those downloads have an outer archive; extract it to get the mod zip.

use the same version on the server and clients, and remove an older copy first. search creative inventory for **Colt 1851 Navy** and **Navy paper charge**.

## controls

| input | what it does |
| --- | --- |
| hold left mouse | cocks for 0.35 seconds, then fires every 0.7 seconds until empty |
| hold right mouse | aims down sights; accuracy tightens over 0.4 seconds |
| release left mouse | stops firing |
| sneak + hold right mouse | fills the missing chambers after 1.4 seconds |

charges need to be in your inventory. a reload takes only the charges needed to fill the cylinder. if you have fewer, it loads those. letting go before the reload finishes consumes nothing.

shots deal 10 piercing damage with a 40-block range. the server handles ammo and damage, and checks PvP settings, attack permissions and land claims. crafting uses the game's existing materials. the loading and firing are simplified for playing; this isn't a historical simulation.

## build it

you need **python 3.10+** and the **.NET 10.0.400 SDK** (or a later patch in that SDK band).

```sh
python3 tools/build.py
```

that downloads the pinned official 1.22.3 server archive, verifies its checksum, takes the three reference DLLs it needs, builds the mod, runs the checks and writes the installable zip to `dist/`. it doesn't launch the server or bundle the game's DLLs. no NuGet packages are needed.

if you already have the game installed:

```sh
python3 tools/build.py --game-path "/path/to/Vintagestory"
```

on Windows, use `python` if `python3` isn't available. GitHub Actions runs the same build command on every push to `main` or `vs1.22.3`, on pull requests, and when started manually.

## the model

the model is [a native Vintage Story shape](assets/navy1851/shapes/item/navy1851.json). [build_assets.py](tools/build_assets.py) generates the geometry, textures and sounds; changing the grip profile there updates the actual game model. rebuilding the artwork needs Pillow, NumPy and libsndfile. those tools aren't needed for a normal mod build because the assets are already included.

```sh
python3 tools/build_assets.py
python3 tools/build.py
```

[changes](CHANGELOG.md)
