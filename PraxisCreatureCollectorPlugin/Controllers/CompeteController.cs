using Google.OpenLocationCode;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Geometries;
using PraxisCore;
using PraxisCore.Support;
using System.Collections.Concurrent;
using System.Text.Json;
using static PraxisCreatureCollectorPlugin.CommonHelpers;
using static PraxisCreatureCollectorPlugin.CreatureCollectorGlobals;
using static PraxisCore.DbTables;

namespace PraxisCreatureCollectorPlugin.Controllers
{
    public class CompeteController : Controller
    {
        //This is the PVP version of Cover mode
        //* Main difference: This runs on Teams, and is shared info, so accommodations are made for privacy.
        //* You work on Cell8s in this mode, rather than the smallest circle you tapped, so that points aren't an immediate givewaway on where you live if you put one at your house.
        //* You can attack points owned by a different team, and contribute fragments from your creatures to a shared point.

        //Scores are covered by the total area for each team. Overlapping points for the same team do not add to score. Overlapping points from different teams do not interfere.
        //Maptiles will be persisted, and expired when points change.

        //Tags and magic strings:
        //competeEntry - on a Cell8 Area, handles data, team is contained within, multiple teams can't claim same Cell8
        //TC - the StyleSet for drawing the map tiles, same as Control mode.
        //competeTeamScoreX - X is 1-4, for the team in question. 0 is intentionally empty. Saved to the DB to make leaderboards faster.

        static Polygon emptyPoly = new Polygon(new LinearRing(new Coordinate[] { new Coordinate(), new Coordinate(0.000000001, 0.000000001), new Coordinate() }));

        private readonly IConfiguration Configuration;
        private static IMemoryCache cache;

        public CompeteController(IConfiguration configuration, IMemoryCache memoryCacheSingleton)
        {
            Configuration = configuration;
            cache = memoryCacheSingleton;
        }

        public static void InitializeCompeteMode()
        {
            GetAllEntriesFromDb();

            for(int i = 1; i < 5; i++)
            {
                var tg = GetTeamGeometry(i);
                if (tg == null)
                    tg = new Polygon(new LinearRing(new Coordinate[] { new Coordinate(), new Coordinate(0.000000001, 0.000000001), new Coordinate() }));
                teamItems[i] = tg;
            }
        }

        [HttpGet]
        [Route("/[controller]/Leaderboards")]
        public string CompeteLeaderboards()
        {
            //Team's score for this mode (placing creatures to make circles PVP) is not saved as secure, since it doesn't tell the owner anything about location.
            //Each value is updated when a change is made.
            var scoreData = new
            {
                team1Score = GenericData.GetGlobalData("competeTeamScore1").ToUTF8String(),
                team2Score = GenericData.GetGlobalData("competeTeamScore2").ToUTF8String(),
                team3Score = GenericData.GetGlobalData("competeTeamScore3").ToUTF8String(),
                team4Score = GenericData.GetGlobalData("competeTeamScore4").ToUTF8String(),
            };
            return JsonSerializer.Serialize(scoreData);
        }

        [HttpGet]
        [Route("/[controller]/Placed/{pluscode}")]
        public CompeteModeEntry GetPlacedCreature(string pluscode)
        {
            var response = GenericData.GetSecureAreaData<CompeteModeEntry>(pluscode, "competeEntry", internalPassword);
            if (response == null)
                return new CompeteModeEntry();

            return response;
        }

