using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PraxisCore;
using PraxisCore.Support;
using PraxisMapper.Classes;
using System.Globalization;

namespace PraxisCreatureCollectorPlugin.Controllers {
    public class CreatureAdminController : Controller {
        //For viewing and setting admin data for this game.
        //Can force-change some values and do some quick tasks here instead of DB access

        string accountId, password;
        public override void OnActionExecuting(ActionExecutingContext context) {
            base.OnActionExecuting(context);
            PraxisAuthentication.GetAuthInfo(Response, out accountId, out password);
        }

        public IActionResult Index() {
            return View();
        }

        [HttpGet]
        [Route("/Slippy/Tibo")]
        [Route("/Tibo/Slippy")]
        public IActionResult Slippy() {
            return View("Slippy");
        }

        [HttpGet]
        [Route("/{controller}/TileInfo/{plusCode8}")]
        public ActionResult TileInfo(string plusCode8) {
            ViewBag.plusCode = plusCode8;
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var istats = new ImageStats(plusCode8);
            var places = Place.GetPlaces(istats.area);
            ViewBag.placeCount = places.Count;
            var tile = MapTileSupport.MapTiles.DrawAreaAtSize(istats, places, "mapTiles");
            ViewBag.imageString = "data:image/png;base64," + Convert.ToBase64String(tile);
            ViewBag.timeToDraw = sw.Elapsed;

            sw.Restart();
            var spawnTable = CommonHelpers.GenerateSpawnTable(plusCode8, out var terrainInfo);
            ViewBag.spawnGenTime = sw.Elapsed;

            List<string> spawnEntries = new List<string>();
            var spawnGrouped = spawnTable.GroupBy(s => s.name);
            foreach (var sg in spawnGrouped.OrderByDescending(sg => sg.Count())) {
                spawnEntries.Add(sg.Key + ": " + sg.Count() + " [" + Math.Round(((sg.Count() * 1.0d) / spawnTable.Count) * 100, 2) + "%]");
            }
            ViewBag.spawnTable = spawnEntries;

            ViewBag.seed = plusCode8.GetDeterministicHashCode();
            var seededRng = plusCode8.GetSeededRandom();
            var weekOfYearNesting = seededRng.Next(26);
            var nestType = seededRng.Next(CreatureCollectorGlobals.gameplayAreas.Count);
            ViewBag.weeksNestActive = weekOfYearNesting + ", " + (weekOfYearNesting + 26);
            ViewBag.nestType = CreatureCollectorGlobals.gameplayAreas[nestType];
            ViewBag.nestSize = seededRng.Next(25, 225);

            ViewBag.wanderingInfo = NextWandering(plusCode8);

            return View();
        }

        public string NextWandering(string plusCode) {
            string results = "";

            foreach (var c in CreatureCollectorGlobals.creatureList.Where(c => c.wanderOdds > 0)) {
                var nextWanderDate = DateTime.MinValue;
                for (int i = 0; i < 100000; i++) {
                    DateTime testDate = DateTime.UtcNow.AddDays(i * c.wandersAfterDays);
                    
                    if (c.SpawnWander(plusCode, testDate)) {
                        results += c.name + ": " + testDate.ToShortDateString() + "<br />";
                        nextWanderDate = testDate;
                        break;
                    }
                }
                if (nextWanderDate == DateTime.MinValue)
                    results += c.name + ": Does not wander here.<br />";
            }

            return results;
        }
    }
}
