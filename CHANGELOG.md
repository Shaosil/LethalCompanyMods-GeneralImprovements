# Changelog

* **v1.0.23** - The "I'm Sorry" Overhaul

	**THIS HAS RESET MANY OF YOUR CONFIGURATION SETTINGS!**

	I'm trying to structure my config, so this HOPEFULLY will be a one time inconvenience. By moving config settings to new sections, it keeps the old, unused value in your config, but generates a new setting with the same name under the new section. Your best bet is to completely delete the config file and let the game regenerate it when it launches.

	* Overhauled the config file since it's growing more than I originally anticipated and I want to keep things organized.
	* Overhauled the extra monitors. They are now more configurable, down to whether the backgrounds show and which monitor they each display on, if any.
	* Slightly overhauled the FixPersonalScanner code, making it even more reliable.
	* Added a config setting to disable L/R arrow keys at the terminal if desired.
	* Hopefully fixed the UI reticle scaling up before disappearing at times.
	<br>

* **v1.0.22** - Compatibility Improvements
	* Added a [wiki.](https://thunderstore.io/c/lethal-company/p/ShaosilGaming/GeneralImprovements/wiki/1267-config-reference/)
	* Updated many of the config settings to default to OFF for new users of the mod.
	* Fixed compatibility with the [Touchscreen](https://thunderstore.io/c/lethal-company/p/TheDeadSnake/Touchscreen/) mod when AddTargetReticle is set to true.
	* Tweaked the radar items' label positions a bit more to make it closer to base game rotation/position when the ship cam is pointed north.
	* Improved the behavior of L/R arrow keys on the terminal to be more compatible with mods like [darmuhsTerminalStuff.](https://thunderstore.io/c/lethal-company/p/darmuh/darmuhsTerminalStuff/).
		* The downside of this extra flexibility means pressing L/R on ANY screen will be the same as if you typed in 'switch ...'
	* Fixing the time display remaining on old time values after taking off from a moon.
	<br>

* **v1.0.21** - Fixing my stuff and adding a medkit
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
	<br>

* **v1.0.20** - More improvements
	* Added a subtle target reticle to HUD so you can more clearly see exactly where you are pointing. May be disabled in config.
	* Added a 'MinimumStartingMoney' config entry that goes well with StartingMoneyPerPlayer, and ensures the group starts with at least that amount.
	* Changed the 'ShowExtraMonitors' from being a single config to having one config entry per monitor, for ULTIMATE CUSTOMIZATION. True nerds may wish to clean up the config to remove the old entries that were renamed.
	<br>

* **v1.0.19** - Weather Monitor, Rollover, and Fixes
	* Fixed the nutcracker shotgun (and any other similar problem) breaking the scan terminal command.
	* Added an option in the config to allow surplus credits to roll over to the next quota. Default OFF.
	* Fixed a bug with the terminal history that was removing the most recent item instead of the last when reaching the history limit.
	* Fleshed out the new weather monitor a bit, including a config setting for 'fancy' animated ASCII art for current weather.
	* Fixed the clipboard and sticky note being rotated incorrectly (again!) on clients.
	<br>

* **v1.0.18** - More Fixes and improvements
	* Fixed (hopefully!) the rest of the problems related to ship objects floating in the wrong spot when loading a game.
	* Fixed softlock that occurred in orbit if CorporateRestructure was installed.
	* Updated the max number of items able to be saved in the game file from 45 to 999.
	<br>

* **v1.0.17** - More tweaks, and key fix
	* Added a config value for customizing the regular and inverse teleport cooldown seconds (both default to 10).
	* Now syncing the little monitors' power state with the map screen (configurable).
	* Fixed a bug in my code that broke inventory slots when using a key to unlock a door.
	* Added little WIP monitors above the profit quote and deadline monitors - more to come in future updates.
	<br>

* **v1.0.16** - Fixing my code and adding compatibility improvements.
	* Fixed the deadline text grammar again.
	* Fixed the money per player not checking for amount of players when the ship was reset (and other minor fixes with it).
	* Attached the ship's scan node to the ship so it appears to fly off with it when it leaves a moon.
		* I put a tiny easter egg in related to this. :)
	* Fixed terminal improvements compatibility with AdvancedCompany.
	* Fixed terminal cam L/R arrow compatibility with darmuhsTerminalStuff.
	* Fixed clipboard and sticky note spawning outside the ship.
	<br>

* **v1.0.15** - Hotfix for gifts
	* Fixing the little remaining bug where opening a gift inside the ship would leave it hovering.
	<br>

* **v1.0.14** - More fixes and improvements
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
	<br>

* **v1.0.13** - Rotation hotfix
	* Just a quick fix to the snap rotation stuff I broke in v1.0.12 :)
	<br>

* **v1.0.12** - Configurable Keys and Ship Total Display
	* Added config entries for the ship build modifier keys to be customized.
	* Added more text to the 'Deadline' monitor in the ship to display the total value of scrap in the ship.
	<br>

* **v1.0.11** - Hotfix for my fixes
	* Tuning the fire exit flip to make sure it happens immediately for clients as well as the host.
	* Fixing the clipboard and sticky note rotating incorrectly when starting a new game.
	* Added a config option to disable seeing the clipboard and sticky note altogether.
	<br>

* **v1.0.10** - Fixes and compatibility
	* Fixing total value at end of round not calculating correctly.
	* Fixing items falling to the ship floor when loading a save. In other words, things should stay on shelves, tables, etc.
	* Finally fixed compatibility with ReservedItemSlot mods by detecting whether it is loaded and adjusting certain things.
	* Fixed compatibility with AdvancedCompany by basically disabling any behavior that shifts item slots around if that mod is loaded.
	<br>

* **v1.0.9** - More fixes and improvements
	* Modified the snap rotation to allow for free rotation when holding ALT (also supports counter clockwise when combined with SHIFT)
	* Fixed compatibility with the [IsThisTheWayICame](https://thunderstore.io/c/lethal-company/p/Electric131/IsThisTheWayICame/) mod by rotating the player instead of the fire entrance coords.
	* Adding a config setting to have the map screen always face straight up (instead of angled a bit). This defaults to OFF.
	* Allowing the ESC key to cancel out of ship build mode instead of bringing up the menu.
	* [Host Only] Fixed whoopie cushions and flasks being marked as conductive for lightning.
	* [Host Only] Added a config option to disable all tools from being struck by lightning. Defaults to OFF.
	* Fixing the end-of-round total scrap value to include hives and generally be synced with what the terminal scan approximates.
	* [Host Only] Attempted to fix desynced positional and rotational data when a client joins a lobby, as well as any active emotes happening.
	<br>

* **v1.0.8** - v47 support and snap rotation
	* Fixing things that broke in Lethal Company version 47. MAY NOT BE BACKWARDS COMPATIBLE.
	* Minor fix to "Starting Money Per Player" to prevent exploits.
	* Added a way to snap rotate to (n) degrees when using ship build modes. (n) is configurable (must be an interval of 15 and go evenly into 360). Setting it to 0 uses vanilla rotation.
	<br>

* **v1.0.7** - More fixes and improvements
	* Fixing clients not registering server ship scrap as in the ship when connecting (which would make the terminal scan inaccurate for them).
	* Changing the hover tip for inverse teleporters to "Beam out" (instead of "Beam Up" like the regular one) for clarity.
	* Adding a "Money per player" config option for hosts, defaulting to 30, that adjusts the group credits before the game starts as players connect and drop. Setting to -1 reverts to vanilla behavior.
	* Changed the clipboard starting position to be hanging on the wall, so it's easier to see initially and not in the way of the teleport button.
	* Fixed certain shadows from two-handed objects (especially the bottles) not having transparency when the auto LAN/ONLINE config option was set (that was fun to track down).
	* Fixed the issue where dropping items would cause certain items (weapons, etc) would not be held correctly or animated.
	<br>

* **v1.0.6** - Little bugfix
	* Fixing L/R arrows not working on dead players.
	<br>

* **v1.0.5** - Hotfix!
	* Reverting the fix from 1.0.4 for compatibility with ReservedItemSlot mods, since it completely broke HotBarPlus and similar mods. Sorry ReservedItemSlot users! Your best bet is to set **RearrangeOnDrop** to false.
	* Fixing the left arrow key not cycling back around properly when viewing radars from the terminal.
	* Fixing the monitor to display the correct player name on radar when first starting a round.
	<br>

* **v1.0.4** - More fixes and improvements
	* Fixed terminal command history storing commands less than 3 characters.
	* Fixed fire entrances facing towards the door when you go in.
	* Fixed the teleporter not showing dead bodies as scrap when collecting them.
	* Now allowing all items to be picked up before game starts.
	* Fixing compatibility with the reserved slot mods - it will no longer shift items in reserved slots when dropping items.
	* Fixing terminal scanner to include all valuables, and use the current scrap value multiplier.
	<br>

* **v1.0.3** - Added arrow key terminal features
	* Added up/down arrow keys for navigating through terminal command history (configurable).
	* Added left/right arrow keys for cycling through radar targets when viewing them on the terminal.
	* Fixed the little 'n' that showed up in the middle of the terminal monitor when switching radar cams.
	<br>

* **v1.0.2** - More features & minor animation bugfix
	* Fixed weird animation bug when dropping items if RearrangeOnDrop was on.
	* Added a config option (defaulting to true) to skip the bootup style menu animation.
	* Added a config option (defaulting to nothing) to specify whether to automatically choose ONLINE or LAN upon launch.
	<br>

* **v1.0.1** - More improvements
	* Now always puts two-handed items in slot 1 (may be disabled in config).
	* Decreasing the time required between inventory slot scrolls (configurable).
	* Decreasing the time required in between placing items on the company counter.
	* You can now instantly begin typing at the terminal when activating it.
	* Updated the key fix to include all non-scrap grabbable objects. Now none of them will show value when scanned or sold.
	<br>

* **v1.0.0** - Initial Release
