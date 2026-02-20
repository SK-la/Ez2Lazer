-- Example Skin Script for osu!
-- This script shows how to customize skin components with Lua scripting

-- Script description and metadata
SCRIPT_DESCRIPTION = "基础皮肤脚本示例，展示如何自定义皮肤组件外观和行为"
SCRIPT_VERSION = "1.0"
SCRIPT_AUTHOR = "osu!team"

-- Called when the script is first loaded
-- This is where you can set up any initial state or subscribe to events
function onLoad()
    osu.Log("Skin script loaded!", "info")
    osu.SubscribeToEvent("HitEvent")
    osu.SubscribeToEvent("InputEvent")
end

-- Called when a skinnable component is loaded
-- You can modify components or react to their creation
function onComponentLoaded(component)
    osu.Log("Component loaded: " .. tostring(component), "debug")
    
    -- Example: Make combo counter text larger if it's a DefaultComboCounter
    if component.Type and component.Type.Name == "DefaultComboCounter" then
        if component.CountDisplay then
            component.CountDisplay.Scale = {X = 1.5, Y = 1.5}
            osu.Log("Modified combo counter size", "info")
        end
    end
end

-- Called when a game event occurs
-- Events include things like hit events, misses, combo breaks, etc.
function onGameEvent(eventName, data)
    if eventName == "HitEvent" then
        osu.Log("Hit event received!", "debug")
        -- You can trigger sound effects or visual effects here
        if data.Result and data.Result.Type == "Great" then
            osu.PlaySample("applause")
        end
    end
end

-- Called when a judgement result is received
-- This includes hit results, misses, etc.
function onJudgement(result)
    -- Example: Play a custom sound on perfect hits
    if result.Type == "Perfect" then
        osu.Log("Perfect hit!", "info")
    end
end

-- Called when an input event occurs
-- This includes key presses, mouse clicks, etc.
function onInputEvent(event)
    osu.Log("Input event: " .. tostring(event), "debug")
end

-- Called every frame
-- Use this for continuous animations or effects
function update()
    -- Example: Create pulsing effects or continuous animations
    -- Note: Be careful with performance in this function
end

-- Return true to indicate the script loaded successfully
return true
