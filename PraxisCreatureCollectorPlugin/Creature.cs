using PraxisCore;

namespace PraxisCreatureCollectorPlugin
{
    //NOTE:
    // Terrain/area/place spawns all apply separately. A creature can spawn at Parks, 82GM0000+00, and 'Pine Street' without interference.
    // spawn times and seasons are applied to all of the above, and to each other. A creature could spawn by the above rules from 11am-1pm on March 29th.

    public class TimeSpawnEntry
    {
        public TimeOnly start;
        public TimeOnly end;
    }

    public class DateSpawnEntry
    {
        public DateOnly start;
        public DateOnly end;
    }

    public class Creature //the full data on creatures
    {
        //Possible additions:
        //A Geometry entry to cover a specific area where spawns are allowed/available, instead of strings? Would have to area-test all of those each time for local spawn tables.
        public long id { get; set; }
        public string name { get; set; }
        public string imageName { get; set; } // downloaded from server, viewed from client device after.
        public LevelStats stats { get; set; }
        public Dictionary<string, long> terrainSpawns { get; set; } = new Dictionary<string, long>(); //Spawns in a Cell8 where a terrain on this list is found. EX: park, nature_reserve
        public Dictionary<string, long> areaSpawns { get; set; } = new Dictionary<string, long>(); //Spawns in a PlusCode cell and all children. "" means 'spawns everywhere'. EX: 86, 86HTGG2C. Use specificSpawns instead of Cell10s.
        public Dictionary<string, long> placeSpawns { get; set; } = new Dictionary<string, long>();//spawns within boundaries/cells where a named element is. EX: only spawn near a chain restaurant, or "Main Street"
        public List<string> specificSpawns { get; set; } = new List<string>(); //If this has entries, this creature only spawns on these Cell#s. Expected to be Cell10s, since Cell8s could be areaSpawns.
        public bool isPermanent { get; set; } = false; //If true, force a creature in the SpecificSpawns space chosen with a new instance of this one. All points must be in the same Cell8 at this point if true.
        public string activeCatchType { get; set; } = CreatureCollectorGlobals.ActiveChallengeOptions; //FUTURE TODO: Might repurpose this to be which of the available active challenges this monster could spawn.
        public int activeCatchDifficulty { get; set; } = 1;  //FUTURE TODO: each challenge will have some pre-set difficulty values built-in, this will be used to set them per creature. (EX: a creature might have 3 different minigames, but is always Difficulty 2)
        public string artist { get; set; } //Credit for who drew the entry.
        public string rights { get; set; } //Most entries will be CC BY-SA 4.0, but may vary or require additional notes.
        public string flavorText { get; set; } //A quick detail or joke to appear when looking at the creature in the creature list scene.
        public string hintText { get; set; } //Short summary on where to find this creature.
        public bool isWild { get; set; } = true; // If false, skip this entry when determining spawn pools.
        /// <summary>
        /// If false, skip this entry when filling in lists of creatures and such. For 'secret' creature that don't show up on the list or have hints, and placeholders.
        /// </summary>
        public bool isHidden { get; set; }
        //Hidden creatures are ones you cannot get if they are not explicitly granted to you. Potentially special reward or event creatures. If you have it, you can see it in your list. If you don't, you will not.
        public long eliteId { get; set; } //if not 0, grant the ID'd creature on an active challenge being completed.
        //NOTE: some creatures may be rewards from tasks in-game by the client and will have this set to 0.
        public bool passportReward { get; set; } = false; //if true, passport mode can randomly award this as an option for completing a set.

        //Shortcut math: shift hours for a plusCode based on distance between 2nd character and F (10th character) in the character list.
        //This isn't perfectly accurate because time zones are stupid, but 20 degrees lines up close enough to an hour that this is reasonable as a shortcut.
        //Times set are UTC, and then we add 84 minutes per Cell2 you move from that line in order to accommodate the difference on the far ends.
        //NOTE: UTC +0 is 9F, UTC -1 is 9C, 97 is -5.5, roughly EST. so this math works sufficiently well. It may not line up perfectly on local time, but it's close enough.
        //NOTE 2: A creature whose spawn times cross midnight needs to have multiple entries (EX: 10pm-2am is a list of [10pm-11:59PM, 12am-2am] 
        
        public List<TimeSpawnEntry> spawnTimes { get; set; } = new List<TimeSpawnEntry>();
        public List<DateSpawnEntry> spawnDates { get; set; } = new List<DateSpawnEntry>();

        public int tierRating { get { return (int)(stats.strengthPerLevel + stats.defensePerLevel + stats.scoutingPerLevel) / 5; } } //Rough estimate of how strong a creature is. 5 stat points = 1 tier. Cost multiplier in the store, and note for dev when setting up its stats.

        //For the benefit of all players, each non-global creature should have the ability to spawn anywhere randomly. The odds do not need to be high, but should be present.
        //For rural players, it increases motivation to explore Cell8s that don't otherwise have any terrain/biomes that spawn interesting creatures,
        //and in those tiles wandering creatures are much easier to find, since they're added to a much smaller spawn pool. 
        //It also gives urban players in areas dense with terrain/biomes reasons to leave their usual routes, to look for the creatures they can't normally find.
        //MOST creatures should wander, how rare and how often they move may vary. 
        //Creatures that are globally present (ignoring date/time restrictions), locked to a specific small area (A specific landmark), or rewards/secrets should NOT wander.
        //Rough estimate: Each 52 points in wanderOdds means that creature has a 63% chance to show up in any given Cell8 maptile once a year if wanderAfterDays is 7.
        //A player walking on a lunch break can probably hit 4 or so Cell8 tiles. Putting a creature's odds over 208 means they PROBABLY won't find that creature wander in a year.
        //Most creatures should be under 200, 'rarely wanders' creatures are probably better set between 300-400, and City-specific creatures should wander much less often (~900ish), 
        public int wanderOdds = 0; //1 in X chance for any Cell8 tile to have this entry spawn there during a particular block of [wanderAfterDays] days. Based on seeded RNG for each tile.
        public long wanderSpawnEntries = 0; //How many times this creature gets added to the spawn table when it is present in a cell8. Only a wandering creature if this is over 0.
        public int wandersAfterDays = 7; //How often this creature wanders. Measured in days. 

        //FUTURE EXPANSION: Allow particularly special creatures to run more complicated spawn rules. These shouldn't be run everytime a spawn check is asked for,
        //but it should probably be more often than once at server startup?
        //public Action<bool> customSpawnRule = new Action<bool>(defaultCustomRule);
        //COULD ALSO add another condition or rule that limits spawning to a region, since currently Areas add rather than restrict. Bounding box, possibly? or a flag to switch areas? or a second set?

