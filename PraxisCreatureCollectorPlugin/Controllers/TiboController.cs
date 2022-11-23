using Google.OpenLocationCode;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PraxisCore;
using System.Text;
using System.Text.Json;
using static CreatureCollectorAPI.CommonHelpers;
using static CreatureCollectorAPI.CreatureCollectorGlobals;

namespace CreatureCollectorAPI.Controllers
{
    //TIBO: The Immortal Battle for Ohio, the original reference implementation.
    //This handles the 'core' stuff related to the game, like account data and getting/setting non-game-mode stuff like ProxyPlay point.
    public class TiboController : Controller
    {
        private readonly IConfiguration Configuration;
        private static IMemoryCache cache;

        public TiboController(IConfiguration configuration, IMemoryCache memoryCacheSingleton)
        {
            Configuration = configuration;
            cache = memoryCacheSingleton;
        }

        [HttpPut]
        [Route("/[controller]/Account/Create")] 
        public bool CreateAccount()
        {
            GetAuthInfo(Response, out var accountName, out var password2);

            var existsCheck = GenericData.GetPlayerData(accountName, "account"); //this will show us encrypted data exists but not decode it.
            if (existsCheck.Length > 0)
                return false;

            Account data = new Account();
            data.name = accountName;
            GenericData.SetSecurePlayerDataJson(accountName, "account", data, password2);

            var creatureInfo = MakeStarterCreatureInfo();
            GenericData.SetSecurePlayerDataJson(accountName, "creatureInfo", creatureInfo, password2);

            var taskInfo = ImprovementTasks.DefaultTasks.ToDictionary(k => k.id, v => v);
            GenericData.SetSecurePlayerDataJson(accountName, "taskInfo", taskInfo, password2);

            var grantBlocks = new Dictionary<string, DateTime>();
            GenericData.SetSecurePlayerDataJson(accountName, "grantBlocks", grantBlocks, password2);

            //Not secure - not attached to any player info besides participating in the game.
            var tutorials = new List<string>();
            GenericData.SetPlayerDataJson(accountName, "viewedTutorials", tutorials);

            return true;
        }

        [HttpGet]
        [Route("/[controller]/Tutorial/")]
        public List<string> GetTutorialViewed()
        {
            var accountId = GetAccountFromHeaders(Response);
            Response.Headers.Add("X-noPerfTrack", "Tibo/Tutorial/GET");
            var tutorials = GenericData.GetPlayerData<List<string>>(accountId, "viewedTutorials");
            return tutorials;
        }

        [HttpPut]
        [Route("/[controller]/Tutorial/{tutorial}")]
        public void SetTutorialViewed(string tutorial)
        {
            Response.Headers.Add("X-noPerfTrack", "Tibo/Tutorial/PUT");
            var accountId = GetAccountFromHeaders(Response);
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                var tutorials = GenericData.GetPlayerData<List<string>>(accountId, "viewedTutorials");
                if (tutorials == null)
                    tutorials = new List<string>();
                if (!tutorials.Contains(tutorial))
                {
                    tutorials.Add(tutorial);
                    GenericData.SetPlayerDataJson(accountId, "viewedTutorials", tutorials);
                }
            }
            DropUpdateLock(accountId, playerLock);
        }

