using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Geometries;
using PraxisCore;
using System.Collections.Concurrent;

namespace PraxisCreatureCollectorPlugin
{
    public static class CreatureCollectorGlobals
    {
        public static List<string> AuthWhiteList = new List<string>() { };

        public static Config config;
        //public static string pepper = ""; //Forces external passwords to be a minimum length and complexity to discourage brute force attacks against the secureData table. TODO move to config or apply to server?
        public static List<Creature> creatureList; //all creatures to collect.
        public static Dictionary<string, List<Creature>> terrainSpawnTables = new Dictionary<string, List<Creature>>();
        public static Dictionary<string, List<Creature>> areaSpawnTables = new Dictionary<string, List<Creature>>();
        public static Dictionary<string, List<Creature>> placeSpawnTables = new Dictionary<string, List<Creature>>();
        public static ConcurrentDictionary<string, DateTime> spawnLocks = new ConcurrentDictionary<string, DateTime>();
        public static string internalPassword = ""; //Leave empty in code, is filled in from main appsettings.json
        public static string ActiveChallengeOptions = "ABCDE";
        public static ConcurrentDictionary<string, SimpleLockable> updateLocks = new ConcurrentDictionary<string, SimpleLockable>();
        public static List<string> gameplayAreas = new List<string>();
        public static ReaderWriterLockSlim startupLock = new ReaderWriterLockSlim();
        public static List<Creature> passportRewards = new List<Creature>();

        public static readonly string[] walkableAreas = new string[] { "tertiary", "trail" };
        public static MemoryCacheEntryOptions SlidingExpiration2Min = new MemoryCacheEntryOptions() { SlidingExpiration = new TimeSpan(0, 2, 0) };
        public static MemoryCacheEntryOptions SlidingExpiration1Hr = new MemoryCacheEntryOptions() { SlidingExpiration = new TimeSpan(1, 0, 0) };
        public static MemoryCacheEntryOptions AbsoluteExpiration30Sec = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = new TimeSpan(0, 0, 30) };
        public static MemoryCacheEntryOptions AbsoluteExpiration15Min = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = new TimeSpan(0, 15, 0) };
        public static MemoryCacheEntryOptions AbsoluteExpiration12Hr = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = new TimeSpan(12, 0, 0) };

        //optimizations for finding values.
        public static Dictionary<long, Creature> creaturesById;
        public static Dictionary<string, Creature> creaturesByName;
        public static List<Creature> additionalSpawns;
        public static List<Creature> wanderingCreatures;

        //Compete mode data.
        public static Geometry[] teamItems = new Geometry[5]; //0 is empty so the rest line up with the teamId. The complete geometry involved for drawing maptiles.
        public static ConcurrentDictionary<string, CompeteModeEntry> allEntries = new ConcurrentDictionary<string, CompeteModeEntry>(); //Used for lookups. Is concurrent now.

        public static long graduateGrantsCount = 10000;
        public static long graduateCreatureCount = 20; //update to ~50% of creature list.
        public static DbTables.Place playBoundary; //Region/county/state/whatever where the game takes place.

        
    }
}
