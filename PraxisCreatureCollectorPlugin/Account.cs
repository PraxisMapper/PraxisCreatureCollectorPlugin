using System.Collections.Concurrent;

namespace CreatureCollectorAPI
{
    //These get JSON parsed to and from the client, so naming might be lower-case to match up there.
    public class Account
    {
        //public long Id { get; set; }
        public string name { get; set; } = "";
        public long team { get; set; } = 0;
        public ProxyPoint? proxyPlayPoint { get; set; }
        public string controlInfo { get; set; } = "";
        public CurrencyList currencies { get; set; } = new CurrencyList();
        public long totalGrants { get; set; } = 0;
        public DateTime dateCreated { get; set; } = DateTime.UtcNow; //ONLY used to tell player at graduation when they started playing or how many days it's been since.
        public bool graduationEligible { get; set; } //Set server-side when grad conditions are met.
        public DateTime lastAudit { get; set; } = new DateTime(2000, 1, 1);
    }

    public class ProxyPoint
    {
        public double lat { get; set; } = 0;
        public double lon { get; set; } = 0;
        public ProxyPoint(double lat, double lon)
        {
            this.lat = lat;
            this.lon = lon;
        }
    }

    public class CurrencyList
    {
        public long baseCurrency { get; set; } = 0;
        public long instantWinTokens { get; set; } = 0;
        public long proxyPlayTokens { get; set; } = 1; //Everyone gets one for free. 
        public long teamSwapTokens { get; set; } = 0;
        public long vortexTokens { get; set; } = 0;
    }

    public class UpdateCommand
    {
        public string verb { get; set; }
        public string target { get; set; }
    }

    public class CoverModeEntry
    {
        public long creatureId { get; set; }
        public long creatureFragmentCount { get; set; }
        public string locationCell10 { get; set; }
        public double scouting { get; set; }
    }

    //TODO: I may want a version of this that only contains data to send to the client. I don't need to send the full dictionary or Geometry to the player every time.
    public class CompeteModeEntry //Since these may persist in memory forever, this object should be as small as possible.
    {
        public int creatureId { get; set; }
        public int teamId { get; set; }
        public ConcurrentDictionary<string, int> creatureFragmentCounts { get; set; } = new ConcurrentDictionary<string, int>(); //<accountId, fragmentsContributed> to track and restore to them later.
        public string locationCell8 { get; set; }
        public int totalFragments { get; set; } //to skip summing over the dictionary.
        public int scouting { get; set; } //saved to avoid needing to sum from creatureFragmentCounts on draw.
    }

    public class CompeteModeEntryForClient
    {
        public int creatureId { get; set; }
        public int teamId { get; set; }
        public string locationCell8 { get; set; }
        public int totalFragments { get; set; } //to skip summing over the dictionary.
        public int playerFragments { get; set; } //The number of fragment this player has contributed to this point, their dictionary entry value from the full-sized object.
    }

    public class PlayerCompeteEntry
    {
        public int creatureId { get; set; }
        public int fragmentCount { get; set; }
    }
}
