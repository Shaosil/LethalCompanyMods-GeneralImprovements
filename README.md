# GeneralImprovements

Everything is mostly configurable and improves (IMO) several things about the game, with more to come.

GENERAL IMPROVEMENTS:
<ul>
	<li>Places newly picked up items in the hotbar in left-right order. May be disabled in config.</li>
	<li>Rearranges hotbar items when dropping things. May be disabled in config.</li>
	<li>Always puts two-handed items in slot 1 (makes selling things a bit faster). May be disabled in config.</li>
	<li>Decreases the time required in between scrolling through inventory slots. Configurable.</li>
	<li>Decreases the time required in between placing items on the company counter.</li>
	<li>Removes the wait to begin typing at the terminal when activating it.</li>
	<li>Skips the "bootup" menu animation when launching the game. May be disabled in config.</li>
	<li>Allows all items to be picked up before the game starts.</li>
</ul>

NEW FEATURES:
<ul>
	<li>If specified in the config, the game will automatically select ONLINE or LAN upon launch.</li>
	<li>Using the up/down arrow keys at the terminal will navigate through the previous (n) commands. (n) may range from 0-100 in the config.</li>
	<li>Using the left/right arrow keys at the terminal while viewing the radar will quickly cycle through available targets.</li>
</ul>

MINOR BUGFIXES:
<ul>
	<li>Stops all non-scrap objects from showing value (when scanned and sold) when they do not actually have any.</li>
	<li>Removes the random 'n' in the middle-left of the terminal monitor when switching through radar cams.</li>
	<li>Flips the rotation of fire entrances 180 degrees so you are facing inside the facility when entering.</li>
	<li>Dead bodies will now instantly show as collected when teleported back to the ship.</li>
	<li>Fixes the scan terminal command to include all valuables outside the ship, as well as factor in the current scrap value multiplier.</li>
	<li>The initial monitor view now shows the correct player name when first starting a round.</li>
</ul>

This pairs well with my other mod, <a href="https://thunderstore.io/c/lethal-company/p/ShaosilGaming/FlashlightFix/">FlashlightFix</a>

I will probably keep adding to this as I see minor things that could be improved or fixed.

### WARNING

Because this mod can shift inventory slots around, if you play with people who do NOT also have this mod installed, the slots they are aware about on your character may be different, and as a result, they may not see you holding what you actually are.

# Changelog

<ul>
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