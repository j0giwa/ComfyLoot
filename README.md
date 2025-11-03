# Comfy Loot

![ComfyLoot Logo](https://raw.githubusercontent.com/j0giwa/ComfyLoot/refs/heads/master/Data/icon.png)
> Icon by our beloved Ireina

Tracks the contents and market value of items dropped while killing monsters. 
And drops from some other activities such as dungeons, raids, gathering, and maps.

Inspired by [RuneLite](https://github.com/runelite/runelite/wiki/Loot-Tracker).

## Features

* Tracks items dropped from enemies and other loot sources
* Displays total loot value (based on market prices, optionally)
* Display metrics in Server Info Bar (optional)
* Works offline by default — API features are **opt-in**
* Lightweight: minimal memory footprint, no background bloat

## Setup

1. Install via Dalamud / XIVLauncher
2. Enable the plugin in-game.
3. (Optional) Enable market price fetching under Settings → Enable Universalis.
4. (Optional) Enable market price fetching under Settings → Enable Server Info bar entry.

ComfyLoot will automatically begin tracking drops once active.

## Usage

Upon install, Comfyloot keeps track of every item you acquire over this play session.
`/loot` opens a simple but handy overview.
See [FAQ](#FAQ) for 

## Roadmap

- Improve loot source classification accuracy
- Removal of unessesary code
- Cleaning up the UI

## Known Issues

- Spelling: I'm dyslexic, so there might be a lot of errors
- Items gained from sources like market boards, Delivery Moogles, and trade (unconfirmed) will count as drops and therefore will get tracked.

Report bugs or suggestions via the Issues tab.
Be as concise and reproducible as possible — screenshots/logs help.

## Known Limitations

- The plugin relies on game memory structures; some updates may temporarily break compatibility.
- API lookups fail if third-party endpoints are offline.

## Contribute

See [Known Issues](#Known_Issues) if you want to help.
Other contributions are generally welcome, but please consider the following...

There are two types of patches: the ones that fit your personal taste and the ones you think should be included in the main.

For patches that fit your personal taste and you want to share with the community, feel free to fork.

For patches that should be included in main, feel free to submit a pull request.
However, it's possible whatever you may want to implement is out of scope for this project, as it's by design intended to be as barebones as possible.
> Simplicity is prerequisite for reliability.
> — Edsger W. Dijkstra

Check the guide in goatcorp's [SamplePlugin](https://github.com/goatcorp/SamplePlugin) to get started.
