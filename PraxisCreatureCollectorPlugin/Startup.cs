using PraxisCreatureCollectorPlugin.Controllers;
using Microsoft.EntityFrameworkCore;
using PraxisCore;
using PraxisCore.Support;
using PraxisMapper.Classes;
using static PraxisCreatureCollectorPlugin.CreatureCollectorGlobals;

namespace PraxisCreatureCollectorPlugin
{
    public class CreatureStartup : IPraxisStartup
    {
        static bool initialized = false;
        public static void Startup()
        {
            if (initialized)
                return;

            var praxisDb = new PraxisContext();

            config = new Config()
            {
                CreatureCountToRespawn = 6,
                CreatureDurationMin = 1800,
                CreatureDurationMax = 3600,
                CreaturesPerCell8 = 18,
                MinWalkableSpacesOnSpawn = 6,
                MinOtherSpacesOnSpawn = 6,
                placeIncludes = new List<string>() { "162061-3" },
                placeExcludes = new List<string>() { "4039900-3" },
                nestsEnabled = true,
                NPCsEnabled = true,
                CoinGrantLockoutSeconds = 2,
            };

            config.LoadFromDatabase();

            if (GenericData.GetGlobalData("artVersion").Length == 0)
                GenericData.SetGlobalData("artVersion", "1".ToByteArrayUTF8());

            CreatureCollectorGlobals.internalPassword = PraxisMapper.Startup.Configuration.GetValue<string>("CreatureInternalPassword");
            if (internalPassword == null)
            {
                ErrorLogger.LogError(new Exception("HEY ADMIN - you need to add a value for CreatureInternalPassword to appsettings.json"));
                PraxisPerformanceTracker.LogInfoToPerfData("CreatureCollector.Startup", "HEY ADMIN - you need to add a value for CreatureInternalPassword to appsettings.json");
            }

            //Add some paths to the PraxisAuth whitelist so we can use some things that don't need it.
            PraxisMapper.Classes.PraxisAuthentication.whitelistedPaths.Add("/Tibo/Slippy"); //Includes SlippyPlaceData since it's a Contains() check.
            PraxisMapper.Classes.PraxisAuthentication.whitelistedPaths.Add("/MapTile/Slippy"); //Includes SlippyPlaceData since it's a Contains() check.

            //Load up the information on the play boundaries before creatures, so we can remove creatures NOT within them from the list.
            bool hasLoadedArea = false;
            gameplayAreas = TagParser.allStyleGroups["mapTiles"].Values.Where(v => v.IsGameElement == true).Select(v => v.Name).ToList();
            foreach (var p in config.placeIncludes)
            {
                string[] splitparts = p.Split("-");
                long[] parts = splitparts.Select(part => part.ToLong()).ToArray();

                var includeElement = praxisDb.Places.Include(p => p.Tags).FirstOrDefault(p => p.SourceItemID == parts[0] && p.SourceItemType == parts[1]);
                if (includeElement == null)
                    continue;
                if (hasLoadedArea == false) {
                    playBoundary = includeElement.ElementGeometry;
                    hasLoadedArea = true;
                }
                else
                    playBoundary.Union(praxisDb.Places.Include(p => p.Tags).First(p => p.SourceItemID == parts[0] && p.SourceItemType == parts[1]).ElementGeometry);
            }

            foreach (var p in config.placeExcludes)
            {
                string[] splitparts = p.Split("-");
                long[] parts = splitparts.Select(part => part.ToLong()).ToArray();
                var excludeElement = praxisDb.Places.Include(p => p.Tags).FirstOrDefault(p => p.SourceItemID == parts[0] && p.SourceItemType == parts[1]);
                if (excludeElement == null) 
                    continue;
                playBoundary = playBoundary.Difference(excludeElement.ElementGeometry);
            }

            //Because graduating users could update spawn data, we use the database as the authoritative list, but we do add new creatures that aren't on the list in.
            //If a creature's default data changes, we need to check and change everything but the default area spawns.
            bool updateVersion = false;
            var currentData = GenericData.GetGlobalData<List<Creature>>("creatureData");
            var defaultCreatures = Creature.MakeCreatures();

            if (currentData == null)
            {
                currentData = defaultCreatures;
                updateVersion = true;
            }

            if (currentData.Count() != defaultCreatures.Count())
                updateVersion = true;

            foreach (var d in defaultCreatures)
            {
                if (!currentData.Any(c => c.id == d.id))
                    currentData.Add(d);

                //check if anything besides graduating player entries has updated. Graduation should be only area spawns.
                var c = currentData.FirstOrDefault(c => c.id == d.id);
                d.areaSpawns = c.areaSpawns; //apply player contributions to wild creatures before comparing objects.
                var dAsJson = d.ToJson();
                var cAsJson = c.ToJson();

                if (dAsJson != cAsJson)
                {
                    c = d;
                    updateVersion = true;
                }
            }

            if (updateVersion)
            {
                GenericData.SetGlobalDataJson("creatureData", currentData);
                var version = GenericData.GetGlobalData("creatureDataVersion").ToUTF8String();
                var intVersion = 0;
                if (version == "")
                    intVersion = 1;
                else
                {
                    intVersion = version.ToInt();
                    intVersion++;
                }
                GenericData.SetGlobalData("creatureDataVersion", intVersion.ToString().ToByteArrayUTF8());
            }

            creatureList = currentData;
            CreatureCollectorGlobals.graduateCreatureCount = creatureList.Count / 2;
            passportRewards = creatureList.Where(c => c.passportReward == true && !c.isHidden).ToList();
            creaturesById = creatureList.ToDictionary(k => k.id, v => v);
            //creaturesByName = creatureList.ToDictionary(k => k.name, v => v);
            additionalSpawns = creatureList.Where(c => c.isWild).ToList(); //don't want elites or prizes, just all valid wild creatures.
            wanderingCreatures = creatureList.Where(c => c.wanderOdds > 0 && !c.isHidden).ToList(); //can include wild spawns! Some will wander and have a fixed point.

            foreach (var c in creatureList.Where(l => l.isWild && !l.isHidden)) //only creatures that actually spawn.
            {
                foreach (var t in c.terrainSpawns)
                {
                    terrainSpawnTables.TryAdd(t.Key, new List<Creature>());
                    var tst = terrainSpawnTables[t.Key];
                    for (int i = 0; i < t.Value; i++)
                        tst.Add(c);
                }

                foreach (var a in c.areaSpawns)
                {
                    areaSpawnTables.TryAdd(a.Key, new List<Creature>());
                    var ast = areaSpawnTables[a.Key];
                    for (int i = 0; i < a.Value; i++)
                        ast.Add(c);
                }

                foreach (var p in c.placeSpawns)
                {
                    placeSpawnTables.TryAdd(p.Key, new List<Creature>());
                    var pst = placeSpawnTables[p.Key];
                    for (int i = 0; i < p.Value; i++)
                        pst.Add(c);
                }
            }

            TagParser.InsertStyles(Styles.TCstyle);
            TagParser.InsertStyles(Styles.CompeteStyle);
            TagParser.InsertStyles(Styles.coverStyle);
            TagParser.InsertStyles(Styles.biomes);

            praxisDb.SaveChanges();
            CompeteController.InitializeCompeteMode();

            initialized = true;
        }
    }
}