        [HttpPut]
        [Route("/[controller]/Placed/{plusCode8}/{creatureId}/{fragmentsUsed}")]
        public long UpdatePlacedCreature(string plusCode8, int creatureId, int fragmentsUsed) //FragmentsUsed is new total from player, not change.
        {
            long returnValue = 0; //Returns how many fragments were placed. May be negative if new fragmentsUsed value is lower than current value.
            Response.Headers.Add("X-noPerfTrack", "Compete/Placed/VARSREMOVED");
            if (!DataCheck.IsInBounds(plusCode8))
                return returnValue;

            GetAuthInfo(Response, out var accountId, out var password);
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                var areaLock = GetUpdateLock(plusCode8);
                lock (areaLock)
                {
                    var account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
                    allEntries.TryGetValue(plusCode8, out CompeteModeEntry placedCreature);
                    if (placedCreature == null)
                    {
                        placedCreature = new CompeteModeEntry() { creatureId = creatureId, locationCell8 = plusCode8, teamId = (int)account.team };
                        allEntries.TryAdd(plusCode8, placedCreature);
                    }
                    
                    if (account.team == placedCreature.teamId) //Ensure that this player is allowed to be here. Don't trust the client alone.
                    {
                        var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                        var playersCreature = creatureData[creatureId];

                        //Cheating checks. Player can't send up more fragments than they have, and can't send more than they have available PLUS the ones already sent to this point.
                        if (fragmentsUsed > playersCreature.totalCaught)
                            fragmentsUsed = (int)playersCreature.totalCaught;
                       

                        var oldArea = GetGeometryFromPlacedEntry(placedCreature);
                        int availableChange = 0;
                        if (!placedCreature.creatureFragmentCounts.ContainsKey(accountId))
                        {
                            availableChange = fragmentsUsed;
                            placedCreature.creatureFragmentCounts.TryAdd(accountId, fragmentsUsed);
                            placedCreature.totalFragments += fragmentsUsed;
                        }
                        else
                        {
                            availableChange = fragmentsUsed - placedCreature.creatureFragmentCounts[accountId];
                            placedCreature.creatureFragmentCounts[accountId] = fragmentsUsed;
                            placedCreature.totalFragments += availableChange;
                        }
                        returnValue = availableChange;

                        playersCreature.currentAvailableCompete -= availableChange;

                        var competeData = GenericData.GetSecurePlayerData<Dictionary<string, PlayerCompeteEntry>>(accountId, "competeInfo", password);
                        if (competeData == null)
                            competeData = new Dictionary<string, PlayerCompeteEntry>();

                        PlayerCompeteEntry thisEntry;
                        if (competeData.ContainsKey(plusCode8))
                        {
                            thisEntry = competeData[plusCode8];
                            thisEntry.fragmentCount += availableChange;
                        }
                        else
                        {
                            thisEntry = new PlayerCompeteEntry();
                            competeData.Add(plusCode8, thisEntry);
                            thisEntry.creatureId = creatureId;
                            thisEntry.fragmentCount = availableChange;
                        }

                        //update player creature info with the fragments they've contributed.
                        PlayerCreatureInfo ci = new PlayerCreatureInfo() { id = creatureId };
                        ci.FastBoost(placedCreature.totalFragments); //get new level
                        placedCreature.scouting = (int)ci.scouting;
                        var newArea = GetGeometryFromPlacedEntry(placedCreature);

                        //Now update the stuff we use for map tiles.
                        if (placedCreature.totalFragments == 0)
                        {
                            allEntries.TryRemove(plusCode8, out var ignore);
                            GenericData.SetSecureAreaData(plusCode8, "competeEntry", "", internalPassword, -1); //expire this data immediately.
                        }
                        else
                        {
                            GenericData.SetSecureAreaDataJson(plusCode8, "competeEntry", placedCreature, internalPassword);
                        }
                        GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
                        GenericData.SetSecurePlayerDataJson(accountId, "competeInfo", competeData, password);
                        UpdateGeometryEntries(placedCreature.teamId, newArea, oldArea);

                        //Update scores and save everything
                        var teamScore = (long)(teamItems[placedCreature.teamId].Area / (ConstantValues.resolutionCell10 * ConstantValues.resolutionCell10));
                        GenericData.SetGlobalData("competeTeamScore" + placedCreature.teamId, teamScore.ToString().ToByteArrayUTF8());
                    }
                }
                DropUpdateLock(plusCode8, areaLock);
            }
            DropUpdateLock(accountId, playerLock);
            
            return returnValue;
        }

        public static void RecalcTeamScores()
        {
            var divisor = (ConstantValues.resolutionCell10 * ConstantValues.resolutionCell10);
            for (int i =1; i <5; i++)
            {
                var teamScore = (long)(teamItems[i].Area / divisor);
                GenericData.SetGlobalData("competeTeamScore" + i, teamScore.ToString().ToByteArrayUTF8());
            }
        }

        [HttpGet]
        [Route("/MapTile/CompeteOverlay/{plusCode}")]
        [Route("/[controller]/PlacedOverlay/{plusCode}")]
        public ActionResult DrawPlacedCreatureMapOverlayTile(string plusCode)
        {
            //NOTE: this is extremely slow on current server for some reason. These take 6 seconds to draw on prod server.
            Response.Headers.Add("X-noPerfTrack", "Compete/PlacedOverlay/VARSREMOVED");
            if (!DataCheck.IsInBounds(plusCode))
            {
                Response.Headers.Add("X-notes", "OOB");
                return StatusCode(500);
            }

            //check for existing tile!
            var db = new PraxisContext();
            var existingResults = db.MapTiles.FirstOrDefault(mt => mt.PlusCode == plusCode && mt.StyleSet == "Compete");
            if (existingResults != null && existingResults.ExpireOn > DateTime.UtcNow)
            {
                return File(existingResults.TileData, "image/png");
            }
            cache.Remove("gen" + plusCode + "Compete");

            //this one is for gameplay tiles, to see where your existing creatures sit.
            var geoArea = plusCode.ToPolygon();

            List<DbTables.Place> mapItems = new List<DbTables.Place>();
            for (int i = 1; i < 5; i++)
                mapItems.Add(new DbTables.Place() { ElementGeometry = teamItems[i], Tags = new List<PlaceTags>(){ new PlaceTags() { Key = "teamOwner", Value = i.ToString() } } });

            //Draw map tile, save it to the server since this is multiplayer. Control can get away with piggybacking off the built-in logic since it uses DB geometry.
            ImageStats stats = new ImageStats(plusCode);
            var paintOps = MapTileSupport.GetPaintOpsForPlacesParseTags(mapItems, "Compete", stats); 
            var mapTile = MapTileSupport.MapTiles.DrawAreaAtSize(stats, paintOps);

            var gen = MapTileSupport.SaveMapTile(plusCode, "Compete", mapTile);
            cache.Set("gen" + plusCode + "Compete", gen);

            return File(mapTile, "image/png");
        }

