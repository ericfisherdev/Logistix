## PersonalLogistics (upstream) changelog

Logistix 1.0.0 is a rebrand of [PersonalLogistics](https://github.com/mattsemar/dsp-personal-logistics) 2.9.8 by Matt Semar. The entries below are its history prior to the rebrand.

#### v2.9.8
Update: update onTechUnlocked signature to match latest game version (0.9.26.12891)

#### v2.9.7
Update: add UI to disable some fuel items from being added to mecha fuel chamber to Request Window

#### v2.9.6
Bugfix: attempt to handle case where main player object becomes inactive
Bugfix: fix error shown on host when client requests item that is not available in any reachable stations

#### v2.9.5
Bugfix: Fix icon

#### v2.9.4
Update: Add item icons to incoming item messages

#### v2.9.3
Bugfix: Fix for errors with deleted stations

#### v2.9.2
Bugfix: Attempted fix for items in PLS not being loaded

#### v2.9.1
Bugfix: Fix issue where some items were not available in network (Failed to load X from logistics stations)

#### v2.9.0 
Update: Add more packets to get Nebula working again for clients.
Update: Lower minAge of buffered items to 20 seconds to make recycling happen faster

#### v2.8.3
Update: Allow disabling item network status tooltips (`showItemTooltips`) 

#### v2.8.2
Bugfix: Fixed issue where requested items could get stuck in the "Cost Calculation pending" state (thanks Stylisher for bug report)

#### v2.8.1
Update: Updated CommonAPI TabSystem usage to use new public methods  

#### v2.8.0
Update: Added ability to show extra tabs on request pages for mods that add extra tabs for items 

#### v2.7.8
Bugfix: Fixed issue with banned in-hand items being duplicated in buffer (thanks KrazePendragon for report) 

#### v2.7.7
Update: Added auto-cancellation for inbound requests that are stuck waiting on warpers
Bugfix: Made escape key work like other UI Windows. Now settings menu won't be shown if Escape is hit with request window open

#### v2.7.6
Bugfix: Fixed issue where orbital collectors would be used as supply despite 'Never Use Mecha Energy' config being enabled

#### v2.7.5
Bugfix: Fixed bug with auto-refueling. Thanks to nirahiel for bug report. Big oops, very sorry

#### v2.7.4
Bugfix: Fix situation where a store task could be retried indefinitely 

#### v2.7.3
Bugfix: Another tweak to IlsDemandRules

#### v2.7.2
Bugfix: Resolved bug introduced in 2.7.1 causing shipping failures (thanks DocHogan for report)
        Resolved bug where ILS stations can ship to player on same planet even with local demand/remote supply

#### v2.7.1
Bugfix: Fix issue with exception thrown while displaying item tooltip (thanks sparky#1253 for report)

#### v2.7.0
Feature: Added support for proliferator points on items delivered to inventory. This is still a bit of a work in progress so please let me know if you see issues.
Bugfix: fixed incoming items area position for clients with reference height set to less than 1000 (thanks Cringely for report) 

#### v2.6.4
Bugfix: Band-aid patch for mysterious NRE on startup (thanks Issytia for report)

#### v2.6.3
Bugfix: fix UI bug with incoming items blocking interaction with world (thanks sparky#1253 for report)

#### v2.6.2
Update: Update to work with game version released 20-Jan-2022 (0.9.24.11187), make sure to update to CommonAPI 1.3+

#### v2.6.1
Bugfix: Fixed issue where warper calculation was not honoring new config property for in-system planets

#### v2.6.0
* Update: Changed incoming item message to show the actual amount being transported to buffer instead of the amount needed for request. This amount will be up to the current logistic vessel capacity and the extra items are stored in your local buffer until needed
* Feature: Added new request mode (Planetary), see "Request Modes" section for more info (thanks zxcvbnm3057 for suggestion)
* Bugfix: No Warpers in ILS, happens if you turn the min distance to enable warp on a station down, but leave "warpers required" checked (thanks DogHogan for report)
* Feature: New config, "Warp Enable Min AU". Setting this above 0 lets you override the min distance to enable warp set on individual stations
* Feature: Added stack size for items to request window

#### v2.5.3
Bugfix: Fixed issue where exception would be thrown when quitting one game and creating another (thanks Valoneu for report) 

#### v2.5.2
Bugfix: Fixed issue where 'New Text' is shown when game is started with ShowIncomingItemProgress disabled (thanks Valoneu for report) 

#### v2.5.1
Feature: Added config for minimum stacks to load from network. Open config tab of legacy UI to configure
Feature: Added configs to disable using mecha energy & warpers for shipping costs. Use with caution, especially the warpers one. Open config tab of legacy UI to set up
Bugfix: Fixed issue where 'Shipment delayed' messages would be shown before shipping costs were attempted the first time 

#### v2.5.0
Feature: Added support for the Nebula Multiplayer mod. This worked previously, but the item buffer and requested items would not be saved between sessions for clients 

#### v2.4.0
Feature: added messages to the incoming items area for items that are being loaded from the Buffer into the inventory, see the 'Incoming Item Notifications' section for more detail     

#### v2.3.0
Feature: added numerical indicators to Requests window icons to make it easier to tell what is requested/banned at a glance   

#### v2.2.1
Bugfix: fixed issue where item icons would not appear in the recycle area (Thanks Speedy on Discord for report)

#### v2.2.1
Bugfix: fixed a longstanding issue where the nearest station would be used to compute costs even if most of the
items are actually coming from other, more distant stations. Now the station that supplies the most items
in the shipment is used for computing the cost.
(Thanks Speedy on discord for bug report)
Tweak: adjusted incoming item text to be a little easier to read against light colored backgrounds

#### v2.2.0
Feature: added play/pause button to request window
Refactor: overhauled state persistence to a more robust approach

#### v2.1.1
Bugfix: fixed issue with loading save where actions for items in recycle area were persisted 

#### v2.1.0
Feature: switched desired inventory state to be persisted with game save, removed support for copying state
from another seed. Added persistence for recycle window contents

#### v2.0.4
Feature: Added popup confirmation before the first time trashed items are recycled automatically.    

#### v2.0.3
Features:
* Added tip for +/- buttons to indicate shift/control for 5 or max 
* Added configurable minimum delay for recycle area. Gives more time to get items back if they were accidentally added   

#### v2.0.2
Bugfix: handled destruction of logistics station while items are being removed from it
Bugfix: fixed issue where recycled item icons were not appearing (blank white square)

#### v2.0.1
Bugfix: resolved issue where request window would not open until after inventory was first opened.

#### v2.0.0
* Overhauled UI for configuring requested items. Updated tooltips to refer to the number of stacks requested/auto-recycled instead of counts. Legacy request config window
is left in place for now, in case of bugs. It can be accessed by clicking the Settings button in the Request Window

#### v1.6.3
* Bugfix: Fix issue with commonapi submodule not being properly initialized. Thanks to tier1thuinfinity on github for bugreport.

#### v1.6.2
* Bugfix: Disable recycle window as Shift/CTRL click target when a station window is open. Also handle the case where other storage window was opened after inv. window

#### v1.6.1
* Feature: Updated Recycle window to allow shift-clicking items to/from inventory. Also added non-persistent checkbox for hiding Recycle section temporarily

#### v1.6.0

* Feature: Added Recycle window where items from inventory can be dropped and automatically sent to Buffer (and then to stations, assuming the item isn't currently requested)

#### v1.5.0

* Switched to storing buffered items and inbound requests using DSPGameSave mod

#### v1.4.0

* Added options to keep Mecha fuel and warpers topped off from player inventory (see Mecha section for more info)
* Updated Incoming Items UI to include amount incoming and changed font to match game UI a little better

#### v1.3.1

Bugfix, fixed issue where planetary bot speed (vs interplanetary vessel speed) would be used for players who have not unlocked warp drive capability. (Thanks to Tivec for bug
report)

* Added 'Cancel requests' button to Actions section to allow canceling inbound requests that have been assigned an arrival time
* Added value to Config section to let player set max time in seconds to wait for item arrival (default 10 minutes)

#### v1.3.0

* Update UI button layout
* Added Clear Buffer button to quickly return all buffered items to Logistics Stations
* Removed fly to build functionality (moved to another plugin, LongArm)

#### v1.2.1

Bugfix, fixed issue in logic of IlsDemandRules mode where remote supply available counts were not being set

#### v1.2.0

Add ability to order mecha to fly to nearest Build Preview location. CTRL+R to toggle (see Build Preview Navigation section above)
Added config option for controlling what types of stations are pulled from. (see Request Modes section for more info)

#### v1.1.2

Bugfix, fixed issue issue where old reference was kept after loading new game save

#### v1.1.1

Bugfix, fixed concurrent modification issue with logistics network Fixed issue where management window close button was not shown

#### v1.1.0

* Fixed longitude labeling for build ghost geo coords (both east and west were labeled 'E')
* Updated inventory checker to count players hand items (so more won't be requested if you pick up all foundation, for example)
* Adjusted incoming items position when using items from inventory for research
* Adjusted logistics drone speed calculation to match game a little better
* Fixed missed sorting of player inventory
* Removed ability to send buffered items to inventory (or network) if there are incoming requests being processed for them (credit: ghostorchidgaming for report)

#### v1.0.9

Bugfix, fixed issue where incoming items would never be shown, oops.

#### v1.0.8

Added indicator text for nearest build ghost (disable with config option ShowNearestBuildGhostIndicator). Added config option to hide incoming items text (ShowIncomingItemProgress)
Adjusted incoming items indicator text positioning

#### v1.0.7

Updated handling of buffered item removal to time it better with inventory insertion. Began preliminary work on support for bypassing buffering for certain items

#### v1.0.5

Fixed bug with handling of max requested amounts. Updated usage of mecha core energy for logistics tasks to align better with the game's implementation

#### v1.0.4

Switched buffered item age to be based off of game time so that buffered items are not expired immediately after loading a savegame

#### v1.0.3

Updated action button position

#### v1.0.2

Added buffered item view, moved button to avoid conflict with other mods. Made sending trash to network default to true

#### v1.0.1

First version
