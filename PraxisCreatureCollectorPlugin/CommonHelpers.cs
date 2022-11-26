using Google.OpenLocationCode;
using NetTopologySuite.Geometries;
using PraxisCore;
using PraxisCore.Support;
using System.Globalization;
using static PraxisCreatureCollectorPlugin.CreatureCollectorGlobals;

namespace PraxisCreatureCollectorPlugin
{
    //TODO: Split this into smaller helper classes. Probably per game mode.
    public static class CommonHelpers
    {
        //Each extra row in a pyramid adds 1 block, not 2x.
        static int[] SharesPerSpot = new int[] { 16, 8, 8, 4, 4, 4, 2, 2, 2, 2, 1, 1, 1, 1, 1 };

        //This is to lock whatever element is going to be changed by subsequent methods. Not specifically for player's update commands.
        public static SimpleLockable GetUpdateLock(string lockedKey)
        {
            //NOTE: replaced ReaderWriterLockSlim with this basic counting class, to avoid weird rare cases where a thread in the IIS thread pool can, rarely,
            //get the same RWLS and look like its acting recursively, and either throw an exception (bad, errors) or allow recusion to get the write lock (differently bad, not actually a lock.)
            updateLocks.TryAdd(lockedKey, new SimpleLockable());
            var entityLock = updateLocks[lockedKey];
            entityLock.counter++;
            return entityLock;
        }

        public static void DropUpdateLock(string lockId, SimpleLockable entityLock)
        {
            entityLock.counter--; //NOTE: this isn't mission-critical, its to keep the list of locks from growing infintely. Its OK if this isn't Interlocked or lives until the next call.
            if (entityLock.counter <= 0)
                updateLocks.TryRemove(lockId, out entityLock);
        }

        public static void UpdateAccountPendingCommand(string accountName, string verb, string target)
        {
            var playerLock = GetUpdateLock(accountName);
            lock (playerLock)
            {

                var commands = GenericData.GetSecurePlayerData<List<UpdateCommand>>(accountName, "Updates", internalPassword);
                if (commands == null)
                    commands = new List<UpdateCommand>();
                commands.Add(new UpdateCommand() { verb = verb, target = target });
                GenericData.SetSecurePlayerDataJson(accountName, "Updates", commands, internalPassword);
            }
            DropUpdateLock(accountName, playerLock);
        }

