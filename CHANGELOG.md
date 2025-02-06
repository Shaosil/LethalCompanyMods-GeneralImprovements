# Changelog

### v1.4.5 - Compile Update and minor Fixes
* Recompiled using the latest version of LC's DLL to ensure full compatibility with v69 (nice).
* Fixed flashlight bulb material always using the "dark" version even when it was turned on.
* Fixed health monitors not updating properly on clients, and showing incorrect health values for dead players.
* Merged PR #236 (Thanks [YoshiRules](https://github.com/YoshiRulz)) that adds the control pad to the list of non conductive scrap.

### v1.4.4 - More fixes and improvements
* Removed an outdated and error prone way of migrating old config values, which should also fix potential errors during migrations.
* Fixed overtime bonus monitor calculation not working with quota rollover.
* Fixed daily scrap, external scrap, and overtime bonus monitors not updating when things are sold at the company.
* Fixed daily and external scrap not updating consistently.
* Fixed MinimumStartingMoney allowing a value of -1 and potentially causing errors.
* Added an option to center the signal translator text. Defaults to true.
* Added an option to keep the terminal's credits display fit inside of its dark green background box. Defaults to true.
* Added another sneaky mask option to allow them to have a configurable chance to display a scrap icon on the map radar, as if they are holding something (though they are not).
* The Med Station ship object may now be placed and moved via ship build mode.
* Updated the ship scrap monitor to include the number of scrap items on the ship.
* Updated the MonitorsAPI for other developers:
	* Fixed GetMonitorAtIndex returning things in some situations even if UseBetterMonitors was false.
	* Fixed NewMonitorMeshActive not working.
	* Added NumMonitorsActive.
	* Added a QueueRender function to the MonitorInfo object.

### v1.4.3 - More fixes and improvements
* Fixed a few things with StartingMoneyPerPlayer and MinimumStartingMoney:
	* MinimumStartingMoney now works without having to be paired with StartingMoneyPerPlayer, if you want to use it as a hardcoded money setting for new saves.
	* Fixed a bug that would subtract the value twice every time a client disconnected.
	* Fixed a bug that would refill the credits if another player joined after a ship unlockable was purchased, or if the tracked credits were negative.
	* Fixed a bug that would throw an error if the tracked credits went negative (background tracking to prevent purchase exploits).
* Fixed a small Time monitor bug that would not initialize properly when quickly exiting out during a level load and reloading the save file.
	* As part of this fix I chose to remove the 1 second update timer requirement for that monitor, since the new rendering system will never render the same thing anyway.
* Remove AllowLookDownMore from the config and code since it has been implemented as of v64.
* Fixed toilet paper being marked as conductive.
	* Removed flask, plastic cup, and whoopie cushion's conductivity fixes since they are now in v64 vanilla.
