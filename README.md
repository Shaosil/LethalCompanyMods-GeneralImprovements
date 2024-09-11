# GeneralImprovements

Everything is mostly configurable and improves (IMO) several things about the game, with more to come.

Check the config settings or the [wiki!](https://thunderstore.io/c/lethal-company/p/ShaosilGaming/GeneralImprovements/wiki/) I default most of the improvements to OFF to maximize compatibility with other mods, but there's some good QoL features in there you might not want to miss.

### GENERAL IMPROVEMENTS:
* Places newly picked up items in the hotbar in left-right order. May be disabled in config.
* Rearranges hotbar items when dropping things. May be disabled in config.
* Always puts two-handed items in slot 1 (makes selling things a bit faster). May be disabled in config.
* Decreases the time required in between scrolling through inventory slots. Configurable.
* Decreases the time required in between placing items on the company counter.
* Removes the wait to begin typing at the terminal when activating it.
* Skips the "bootup" menu animation when launching the game. May be disabled in config.
* Allows all items to be picked up before the game starts.
* Changes the "Beam up" hover tip for inverse teleporters to say "Beam out" for clarity.
* Moves the ship clipboard manual to start pinned to the wall. This makes it easier to find, and moves it out of the way of the teleport button.
* Introduces a degrees config option that snap rotates placeable ship objects in build mode, along with configurable modifier keybinds.
* Adds an option to make the ship's map camera rotated so that it can face any cardinal direction, instead of at a southwest angle.
* The ESC key will now cancel out of ship build mode instead of bringing up the menu (similar to the terminal).
* Adds a config setting to hide the clipboard and sticky note.
* Changes the inverse teleporter cooldown to be the same as the regular (10 seconds). Both cooldowns may be customized in the config.
* The little monitors above the map screen will now share the power state of the map screen. Behavior may be disabled in config.
* Increases the max items able to be saved on the ship from 45 to 999 (affects saving and loading game files).
* Adds an option to show a small reticle on the HUD UI so you can see where you are pointing.
* Adds an option to hide scan node subtext if it has no scrap value or description.
* Adds an option to scan tools.
* [Host Must Have Mod] Adds the ability to toggle the fancy lamp on and off while holding it.
* Locks the camera while at the terminal so it doesn't keep getting pulled back if you try to move it. Behavior may be disabled in config.
* Fixed clients not knowing the amount of days spent and total deaths if joining on later quotas/days, if both the host and client have this mod.
* Improves the spray can color randomness, and persists the colors between clients and even on reloading the saves.
* Saving item rotations so they aren't all facing the same way when you load up a saved game.
* Items which cannot be grabbed will not display the "Grab" hover tip.
* Improves performance in many situations where audio reverb objects were searching for a static gameobject every frame.
* Adds an option to center the signal translator text.
* Adds an option to keep the credits display of the terminal fit inside the dark green background behind it.

### NEW FEATURES:
* A total of 14 customizable, screen-integrated monitors for the ship (instead of the vanilla 8)! Choose which monitor to put what item, including ship cams and background color.
* Available monitors to customize are things like weather, sales, time, credits, door power, ship cams, total days, total quotas, scrap remaining, etc. Work in progress, may be enabled individually in the config.
* If specified in the config, the game will automatically select ONLINE or LAN upon launch.
* Using the up/down arrow keys at the terminal will navigate through the previous (n) commands. (n) may range from 0-100 in the config.
* Using the left/right arrow keys at the terminal while viewing the radar will quickly cycle through available targets.
* [Host Only] Adds an option to specify starting money per player, as well as define a minumum credit amount. Accepts ranges from -1 (disabled/default) to 1000.
* [Host Only] Adds an option to prevent tools from getting struck by lightning. This is a bit of a cheat in some opinions, so I recommend leaving it off. If the host/server player has this enabled, it will apply to all clients.
* Adds an option to display text under the 'Deadline' monitor with the current ship loot total.
* [Host Required] Adds an option to roll over surplus credits to the next quota. If clients do NOT have this enabled, there will be visual desyncs only.
* Adds an option for adding a little medical station above the ship's charging station that heals you back to full health when used.
* Adds an option to use keys from any inventory slot, as well as an option to prevent them from despawning when they are used, AND an option to destroy them when orbiting.
* Adds an option to hide player names.
* Adds options to bring held or all items when using teleporters.
* Adds an option to be able to scan the item dropship.
* Adds an option to disable overtime bonuses.
* Adds an option to remove the ship's cupboard's doors.
* Adds an option to display moon costs next to their names in the terminal.
* Adds options for a 24 hour clock, and converting lbs to kgs.
* Adds an option to always show the clock when landed on a moon.
* Adds an option to disable some colliders for placeable ship objects, allowing them to intersect.
* Allows the host player to save everyone's last known suit, which persists when possible as the game resets and the same players connect.
* Allows the host to save furniture position (per save file), which means default furniture will not be reset after being fired.
* Adds an option to display any hidden moons in the terminal's moon catalog. Defaults to "AfterDiscovery".
* Adds an option to be able to scan other players. This works nicely with masks as well, as they will take the name of a random connected player.
* Adds an option to allow the masked entities to blend in more, which means they will not wear masks, and will don the suit (and MoreCompany cosmetics if necessary) of a random real player.
* Adds an option to control the menu music volume.
* Adds configurable settings for the chat fade delay and opacity level.
* Adds configurable weather multipliers for scrap values and amounts. Supports any custom or modded weather.

### MINOR BUGFIXES:
* Stops all non-scrap objects from showing value (when scanned and sold) when they do not actually have any.
* Removes the random 'n' in the middle-left of the terminal monitor when switching through radar cams.
* Flips the rotation of fire entrances 180 degrees so you are facing inside the facility when entering.
* Dead bodies will now instantly show as collected when teleported back to the ship (configurable).
* Fixes the scan terminal command and end-of-round scrap sum to include all valuables outside the ship, as well as factor in the current scrap value multiplier (under the hood, not related to company).
* The initial monitor view now shows the correct player name when first starting a round.
* The map seed is now properly randomized as soon as you start a new save file, which means sales, weather, etc will be properly randomized.
* Fixes ship scrap not being marked as 'in the ship' for clients when joining. This fixes several things client side, including terminal scans, extra scrap collection pings, and more.
* [Host Only] Certain items (soccer ball, toilet paper, etc) will no longer be hit by lightning. If the host/server player has this enabled, it will apply to all clients.
* [Host Only] When a new client connects to your lobby, they should see the correct position, rotation, and current emote animation of each player, as well as correct states of the ship lights and monitor power.
* When loading a file, items in the ship will no longer fall through shelves, tables, etc. May be disabled.
* Adds an option to fix the personal scanners sensitivity, making it function more reliably, for example being able to ping the ship on Rend.
* Fixes the ship scan node showing up outside of the ship while flying in to a moon.
* Fixes the item sales being empty every time a host starts the game, until a day passes.
* Fixes landmines remaining on the map screens and still being scannable after detonating.
* Fixes several things about flashlights:
	* Flashlights will no longer toggle on or off when being placed in the ship cupboard (and certain other "E" interactions.) 
	* Flashlights/helmet lights no longer turn off when picking up an additional inactive flashlight.
		* It will now turn on the new flashlight, and turn off the old one. If the new one is out of batteries, the old one will stay on. This behavior may be disabled in the config.
	* Multiple flashlights can no longer be active at the same time. This behavior may be disabled in the config.
	* Laser pointers are no longer treated as flashlights. This behavior may be disabled in the config.
	* Flashlights will no longer toggle themselves when using/switching items and picking things up
		* As a result, they will not drain battery randomly while not truly on.
	* WARNING: Players who do not use this mod may, in rare circumstances, not see your flashlight in the state it should be.
* Fixes grabbable objects having a hovertip that they are grabbable when they are not (Nutcracker shotgun, deposit items desk, etc).

I will probably keep adding to this as I see minor things that could be improved or fixed.

### WARNING

Because this mod can shift inventory slots around, if you play with people who do NOT also have this mod installed, the slots they are aware about on your character may be different, and as a result, they may not see you holding what you actually are.