        public static void ProcessPendingCommand(string accountId, string password)
        {
            var data = GenericData.GetSecurePlayerData<List<UpdateCommand>>(accountId, "Updates", internalPassword);
            if (data == null)
                return;

            var account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
            var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
            var competeData = GenericData.GetSecurePlayerData<Dictionary<string, PlayerCompeteEntry>>(accountId, "competeInfo", password);
            var saveCreatureData = false;
            var saveCompeteData = false;
            foreach (UpdateCommand command in data)
            {
                switch (command.verb)
                {
                    case "RETURN": //A creature becomes available again in Control mode
                        var thisCreature = creatureData[command.target.ToLong()];
                        thisCreature.available = true;
                        thisCreature.assignedTo = "";
                        saveCreatureData = true;
                        break;
                    case "RETURNCOMPETE": //A creature was kicked out from a spot in Compete mode, we have some fragments available again.
                        var values = command.target.Split("|");
                        var thisCreature2 = creatureData[values[0].ToLong()];
                        thisCreature2.currentAvailableCompete += values[1].ToLong();
                        saveCreatureData = true;
                        competeData.Remove(values[2]);
                        saveCompeteData = true;
                        break;
                    case "GRADUATE": //Not sure why you'd get this while offline. This tells the client to show the graduation tutorial.
                        account.graduationEligible = true;
                        break;
                    case "ADMINDUMP": //This is for troubleshooting, and only happens when both an admin sets this request AND the player is logged in to process this function.
                        PraxisMapper.Classes.PraxisPerformanceTracker.LogInfoToPerfData("Account: " + accountId, account.ToJson());
                        PraxisMapper.Classes.PraxisPerformanceTracker.LogInfoToPerfData("Creatures: " + accountId, creatureData.ToJson());
                        break;
                    case "RESET":
                        //Put everything back, don't worry about removing stuff from the map. This is the 'errors made info go out of sync and stuff got lost' fix.
                        foreach (var c in creatureData)
                        {
                            c.Value.currentAvailable = c.Value.totalCaught;
                            c.Value.currentAvailableCompete = c.Value.totalCaught;
                            c.Value.available = true;
                        }
                        saveCreatureData = true;
                        break;
                    case "ADMINGRANT":
                        //In a | separated list of stuff to grant.
                        var pieces = command.target.Split('|');
                        switch (pieces[0])
                        {
                            case "catch":
                                //1 is creature id, 2 is amount to add.
                                int creatureId = int.Parse(pieces[1]);
                                int amount = int.Parse(pieces[2]);

                                if (creatureData[creatureId] == null)
                                {
                                    PlayerCreatureInfo pci = new PlayerCreatureInfo() { id = creatureId };
                                    pci.FastBoost(amount);

                                    creatureData.Add(creatureId, pci);
                                }
                                else
                                    creatureData[creatureId].FastBoost(amount);
                                saveCreatureData = true;
                                break;
                            case "coins":
                                account.currencies.baseCurrency += pieces[1].ToInt();
                                break;
                            case "proxytokens":
                                account.currencies.proxyPlayTokens += pieces[1].ToInt();
                                break;
                            case "swaptokens":
                                account.currencies.teamSwapTokens += pieces[1].ToInt();
                                break;
                                //TODO: be able to grant other things.
                        }
                        break;
                    case "DEVHELPER": //Throw a bunch of stuff onto an account for a developer to test with.
                        account.currencies.proxyPlayTokens += 20;
                        account.currencies.vortexTokens += 20;
                        foreach (var c in creatureList)
                        {
                            if (creatureData == null)
                                creatureData = new Dictionary<long, PlayerCreatureInfo>();
                            if (!creatureData.ContainsKey(c.id))
                            {
                                creatureData.Add(c.id, new PlayerCreatureInfo() { id = c.id, available = true, currentAvailable = 1, currentAvailableCompete = 1, totalCaught = 1, level = 1 });
                            }
                            creatureData[c.id].totalCaught += 50;
                            creatureData[c.id].currentAvailable += 50;
                            creatureData[c.id].currentAvailableCompete += 50;

                        }
                        saveCreatureData = true;
                        break;
                }
            }

            if (saveCreatureData)
                GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);

            if (saveCompeteData)
                GenericData.SetSecurePlayerDataJson(accountId, "competeInfo", competeData, password);

            GenericData.SetSecurePlayerDataJson(accountId, "account", account, password);
            ClearAccountPendingCommands(accountId);
        }

        public static bool ClearAccountPendingCommands(string accountId)
        {
            return GenericData.SetSecurePlayerData(accountId, "Updates", "", internalPassword);
        }

        public static List<ClaimData> GetCreaturesInArea(string plusCode)
        {
            //This is for Control PvP
            var place = AreaTypeInfo.GetSingleGameplayPlaceFromArea(plusCode);
            List<ClaimData> placeCreatures = GenericData.GetPlaceData<List<ClaimData>>(place.PrivacyId, "creatures");
            if (placeCreatures == null)
                placeCreatures = new List<ClaimData>();

            return placeCreatures;
        }

        public static void SaveClaimForArea(string plusCode, List<ClaimData> creatures, ClaimData added)
        {
            var place = AreaTypeInfo.GetSingleGameplayPlaceFromArea(plusCode);
            string teamOwnerId = "0";
            string currentTeamOwner = "0";
            currentTeamOwner = GenericData.GetPlaceData(place.PrivacyId, "teamOwner").ToUTF8String();

            AddCreatureFromControl(creatures, added, place.PrivacyId);
            creatures = creatures.OrderByDescending(c => c.level).ToList();
            if (creatures.Count() > 0)
            {
                teamOwnerId = creatures.FirstOrDefault().team.ToString();
            }

            var db = new PraxisContext();
            GenericData.SetPlaceData(place.PrivacyId, "teamOwner", teamOwnerId.ToByteArrayUTF8());
            GenericData.SetPlaceDataJson(place.PrivacyId, "creatures", creatures);
            db.ExpireMapTiles(place.PrivacyId, "TC");
            db.ExpireSlippyMapTiles(place.PrivacyId, "TC");
        }

