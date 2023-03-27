using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PraxisCore;
using PraxisMapper.Classes;
using static PraxisCreatureCollectorPlugin.CreatureCollectorGlobals;

namespace PraxisCreatureCollectorPlugin.Controllers {
    public class PassportController : Controller {
        static List<string> validTerrains = new List<string>() { "university", "retail", "tourism", "historical", "artsCulture", "water", "park", "natureReserve", "cemetery", "trail" };
        static int entryCount = 3;

        string accountId, password;
        public override void OnActionExecuting(ActionExecutingContext context) {
            base.OnActionExecuting(context);
            PraxisAuthentication.GetAuthInfo(Response, out accountId, out password);
        }

        [HttpGet]
        [Route("/[controller]/")]
        public Dictionary<string, PassportEntry> GetPassportData() {
            Response.Headers.Add("X-noPerfTrack", "Creature/Passport/VARSREMOVED-GET");
            var entries = GenericData.GetSecurePlayerData<Dictionary<string, PassportEntry>>(accountId, "passport", password);
            if (entries == null) {
                entries = new Dictionary<string, PassportEntry>();
                foreach (var t in validTerrains)
                    entries.Add(t, new PassportEntry());

                GenericData.SetSecurePlayerDataJson(accountId, "passport", entries, password);
            }
            return entries;
        }

        [HttpPut]
        [Route("/[controller]/Stamp/{plusCode}")]
        public string StampPlace(string plusCode) {
            Response.Headers.Add("X-noPerfTrack", "Passport/Stamp/VARSREMOVED");
            if (!DataCheck.IsInBounds(plusCode))
                return "";
            string response = "";

            SimpleLockable.PerformWithLock(accountId, () => {
                //get passport data
                Dictionary<string, PassportEntry>? passportEntries = GenericData.GetSecurePlayerData<Dictionary<string, PassportEntry>>(accountId, "passport", password);
                var checkArea = plusCode.ToGeoArea().PadGeoArea(ConstantValues.resolutionCell10);

                //See what overlaps in this spot that's not already on our lists.
                var thesePlaces = PraxisCore.Place.GetPlaces(checkArea);
                foreach (var p in thesePlaces) {
                    string terrain = TagParser.GetStyleName(p);
                    if (validTerrains.Contains(terrain)) {
                        var currentPassportData = passportEntries[terrain];
                        var name = TagParser.GetName(p);
                        if (name == "")
                            name = terrain;
                        response = terrain + "|" + name + "|" + p.PrivacyId;
                        if (!currentPassportData.currentEntries.Any(e => e.StartsWith(name) || e.EndsWith(p.PrivacyId.ToString()))) //can't hit 2 different unnamed places.
                        {
                            currentPassportData.currentEntries.Add(response);
                            if (currentPassportData.currentEntries.Count == entryCount) {
                                currentPassportData = new PassportEntry();

                                //Grant reward for completing a set.
                                var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                                var rewardCreature = passportRewards.PickOneRandom();
                                var reward = creatureData.TryGetValue(rewardCreature.id, out var foundReward);
                                if (!reward) {
                                    foundReward = new PlayerCreatureInfo() { id = rewardCreature.id, available = true, level = 0, totalCaught = 0, currentAvailable = 0, currentAvailableCompete = 0 };
                                }
                                foundReward.BoostCreature();
                                GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
                                response = terrain + "|" + creatureList[(int)foundReward.id].name;
                            }
                        }
                        break;
                    }
                }

                //save passport data if any changes occurred
                if (response != "")
                    GenericData.SetSecurePlayerDataJson(accountId, "passport", passportEntries, password);
            });
            return response;
        }
    }
}