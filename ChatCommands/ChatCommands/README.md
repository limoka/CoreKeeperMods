## Testing Utils

A small [Core Keeper](https://store.steampowered.com/app/1621690/Core_Keeper/) plugin to help mod developers test things out in-game. Currently adds 10 chat commands:

`/help`:

&emsp;Use this to get a list of all commands in-game or to get more info on a command with `/help {command}`

`/give`:

&emsp;Give yourself any item. Use `/give name:{itemName} {count}` or `/give id:{itemId} {count}`. The count parameter defaults to 1.

`/clearInv`:

&emsp;Clears the player's inventory.

`/heal`:

&emsp;Use `/heal` to fully heal player. `/heal {amount}` for a specific amount.

`/feed`:

&emsp;Use `/feed` to fully feed player. `/feed {amount}` for a specific amount.

`/maxSkills`:

&emsp;Maxes out all skills.

`/resetSkills`:

&emsp;Resets all skills to 0.

`/setSkill`:

&emsp;Use `/setSkill {skillName} {level}` to set the given skill to the given level (0-100).

`/kill`:

&emsp;Kills the player.

`/invincibility`:

&emsp;Toggles the player's invincibility.

## Installation

This mod requires BepInEx6 to be installed, and should be installed in BepInEx's plugin folder. 
For information on installing BepInEx for Core Keeper look here https://core-keeper.thunderstore.io/package/BepInEx/BepInExPack_Core_Keeper/