        public static int[] CalculateScores(double totalPoints, List<ClaimData> creatures)
        {
            var shares = new int[5]; //4 teams, plus 0 to line things up nicer.
            for (int i = 0; i < creatures.Count; i++)
            {
                shares[creatures[i].team] += SharesPerSpot[i];
            }

            var pointsPerShare = shares.Sum() / shares.Count();
            var pointByTeam = shares.Select(s => s * pointsPerShare).ToArray();

            return pointByTeam;
        }

        public static void ApplyScoreChange(int[] oldScores, int[] newScores)
        {
            for (int i = 1; i < 5; i++) //no team 0
            {
                GenericData.IncrementGlobalData("team" + i + "Score", newScores[i] - oldScores[i]);
            }
        }

        public static void RemoveCreatureFromControl(List<ClaimData> creatures, ClaimData removed, Guid placeId)
        {
            //Take a selected creature out of the claim data list, adjust scores, save everything.
            var totalscore = GenericData.GetPlaceData(placeId, "score").ToUTF8String().ToDouble();
            var currentScores = CalculateScores(totalscore, creatures);

            creatures.Remove(removed);
            creatures = creatures.OrderByDescending(c => c.level).ToList();

            var newScores = CalculateScores(totalscore, creatures);
            ApplyScoreChange(currentScores, newScores);
        }

        public static void AddCreatureFromControl(List<ClaimData> creatures, ClaimData added, Guid placeId)
        {
            //Take a selected creature out of the claim data list, adjust scores, save everything.
            var totalscore = GenericData.GetPlaceData(placeId, "score").ToUTF8String().ToDouble();
            var currentScores = CalculateScores(totalscore, creatures);

            creatures.Add(added);
            creatures = creatures.OrderByDescending(c => c.level).ToList();

            var newScores = CalculateScores(totalscore, creatures);
            ApplyScoreChange(currentScores, newScores);
        }

        public static Dictionary<long, PlayerCreatureInfo> MakeStarterCreatureInfo()
        {
            var creatureInfo = new Dictionary<long, PlayerCreatureInfo>();
            PlayerCreatureInfo starter = new PlayerCreatureInfo();
            starter.id = 27; //gliterrati.
            starter.BoostCreature();
            creatureInfo.Add(starter.id, starter);
            return creatureInfo;
        }

        public static Dictionary<string, ImprovementTasks> CheckImprovementTasks(string accountId, string password)
        {
            var accountData = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
            bool saveAccount = false;

            var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
            bool saveCreatures = false;

            var taskData = GenericData.GetSecurePlayerData<Dictionary<string, ImprovementTasks>>(accountId, "taskInfo", password);
            if (taskData == null)
                taskData = ImprovementTasks.DefaultTasks.ToDictionary(k => k.id, v => v);

            foreach (var t in taskData)
            {
                if (t.Value.assigned > 0)
                {
                    t.Value.accrued += (long)(DateTime.UtcNow - t.Value.lastCheck).TotalSeconds;
                    long rewards = t.Value.accrued / t.Value.timePerResult;
                    t.Value.accrued -= (t.Value.timePerResult * rewards);
                    t.Value.lastCheck = DateTime.UtcNow;

                    if (rewards > 0)
                    {
                        //update this info on the core account.
                        switch (t.Value.id)
                        {
                            case "clone":
                                var creature = creatureData[t.Value.assigned];
                                for (int i = 0; i < rewards; i++)
                                    creature.BoostCreature();
                                saveCreatures = true;
                                break;
                            case "ppt":
                                accountData.currencies.proxyPlayTokens += rewards;
                                saveAccount = true;
                                break;
                            case "hint":
                                //find first creature we DONT have, and enable its hint.
                                var hintCreature = creatureList.FirstOrDefault(c => !c.isHidden && !creatureData.ContainsKey(c.id));
                                if (hintCreature != null)
                                    creatureData.Add(hintCreature.id, new PlayerCreatureInfo() { id = hintCreature.id, hintUnlocked = true });
                                break;
                            case "tst":
                                accountData.currencies.teamSwapTokens += rewards;
                                saveAccount = true;
                                break;
                            case "vortex":
                                accountData.currencies.vortexTokens += rewards;
                                saveAccount = true;
                                break;
                        }
                    }
                }
            }

            GenericData.SetSecurePlayerDataJson(accountId, "taskInfo", taskData, password);
            if (saveAccount)
            {
                GenericData.SetSecurePlayerDataJson(accountId, "account", accountData, password);
            }

            if (saveCreatures)
            {
                GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
            }
            return taskData;
        }

