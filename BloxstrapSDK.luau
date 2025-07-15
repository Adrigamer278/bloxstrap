--!strict

-- BloxstrapRPC Helper Module v1.0.1
-- Written by pizzaboxer (@xtremeguy2256) and Epix (@ElusiveEpix)
-- Types reflected from https://github.com/pizzaboxer/bloxstrap/tree/main/Bloxstrap/Models/BloxstrapRPC
-- Further documentation is available at https://github.com/pizzaboxer/bloxstrap/wiki/Integrating-Bloxstrap-functionality-into-your-game

local HttpService = game:GetService("HttpService")
local ContentProvider = game:GetService("ContentProvider");

local BloxstrapRPC = {}

export type RichPresence = {
	details: string?,
	state: string?,
	timeStart: number?,
	timeEnd: number?,
	smallImage: RichPresenceImage?,
	largeImage: RichPresenceImage?,
}

export type RichPresenceImage = {
	assetId: number? | string?,
	hoverText: string?,
	clear: boolean?,
	reset: boolean?,
}

export type WindowData = {
	x:				number?,
	y: 				number?,
	width:			number?,
	height: 		number?,

	scaleWidth: 	number?,
	scaleHeight: 	number?,

	reset:			boolean?,
}

export type WindowTransparencyData = {
	transparency:	number?;
	color:			Color3|string?;
	useAlpha:		boolean?;
}

--[=[
	Removes any non-number characters from AssetId, useful if the developer is passing in a ID that is taken directly from Instances like Decals or ImageLabels that contain `rbxassetid` or a URL like `http://www.roblox.com/asset/?id=`.
]=]
function SanitiseAssetId(assetId: string|number): number?
	if type(assetId) ~= "string" then
		return assetId
	end

	return tonumber(string.match(assetId, "%d+"))
end

--[=[
	Send a raw RPC message.
	Avoid using this, as every command available will have a dedicated function for it.
]=]
function BloxstrapRPC.SendMessage(command: string, data: any)
	local json = HttpService:JSONEncode({
		command = command,
		data = data,
	})

	print("[BloxstrapRPC] " .. json)
end

--[=[
	Change or reset the user's Rich Presence activity if they are running Bloxstrap with the option enabled.
]=]
function BloxstrapRPC.SetRichPresence(data: RichPresence)
	if data.timeStart ~= nil then
		data.timeStart = math.round(data.timeStart)
	end
	if data.timeEnd ~= nil then
		data.timeEnd = math.round(data.timeEnd)
	end

	if data.smallImage ~= nil then
		data.smallImage.assetId = SanitiseAssetId(data.smallImage.assetId)
	end
	if data.largeImage ~= nil then
		data.largeImage.assetId = SanitiseAssetId(data.largeImage.assetId)
	end

	BloxstrapRPC.SendMessage("SetRichPresence", data)
end

--[=[
	Set the launch data used for invite deeplinks
]=]
function BloxstrapRPC.SetLaunchData(data: string)
	BloxstrapRPC.SendMessage("SetLaunchData", data)
end

--[=[
	These tables hold the cached values for win pos and size
]=]
local prevWinData : WindowData = {};
local prevTransWinData : WindowTransparencyData = {};

--[=[
	These uri point to .png files that exist under the roblox 'content/bloxstrap' directory
	enabled.png will show if the user has the window control setting enabled
	GAMEID.png will only load if the user has given the universe permission to move the window,
	additionally, the pixels in this last image are a true/false, if they are white its a true value, if they are transparent is a false value
	-> first pixel = can the window be moved and resized?
	-> second pixel = can the window change title?
	-> third pixel = can the window be transparent?
	
	These values are just for a sync-settings or to not flood logs, however they require EditableImages which are locked behind ID verification
	so they won't be implemented here by default and might be removed in the future as nothing's final so add a fail-safe or check for the image size
]=]
local bloxstrapEnabledContent = "rbxasset://bloxstrap/enabled.png";
local gameAllowedContent = `rbxasset://bloxstrap/{game.GameId}.png`;

