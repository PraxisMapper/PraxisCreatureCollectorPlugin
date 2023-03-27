using Google.OpenLocationCode;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PraxisCore;
using PraxisMapper.Classes;
using System.Text.Json;
using static PraxisCreatureCollectorPlugin.CommonHelpers;
using static PraxisCreatureCollectorPlugin.CreatureCollectorGlobals;

namespace PraxisCreatureCollectorPlugin.Controllers {
    //TIBO: The Immortal Battle for Ohio, the original reference implementation.
    //This handles the 'core' stuff related to the game, like account data and getting/setting non-game-mode stuff like ProxyPlay point.
    public class TiboController : Controller {
        private readonly IConfiguration Configuration;
        private static IMemoryCache cache;

        string accountId, password;
        public override void OnActionExecuting(ActionExecutingContext context) {
            base.OnActionExecuting(context);
            PraxisAuthentication.GetAuthInfo(Response, out accountId, out password);
        }

        public TiboController(IConfiguration configuration, IMemoryCache memoryCacheSingleton) {
            Configuration = configuration;
            cache = memoryCacheSingleton;
        }

        [HttpPut]
        [Route("/[controller]/Account/Create")]
        public bool CreateAccount() {

            var existsCheck = GenericData.GetPlayerData(accountId, "account"); //this will show us encrypted data exists but not decode it.
            if (existsCheck.Length > 0)
                return false;

            Account data = new Account();
            data.name = accountId;
            GenericData.SetSecurePlayerDataJson(accountId, "account", data, password);

            var creatureInfo = MakeStarterCreatureInfo();
            GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureInfo, password);

            var taskInfo = ImprovementTasks.DefaultTasks.ToDictionary(k => k.id, v => v);
            GenericData.SetSecurePlayerDataJson(accountId, "taskInfo", taskInfo, password);

            var grantBlocks = new Dictionary<string, DateTime>();
            GenericData.SetSecurePlayerDataJson(accountId, "grantBlocks", grantBlocks, password);

            //Not secure - not attached to any player info besides participating in the game.
            var tutorials = new List<string>();
            GenericData.SetPlayerDataJson(accountId, "viewedTutorials", tutorials);

            return true;
        }

        [HttpGet]
        [Route("/[controller]/Tutorial/")]
        public List<string> GetTutorialViewed() {
            Response.Headers.Add("X-noPerfTrack", "Tibo/Tutorial/GET");
            var tutorials = GenericData.GetPlayerData<List<string>>(accountId, "viewedTutorials");
            return tutorials;
        }

        [HttpPut]
        [Route("/[controller]/Tutorial/{tutorial}")]
        public void SetTutorialViewed(string tutorial) {
            Response.Headers.Add("X-noPerfTrack", "Tibo/Tutorial/PUT");
            SimpleLockable.PerformWithLock(accountId, () => {
                var tutorials = GenericData.GetPlayerData<List<string>>(accountId, "viewedTutorials");
                if (tutorials == null)
                    tutorials = new List<string>();
                if (!tutorials.Contains(tutorial)) {
                    tutorials.Add(tutorial);
                    GenericData.SetPlayerDataJson(accountId, "viewedTutorials", tutorials);
                }
            });
        }

        [HttpPut]
        [Route("/[controller]/Team/{teamId}")]
        public bool SetPlayerTeam(long teamId) {
            Response.Headers.Add("X-noPerfTrack", "Tibo/Team/VARSREMOVED");
            SimpleLockable.PerformWithLock(accountId, () => {
                Account account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
                if (account.team == 0) //First time we call this, don't burn a token.
                {
                    account.team = teamId;
                }
                else if (account.currencies.teamSwapTokens > 0) {
                    account.currencies.teamSwapTokens--;
                    account.team = teamId;

                    DeleteAllPlayerControlEntries(accountId, password);
                    DeleteAllPlayerCompeteEntries(accountId, password);
                    ResetCreatureData(accountId, password);
                }
                GenericData.SetSecurePlayerDataJson(accountId, "account", account, password);
            });
            return true;
        }

        [HttpPut]
        [Route("/[controller]/ProxyPlay/")]
        [Route("/[controller]/ProxyPlay/{plusCode10}")]
        public string SetProxyPlayPoint(string plusCode10 = "") {
            if (!string.IsNullOrWhiteSpace(plusCode10) && !DataCheck.IsInBounds(plusCode10))
                return "";

            //take a PlusCode, set the point.
            Response.Headers.Add("X-noPerfTrack", "Tibo/ProxyPlay/VARSREMOVED");
            string response = "";
            SimpleLockable.PerformWithLock(accountId, () => {
                Account account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
                if (account.currencies.proxyPlayTokens > 0) {
                    account.currencies.proxyPlayTokens--;
                    if (string.IsNullOrWhiteSpace(plusCode10)) {
                        account.proxyPlayPoint = null;
                        response = "{}";
                    }
                    else {
                        var point = OpenLocationCode.DecodeValid(plusCode10);
                        account.proxyPlayPoint = new ProxyPoint(point.CenterLatitude, point.CenterLongitude);
                        response = "{\"lat\":" + point.CenterLatitude.ToString() + ",\"lon\":" + point.CenterLongitude.ToString() + "}";
                    }

                    GenericData.SetSecurePlayerDataJson(accountId, "account", account, password);
                }
            });
            return response;
        }

