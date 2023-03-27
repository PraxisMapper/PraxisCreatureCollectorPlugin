using Google.OpenLocationCode;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using PraxisCore;
using PraxisCore.Support;
using PraxisMapper.Classes;
using System.Text;
using static PraxisCreatureCollectorPlugin.CommonHelpers;
using static PraxisCreatureCollectorPlugin.CreatureCollectorGlobals;
using static PraxisCore.DbTables;

namespace PraxisCreatureCollectorPlugin.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CreatureController : Controller, IPraxisPlugin
    {
        private readonly IConfiguration Configuration;
        private static IMemoryCache cache;

        string accountId, password;
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            PraxisAuthentication.GetAuthInfo(Response, out accountId, out password);
        }

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

        public void Spawn(string plusCode, List<AreaData> existingCreatures)
        {
            List<Creature> localSpawnTable = GenerateSpawnTable(plusCode, out var terrainInfo);
            RunSpawnProcess(existingCreatures, localSpawnTable, terrainInfo);
            return;
        }

        public static CreatureInstance MakeRandomCreatureInstance(List<Creature> spawnTable)
        {
            CreatureInstance c = new CreatureInstance();
            var creatureSpawned = spawnTable.PickOneRandom();
            c.name = creatureSpawned.name;
            c.uid = Guid.NewGuid();
            c.id = creatureSpawned.id;
            c.activeGame = ActiveChallengeOptions.PickOneRandom().ToString(); //FUTURE TODO: pull from selected creature instead of global, once creatures can set their own challenge list.
            c.difficulty = creatureSpawned.activeCatchDifficulty;
            return c;
        }

        public static AreaData MakeCreatureSpawn(AreaDetail spawnArea, List<Creature> spawnTable)
        {
            var entry = new AreaData();
            entry.DataKey = "creature";
            entry.PlusCode = spawnArea.plusCode;
            entry.AreaCovered = entry.PlusCode.ToPolygon();
            entry.Expiration = DateTime.UtcNow.AddSeconds((double)Random.Shared.Next(config.CreatureDurationMin, config.CreatureDurationMax));
            var creature = MakeRandomCreatureInstance(spawnTable);
            entry.DataValue = creature.ToJsonByteArray();
            return entry;
        }

        public static void RunSpawnProcess(List<AreaData> existingCreatures, List<Creature> spawnTable, List<AreaDetail> terrainInfo)
        {
            var occupied = existingCreatures.ToLookup(k => k.PlusCode); //We will never pull a creature into a cell that has a spawn on any of the lists.

            int totalCount = (int)config.CreaturesPerCell8;
            int walkableCount = (int)config.MinWalkableSpacesOnSpawn;
            int otherCount = (int)config.MinOtherSpacesOnSpawn;

            //Iterate terrainInfo once, randomize order once. Should be more efficient.
            List<AreaDetail> unoccupied = new List<AreaDetail>(400);
            List<AreaDetail> walkable = new List<AreaDetail>(walkableCount);
            List<AreaDetail> other = new List<AreaDetail>(otherCount);

            foreach (var t in terrainInfo.OrderBy(o => Random.Shared.Next()))
            {
                bool putOnSubList = false;
                if (occupied.Contains(t.plusCode))
                    continue;

                if (walkableAreas.Contains(t.data.style))
                {
                    if (walkable.Count() < walkableCount)
                    {
                        walkable.Add(t);
                        putOnSubList = true;
                    }
                }
                else if (other.Count() < otherCount)
                {
                    other.Add(t);
                    putOnSubList = true;
                }

                if (!putOnSubList)
                    unoccupied.Add(t);

                if (walkableAreas.Count() >= walkableCount && other.Count() >= otherCount && (unoccupied.Count() >= (totalCount - walkableAreas.Count() - other.Count())))
                    break; //we have enough entries to do this task, jump out early.
            }

            var rest = unoccupied
                .Take(totalCount - walkable.Count - other.Count)
                .ToList();

            List<AreaData> results = new List<AreaData>(totalCount);
            var db = new PraxisContext();
            db.ChangeTracker.AutoDetectChangesEnabled = false;

            for (int i = 0; i < walkable.Count(); i++)
                results.Add(MakeCreatureSpawn(walkable[i], spawnTable));

            for (int i = 0; i < other.Count(); i++)
                results.Add(MakeCreatureSpawn(other[i], spawnTable));

            for (int i = 0; i < rest.Count(); i++)
                results.Add(MakeCreatureSpawn(rest[i], spawnTable));

            foreach (var p in spawnTable.Where(s => s.isPermanent).Distinct())
            {
                //Should use MakeCreatureSpawn here.
                string pluscode = p.specificSpawns.PickOneRandom();
                AreaData removeEntry = existingCreatures.FirstOrDefault(d => d.PlusCode == pluscode);
                if (removeEntry != null)
                    db.AreaData.Remove(removeEntry);

                var entry = new AreaData();
                entry.DataKey = "creature";
                entry.PlusCode = pluscode;
                entry.AreaCovered = entry.PlusCode.ToPolygon();
                entry.Expiration = DateTime.UtcNow.AddSeconds(config.CreatureDurationMax);
                CreatureInstance ci = new CreatureInstance() { activeGame = ActiveChallengeOptions.PickOneRandom().ToString(), difficulty = p.activeCatchDifficulty, id = p.id, name = p.name, uid = Guid.NewGuid() };
                entry.DataValue = ci.ToJsonByteArray();
                results.Add(entry);
            }

            db.AreaData.AddRange(results);
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
                results.Append(DetermineCoinCost(c) + "\t");
                results.AppendLine();
            }

            return results.ToString();
        }

        [HttpPut]
        [Route("/[controller]/ChallengeDone/{baseCreatureId}")]
        public void BoostChallengeCreature(long baseCreatureId)
        {
            Response.Headers.Add("X-noPerfTrack", "Creature/ChallengeDone/VARSREMOVED");
            SimpleLockable.PerformWithLock(accountId, () =>
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
            });
        }

        [HttpPut]
        [Route("/[controller]/Enter/{plusCode}")]
        public EnterAreaResults ProcessStepIntoArea(string plusCode)
        {
            //player walks into tile, check Dictionary<String, DateTime> to see if they get a coin grant, and then check if there's a creature they catch.
            Response.Headers.Add("X-noPerfTrack", "Creature/Enter/VARSREMOVED");
            if (!DataCheck.IsInBounds(plusCode))
                return null;

            var results = new EnterAreaResults();
            SimpleLockable.PerformWithLock(accountId, () =>
            {
                Account account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
                var grantBlocks = GenericData.GetSecurePlayerData<Dictionary<string, DateTime>>(accountId, "grantBlocks", password);
                if (grantBlocks == null)
                    grantBlocks = new Dictionary<string, DateTime>();

                var grantLock = GenericData.GetPlayerData(accountId, "GrantLock"); //The player can only get coins every 2 seconds
                if (grantLock.ToUTF8String() != config.CoinGrantLockoutSeconds.ToString())
                {
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
                        GenericData.SetPlayerData(accountId, "GrantLock", config.CoinGrantLockoutSeconds.ToString().ToByteArrayUTF8(), config.CoinGrantLockoutSeconds);
                    }
                }

                //Player catches everything in their Cell10 and the neighboring cell10s
                GeoArea playerCell = OpenLocationCode.DecodeValid(plusCode);
                GeoArea radius = new GeoArea(playerCell.SouthLatitude - ConstantValues.resolutionCell10 - .000001, playerCell.WestLongitude - ConstantValues.resolutionCell10 - .000001, playerCell.NorthLatitude + ConstantValues.resolutionCell10 + .000001, playerCell.EastLongitude + ConstantValues.resolutionCell10 + .000001);
                var allCreatures = GenericData.GetAllDataInArea(radius);
                allCreatures = allCreatures.Where(c => c.DataKey == "creature").ToList();

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
                    var creatureInstance = c.DataValue.FromJsonBytesTo<CreatureInstance>();
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
                        results.plusCode = c.PlusCode.Substring(0, 8);
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
            });
            return results;
        }

        [HttpGet]
        [Route("/[controller]/SpawnOdds/{plusCode}")]
        public string ListSpawnTableOdds(string plusCode)
        {
            if (!DataCheck.IsInBounds(plusCode))
                return null;

            List<AreaDetail>? terrainInfo = new List<AreaDetail>();
            List<Creature> localSpawnTable = GenerateSpawnTable(plusCode, out terrainInfo);

            var totalEntries = localSpawnTable.Count;
            var combo = localSpawnTable.GroupBy(l => l.id);

            string results = "Total Entries:" + totalEntries + "\n";
            foreach (var c in combo)
                results += c.First().name + ": " + c.Count() + "(" + Math.Round((1.0 * c.Count() / totalEntries) * 100, 2) + "%)\n";

            return results;
        }

        public record WildResult(string plusCode, string value);

        [HttpGet]
        [Route("/[controller]/Wild/{plusCode8}/")]
        public List<WildResult> GetWildCreatures(string plusCode8)
        {
            //This is per-client results because we filter out ones they've already caught. We can cache the base list but we need to process it anyways each call.
            if (!DataCheck.IsInBounds(plusCode8))
            {
                Response.Headers.Add("X-noPerfTrack", "Creature/Wild/VARSREMOVED"); //Set here because its normally set later in this function and these values must be blocked.
                return null;
            }
            List<AreaData> results = new List<AreaData>();

            bool callAgain = false;
            SimpleLockable.PerformWithLock("spawnLock" + plusCode8, () =>
            {
                results = GenericData.GetAllDataInArea(plusCode8);
                results = results.Where(r => r.DataKey == "creature").ToList();

                if (results.Count <= config.CreatureCountToRespawn)
                {
                    Spawn(plusCode8, results);
                    callAgain = true;
                }
            });
            if (callAgain)
                return GetWildCreatures(plusCode8);

            var recentlyCaught = GenericData.GetSecurePlayerData<List<Guid>>(accountId, "recentlyCaught", password);
            if (recentlyCaught == null)
                recentlyCaught = new List<Guid>();

            //Proposed new way. One list iteration and creation.
            results = results.Where(r => !recentlyCaught.Contains(r.DataValue.FromJsonBytesTo<CreatureInstance>().uid)).ToList();

            //Original way. Iterates results twice, may redo array each time a removal occurs.
            //var toRemove = new List<int>();
            //for (int i = results.Count - 1; i >= 0; i--) //Backwards since these indexes change if we remove them in ascending order.
            //{
            //    var instance = results[i].value.FromJsonTo<CreatureInstance>();
            //    if (recentlyCaught.Contains(instance.uid)) {
            //        toRemove.Add(i);
            //    }
            //}

            //results = results.Where(r => !recentlyCaught.Contains(r.value.FromJsonTo<CreatureInstance>().uid)).ToList();
            //foreach (var removed in toRemove)
            //    results.RemoveAt(removed);
            //end original way.

            Response.Headers.Add("X-noPerfTrack", "Creature/Wild/VARSREMOVED"); //Set here because it fails on the recursive call when spawning creatures if you add a header twice.
            return results.Select(r => new WildResult(r.PlusCode, r.DataValue.ToUTF8String())).ToList();
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
            var results = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
            return results;
        }

        //Maybe this is a Tibo endpoint.It probably should be. TODO move this, TODO make this only area on the client side.
        [HttpPut]
        [Route("/[controller]/Graduate/{creatureChosenId}/{area}")]
        public bool Graduate(long creatureChosenId, string area)
        {
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

                if (areaSpawnTables.ContainsKey(area))
                    areaSpawnTables[area].Add(creature);
                else
                    areaSpawnTables.Add(area, new List<Creature>() { creature });
            }

            //update database tags.
            SimpleLockable.PerformWithLock("spawnLock" + area, () =>
            {
                GenericData.SetGlobalDataJson("creatureData", creatureList);
                var version = GenericData.GetGlobalData("creatureDataVersion").ToUTF8String();
                var intVersion = version.ToInt();
                intVersion++;
                GenericData.SetGlobalData("creatureDataVersion", intVersion.ToString().ToByteArrayUTF8());
            });

            //Now reset the player's account info, except tutorials. This could be a delete/recreate call, but I want to keep their password in tact.
            SimpleLockable.PerformWithLock(accountId, () =>
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
            });
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

            var spawn = GenerateSpawnTable(plusCode8, out _);
            var possible = spawn.Distinct().Select(s => s.id).ToList();

            cache.Set("possible-" + plusCode8, possible, AbsoluteExpiration15Min);
            return possible;
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

            //Sanity checks
            if (plusCode8.Length != 8)
                return null;

            var account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
            if (account.currencies.vortexTokens <= 0)
                return null;

            var results = new Dictionary<long, long>(); //id, count

            SimpleLockable.PerformWithLock(accountId, () =>
            {
                int actualCaughtTotal = 0;
                var baseGeoArea = plusCode8.ToGeoArea().PadGeoArea(ConstantValues.resolutionCell8);
                var allCreatures = GenericData.GetAllDataInArea(baseGeoArea, "creature");

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
                        var creatureInstance = c.DataValue.FromJsonBytesTo<CreatureInstance>();
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

                if (actualCaughtTotal >= vortexMin) //don't save, this is mostly a waste of a token and probably an accidental button tap.
                {
                    GenericData.SetSecurePlayerDataJson(accountId, "account", account, password);
                    GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
                    GenericData.SetSecurePlayerDataJson(accountId, "recentlyCaught", recentlyCaught, password);
                }
            });

            return results;
        }
    }
}