--[=[
	Gets if a FFlag is true/false if that FFlag starts with 'FFlagUser'
	Used in previous versions of the fork, now just kept for compatibility reasons but the FFlag will eventually be removed since user edits on the FFlag list are getting restricted on roblox's side
]=]
local function GetFFlag(flag) : boolean 
	local suc,result:boolean = pcall(function()
		return UserSettings():IsUserFeatureEnabled(flag);
	end)
	
	return suc and result or false;
end

--[=[
	Returns the difference between two tables, used to calculate the difference between the previous and new window data passed
]=]
local function makeDiff(a, b)
	local new = {};
	for k,v in b do
		new[k] = v;
	end

	for k,v in a do 
		if new[k]==v then new[k] = nil end
	end
	return new
end

ContentProvider:PreloadAsync({bloxstrapEnabledContent,gameAllowedContent});

-- Legacy FFlag support, easier this way, it's also a general permission thingy
BloxstrapRPC.UsesBloxstrap = GetFFlag("UserIsBloxstrap") or ContentProvider:GetAssetFetchStatus(bloxstrapEnabledContent) == Enum.AssetFetchStatus.Success;
BloxstrapRPC.AllowsWindowControl = BloxstrapRPC.UsesBloxstrap and (GetFFlag("UserAllowsWindowMovement") or ContentProvider:GetAssetFetchStatus(gameAllowedContent) == Enum.AssetFetchStatus.Success);
BloxstrapRPC.UsesLegacyDetection = GetFFlag("UserAllowsWindowMovement");
BloxstrapRPC.WindowStarted = false; -- Replicates the method whitelist on window control methods when StartWindow is not called (only allows SetTitle)

if game:GetService("RunService"):IsStudio() then -- Change these values when you test your stuff in-studio
	BloxstrapRPC.UsesBloxstrap = true;
	BloxstrapRPC.AllowsWindowControl = true;
end

-- Enabled is applied as a modification, meaning we only have to check if the whitelisted one loads
if not BloxstrapRPC.AllowsWindowControl and BloxstrapRPC.UsesBloxstrap then
	--print("[RPC] Listening for status changed")
	
	local fetchStatusChanged : RBXScriptConnection; fetchStatusChanged = ContentProvider:GetAssetFetchStatusChangedSignal(gameAllowedContent):Connect(function(status : Enum.AssetFetchStatus)
		if status == Enum.AssetFetchStatus.Success then
			BloxstrapRPC.AllowsWindowControl = true;
			--print("[RPC] Allowed window control");
			
			fetchStatusChanged:Disconnect(); 
			fetchStatusChanged = nil; 
		end
	end)
	
	task.spawn(function() -- This has a ~0.3 MS overhead, you are free to increase the wait timer (or find another method) whatever works best
		while task.wait(1.5) and not BloxstrapRPC.AllowsWindowControl do
			ContentProvider:Preload(gameAllowedContent); -- Preload is the only method that forces a re-check and doesn't cache the result
		end
	end)
end

--[=[
	Popups up a window asking the user if they want to allow the game to control the window through Bloxstrap, it can only be used once per session,
	so if the user denies (without marking the dont ask again) or closes the window, they'll have to rejoin, this is to avoid spam from the game
	
	It's recommended that you use a popup (which should only display if bloxstrap is detected) with a simple explanation and let the user click a button to request permssions themselves
]=]
function BloxstrapRPC.RequestWindowPermission()
	if not BloxstrapRPC.UsesBloxstrap or BloxstrapRPC.AllowsWindowControl then return end;
	BloxstrapRPC.SendMessage("RequestWindowPermission", {})
end

--[=[
	Call this when you want to control the window in any way, will save the current window size and position on bloxstrap and will restore them on StopWindow (or game leave),
	also changes the log speed from checking every second to checking on the rate the user has selected (This is what the window control fps setting is for), which means there's more
	work on bloxstrap's side by checking the logs more frequently so it's better to call StopWindow when the window is no longer gonna be moved.
	
	Note: SetTitle will work however it can take up to a full second for the title to be seen (its using the normal log read speed)
]=]
function BloxstrapRPC.StartWindow()
	BloxstrapRPC.WindowStarted = true;
	BloxstrapRPC.SendMessage("StartWindow", {})
