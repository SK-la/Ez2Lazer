# NOTICE — PerformancePlus (PP+) skill axes

Ez2Lazer’s osu!standard skill radar targets **PerformancePlus (PP+)** axes
(Jump Aim / Flow Aim / Precision / Speed / Stamina / Accuracy, plus Aim Total),
as described at <https://syrin.me/pp+/>.

Realm system ids: `beatmap_ppplus` (chart), `player_ppplus` (player).

## Current status

`IEzPpPlusEngine` is currently backed by **`EzPpPlusStubEngine`**: MIT heuristics from official difficulty attributes for wiring / UI only. Values are **not** PerformancePlus ratings.

## Before swapping in a real engine

Using a third-party PP+ (or similar) algorithm requires prior authorisation from the rights holder. Until then, keep the stub; replace by implementing `IEzPpPlusEngine` and bumping `EzOsuSkillAlgorithm.VERSION`. Chart / player writers, HUD, and SystemIds stay stable.