        [HttpGet]
        [Route("/MapTile/CompeteFull")]
        [Route("/[controller]/MapFull")]
        public ActionResult DrawPlacedCreatureFullMap()
        {
            //cache this image for 15 minutes or so, since it's pretty heavy duty to draw this.
            if (cache.TryGetValue("CompeteFullMap", out byte[] mapImage))
                return File(mapImage, "image/png");

            List<DbTables.Place> mapItems = new List<DbTables.Place>();
            for (int i = 1; i < 5; i++)
            { 
                if (teamItems[i] != null)
                    mapItems.Add(new DbTables.Place() { ElementGeometry = teamItems[i], Tags = new List<PlaceTags>() { new PlaceTags() { Key = "teamOwner", Value = i.ToString() } } });
            }
            mapItems.Add(CreatureCollectorGlobals.playBoundary);

            var env = playBoundary.ElementGeometry.EnvelopeInternal;
            //Note: Could call PadGeoArea here, but this is slightly faster since there's no existing GeoArea.
            GeoArea state = new GeoArea(env.MinY - ConstantValues.resolutionCell6, env.MinX - ConstantValues.resolutionCell6, env.MaxY + ConstantValues.resolutionCell6, env.MaxX + ConstantValues.resolutionCell6);

            //Draw map tile, save it to the server since this is multiplayer. Control can get away with piggybacking off the built-in logic since it uses DB geometry.
            ImageStats stats = new ImageStats(state, 1024, 1024);
            var paintOps = MapTileSupport.GetPaintOpsForPlacesParseTags(mapItems, "Compete", stats);
            var mapTile = MapTileSupport.MapTiles.DrawAreaAtSize(stats, paintOps);

            cache.Set("CompeteFullMap", mapTile, AbsoluteExpiration15Min);
            return File(mapTile, "image/png");
        }

        public static void GetAllEntriesFromDb()
        {
            //Load up all saved entries from the DB.
            //NOTE: GetAllDataInArea breaks with a non-emptystring key, so I have to pull this data directly here.
            var db = new PraxisContext();
            //HOWEVER, since it's by Cell8 and by team, I ONLY need to save/load the radius of the geometry for each of these points.
            //which is good, because there are up to 64 million Cell8s in Ohio. (20 Cell4 = 8,000 Cell6 = 64M Cell8) * 4 teams = 256M * 4 bytes = 1 GB of RAM max.
            //I don't have a GenericData call to do this for Places or Areas.
            var dbAllEntries = db.AreaGameData.Where(p => p.DataKey == "competeEntry").Select(d => new {d.PlusCode, data = GenericData.DecryptValue(d.IvData, d.DataValue, internalPassword)}).ToList();
            allEntries = new ConcurrentDictionary<string, CompeteModeEntry>(dbAllEntries.ToDictionary(k => k.PlusCode, v => v.data.FromJsonBytesTo<CompeteModeEntry>()));
        }

        [HttpGet]
        [Route("/[controller]/Attack/{plusCode8}/{creatureId}")]
        public bool Attack(string plusCode8, long creatureId)
        {
            //1 player sends up all their available fragments of a creature. If that has more Offense than the placed creature has Defense, it gets defeated.
            //send all fragments back to the players that contributed, remove the geometry attached, redraw all covered map tiles.
            GetAuthInfo(Response, out var accountId, out var password);

            //get player's creatures, use all available fragments for creature.
            var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
            var thisCreature = creatureData[creatureId];

            var placedEntry = GetPlacedCreature(plusCode8);
            
            var attackerCi = new PlayerCreatureInfo() { id = thisCreature.id };
            attackerCi.FastBoost(thisCreature.currentAvailableCompete);

            var defenderCi = new PlayerCreatureInfo() { id = thisCreature.id };
            defenderCi.FastBoost(placedEntry.totalFragments);

            if (CreaturesFight(attackerCi, defenderCi, plusCode8))
            {
                //Expire tiles on the geometry before redrawing it.
                allEntries.TryRemove(plusCode8, out var ignore);
                GenericData.SetSecureAreaData(plusCode8, "competeEntry", "", internalPassword, -1);
                UpdateGeometryEntries(placedEntry.teamId, new Polygon(new LinearRing(new Coordinate[] { new Coordinate(), new Coordinate(0.000000001, 0.000000001), new Coordinate() })), GetGeometryFromPlacedEntry(placedEntry));

                foreach (var player in placedEntry.creatureFragmentCounts)
                {
                    UpdateAccountPendingCommand(player.Key, "RETURNCOMPETE", placedEntry.creatureId + "|" + player.Value + "|" + plusCode8);
                }

                return true;
            }
            return false;
        }
    }
}