end

--[=[
	Resets the log speed to 1s, and resets the window position and size to what it was before StartWindow was called
	It's also called on game leave
]=]
function BloxstrapRPC.StopWindow()
	BloxstrapRPC.WindowStarted = false;
	BloxstrapRPC.SendMessage("StopWindow", {})
end

--[=[
	Sets a window position and size based on a given position, size and the screen size they are based on for scaling on other displays
	This method is optimized to only send data that is different to reduce a big increase in roblox's log size
	Every single method on bloxstrap's side caches the values you pass so you can avoid passing them and save some log size
	
	For testing in studio, it's recommended you redirect this method to move and size a frame that simulates the window container
]=]
function BloxstrapRPC.SetWindow(data:WindowData)
	if not BloxstrapRPC.WindowStarted or not BloxstrapRPC.AllowsWindowControl then return end;

	if data.reset then
		BloxstrapRPC.SendMessage("SetWindow", {reset=true});
		prevWinData = {};
		return;
	end

	data.reset = nil;

	for i,v in data do
		data[i] = math.round(v::number)
	end

	local diff = makeDiff(prevWinData,data)
	if not next(diff) then return end;

	for i,v in diff do
		prevWinData[i] = v;
	end

	BloxstrapRPC.SendMessage("SetWindow", diff)
end

--[=[
	Restores the window to the position and size it was before StartWindow was called,
	also called on bloxstrap on StopWindow
]=]
function BloxstrapRPC.ResetWindow()
	if not BloxstrapRPC.WindowStarted or not BloxstrapRPC.AllowsWindowControl then return end;
	
	BloxstrapRPC.SendMessage("ResetWindow");
	prevWinData = {};
end

--[=[
	Sets the window title if the user has the window control and the title control settings enabled
]=]
function BloxstrapRPC.SetTitle(data: string?)
	if not BloxstrapRPC.AllowsWindowControl then return end;
	
	BloxstrapRPC.SendMessage("SetWindowTitle", data);
end

--[=[
	Same as calling .SetTitle with nil, bloxstrap will reset the title to 'Roblox' when no name argument is passed
]=]
function BloxstrapRPC.ResetTitle()
	if not BloxstrapRPC.AllowsWindowControl then return end;
	
	BloxstrapRPC.SendMessage("SetWindowTitle", nil)
end

--[=[
	Makes the window behave like a chroma key / transparent image, where transparency is actually alpha (1-robloxTransparency) or (0=Fully Transparency and 1=Fully Visible)
	and color is the color that will be keyed, its only the exact color,
	green is usually recommended since its not common and you can easily add a 0.98 transparency black frame to avoid full green color from showing where you dont want to.

	if useAlpha is passed as true, the chroma key color will not be used, additionally due to limitations
	-> If chroma keys are used, the window will be either fully transparent or fully opaque and every pixel the same color of the chroma key will be able to be click through
	-> If alpha is used, the window will be able to have any transparency and will not be able to be click through
]=]
function BloxstrapRPC.SetTransparency(data:WindowTransparencyData)
	if not BloxstrapRPC.WindowStarted or not BloxstrapRPC.AllowsWindowControl then return end;
	
	local diff = makeDiff(prevTransWinData,data)
	if not next(diff) then return end;
	
	for i,v in diff do
		prevWinData[i] = v;
	end
	
	if typeof(diff.color) == "Color3" then
		diff.color = "0x"..diff.color:ToHex();
	end
	
	BloxstrapRPC.SendMessage("SetWindowTransparency",diff)
end

--[=[
	Resets the transparency to 1, making the window fully visible
	Keeps the last sent color and transparency mode (chroma key or alpha)
]=]
function BloxstrapRPC.ResetTransparency()
	if not BloxstrapRPC.WindowStarted or not BloxstrapRPC.AllowsWindowControl then return end;
	
	BloxstrapRPC.SendMessage("SetWindowTransparency", {
		transparency = 1;
	})
	
	prevTransWinData.transparency = 1;
end

return BloxstrapRPC