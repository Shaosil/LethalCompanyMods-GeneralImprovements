# Changelog

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
* Slight potential performance improvement when paired with [OpenBodyCams.](https://thunderstore.io/c/lethal-company/p/Zaggy1024/OpenBodyCams/v/1.0.24/)

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