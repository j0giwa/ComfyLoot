<p align="center">
  <img src="https://raw.githubusercontent.com/j0giwa/ComfyLoot/refs/heads/master/Data/icon.png" width="96" alt="ComfyLoot Logo" />
</p>
<h1 align="center">ComfyLoot</h1>
<p align="center">
  Tracks the contents and market value of items dropped while killing monsters.<br>
  And drops from some other activities such as dungeons, raids, gathering, and maps.<br>
  <br>
  Inspired by <a href="https://wwhttps://github.com/runelite/runelite/wiki/Loot-Trackerw.google.com" target="_blank">Runelite</a>.
</p>
<p align="center">
  <a href="https://github.com/j0giwa/ComfyLoot/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/j0giwa/ComfyLoot?style=flat-square&color=orange"></a>
  <a href="https://github.com/j0giwa/ComfyLoot/releases"><img alt="Downloads" src="https://img.shields.io/endpoint?url=https://qzysathwfhebdai6xgauhz4q7m0mzmrf.lambda-url.us-east-1.on.aws/ComfyLoot?style=flat-square&color=orange&cacheSeconds=300"></a>
  <a href="LICENSE.md"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0--or--later-orange?style=flat-square"></a>
</p>

## Features

![ComfyLoot UI](https://raw.githubusercontent.com/j0giwa/ComfyLoot/refs/heads/master/Data/image1.png)

- Tracks items dropped from enemies and other loot sources
- Displays total loot value (based on market prices, optionally)
- Display metrics in Server Info Bar (optional)
- Works offline by default - API features are **opt-in.**
- Lightweight: minimal memory footprint, no background bloat

## Usage

Upon install, Comfyloot keeps track of every item you acquire over this play session.
`/loot` opens a simple but handy overview.
See [FAQ](#FAQ) for

## Known Issues

- Counters in inventory view may show 0.
- Spelling: I'm dyslexic, so there might be a lot of errors

Report bugs or suggestions via the Issues tab.
Be as concise and reproducible as possible - screenshots/logs help.

## Limitations

- The plugin tracking needs a second to kick in: that's on purpose to prevent login issues (This could resolve after some extensive testing).
- Pickup detection relys on inventory events; they are not 100% reliable.
   - Other methods might break guidelines or are frankly just not acceptable.
- Some functions rely on game memory structures that may or may not break on game patches.
- third-party integrations might fail if endpoints are offline (durr) or busy.

## Roadmap

- Removal of unnecessary code (continuous)
- fixing reported issues

## Contribute

See [Known Issues](#Known_Issues) if you want to help.
Other contributions are generally welcome, but please consider the following...

There are two types of patches: the ones that fit your personal taste and the ones you think should be included in the main.

For patches that fit your personal taste and you want to share with the community, feel free to fork.

For patches that should be included in main, feel free to submit a pull request.
Most of the time this will be patches that increase reliability and improve the codebase.
New features are very likely to be out of scope for this project,
as it's by design intended to be as barebones as possible.
> Simplicity is prerequisite for reliability.
> — Edsger W. Dijkstra

Check the guide in GoatCorp's [SamplePlugin](https://github.com/goatcorp/SamplePlugin) to get started.

## FAQ

``` txt
Q: When will the full version be released?
A: The plugin version will release on the stable branch as soon as it gets classified as "stable".
   This means the plugin has 0 known bugs, and passes one full week of real world testing without any reported issues.

Q: What is this for?
A: I started working on this because i wanted a way to keep track of my stuff during map runs.

Q: Why is ever item valued "N/A"?
A: The Value is N/A if the item is either untradable, or universalis can't be reached.
   This relies on Universalis data, if this feature is disabled the value will aslo be "N/A"

Q: Why is Universalis disabled by default?
A: Even tho this is the whole point of the plugin, this has to be disbabled.
   Im based in the EU, due to privacy laws i have to make this an opt-in feature.
   Nothing i can do about that.
```

## AI usage

> [!WARNING]
> AI Generated Content Disclosure <br><br>
> This project follows a anti AI policy, no AI generated line will ever make it into a stable build.

## Acknowledgments
- Thanks to the [Dalamud](https://github.com/goatcorp/Dalamud) peeps to make this project possible
- Thanks to the Runelite developers for inspiration
- Universalis API for marketboard data
- Thanks to our beloved Ireina for the cute icon
