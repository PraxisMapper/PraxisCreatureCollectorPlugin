using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PraxisCore;
using PraxisMapper.Classes;
using System.Text.Json;
using static PraxisCreatureCollectorPlugin.CommonHelpers;

namespace PraxisCreatureCollectorPlugin.Controllers {
    //Control mode is the territory-control PVP mode in TIBO.
    //You use each creature in one territory, providing it's full strength.

    public class ControlController : Controller {
        static List<int> zodiacEntries = new List<int>() { 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51 };  //TODO: make a dictionary of required and result creatures for this logic later.

        string accountId, password;
        public override void OnActionExecuting(ActionExecutingContext context) {
            base.OnActionExecuting(context);
            PraxisAuthentication.GetAuthInfo(Response, out accountId, out password);
        }

        [HttpPut]
        [Route("/[controller]/Claim/{plusCode}")]
        public bool ClaimPlace(string plusCode) {
            //THis is called when you send a creature into an empty area, or add to the stack of creatures claiming it.
            //NOTE: 4+ DB calls for this, can probably reduce that and boost perf later.
            //json data on creature in body.
            //Incoming data includes: team, owner account name, and creature combat stats.

            Response.Headers.Add("X-noPerfTrack", "Creature/Claim/" + plusCode + "/VARSREMOVED");
            if (!DataCheck.IsInBounds(plusCode))
                return false;

            bool results = true;
            SimpleLockable.PerformWithLock(accountId, () => {
                SimpleLockable.PerformWithLock(plusCode, () => {
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
                        //Special check: Doublebat transform around Toledo.
                        if (sentCreature.creatureId == 17 && (plusCode.StartsWith("86GQ") || plusCode.StartsWith("86HQ") || plusCode.StartsWith("86HR"))) //set to 59
                        {
                            sentCreature.creatureId = 59;
                            if (creatureData.ContainsKey(59)) {
                                var swapFrom = creatureData[17];
                                var swapTo = creatureData[59];
                                swapTo.totalCaught += swapFrom.totalCaught;
                                swapTo.currentAvailable += swapFrom.currentAvailable;
                                swapTo.currentAvailableCompete += swapFrom.currentAvailableCompete;
                                swapFrom.totalCaught = 0;
                                swapFrom.currentAvailable = 0;
                                swapFrom.currentAvailableCompete = 0;
                            }
                            else {
                                creatureData.Add(59, new PlayerCreatureInfo() { currentAvailable = sentCreatureData.currentAvailable, currentAvailableCompete = sentCreatureData.currentAvailableCompete, totalCaught = sentCreatureData.totalCaught, toNextLevel = sentCreatureData.toNextLevel });
                                var swapFrom = creatureData[17];
                                swapFrom.totalCaught = 0;
                                swapFrom.currentAvailable = 0;
                                swapFrom.currentAvailableCompete = 0;
                                sentCreatureData = creatureData[59];
                            }
                        }
                        //Special check: Triplebat transform around Toledo.
                        else if (sentCreature.creatureId == 18 && (plusCode.StartsWith("86GQ") || plusCode.StartsWith("86HQ") || plusCode.StartsWith("86HR"))) //set to 60
                        {
                            sentCreature.creatureId = 60;
                            if (creatureData.ContainsKey(60)) {
                                var swapFrom = creatureData[18];
                                var swapTo = creatureData[60];
                                swapTo.totalCaught += swapFrom.totalCaught;
                                swapTo.currentAvailable += swapFrom.currentAvailable;
                                swapTo.currentAvailableCompete += swapFrom.currentAvailableCompete;
                                swapFrom.totalCaught = 0;
                                swapFrom.currentAvailable = 0;
                                swapFrom.currentAvailableCompete = 0;
                            }
                            else {
                                creatureData.Add(60, new PlayerCreatureInfo() { currentAvailable = sentCreatureData.currentAvailable, currentAvailableCompete = sentCreatureData.currentAvailableCompete, totalCaught = sentCreatureData.totalCaught, toNextLevel = sentCreatureData.toNextLevel });
                                var swapFrom = creatureData[18];
                                swapFrom.totalCaught = 0;
                                swapFrom.currentAvailable = 0;
                                swapFrom.currentAvailableCompete = 0;
                                sentCreatureData = creatureData[60];
                            }
                        }

                        sentCreatureData.available = false;
                        sentCreatureData.assignedTo = plusCode;
                        GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);

                        //now recalculate total team strength to determine owner.
                        sentCreature.level = sentCreatureData.level;

                        //Special check for special things!
                        if (!SpecialCheck(placeCreatures, plusCode)) //if SpecialCheck is true, don't do the normal save, let it save instead.
                            SaveClaimForArea(plusCode, placeCreatures, sentCreature);
                    }
                    else
                        results = false;
                });
            });