        public static void GetAuthInfo(HttpResponse response, out string account, out string password)
        {
            account = "";
            password = "";
            if (response.Headers.ContainsKey("X-account"))
                account = response.Headers["X-account"].ToString();
            if (response.Headers.ContainsKey("X-internalPwd"))
                password = response.Headers["X-internalPwd"].ToString();
        }

        public static string GetPasswordFromHeaders(HttpResponse response)
        {
            if (response.Headers.ContainsKey("X-account"))
                return response.Headers["X-internalPwd"].ToString();
            return "";
        }

        public static string GetAccountFromHeaders(HttpResponse response)
        {
            if (response.Headers.ContainsKey("X-internalPwd"))
                return response.Headers["X-account"].ToString();
            return "";
        }

        public static int DetermineCoinCost(Creature c)
        {
            //Cost expectations:
            //average coins per Cell10: 5.5
            //Spaces walked to buy common/cheap thing: 20 (110 total)
            //terrain data: increase value based on area/count of terrains?
            //limited to certain terrains? ?x cost, based on total area of terrains or number of terrains?
            //limited to certain named places? ?x cost, based on total count?
            //TODO: factor in graduation changes to terrain info. A creature should not get MORE expensive because someone added it to a terrain or an area.
            int cost = 110 + (int)c.id;  //base cost, average reward for walking across 1 Cell8 in either direction in a straight line.

            cost *= c.tierRating; //Tier 1 is weakest, each number is another step up in stat determination.

            if (!c.isWild) //elite? (not a wild spawn, also not a passport reward)
                cost *= 3;

            if (c.spawnTimes.Count > 0) //Locked to certain hours.
                cost *= 2;

            if (c.spawnDates.Count > 0) //locked to certain days of the year
                cost *= 10;

            if (c.placeSpawns.Count > 0) //Is attached to specific named places on the map, 2-5x multiplier, -1 for each separate place name to a minimum of 2.
                cost *= Math.Min(2, 6 - c.placeSpawns.Count);

            if (c.areaSpawns.Count > 0 && c.terrainSpawns.Count == 0 && c.areaSpawns.Keys.All(k => k.Length > 0)) //Area locked, but don't apply to things available globally. Ignore player additions from graduation.
                cost *= 3;

            if (c.specificSpawns.Count > 0) //Always spawns in specific Cell10(s).
                cost *= 6;

            if (c.wanderOdds > 0) //will already have a 3x multiplier applied from not being wild. This could take the Elite slot right now.
                cost *= Math.Min(3, 10 - (int)c.wanderSpawnEntries);

            return cost;
        }

        public static bool CreaturesFight(ClaimData attacker, ClaimData defender, string location = "")
        {
            PlayerCreatureInfo attack1 = new PlayerCreatureInfo();
            attack1.id = attacker.creatureId;
            attack1.SetToLevel(attacker.level);

            PlayerCreatureInfo defend1 = new PlayerCreatureInfo();
            defend1.id = defender.creatureId;
            defend1.SetToLevel(defender.level);

            return CreaturesFight(attack1, defend1, location);
        }

        public static bool CreaturesFight(PlayerCreatureInfo attacker, PlayerCreatureInfo defender, string location = "")
        {
            if (attacker.strength > defender.defense)
                return true;

            return false;
        }

