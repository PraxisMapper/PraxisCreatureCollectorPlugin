using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PraxisCore;
using static PraxisCreatureCollectorPlugin.CommonHelpers;
using static PraxisCreatureCollectorPlugin.CreatureCollectorGlobals;

namespace PraxisCreatureCollectorPlugin.Controllers
{
    public class CoinShopController : Controller
    {
        //This handles an in-game-currency shop. Players can buy some fragments of creatures in exchange for some coins earned while walking.
        //Ideally this rotates daily, is generated once on demand and cached until the next reset time.
        private readonly IConfiguration Configuration;
        private static IMemoryCache cache;

        public CoinShopController(IConfiguration configuration, IMemoryCache memoryCacheSingleton)
        {
            Configuration = configuration;
            cache = memoryCacheSingleton;
        }

        [HttpGet]
        [Route("/[controller]/Entries")]
        public List<ShopEntry> GetShopEntries()
        {
            List<ShopEntry> results;
            if (cache.TryGetValue<List<ShopEntry>>("shopEntries", out results))
                return results;

            //Plan B: see if we have a set of data persisted to the DB to load (the cache may drop stuff if there's enough memory pressure)
            //This is stored in a Cell2 area so that it can be expired. Global data does not expire.
            results = GenericData.GetAreaData<List<ShopEntry>>("86", "shopEntries");
            if (results != null)
                return results;

            results = new List<ShopEntry>();
            //make new entries.
            //Rules: 1 wild spawn, 1 elite reward, 1 special (out-of-season, unavilable, or high-tier)
            var wild = creatureList.Where(c => !c.isHidden && c.isWild && c.CanSpawnNow(DateTime.UtcNow)).PickOneRandom();
            results.Add(new ShopEntry() { creatureId = wild.id, creatureCost = CommonHelpers.DetermineCoinCost(wild) });
            var elite = creatureList.Where(c => !c.isHidden && !c.isWild && !c.passportReward).PickOneRandom();
            results.Add(new ShopEntry() { creatureId = elite.id, creatureCost = CommonHelpers.DetermineCoinCost(elite) });
            var special = creatureList.Where(c => !c.isHidden && !c.CanSpawnNow(DateTime.UtcNow)).PickOneRandom();
            if (special != null)
                results.Add(new ShopEntry() { creatureId = special.id, creatureCost = CommonHelpers.DetermineCoinCost(special) });

            // expires at midnight UTC
            var expireTime = DateTime.UtcNow.AddHours(23 - DateTime.UtcNow.Hour).AddMinutes(59 - DateTime.UtcNow.Minute).AddSeconds(59 - DateTime.UtcNow.Second);
            cache.Set("shopEntries", results, new DateTimeOffset(expireTime));
            GenericData.SetAreaDataJson("86", "shopEntries", results, (expireTime - DateTime.UtcNow).TotalSeconds);
            return results;
        }

        [HttpGet]
        [Route("/[controller]/Buy/{creatureId}")]
        public ShopEntry BuyFragment(int creatureId)
        {
            //Get player info and shop info.
            GetAuthInfo(Response, out var accountId, out var password);
            var shopData = GetShopEntries();
            if (!shopData.Any(s => s.creatureId == creatureId))
                return new ShopEntry();

            //if creature in shop, and player has coins, then add 1 to that creature info entry for the player and remove coins.
            var results = new ShopEntry();
            var playerLock = GetUpdateLock(accountId);
            var cost = DetermineCoinCost(creatureList.First(c => c.id == creatureId));
            lock (playerLock)
            {
                Account account = GenericData.GetSecurePlayerData<Account>(accountId, "account", password);
                if (account.currencies.baseCurrency >= cost)
                {
                    account.currencies.baseCurrency -= cost;
                    var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                    if (creatureData.ContainsKey(creatureId))
                        creatureData[creatureId].BoostCreature();
                    else
                    {
                        creatureData.Add(creatureId, new PlayerCreatureInfo() { level = 0, toNextLevel = 1 });
                        creatureData[creatureId].BoostCreature();
                    }
                    GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
                    GenericData.SetSecurePlayerDataJson(accountId, "account", account, password);
                    results.creatureCost = cost;
                    results.creatureId = creatureId;
                }
            }
            return results;
        }
    }

    public class ShopEntry
    { 
        public long creatureId { get; set; }
        public int creatureCost { get; set; }
    }
}
