# GeneralImprovements

Everything is mostly configurable and improves (IMO) several things about the game, with more to come.

### GENERAL IMPROVEMENTS:
<ul>
	<li>Places newly picked up items in the hotbar in left-right order. May be disabled in config.</li>
	<li>Rearranges hotbar items when dropping things. May be disabled in config.</li>
	<li>Always puts two-handed items in slot 1 (makes selling things a bit faster). May be disabled in config.</li>
	<li>Decreases the time required in between scrolling through inventory slots. Configurable.</li>
	<li>Decreases the time required in between placing items on the company counter.</li>
	<li>Removes the wait to begin typing at the terminal when activating it.</li>
	<li>Skips the "bootup" menu animation when launching the game. May be disabled in config.</li>
	<li>Allows all items to be picked up before the game starts.</li>
	<li>Changes the "Beam up" hover tip for inverse teleporters to say "Beam out" for clarity.</li>
	<li>Moves the ship clipboard manual to start pinned to the wall. This makes it easier to find, and moves it out of the way of the teleport button.</li>
	<li>Introduces a degrees config option that snap rotates placeable ship objects in build mode, along with configurable modifier keybinds.</li>
	<li>Allows the ship's map camera to be rotated so that it faces straight up, instead of at an angle. This behavior is DISABLED by default in the config.</li>
	<li>The ESC key will now cancel out of ship build mode instead of bringing up the menu (similar to the terminal).</li>
	<li>Adds a config setting to hide the clipboard and sticky note. Defaults to off.</li>
	<li>Adds text to the 'Deadline' monitor to also display current ship loot total.</li>
</ul>

### NEW FEATURES:
<ul>
	<li>If specified in the config, the game will automatically select ONLINE or LAN upon launch.</li>
	<li>Using the up/down arrow keys at the terminal will navigate through the previous (n) commands. (n) may range from 0-100 in the config.</li>
	<li>Using the left/right arrow keys at the terminal while viewing the radar will quickly cycle through available targets.</li>
	<li>Hosts may change the starting money per player in the config. Default is now 30 (ranges from -1 - 1000). Setting to -1 will disable this behavior.</li>
	<li>[Host Only] Added an option (default OFF) to prevent tools from getting struck by lightning. This is a bit of a cheat in some opinions, so I recommend leaving it off. If the host/server player has this enabled, it will apply to all clients.</li>
</ul>

### MINOR BUGFIXES:
<ul>
	<li>Stops all non-scrap objects from showing value (when scanned and sold) when they do not actually have any.</li>
	<li>Removes the random 'n' in the middle-left of the terminal monitor when switching through radar cams.</li>
	<li>Flips the rotation of fire entrances 180 degrees so you are facing inside the facility when entering.</li>
	<li>Dead bodies will now instantly show as collected when teleported back to the ship.</li>
	<li>Fixes the scan terminal command and end-of-round scrap sum to include all valuables outside the ship, as well as factor in the current scrap value multiplier (under the hood, not related to company).</li>
	<li>The initial monitor view now shows the correct player name when first starting a round.</li>
	<li>Fixes ship scrap not being marked as 'in the ship' for clients when joining. This fixes several things client side, including terminal scans, extra scrap collection pings, and more.</li>
	<li>[Host Only] Whoopie cushions and flasks will no longer be hit by lightning. If the host/server player has this enabled, it will apply to all clients.</li>
	<li>[Host Only] When a new client connects to your lobby, they should see the correct position, rotation, and current emote of each player.</li>
	<li>When loading a file, items in the ship will no longer fall through shelves, tables, etc.</li>
</ul>

This pairs well with my other mod, <a href="https://thunderstore.io/c/lethal-company/p/ShaosilGaming/FlashlightFix/">FlashlightFix</a>

I will probably keep adding to this as I see minor things that could be improved or fixed.

### WARNING

Because this mod can shift inventory slots around, if you play with people who do NOT also have this mod installed, the slots they are aware about on your character may be different, and as a result, they may not see you holding what you actually are.