        public static List<Creature> GenerateSpawnTable(string plusCode, out List<FindPlaceResult> terrainInfo)
        {
            GeoArea box = OpenLocationCode.DecodeValid(plusCode);
            List<Creature> localSpawnTable = new List<Creature>(400);

            var timeShift = OpenLocationCode.CodeAlphabet.IndexOf(plusCode[1]) - 9; //OpenLocationCode.CodeAlphabet.IndexOf('F') == 9
            var shiftedTime = DateTime.UtcNow.AddHours(timeShift).AddMinutes(timeShift * 24); //Adding 24 minutes per shift helps make up for the difference between time zones and PlusCodes.

            var places = PraxisCore.Place.GetPlaces(box);
            places = places.Where(p => p.GameElementName != TagParser.defaultStyle.Name).ToList();
            terrainInfo = AreaTypeInfo.SearchArea(ref box, ref places);
            var terrainCounts = terrainInfo.GroupBy(t => t.data.areaType).ToDictionary(k => k.First().data.areaType, v => v.Count());

            //Check terrains, but call CanSpawnNow once per type, not once per cell.
            var tempTerrainDict = new Dictionary<string, List<Creature>>();
            foreach (var t in terrainSpawnTables)
                if (terrainCounts.ContainsKey(t.Key))
                    tempTerrainDict.Add(t.Key, t.Value.Where(tt => tt.CanSpawnNow(shiftedTime)).ToList());

            foreach (var t in terrainCounts)
                if (tempTerrainDict.ContainsKey(t.Key))
                    localSpawnTable.AddRange(tempTerrainDict[t.Key]); //Each cell10 adds its type's list to the spawn pool

            //check areas.
            foreach (var a in areaSpawnTables.Keys)
            {
                if (plusCode.StartsWith(a))
                {
                    localSpawnTable.AddRange(areaSpawnTables[a].Where(t => t.CanSpawnNow(shiftedTime)));
                }
            }

            //check places.
            foreach (var p in places)
            {
                if (placeSpawnTables.ContainsKey(p.GameElementName))
                    localSpawnTable.AddRange(placeSpawnTables[p.GameElementName].Where(t => t.CanSpawnNow(shiftedTime)));
            }

            //nests: Run a check for this area. If it's got an active nest, add the nest's terrain type to the spawn table. Nest activate about twice a year.
            //All of these random numbers need to be pulled every time in the same order so that they stay consistent.
            var seededRng = plusCode.GetSeededRandom();
            var weekOfYear = ISOWeek.GetWeekOfYear(DateTime.UtcNow); //results range from 1-53
            var nestIsActive = (seededRng.Next(26) + 1) == weekOfYear % 26; //26 means twice a year any given space has a nest active, on average.
            var nestType = seededRng.Next(gameplayAreas.Count);
            var nestSize = seededRng.Next(25, 225); //nest takes up between 25 and 225 Cell10s. Since we aren't drawing them, shape and location are irrelevant, so we pick an arbitrary size.

            if (nestIsActive && CreatureCollectorGlobals.config.nestsEnabled)
            {
                for (int i = 0; i < nestSize; i++)
                    if (tempTerrainDict.ContainsKey(gameplayAreas[nestType]))
                        localSpawnTable.AddRange(tempTerrainDict[gameplayAreas[nestType]]);
            }

            //Wandering Creatures: Some creatures may only spawn in random locations. This needs checked at this time as well.
            foreach (var w in wanderingCreatures)
            {
                var allowThisWeek = seededRng.Next(w.wanderOdds * weekOfYear) <= weekOfYear - 1;  //-1 because 0 is a valid result
                //This changes the results weekly, without changing the actual odds.
                //week 1, odds 30: 1 in 30 chance 3.3%
                //week 30, odds 30: 30 in 900 chance. 3.3%

                if (allowThisWeek)
                    for (int i = 0; i < w.wanderSpawnEntries; i++)
                        localSpawnTable.Add(w);
            }

            return localSpawnTable;
        }

        public static Geometry GetGeometryFromPlacedEntry(CompeteModeEntry data)
        {
            return data.locationCell8.ToGeoArea().ToPoint().Buffer(data.scouting * ConstantValues.resolutionCell10);
        }

        public static void UpdateGeometryEntries(int teamId, Geometry newGeo, Geometry oldGeo)
        {
            //This ONLY updates the geometry data for drawing maps. All the other stuff is handled in whatever called this.
            //Remember, bigger area is used to expire map tiles. 
            var db = new PraxisContext();
            var teamGeo = teamItems[teamId];
            if (newGeo.Area < oldGeo.Area)
            {
                //This requires recomputing the whole teamGeo if any entry overlapped this one. The entry was updated already in whatever called this.
                //teamGeo = teamGeo.Difference(oldGeo).Union(newGeo); //works, but only if oldGeo did not overlap anything.
                teamGeo = GetTeamGeometry(teamId);
                teamItems[teamId] = teamGeo;
                db.ExpireSlippyMapTiles(oldGeo, "Compete");
                db.ExpireMapTiles(oldGeo, "Compete");
            }
            else
            {
                //Shortcut - can just union the new area overtop the old.
                teamGeo = newGeo.Union(teamGeo);
                teamItems[teamId] = teamGeo;
                db.ExpireSlippyMapTiles(teamGeo, "Compete");
                db.ExpireMapTiles(teamGeo, "Compete");
            }
            var teamScore = (long)(teamItems[teamId].Area / (ConstantValues.resolutionCell10 * ConstantValues.resolutionCell10));
            GenericData.SetGlobalData("competeTeamScore" + teamId, teamScore.ToString().ToByteArrayUTF8());
        }

