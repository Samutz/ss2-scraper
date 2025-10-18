# About
This is a [Mutagen](https://github.com/Mutagen-Modding/Mutagen) project that I created to collect data from addon packs for [Sim Settlements 2](https://simsettlements2.com/). This is data is used to populate [SS2 Catalog](https://samutz.com/ss2db).

I am inexperienced with C#/.net, so don't be surprised if my code looks stupid.

# Where's the download?
For the time being I'm not providing the compiled binary as I'm still constantly updating the tool to add more functions. When it reaches a point that I'm not updating as frequently, I might start posting it in the releases section.

# Output
Collected data is written to JSON files in a json subfolder. One JSON file per plugin file.

# Parameters
`-data <data-path>`  
Specifies the path to the FO4 data folder to scan.

`-lomode`  
Loads all plugins together so that overrided data can be collected. This is the default if neither of `-lomode` or `-singlemode` are specificed.

`-singlemode`  
Loads each plugin and its masters as a load order one at a time. The is the method I used to use until I ran in to plugins that had patches in other plugins (such as IDEK's Logistics Station 2). I keep this mode as an option for debugging purposes.

`-mo2 <modlist.txt-path>`  
Specifies a modlist.txt from [Mod Organizer 2](https://github.com/ModOrganizer2/modorganizer) to collect Nexus info from the mods' meta.ini files. This file is usually located in `<MO2 folder>\profiles\<profile name>\modlist.txt`.

# Names / Strings
In order to get localized names of vanilla/DLC records, the ba2 archives containing those strings must be in the same directory as the plugin.
