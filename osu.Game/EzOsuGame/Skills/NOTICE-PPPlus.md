# NOTICE — PPP (PP+) skill metrics

## Naming and expectations

**PPP** stands for **PP Plus**. The label is widely used and understood in the community, so Ez2Lazer keeps it. The PPP pipeline in this client **will keep evolving** (including Ez2Lazer-specific changes and fixes), so **values need not match** other tools, APIs, or earlier builds.

The **mathematical formulas** used here are **distilled from** [Syriiin/osu](https://github.com/Syriiin/osu). **Integration with Ez2Lazer** (runtime code, Realm storage, background compute, and coupling to ppy/osu beatmaps) is **implemented in this fork’s own way**; upstream project sources are not vendored.

## What Ez2Lazer ships

| Ruleset | PPP axes (chart / player) |
|---------|---------------------------|
| osu! | Aim, Jump Aim, Flow Aim, Precision, Speed, Stamina, Accuracy, accuracy difficulty, star rating |
| taiko | Rhythm, Colour, Stamina, star rating |
| catch | Movement, star rating |
| mania | Strain, star rating (player rows by key count) |


Mania default skill radar remains MSD/SunnySR; PPP radar display modes are optional in HUD / song select.

## License

Ez2Lazer is distributed under the repository **MIT License**. Additional provenance may appear in README or ACKNOWLEDGEMENTS; LICENSE itself is unchanged.