        public static Geometry GetTeamGeometry(int teamId)
        {
            var teamItems = allEntries.Values.Where(v => v != null && v.teamId == teamId).Select(v => GetGeometryFromPlacedEntry(v)).ToList();
            var teamGeo = NetTopologySuite.Operation.Union.CascadedPolygonUnion.Union(teamItems); //This should be the optimal call for this logic.

            if (teamGeo == null)
                teamGeo = new Polygon(new LinearRing(new Coordinate[] { new Coordinate(), new Coordinate(0.000000001, 0.000000001), new Coordinate() }));
            return teamGeo;
        }

        public static void DeleteAllPlayerCompeteEntries(string accountName, string password)
        {
            var competeData = GenericData.GetSecurePlayerData<Dictionary<string, PlayerCompeteEntry>>(accountName, "competeInfo", password);
            foreach (var c in competeData)
            {
                var thisLock = GetUpdateLock(c.Key);
                lock (thisLock)
                {
                    var areaCompeteData = GenericData.GetSecureAreaData<CompeteModeEntry>(c.Key, "competeEntry", internalPassword);
                    var oldArea = GetGeometryFromPlacedEntry(areaCompeteData);
                    areaCompeteData.creatureFragmentCounts.Remove(accountName, out var frags);
                    areaCompeteData.totalFragments = areaCompeteData.creatureFragmentCounts.Sum(c => c.Value);
                    if (areaCompeteData.totalFragments > 0)
                    {
                        PlayerCreatureInfo ci = new PlayerCreatureInfo() { id = areaCompeteData.creatureId };
                        ci.FastBoost(areaCompeteData.totalFragments);
                        areaCompeteData.scouting = (int)ci.scouting;
                    }
                    else
                    {
                        GenericData.SetSecureAreaData(c.Key, "competeEntry", "", internalPassword, -1); //expire this data immediately.
                    }

                    var newArea = GetGeometryFromPlacedEntry(areaCompeteData);
                    UpdateGeometryEntries(areaCompeteData.teamId, newArea, oldArea);
                    GenericData.SetSecureAreaDataJson(c.Key, "competeEntry", areaCompeteData, internalPassword);
                    allEntries[c.Key] = areaCompeteData;
                }
                DropUpdateLock(c.Key, thisLock);
            }
            GenericData.SetSecurePlayerDataJson(accountName, "competeInfo", competeData, password, -1);
        }

        public static void DeleteAllPlayerControlEntries(string accountName, string password)
        {
            var creatures = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountName, "creatureInfo", password);
            foreach (var c in creatures)
            {
                if (OpenLocationCode.IsValid(c.Value.assignedTo))
                {
                    var thisLock = GetUpdateLock(c.Value.assignedTo);
                    lock (thisLock)
                    {
                        var placeCreatures = GetCreaturesInArea(c.Value.assignedTo);
                        placeCreatures.Remove(placeCreatures.First(p => p.owner == accountName));
                        SaveClaimForArea(c.Value.assignedTo, placeCreatures, null);
                    }
                    DropUpdateLock(c.Value.assignedTo, thisLock);
                }
            }
        }

        public static void ResetCreatureData(string accountName, string password)
        {
            var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountName, "creatureInfo", password);
            foreach (var c in creatureData)
            {
                c.Value.assignedTo = "";
                c.Value.available = true;
                //c.Value.currentAvailable = c.Value.totalCaught; //This is for Cover mode, don't erase this on team swap.
                c.Value.currentAvailableCompete = c.Value.totalCaught;
            }
            GenericData.SetSecurePlayerDataJson(accountName, "creatureInfo", creatureData, password);
        }
    }
}
