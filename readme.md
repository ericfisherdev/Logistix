Logistix

Logistix revives [PersonalLogistics](https://github.com/mattsemar/dsp-personal-logistics) by Matt Semar for current versions of Dyson Sphere Program. PersonalLogistics is MIT licensed; the original copyright notice is retained in [LICENSE](LICENSE) alongside a new one for this fork.

### Overview
This mod is an inventory management system backed by your logistics network. Features:

* Set auto-request amounts for items. For example, always keep 4 stacks of conveyor belts in your inventory
* Set auto-recycle limits for items (can be 0) to automatically send extras of those items the logistics stations
* Send littered items to your logistics stations instead of holding 'Z' and running around like a crazy person
* Add fuel from your inventory to the Mecha automatically. Combine this with an auto-request of your preferred fuel to keep the Mecha running at full power
* Keep the Mecha's warper supply topped off from your inventory
* Adds a Recycle area to the inventory window where you can drop items you don't want

#### Glossary of Terms/Abbreviations
* ILS - [Interstellar Logistics Station](https://dsp-wiki.com/Interstellar_Logistics_Station) - relevant because it's capable of shipping items between planets/systems to player
* PLS - [Planetary Logistics Station](https://dsp-wiki.com/Planetary_Logistics_Station) - only capable of supplying the player with items from the local planet
* Mecha - [Icarus](https://dsp-wiki.com/Icarus) - relevant because it's the local source of inventory and power
* Buffer - A subsystem of Logistix (this mod), see the [Buffer](#buffer) section for more info

#### Getting started
Open the request management window using this button

![Config](https://github.com/ericfisherdev/Logistix/blob/main/Examples/ex2.png?raw=true)

#### Manual recycling
Recycle items from inventory by dropping them into the Recycle window. To disable, set the `ShowRecycleWindow` config property to false. Items added here will first go the local
Buffer (details below) and then will be sent to the nearest Logistics Station with capacity. Of course, if you try and recycle an item you're currently requesting, it's just going
to come back to your inventory.

Note that items that have no Logistics Stations will not be removed from Recycle area

![Recycle](https://github.com/ericfisherdev/Logistix/blob/main/Examples/Recycle.png?raw=true)

#### Requests window
Below is an example of how you would configure the mod to always keep 5 stacks of Plane smelters in inventory and also to send extra Plane smelters to logistics stations (when you have more than 5 stacks)

![Ban](https://github.com/ericfisherdev/Logistix/blob/main/Examples/ex3.png?raw=true)

Example showing how to keep crude oil out of your inventory completely.

![Requested](https://github.com/ericfisherdev/Logistix/blob/main/Examples/ex4.png?raw=true)

Trash can be sent to logistics network also (disable using SendLitterToLogisticsNetwork config property)

![Requested](https://github.com/ericfisherdev/Logistix/blob/main/Examples/TrashManagement.gif?raw=true)

#### (New in 2.3.0)

Numerical indicators on item icons in Requests window let you quickly see what is currently requested/banned.
![Requested](https://github.com/ericfisherdev/Logistix/blob/main/Examples/Indicators.png?raw=true)

#### Numerical indicator FAQ

```
Q: I hate these new indicators, they look terrible
A: That's not really a question, but you can disable them by opening the legacy UI, going to Config section and
 disabling 'showAmountsInRequestWindow'. Or edit the config the old-fashioned way

Q: What does the red '0' next to iron ore mean?
A: That item is banned, not allowed to be in your inventory. It will be immediately auto-recycled 

Q: Ok, so why does copper ore have a red '1' next to it?
A: That item is not being auto-requested, AND anything more than 1 stack of it in your inventory it will be auto-recyled 

Q. In the example above why doesn't titanium glass have a number next to it?
A. That item is not managed by Logistix, it's ignored

Q. All right, what about regular glass, does the blue '1' next to it mean that 1 stack of that item is kept in inventory?
 How do I know what the max allowed for that item is?
A. Yes, 1 stack will be kept in your inventory, but there's only so much info you can convey with an overview UI like this.
 To see the auto-recycle amount you have to click the item for more details

Q. These questions are dumb
A. Check the contact info at the end of this readme and send in smarter ones?    

```

## Details

### Usage

This mod does not alter save games in any way, and as of the latest release available on 2021-Dec-02 does not trigger the game's abnormality checks so should not affect
achievements or milestones. Its intent is to improve QOL and a good deal of effort has been taken to respect the game's built-in costs for item transportation. Logistics vessel
speed is the limiting factor on item delivery (as well as warper availability) so leveling up vessel speed should provide noticeable increases in transportation time

### Buffer

Requested items are loaded into a local [Buffer](#buffer) which requests 1 logistic vessel capacity worth of an item at a time. This is done to save on warpers & energy needed by vessels for transporting items. It also allows for faster loading when laying down blueprints.

The [Buffer](#buffer) is persisted locally next to your game save (using DSPModSave) so that your items won't get lost if you load up a different save.

To clear your local [Buffer](#buffer) of an item type, you can set that item to be neither requested nor banned (Request 0, Recycle Inf). The [Buffer](#buffer) won't be cleared immediately, so if you are uninstalling the mod,
make sure to look at the Buffered items window (click `Buffered` section in the settings window) to make sure everything is returned to either your inventory or to logistics stations.

### Litter

By default, littered items will be sent to the closest logistics station with capacity to hold them. This can be disabled using the SendLitterToLogisticsNetwork config property.
Litter, like banned items are first sent to the local [Buffer](#buffer) where they will be automatically sent to stations (provided the item type is not currently requested).

Littered items are not completely intercepted to avoid affecting the game's responsiveness. Instead, when littered items are detected a task is created that gets processed
later. That task will only cleanup litter that is less than 1km away from the player, (so basically on the local planet). Because of this, some litter may be missed.

### Mecha

In some cases, warpers and energy from the Mecha will be used. This happens when the nearby source for an item is a station with low energy or no warpers. When this happens a
UI message will be shown.

#### Add Fuel to Mecha Fuel Chamber

The plugin can be configured to automatically keep your Mecha's fuel chamber filled. This must be enabled in the Config section (Add fuel to mecha fuel chamber). Which items are
used are decided in the using these priorities (highest priority first)

* Item is currently being burned by Mecha and either an empty slot is available or a partially filled slot for that item type exists (top priority)
* Item is in one of the slots, but stack is not full. Note that empty slots will be filled with this item, if available
* Item is requested from logistics network and empty slot exists for it
* Item is fuel - any available fuel in inventory will be used to fill Mecha, starting with the fuel with the highest energy

#### Add Warpers To Mecha

Similar to adding fuel, the mecha's supply of warpers can be kept topped-off using this setting in the Config section of UI. This will only be done automatically if the logistics
network has warpers available and the requested minimum for warpers is at least 1 (can't be banned or unset).

### Request Modes

The `Station Request Mode` config option can be used to change which stations will be used for supplying your mecha's inventory. The different modes are

* `All` - Takes items from any station that has it, regardless of supply or demand. Closest stations have highest priority
* `AnySupply` - Takes items from any station with item set to, "Supply" (PLS) or "Remote Supply", "Local Supply" (ILS)
* `IlsDemandRules` (default) - Follows the same rules as a nearby ILS with Remote Demand/Local Demand set. Will not take from PLS on other planets
* `Planetary` - Only take items from supplying stations on the local planet

Note that pre-2.2.1 `All` was the default. The change to the default value only affects new users since it is stored
as a config property which does change unless you delete the config file

#### Planetary Request Mode
`Planetary` request mode is for players who just want to automate the process of taking items from logistics stations, so it has a few additional options

* `Unrestricted` - When you go to a new planet, buffered items can still be used to refill inventory. So, as you use items on the new planet, your inventory will be replenished from your buffer until empty 
* `OnlyLocallyAvailable` - When you go to a new planet, buffered items can still be used to refill inventory, but *only* if the item is also available on the new planet 
* `ReturnBufferOnDepart` - When you leave a planet, all buffered items are returned to the logistics network 

### Incoming Item Notifications (updated in v2.4.0)

To make it easier to understand what is happening with your requested items, an area on the left side of the screen shows the status of each requested item that
is being delivered. This can be disabled by opening settings from the Request window and disabling `ShowIncomingItemProgress` from the config tab  

![Incoming](https://github.com/ericfisherdev/Logistix/blob/main/Examples/Incoming.png?raw=true)

There are a few different messages depending on where the requested items are in their journey to you.
* `Copper ingot (7) in-transit to local buffer, ETA 25s` 
  - A shipment of copper ingots has been removed from logistics stations and is on its way to the local Buffer. After the Buffer, 7 they will be loaded to inventory (~5 seconds later)
* `Loading Artificial star (2) from buffer to inventory` - Artificial stars that are already in your Buffer will be added to your inventory shortly, usually less than 5 seconds   
* `Failed to load Artificial star (7) from Logistics Stations`
   The mod was not able to find any stations providing Artificial stars. Check the item tooltip to see how many are available in the network. If the tooltip shows: "Supplied: 0" but "Total Items: X" where X > 0 then the item is probably in a PLS on a remote planet  
* `Shipment of Iron ingot delayed due to lack of warpers` - no warpers in inventory or in supplying station, item can't be transported yet  
* `Shipment of Iron ingot delayed due to lack of available energy` - the station supplying the ingots didn't have enough power available to power the drones and Icarus was too low on power also  

Note that the `Failed to load` message is only shown periodically for each item type, and won't appear if it was able 
to find _any_ of the item (even if it's less than the requested amount). If these failure messages are too annoying, they
can be disabled using the `HideIncomingItemFailures` option from the config window. Also, if these items are only available in a PLS on a remote planet
then you'll have to fly to that planet before the items will be loaded. Add them to ILS to make them available everywhere

## Translations
Some work has been done to support localization. Sadly the only translations right now come either from the game (by re-using labels that the game uses) or from Google Translate.
This is very much a WIP so please send along any recommendations for better translations. At the moment, the only languages supported by the game are EN, CN & FR.

## Nebula 
This mod has been updated to be compatible with Nebula Multiplayer Mod. Note that it relies on the host to store the client's
state so the first time a client connects they won't have any requested items set up.
Note: Nebula Multiplayer mod itself is NOT required. Only its API plugin is a dependency

## How to install

This mod requires BepInEx to function, download and install it
first: [link](https://bepinex.github.io/bepinex_docs/master/articles/user_guide/installation/index.html?tabs=tabid-win)

#### Manually

First install the [CommonAPI](https://dsp.thunderstore.io/package/CommonAPI/CommonAPI/) mod. Next install
the [DSPModSave](https://dsp.thunderstore.io/package/CommonAPI/DSPModSave/) mod.
Next, install the [Nebula Multiplayer Mod API](https://dsp.thunderstore.io/package/nebula/NebulaMultiplayerModApi/) mod
Then, extract the archive file and drag `Logistix.dll` into the `BepInEx/plugins` directory.

Dependencies (Thunderstore versions this release was built against):

* `xiaoye97-BepInEx-5.4.17`
* `xiaoye97-LDBTool-3.0.3`
* `CommonAPI-CommonAPI-1.6.7`
* `CommonAPI-DSPModSave-1.2.2`
* `nebula-NebulaMultiplayerModApi-2.1.0`

#### Mod manager

Click the `Install with Mod Manager` link above. Make sure dependencies are installed, when prompted

## Building from source

```
dotnet build Logistix.csproj -c Release
```

produces a Thunderstore-ready `dist/Logistix-<version>.zip`. To cut a release, bump `<Version>` in
`Logistix.csproj` first — it drives both the plugin version and the package's `manifest.json`.
Debug builds do not produce a zip.

## Changelog

#### v1.0.0
Rebrand of PersonalLogistics 2.9.8; new GUID, config and save data start fresh

[PersonalLogistics (upstream) changelog](archived.changelog.md)

## Contact

Bugs? [Create an issue in the github repository](https://github.com/ericfisherdev/Logistix/issues).