        public override string ToString()
        {
            return name;
        }

        public bool CanSpawnNow(DateTime adjustedDate)
        {
            TimeOnly adjustedTime = TimeOnly.FromDateTime(adjustedDate);

            return (
                (spawnTimes.Count == 0 || spawnTimes.Any(s => s.start <= adjustedTime && s.end >= adjustedTime)) &&
                (spawnDates.Count == 0 || spawnDates.Any(s => s.start.DayOfYear <= adjustedDate.DayOfYear && s.end.DayOfYear >= adjustedDate.DayOfYear))
                );
        }

        //var creatureX = new Creature()
        //{
        //    Id = 1,
        //    Name = "New",
        //    ImageName = "placeholder.png",
        //    Strength = 1,
        //    ActiveCatchType = "A",
        //    artist = "Drake Williams",
        //    flavorText = "joke",
        //    rights = "CC BY-SA 4.0 Licensed",
        //    hintText = "Common, found nearly everywhere",
        //    eliteName = "name"
        //};
        //creatureX.AreaSpawns.Add("", 20); //This adds this creature to all areas, is read once per Cell8 (added Value times)
        //creatureX.TerrainSpawns.Add("park", 2); //Slightly more common near parks and tourist/culture areas.
        //creatureX.TerrainSpawns.Add("tourism", 2);
        //l.Add(creatureX);

        //NOTE: based on Ohio data, the general expectation for balance is as follows:
        //slow-leveling creatures: 
        //water/trail/retail/park
        //medium-leveling creatures:
        //namedBuilding, natureReserve,cemetery,wetlands
        //fast-leveling creatures:
        //tourism/university/historical/beach/artsCulture.

        //rough balance expectations:
        //common wild creatures should be Tier 1 (5 points across 3 stats per level)
        //rare wild creatures should be at least Tier 2 (10 points across 3 stats), if not higher tier.
        //elite version should be 1 tier up (+5 points, keeping roughly same proportions. Good stat should stay good stat, bad stat should stay bad stat).
        //rewards for tasks and such should be at least Tier 3. (15 points across 3 stats).
        //The speed at which creatures gain levels with fragments should vary some, but not so much to make anything completely garbage. Lower tiers should level faster, but not so fast to make high tiers pointless.