        [HttpGet]
        [Route("/[controller]/Account/")]
        public Account GetAccount() {
            //This function is by far the slowest part of startup, mostly due to the daily creature audit. Skipping that this is much faster.
            Response.Headers.Add("X-noPerfTrack", "Tibo/Account/VARSREMOVED");

            Account account = new Account();
            SimpleLockable.PerformWithLock(accountId, () => {
                CheckImprovementTasks(accountId, password); //This possibly saves data to Account, so load that after this call.
                ProcessPendingCommand(accountId, password); //We can do these here, instead of the client handling them, because i have the password while they're logged in.

                account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
                if (account.lastAudit < DateTime.UtcNow.AddDays(-1)) //An audit is done once every 24 hours, to save time on repeated logins.
                {
                    //Audit creatures, see which ones have returned from claimed places that got flipped in Control mode.
                    var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                    if (AuditCreaturePlacement(accountId, ref creatureData)) {
                        GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
                    }

                    //TODO: how to audit entries in Compete mode? Might need a separate list of the player's placements, creatures, and counts.
                    account.lastAudit = DateTime.UtcNow;
                    GenericData.SetSecurePlayerDataJson(accountId, "account", account, password);
                }
            });

            return account;
        }

        [HttpDelete]
        [Route("/[controller]/Account/")]
        public bool DeleteAccount() {
            Response.Headers.Add("X-noPerfTrack", "Tibo/DeleteAccount/VARSREMOVED");
            //NOTE: core PraxisMapper server will handle removing DB entries on DELETE /Account/accountId. I only have to handle removing shared data here.
            SimpleLockable.PerformWithLock(accountId, () => {
                Account data = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
                //var internalPassword = data.internalPwd;

                //No errors here means we want to totally remove this player from the game.
                //Account, player data components, placed creatures.
                DeleteAllPlayerControlEntries(accountId, password);
                DeleteAllPlayerCompeteEntries(accountId, password);
            });
            return true;
        }

        [HttpPut]
        [Route("/[controller]/Account/ChangePwd/{accountName}/{oldPassword}/{newPassword}")] //ssl should keep this safe, but it would be better to move it to the body later.
        public bool ChangePassword(string accountId, string oldPassword, string newPassword) {
            Response.Headers.Add("X-noPerfTrack", "Tibo/Account/ChangePassword/VARSREMOVED");
            //NOTE: despite the server ChangePassword call, I still need this one to update the Account data field.
            //NOTE: this will need to re-encrypt the account field, all the internal ones should stay since the user isn't changing those and never sees those.
            //if (GenericData.CheckPassword(accountName, oldPassword + pepper))  //Player password is encrypted with super-good stuff
            //{
            bool results = false;
            SimpleLockable.PerformWithLock(accountId, () => {
                var accountData = GenericData.GetSecurePlayerData(accountId, "password", oldPassword);
                results = GenericData.SetSecurePlayerData(accountId, "password", accountData, newPassword);
            });
            return results;
        }

        public static bool AuditCreaturePlacement(string accountId, ref Dictionary<long, PlayerCreatureInfo> creatures) {
            //If true, something was fixed, save the creature data back to the database.
            //TODO include compete mode entries in the list of stuff to audit.
            bool needsSaved = false;
            string[] improvementTasks = new string[] { "clone", "ppt", "hint", "tst", "vortex" };
            string[] removedTasks = new string[] { "iwt", "rpt" };
            foreach (var c in creatures) {
                var thisCreature = c.Value;

                if (thisCreature.currentAvailableCompete > thisCreature.totalCaught) {
                    thisCreature.currentAvailableCompete = thisCreature.totalCaught;
                    needsSaved = true;
                }

                if (thisCreature.currentAvailable > thisCreature.totalCaught) {
                    thisCreature.currentAvailable = thisCreature.totalCaught;
                    needsSaved = true;
                }

                if (thisCreature.available)
                    continue;


                //if creatures were doing tasks, update those tasks with results.
                if (improvementTasks.Contains(thisCreature.assignedTo))
                    continue; //This is handled in a separate function.


                if (removedTasks.Contains(thisCreature.assignedTo)) {
                    thisCreature.assignedTo = "";
                    thisCreature.available = true;
                    needsSaved = true;
                    continue;
                }

                var placeAt = GetCreaturesInArea(thisCreature.assignedTo);
                if (!placeAt.Any(p => p.creatureId == c.Key && p.owner == accountId)) {
                    thisCreature.assignedTo = "";
                    thisCreature.available = true;
                    needsSaved = true;
                }
            }
            return needsSaved;
        }

