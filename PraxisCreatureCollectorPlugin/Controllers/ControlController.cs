using Microsoft.AspNetCore.Mvc;
using PraxisCore;
using System.Text.Json;
using static PraxisCreatureCollectorPlugin.CommonHelpers;

namespace PraxisCreatureCollectorPlugin.Controllers
{
    //Control mode is the territory-control PVP mode in TIBO.
    public class ControlController : Controller
    {
        [HttpPut]
        [Route("/[controller]/Claim/{plusCode}")]
        public bool ClaimPlace(string plusCode)
        {
            //THis is called when you send a creature into an empty area, or add to the stack of creatures claiming it.
            //NOTE: 4+ DB calls for this, can probably reduce that and boost perf later.
            //json data on creature in body.
            //Incoming data includes: team, owner account name, and creature combat stats.

            Response.Headers.Add("X-noPerfTrack", "Creature/Claim/" + plusCode + "/VARSREMOVED");
            if (!DataCheck.IsInBounds(plusCode))
                return false;
            GetAuthInfo(Response, out var accountId, out var password);

            bool results = true;
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                var areaLock = GetUpdateLock(plusCode);
                lock (areaLock)
                {
                    var sentCreature = GenericData.ReadBody(Request.BodyReader, (int)Request.ContentLength).FromJsonBytesTo<ClaimData>();

                    //lock the player's creature out server-side.
                    var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                    var sentCreatureData = creatureData[sentCreature.creatureId];

                    //Assign the creature to the place selected.
                    List<ClaimData> placeCreatures = GetCreaturesInArea(plusCode);
                    if (placeCreatures == null)
                        placeCreatures = new List<ClaimData>();
                    if (placeCreatures.Count < 15) //Sanity check for open slots, since someone else may have sent a creature before server got a clients message.
                    {
                        sentCreatureData.available = false;
                        sentCreatureData.assignedTo = plusCode;
                        GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);

                        //now recalculate total team strength to determine owner.
                        sentCreature.level = sentCreatureData.level;
                        SaveClaimForArea(plusCode, placeCreatures, sentCreature);
                    }
                    else
                        results = false;
                }
                DropUpdateLock(accountId, areaLock);
            }
            DropUpdateLock(plusCode, playerLock);
            return results;
        }

        [HttpGet]
        [Route("/[controller]/Combat/{plusCode}")]
        public bool CombatArea(string plusCode)
        {
            if (!DataCheck.IsInBounds(plusCode))
                return false;

            bool results = false;
            var areaLock = GetUpdateLock(plusCode);
            lock (areaLock)
            {

                var sentCreature = GenericData.ReadBody(Request.BodyReader, (int)Request.ContentLength).FromJsonBytesTo<ClaimData>();
                var placeCreatures = GetCreaturesInArea(plusCode);
                var place = AreaTypeInfo.GetSingleGameplayPlaceFromArea(plusCode);

                if(CreaturesFight(sentCreature, placeCreatures[0], plusCode))
                {
                    //Flip the whole pyramid.
                    FlipArea(placeCreatures, plusCode);
                    results = true;
                }
                else if (CreaturesFight(sentCreature, placeCreatures.Last(), plusCode))
                {
                    //attack wins, remove hit creature from the pyramid and check for a collapse.
                    //pyramid collapses if removing this creature creates an empty row (so there are 1/3/6/10 creatures left after the attack succeeds (15 is the cap, will never be 15 after an attack.)
                    if (placeCreatures.Count == 1 || placeCreatures.Count == 3 || placeCreatures.Count == 6 || placeCreatures.Count == 10)
                    {
                        FlipArea(placeCreatures, plusCode);
                        results = true;
                    }
                    else
                    {
                        var c = placeCreatures.Last();
                        UpdateAccountPendingCommand(c.owner, "RETURN", c.creatureName);
                        RemoveCreatureFromControl(placeCreatures, c, place.PrivacyId);
                        GenericData.SetPlaceDataJson(place.PrivacyId, "creatures", placeCreatures);
                        results = true;
                    }
                }
                else
                {
                    //player lost, do.... something.
                }
            }
            DropUpdateLock(plusCode, areaLock);
            return results;
        }

        public void FlipArea(List<ClaimData> creatures, string plusCode)
        {
            foreach (var c in creatures)
            {
                UpdateAccountPendingCommand(c.owner, "RETURN", c.creatureName);
            }
            var db = new PraxisContext();
            var place = AreaTypeInfo.GetSingleGameplayPlaceFromArea(plusCode);
            if (place != null)
            {
                var oldScores = CalculateScores(1, creatures);
                ApplyScoreChange(oldScores, new int[] { 0, 0, 0, 0, 0 });

                GenericData.SetPlaceData(place.PrivacyId, "teamOwner", "0".ToByteArrayUTF8());
                GenericData.SetPlaceDataJson(place.PrivacyId, "creatures", new List<ClaimData>());
                db.ExpireMapTiles(place.PrivacyId, "TC");
                db.ExpireSlippyMapTiles(place.PrivacyId, "TC");
            }
        }

        [HttpGet]
        [Route("/[controller]/Info/{plusCode}")]
        public string GetPlaceInfo(string plusCode)
        {
            if (!DataCheck.IsInBounds(plusCode))
                return null;
            var place = AreaTypeInfo.GetSingleGameplayPlaceFromArea(plusCode);
            if (place != null)
            {
                var placeName = TagParser.GetPlaceName(place.Tags);
                if (placeName == "")
                    placeName = place.GameElementName;
                var placeOpenData = GenericData.GetAllDataInPlace(place.PrivacyId); //NOTE: this only loads insecure data.  I have at least 1 secure entry but this function may not read it.

                //teamOwner - easy look up, whichever team has the highest level creature.
                //creatures - JSON data on which things are in this place. Each creature needs at LEAST ID, Team, and Level sent to the client.
                //score - max score. calculate once, first time a place is tapped. Player should know this value. I don't want to recalc this if entries change

                var owner = placeOpenData.FirstOrDefault(p => p.key == "teamOwner");
                var creatures = placeOpenData.FirstOrDefault(p => p.key == "creatures"); //this might be secure data, since it might have account names.
                var score = placeOpenData.FirstOrDefault(p => p.key == "score");
                long scoreVal = 0;
                if (score.key == null)
                {
                    scoreVal = ScoreData.GetScoreForSinglePlace(place.ElementGeometry);
                    GenericData.SetPlaceData(place.PrivacyId, "score", scoreVal.ToString().ToByteArrayUTF8());
                }
                else
                    scoreVal = score.value.ToInt();

                var a = new
                {
                    name = placeName,
                    placeId = place.PrivacyId,
                    teamOwner = owner.key == null ? 0 : owner.value.ToInt(),
                    creatures = creatures.key == null ? null : creatures.value.FromJsonTo<List<ClaimData>>(),
                    score = scoreVal
                };

                var jsonData = JsonSerializer.Serialize(a);
                return jsonData;
            }
            return "";
        }

        [HttpGet]
        [Route("/[controller]/Leaderboards")]
        public ControlLeaderboardResult ControlLeaderboards()
        {
            var results = new ControlLeaderboardResult();
            results.team1Score = GenericData.GetGlobalData("team1Score").ToUTF8String().ToLong(); //RED
            results.team2Score = GenericData.GetGlobalData("team2Score").ToUTF8String().ToLong(); //GREEN
            results.team3Score = GenericData.GetGlobalData("team3Score").ToUTF8String().ToLong(); //PURPLE
            results.team4Score = GenericData.GetGlobalData("team4Score").ToUTF8String().ToLong(); //GREY

            return results;
        }
    }
}