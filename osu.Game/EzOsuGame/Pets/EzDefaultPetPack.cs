// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Pets
{
    public static class EzDefaultPetPack
    {
        public const string NAME = "Default";

        public const string PET_JSON = """
            {
              "defaultState": "idle",
              "clips": {
                "idle": { "frames": "idle_{00}", "fps": 12, "loop": true },
                "hover": { "frames": "hover_{00}", "fps": 12, "loop": true },
                "poke": { "frames": "poke_{00}", "fps": 15, "loop": false },
                "starEasy": { "frames": "star_easy_{00}", "fps": 12, "loop": false },
                "starHard": { "frames": "star_hard_{00}", "fps": 12, "loop": false },
                "enter": { "frames": "enter_{00}", "fps": 12, "loop": false },
                "combo50": { "frames": "combo50_{00}", "fps": 12, "loop": false },
                "combo300": { "frames": "combo300_{00}", "fps": 12, "loop": false },
                "miss": { "frames": "miss_{00}", "fps": 12, "loop": false },
                "idlePlay": { "frames": "idle_play_{00}", "fps": 10, "loop": false },
                "idleYawn": { "frames": "idle_yawn_{00}", "fps": 8, "loop": false },
                "idleSleep": { "frames": "idle_sleep_{00}", "fps": 6, "loop": true }
              },
              "states": {
                "idle": { "clip": "idle" },
                "hover": { "clip": "hover" },
                "poke": { "clip": "poke", "next": "idle" },
                "starEasy": { "clip": "starEasy", "next": "idle" },
                "starHard": { "clip": "starHard", "next": "idle" },
                "enter": { "clip": "enter", "next": "idle" },
                "combo50": { "clip": "combo50", "next": "idle" },
                "combo300": { "clip": "combo300", "next": "idle" },
                "miss": { "clip": "miss", "next": "idle" },
                "idlePlay": { "clip": "idlePlay", "next": "idle" },
                "idleYawn": { "clip": "idleYawn", "next": "idle" },
                "idleSleep": { "clip": "idleSleep" }
              },
              "starBands": [
                { "min": 0, "max": 2, "goto": "starEasy" },
                { "min": 4.5, "max": 99, "goto": "starHard" }
              ],
              "rules": [
                { "when": "hover", "goto": "hover", "interrupt": true },
                { "when": "hoverEnd", "goto": "idle" },
                { "when": "click", "goto": "poke", "interrupt": true },
                { "when": "gameplayEnter", "goto": "enter", "interrupt": true },
                { "when": "combo", "at": 50, "goto": "combo50" },
                { "when": "combo", "at": 300, "goto": "combo300" },
                { "when": "miss", "goto": "miss", "interrupt": true },
                { "when": "idle", "afterSeconds": 300, "goto": "idlePlay" },
                { "when": "idle", "afterSeconds": 600, "goto": "idleYawn" },
                { "when": "idle", "afterSeconds": 900, "goto": "idleSleep" }
              ]
            }
            """;
    }
}
