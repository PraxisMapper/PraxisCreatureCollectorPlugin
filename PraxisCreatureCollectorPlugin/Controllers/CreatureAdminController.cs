using Microsoft.AspNetCore.Mvc;
using PraxisCore;
using PraxisCore.Support;
using System.Globalization;

namespace PraxisCreatureCollectorPlugin.Controllers
{
    public class CreatureAdminController : Controller
    {
        //For viewing and setting admin data for this game.
        //Can force-change some values and do some quick tasks here instead of DB access
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [Route("/Slippy/Tibo")]
        [Route("/Tibo/Slippy")]
        public IActionResult Slippy()
        {
            return View("Slippy");
        }

        [HttpGet]
        [Route("/{controller}/TileInfo/{plusCode8}")]
        public ActionResult TileInfo(string plusCode8)
        {
            ViewBag.plusCode = plusCode8;
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var istats = new ImageStats(plusCode8);
            var places = Place.GetPlaces(istats.area);
            ViewBag.placeCount = places.Count;
            var tile = MapTileSupport.MapTiles.DrawAreaAtSize(istats, places, "mapTiles", false);
            ViewBag.imageString = "data:image/png;base64," + Convert.ToBase64String(tile);
            ViewBag.timeToDraw = sw.Elapsed;

            sw.Restart();
            var spawnTable = CommonHelpers.GenerateSpawnTable(plusCode8, out var terrainInfo);
            ViewBag.spawnGenTime = sw.Elapsed;

            List<string> spawnEntries = new List<string>();
            var spawnGrouped = spawnTable.GroupBy(s => s.name);
            foreach (var sg in spawnGrouped.OrderByDescending(sg => sg.Count()))
            {
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

            string wanderers = "";
            var weekOfYear = ISOWeek.GetWeekOfYear(DateTime.UtcNow); //results range from 1-53

            //here, SeededRNG is ready to report on wandering creatures, but only for the current week, so I need to reset it to this state for each week per creature.
            foreach (var w in CreatureCollectorGlobals.wanderingCreatures)
                for (int i = 1; i <= 53; i++)
                {
                    var seeded2 = plusCode8.GetSeededRandom();
                    var ignore = seeded2.Next(26);
                    ignore = seeded2.Next(CreatureCollectorGlobals.gameplayAreas.Count);
                    ignore = seeded2.Next(25, 225);

                    //NOW i can reliably check.
                    if (seeded2.Next(i * w.wanderOdds) <= i - 1)
                    {
                        wanderers += w.name + " appears on week " + i + "<br />";
                    }
                }

            ViewBag.wanderers = wanderers;
            return View();
        }
    }
}
