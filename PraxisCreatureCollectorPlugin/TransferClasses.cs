namespace CreatureCollectorAPI
{
    public class ClaimData
    {
        public int team { get; set; }
        public string owner { get; set; }
        public long level { get; set; } //calculate stats as needed based on level.
        public int creatureId { get; set; }
        public string creatureName { get; set; }
    }

    public class PassportEntry
    {
        public List<string> currentEntries { get; set; } = new List<string>();
    }

    public class PlayerCreatureInfo
    {
        public long id { get; set; } //Which creature this is for.
        public long level { get; set; }
        public bool available { get; set; } = true; //boolean for use in Control mode. Always uses max strength, only put a creature in 1 place
        public string assignedTo { get; set; } = "";
        public long totalCaught { get; set; } //Total count of this creature we've ever caught
        public long currentAvailable { get; set; } //How many copies of this creature we have left in Cover(PVE) mode
        public long currentAvailableCompete { get; set; } //How many copies of this creature we have left in Compete(PVP) mode
        public long toNextLevel { get; set; }
        public bool hintUnlocked { get; set; }
        //These may not be necessary for the account total value, since they're derived from the creature's stats info.
        public long strength { get; set; }
        public long defense { get; set; }
        public long scouting { get; set; }


        public void BoostCreature() //We just caught one of these.
        {
            toNextLevel--;
            totalCaught++;
            currentAvailable++;
            currentAvailableCompete++;
            if (toNextLevel < 1)
            {
                LevelUp();
            }
        }

        public void FastBoost(long fragmentCount)
        {
            currentAvailable += fragmentCount;
            currentAvailableCompete += fragmentCount;
            while (fragmentCount >= toNextLevel)
            {
                fragmentCount = fragmentCount - toNextLevel;
                LevelUp();
            }
            toNextLevel = toNextLevel - fragmentCount;
        }

        public void LevelUp()
        {
            level++;
            SetToLevel(level);
        }

        public void SetToLevel(long newlevel)
        {
            level = newlevel;
            var creatureBaseInfo = CreatureCollectorGlobals.creaturesById[id];
            strength = (long)(level * creatureBaseInfo.stats.strengthPerLevel);
            defense = (long)(level * creatureBaseInfo.stats.defensePerLevel);
            scouting = (long)(level * creatureBaseInfo.stats.scoutingPerLevel);
            toNextLevel = (long)(level * creatureBaseInfo.stats.multiplierPerLevel) + (creatureBaseInfo.stats.addedPerLevel * level);
        }
    }

    public class ImprovementTasks
    {
        public string id { get; set; }
        public string name { get; set; }
        public long timePerResult { get; set; }
        public long accrued { get; set; }
        public long assigned { get; set; } //creature id
        public DateTime lastCheck { get; set; }
        public string desc { get; set; }

        public static List<ImprovementTasks> DefaultTasks = new List<ImprovementTasks>()
        {
            new ImprovementTasks() { id = "clone", name = "Find Creature", timePerResult = 60 * 60 * 12, desc = "Level up assigned creature slowly." },
            new ImprovementTasks() { id = "ppt", name = "ProxyPlay Token", timePerResult = 60 * 60 * 24 * 7, desc = "Choose a different place to explore remotely." },
            new ImprovementTasks() { id = "hint", name = "Creature Hint", timePerResult = 60 * 60 * 60 * 24, desc = "A clue for the next unfound creature" },
            new ImprovementTasks() { id = "tst", name = "Team Swap Token", timePerResult = 60 * 60 * 24 * 14, desc = "Change which team you're part of." },
            //new ImprovementTasks() { id = "iwt", name = "Instant-Win Token", timePerResult = 60 * 60 * 24 * 7, desc = "Force-flip a place regardless of creature strength." },
            new ImprovementTasks() { id = "vortex", name = "Vortex Token", timePerResult = 60 * 60 * 24, desc = "Collect all creature fragments in current map tile and neighbors." },
        };
    }

    public class SimpleLockable
    {
        public long counter { get; set; }
    }

    public class EnterAreaResults
    {
        public long coinsGranted { get; set; }
        public long creatureIdCaught { get; set; }
        public Guid creatureUidCaught { get; set; }
        public string plusCode { get; set; }
        public string activeGame { get; set; }
        public int difficulty { get; set; }
    }

    public class ControlLeaderboardResult
    {
        public long team1Score { get; set; }
        public long team2Score { get; set; }
        public long team3Score { get; set; }
        public long team4Score { get; set; }
    }

    public class CheckEnterArea
    {
        public string accountId { get; set; }
        public string password { get; set; }
        public string plusCode { get; set; }

    }
}