        public static List<Creature> MakeCreatures()
        {
            List<Creature> l = new List<Creature>();
            //actual creature entries should have 5 points across TerrainSpawn entries. AreaSpawns are optional and usually smaller.
            var testingno = new Creature()
            {
                id = 0,
                name = "TestingNo",
                imageName = "placeholder.png",
                stats = new LevelStats() { strengthPerLevel = 1.5, defensePerLevel = 1.5, scoutingPerLevel = 2, addedPerLevel = 1, multiplierPerLevel = 2 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "A placeholder! Truly, the rarest of creatures.",
                rights = "CC0 - Public Domain",
                hintText = "You can't catch this intentionally",
                isHidden = true,
                isWild = false,
            };
            
            l.Add(testingno);

            var reel = new Creature()
            {
                id = 1,
                name = "Reel",
                imageName = "reel.png",
                stats = new LevelStats() { strengthPerLevel = 1.5, defensePerLevel = 1.5, scoutingPerLevel = 2, addedPerLevel = 1, multiplierPerLevel = 2 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "In most planes, birds aren't reels. In the ones where they are, cinema never became a popular art form.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Common, found nearly everywhere",
                eliteId = 2
            };
            reel.areaSpawns.Add("", 10); //This adds this creature to all areas, is read once per Cell8 (added Value times)
            l.Add(reel);

            var toreel = new Creature()
            {
                id = 2,
                name = "Toreel",
                imageName = "toreel.png",
                stats = new LevelStats() { strengthPerLevel = 3, defensePerLevel = 3, scoutingPerLevel = 4, addedPerLevel = 2, multiplierPerLevel = 1.5 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Movies about birds are common. Birds about movies are significantly rarer.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(toreel);

            var merman = new Creature()
            {
                id = 3,
                name = "Merman",
                imageName = "merman.png",
                stats = new LevelStats() { strengthPerLevel = 2, defensePerLevel = 1.5, scoutingPerLevel = 1.5, addedPerLevel = 1, multiplierPerLevel = 2.4 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "'Mer' is short for 'meridian', which made sense back when merpeople were only found around the equator.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "The northern coastline",
                eliteId = 4,
                wanderOdds = 300, //rarer than most, named place.
                wanderSpawnEntries = 2,
            };
            merman.placeSpawns.Add("Lake Erie", 4);
            l.Add(merman);

            var merwoman = new Creature()
            {
                id = 4,
                name = "Merwoman",
                imageName = "merwoman.png",
                stats = new LevelStats() { strengthPerLevel = 4, defensePerLevel = 3, scoutingPerLevel = 3, addedPerLevel = 1, multiplierPerLevel = 1.6 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Mer-maid is the entirely wrong term. They're not named after their careers. If they were, she'd be a Mer-ornithologist.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(merwoman);

            var swordtree = new Creature()
            {
                id = 5,
                name = "Shadetree",
                imageName = "shadetree.png",
                stats = new LevelStats() { strengthPerLevel = 1.5, defensePerLevel = 2, scoutingPerLevel = 1.5, addedPerLevel = 2, multiplierPerLevel = 1.9 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "It's an oak tree, but it's got apples? That is awfully shady. The sword doesn't help those suspicions any.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Common, found nearly everywhere",
                eliteId = 6
            };
            swordtree.areaSpawns.Add("", 4);
            swordtree.terrainSpawns.Add("nature_reserve", 3);
            l.Add(swordtree);

            var swordfall = new Creature()
            {
                id = 6,
                name = "Swordfall",
                imageName = "swordfall.png",
                stats = new LevelStats() { strengthPerLevel = 3, defensePerLevel = 4, scoutingPerLevel = 3, addedPerLevel = 2, multiplierPerLevel = 1.3 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Less commonly known as the Western Reserve Dueling Tree, woodland critters seek its boughs for safety from predators.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(swordfall);

            var armedbear = new Creature()
            {
                id = 7,
                name = "ArmedBear",
                imageName = "armedbear.png",
                stats = new LevelStats() { strengthPerLevel = 4, defensePerLevel = 4, scoutingPerLevel = 2, addedPerLevel = 3, multiplierPerLevel = 2.8 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Occasionally bears make the news for showing up in the suburbs. This bear made the news for winning a juggling contest.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Common, found nearly everywhere",
                eliteId = 8
            };
            armedbear.areaSpawns.Add("", 4);
            armedbear.terrainSpawns.Add("nature_reserve", 2);
            l.Add(armedbear);

            var leggedbear = new Creature()
            {
                id = 8,
                name = "Leggedbear",
                imageName = "leggedbear.png",
                stats = new LevelStats() { strengthPerLevel = 6, defensePerLevel = 6, scoutingPerLevel = 3, addedPerLevel = 2, multiplierPerLevel = 1.8 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "The octopus DNA is starting to come through a lot more on this one. Weird that nobody noticed it before. It's the elbows that made them bear-arms and not bear-tentacles.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(leggedbear);

            var jinky = new Creature()
            {
                id = 9,
                name = "Jinky",
                imageName = "jinky.png",
                stats = new LevelStats() { strengthPerLevel = 3, defensePerLevel = 3, scoutingPerLevel = 4, addedPerLevel = 2, multiplierPerLevel = 1.2 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "That bow and lipstick remind me of someone. Haven't seen her in a decade, used to be quite a pill-popper. Wonder what happened to her. Oh, oh no.....",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Look around cemeteries",
                eliteId = 10,
                wanderOdds = 156,
                wanderSpawnEntries = 2,
            };
            jinky.terrainSpawns.Add("cemetery", 5);
            l.Add(jinky);

            var ladyinblue = new Creature()
            {
                id = 10,
                name = "Lady In Blue",
                imageName = "ladyinblue.png",
                stats = new LevelStats() { strengthPerLevel = 5, defensePerLevel = 4, scoutingPerLevel = 6, addedPerLevel = 1, multiplierPerLevel = 1.1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Ghosts are always more afraid of you than you are of them, so it's not a terrible thing if you're very, really, extremely, totally scared of her.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(ladyinblue);

            var caladbolg = new Creature()
            {
                id = 11,
                name = "Caladbolg",
                imageName = "caladbolg.png",
                stats = new LevelStats() { strengthPerLevel = 5, defensePerLevel = 4, scoutingPerLevel = 1, addedPerLevel = 1, multiplierPerLevel = 2.6 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "This sword is so happy you remembered to add them to your team. They aren't real sure what swords are supposed to do but they're ready to learn.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Historical locations",
                eliteId = 12,
                wanderOdds = 220,
                wanderSpawnEntries = 2,
            };
            caladbolg.terrainSpawns.Add("historical", 5);
            l.Add(caladbolg);

            var agauaucuau = new Creature()
            {
                id = 12,
                name = "Agauaucuau", //rough proportions for rose gold. 75% gold, 22% copper, 3% silver
                imageName = "agauaucuau.png",
                stats = new LevelStats() { strengthPerLevel = 8, defensePerLevel = 5, scoutingPerLevel = 2, addedPerLevel = 1, multiplierPerLevel = 1.5 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "This sword learned how to knife fight from the world's self-proclaimed best chainsaw juggler. They only had 1 hand, but does that mean they're better or worse at it?",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(agauaucuau);

            var tableaux = new Creature()
            {
                id = 13,
                name = "Tableaux",
                imageName = "tableaux.png",
                stats = new LevelStats() { strengthPerLevel = 4, defensePerLevel = 5, scoutingPerLevel = 1, addedPerLevel = 2, multiplierPerLevel = 1.2 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "It was long believed that the hat was placed on this statue as a prank, and throw in the trash nightly by a custodian. It took 25 years to catch the statue walking over to the trash and taking its hat back.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Cemetaries, universities, and historical areas",
                eliteId = 14,
                wanderOdds = 123,
                wanderSpawnEntries = 2,
            };
            tableaux.terrainSpawns.Add("historical", 2);
            tableaux.terrainSpawns.Add("cemetery", 2);
            tableaux.terrainSpawns.Add("university", 1);
            l.Add(tableaux);

            var mortebleaux = new Creature()
            {
                id = 14,
                name = "Mortebleaux",
                imageName = "mortebleaux.png",
                stats = new LevelStats() { strengthPerLevel = 5, defensePerLevel = 8, scoutingPerLevel = 2, addedPerLevel = 2, multiplierPerLevel = 1.1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Statues becoming zombified makes exactly as much sense as people becoming zombified when you put the right amount of thought into the zombie-making process.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(mortebleaux);

            var boxturtle = new Creature()
            {
                id = 15,
                name = "BoxTurtle",
                imageName = "boxturtle.png",
                stats = new LevelStats() { strengthPerLevel = 1, defensePerLevel = 2, scoutingPerLevel = 2, addedPerLevel = 1, multiplierPerLevel = 1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Box turtles are quite common in some parts of the state, making homes in marshes that resemble their native territory in the Amazon.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Wetlands",
                eliteId = 16,
                wanderOdds = 142,
                wanderSpawnEntries = 2,
            };
            boxturtle.terrainSpawns.Add("wetlands", 5);
            l.Add(boxturtle);

            var octortise = new Creature()
            {
                id = 16,
                name = "Octortoise",
                imageName = "octortise.png",
                stats = new LevelStats() { strengthPerLevel = 2.4, defensePerLevel = 2.6, scoutingPerLevel = 5, addedPerLevel = 1, multiplierPerLevel = 1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Extremely acrobatic, the northern octortoise is the only reptile you can ship 'Any end up'. Appropriate postage still required.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false,
                isHidden = true
            };
            l.Add(octortise);

            var doublebat = new Creature()
            {
                id = 17,
                name = "Doublebat",
                imageName = "doublebat.png",
                stats = new LevelStats() { strengthPerLevel = 3, defensePerLevel = 2.5, scoutingPerLevel = 4.5, addedPerLevel = 1, multiplierPerLevel = 1.33 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "The wooden doublebat is the most common of its genus. Its urban counterpart, the aluminum doublebat, is nearly extinct due to overharvesting and recycling incentives.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Cincinnatti",
                eliteId = 18,
                wanderOdds = 950, //City area, should be rare.
                wanderSpawnEntries = 2,
            };
            doublebat.areaSpawns.Add("86FQ", 5);
            doublebat.areaSpawns.Add("86CQ", 5);
            doublebat.areaSpawns.Add("86CR", 5);
            doublebat.terrainSpawns.Add("cemetery", 1);
            l.Add(doublebat);

            var triplebat = new Creature()
            {
                id = 18,
                name = "Triplebat",
                imageName = "triplebat.png",
                stats = new LevelStats() { strengthPerLevel = 4, defensePerLevel = 3.5, scoutingPerLevel = 7.5, addedPerLevel = 1, multiplierPerLevel = 1.1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Someone tripled down on this pun, and we all must suffer for it.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(triplebat);

            var registarf = new Creature()
            {
                id = 19,
                name = "Registarf",
                imageName = "registarf.png",
                stats = new LevelStats() { strengthPerLevel = 1, defensePerLevel = 3, scoutingPerLevel = 1, addedPerLevel = 1, multiplierPerLevel = 1.45 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Once wizards realized money could be used for a lot of spells in place of souls, mimics started showing up in different, less lethal, more adorable forms.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Retail areas",
                eliteId = 20,
                wanderOdds = 125,
                wanderSpawnEntries = 2,
            };
            registarf.terrainSpawns.Add("retail", 5);
            l.Add(registarf);

            var registarr = new Creature()
            {
                id = 20,
                name = "Registarrr",
                imageName = "registarrr.png",
                stats = new LevelStats() { strengthPerLevel = 2, defensePerLevel = 6, scoutingPerLevel = 2, addedPerLevel = 1, multiplierPerLevel = 1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "More aggressive mimics outright turned to crime instead of passively waiting to be given the booty they sought, adapting their old attitudes and features while keeping a modern frame.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(registarr);

            var fsh = new Creature()
            {
                id = 21,
                name = "Fsh",
                imageName = "fsh.png",
                stats = new LevelStats() { strengthPerLevel = 2, defensePerLevel = 2, scoutingPerLevel = 1, addedPerLevel = 1, multiplierPerLevel = 1.33 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "What do you call a fish with no eyes? I named my 'CaveSwimmer, Herald of the Unending Dark'",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Water and Water-Adjacent areas",
                eliteId = 22,
                wanderOdds = 184,
                wanderSpawnEntries = 2,
            };
            fsh.terrainSpawns.Add("beach", 2); 
            fsh.terrainSpawns.Add("water", 3);
            l.Add(fsh);

            var fiiiiish = new Creature()
            {
                id = 22,
                name = "Fiiiiish",
                imageName = "fiiiiish.png",
                stats = new LevelStats() { strengthPerLevel = 4, defensePerLevel = 4, scoutingPerLevel = 2, addedPerLevel = 1, multiplierPerLevel = 1.1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Five eyed spy fry try and pry white lies from wise guys, told goodbye with no reply, left high and dry, unable to cry",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(fiiiiish);

            var loafer = new Creature()
            {
                id = 23,
                name = "Loafer",
                imageName = "loafer.png",
                stats = new LevelStats() { strengthPerLevel = 2, defensePerLevel = 2, scoutingPerLevel = 1, addedPerLevel = 1, multiplierPerLevel = 1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Need to defend your local wetlands? Leave it to this one, they'll take care of it. After a few naps. Eventually. Probably.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Wetlands",
                eliteId = 24,
                wanderOdds = 175,
                wanderSpawnEntries = 2,
            };
            loafer.terrainSpawns.Add("wetlands", 5);
            l.Add(loafer);

            var beever = new Creature()
            {
                id = 24,
                name = "Beever",
                imageName = "beever.png",
                stats = new LevelStats() { strengthPerLevel = 6, defensePerLevel = 2, scoutingPerLevel = 2, addedPerLevel = 1, multiplierPerLevel = 1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "The yellow and black stripes are a warning sign that this creature will sting you if you get too close, or drop a tree at you if you're not all that close.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(beever);

            var evep = new Creature()
            {
                id = 25,
                name = "Eve P.",
                imageName = "evep.png",
                stats = new LevelStats() { strengthPerLevel = 5, defensePerLevel = 4, scoutingPerLevel = 6, addedPerLevel = 2, multiplierPerLevel = 1.2 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "You can only hear this spectre sing by listening in the static between stations.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Complete a Passport set.",
                isWild = false,
                passportReward = true
            };
            l.Add(evep);

            var bumper = new Creature()
            {
                id = 26,
                name = "Bumper",
                imageName = "bumper.png",
                stats = new LevelStats() { strengthPerLevel = 3.5, defensePerLevel = 8, scoutingPerLevel = 3.5, addedPerLevel = 1, multiplierPerLevel = 1.1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "You might suspect that a life of being smacked around would be miserable and painful. Well, have a seat and get ready for some stories confirming your suspicions.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Complete a Passport set.",
                isWild = false,
                passportReward = true
            };
            l.Add(bumper);

            var glitterati = new Creature()
            {
                id = 27,
                name = "Glitterrati",
                imageName = "glitterrati.png",
                stats = new LevelStats() { strengthPerLevel = 1, defensePerLevel = 1, scoutingPerLevel = 3, addedPerLevel = 1, multiplierPerLevel = .8 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "The glitter rat has spilled their glitter! You know what that means. 6 more weeks of cleaning up glitter.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Your starter. How could you have forgotten?",
                isWild = false,
                wanderOdds = 45, //Common as a wanderer, since it doesn't show up elsewhere.
                wanderSpawnEntries = 2,
            };
            l.Add(glitterati);

            var centerguard = new Creature()
            {
                id = 28,
                name = "Center Guard",
                imageName = "centerguard.png",
                stats = new LevelStats() { strengthPerLevel = 4, defensePerLevel = 4, scoutingPerLevel = 2, addedPerLevel = 1, multiplierPerLevel = 1.4 },
                activeCatchType = "", //No elite form of this one.
                artist = "Drake Williams",
                flavorText = "What secrets does does this cardinal know? Why does it perch here? It does not share. It merely sits. If it is waiting, what for?",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Guards the center of the world.",
            };
            centerguard.areaSpawns.Add("86HX63HR", 5);
            centerguard.areaSpawns.Add("86HX63HQ", 5);
            centerguard.areaSpawns.Add("86HX63HV", 5);
            centerguard.areaSpawns.Add("86HX63JR", 5);
            centerguard.areaSpawns.Add("86HX63JQ", 5);
            centerguard.areaSpawns.Add("86HX63JV", 5);
            centerguard.areaSpawns.Add("86HX63GR", 5);
            centerguard.areaSpawns.Add("86HX63GQ", 5);
            centerguard.areaSpawns.Add("86HX63GV", 5);

            l.Add(centerguard);

            var bearocle = new Creature()
            {
                id = 29,
                name = "Bearocle",
                imageName = "bearocle.png",
                stats = new LevelStats() { strengthPerLevel = 4.5, defensePerLevel = 3.5, scoutingPerLevel = 2, addedPerLevel = 1, multiplierPerLevel = 1.5 },
                activeCatchType = "A",
                artist = "Drake Williams, referencing/remixing photo by Diginatur",
                flavorText = "Getting a suit in 'Autumn Ursine' size requires a specifically trained tailor. The monocle and hat are easy enough to find digging through the rich suburb's trash.",
                rights = "CC BY-SA 3.0 Licensed.",
                hintText = "Cleveland and the eastern border.",
                eliteId = 30,
                wanderOdds = 924, //City spawns are rare wanders.
                wanderSpawnEntries = 2,
            };
            bearocle.areaSpawns.Add("86HW", 5);
            bearocle.areaSpawns.Add("86HX", 5);
            bearocle.areaSpawns.Add("86GX", 5);
            bearocle.areaSpawns.Add("86GW", 5);
            l.Add(bearocle);

            var hiburnator = new Creature()
            {
                id = 30,
                name = "Hiburnator",
                imageName = "hiburnator.png",
                stats = new LevelStats() { strengthPerLevel = 6.5, defensePerLevel = 4.5, scoutingPerLevel = 3, addedPerLevel = 1, multiplierPerLevel = 1.3 },
                activeCatchType = "A",
                artist = "Drake Williams, referencing/remixing photo by Diginatur",
                flavorText = "All bears this size can eat 20,000 calories a day. This is the only one that can eat 20 kilograms of TNT to get those calories, let alone spit them all back out at once.",
                rights = "CC BY-SA 3.0 Licensed.",
                hintText = "Active Challenge",
                isWild = false
            };
            l.Add(hiburnator);

            var buckeye = new Creature()
            {
                id = 31,
                name = "Buckeye",
                imageName = "buckeye.png",
                stats = new LevelStats() { strengthPerLevel = 3.5, defensePerLevel = 3, scoutingPerLevel = 3.5, addedPerLevel = 1, multiplierPerLevel = 1.25 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Per interdimensional treaties and common law, buckeyes are required to be drilled into your memory near the state capital. Sorry about that, can't be helped.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "State capital",
                eliteId = 32,
                wanderOdds = 894, //City spawns are rare wanderers.
                wanderSpawnEntries = 2,
            };
            buckeye.areaSpawns.Add("86GR", 5);
            buckeye.areaSpawns.Add("86GV", 5);
            buckeye.areaSpawns.Add("86FR", 5);
            buckeye.areaSpawns.Add("86FV", 5);
            l.Add(buckeye);

            var bugeye = new Creature()
            {
                id = 32,
                name = "Bugeye",
                imageName = "bugeye.png",
                stats = new LevelStats() { strengthPerLevel = 4, defensePerLevel = 4, scoutingPerLevel = 7, addedPerLevel = 1, multiplierPerLevel = 1.1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Make sure that when you envision Ohio, that you enunciate the hard K sound in 'buckeye'. Not doing so may result in the state having the worst mascot and tree in the multiverse. No one like the Ohio this thing came from.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge.",
                isWild = false
            };
            l.Add(bugeye);

            //2 creatures were here, and have been pulled for now. 

            var cactuscat = new Creature()
            {
                id = 35,
                name = "Cactus Cat",
                imageName = "cactuscat.png",
                stats = new LevelStats() { strengthPerLevel = 6, defensePerLevel = 4, scoutingPerLevel = 5, addedPerLevel = 2, multiplierPerLevel = 1.1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Cactus Cats were a popular pet imported from their native habitat of the American Southwest. A few runaways have established a feral population in hospitable nooks and crannies of Ohio.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Beaches and Universities",
                eliteId = 36,
                wanderOdds = 300, //Significantly rarer than most wanderers.
                wanderSpawnEntries = 2,
            };
            cactuscat.terrainSpawns.Add("beach", 3);
            cactuscat.terrainSpawns.Add("university", 2);
            l.Add(cactuscat);

            var maplecat = new Creature()
            {
                id = 36,
                name = "Maple Cat",
                imageName = "maplecat.png",
                stats = new LevelStats() { strengthPerLevel = 8, defensePerLevel = 6, scoutingPerLevel = 6, addedPerLevel = 1, multiplierPerLevel = 0.9 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Instead of surviving on wild mezcal, Maple Cats have adapted to live on maple sap from trees in their new habitat. The new breed is less prickly, more sticky.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge",
                isWild = false
            };
            l.Add(maplecat);

            var gdragon = new Creature()
            {
                id = 37,
                name = "G.Dragon",
                imageName = "gdragon.png",
                stats = new LevelStats() { strengthPerLevel = 6, defensePerLevel = 2, scoutingPerLevel = 7, addedPerLevel = 2, multiplierPerLevel = 1.1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "For reasons unknown, dragons are one of the few things native to nearly every plane in existence. You're more likely to find a world where trees don't exist than one without dragons.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Places where money moves around",
                eliteId = 36,
                wanderOdds = 264,
                wanderSpawnEntries = 2,
            };
            gdragon.terrainSpawns.Add("retail", 2);
            l.Add(gdragon);

            var rdragon = new Creature()
            {
                id = 38,
                name = "R.Dragon",
                imageName = "rdragon.png",
                stats = new LevelStats() { strengthPerLevel = 7, defensePerLevel = 4, scoutingPerLevel = 9, addedPerLevel = 1, multiplierPerLevel = 0.9 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "A popular theory is that dragons are WorldWeavers who hoarded too much material wealth, and become bound to their possessions. If they've lost such power, they are to be pitied.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge",
                isWild = false
            };
            l.Add(rdragon);

            //Times are estimated to match up with daylight hours, roughly on the starting day of the sign
            var aries = new Creature()
            {
                id = 39,
                name = "Aries",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 3, 21), end = new DateOnly(2000, 4, 19) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(7, 30) }, new TimeSpawnEntry() { start = new TimeOnly(19, 30), end = new TimeOnly(23, 59) } },
                imageName = "aries.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 }, //all 12 zodiacs share stats, Tier 2 for rarity.
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "Ram fans can go ham and spam you if you call this a goat by accident.",
                hintText = "While the stars are out, around April",
            };
            aries.areaSpawns.Add("", 3);
            l.Add(aries);

            var taurus = new Creature()
            {
                id = 40,
                name = "Taurus",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 4, 20), end = new DateOnly(2000, 5, 20) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(6, 45) }, new TimeSpawnEntry() { start = new TimeOnly(20, 11), end = new TimeOnly(23, 59) } },
                imageName = "taurus.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 },
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "This sign is 100% bull. All horoscopes are, but this one more so.",
                hintText = "While the stars are out, around May",
            };
            taurus.areaSpawns.Add("", 3);
            l.Add(taurus);

            var gemini = new Creature()
            {
                id = 41,
                name = "Gemini",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 5, 21), end = new DateOnly(2000, 6, 20) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(6, 00) }, new TimeSpawnEntry() { start = new TimeOnly(20, 45), end = new TimeOnly(23, 59) } },
                imageName = "gemini.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 },
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "The original Gemini was just one guy that could change clothes really fast. It was a great party trick, and stories got exaggerated just a little more each time until he's twins.",
                hintText = "While the stars are out, around June",
            };
            gemini.areaSpawns.Add("", 3);
            l.Add(gemini);

            var cancer = new Creature()
            {
                id = 42,
                name = "Cancer",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 6, 21), end = new DateOnly(2000, 7, 22) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(5, 50) }, new TimeSpawnEntry() { start = new TimeOnly(21, 5), end = new TimeOnly(23, 59) } },
                imageName = "cancer.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 },
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "Eventually, every constellation evolves into a crab. Just like living creatures.",
                hintText = "While the stars are out, around July",
            };
            cancer.areaSpawns.Add("", 3);
            l.Add(cancer);

            var leo = new Creature()
            {
                id = 43,
                name = "Leo",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 7, 23), end = new DateOnly(2000, 8, 22) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(6, 15) }, new TimeSpawnEntry() { start = new TimeOnly(20, 50), end = new TimeOnly(23, 59) } },
                imageName = "leo.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 },
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "And now, with a lion remembered, we can release this back into the sky with a mighty roar. But next time, please remember a much prouder lion.",
                hintText = "While the stars are out, around August",
            };
            leo.areaSpawns.Add("", 3);
            l.Add(leo);

            var virgo = new Creature()
            {
                id = 44,
                name = "Virgo",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 8, 22), end = new DateOnly(2000, 9, 22) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(6, 45) }, new TimeSpawnEntry() { start = new TimeOnly(20, 15), end = new TimeOnly(23, 59) } },
                imageName = "virgo.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 },
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "Nothing about this set of stars or symbols looks anything like wheat to me. Wheat's the important part of this sign!",
                hintText = "While the stars are out, around September",
            };
            virgo.areaSpawns.Add("", 3);
            l.Add(virgo);

            var libra = new Creature()
            {
                id = 45,
                name = "Libra",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 9, 23), end = new DateOnly(2000, 10, 22) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(7, 15) }, new TimeSpawnEntry() { start = new TimeOnly(19, 15), end = new TimeOnly(23, 59) } },
                imageName = "libra.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 },
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "Libra as is beer, or Libra as in freedom? What license is this constellation assembled under?",
                hintText = "While the stars are out, around October",
            };
            libra.areaSpawns.Add("", 3);
            l.Add(libra);

            var scorpio = new Creature()
            {
                id = 46,
                name = "Scorpio",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 10, 23), end = new DateOnly(2000, 11, 21) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(7, 45) }, new TimeSpawnEntry() { start = new TimeOnly(18, 30), end = new TimeOnly(23, 59) } },
                imageName = "scorpio.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 },
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "8th entry, 8 legs, 18 stars, 0 mercy.",
                hintText = "While the stars are out, around November",
            };
            scorpio.areaSpawns.Add("", 3);
            l.Add(scorpio);

            var sagittarius = new Creature()
            {
                id = 47,
                name = "Sagittarius",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 11, 22), end = new DateOnly(2000, 12, 21) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(7, 20) }, new TimeSpawnEntry() { start = new TimeOnly(17, 0), end = new TimeOnly(23, 59) } },
                imageName = "sagittarius.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 },
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "Chiron, as illustrated in the sky here, is the only centaur capable of hitting 1,479 horsepower.",
                hintText = "While the stars are out, around December",
            };
            sagittarius.areaSpawns.Add("", 3);
            l.Add(sagittarius);

            var capricorn = new Creature()
            {
                id = 48,
                name = "Capricorn",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 12, 22), end = new DateOnly(2000, 12, 31) }, new DateSpawnEntry() { start = new DateOnly(2000, 1, 1), end = new DateOnly(2000, 1, 19) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(7, 50) }, new TimeSpawnEntry() { start = new TimeOnly(17, 0), end = new TimeOnly(23, 59) } },
                imageName = "capricorn.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 },
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "The largest goat possible to simulate in a video game. There are none bigger.",
                hintText = "While the stars are out, around January",
            };
            capricorn.areaSpawns.Add("", 3);
            l.Add(capricorn);

            var aquarius = new Creature()
            {
                id = 49,
                name = "Aquarius",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 1, 20), end = new DateOnly(2000, 2, 18) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(7, 50) }, new TimeSpawnEntry() { start = new TimeOnly(17, 30), end = new TimeOnly(23, 59) } },
                imageName = "aquarius.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 },
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "Why is the water-bearer associated with the element of air? Are you sure you remembered that right?",
                hintText = "While the stars are out, around February",
            };
            aquarius.areaSpawns.Add("", 3);
            l.Add(aquarius);

            var pisces = new Creature()
            {
                id = 50,
                name = "Pisces",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 2, 19), end = new DateOnly(2000, 3, 20) } },
                spawnTimes = new List<TimeSpawnEntry>() { new TimeSpawnEntry() { start = new TimeOnly(0, 0), end = new TimeOnly(7, 15) }, new TimeSpawnEntry() { start = new TimeOnly(18, 0), end = new TimeOnly(23, 59) } },
                imageName = "pisces.png",
                stats = new LevelStats() { strengthPerLevel = 3.2, defensePerLevel = 3.1, scoutingPerLevel = 3.7, addedPerLevel = 2, multiplierPerLevel = .9 },
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "In strong competition with Virgo for the most delicious constellation. Fish without chips is a hard sell, though.",
                hintText = "While the stars are out, around March",
            };
            pisces.areaSpawns.Add("", 3);
            l.Add(pisces);

            var ophiuchus = new Creature()
            {
                id = 51,
                name = "Ophiuchus",
                isHidden = true,
                stats = new LevelStats() { strengthPerLevel = 6.4, defensePerLevel = 6.2, scoutingPerLevel = 7.4, addedPerLevel = 1, multiplierPerLevel = .85 }, //Tier 4, requires 12 players to coordinate for each grant of this.
                imageName = "ophiuchus.png",
                artist = "Drake Williams",
                rights = "CC BY-SA 4.0 Licensed",
                flavorText = "Buy all 12 constellations, get the 13th for just one penny!",
                hintText = "Get all 12 Zodiac constellations in one Place.",
            };
            l.Add(ophiuchus);

            var frogtional = new Creature() {
                id = 52,
                name = "Frogtional",
                imageName = "frogtional.png",
                stats = new LevelStats() { strengthPerLevel = 4.5, defensePerLevel = 4.5, scoutingPerLevel = 6, addedPerLevel = 2, multiplierPerLevel = 1.2 }, //Tier3, wetlands and lower spawn rate.
                activeCatchType = "A",
                artist = "Drake Williams, remixing work by Brian Gatwicke",
                flavorText = "The smartest 387/1924th of a frog I've ever seen! They've completely numerated their denominator!",
                rights = "CC BY 2.0 Licensed",
                hintText = "Wetlands",
                eliteId = 53,
                wanderOdds = 204,
                wanderSpawnEntries = 2,
            };
            frogtional.terrainSpawns.Add("wetlands", 3);
            l.Add(frogtional);

            var spearfrog = new Creature() {
                id = 53,
                name = "Spearfrog",
                imageName = "spearfrog.png",
                stats = new LevelStats() { strengthPerLevel = 6, defensePerLevel = 7.5, scoutingPerLevel = 6.5, addedPerLevel = 1, multiplierPerLevel = 1.15 }, //Tier 4
                activeCatchType = "A",
                artist = "Drake Williams, modifying image from Tiny RPG Pack at https://ansimuz.itch.io/",
                flavorText = "The idea of frogs as princes or heroic knights is nearly always singular, whereas cities of frog-people making do with what you can make in a marsh requires a city's worth of them.",
                rights = "Permitted for use in personal/commerical projects",
                hintText = "Active Challenge",
                isWild = false,
            };
            l.Add(spearfrog);

            var crab = new Creature() {
                id = 54,
                name = "Teeny Crab",
                imageName = "teenycrab.png",
                stats = new LevelStats() { strengthPerLevel = 6.2, defensePerLevel = 4, scoutingPerLevel = 4.8, addedPerLevel = 1, multiplierPerLevel = 1.3 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "This crab is so small, it can't even hold 5 colors at the same time.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Enjoy a day at the beach",
                eliteId = 55,
                wanderOdds = 236,
                wanderSpawnEntries = 2,
            };
            crab.terrainSpawns.Add("beach", 5);
            l.Add(crab);

            var raveCrab = new Creature() {
                id = 55,
                name = "Rave Crab",
                imageName = "ravecrab.png",
                stats = new LevelStats() { strengthPerLevel = 8, defensePerLevel = 5.2, scoutingPerLevel = 5.8, addedPerLevel = 1, multiplierPerLevel = 1.0 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Glowsticks not to scale. That crab has custom ones made just for them.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge",
                isWild = false
            };
            l.Add(raveCrab);

            var wizard = new Creature() {
                id = 56,
                name = "Wizard",
                imageName = "wizard.png",
                stats = new LevelStats() { strengthPerLevel = 4, defensePerLevel = 2, scoutingPerLevel = 4, addedPerLevel = 4, multiplierPerLevel = 1.1 },
                activeCatchType = "A",
                artist = "https://ansimuz.itch.io/",
                flavorText = "I think this wizard is in the wrong game, but I'm too afraid to tell him. You tell him. It's probably fine. He probably won't obliterate you. Probably.",
                rights = "Permitted for use in personal/commerical projects",
                hintText = "Cemeteries",
                eliteId = 57,
                wanderOdds = 450, //also rarer than most.
                wanderSpawnEntries = 2,
            };
            wizard.terrainSpawns.Add("cemetery", 3); //slightly less common.
            l.Add(wizard);

            var wizchach = new Creature() {
                id = 57,
                name = "Wizchach",
                imageName = "wizchach.png",
                stats = new LevelStats() { strengthPerLevel = 5.3, defensePerLevel = 3.3, scoutingPerLevel = 6.4, addedPerLevel = 3, multiplierPerLevel = 1.0 },
                activeCatchType = "A",
                artist = "Drake Williams, modifying work from https://ansimuz.itch.io/",
                flavorText = "The Mirror Image spell makes a duplicate of the caster, and can protect them by making attackers argue if they see 2 wizards, a butterfly in a storm cloud, or seahorses kissing.",
                rights = "Permitted for use in personal/commerical projects",
                isWild = false,
                hintText = "Active Challenge",
            };
            l.Add(wizchach);

            //Halloween spawn
            var hallowiz = new Creature() {
                id = 58,
                name = "Hallowiz",
                imageName = "hallowiz.png",
                stats = new LevelStats() { strengthPerLevel = 3.5, defensePerLevel = 3.5, scoutingPerLevel = 3, addedPerLevel = 2, multiplierPerLevel = 1.2 },
                activeCatchType = "A",
                artist = "Drake Williams, modifying work from https://ansimuz.itch.io/",
                flavorText = "Prestidigitation is a great spell for palette swaps. Get yourself in the holiday spirit with a quick cantrip and a new color scheme.",
                rights = "Permitted for use in personal/commerical projects",
                hintText = "Around Halloween",
                spawnDates = new List<DateSpawnEntry>() { new DateSpawnEntry() { start = new DateOnly(2000, 10, 28), end = new DateOnly(2000, 11, 3) } }
            };
            hallowiz.areaSpawns.Add("", 3);
            l.Add(hallowiz);

            var doubalum = new Creature() {
                id = 59,
                name = "Doubalum",
                imageName = "doubalum.png",
                stats = new LevelStats() { strengthPerLevel = 3, defensePerLevel = 2.5, scoutingPerLevel = 4.5, addedPerLevel = 1, multiplierPerLevel = 1.33 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Bring Doublebat north.",
                isWild = false,
                eliteId = 60
            };
            l.Add(doubalum);

            var tripalum = new Creature() {
                id = 60,
                name = "Tripalum",
                imageName = "tripalum.png",
                stats = new LevelStats() { strengthPerLevel = 4, defensePerLevel = 3.5, scoutingPerLevel = 7.5, addedPerLevel = 1, multiplierPerLevel = 1.1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "The Aluminum Bat-Winged Fruit Bat Which Is Also A Baseball Bat is not entirely extinct, but conservation efforts will need a catchier name to succeed.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Bring Triplebat north.",
                isWild = false
            };
            l.Add(tripalum);

            //The original prototype wandering creature. MOST creatures should wander, The Fool is the creatures that shows how that works.
            var fool = new Creature() {
                id = 61,
                name = "The Fool",
                imageName = "fool.png",
                stats = new LevelStats() { strengthPerLevel = 2, defensePerLevel = 2, scoutingPerLevel = 11, addedPerLevel = 2, multiplierPerLevel = 1.4 }, //T3, they're rare and don't upgrade
                activeCatchType = "",
                artist = "Drake Williams, modifying original Rider-Waite illustration",
                flavorText = "The Fool is often seen standing on the edge of a cliff, unaware of the disaster that lies before him. He must never actually fall, or else he becomes The Splat.",
                rights = "Public Domain",
                hintText = "Wanders twice a week. Easier in rural areas",
                isWild = false,
                isHidden = false,
                wanderOdds = 26, //Extremely common, intentionally.
                wanderSpawnEntries = 1, // in rural areas, 1/19 (10+4+4+1) or ~5% odds of spawning when allowed to. Significantly less common in tiles with other spawns.
                wandersAfterDays = 3, //moves more often than most.
                eliteId = 0
            };
            l.Add(fool);

            var slime = new Creature() {
                id = 62,
                name = "Slime",
                imageName = "slime.png",
                stats = new LevelStats() { strengthPerLevel = 3, defensePerLevel = 4, scoutingPerLevel = 3, addedPerLevel = 3, multiplierPerLevel = 1.4 }, //Tier 2
                activeCatchType = "A",
                artist = "https://ansimuz.itch.io/",
                flavorText = "Eyes are physiologically part of the brain. These slimes may be the most intelligent creature you'll find here.",
                rights = "Permitted for use in personal/commerical projects",
                hintText = "Main Streets",
                eliteId = 63,
            };
            slime.placeSpawns.Add("Main Street", 5); //Most common named entry in Ohio.
            slime.placeSpawns.Add("North Main Street", 5); //and the variants are all also in the top 10.
            slime.placeSpawns.Add("South Main Street", 5);
            slime.placeSpawns.Add("East Main Street", 5);
            slime.placeSpawns.Add("West Main Street", 5);
            l.Add(slime);

            var slimeye = new Creature() {
                id = 63,
                name = "Slimeye",
                imageName = "slimeye.png",
                stats = new LevelStats() { strengthPerLevel = 4, defensePerLevel = 7, scoutingPerLevel = 4, addedPerLevel = 2, multiplierPerLevel = 1.2 },
                activeCatchType = "",
                artist = "https://ansimuz.itch.io/",
                flavorText = "Without a stable, stationary eye, the extra eyes fail to provide any additional depth perception over a single eye. Not so smart now, are you slime?",
                rights = "Permitted for use in personal/commerical projects",
                hintText = "Active Challenge",
                isWild = false,
                eliteId = 0
            };
            l.Add(slimeye);

            var mishipeshu = new Creature() {
                id = 64,
                name = "Mishipeshu",
                imageName = "mishipeshu.png",
                stats = new LevelStats() { strengthPerLevel = 7, defensePerLevel = 4, scoutingPerLevel = 9, addedPerLevel = 1, multiplierPerLevel = 0.9 },
                activeCatchType = "A",
                artist = "Drake Williams, remixing work by davidraju",
                flavorText = "Water panthers are best known for living in the Great Lakes, but have been moving south into smaller rivers and ponds because of waterfront housing prices.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Rare water spawn",
                eliteId = 65,
                wanderOdds = 280, //rarer than most wanderers.
                wanderSpawnEntries = 2,
            };
            mishipeshu.terrainSpawns.Add("water", 1);
            l.Add(mishipeshu);

            var weshipeshu = new Creature() {
                id = 65,
                name = "We-shipeshu", //WE-shipeshu, a small group of them. Or a cerebus-like one with multiple heads.
                imageName = "weshipeshu.png",
                stats = new LevelStats() { strengthPerLevel = 7.5, defensePerLevel = 7.5, scoutingPerLevel = 10, addedPerLevel = 0, multiplierPerLevel = 0.89 },
                activeCatchType = "A",
                artist = "Drake Williams, remixing work by davidraju",
                flavorText = "Unlike 3-headed hellhounds, 3-headed water panthers aren't good for guarding much of anything. They just ignore you 3 times harder. Big cats are still cats.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Active Challenge",
                isWild = false,
            };
            l.Add(weshipeshu);
            //Post-processing: As a safety check, remove creatures that we know will not spawn in-game, in case we haven't moved them
            var posibleOobCreatures = l.Where(entry => (entry.areaSpawns.Count > 0 && entry.areaSpawns.All(a => a.Key != "") && entry.placeSpawns.Count == 0 && entry.specificSpawns.Count == 0)).ToList();
            foreach (var c in posibleOobCreatures)
            {
                var areas = c.areaSpawns.Select(s => s.Key.ToPolygon()).ToList();
                if (!areas.Any(a => CreatureCollectorGlobals.playBoundary.Intersects(a)))
                    l.Remove(c);
            }

            return l;
        }

        public bool SpawnWander(string plusCode, DateTime dateTime) {
            var dateFactor = dateTime.Ticks / TimeSpan.TicksPerDay; //A more granular baseline to allow creatures to move at different rates. Day is the minimum. Divide by range to get usable block. in the 780,000 range now for days, plenty safe.
            var moveFactor = (dateFactor / wandersAfterDays);

            //This is the best seed option. It's consistent for that value of time, and is sufficiently distinct across all inputs to avoid too much clustering.
            var seededRNG = new Random(plusCode.GetDeterministicHashCode() + (int)moveFactor + (int)id);
            var randomNumber = seededRNG.Next(wanderOdds);
            var allowNow = randomNumber <= 1;
            return allowNow;
        }
    }

    public class CreatureInstance //the data spawns on the map.
    {
        public long id { get; set; }
        public Guid uid { get; set; }
        public string name { get; set; }
        public string activeGame { get; set; } //which active challenge to use for this creature.
        public int difficulty { get; set; }
    }

    public class LevelStats 
    {
        //Base value, for determining a creature's stats based on its level.
        public double multiplierPerLevel { get; set; } = 2.1; //How many of these do I need to catch to gain the next level (level * this value)
        public int addedPerLevel { get; set; } = 0; //After multiplying multiplierPerLevel * level, add this number * level to that total.
        public double strengthPerLevel { get; set; } = 1;
        public double scoutingPerLevel { get; set; } = 1;
        public double defensePerLevel { get; set; } = 1;
    }
}
