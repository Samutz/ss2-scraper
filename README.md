# About
This is a [Mutagen](https://github.com/Mutagen-Modding/Mutagen) project I am working on to collect data from addon packs for [Sim Settlements 2](https://simsettlements2.com/). This is data will be used for a website am also working on. (Link to website will be added later when it is public.)

# Parameters
`-plugin <plugin-path>`  
Specifies the path to the plugin file to scan. Can also be a directory containing plugins, such as the Fallout 4 Data directory, to scan multiple plugins at once.

`-mo2 <modlist.txt-path>`  
Specifies a modlist.txt from [Mod Organizer 2](https://github.com/ModOrganizer2/modorganizer) to collect Nexus info from the mods' meta.ini files. This file is usually located in `<MO2 folder>\profiles\<profile name>\modlist.txt`.

`-json`  
Outputs the serailized data to `json\<plugin-name>.json`.

`-upload`  
Uploads the serailized data to the configured API (see below).

# Names / Strings
In order to get localized names of vanilla/DLC records, the ba2 archives containing those strings must be in the same directory as the plugin.

# API Config
The API is configured specifically for the website I am working on. The way I'm doing it might not match your own needs and you may need to make changes to the code.

Create a `config.json` file in the same folder as SS2Scraper.exe, if it doesn't already exist. Set the values.
```json
{
	"base_url": "https://localhost:8443/",
	"api_key": "yourkeyhere",
	"ignore_ssl": true
}
```

`base_url`  
SS2Scraper.exe appends `api/upload_json` to the `base_url` when sending the data. You may need to change this path in the source if you are creating your own project.  

`api_key`  
This is appended to the JSON data. The website checks and verifies if it is valid.

`ignore_ssl`  
This tells the .NET HTTP client to ignore SSL errors. This is primarly for when the site is running in a developer environment and doesn't have a proper certificate.