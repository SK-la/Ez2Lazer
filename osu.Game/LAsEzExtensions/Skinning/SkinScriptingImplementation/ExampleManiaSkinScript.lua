-- Mania-specific skin script example (practical test effects)
-- Added effects:
-- 1) Hold note alpha: 0.5 -> 1.0 over hold duration
-- 2) Per-column KPS key-light color: green/yellow/red/blue
-- 3) Hide judgement drawable when result is Meh

-- Script description and metadata
SCRIPT_DESCRIPTION = "Mania test script: hold alpha ramp, per-column KPS color, hide Meh judgement"
SCRIPT_VERSION = "1.0"
SCRIPT_AUTHOR = "osu!team"

-- Cache for column information
local columnData = {}

-- Hold note tracking
local holdNotes = {}

-- Column key-light tracking (column -> drawable)
local columnLights = {}

-- Keep recent judgement drawables for fallback hiding
local recentJudgementDrawables = {}

-- Utility
local function clamp(x, minVal, maxVal)
    if x < minVal then return minVal end
    if x > maxVal then return maxVal end
    return x
end

local function colorForKps(kps)
    -- <5: green
    -- 5-6: yellow
    -- 7-8: red
    -- >8: blue
    if kps < 5 then
        return { R = 0.20, G = 1.00, B = 0.20, A = 1.00 }
    elseif kps <= 6 then
        return { R = 1.00, G = 0.90, B = 0.20, A = 1.00 }
    elseif kps <= 8 then
        return { R = 1.00, G = 0.20, B = 0.20, A = 1.00 }
    end

    return { R = 0.20, G = 0.50, B = 1.00, A = 1.00 }
end

local function setDrawableColor(drawable, colour)
    if drawable ~= nil then
        drawable.Colour = colour
    end
end

local function setDrawableAlpha(drawable, alpha)
    if drawable ~= nil then
        drawable.Alpha = alpha
    end
end

local function noteKey(note)
    -- use tostring(note) as a weak identity key for Lua-side tracking
    return tostring(note)
end

-- Called when the script is first loaded
function onLoad()
    osu.Log("Mania skin script loaded!", "info")
    osu.SubscribeToEvent("ManiaColumnHit")
    osu.SubscribeToEvent("ManiaHoldActivated")
    osu.SubscribeToEvent("ManiaHoldReleased")
    
    -- Initialize column data if we're in mania mode
    if osu.GetRulesetName() == "mania" then
        local columnCount = mania.GetColumnCount()
        osu.Log("Mania mode detected with " .. columnCount .. " columns", "info")
        
        -- Store information about each column
        for i = 0, columnCount - 1 do
            columnData[i] = {
                binding = mania.GetColumnBinding(i),
                width = mania.GetColumnWidth(i),
                lastHitTime = 0,
                isHolding = false,
                hitTimes = {}
            }
            osu.Log("Column " .. i .. " has binding " .. columnData[i].binding, "debug")
        end
    end
end

-- Called when a component is loaded
function onComponentLoaded(component)
    if component.Type and component.Type.Name == "ManiaStageComponent" then
        osu.Log("Mania stage component loaded", "info")
        
        -- Here you could modify the appearance of the mania stage
        -- For example, change colors, sizes, etc.
    elseif component.Type and component.Type.Name == "ManiaNote" then
        osu.Log("Mania note component loaded", "debug")
        
        -- You could customize individual notes here
        -- For example, change the color based on the column
        local note = component
        if note.Column ~= nil then
            local columnIndex = mania.GetNoteColumn(note)
            
            -- Example: Apply different styling to different columns
            if columnIndex % 2 == 0 then
                -- Even columns get one style
                note.Colour = {R = 0.9, G = 0.4, B = 0.4, A = 1.0}
            else
                -- Odd columns get another style
                note.Colour = {R = 0.4, G = 0.4, B = 0.9, A = 1.0}
            end

            -- Practical effect #1:
            -- If this is a hold note and has start/end time, initialise alpha at 0.5 and track it.
            if note.EndTime ~= nil and note.StartTime ~= nil and note.EndTime > note.StartTime then
                setDrawableAlpha(note, 0.5)
                holdNotes[noteKey(note)] = note
            end
        end
    elseif component.Type and (component.Type.Name == "LegacyHoldNoteHeadPiece" or component.Type.Name == "LegacyBodyPiece" or component.Type.Name == "LegacyHoldNoteTailPiece") then
        -- Current lazer legacy mania pipeline exposes hold pieces with these types.
        -- Make hold-related pieces semi-transparent for a clear visible test effect.
        setDrawableAlpha(component, 0.55)
    elseif component.Type and (component.Type.Name == "ManiaColumnLighting" or component.Type.Name == "ManiaKeyLighting" or component.Type.Name == "ManiaStageLight") then
        -- Best-effort binding of column key light drawable.
        if component.Column ~= nil then
            columnLights[component.Column] = component
        end
    elseif component.Type and (component.Type.Name == "Judgement" or component.Type.Name == "ManiaJudgement" or component.Type.Name == "JudgementResult") then
        table.insert(recentJudgementDrawables, component)
        if #recentJudgementDrawables > 8 then
            table.remove(recentJudgementDrawables, 1)
        end
    end
