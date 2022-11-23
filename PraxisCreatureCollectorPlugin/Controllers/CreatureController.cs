using Google.OpenLocationCode;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PraxisCore;
using PraxisCore.Support;
using System.Text;
using static CreatureCollectorAPI.CommonHelpers;
using static CreatureCollectorAPI.CreatureCollectorGlobals;
using static PraxisCore.DbTables;

namespace CreatureCollectorAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CreatureController : Controller, IPraxisPlugin
    {
        private readonly IConfiguration Configuration;
        private static IMemoryCache cache;

        //Data notes:
        //Players have:
        //account - core account data, encrypted
        //team - separate to allow for server stat collection and high scores, plaintext.
        //creatureInfo - their creatures and the stats on each of them.
        //tutorialInfo - which tutorial scenes the player has seen.

        //areas have:
        //teamOwning - which color team control this area. plaintext
        //rank - how many tiers of a pyramid this area has for pvp purposes. plaintext
        //players - which players are currently here. encrypted with system password.

        public CreatureController(IConfiguration configuration, IMemoryCache memoryCacheSingleton)
        {
            Configuration = configuration;
            cache = memoryCacheSingleton;
        }

        [HttpGet]
        [Route("/[controller]/Test")]
        public string Test()
        {
            string results = "OK";
            return results;
        }

        public void Spawn(string plusCode)
        {
            if (!DataCheck.IsInBounds(plusCode))
                return;

            GeoArea box = OpenLocationCode.DecodeValid(plusCode);
            var alldata = GenericData.GetAllDataInArea(box);
            var data = alldata.Where(d => d.key == "creature").ToList();

            var spawnLock = GetUpdateLock(plusCode);
            if (spawnLock.counter > 1) //Someone else is working on it, we don't need to wait
            {
                DropUpdateLock(plusCode, spawnLock);
                return;
            }
            lock (spawnLock)
            {
                //Generate spawn table
                List<Creature> localSpawnTable = GenerateSpawnTable(plusCode, out var terrainInfo);                
                RunSpawnProcess(data, "creature", localSpawnTable, terrainInfo);
            }
            DropUpdateLock(plusCode, spawnLock);
            return;
        }

        public static CreatureInstance MakeRandomCreatureInstance(List<Creature> spawnTable)
        {
            CreatureInstance c = new CreatureInstance();
            var creatureSpawned = spawnTable[Random.Shared.Next(spawnTable.Count)];
            c.name = creatureSpawned.name;
            c.uid = Guid.NewGuid();
            c.id = creatureSpawned.id;
            c.activeGame = ActiveChallengeOptions[Random.Shared.Next(ActiveChallengeOptions.Length)].ToString(); //FUTURE TODO: pull from selected creature instead of global, once creatures can set their own challenge list.
            c.difficulty = creatureSpawned.activeCatchDifficulty;
            return c;
        }

        public static AreaGameData MakeCreatureSpawn(string dataKey, FindPlaceResult spawnArea, List<Creature> spawnTable)
        {
            var entry = new AreaGameData();
            entry.DataKey = dataKey;
            entry.PlusCode = spawnArea.plusCode;
            entry.GeoAreaIndex = Converters.GeoAreaToPolygon(OpenLocationCode.DecodeValid(entry.PlusCode));
            entry.Expiration = DateTime.UtcNow.AddSeconds((double)Random.Shared.Next(config.CreatureDurationMin, config.CreatureDurationMax));
            var creature = MakeRandomCreatureInstance(spawnTable);
            entry.DataValue = creature.ToJsonByteArray();
            return entry;
        }

        public static void RunSpawnProcess(List<CustomDataAreaResult> data, string dataKey, List<Creature> spawnTable, List<FindPlaceResult> terrainInfo)
        {
            var occupied = data.Where(d => d.key == dataKey).ToLookup(k => k.plusCode); //We will never pull a creature into a cell that has a spawn on any of the lists.
            terrainInfo = terrainInfo.Where(t => !occupied.Contains(t.plusCode)).ToList();
            var walkable = terrainInfo.Where(t => walkableAreas.Contains(t.data.areaType)).ToList();
            var other = terrainInfo.Where(t => !walkableAreas.Contains(t.data.areaType)).ToList();

            var minWalkable = walkable.OrderBy(o => Random.Shared.Next()).Take((int)config.MinWalkableSpacesOnSpawn).ToList();
            var minOther = other.OrderBy(o => Random.Shared.Next()).Take((int)config.MinOtherSpacesOnSpawn).ToList();
            var rest = terrainInfo
                .OrderBy(o => Random.Shared.Next())
                .Where(r => !minWalkable.Any(w => w.plusCode == r.plusCode) && !minOther.Any(o => o.plusCode == r.plusCode))
                .Take((int)config.CreaturesPerCell8 - minWalkable.Count - minOther.Count)
                .ToList();
            rest = rest.ToList(); //Dodge duplicate entries, ensure we still have our minimum values.

            List<AreaGameData> results = new List<AreaGameData>((int)config.CreaturesPerCell8);
            var db = new PraxisContext();

            for (int i = 0; i < minWalkable.Count(); i++)
            {
                results.Add(MakeCreatureSpawn(dataKey, minWalkable[i], spawnTable));
            }

            for (int i = 0; i < minOther.Count(); i++)
            {
                results.Add(MakeCreatureSpawn(dataKey, minOther[i], spawnTable));
            }

            for (int i = 0; i < rest.Count(); i++)
            {
                results.Add(MakeCreatureSpawn(dataKey, rest[i], spawnTable));
            }

            foreach (var p in spawnTable.Where(s => s.isPermanent).Distinct())
            {
                //NOTE: may need to remove/replace existing entries.
                var entry = new AreaGameData();
                entry.DataKey = dataKey;
                entry.PlusCode = p.specificSpawns[Random.Shared.Next(p.specificSpawns.Count)];
                entry.GeoAreaIndex = Converters.GeoAreaToPolygon(OpenLocationCode.DecodeValid(entry.PlusCode));
                entry.Expiration = DateTime.UtcNow.AddSeconds(config.CreatureDurationMax);
                CreatureInstance ci = new CreatureInstance() { activeGame = ActiveChallengeOptions[Random.Shared.Next(ActiveChallengeOptions.Length)].ToString(), difficulty = p.activeCatchDifficulty, id = p.id, name = p.name, uid = Guid.NewGuid() };
                entry.DataValue = ci.ToJsonByteArray();
                results.Add(entry);
            }

            db.AreaGameData.AddRange(results);
            db.SaveChanges();
        }

        public static void SetAreaDataFast(string plusCode, string key, byte[] value, double? expiration = null)
        {
            //I save this all above in RunSpawnProcessList with an AddRange call.
            //Since this controller is running server-side, I KNOW I am not attaching player data to a location, 
            //and will skip the checks for such.
            var db = new PraxisContext();
            var row = db.AreaGameData.FirstOrDefault(p => p.PlusCode == plusCode && p.DataKey == key);
            if (row == null)
            {
                row = new DbTables.AreaGameData();
                row.DataKey = key;
                row.PlusCode = plusCode;
                row.GeoAreaIndex = Converters.GeoAreaToPolygon(OpenLocationCode.DecodeValid(plusCode));
                db.AreaGameData.Add(row);
            }
            if (expiration.HasValue)
                row.Expiration = DateTime.UtcNow.AddSeconds(expiration.Value);
            else
                row.Expiration = null;
            row.IvData = null;
            row.DataValue = value;
            db.SaveChanges();
        }

        [HttpGet]
        [Route("/[controller]/MinMax/")]
        public string DumpCreatureMathSpreadsheet()
        {
            //A function for an admin to call, to get some stats on the creature array in the game.
            StringBuilder results = new StringBuilder();

            //header
            results.AppendLine("Name\tCatchPerLevel\tToLevel30\t30Strength\t30Defense\t30Scouting\t30Area\tRadiusPerCatch30\tAreaPerCatch30\tCatchesToRadius50\tIdleDaysToLevel30\tCostInShop");

            foreach (var c in creatureList)
            {
                long catchesTo50 = 0;
                results.Append(c.name + "\t");
                results.Append(c.stats.multiplierPerLevel + "\t");

                PlayerCreatureInfo testCi = new PlayerCreatureInfo();
                testCi.id = c.id;
                while (testCi.level < 30)
                {
                    testCi.BoostCreature();
                    if (catchesTo50 == 0 && testCi.scouting > 50)
                        catchesTo50 = testCi.totalCaught;
                }

                results.Append(testCi.totalCaught + "\t");
                results.Append(testCi.strength + "\t");
                results.Append(testCi.defense + "\t");
                results.Append(testCi.scouting + "\t");
                double area = testCi.scouting * testCi.scouting * 3.14;
                results.Append(area.ToString() + "\t");

                results.Append(testCi.scouting / (double)testCi.totalCaught + "\t");
                results.Append(area / testCi.totalCaught + "\t");

                var idleTimeTo30 = testCi.totalCaught / 2; //How many days it takes for a creature sitting in the Improve box to hit level 30 from 1 fragment.

                while (catchesTo50 == 0)
                {
                    testCi.BoostCreature();
                    if (testCi.scouting > 50)
                        catchesTo50 = testCi.totalCaught;
                }

                results.Append(catchesTo50 + "\t");
                results.Append(idleTimeTo30 + "\t");
                results.Append(CommonHelpers.DetermineCoinCost(c) + "\t");
                results.AppendLine();
            }

            return results.ToString();
        }

        [HttpPut]
        [Route("/[controller]/ChallengeDone/{baseCreatureId}")]
        public void BoostChallengeCreature(long baseCreatureId)
        {
            Response.Headers.Add("X-noPerfTrack", "Creature/ChallengeDone/VARSREMOVED");
            GetAuthInfo(Response, out var accountId, out var password);
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                var eliteCreature = creaturesById[baseCreatureId].eliteId;
                var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                var creatureExists = creatureData.TryGetValue(eliteCreature, out var creature);
                if (!creatureExists)
                {
                    creature = new PlayerCreatureInfo();
                    creature.id = eliteCreature;
                    creatureData.Add(eliteCreature, creature);
                }
                creature.BoostCreature();
                GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
            }
            DropUpdateLock(accountId, playerLock);
        }

        [HttpPut]
        [Route("/[controller]/Enter/{plusCode}")]
        public EnterAreaResults ProcessStepIntoArea(string plusCode)
        {
            //player walks into tile, check Dictionary<String, DateTime> to see if they get a coin grant, and then check if there's a creature they catch.
            Response.Headers.Add("X-noPerfTrack", "Creature/Enter/VARSREMOVED");
            if (!DataCheck.IsInBounds(plusCode))
                return null;
            
            GetAuthInfo(Response, out var accountId, out var password);

            var results = new EnterAreaResults();
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                Account account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
                var grantBlocks = GenericData.GetSecurePlayerData<Dictionary<string, DateTime>>(accountId, "grantBlocks", password);
                if (grantBlocks == null)
                    grantBlocks = new Dictionary<string, DateTime>();

                var checkTimer = grantBlocks.TryGetValue(plusCode, out var allowGrant);
                if (!checkTimer || allowGrant < DateTime.UtcNow) //We only clear old values on allow, so we do need to check if this timer expired.
                {
                    int coinsGranted = 0;
                    coinsGranted = Random.Shared.Next(3, 7);
                    if (checkTimer == false)
                        grantBlocks.Add(plusCode, DateTime.UtcNow.AddHours(22));
                    else
                        allowGrant = DateTime.UtcNow.AddHours(22);
                    grantBlocks = grantBlocks.Where(g => g.Value > DateTime.UtcNow).ToDictionary(k => k.Key, v => v.Value);

                    account.currencies.baseCurrency += coinsGranted;
                    account.totalGrants++;

                    //Ideal Additional check: have caught at least 50% of creatures on list. (player's creature list count is half of total creature list count). Doable with active challenges and store fairly easily.
                    //TODO: juggle around loading/saving values to be able to check creatures and total grants at the same time.
                    if (!account.graduationEligible && account.totalGrants > graduateGrantsCount) //Are there additional critera to add here in the future?
                    {
                        account.graduationEligible = true;
                    }
                    results.coinsGranted = coinsGranted;

                    GenericData.SetSecurePlayerDataJson(accountId, "grantBlocks", grantBlocks, password);
                    GenericData.SetSecurePlayerDataJson(accountId, "account", account, password);
                }

                //Player catches everything in their Cell10 and the neighboring cell10s
                GeoArea playerCell = OpenLocationCode.DecodeValid(plusCode);
                GeoArea radius = new GeoArea(playerCell.SouthLatitude - ConstantValues.resolutionCell10 - .000001, playerCell.WestLongitude - ConstantValues.resolutionCell10 - .000001, playerCell.NorthLatitude + ConstantValues.resolutionCell10 + .000001, playerCell.EastLongitude + ConstantValues.resolutionCell10 + .000001);
                var allCreatures = GenericData.GetAllDataInArea(radius);

                allCreatures = allCreatures.Where(c => c.key == "creature").ToList();

                List<Guid> recentlyCaught = new List<Guid>();
                Dictionary<long, PlayerCreatureInfo> creatureData = new Dictionary<long, PlayerCreatureInfo>();
                if (allCreatures.Count > 0)
                {
                    creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                    if (creatureData == null)
                        creatureData = new Dictionary<long, PlayerCreatureInfo>();
                    recentlyCaught = GenericData.GetSecurePlayerData<List<Guid>>(accountId, "recentlyCaught", password);
                    if (recentlyCaught == null)
                        recentlyCaught = new List<Guid>();
                }

                bool save = false;
                foreach (var c in allCreatures)
                {
                    var creatureInstance = c.value.FromJsonTo<CreatureInstance>();
                    if (recentlyCaught.Count() > 499)
                        recentlyCaught.RemoveAt(0);
                    if (!recentlyCaught.Contains(creatureInstance.uid))
                    {
                        results.creatureIdCaught = creatureInstance.id;
                        recentlyCaught.Add(creatureInstance.uid);

                        var creatureExists = creatureData.TryGetValue(creatureInstance.id, out var creature);
                        if (!creatureExists)
                        {
                            creature = new PlayerCreatureInfo();
                            creature.id = creatureInstance.id;
                            creatureData.Add(creatureInstance.id, creature);
                        }
                        creature.BoostCreature();
                        results.creatureIdCaught = creatureInstance.id;
                        results.creatureUidCaught = creatureInstance.uid;
                        results.plusCode = c.plusCode.Substring(0, 8);
                        results.activeGame = creatureInstance.activeGame;
                        results.difficulty = creatureList.First(c => c.id == creatureInstance.id).activeCatchDifficulty;
                        save = true;
                    }
                }

                if (save)
                {
                    GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
                    GenericData.SetSecurePlayerDataJson(accountId, "recentlyCaught", recentlyCaught, password);
                }

                GenericData.SetPlayerData(accountId, "GrantLock", "2".ToByteArrayUTF8(), 2); //Only grant coins every 2 seconds, should allow bikers to get full credit and drivers half credit.
            }
            cache.Set("processLock-" + accountId, true, TimeSpan.FromSeconds(2));
            DropUpdateLock(accountId, playerLock);
            return results;
        }

        [HttpGet]
        [Route("/[controller]/SpawnOdds/{plusCode}")]
        public string ListSpawnTableOdds(string plusCode)
        {
            if (!DataCheck.IsInBounds(plusCode))
                return null;

            List<FindPlaceResult>? terrainInfo = new List<FindPlaceResult>();
            List<Creature> localSpawnTable = GenerateSpawnTable(plusCode, out terrainInfo);

            var totalEntries = localSpawnTable.Count;
            var combo = localSpawnTable.GroupBy(l => l.id);

            string results = "Total Entries:" + totalEntries + "\n";
            foreach (var c in combo)
                results += c.First().name + ": " + c.Count() + "(" + Math.Round((1.0 * c.Count() / totalEntries) * 100, 2) + "%)\n";

            return results;
        }

        [HttpGet]
        [Route("/[controller]/Wild/{plusCode8}/")]
        public List<CustomDataAreaResult> GetWildCreatures(string plusCode8)
        {
            //This is per-client results because we filter out ones they've already caught. We can cache the base list but we need to process it anyways each call.
            if (!DataCheck.IsInBounds(plusCode8))
            {
                Response.Headers.Add("X-noPerfTrack", "Creature/Wild/VARSREMOVED"); //Set here because its normally set later in this function and these values must be blocked.
                return null;
            }
            GetAuthInfo(Response, out var accountId, out var password);
            List<CustomDataAreaResult> results;

            var dataKey = "creature";
            var account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);

            results = GenericData.GetAllDataInArea(plusCode8); //MariaDB won't run AllData with a subkey.
            results = results.Where(r => r.key == dataKey).ToList();

            if (results.Count <= config.CreatureCountToRespawn)
            {
                Spawn(plusCode8);
                return GetWildCreatures(plusCode8);
            }

            var recentlyCaught = GenericData.GetSecurePlayerData<List<Guid>>(accountId, "recentlyCaught", password);
            if (recentlyCaught == null)
                recentlyCaught = new List<Guid>();

            var toRemove = new List<int>();
            for (int i = results.Count - 1; i >= 0; i--) //Backwards since these indexes change if we remove them in ascending order.
            {
                var instance = results[i].value.FromJsonTo<CreatureInstance>();
                if (recentlyCaught.Contains(instance.uid))
                {
                    toRemove.Add(i);
                }
            }

            foreach (var removed in toRemove)
                results.RemoveAt(removed);

            Response.Headers.Add("X-noPerfTrack", "Creature/Wild/VARSREMOVED"); //Set here because it fails on the recursive call when spawning creatures if you add a header twice.
            return results;
        }

        [HttpGet]
        [Route("/[controller]/CreatureDataVersion")]
        public string GetCreatureDataVersion()
        {
            //can be cached for duration of the run. reload on server restart.
            if (cache.TryGetValue("creatureDataVersion", out string versionId))
                return versionId;

            string version = GenericData.GetGlobalData("creatureDataVersion").ToUTF8String();
            cache.Set("creatureDataVersion", version);
            return version;
        }

        [HttpGet]
        [Route("/[controller]/CreatureData")]
        public List<Creature> getCreatureData()
        {
            return creatureList;
        }

        [HttpGet]
        [Route("/[controller]/CreatureInfo/")]
        public Dictionary<long, PlayerCreatureInfo> getPlayerCreatureInfo()
        {
            Response.Headers.Add("X-noPerfTrack", "Creature/CreatureInfo/VARSREMOVED");
            GetAuthInfo(Response, out var accountId, out var password);
            var results = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
            return results;
        }

        //Maybe this is a Tibo endpoint.It probably should be. TODO move this, TODO make this only area.
        [HttpPut]
        [Route("/[controller]/Graduate/{creatureChosenId}/{area}")]
        public bool Graduate(long creatureChosenId, string area)
        {
            GetAuthInfo(Response, out var accountId, out var password);
            //when a player graduates, update spawn data to make the creature they chose have 1 more entry in the spawn table for the selected area.
            ////Place and Type were considered, but I've chosen not to allow those for now.
            var creature = creaturesById[creatureChosenId];

            long count = 0;
            if (DataCheck.IsPlusCode(area)) //the player can't make something spawn globally, though a Cell2 would be allowed.
            {
                //update creature spawn for selected plus code.
                if (!creature.areaSpawns.TryGetValue(area, out count))
                    creature.areaSpawns.Add(area, 1);
                else
                    creature.areaSpawns[area] = count + 1;
            }

            //update database tags.
            var dbLock = GetUpdateLock("spawnList");
            lock (dbLock)
            {
                GenericData.SetGlobalDataJson("creatureData", creatureList);
                var version = GenericData.GetGlobalData("creatureDataVersion").ToUTF8String();
                var intVersion = version.ToInt();
                intVersion++;
                GenericData.SetGlobalData("creatureDataVersion", intVersion.ToString().ToByteArrayUTF8());
            }
            DropUpdateLock("spawnList", dbLock);

            //Now reset the player's account info, except tutorials. This could be a delete/recreate call, but I want to keep their password in tact.
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                Account data = new Account();
                data.name = accountId;
                GenericData.SetSecurePlayerDataJson(accountId, "account", data, password);

                var creatureInfo = MakeStarterCreatureInfo();
                GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureInfo, password);

                var taskInfo = ImprovementTasks.DefaultTasks.ToDictionary(k => k.id, v => v);
                GenericData.SetSecurePlayerDataJson(accountId, "taskInfo", taskInfo, password);

                var grantBlocks = new Dictionary<string, DateTime>();
                GenericData.SetSecurePlayerDataJson(accountId, "grantBlocks", grantBlocks, password);
            }
            DropUpdateLock(accountId, playerLock);
            return true;
        }

        [HttpGet]
        [Route("/[controller]/Possible/{plusCode8}")]
        public List<long> PossibleSpawns(string plusCode8)
        {
            Response.Headers.Add("X-noPerfTrack", "Creature/Possible/VARSREMOVED");
            //This is an array of 3-12 longs, will cache this to save a lot of DB reads (hopefully) without tanking free memory.
            if (cache.TryGetValue("possible-" + plusCode8, out List<long> results))
                return results;

            //Generate the list of creatures that appear in the spawn table, but not the actual spawn tables.
            GeoArea box = plusCode8.ToGeoArea();
            List<long> localSpawnTable = new List<long>(20);
            List<FindPlaceResult> terrainInfo;

            var timeShift = OpenLocationCode.CodeAlphabet.IndexOf(plusCode8[1]) - 9; //OpenLocationCode.CodeAlphabet.IndexOf('F') == 9
            var shiftedTime = DateTime.UtcNow.AddHours(timeShift).AddMinutes(timeShift * 24);

            var places = PraxisCore.Place.GetPlaces(box);
            places = places.Where(p => p.GameElementName != TagParser.defaultStyle.Name).ToList();
            terrainInfo = AreaTypeInfo.SearchArea(ref box, ref places);

            //check terrain
            foreach (var t in terrainInfo)
                if (terrainSpawnTables.ContainsKey(t.data.areaType))
                    localSpawnTable.AddRange(terrainSpawnTables[t.data.areaType].Where(t => t.CanSpawnNow(shiftedTime)).Select(c => c.id).Distinct()); //Each cell10 adds its type's list to the spawn pool

            //check areas.
            foreach (var a in areaSpawnTables.Keys)
            {
                if (plusCode8.StartsWith(a))
                {
                    localSpawnTable.AddRange(areaSpawnTables[a].Where(t => t.CanSpawnNow(shiftedTime)).Select(c => c.id).Distinct());
                }
            }

            //check places.
            foreach (var p in places)
            {
                if (placeSpawnTables.ContainsKey(p.GameElementName))
                    localSpawnTable.AddRange(placeSpawnTables[p.GameElementName].Where(t => t.CanSpawnNow(shiftedTime)).Select(c => c.id).Distinct());
            }

            var final = localSpawnTable.Distinct().ToList();
            cache.Set("possible-" + plusCode8, final, AbsoluteExpiration15Min);
            return final;
        }

        [HttpPut]
        [Route("/[controller]/Vortex/{plusCode8}")]
        public Dictionary<long, long> Vortex(string plusCode8)
        {
            //TODO: This is 80% identical to ProcessStepIntoArea, except it's a custom radius and doesn't grant coins.
            //Player used a token, catch all uncaught creatures in a 3x3 Cell8 area from their current Cell8.
            //Functionalize that so I can clean up the shared code.

            long vortexMin = config.CreatureCountToRespawn;
            Response.Headers.Add("X-noPerfTrack", "Creature/Vortex/VARSREMOVED");
            GetAuthInfo(Response, out var accountId, out var password);

            //Sanity checks
            if (plusCode8.Length != 8)
                return null;

            var account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
            if (account.currencies.vortexTokens <= 0)
                return null;

            var results = new Dictionary<long, long>(); //id, count

            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                int actualCaughtTotal = 0;
                var baseGeoArea = plusCode8.ToGeoArea().PadGeoArea(ConstantValues.resolutionCell8);
                var allCreatures = GenericData.GetAllDataInArea(baseGeoArea).Where(d => d.key == "creature");

                List<Guid> recentlyCaught = new List<Guid>();
                Dictionary<long, PlayerCreatureInfo> creatureData = new Dictionary<long, PlayerCreatureInfo>();
                if (allCreatures.Count() > vortexMin)
                {
                    account.currencies.vortexTokens--;
                    creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                    if (creatureData == null)
                        creatureData = new Dictionary<long, PlayerCreatureInfo>();
                    recentlyCaught = GenericData.GetSecurePlayerData<List<Guid>>(accountId, "recentlyCaught", password);
                    if (recentlyCaught == null)
                        recentlyCaught = new List<Guid>();

                    foreach (var c in allCreatures)
                    {
                        var creatureInstance = c.value.FromJsonTo<CreatureInstance>();
                        if (recentlyCaught.Count() > 499)
                            recentlyCaught.RemoveAt(0);
                        if (!recentlyCaught.Contains(creatureInstance.uid))
                        {
                            if (!results.ContainsKey(creatureInstance.id))
                                results.Add(creatureInstance.id, 1);
                            else
                                results[creatureInstance.id] += 1;

                            recentlyCaught.Add(creatureInstance.uid);
                            actualCaughtTotal++;

                            var creatureExists = creatureData.TryGetValue(creatureInstance.id, out var creature);
                            if (!creatureExists)
                            {
                                creature = new PlayerCreatureInfo();
                                creature.id = creatureInstance.id;
                                creatureData.Add(creatureInstance.id, creature);
                            }
                            creature.BoostCreature();
                        }
                    }
                }
                
                if (actualCaughtTotal < vortexMin) //don't save, this is mostly a waste of a token and probably an accidental button tap.
                {
                    return null;
                }
                GenericData.SetSecurePlayerDataJson(accountId, "account", account, password);
                GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
                GenericData.SetSecurePlayerDataJson(accountId, "recentlyCaught", recentlyCaught, password);
            }

            return results;
        }
    }
}