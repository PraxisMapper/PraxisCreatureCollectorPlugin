# PraxisCreatureCollectorPlugin
Server-side logic for Creature Collector games.

# Setup
* Have a PraxisMapper instance setup and running. See the documentation on the PraxisMapper repo for details.
* Add a value to the end of your appsettings.json file. The plugin will use this to encrypt its shared data.
* * "CreatureInternalPassword": "Use_some_good_secure_value_4_this!"
* Copy the PraxisCreatureCollectorPlugin DLL to the plugins folder in PraxisMapper, then restart the server, hit the /Creature/Test endpoint, and restart the server again to make sure all initialization stuff has been done server-side.
* In the database, edit the placeIncludes values in the GlobalDataEntries table to contain the OSM elements you are using as your boundaries, as an array of strings, with -3 indicating a Relation or a -2 indicating a Way. (EX: use ["162061-3"] to have Ohio as your gameplay boundaries)
* Optional: Set the placeExcludes value in the GlobalDataEntries to pick any elements you want to have removed from the gameplay boundaries in the same format (EX: use ["4039900-3"] to have Lake Erie removed from the boundary element.)

# Licensing
The code presented here (settings and .lua files) are under the MIT license.
Graphics are released under a CC BY-SA 4.0 license.