        [HttpGet]
        [Route("/[controller]/Updates/")]
        public List<UpdateCommand> GetUpdates() {
            Response.Headers.Add("X-noPerfTrack", "Tibo/Updates/VARSREMOVED");

            List<UpdateCommand> updateData = null;
            SimpleLockable.PerformWithLock(accountId, () => {
                updateData = ProcessPendingCommand(accountId, password); //also clears out the saved data.
            });
            return updateData;
        }

        [HttpGet]
        [Route("/[controller]/SuggestProxy")]
        public string SuggestProxyPlayPoint() {
            if (cache.TryGetValue("suggestedProxyPoint", out string code))
                return code;

            var newCode = PraxisCore.Place.RandomPoint(cache.Get<DbTables.ServerSetting>("settings")); //This generates a value somewhere inside the bounding box.
            var polyCode = newCode.ToPolygon();
            while (!polyCode.Intersects(CreatureCollectorGlobals.playBoundary)) //This will make sure the generated area actually appears inside the play area.
            {
                newCode = PraxisCore.Place.RandomPoint(cache.Get<DbTables.ServerSetting>("settings"));
                polyCode = newCode.ToPolygon();
            }

            cache.Set("suggestedProxyPoint", newCode, AbsoluteExpiration12Hr);
            return newCode;
        }

        //Developer helper call for testing stuff, since I can't actually access data directly.
        [HttpGet]
        [Route("/[controller]/DevHelper/{accountId}")]
        public void SetDevHelpCommand(string accountId) {
            UpdateAccountPendingCommand(accountId, "DEVHELPER", "");
        }

        [HttpGet]
        [Route("/[controller]/ArtVersion")]
        public string GetArtVersion() {
            //TODO: cache this on a sliding expiration.
            return GenericData.GetGlobalData("artVersion").ToUTF8String();
        }

        public bool IncrementAllValues() //Call when updating the server to clear changed data and increment version counts, then restart server.
        {
            //TODO implement
            return true;
        }

        public string SuggestProxyPlayPoints() //TODO: move this to startup, since its going to be run on start if no proxy play points exist in db.
        {
            //scan tags DB for 3-5 the following entries:
            //* Largest city/metro by population
            //* Largest park and nature reserve
            //* Largest body of water

            var db = new PraxisContext();

            //TODO: set these up to use variables instead of constant strings.
            var cities = db.PlaceTags.Include(t => t.Place).Where(t => t.Key == "admin_level" && t.Value == "8").ToList(); // admin_level=8 is generally what we're looking for.
            var topCity = cities.OrderByDescending(c => c.Place.DrawSizeHint).FirstOrDefault(); //biggest physical city.

            var parks = db.PlaceTags.Include(t => t.Place).Where(t => t.Key == "leisure" && (t.Value == "park" || t.Value == "playground")).ToList();
            var topPark = parks.OrderByDescending(c => c.Place.DrawSizeHint).FirstOrDefault(); //biggest physical park

            var natureReserves = db.PlaceTags.Include(t => t.Place).Where(t => t.Key == "leisure" && t.Value == "nature_reserve").ToList();
            var topNR = natureReserves.OrderByDescending(c => c.Place.DrawSizeHint).FirstOrDefault();

            string[] waterTypes = new string[] { "water", "strait", "bay", "coastline" };
            var waters = db.PlaceTags.Include(t => t.Place).Where(t => t.Key == "natural" && waterTypes.Contains(t.Value)).ToList();
            var topWater = waters.OrderByDescending(c => c.Place.DrawSizeHint).FirstOrDefault();

            var pointCity = new OpenLocationCode(topCity.Place.ElementGeometry.Centroid.Y, topCity.Place.ElementGeometry.Centroid.X);
            var pointPark = new OpenLocationCode(topPark.Place.ElementGeometry.Centroid.Y, topPark.Place.ElementGeometry.Centroid.X);
            var pointNR = new OpenLocationCode(topNR.Place.ElementGeometry.Centroid.Y, topNR.Place.ElementGeometry.Centroid.X);
            var pointWater = new OpenLocationCode(topWater.Place.ElementGeometry.Centroid.Y, topWater.Place.ElementGeometry.Centroid.X); //This might be better set at the coastline nearest the middle of the play area.

            var suggestions = new {
                city = new { name = TagParser.GetName(topCity.Place), desc = "Largest City", location = pointCity.CodeDigits },
                park = new { name = TagParser.GetName(topPark.Place), desc = "Largest Park", location = pointPark.CodeDigits },
                natureReserve = new { name = TagParser.GetName(topNR.Place), desc = "Largest Nature Reserve", location = pointNR.CodeDigits },
                water = new { name = TagParser.GetName(topWater.Place), desc = "Largest Body of Water", location = pointWater.CodeDigits }
            };

            var sugString = JsonSerializer.Serialize(suggestions);
            return sugString;
        }
    }
}