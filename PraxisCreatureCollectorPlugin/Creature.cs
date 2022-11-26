using PraxisCore;

namespace PraxisCreatureCollectorPlugin
{
    //NOTE:
    // Terrain/area/place spawns all apply. A creature can spawn at Parks, 82GM0000+00, and 'Pine Street' without interference.
    // spawn times and seasons apply to all of the above, and to each other. A creature could spawn by the above rules for 3 hours on March 29th.

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
        public bool isHidden { get; set; } // If false, skip this entry when filling in lists of creatures and such. For 'secret' creature that don't show up on the list or have hints, and placeholders.
        //Hidden creatures are ones you cannot get if they are not explicitly granted to you. Potentially special reward or event creatures. If you have it, you can see it in your list. If you don't, you will not.
        public long eliteId { get; set; } //if not 0, grant the ID'd creature on an active challenge being completed.
        //NOTE: some creatures may be rewards from tasks in-game by the client and will have this set to 0.
        public bool passportReward { get; set; } = false; //if true, passport mode can randomly award this as an option for completing a set.

        //Shortcut math: shift hours for a plusCode based on distance between 2nd character and F (10th character) in the character list.
        //This isn't perfectly accurate because time zones are stupid, but 20 degrees lines up close enough to an hour that this is reasonable as a shortcut.
        //NOTE: UTC +0 is 9F, UTC -1 is 9C, 97 is -4, roughly EST. so this math works sufficiently well, in that its max shift is -9/+8 instead of -12/+12
        //So a creature that spawns from 0:00 to 1:00 UTC will spawn at 8:00-9:00 UTC [20:00-21:00 local time] in the easternmost part of the map, 
        //and 13:00-14:00 UTC [1:00-2:00 local time] on the westernmost part. it's still "late night" in both cases even though it's not exact.
        //I decided to shift the time by ~24 minutes per Cell2 if you wanted to accomodate for the creep, or by ~1 minute per Cell4 to be extremely precise.
        //NOTE 2: A creature whose spawn times cross midnight needs to have multiple entries (EX: 10pm-2am is a list of [10pm-11:59PM, 12am-2am] 
        
        public List<TimeSpawnEntry> spawnTimes { get; set; } = new List<TimeSpawnEntry>();
        public List<DateSpawnEntry> spawnDates { get; set; } = new List<DateSpawnEntry>();

        public int tierRating { get { return (int)(stats.strengthPerLevel + stats.defensePerLevel + stats.scoutingPerLevel) / 5; } } //Rough estimate of how strong a creature is. 5 stat points = 1 tier. Cost multiplier in the store, and note for dev when setting up its stats.

        public int wanderOdds = 0; //1 in X chance for any Cell8 tile to have this entry spawn there this week. Based on seeded RNG for each tile.
        public long wanderSpawnEntries = 0; //How many times this creature gets added to the spawn table when it is present in a cell8.

        //FUTURE EXPANSION: Allow particularly special creatures to run more complicated spawn rules. These shouldn't be run everytime a spawn check is asked for,
        //but it should probably be more often than once at server startup?
        //public Action<bool> customSpawnRule = new Action<bool>(defaultCustomRule);

        public override string ToString()
        {
            return name;
        }

        public bool CanSpawnNow(DateTime adjustedDate)
        {
            TimeOnly adjustedTime = TimeOnly.FromDateTime(adjustedDate);

            return (spawnTimes.Count == 0 || spawnTimes.Any(s => s.start <= adjustedTime && s.end >= adjustedTime) && 
                (spawnDates.Count == 0 || spawnDates.Any(s => s.start.DayOfYear <= adjustedDate.DayOfYear && s.end.DayOfYear >= adjustedDate.DayOfYear)));
                
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
            reel.areaSpawns.Add("", 20); //This adds this creature to all areas, is read once per Cell8 (added Value times)
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
                eliteId = 4
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
                eliteId = 10
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
                eliteId = 12
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
                eliteId = 14
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
                stats = new LevelStats() { strengthPerLevel = 1, defensePerLevel =2, scoutingPerLevel = 2,  addedPerLevel = 1, multiplierPerLevel = 1 },
                activeCatchType = "A",
                artist = "Drake Williams",
                flavorText = "Box turtles are quite common in some parts of the state, making homes in marshes that resemble their native territory in the Amazon.",
                rights = "CC BY-SA 4.0 Licensed",
                hintText = "Wetlands",
                eliteId = 16,
            };
            boxturtle.terrainSpawns.Add("wetlands", 5);
            l.Add(boxturtle);

            var octortise = new Creature()
            {
                id = 16,
                name = "Octortoise",
                imageName = "octortise.png",
                stats = new LevelStats() { strengthPerLevel = 2.4, defensePerLevel = 2.6, scoutingPerLevel = 5,  addedPerLevel = 1, multiplierPerLevel = 1 },
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
                hintText = "Cemeteries or Cincinnatti",
                eliteId = 18
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
                eliteId = 20
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
                eliteId = 22
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
                eliteId = 24
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
                isWild = false
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
                eliteId = 30
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
                eliteId = 32
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
                hintText = "Nature Reserves",
                eliteId = 36
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

            //Post-processing: As a safety check, remove creatures that we know will not spawn in-game, in case we haven't moved them
            var posibleOobCreatures = l.Where(entry => (entry.areaSpawns.Count > 0 && entry.areaSpawns.All(a => a.Key != "") && entry.placeSpawns.Count == 0 && entry.specificSpawns.Count == 0)).ToList();
            foreach (var c in posibleOobCreatures)
            {
                var areas = c.areaSpawns.Select(s => s.Key.ToPolygon()).ToList();
                if (!areas.Any(a => CreatureCollectorGlobals.playBoundary.ElementGeometry.Intersects(a)))
                    l.Remove(c);
            }

            return l;
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