            return results;
        }

        [HttpGet]
        [Route("/[controller]/Combat/{plusCode}")]
        public bool CombatArea(string plusCode) {
            if (!DataCheck.IsInBounds(plusCode))
                return false;

            bool results = false;
            SimpleLockable.PerformWithLock(plusCode, () => {
                var sentCreature = Request.ReadBody<ClaimData>();
                var placeCreatures = GetCreaturesInArea(plusCode);
                var place = AreaStyle.GetSinglePlaceFromArea(plusCode);

                if (CreaturesFight(sentCreature, placeCreatures[0], plusCode)) {
                    //Flip the whole pyramid.
                    FlipArea(placeCreatures, plusCode);
                    results = true;
                }
                else if (CreaturesFight(sentCreature, placeCreatures.Last(), plusCode)) {
                    //attack wins, remove hit creature from the pyramid and check for a collapse.
                    //pyramid collapses if removing this creature creates an empty row (so there are 1/3/6/10 creatures left after the attack succeeds (15 is the cap, will never be 15 after an attack.)
                    if (placeCreatures.Count == 3 || placeCreatures.Count == 6 || placeCreatures.Count == 10) {
                        FlipArea(placeCreatures, plusCode);
                        results = true;
                    }
                    else {
                        var c = placeCreatures.Last();
                        UpdateAccountPendingCommand(c.owner, "RETURN", c.creatureName);
                        RemoveCreatureFromControl(placeCreatures, c, place.PrivacyId);
                        GenericData.SetPlaceDataJson(place.PrivacyId, "creatures", placeCreatures);
                        results = true;
                    }
                }
                else {
                    //player lost, nothing happens.
                }
            });
            return results;
        }

        public void FlipArea(List<ClaimData> creatures, string plusCode) {
            foreach (var c in creatures) {
                UpdateAccountPendingCommand(c.owner, "RETURN", c.creatureName);
            }
            var db = new PraxisContext();
            var place = AreaStyle.GetSinglePlaceFromArea(plusCode);
            if (place != null) {
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
        public string GetPlaceInfo(string plusCode) {
            if (!DataCheck.IsInBounds(plusCode))
                return null;
            var place = AreaStyle.GetSinglePlaceFromArea(plusCode);
            if (place != null) {
                var placeName = TagParser.GetName(place);
                if (placeName == "")
                    placeName = place.StyleName;
                var placeOpenData = GenericData.GetAllDataInPlace(place.PrivacyId); //NOTE: this only loads insecure data.  I have at least 1 secure entry but this function may not read it.

                //teamOwner - easy look up, whichever team has the highest level creature.
                //creatures - JSON data on which things are in this place. Each creature needs at LEAST ID, Team, and Level sent to the client.
                //score - max score. calculate once, first time a place is tapped. Player should know this value. I don't want to recalc this if entries change

                var owner = placeOpenData.FirstOrDefault(p => p.DataKey == "teamOwner");
                var creatures = placeOpenData.FirstOrDefault(p => p.DataKey == "creatures"); //this might be secure data, since it might have account names.
                var score = placeOpenData.FirstOrDefault(p => p.DataKey == "score");
                long scoreVal = 0;
                if (score == null || score.DataKey == null) {
                    scoreVal = PraxisCore.GameTools.ScoreData.GetScoreForSinglePlace(place.ElementGeometry);
                    GenericData.SetPlaceData(place.PrivacyId, "score", scoreVal.ToString().ToByteArrayUTF8());
                }
                else
                    scoreVal = score.DataValue.ToUTF8String().ToInt();

                var a = new {
                    name = placeName,
                    placeId = place.PrivacyId,
                    teamOwner = (owner == null || owner.DataKey == null) ? 0 : owner.DataValue.ToUTF8String().ToInt(),
                    creatures = (creatures == null || creatures.DataKey == null) ? null : creatures.DataValue.FromJsonBytesTo<List<ClaimData>>(),
                    score = scoreVal
                };

                var jsonData = JsonSerializer.Serialize(a);
                return jsonData;
            }
            return "";
        }

        [HttpGet]
        [Route("/[controller]/Leaderboards")]
        public ControlLeaderboardResult ControlLeaderboards() {
            var results = new ControlLeaderboardResult();
            results.team1Score = GenericData.GetGlobalData("team1Score").ToUTF8String().ToLong(); //RED
            results.team2Score = GenericData.GetGlobalData("team2Score").ToUTF8String().ToLong(); //GREEN
            results.team3Score = GenericData.GetGlobalData("team3Score").ToUTF8String().ToLong(); //PURPLE
            results.team4Score = GenericData.GetGlobalData("team4Score").ToUTF8String().ToLong(); //GREY

            return results;
        }

        bool SpecialCheck(List<ClaimData> placeCreatures, string plusCode) //if true, skip normal save.
        {
            if (zodiacEntries.All(z => placeCreatures.Any(p => p.creatureId == z))) {
                foreach (var c in placeCreatures) {
                    UpdateAccountPendingCommand(c.owner, "GRANT", "52|10"); //ID 52, 10 frags
                }
                //Clear out this location.
                FlipArea(placeCreatures, plusCode);
                return true; //In case there's other special checks later that get added here.
            }
            return false;
        }
    }
}