        [HttpPut]
        [Route("/[controller]/Team/{teamId}")]
        public bool SetPlayerTeam(long teamId)
        {
            Response.Headers.Add("X-noPerfTrack", "Tibo/Team/VARSREMOVED");
            GetAuthInfo(Response, out var accountId, out var password);
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                Account account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
                if (account.team == 0) //First time we call this, don't burn a token.
                {
                    account.team = teamId;
                }
                else if (account.currencies.teamSwapTokens > 0)
                {
                    account.currencies.teamSwapTokens--;
                    account.team = teamId;

                    DeleteAllPlayerControlEntries(accountId, password);
                    DeleteAllPlayerCompeteEntries(accountId, password);
                    ResetCreatureData(accountId, password);
                }
                GenericData.SetSecurePlayerDataJson(accountId, "account", account, password);
            }
            DropUpdateLock(accountId, playerLock);
            return true;
        }

        [HttpPut]
        [Route("/[controller]/ProxyPlay/")]
        [Route("/[controller]/ProxyPlay/{plusCode10}")]
        public string SetProxyPlayPoint(string plusCode10 = "")
        {
            if (!string.IsNullOrWhiteSpace(plusCode10) && !DataCheck.IsInBounds(plusCode10))
                return "";

            //take a PlusCode, set the point.
            Response.Headers.Add("X-noPerfTrack", "Tibo/ProxyPlay/VARSREMOVED");
            GetAuthInfo(Response, out var accountId, out var password);
            string response = "";
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                Account account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
                if (account.currencies.proxyPlayTokens > 0)
                {
                    account.currencies.proxyPlayTokens--;
                    if (string.IsNullOrWhiteSpace(plusCode10))
                    {
                        account.proxyPlayPoint = null;
                        response = "{}";
                    }
                    else
                    {
                        var point = OpenLocationCode.DecodeValid(plusCode10);
                        account.proxyPlayPoint = new ProxyPoint(point.CenterLatitude, point.CenterLongitude);
                        response = "{\"lat\":" + point.CenterLatitude.ToString() + ",\"lon\":" + point.CenterLongitude.ToString() + "}";
                    }

                    GenericData.SetSecurePlayerDataJson(accountId, "account", account, password);
                }
            }
            DropUpdateLock(accountId, playerLock);
            return response;
        }

        [HttpGet]
        [Route("/[controller]/Account/")] 
        public Account GetAccount() 
        {
            //This function is by far the slowest part of startup, mostly due to the daily creature audit. Skipping that this is much faster.
            Response.Headers.Add("X-noPerfTrack", "Tibo/Account/VARSREMOVED");
            GetAuthInfo(Response, out var accountName, out var password);

            Account account;
            var playerLock = GetUpdateLock(accountName);
            lock (playerLock)
            {
                CheckImprovementTasks(accountName, password); //This possibly saves data to Account, so load that after this call.
                ProcessPendingCommand(accountName, password); //We can do these here, instead of the client handling them, because i have the password while they're logged in.

                account = GenericData.GetSecurePlayerData<Account>(accountName, "account", password);
                if (account.lastAudit < DateTime.UtcNow.AddDays(-1)) //An audit is done once every 24 hours, to save time on repeated logins.
                {
                    //Audit creatures, see which ones have returned from claimed places that got flipped in Control mode.
                    var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountName, "creatureInfo", password);
                    if (AuditCreaturePlacement(accountName, ref creatureData))
                    {
                        GenericData.SetSecurePlayerDataJson(accountName, "creatureInfo", creatureData, password);
                    }

                    //TODO: how to audit entries in Compete mode? Might need a separate list of the player's placements, creatures, and counts.
                    account.lastAudit = DateTime.UtcNow;
                    GenericData.SetSecurePlayerDataJson(accountName, "account", account, password);
                }
            }
            DropUpdateLock(accountName, playerLock);

            return account;
        }

        [HttpDelete]
        [Route("/[controller]/Account/")]
        public bool DeleteAccount()
        {
            Response.Headers.Add("X-noPerfTrack", "Tibo/DeleteAccount/VARSREMOVED");
            GetAuthInfo(Response, out var accountName, out var password);
            //NOTE: core PraxisMapper server will handle removing DB entries on DELETE /Account/accountId. I only have to handle removing shared data here.
            var playerLock = GetUpdateLock(accountName);
            lock (playerLock)
            {
                Account data = GenericData.GetSecurePlayerData<Account>(accountName, "account", password);
                //var internalPassword = data.internalPwd;

                //No errors here means we want to totally remove this player from the game.
                //Account, player data components, placed creatures.
                DeleteAllPlayerControlEntries(accountName, password);
                DeleteAllPlayerCompeteEntries(accountName, password);
            }
            DropUpdateLock(accountName, playerLock);
            return true;
        }

        [HttpPut]
        [Route("/[controller]/Account/ChangePwd/{accountName}/{oldPassword}/{newPassword}")] //ssl should keep this safe, but it would be better to move it to the body later.
        public bool ChangePassword(string accountName, string oldPassword, string newPassword)
        {
            Response.Headers.Add("X-noPerfTrack", "Tibo/Account/ChangePassword/VARSREMOVED");
            //NOTE: despite the server ChangePassword call, I still need this one to update the Account data field.
            //NOTE: this will need to re-encrypt the account field, all the internal ones should stay since the user isn't changing those and never sees those.
            //if (GenericData.CheckPassword(accountName, oldPassword + pepper))  //Player password is encrypted with super-good stuff
            //{
            bool results = false;
            var playerLock = GetUpdateLock(accountName);
            lock (playerLock)
            {
                var accountData = GenericData.GetSecurePlayerData(accountName, "password", oldPassword);
                results = GenericData.SetSecurePlayerData(accountName, "password", accountData, newPassword);
            }
            DropUpdateLock(accountName, playerLock);
            return results;
        }

        public static bool AuditCreaturePlacement(string accountId, ref Dictionary<long, PlayerCreatureInfo> creatures)
        {
            //If true, something was fixed, save the creature data back to the database.
            //TODO include compete mode entries in the list of stuff to audit.
            bool needsSaved = false;
            string[] improvementTasks = new string[] { "clone", "ppt", "hint", "tst", "vortex" };
            string[] removedTasks = new string[] { "iwt", "rpt" };
            foreach (var c in creatures)
            {
                var thisCreature = c.Value;

                if (thisCreature.currentAvailableCompete > thisCreature.totalCaught)
                {
                    thisCreature.currentAvailableCompete = thisCreature.totalCaught;
                    needsSaved = true;
                }

                if (thisCreature.currentAvailable > thisCreature.totalCaught)
                {
                    thisCreature.currentAvailable = thisCreature.totalCaught;
                    needsSaved = true;
                }

                if (thisCreature.available)
                    continue;

                
                //if creatures were doing tasks, update those tasks with results.
                if (improvementTasks.Contains(thisCreature.assignedTo))
                    continue; //This is handled in a separate function.

                
                if (removedTasks.Contains(thisCreature.assignedTo))
                {
                    thisCreature.assignedTo = "";
                    thisCreature.available = true;
                    needsSaved = true;
                    continue;
                }

                var placeAt = GetCreaturesInArea(thisCreature.assignedTo);
                if (!placeAt.Any(p => p.creatureId == c.Key && p.owner == accountId))
                {
                    thisCreature.assignedTo = "";
                    thisCreature.available = true;
                    needsSaved = true;
                }
            }
            return needsSaved;
        }

        [HttpGet]
        [Route("/[controller]/Updates/")]
        public string GetUpdates()
        {
            Response.Headers.Add("X-noPerfTrack", "Tibo/Updates/VARSREMOVED");
            GetAuthInfo(Response, out var accountId, out var password);

            var updateData = "";
            var playerLock = GetUpdateLock(accountId);
            lock (playerLock)
            {
                updateData = GenericData.GetSecurePlayerData(accountId, "Updates", internalPassword).ToUTF8String();
                if (updateData != "")
                    ProcessPendingCommand(accountId, password); //also clears out the saved data.
            }
            return updateData;
        }

        [HttpGet]
        [Route("/[controller]/SuggestProxy")]
        public string SuggestProxyPlayPoint()
        {
            if (cache.TryGetValue("suggestedProxyPoint", out string code))
                return code;

            var newCode = PraxisCore.Place.RandomPoint(cache.Get<DbTables.ServerSetting>("settings"));
            cache.Set("suggestedProxyPoint", newCode, AbsoluteExpiration12Hr);
            return newCode;
        }

        //Developer helper call for testing stuff, since I can't actually access data directly.
        [HttpGet]
        [Route("/[controller]/DevHelper/{accountId}")]
        public void SetDevHelpCommand(string accountId)
        {
            UpdateAccountPendingCommand(accountId, "DEVHELPER", "");
        }

        [HttpGet]
        [Route("/[controller]/ArtVersion")]
        public string GetArtVersion()
        {
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

            var cities = db.PlaceTags.Include(t => t.Place).Where(t => t.Key == "admin_level" && t.Value == "8").ToList(); // admin_level=8 is generally what we're looking for.
            var topCity = cities.OrderByDescending(c => c.Place.AreaSize).FirstOrDefault(); //biggest physical city.

            var parks = db.PlaceTags.Include(t => t.Place).Where(t => t.Key == "leisure" && (t.Value == "park" || t.Value == "playground")).ToList();
            var topPark = parks.OrderByDescending(c => c.Place.AreaSize).FirstOrDefault(); //biggest physical park

            var natureReserves = db.PlaceTags.Include(t => t.Place).Where(t => t.Key == "leisure" && t.Value == "nature_reserve").ToList();
            var topNR = natureReserves.OrderByDescending(c => c.Place.AreaSize).FirstOrDefault();

            string[] waterTypes = new string[] { "water", "strait", "bay", "coastline" };
            var waters = db.PlaceTags.Include(t => t.Place).Where(t => t.Key == "natural" && waterTypes.Contains(t.Value)).ToList();
            var topWater = waters.OrderByDescending(c => c.Place.AreaSize).FirstOrDefault();

            var pointCity = new OpenLocationCode(topCity.Place.ElementGeometry.Centroid.Y, topCity.Place.ElementGeometry.Centroid.X);
            var pointPark = new OpenLocationCode(topPark.Place.ElementGeometry.Centroid.Y, topPark.Place.ElementGeometry.Centroid.X);
            var pointNR = new OpenLocationCode(topNR.Place.ElementGeometry.Centroid.Y, topNR.Place.ElementGeometry.Centroid.X);
            var pointWater = new OpenLocationCode(topWater.Place.ElementGeometry.Centroid.Y, topWater.Place.ElementGeometry.Centroid.X); //This might be better set at the coastline nearest the middle of the play area.

            var suggestions = new { 
                city =  new { name = TagParser.GetPlaceName(topCity.Place.Tags), desc = "Largest City", location = pointCity.CodeDigits },
                park = new { name = TagParser.GetPlaceName(topPark.Place.Tags), desc = "Largest Park", location = pointPark.CodeDigits },
                natureReserve = new { name = TagParser.GetPlaceName(topNR.Place.Tags), desc = "Largest Nature Reserve", location = pointNR.CodeDigits },
                water =new { name = TagParser.GetPlaceName(topWater.Place.Tags), desc = "Largest Body of Water", location = pointWater.CodeDigits }
            };

            var sugString = JsonSerializer.Serialize(suggestions);
            return sugString;
        }

        //TODO move this to some helper class or plugin. Maybe Larry?
        [HttpGet]
        [Route("/[controller]/terrainSize")]
        public string TestGetTerrainAreaSizes()
        {
            StringBuilder sb = new StringBuilder();
            var db = new PraxisContext();
            //NOTE: AreaSize is length or perimeter. Not surface area of the planet. It's an estimate to help guess on things for the database without calculating things every time.
            var areas = db.Places
                .Include(p => p.Tags)
                .Select(p => new { Area = p.AreaSize, ID = p.Id, tags = p.Tags })
                .AsEnumerable()
                .GroupBy(p => TagParser.GetAreaType(p.tags, "mapTiles"))
                .OrderByDescending(a => a.Count());

            foreach (var a in areas)
                sb.AppendLine(a.Key + ": " + a.Sum(aa => aa.Area) + " deg long, " + a.Count() + " entries");

            return sb.ToString();
        }
    }
}