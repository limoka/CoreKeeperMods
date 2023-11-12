# Mod Reporter
View current state of your mods in a convenient menu and see any issues easily.

Features included:
- View a list of all installed and subscribed mods
- View state of each mod. The states Mod Reporter can report include:
  - Ok: your mod loaded correctly
  - Disabled: you have disabled the mod
  - Errored: The mod has thrown errors while loading and might not work
  - Missing Dependency: The mod installed does not have one or more dependencies installed
  - Not Downloaded: Ingame mod manager tried to download the mod, but it's not downloaded. (You probably should restart)
  - Corrupted: Some of the mod files got corrupted
- View selected mod dependencies
- View selected mod error and general log. You can also copy the whole log into your clipboard and send to the mod author easily.

## NOTE:
Mod Reporter might misinterpret to which mod a log belongs to or might loose some log. As such you should always trust `Player.log` file found in `C:\Users\<user-name>\AppData\LocalLow\Pugstorm\Core Keeper` folder.

## For mod developers
If you want to ensure Mod Reporter reports all of your mod logs correctly, ensure you are logging them in the following format: 

```
[Mod Name]: Actual log message
```
The log message should start from `[` character immitidately followed by your mod name, as stated in the mod manifest. Spaces are allowed. Then `]:` characters should follow. After this you are free to log anything.

## Feedback and Bug Report
Feel free to contact me via Discord (`kremnev8a`) for any feedback, bug-reports or suggestions.

## How to support development
If you like what I do and would like to support development, you can [donate](https://boosty.to/kremnev8).