* Made the CompanyBuyRate monitor more compatible with [BuyRateSettings](https://thunderstore.io/c/lethal-company/p/MoonJuice/BuyRateSettings/)
	* The monitor will still briefly show an "incorrect" buy rate for a short duration after a quota is met due to how BuyRateSettings updates it.
* Fixed AllowPreGameLeverPullAsClient not working for clients if a new moon was travelled to before the game start.
* Fixed ScanPlayers only working if FixPersonalScanner was also true.
* Fixed a bug where PlayerHealth monitors would not update after a player was killed (mostly affected PlayerHealthExact).
* Fixed a bug where the OvertimeCalculator monitor would not update while in orbit over the company.
* Fixed a bug where the ScrapLeft monitor would display an incomplete value for clients on very large moons from calculating before the scrap was fully loaded.
* Potentially fixed a very rare edge case when using certain regional PC settings with Gale mod manager that would interpret weather multiplier values incorrectly and clamp them.

### v1.4.2 - Performance hotfix
* Fixed a performance bug that remained from v1.3.8 when AddMoreBetterMonitors was set to false.

### v1.4.1 - Hotfix
* Fixed a bug with AddMoreBetterMonitors not working properly with the new monitor rendering system.

### v1.4.0 - More monitors, better performance, and weather multipliers
* Further optimized the better monitor rendering system, resulting in a decent performance boost when UseBetterMonitors = true.
* Added a "Company Buy Rate" monitor.
* Added a "Daily Profit" monitor. (Yer an Employee, Harry!)
* Added an "Average Daily Scrap" monitor.
* Added an "Overtime Calculator" monitor.
* Added config options to have loot and amount multipliers based on the current weather.
	* Defaults to the vanilla values everywhere, but recommended config strings are in the description.
	* It should also support custom weathers if you specify the correct name.
	* This will affect the apparatus as well but does it earlier than other mods (like [FacilityMeltdown](https://thunderstore.io/c/lethal-company/p/loaforc/FacilityMeltdown/) so they take precedence.)
* Fixed the internal ship cam still showing underneath a canvas if UseBetterMonitors was set to false and a custom monitor replaces it.
* Fixed challenge files not properly resetting several GI related stats and monitors
* Fixed the plastic cup, soccer ball, and zed dog being conductive.

### v1.3.8 - Hotfix
* Fixed the sales monitor breaking in v1.3.7

### v1.3.7 - More fixes and improvements
* Added a config option to disable the automatic collection of dead player bodies when teleporting them to the ship, and attempted to fix the collection being spammed with certain mod conflicts.
* Terminal command history now supports a minimum of 2 characters to store instead of 3, meaning it will save door/mechanism commands as well.
* Fixed the PlayerHealthExact monitor not working if there were no PlayerHealth monitors.
* Fixed word wrapping and overflow with long player names in player health monitors.
* Fixed the chat opacity config settings not being applied when using the ship terminal.
* Fixed masked entities having a scan node created when MaskedEntitiesShowPlayerNames was true but ScanPlayers was false. Both need to be set to true for them to be "scannable" as players.
* Fixed the danger level monitors being constantly inaccurate.
* Fixed a big vanilla performance problem related to audio reverb triggers continually searching for a gameobject, resulting in a 5-10x performance boost in certain cases.
	* This is fully compatible with [ReverbTriggerFix](https://thunderstore.io/c/lethal-company/p/JacobG5/ReverbTriggerFix/) as they technically optimize different things.
* Potentially fixed a rare edge case in which the sales monitor would throw an exception when trying to update.
* Potentially fixed a rare exception with storing terminal command history when submitting a command.

### v1.3.6 - Hotfix
* Fixed the sell counter not accepting items if SellCounterItemLimit was greater than 127.
* Fixed the new PlayerHealth and PlayerHealthExact monitors not working propertly if UseBetterMonitors was set to false.
* Fixed the new PlayerHealth and PlayerHealthExact monitors not syncing player names until later.

### v1.3.5 - More fixes and improvements
* Fixed an alignment bug with the ship cupboard when placing items when ShipPlaceablesCollide was set to false.
* Fixed dropped items not colliding with ship placeables if ShipPlaceablesCollide was set to false.
* Fixed a bug where the blue monitor backgrounds still showed up when both UseBetterMonitors and ShowBlueMonitorBackground were set to false.
* Fixed players rotation not being immediately synced when clients join a lobby.
* Fixed the ship light and monitor power status not being synced when a client joins.
* Hopefully fixed the danger level monitor showing LETHAL after taking off from a moon.
* Updated SellCounterItemLimit to support up to a value of 999 instead of 100.
* Added PlayerHealth and PlayerHealthExact monitor options.
	* PlayerHealth will only show green, yellow, and red icons per player without revealing whether they are dead or not.
	* PlayerHealthExact will show the exact amount of health each player has, meaning you can deduce their living status.
	* The monitors update when the usual player damage and revived functions are called. If other mods change health outside of those areas, the monitors may sometimes be out of sync.
	* I had to resize the font of whatever monitors use these options, so I recommend displaying them on a large monitor unless you have an HD mod.
		* This also means I had to upscale the better monitors resolution a bit so you could make out the text. Don't forget to replace the assets file if you manually update.
* Added a bit of color to some of my monitors' texts.
* Added a warning to the AutoSelectLaunchMode setting description to inform people that it may prevent certain other mods from initializing properly.

### v1.3.4 - External cam hotfix
* Fixing a bug I introduced in v1.3.3 where the external cam monitor would no longer function when using better monitors.

### v1.3.3 - Reflection Removal and a couple new features
* Removed the usage of System.Reflection methods from everything after transpilers run during startup. This may result in a slight performance boost in some areas.
* Updated the config option for rotating the ship map cam to support all 4 directions instead of just north or nothing.
* Added a new monitor option: Danger Level. This will reflect the approximate danger level (Safe, Warning, Hazardous, Dangerous, Lethal) in your current area.
	* The danger level is relative to the current moon. Lethal on Experimentation is much different than lethal on Titan.

### v1.3.2 - More fixes and improvements
* Added an option to configure the ship' clipboard starting on the wall instead of the table (defaults to start on the wall).
* Added an option to adjust the chat's fade duration and opacity.
	* Modifying these options will not work if [HideChat](https://thunderstore.io/c/lethal-company/p/Monkeytype/HideChat/) is also installed (that mod will simply overwrite GI's behavior).
* Fixed a bug with FixPersonalScanner where an error would be spammed in the log in certain situations.
* Sorted all config sub-entries alphabetically to make future option finding easier (hopefully).

### v1.3.1 - Performance improvements, bug fixes, and more general improvements
* Improved performance when FixPersonalScanner = true.
	* In doing so, I also removed the ScanHeldPlayerItems config option since some of the optimizations would have required that to be recoded.
* Added an option to configure the existing improvement that allows item pickup before game start (defaults to true).
* Improved compatibility with [CodeRebirth](https://thunderstore.io/c/lethal-company/p/XuXiaolan/CodeRebirth/) item crates when using UnlockcDoorsFromInventory or KeysHaveInfiniteUses.
* Fixed a bug with FixInternalFireExits not working after v55.
* Fixed the "Grab" hovertip showing too far away from grabbable items if MaskedEntitiesShowPlayerNames was set to true.
* Fixed the bug where if lightning strikes the ship while the monitors are off (while using SyncExtraMonitorsPower), they won't turn back on until lobby restart (for realsies this time).
* Patched Harmony's transpiler method to fix rare cases where certain methods result in unexpected behavior when transpiled (thanks for pointing me in the right direction DiFFoZ).

### v1.3.0 - v56 Support, fixes, and cleanup
* Added compatibility for v56+ (thanks for the PR help 1A3Dev!).
* Removed "error" logs when targeting masked entities that I left over from debugging.
* Fixed the KeepItemsDuringInverse not working (thanks for the PR EugeneWolf!).

### v1.2.8 - Fixes and masked settings
* Fixed ShowBackgroundOnAllScreens not working with UseBetterMonitors after the refactor.
* Fixed a bug where lightning strikes (and potentially just turning off the monitors) would sometimes softlock their power state to off until a lobby restart.
* Updated the masked entity blend options once again to be a few separate settings instead of one confusing dropdown.
	* Added an option to stop the masked entity's radar icon from spinning.
	* Added an option to prevent masked entity arms from reaching out when chasing players.
	* Added an option to display a masked entity's targeted player name above their head when not using HidePlayerNames.
		* This option will also allow the masks to be scannable now instead of ScanPlayers, and they will continue to impersonate a target player.
* Fixed scanned masked players always saying "Deceased" instead of their respective health when using MaskedEntitiesShowPlayerNames.
* Fixed bought furniture being saved after players were fired when re-hosting the game if SaveShipFurniturePlaces = All.
* Fixed moon prices not showing with hidden moons when applicable.
* Added infinite sprint if debug tools and invincibility are enabled (mostly so I can test more easily with [LethalDevMode](https://thunderstore.io/c/lethal-company/p/megumin/LethalDevMode/)).

### v1.2.7 - Refactor and compatibility
* Refactored how the better monitors get initialized, since the code complexity had slowly been increasing with each update. There should be no noticeable changes on users' ends.
* Updated compatibility with [FacilityMeltdown](https://thunderstore.io/c/lethal-company/p/loaforc/FacilityMeltdown/) so the custom hover tip will be displayed on the apparatus when it isn't grabbable.

### v1.2.6 - More fixes and improvements
* Added an option to use better monitors without adding the extra left hand group of monitors. Defaults to false (uses extra monitors by default if UseBetterMonitors = true).
	* Will smoothly keep the same ship monitor settings for you if you were previously NOT using better monitors because of the additional monitor group.
* Updated better monitor render optimization's "in ship" check to include spectated player if applicable.
* Fixed the bottom two right monitors having text cut off if UseBetterMonitors = False.
* Updated the gold bar's resting position to lay on its bottom instead of its side.
* Updated compatibility with [OpenBodyCams](https://thunderstore.io/c/lethal-company/p/Zaggy1024/OpenBodyCams) so it should no longer overwrite the bodycam monitor material.
	* Should also work with other mods that overwrite monitor materials, even when syncing from host.
* Fixed the internal and external ship cams resetting to their default 1x resolution and vanilla FPS if you exit and re-enter a game.
* Updated the MaskedLookLikePlayers setting to a MaskedEntityBlendLevel enum that allows more options for mask entity stealth.
* Updated the save ship furniture config setting to include an option for "All", which should hopefully keep placements of ALL bought unlockables even after reset (once repurchased).
* Fixed an issue where better monitor text would spill over or the canvas was cut off when another mod changed the camera's FoV (such as [Imperium](https://thunderstore.io/c/lethal-company/p/giosuel/Imperium/)).
* Updated the profit quota monitors to always use 4 lines when using custom monitor settings to avoid potential word wrap issues.
* [Misc Tech Debt]
	* Updated the plugin initialization to use one instance of Harmony for patching instead of a separate instance for each patch.
	* Updated the monitor code when using internal/external cams so it will now use the correct reference mesh for visibility checks.
		* The external cam above the door controls MAY not update properly in some cases if there is no internal cam monitor. It's the nature of the vanilla cam rendering scripts.
	* Moved some of the grabbable objects' item properties code to a better location.

### v1.2.5 - More fixes and improvements
* Added a manual timer to the better time monitors to help with frame loss when using mods that increase the time display update cycles.
* Added an option to save the last used suit in the current save file, which will persist between loads and being fired. Defaults to true.
	* Only works if using Online mode, not LAN (it associates suit IDs to Steam IDs in your save files).
	* Should be compatible with all suit mods.
	* Saves every player's last known suit, so clients connecting to future games in the same file will have their last suit applied.
	* Coded carefully to avoid errors in case of mod differences or changes.
* Added an option to save the furniture position per save file, which will prevent their positions and storage states changing each time players are fired. Defaults to true.
* Added an option to display the hidden moons on the terminal's moon list. Defaults to AfterDiscovery.
	* If the host also has the mod, they will also sync their own already found moons when you join their lobby.
* Added an option to be able to scan other players. Masked entities will also blend in nicely when scanned. Defaults to false.
* Added an option to let masks blend in more visually, meaning they would look exactly like a normal player, including having different suits. Works with MoreCompany cosmetics. Defaults to false.
* Improved compatibility with [WeatherTweaks](https://thunderstore.io/c/lethal-company/p/mrov/WeatherTweaks/) when using certain Terminal config options.
* Fixed a bug that softlocked the game in certain situations if a client tried to start the game before the host (when using AllowPreGameLeverPullAsClient).
* Added an option to change the menu music volume percentage. Defaults to 100.

### v1.2.4 - More fixes and improvements
* Fixed a rare softlock where using QuotaRollover would not detect the quota was reached on day 0.
	* Also, quota rollover will now support selling items before the final day - if you exceed the target quota afterwards, a new one will be assigned as expected.
* Fixed a major lag spike that occurred as a client if you had SyncMonitorsFromOtherHost set to true AND the internal/external cam resolution multiplier high.
* Finally supporting [LethalConfig's](https://thunderstore.io/c/lethal-company/p/AinaVT/LethalConfig/) dropdown options for my config values. Especially useful for ship monitors and keybinds!
* Added an option to disable "blank screen" during view monitor, which typically requires you to scroll down after typing a comcmand while view monitor is active. Defaults to show (vanilla behavior).
* Tweaked the original "n" fix in the terminal to use the intended number of lines so you can see the first line at the bottom.
* Added an option to have placeable ship items check for other colliders, preventing tight/overlapping placement. Defaults to true (vanilla behavior).
* Updated the DropShipItemLimit behavior to allow terminal purchases of up to that amount, instead of a max of 10.
* Updated the starting money config values to take any typed value instead of being clamped to a slider up to 1k. It will still prevent values above 10k internally though.
* Fixing items on sell counter appearing to be grabbable. This makes placing items on the counter much easier.
* Fixed general grabbables' hover tips showing when they can't be grabbed (Nutcracker shotgun, deposit items desk, etc).
* Updated wording of teleporters' button glass lids to be dynamic ("Shut" or "Lift" depending on the state).

### v1.2.3 - Hotfix!
* Hopefully fixing a game load exception that was causing items to be lost on load. Sorry!
* Fixing the player's max look down angle when AllowLookDownMore = true, since the last update broke it with all the refactoring.

### v1.2.2 - More fixes and improvements
* Refactored the transpilers to be more compatible with other mods that may modify the same functions.
* Added extension ladders and knives to the tools that are non conductive when ToolsDoNotAttractLightning = true.
* Fixed exploded grenades (and eggs) counting towards monitor scrap calculations.
* Fixed new save files always using 0 for a map seed before starting the game, which caused sales, weather, and other things to always be the same.
* Fixed romantic table rotation not lining up perfectly for 45 degree snapping increments.
* Fixed "ghost outlines" being incorrectly shown when using snapping during build mode.
* Added an option to destroy keys in inventory (and ship if host) after orbiting. Pairs well with KeysHaveInfiniteUses. Defaults to false.
* Added an option to use a 24 hour clock instead of 12. Defaults to false.
* Added an option to have the clock always show on the HUD when landed on a moon. Defaults to false.
* Added an option to display kg instead of pounds (yes, it converts). Defaults to false.

### v1.2.1 - Performance and compatibility improvements
* Cleaned up certain technical pieces of code that may have been causing slight optimiziation issues.
* Ensured that keeping vanilla teleporter cooldown options does NOT modify the teleporter cooldown code at all, thus ensuring compatibility with mods that do.
* Fixed a small bug that would make a shifted inventory item invisible on the HUD when using a key in another slot.
* Optimized the code for UnlockDoorsFromInventory to have less performance impact.
* Fixed landmines retaining their scan node after they explode.
* Fixed FixItemsLoadingSameRotation only working when FixItemsFallingThrough was enabled.
* Fixed a bug where starting credits would be incorrect after player(s) left or rejoined after anyone bought something.
* Fixed an old bug with my teleporter fix no longer collecting dead bodies as scrap.
* Fixed dead bodies being counted as scrap on certain monitors.

### v1.2.0 - FlashlightFix implemented
* Brought over the code from my other mod, the now deprecated [FlashlightFix](https://thunderstore.io/c/lethal-company/p/ShaosilGaming/FlashlightFix/).
	* Although it's recommended to remove the other mod if you're using it, I did put in detection methods to avoid conflicts if needed.
* FlashlightFix introduces the following changes:
	* You may configure a shortcut key to toggle on/off a flashlight at any time (defaults to None).
	* Flashlights/helmet lights no longer turn off when picking up an additional inactive flashlight.
		* It will now turn on the new flashlight, and turn off the old one. If the new one is out of batteries, the old one will stay on. This behavior may be disabled in the config.
	* Multiple flashlights can no longer be active at the same time. This behavior may be disabled in the config.
	* Laser pointers are no longer treated as flashlights. This behavior may be disabled in the config.
	* Flashlights will no longer toggle themselves when using/switching items and picking things up
		* As a result, they will not drain battery randomly while not truly on.
* Fixed spray can items saving/loading in a single file instead of per save.
	* If you had some saved, this will cause a one-time reroll of the colors.
* Added an option to save item rotations per save file so they aren't all facing the same way when you load back in (defaults to true).

### v1.1.15 - More fixes and improvements
* Made the spray paint can color more random when they spawn, instead of using the same color per map seed.
* Saving the spray paint can colors in the save files so they are no longer randomized each load.
* Fixed landmines remaining on the map screen after detonating.
* Added an option to remove the ship's storage cabinet's doors (defaults to false).
* Added an option to display each moon's route cost in the terminal moons screen (defaults to false).

### v1.1.14 - Small hotfixes
* Fixed the deadline monitor not working with the old style monitors.
* Updated the sync monitors to host option to NOT overwrite existing configs, so clients will keep their old settings when hosting their own game.
* Fixed old style monitor text updates not being detected for debug logging.

### v1.1.13 - More fixes and improvements
* Added an option to sync all monitor settings from host (only works if they are using GeneralImprovements, obviously). Will use your configured settings otherwise, or when hosting. Defaults to off.
* Added an option to add scan nodes to fire entrances and all exits. Compatible with [mimic mod!](https://thunderstore.io/c/lethal-company/p/x753/Mimics/) (Will say "Fire Exit?" instead of "Fire Exit"). Defaults to off.
* Fixed the monitors staying out of date if they tried to update when you are outside of the ship. They will now refresh once you re-enter the ship.
* Fixed the spray paint can spawning with debug "error" messages.
* Fixed some instances of "destroyed" inventory items not applying the inventory shifting code, if active.
* Using a more reliable method to disable overtime bonus when AllowOvertimeBonus = false.
* Improved the behavior of AlwaysShowNews when set to false.

### v1.1.12 - More small fixes and improvements
* Hosts now have an option to disable overtime bonuses (defaults to false).
* No longer doing any render calls for better monitors if the player is outside of the ship. May be disabled in config (for example if you want to see udpates when spectating).
* Updated wording of ship lever when disabled to be "[Ship in motion]" (instead of "[Wait for ship to land]", which also shows when taking off).
* Fixed some cases of spray paint colors being different between host and clients.

### v1.1.11 - Hotfix
* Updated the ship scan text to say 'exact' or 'approximate' based on the value of ScanCommandUsesExactAmount in the config.
* Fixed the AllowPreGameLeverPullAsClient setting softlocking the game if a client tried to use it.
* Fixed the internal and external materials not being created in some cases with better monitors (namely when [LethalExpansion](https://thunderstore.io/c/lethal-company/p/HolographicWings/LethalExpansion/) was loaded).

### v1.1.10 - More fixes and improvements
* Added an option to allow yourself to pull the ship starting lever as a client, before the game starts (defaults to true).
* Added an option to allow the dropship to be scanned (defaults to false).
* Updated HidePlayerNames's behavior to only hide names when the ship is not in orbit.
* Added a "NonScrap" option to the KeepItemsDuringTeleport and KeepItemsDuringInverse config options.
* Added "Quota #" to the things that get synced on client connect.
* Optimized the door power monitor so it doesn't drain FPS when active.
* Fixed the sales not showing up for the host player until the 2nd day.
* Fixed a minor potential bug when shifting item slots around when not holding anything.
* Fixed carry weight after teleporting while retaining select items.
* Fixed vanilla behavior of scan nodes not using their collider sizes when doing line casts.
* Fixed an exception when calculating outside scrap if a scrap's min value was greater than its max.
* Removed DLLs from source control (just copy what you need manually from the LethalCompany directory if you build it yourself)

### v1.1.9 - More fixes and improvements
* Fixed DaysSinceLastDeath not resetting to "no deaths yet" after players are fired.
* Fixed better monitors offsets, which was most noticeably incorrect when left aligning text.
* Added an option to prevent keys from despawning when they are used.
* No longer defaulting UnlockDoorsFromInventory to true, since it may conflict with other mods (such as ImmersiveCompany).
* Added an option to hide player names.
* Added options to keep None, Held, or All inventory items when using the teleporters (configurable per teleporter, defaults to "None" in both cases).
	* WARNING: Keeping items will probably cause inventory desyncs if all other players do not share your same setting!

### v1.1.8 - More fixes and improvements
* No longer attempting to pluralize sales items on the sales monitor.
* Centered the sales monitor text vertically.
* Slowed the sales monitor cycling.
* Added a "Total Deaths" monitor.
* Fixed the AutoChargeOnOrbit option only working for server players.
* Fixed clients not seeing the correct amount of days spent or deaths on the game over screen. This fix will only work if both the host AND client have this mod.
* Update the ToggleableFancyLamp tooltip to have the keybind on the right side instead of left.
* Fixed several logic bugs with DaysSinceLastDeath on both hosts and clients.
* Added an option to allow the player to look down more (defaults to true).
* Added an option to disable post processing for the internal and external ship cams (defaults to false).
* Improving performance by no longer trying to find a reference to StormyWeather every frame in some cases.
* Added an option for the number of items the dropship can deliver at once (defaults to 24).
* Added an option for the number of items the company sell counter can hold at once (defaults to 24).
* Added an option to force the terminal scan command (and scrap left monitor) to display the exact amount instead of approximate (defaults to false).
* Added an option to allow keys to be used to unlock doors no matter where they are in your inventory (defaults to true).

### v1.1.7 - More fixes
* Fixed sale items adding an extra 's' on the sales monitor if it already ended with s.
* Fixed monitors not coming back on after the being shut off if UseBetterMonitors = false.
* Doubled the distance required to scan the light switch.
* Optimizing the monitors code a bit more to try preventing more startup errors in rare cases.

### v1.1.6 -  Hotfix
* Adding more null checks to a couple areas in code that were causing problems (and breaking things since further code would not get executed).

### v1.1.5 - Fixes and improvements
* Fixed compatibility issues with [TwoRadarMaps](https://thunderstore.io/c/lethal-company/p/Zaggy1024/TwoRadarMaps/) (thanks Zaggy).
* Fixed the problem of always forgetting to add new display types to the power toggle when UseBetterMonitors = False.
* Added a hash specifier to the asset bundle load, which circumvents a bug that prevents my asset bundle from loading when certain other mods are loaded.
* Added an option for showing a fancy lightning overlay on item slots that are being targeted by lightning strikes (defaults to true).
* Added an option to always show the news popup that always displays on the main menu every time (defaults to false - vanilla is true).
* Added an option to add a scan node to the light switch (defaults to true).
* Added an option to auto charge all batteries when the ship orbits (defaults to false).
	* Without making this much more complicated or host only, the best I can do with vanilla code is to only charge the items that you (the mod user) own - or in other words, you were the last one to hold it.

### v1.1.4 - Fixes and more monitor displays
* Fixed credits and ship scrap not updating properly if old style monitors were used.
* Fixed sales showing inverted percentages.
* Updated the credits monitor to refresh a few times a second to guarantee it catches any credits changes, whether from vanilla or other mods.
* Added the option to display current hitpoints remaining (defaults to on).
* Added new "Total Days", "Quota Number", "Scrap Left", and "DaysSinceDeath" options to the monitor displays.
	* Note that the scrap left monitor gives the same results as the scan command in the terminal, and is an *approximate* value.
* Fixed several cases of white backgrounds with better monitors when they are first loaded.
* Slight potential performance improvement when paired with [OpenBodyCams.](https://thunderstore.io/c/lethal-company/p/Zaggy1024/OpenBodyCams)

### v1.1.3 - Bugfixing my code
* I am now ensuring that any monitor tweaks are disabled if none of the monitor settings have been changed, to prevent any monitor bugs from affecting users who do not use them at all.
* Added two new display monitor options (current credits & door power).
* Finally implemented the sales monitor.
* Fixed a bug in monitor migration if you had old monitor settings set to "0"
* Fixed the profit quota monitor scan node being in the wrong spot again if using better monitors.
* Fixed the ship loot monitor not updating properly when things were dropped in the ship.
* The new style monitors now adhere to the CenterAlignMonitorText setting.
* Renaming the asset bundle to something more specific to improve compatiblity and naming conflicts.
* Fixed ShowBlueMonitorBackground not working if UseBetterMonitors was false.
* Fixed the profit quota and deadline texts not updating if monitor tweaks were made while not using the new style monitors.
* Changed the default FPS of the internal and external cams to be 0, or unrestricted, since that may actually improve performance in some cases.
* Fixed medkit not working from v1.1.2.

### v1.1.2 - Tweaks
* Updated the way the config sets monitor positions (don't worry, your old settings will be migrated and cleaned up). You now specify which displays go in which position - for example: "ShipMonitor1 = ProfitQuota" instead of "ProfitQuotaShipMonitorNum = 1". See UseBetterMonitors for a description of what the numbers mean.
* Separated "Weather" and "FancyWeather" into two unique monitor display options.
* Improving performance with the radar map rendering too often from the more monitors update.
* Now defaulting the inventory management options to false since it can cause desyncs with people who don't use the same config options or have the mod.
* Improving compatibility with [LCVR](https://thunderstore.io/c/lethal-company/p/DaXcess/LethalCompanyVR/) regarding occluded scan nodes.
* Added more custom RPC calls for the med station item to try improving compatibility with other mods that modify health.
* Fixed MinimumStartingMoney not working.
* Fixed compatibility with [UnlockOnStart](https://thunderstore.io/c/lethal-company/p/mrov/UnlockOnStart/) when SpeakerPlaysIntroVoice was set to False.
* Trying to optimize when the ship scrap monitor is calculated/updated by moving its calls to when things are only dropped in ship or picked up while in the ship.
* Fixed a case where the ship scrap display would not update after a reset.
* Fixed a bug where extra monitors would not work on further hosts from the main menu during the same game session.

### v1.1.1 - Fixes and more customization
* Fixed the medkit not parented to the ship for client players.
* Attached the profit quota scan node to the profit quota monitor, wherever it is.
* Added options for adjusting the FPS and resolution multiplier for the external ship cam.
* Fixed certain tools (such as the DIY flashbang) having an extra scan node when ScannableTools was set.
* Fixed a bug with StartingMoneyPerPlayer where you could keep loading an unstarted game and connecting clients would add more to the group credits.
* Added color customization options to the config for monitor background and text.
* Updated ShowBackgroundOnAllScreens to work on the old style monitors as well.
* Added an option to disable the speaker playing the intro for new games.
* Fixed some bugs with the free rotate and CCW modifier keys, and when they were set to "None"
* Added an option to lock the camera while at the terminal.
* Fixed the reticle (if enabled) displaying while using the terminal or not controlling the player.
* Fixing the terminal view monitor command not updating the map when the internal security cam's FPS is limited.
* Added code to support the automatic migration and removal of old config entries, since more changes may be coming soon...

### v1.1.0 - More monitors!
* Completely reworked the in game ship monitor system! There are now 14 monitors (12 small and two big) instead of 8, each of which can be used with both the in game profit quota and deadline monitors, or any of my extra monitors.
	* In theory this should work with other mods that overlay extra things onto monitors as well, but those will still look like overlays instead of integrated into the screen itself.
	* Added options to specifiy which monitors the base profit quota and deadline monitors show up on. This may require you to shift existing settings around if you had things on monitors 5 or 6 before updating.
	* Added options to specify which monitors the internal and external ship cams show up on. This is only applicable if using the new style monitors.
	* Added more options for the ship's internal cam, such as resolution multipliers and FPS limiting
	* Added an option to show the blue background on all screens, not just active ones. Works well with mods like [Corporate Restructure.](https://thunderstore.io/c/lethal-company/p/Jamil/Corporate_Restructure/)
* Moved the clipboard starting position (again) to be near the charger since the new big monitor would have covered it up.

### v1.0.27 - More fixes and improvements
* Fixed my personal scanner fix picking up inactive or hidden nodes.
* Added an option to scan held player items, if FixPersonalScanner is also true.
* Fixed the med station not scanning for the vanilla personal scanner.
* Fixed the weather ASCII art clipping through the background on certain screens.
* Fixing tool scan nodes not scanning for the vanilla personal scanner.

### v1.0.26 - More fixes and improvements
* Added an option (defaults ON) to center align text on the small monitors in the ship.
* Fixing ScannableTools not accepting "All" (and fixing the config description).
* [Host Requires Mod] Adding the ability to toggle the fancy lamp on and off while holding it.
* Fixing the med station sound clip playing globally.
* Fixing the med station staying floating in the level when the ship lands/takes off.
* Fixed the ship total monitor power state not syncing with the others.

### v1.0.25 - More fixes and improvements
* Fixing compatibility with custom made levels when the level does not contain a scannable ship node.
* Added a setting to hide scan subtext if it is blank or has no value.
* Added a setting to show tools on the personal scanner.
* Fixing the scanner activating on game load.
* Added the scan node for the med station, which I forgot to do when I added it.
* The 'heal' sound effect from the med station will now play for all players, instead of only the local player.
* The Med Station is now host required - meaning it only shows up for clients if the host also has it enabled.

### v1.0.24 - Fixing my stuff yet again
* Fixing the foggy weather ASCII art not showing up.
* Fixing the personal scanner fix throwing errors and scanning held items.
* Fixing ship total not resetting when players are fired.
* Fixing the medical station not working for clients unless they look at it before taking damage.
* Fixed an error that occurred during the creation of the extra monitors if any were set to 0.
* Slightly improved compatibility with Corporate Restructure.
* Yet again fixed the UI reticle behavior...

### v1.0.23 - The "I'm Sorry" Overhaul

### THIS HAS RESET MANY OF YOUR CONFIGURATION SETTINGS!

I'm trying to structure my config, so this HOPEFULLY will be a one time inconvenience. By moving config settings to new sections, it keeps the old, unused value in your config, but generates a new setting with the same name under the new section. Your best bet is to completely delete the config file and let the game regenerate it when it launches.

* Overhauled the config file since it's growing more than I originally anticipated and I want to keep things organized.
* Overhauled the extra monitors. They are now more configurable, down to whether the backgrounds show and which monitor they each display on, if any.
* Slightly overhauled the FixPersonalScanner code, making it even more reliable.
* Added a config setting to disable L/R arrow keys at the terminal if desired.
* Hopefully fixed the UI reticle scaling up before disappearing at times.

### v1.0.22 - Compatibility Improvements
* Added a [wiki.](https://thunderstore.io/c/lethal-company/p/ShaosilGaming/GeneralImprovements/wiki/1267-config-reference/)
* Updated many of the config settings to default to OFF for new users of the mod.
* Fixed compatibility with the [Touchscreen](https://thunderstore.io/c/lethal-company/p/TheDeadSnake/Touchscreen/) mod when AddTargetReticle is set to true.
* Tweaked the radar items' label positions a bit more to make it closer to base game rotation/position when the ship cam is pointed north.
* Improved the behavior of L/R arrow keys on the terminal to be more compatible with mods like [darmuhsTerminalStuff.](https://thunderstore.io/c/lethal-company/p/darmuh/darmuhsTerminalStuff/).
	* The downside of this extra flexibility means pressing L/R on ANY screen will be the same as if you typed in 'switch ...'
* Fixing the time display remaining on old time values after taking off from a moon.

### v1.0.21 - Fixing my stuff and adding a medkit
* Fixed the quota rollover showing zero incorrectly for clients after a deadline was met.
* Fixed a rare bug that caused a softlock when trying to leave moons.
* Fixed clients seeing $0 for the ship total on the monitor when first connecting.
* Updated target reticle transparency, size, and now defaulting to OFF for new users.
* Fixed map labels for turrets, doors, etc being too far offset from their respective objects.
* Adds a medkit 'charging station' the ship above the battery charger to recharge your health (defaults to OFF).
* Finished the time display monitor and it now shows current time on moons. May be disabled in config.
* Centering the time & weather text a little better in the ship monitors.
* Disabled the ability to scan during ship building mode.
* Added mouse button support to snap rotation keybinds in the config.

### v1.0.20 - More improvements
* Added a subtle target reticle to HUD so you can more clearly see exactly where you are pointing. May be disabled in config.
* Added a 'MinimumStartingMoney' config entry that goes well with StartingMoneyPerPlayer, and ensures the group starts with at least that amount.
* Changed the 'ShowExtraMonitors' from being a single config to having one config entry per monitor, for ULTIMATE CUSTOMIZATION. True nerds may wish to clean up the config to remove the old entries that were renamed.

### v1.0.19 - Weather Monitor, Rollover, and Fixes
* Fixed the nutcracker shotgun (and any other similar problem) breaking the scan terminal command.
* Added an option in the config to allow surplus credits to roll over to the next quota. Default OFF.
* Fixed a bug with the terminal history that was removing the most recent item instead of the last when reaching the history limit.
* Fleshed out the new weather monitor a bit, including a config setting for 'fancy' animated ASCII art for current weather.
* Fixed the clipboard and sticky note being rotated incorrectly (again!) on clients.

### v1.0.18 - More Fixes and improvements
* Fixed (hopefully!) the rest of the problems related to ship objects floating in the wrong spot when loading a game.
* Fixed softlock that occurred in orbit if CorporateRestructure was installed.
* Updated the max number of items able to be saved in the game file from 45 to 999.

### v1.0.17 - More tweaks, and key fix
* Added a config value for customizing the regular and inverse teleport cooldown seconds (both default to 10).
* Now syncing the little monitors' power state with the map screen (configurable).
* Fixed a bug in my code that broke inventory slots when using a key to unlock a door.
* Added little WIP monitors above the profit quote and deadline monitors - more to come in future updates.

### v1.0.16 - Fixing my code and adding compatibility improvements.
* Fixed the deadline text grammar again.
* Fixed the money per player not checking for amount of players when the ship was reset (and other minor fixes with it).
* Attached the ship's scan node to the ship so it appears to fly off with it when it leaves a moon.
	* I put a tiny easter egg in related to this. :)
* Fixed terminal improvements compatibility with AdvancedCompany.
* Fixed terminal cam L/R arrow compatibility with darmuhsTerminalStuff.
* Fixed clipboard and sticky note spawning outside the ship.

### v1.0.15 - Hotfix for gifts
* Fixing the little remaining bug where opening a gift inside the ship would leave it hovering.

### v1.0.14 - More fixes and improvements
* Fixed the map labels to be correctly rotated if ShipMapCamDueNorth is on.
* Fixed deadline monitor showing -1 when it's over.
* Fixed command history to reset its current 'index' when loading the terminal (U/D arrows make more sense now).
* Fixed dropship items not dropping to ground sometimes, and improved the fix for things falling through shelves on load.
* Added a config entry to toggle the fix for things falling through shelves.
* Added a config entry to toggle the ship scrap value total display.
* Tweaked the 'switch' terminal command to show the name of the target it switched to (affects L/R arrow keys as well).
* Fixed the personal scanner not being reliable in certain situations (for example trying to scan for the ship on Rend). May be disabled in the config.
* Added a config entry to disable the fire exit flipping logic fix if desired.
* Very slightly tweaked the width and font size of the tiny ship monitors so it doesn't look like letters are spilling onto the edge of the monitor.

### v1.0.13 - Rotation hotfix
* Just a quick fix to the snap rotation stuff I broke in v1.0.12 :)

### v1.0.12 - Configurable Keys and Ship Total Display
* Added config entries for the ship build modifier keys to be customized.
* Added more text to the 'Deadline' monitor in the ship to display the total value of scrap in the ship.

### v1.0.11 - Hotfix for my fixes
* Tuning the fire exit flip to make sure it happens immediately for clients as well as the host.
* Fixing the clipboard and sticky note rotating incorrectly when starting a new game.
* Added a config option to disable seeing the clipboard and sticky note altogether.

### v1.0.10 - Fixes and compatibility
* Fixing total value at end of round not calculating correctly.
* Fixing items falling to the ship floor when loading a save. In other words, things should stay on shelves, tables, etc.
* Finally fixed compatibility with ReservedItemSlot mods by detecting whether it is loaded and adjusting certain things.
* Fixed compatibility with AdvancedCompany by basically disabling any behavior that shifts item slots around if that mod is loaded.

### v1.0.9 - More fixes and improvements
* Modified the snap rotation to allow for free rotation when holding ALT (also supports counter clockwise when combined with SHIFT)
* Fixed compatibility with the [IsThisTheWayICame](https://thunderstore.io/c/lethal-company/p/Electric131/IsThisTheWayICame/) mod by rotating the player instead of the fire entrance coords.
* Adding a config setting to have the map screen always face straight up (instead of angled a bit). This defaults to OFF.
* Allowing the ESC key to cancel out of ship build mode instead of bringing up the menu.
* [Host Only] Fixed whoopie cushions and flasks being marked as conductive for lightning.
* [Host Only] Added a config option to disable all tools from being struck by lightning. Defaults to OFF.
* Fixing the end-of-round total scrap value to include hives and generally be synced with what the terminal scan approximates.
* [Host Only] Attempted to fix desynced positional and rotational data when a client joins a lobby, as well as any active emotes happening.

### v1.0.8 - v47 support and snap rotation
* Fixing things that broke in Lethal Company version 47. MAY NOT BE BACKWARDS COMPATIBLE.
* Minor fix to "Starting Money Per Player" to prevent exploits.
* Added a way to snap rotate to (n) degrees when using ship build modes. (n) is configurable (must be an interval of 15 and go evenly into 360). Setting it to 0 uses vanilla rotation.

### v1.0.7 - More fixes and improvements
* Fixing clients not registering server ship scrap as in the ship when connecting (which would make the terminal scan inaccurate for them).
* Changing the hover tip for inverse teleporters to "Beam out" (instead of "Beam Up" like the regular one) for clarity.
* Adding a "Money per player" config option for hosts, defaulting to 30, that adjusts the group credits before the game starts as players connect and drop. Setting to -1 reverts to vanilla behavior.
* Changed the clipboard starting position to be hanging on the wall, so it's easier to see initially and not in the way of the teleport button.
* Fixed certain shadows from two-handed objects (especially the bottles) not having transparency when the auto LAN/ONLINE config option was set (that was fun to track down).
* Fixed the issue where dropping items would cause certain items (weapons, etc) would not be held correctly or animated.

### v1.0.6 - Little bugfix
* Fixing L/R arrows not working on dead players.

### v1.0.5 - Hotfix!
* Reverting the fix from 1.0.4 for compatibility with ReservedItemSlot mods, since it completely broke HotBarPlus and similar mods. Sorry ReservedItemSlot users! Your best bet is to set **RearrangeOnDrop** to false.
* Fixing the left arrow key not cycling back around properly when viewing radars from the terminal.
* Fixing the monitor to display the correct player name on radar when first starting a round.

### v1.0.4 - More fixes and improvements
* Fixed terminal command history storing commands less than 3 characters.
* Fixed fire entrances facing towards the door when you go in.
* Fixed the teleporter not showing dead bodies as scrap when collecting them.
* Now allowing all items to be picked up before game starts.
* Fixing compatibility with the reserved slot mods - it will no longer shift items in reserved slots when dropping items.
* Fixing terminal scanner to include all valuables, and use the current scrap value multiplier.

### v1.0.3 - Added arrow key terminal features
* Added up/down arrow keys for navigating through terminal command history (configurable).
* Added left/right arrow keys for cycling through radar targets when viewing them on the terminal.
* Fixed the little 'n' that showed up in the middle of the terminal monitor when switching radar cams.

### v1.0.2 - More features & minor animation bugfix
* Fixed weird animation bug when dropping items if RearrangeOnDrop was on.
* Added a config option (defaulting to true) to skip the bootup style menu animation.
* Added a config option (defaulting to nothing) to specify whether to automatically choose ONLINE or LAN upon launch.

### v1.0.1 - More improvements
* Now always puts two-handed items in slot 1 (may be disabled in config).
* Decreasing the time required between inventory slot scrolls (configurable).
* Decreasing the time required in between placing items on the company counter.
* You can now instantly begin typing at the terminal when activating it.
* Updated the key fix to include all non-scrap grabbable objects. Now none of them will show value when scanned or sold.

### v1.0.0 - Initial Release