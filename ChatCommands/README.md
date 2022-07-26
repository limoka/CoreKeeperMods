# Chat Commands

This mod adds different chat commands, that help mod developers and others can use test things out in-game.

Commands list:<br>

- `/give {itemName} [count]`: Give yourself any item.
- `/clearInv`: Clear the player inventory.
- `/heal [amount]`: Use to fully heal player.
- `/feed [amount]`: Use to fully feed player.
- `/maxSkills`: Max out all skills.
- `/resetSkills`: Reset all skills to 0.
- `/setSkill {skillName} {level}`: Set the given skill to the given level (0-100).
- `/kill`: Kill the player.
- `/invincibility`: Toggle the player's invincibility.
- `/passive` Toggle enemy AI passive behavior.
- `/noclip` Move freely without physical limitations. Variations:<br>
  `/noclip` Toggle noclip state<br>
  `/noclip speed {multilplier}` Set noclip movement speed<br>


- `/hide <target> [state]` Hide User Interface, Inventory and Player visual elements. Possible targets: player, ui, inventory

Arguments in curly brackets (`{}`) are mandatory and you must specify them, while arguments in square brackets (`[]`) are optional and if you don't specify them, their value will be automatically inferred.

This mod was originally written by `cato1001#8659`, original source code can be found on [github](https://github.com/PatelRahil/TestingUtils). Since original author didn't upload it to Thunderstore, I have decided to improve the mod and publish it on Thunderstore.

More features might come in the future. If you have any feature you would like to see added, message me on Discord

## Feedback and Bug Report
Feel free to contact me via Discord (`Kremnev8#3756`) for any feedback, bug-reports or suggestions.

## Installation
### With Mod Manager

Simply open the mod manager (if you don't have it install it [here](https://core-keeper.thunderstore.io/package/ebkr/r2modman/)), select **Chat Commands by kremnev8**, then **Download**.

If prompted to download with dependencies, select `Yes`.

Then just click **Start modded**, and the game will run with the mod installed.

### Manually
Install BepInEx Pack from [here](https://core-keeper.thunderstore.io/package/BepInEx/BepInExPack_Core_Keeper/)<br/>
Install CoreLib from [here](https://core-keeper.thunderstore.io/package/CoreMods/CoreLib/)<br/>

Unzip all files into `Core Keeper\BepInEx\plugins\ChatCommands/` (Create folder named `ChatCommands`)<br/>

## Changelog
<details>
<summary>Changelog</summary>

### v1.1.0
- Migrate to CoreLib 1.0.0

### v1.0.0
- Initial Release
</details>