end

-- Called when a game event occurs
function onGameEvent(eventName, data)
    if eventName == "ManiaColumnHit" then
        local columnIndex = data.ColumnIndex
        
        if columnData[columnIndex] then
            local now = osu.GetCurrentTime()
            columnData[columnIndex].lastHitTime = now

            -- Practical effect #2: per-column KPS -> key-light color
            local hitTimes = columnData[columnIndex].hitTimes
            table.insert(hitTimes, now)

            -- keep only 1-second window
            local i = 1
            while i <= #hitTimes do
                if now - hitTimes[i] > 1000 then
                    table.remove(hitTimes, i)
                else
                    i = i + 1
                end
            end

            local kps = #hitTimes
            local colour = colorForKps(kps)
            setDrawableColor(columnLights[columnIndex], colour)
            
            -- Example: Create a visual effect when a column is hit
            -- This would require a custom component to be defined elsewhere
            osu.Log("Hit on column " .. columnIndex .. ", KPS=" .. kps, "debug")
        end
    elseif eventName == "ManiaHoldActivated" then
        local columnIndex = data.ColumnIndex
        
        if columnData[columnIndex] then
            columnData[columnIndex].isHolding = true
            
            -- Example: Apply a continuous effect while holding
            osu.Log("Hold started on column " .. columnIndex, "debug")
        end
    elseif eventName == "ManiaHoldReleased" then
        local columnIndex = data.ColumnIndex
        
        if columnData[columnIndex] then
            columnData[columnIndex].isHolding = false
            
            -- Example: End continuous effects when holding stops
            osu.Log("Hold released on column " .. columnIndex, "debug")
        end
    end
end

-- Called when a judgement result is received
function onJudgement(result)
    if result.HitObject and result.HitObject.Column ~= nil then
        local columnIndex = result.HitObject.Column

        -- Practical effect #3: hide judgement drawable for Meh
        if result.Type == "Meh" then
            if result.Drawable ~= nil then
                setDrawableAlpha(result.Drawable, 0)
            else
                -- fallback: hide most recent judgement drawable if explicit drawable isn't provided
                local fallback = recentJudgementDrawables[#recentJudgementDrawables]
                setDrawableAlpha(fallback, 0)
            end
            return
        end
        
        -- Example: Play different sounds based on column and hit result
        if result.Type == "Perfect" then
            osu.Log("Perfect hit on column " .. columnIndex, "info")
            
            -- Example: Custom sound per column
            if columnIndex % 2 == 0 then
                osu.PlaySample("normal-hitnormal")
            else
                osu.PlaySample("normal-hitwhistle")
            end
        end
    end
end

-- Called when an input event occurs
function onInputEvent(event)
    -- Example: Map keyboard events to column effects
    if event.Key then
        -- Check if the key corresponds to a column binding
        for i = 0, #columnData do
            if columnData[i] and columnData[i].binding == tostring(event.Key) then
                osu.Log("Input detected for column " .. i, "debug")
                
                -- Here you could create custom input visualizations
                -- This is especially useful for key overlay effects
            end
        end
    end
end

-- Called every frame for continuous effects
function update()
    local currentTime = osu.GetCurrentTime()

    -- Practical effect #1 runtime update:
    -- While hold note is active, alpha interpolates 0.5 -> 1.0 by progress.
    for key, note in pairs(holdNotes) do
        if note == nil or note.StartTime == nil or note.EndTime == nil or note.EndTime <= note.StartTime then
            holdNotes[key] = nil
        else
            if currentTime < note.StartTime then
                setDrawableAlpha(note, 0.5)
            elseif currentTime > note.EndTime then
                setDrawableAlpha(note, 1.0)
                holdNotes[key] = nil
            else
                local progress = clamp((currentTime - note.StartTime) / (note.EndTime - note.StartTime), 0, 1)
                setDrawableAlpha(note, 0.5 + 0.5 * progress)
            end
        end
    end
    
    -- Example: Create pulsing effects on recently hit columns
    for i = 0, #columnData do
        if columnData[i] then
            local timeSinceHit = currentTime - columnData[i].lastHitTime
            
            if timeSinceHit < 500 then -- 500ms of effect
                -- Calculate a decay effect (1.0 -> 0.0 over 500ms)
                local intensity = 1.0 - (timeSinceHit / 500)
                
                -- Here you would apply the effect to column visualizations
                -- Example: column.Glow = intensity
            end
            
            -- Apply continuous effects to held columns
            if columnData[i].isHolding then
                -- Example: Create pulsing or glowing effects while holding
                -- local pulseAmount = math.sin(currentTime / 100) * 0.2 + 0.8
                -- column.HoldEffectIntensity = pulseAmount
            end
        end
    end
end

-- Return true to indicate the script loaded successfully
return true
