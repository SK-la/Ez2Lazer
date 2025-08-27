-- Mania-specific skin script example
-- This script shows how to customize Mania mode skin components

-- Script description and metadata
SCRIPT_DESCRIPTION = "Mania模式特定的皮肤脚本示例，展示如何自定义下落式键盘模式的外观和行为"
SCRIPT_VERSION = "1.0"
SCRIPT_AUTHOR = "osu!team"

-- Cache for column information
local columnData = {}

-- Called when the script is first loaded
function onLoad()
    osu.Log("Mania skin script loaded!", "info")
    osu.SubscribeToEvent("ManiaColumnHit")
    osu.SubscribeToEvent("ManiaHoldActivated")
    
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
                isHolding = false
            }
            osu.Log("Column " .. i .. " has binding " .. columnData[i].binding, "debug")
        }
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
        end
    end
end

-- Called when a game event occurs
function onGameEvent(eventName, data)
    if eventName == "ManiaColumnHit" then
        local columnIndex = data.ColumnIndex
        
        if columnData[columnIndex] then
            columnData[columnIndex].lastHitTime = osu.GetCurrentTime()
            
            -- Example: Create a visual effect when a column is hit
            -- This would require a custom component to be defined elsewhere
            osu.Log("Hit on column " .. columnIndex, "debug")
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
