# Changelog

<ul>
	<li><b>v1.0.15</b> - Hotfix for gifts</li>
	<ul>
		<li>Fixing the little remaining bug where opening a gift inside the ship would leave it hovering.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.14</b> - More fixes and improvements</li>
	<ul>
		<li>Fixed the map labels to be correctly rotated if ShipMapCamDueNorth is on.</li>
		<li>Fixed deadline monitor showing -1 when it's over.</li>
		<li>Fixed command history to reset its current 'index' when loading the terminal (U/D arrows make more sense now).</li>
		<li>Fixed dropship items not dropping to ground sometimes, and improved the fix for things falling through shelves on load.</li>
		<li>Added a config entry to toggle the fix for things falling through shelves.</li>
		<li>Added a config entry to toggle the ship scrap value total display.</li>
		<li>Tweaked the 'switch' terminal command to show the name of the target it switched to (affects L/R arrow keys as well).</li>
		<li>Fixed the personal scanner not being reliable in certain situations (for example trying to scan for the ship on Rend). May be disabled in the config.</li>
		<li>Added a config entry to disable the fire exit flipping logic fix if desired.</li>
		<li>Very slightly tweaked the width and font size of the tiny ship monitors so it doesn't look like letters are spilling onto the edge of the monitor.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.13</b> - Rotation hotfix</li>
	<ul>
		<li>Just a quick fix to the snap rotation stuff I broke in v1.0.12 :)</li>
	</ul>
	&nbsp;
	<li><b>v1.0.12</b> - Configurable Keys and Ship Total Display</li>
	<ul>
		<li>Added config entries for the ship build modifier keys to be customized.</li>
		<li>Added more text to the 'Deadline' monitor in the ship to display the total value of scrap in the ship.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.11</b> - Hotfix for my fixes</li>
	<ul>
		<li>Tuning the fire exit flip to make sure it happens immediately for clients as well as the host.</li>
		<li>Fixing the clipboard and sticky note rotating incorrectly when starting a new game.</li>
		<li>Added a config option to disable seeing the clipboard and sticky note altogether.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.10</b> - Fixes and compatibility</li>
	<ul>
		<li>Fixing total value at end of round not calculating correctly.</li>
		<li>Fixing items falling to the ship floor when loading a save. In other words, things should stay on shelves, tables, etc.</li>
		<li>Finally fixed compatibility with ReservedItemSlot mods by detecting whether it is loaded and adjusting certain things.</li>
		<li>Fixed compatibility with AdvancedCompany by basically disabling any behavior that shifts item slots around if that mod is loaded.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.9</b> - More fixes and improvements</li>
	<ul>
		<li>Modified the snap rotation to allow for free rotation when holding ALT (also supports counter clockwise when combined with SHIFT)</li>
		<li>Fixed compatibility with the <a href="https://thunderstore.io/c/lethal-company/p/Electric131/IsThisTheWayICame/">IsThisTheWayICame</a> mod by rotating the player instead of the fire entrance coords.</li>
		<li>Adding a config setting to have the map screen always face straight up (instead of angled a bit). This defaults to OFF.</li>
		<li>Allowing the ESC key to cancel out of ship build mode instead of bringing up the menu.</li>
		<li>[Host Only] Fixed whoopie cushions and flasks being marked as conductive for lightning.</li>
		<li>[Host Only] Added a config option to disable all tools from being struck by lightning. Defaults to OFF.</li>
		<li>Fixing the end-of-round total scrap value to include hives and generally be synced with what the terminal scan approximates.</li>
		<li>[Host Only] Attempted to fix desynced positional and rotational data when a client joins a lobby, as well as any active emotes happening.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.8</b> - v47 support and snap rotation</li>
	<ul>
		<li>Fixing things that broke in Lethal Company version 47. MAY NOT BE BACKWARDS COMPATIBLE.</li>
		<li>Minor fix to "Starting Money Per Player" to prevent exploits.</li>
		<li>Added a way to snap rotate to (n) degrees when using ship build modes. (n) is configurable (must be an interval of 15 and go evenly into 360). Setting it to 0 uses vanilla rotation.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.7</b> - More fixes and improvements</li>
	<ul>
		<li>Fixing clients not registering server ship scrap as in the ship when connecting (which would make the terminal scan inaccurate for them).</li>
		<li>Changing the hover tip for inverse teleporters to "Beam out" (instead of "Beam Up" like the regular one) for clarity.</li>
		<li>Adding a "Money per player" config option for hosts, defaulting to 30, that adjusts the group credits before the game starts as players connect and drop. Setting to -1 reverts to vanilla behavior.</li>
		<li>Changed the clipboard starting position to be hanging on the wall, so it's easier to see initially and not in the way of the teleport button.</li>
		<li>Fixed certain shadows from two-handed objects (especially the bottles) not having transparency when the auto LAN/ONLINE config option was set (that was fun to track down).</li>
		<li>Fixed the issue where dropping items would cause certain items (weapons, etc) would not be held correctly or animated.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.6</b> - Little bugfix</li>
	<ul>
		<li>Fixing L/R arrows not working on dead players.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.5</b> - Hotfix!</li>
	<ul>
		<li>Reverting the fix from 1.0.4 for compatibility with ReservedItemSlot mods, since it completely broke HotBarPlus and similar mods. Sorry ReservedItemSlot users! Your best bet is to set <b>RearrangeOnDrop</b> to false.</li>
		<li>Fixing the left arrow key not cycling back around properly when viewing radars from the terminal.</li>
		<li>Fixing the monitor to display the correct player name on radar when first starting a round.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.4</b> - More fixes and improvements</li>
	<ul>
		<li>Fixed terminal command history storing commands less than 3 characters.</li>
		<li>Fixed fire entrances facing towards the door when you go in.</li>
		<li>Fixed the teleporter not showing dead bodies as scrap when collecting them.</li>
		<li>Now allowing all items to be picked up before game starts.</li>
		<li>Fixing compatibility with the reserved slot mods - it will no longer shift items in reserved slots when dropping items.</li>
		<li>Fixing terminal scanner to include all valuables, and use the current scrap value multiplier.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.3</b> - Added arrow key terminal features</li>
	<ul>
		<li>Added up/down arrow keys for navigating through terminal command history (configurable).</li>
		<li>Added left/right arrow keys for cycling through radar targets when viewing them on the terminal.</li>
		<li>Fixed the little 'n' that showed up in the middle of the terminal monitor when switching radar cams.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.2</b> - More features & minor animation bugfix</li>
	<ul>
		<li>Fixed weird animation bug when dropping items if RearrangeOnDrop was on.</li>
		<li>Added a config option (defaulting to true) to skip the bootup style menu animation.</li>
		<li>Added a config option (defaulting to nothing) to specify whether to automatically choose ONLINE or LAN upon launch.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.1</b> - More improvements</li>
	<ul>
		<li>Now always puts two-handed items in slot 1 (may be disabled in config).</li>
		<li>Decreasing the time required between inventory slot scrolls (configurable).</li>
		<li>Decreasing the time required in between placing items on the company counter.</li>
		<li>You can now instantly begin typing at the terminal when activating it.</li>
		<li>Updated the key fix to include all non-scrap grabbable objects. Now none of them will show value when scanned or sold.</li>
	</ul>
	&nbsp;
	<li><b>v1.0.0</b> - Initial Release</li>
</ul>