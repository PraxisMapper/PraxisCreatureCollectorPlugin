using PraxisCore;

namespace PraxisCreatureCollectorPlugin {
    public class Config {
        public long CreaturesPerCell8 { get; set; }
        public long MinWalkableSpacesOnSpawn { get; set; }
        public long MinOtherSpacesOnSpawn { get; set; }
        public long CreatureCountToRespawn { get; set; }
        public int CreatureDurationMin { get; set; }
        public int CreatureDurationMax { get; set; }
        public List<string> placeIncludes { get; set; } = new List<string>();
        public List<string> placeExcludes { get; set; } = new List<string>();
        public bool nestsEnabled { get; set; } = true;
        public bool NPCsEnabled { get; set; } = true;
        public int CoinGrantLockoutSeconds { get; set; } = 2; //2 seconds should allow bikers to get 50%+ credit, car drives <25% credit.

        public void LoadFromDatabase() {
            var db = new PraxisContext();

            var c1 = db.GlobalData.Where(g => g.DataKey == "CreaturesPerCell8").FirstOrDefault();
            if (c1 == null) {
                c1 = new DbTables.GlobalData() { DataKey = "CreaturesPerCell8", DataValue = CreaturesPerCell8.ToString().ToByteArrayUTF8() };
                db.GlobalData.Add(c1);
            }
            CreaturesPerCell8 = c1.DataValue.ToUTF8String().ToLong();

            var c2 = db.GlobalData.Where(g => g.DataKey == "MinWalkableSpacesOnSpawn").FirstOrDefault();
            if (c2 == null) {
                c2 = new DbTables.GlobalData() { DataKey = "MinWalkableSpacesOnSpawn", DataValue = MinWalkableSpacesOnSpawn.ToString().ToByteArrayUTF8() };
                db.GlobalData.Add(c2);
            }
            MinWalkableSpacesOnSpawn = c2.DataValue.ToUTF8String().ToLong();

            var c3 = db.GlobalData.Where(g => g.DataKey == "MinOtherSpacesOnSpawn").FirstOrDefault();
            if (c3 == null) {
                c3 = new DbTables.GlobalData() { DataKey = "MinOtherSpacesOnSpawn", DataValue = MinOtherSpacesOnSpawn.ToString().ToByteArrayUTF8() };
                db.GlobalData.Add(c3);
            }
            MinOtherSpacesOnSpawn = c3.DataValue.ToUTF8String().ToLong();

            var c4 = db.GlobalData.Where(g => g.DataKey == "CreatureCountToRespawn").FirstOrDefault();
            if (c4 == null) {
                c4 = new DbTables.GlobalData() { DataKey = "CreatureCountToRespawn", DataValue = CreatureCountToRespawn.ToString().ToByteArrayUTF8() };
                db.GlobalData.Add(c4);
            }
            CreatureCountToRespawn = c4.DataValue.ToUTF8String().ToLong();

            var c5 = db.GlobalData.Where(g => g.DataKey == "CreatureDurationMin").FirstOrDefault();
            if (c5 == null) {
                c5 = new DbTables.GlobalData() { DataKey = "CreatureDurationMin", DataValue = CreatureDurationMin.ToString().ToByteArrayUTF8() };
                db.GlobalData.Add(c5);
            }
            CreatureDurationMin = c5.DataValue.ToUTF8String().ToInt();

            var c6 = db.GlobalData.Where(g => g.DataKey == "CreatureDurationMax").FirstOrDefault();
            if (c6 == null) {
                c6 = new DbTables.GlobalData() { DataKey = "CreatureDurationMax", DataValue = CreatureDurationMax.ToString().ToByteArrayUTF8() };
                db.GlobalData.Add(c6);
            }
            CreatureDurationMax = c6.DataValue.ToUTF8String().ToInt();

            var c7 = db.GlobalData.Where(g => g.DataKey == "placeIncludes").FirstOrDefault();
            if (c7 == null) {
                c7 = new DbTables.GlobalData() { DataKey = "placeIncludes", DataValue = placeIncludes.ToJsonByteArray() };
                db.GlobalData.Add(c7);
            }
            placeIncludes = c7.DataValue.FromJsonBytesTo<List<string>>();

            var c8 = db.GlobalData.Where(g => g.DataKey == "placeExcludes").FirstOrDefault();
            if (c8 == null) {
                c8 = new DbTables.GlobalData() { DataKey = "placeExcludes", DataValue = placeExcludes.ToJsonByteArray() };
                db.GlobalData.Add(c8);
            }
            placeExcludes = c8.DataValue.FromJsonBytesTo<List<string>>();

            var c9 = db.GlobalData.Where(g => g.DataKey == "nestsEnabled").FirstOrDefault();
            if (c9 == null) {
                c9 = new DbTables.GlobalData() { DataKey = "nestsEnabled", DataValue = nestsEnabled.ToJsonByteArray() };
                db.GlobalData.Add(c9);
            }
            nestsEnabled = c9.DataValue.FromJsonBytesTo<bool>();

            var c10 = db.GlobalData.Where(g => g.DataKey == "NPCsEnabled").FirstOrDefault();
            if (c10 == null)
            {
                c10 = new DbTables.GlobalData() { DataKey = "NPCsEnabled", DataValue = NPCsEnabled.ToJsonByteArray() };
                db.GlobalData.Add(c10);
            }
            NPCsEnabled = c10.DataValue.FromJsonBytesTo<bool>();

            var c11 = db.GlobalData.Where(g => g.DataKey == "CoinGrantLockoutSeconds").FirstOrDefault();
            if (c11 == null)
            {
                c11 = new DbTables.GlobalData() { DataKey = "CoinGrantLockoutSeconds", DataValue = CoinGrantLockoutSeconds.ToJsonByteArray() };
                db.GlobalData.Add(c11);
            }
            CoinGrantLockoutSeconds = c11.DataValue.FromJsonBytesTo<int>();

            db.SaveChanges();
        }

    }
}
