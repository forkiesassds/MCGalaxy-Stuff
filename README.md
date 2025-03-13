# MCGalaxy-Stuff
plugins and commands for the MCGalaxy server software, also includes forked versions of popular plugins and commands.

To easily work with plugins, especially ones in the `proto` directory open up the pluginWorkspace.csproj file in an IDE.
Make sure you have MCGalaxy source code cloned in the same directory as the workspace folder, 
DO NOT PUT IT IN THIS FOLDER, AS IT WILL CAUSE COMPILE ERRORS. Then do dotnet restore in the plugin workspace folder.

## CmdDecide.cs
Command for deciding on things, originally came from The Build, but this version has different wording.
## Greentext.cs
Plugin to make text green if the message starts with ">", originally came from The Build.
## nasgen.cs
Edited version of NasGen, made for the Omniarchive Classic (originally known as ClassicalNuts) server.
## 8ball2.cs
A NA2 like version of /8ball
## spleef.cs
An implementation of the spleef minigame.
## alphagen.cs
A port of Minecraft Alpha's terrain generator.
## Rainbow.cs
Improved version of the rainbow text plugin. The plugin uses color code r for rainbow text.
## fancyvoronoigen.cs
An attempt to improve the Voronoi terrain generator in MCGalaxy to have terrain that doesn't consist of only cliffs
## ForEach.cs
A command that runs commands for each block within a selection. Inspired by /foreach on NA2.
## usedCmdWarn.cs
Plugin that warns opchat when a command that is defined in `plugins/warncmds.txt` has been used
## preventDiscordBackdoor.cs
This plugin prevents any backdoors that people who have been banned from a server yet still are in discordcontrollers.txt to be able to abuse operator commands.
## ServerSettingCmdPlugin.cs
Plugin that adds commands to view and manage server settings from ingame.
## MessageConsent.cs
Plugin that makes everyone require to confirm if they should see any message sent by anyone. Was made mostly as a joke.
## LoginToLastPosPlugin.cs
This plugin makes players login at their last position. This includes the map they were on and precise position and orientation.
## BetacraftV2Heartbeat.cs
Plugin that implements Betacraft V2 heartbeat to list to the Betacraft V2 server list.
## ClientFilter.cs
**PLEASE READ [SetupJsonDotNET.md](SetupJsonDotNET.md) TO MAKE SURE THE PLUGIN COMPILES AND WORKS**

Plugin that filters clients that can join the server. Is highly customisable and alerts OPs about players trying to join with prohibited clients.
Example configuration:
```json
{
  "blacklistedClients": [
    {
      "warnMessage": "Cheat/utility clients are against the rules!",
      "clients": [
        "Jini",
        "Samsung Smart Fridge"
      ]
    },
    {
      "warnMessage": "Please remove the Classic64 plugin to play here.",
      "clients": [
        "Classic64 Mario64"
      ]
    }
  ],
  "requiredCPEExtensions": {
    "LongerMessages": 1,
    "NotifyAction": 1
  },
  "requireCPE": false,
  "cpeRequiredMessage": "Please enable enhanced mode in the launcher!"
}
```
