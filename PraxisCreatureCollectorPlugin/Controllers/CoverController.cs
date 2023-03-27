using Google.OpenLocationCode;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NetTopologySuite.Geometries;
using PraxisCore;
using PraxisCore.Support;
using PraxisMapper.Classes;
using System.Text.Json;
using static PraxisCore.DbTables;

namespace PraxisCreatureCollectorPlugin.Controllers {
    public class CoverController : Controller {
        //COVER is the name for the mode where you place creature fragments to generate coverage areas.
        //They don't overlap, but you get points for how much total unique area is covered.
        //PVP version is Compete mode to stick to alliterative entries.
        private readonly IConfiguration Configuration;

        string accountId, password;
        public override void OnActionExecuting(ActionExecutingContext context) {
            base.OnActionExecuting(context);
            PraxisAuthentication.GetAuthInfo(Response, out accountId, out password);
        }

        public CoverController(IConfiguration config) {
            Configuration = config;
        }

        [HttpGet]
        [Route("/[controller]/Leaderboards")]
        public string PlaceCreaturesLeaderboards() {
            //Player's score for this mode (placing creatures to make circles PVE) is not saved as secure, since it doesn't tell the owner anything about location.
            //NOTE: 788,122,959 is the value required to entirely cover Ohio, including the parts of Lake Erie inside the border.
            //TODO: cache this, it could be heavy-duty to read and convert all scores every time someone checks.
            var data = GenericData.GetAllPlayerDataByKey("coverScore")
                .Select(k => new { k.accountId, score = k.DataValue.ToUTF8String().ToLong() })
                .OrderByDescending(k => k.score)
                .Take(25);
            return JsonSerializer.Serialize(data);
        }

        [HttpGet]
        [Route("/[controller]/Placed/{pluscode10}")]
        public CoverModeEntry GetPlacedCreature(string pluscode10) {
            Response.Headers.Add("X-noPerfTrack", "Cover/Placed/VARSREMOVED");

            var response = GenericData.GetSecurePlayerData<Dictionary<string, CoverModeEntry>>(accountId, "placedCreatures", password);
            if (response == null)
                return new CoverModeEntry();

            var tappedArea = Singletons.preparedGeometryFactory.Create(pluscode10.ToPolygon());
            CoverModeEntry results = null;
            Geometry resultsSize = null;
            foreach (var pcm in response.Values) {
                var thisArea = pcm.locationCell10.ToGeoArea();
                var thisCreatureGeo = new Point(thisArea.CenterLongitude, thisArea.CenterLatitude).Buffer(pcm.scouting * ConstantValues.resolutionCell10);
                if (tappedArea.Intersects(thisCreatureGeo)) {
                    if (resultsSize == null || thisCreatureGeo.Area < resultsSize.Area) {
                        resultsSize = thisCreatureGeo;
                        results = pcm;
                    }
                }
            }

            return results;
        }

        [HttpGet]
        [Route("/[controller]/Placed/")]
        public Dictionary<string, CoverModeEntry> GetPlacedCreatureList() {
            //TODO: this might not be used, remove it? Or save this for future use?
            var response = GenericData.GetSecurePlayerData<Dictionary<string, CoverModeEntry>>(accountId, "placedCreatures", password);
            return response;
        }

        [HttpPut]
        [Route("/[controller]/Placed/{plusCode10}/{creatureId}/{fragmentsUsed}")]
        public long UpdatePlacedCreature(string plusCode10, long creatureId, long fragmentsUsed) //FragmentsUsed is new total, not change.
        {
            long returnValue = 0; //Returns how many fragments were placed. May be negative if new fragmentsUsed value is lower than current value.
            Response.Headers.Add("X-noPerfTrack", "Cover/Placed/VARSREMOVED");
            if (!DataCheck.IsInBounds(plusCode10))
                return returnValue;

            SimpleLockable.PerformWithLock(accountId, () => {
                var creatureData = GenericData.GetSecurePlayerData<Dictionary<long, PlayerCreatureInfo>>(accountId, "creatureInfo", password);
                var thisCreature = creatureData[creatureId];
                var placedDict = GenericData.GetSecurePlayerData<Dictionary<string, CoverModeEntry>>(accountId, "placedCreatures", password);
                if (placedDict == null)
                    placedDict = new Dictionary<string, CoverModeEntry>();

                var area = OpenLocationCode.DecodeValid(plusCode10);
                var point = new Point(area.CenterLongitude, area.CenterLatitude);

                placedDict.TryGetValue(plusCode10, out var creature);
                if (creature == null) {
                    creature = new CoverModeEntry();
                    placedDict.Add(plusCode10, creature);
                    returnValue = fragmentsUsed;
                }
                else
                    returnValue = fragmentsUsed - creature.creatureFragmentCount;

                if (fragmentsUsed == 0)
                    placedDict.Remove(plusCode10);

                thisCreature.currentAvailable += creature.creatureFragmentCount; //return any existing fragments to our total.
                fragmentsUsed = Math.Min(thisCreature.currentAvailable, fragmentsUsed); // can't use more fragments than you've caught total for this creature.
                thisCreature.currentAvailable -= fragmentsUsed;
                creature.locationCell10 = plusCode10;
                creature.creatureId = creatureId;
                creature.creatureFragmentCount = fragmentsUsed;

                PlayerCreatureInfo ci = new PlayerCreatureInfo() { id = creatureId };
                ci.FastBoost(creature.creatureFragmentCount); //get new score
                creature.scouting = ci.scouting;

                var allGeoArea = placedDict.Values.Select(p => p.locationCell10.ToGeoArea().ToPoint().Buffer(p.scouting * ConstantValues.resolutionCell10)).ToList();
                var mergedGeo = NetTopologySuite.Operation.Union.CascadedPolygonUnion.Union(allGeoArea); //This should be the optimal call for this logic.

                long playerScore = (long)(mergedGeo.Area / ConstantValues.squareCell10Area);
                GenericData.SetPlayerData(accountId, "coverScore", playerScore.ToString().ToByteArrayUTF8());
                GenericData.SetSecurePlayerDataJson(accountId, "placedCreatures", placedDict, password);
                GenericData.SetSecurePlayerDataJson(accountId, "creatureInfo", creatureData, password);
            });
            return returnValue;
        }

        [HttpGet]
        [Route("/MapTile/PlacedOverlay/{plusCode}")]
        [Route("/[controller]/PlacedOverlay/{plusCode}")]
        public ActionResult DrawPlacedCreatureMapOverlayTile(string plusCode) {
            Response.Headers.Add("X-noPerfTrack", "Cover/PlacedOverlay/VARSREMOVED");
            if (!DataCheck.IsInBounds(plusCode)) {
                Response.Headers.Add("X-notes", "OOB");
                return StatusCode(500);
            }
            var geoArea = plusCode.ToPolygon();

            //get placed creatures.
            var creatures = GenericData.GetSecurePlayerData<Dictionary<string, CoverModeEntry>>(accountId, "placedCreatures", password);
            if (creatures == null)
                creatures = new Dictionary<string, CoverModeEntry>();

            List<DbTables.Place> mapItems = new List<DbTables.Place>();
            foreach (var c in creatures) {
                //We will use the CENTER for drawing these, since that looks better on maps and is what everyone expects to see.
                var radius = c.Value.scouting;
                var point = c.Value.locationCell10.ToGeoArea().ToPoint();
                //NOTE: I think it might be faster to make squares that have the same radius, and check those for intersection, THEN buffer the ones that intersect the area.
                //Buffer creates 33 point circle, but making 5 points for a closed polygon is almost certainly faster than 33 in the buffer.
                var intersectCheck = new Polygon(new LinearRing(new Coordinate[] {
                    new Coordinate(point.X - radius, point.Y - radius),
                    new Coordinate(point.X - radius, point.Y + radius),
                    new Coordinate(point.X + radius, point.Y + radius),
                    new Coordinate(point.X + radius, point.Y - radius),
                    new Coordinate(point.X - radius, point.Y - radius)})
                );
                if (intersectCheck.Intersects(geoArea)) {
                    ICollection<PlaceTags> tags = new List<PlaceTags>() {
                        new PlaceTags() { Key = "generated", Value = "true" },
                        new PlaceTags() { Key = "creatureId", Value = c.Value.creatureId.ToString() },
                    };
                    var drawnGeo = point.Buffer(c.Value.scouting * ConstantValues.resolutionCell10);
                    var place = new DbTables.Place() { Tags = tags, ElementGeometry = drawnGeo}; 
                    mapItems.Add(place);
                }
            }
            TagParser.ApplyTags(mapItems, "Cover");

            //draw map tile here. These don't get cached on the server, so the client should wipe any images when a creature is placed in Cover mode and ask to redraw them.
            ImageStats stats = new ImageStats(plusCode);

            var mapTile = MapTileSupport.MapTiles.DrawAreaAtSize(stats, mapItems, "Cover");

            return File(mapTile, "image/png");
        }

        [HttpGet]
        [Route("/MapTile/PlacedFull/")]
        [Route("/[controller]/PlacedFull/")]
        public ActionResult DrawPlacedCreatureFullMap() {
            //This one is for drawing an image of your whole range of placed creatures
            Response.Headers.Add("X-noPerfTrack", "Cover/PlacedFull/VARSREMOVED");

            //get placed creatures.
            var creatures = GenericData.GetSecurePlayerData<Dictionary<string, CoverModeEntry>>(accountId, "placedCreatures", password);
            if (creatures == null)
                creatures = new Dictionary<string, CoverModeEntry>();

            List<DbTables.Place> mapItems = new List<DbTables.Place>();

            foreach (var c in creatures) {
                if (c.Value.scouting == 0) //Shouldn't happen. May be a testing thing on my debug account. TODO remove
                    continue;

                //We will use the CENTER for drawing these, since that looks better on maps and is what everyone expects to see.
                var point = c.Value.locationCell10.ToGeoArea().ToPoint();
                ICollection<PlaceTags> tags = new List<PlaceTags>() {
                        new PlaceTags() { Key = "generated", Value = "true" },
                        new PlaceTags() { Key = "creatureId", Value = c.Value.creatureId.ToString() },
                    };
                var drawnGeo = point.Buffer(c.Value.scouting * .000125);
                var place = new DbTables.Place() { Tags = tags, ElementGeometry = drawnGeo, StyleName = c.Value.creatureId.ToString() };
                place.DrawSizeHint = GeometrySupport.CalculateDrawSizeHint(place);
                mapItems.Add(place);
            }

            if (mapItems.Count == 0)
                return StatusCode(500);

            var northExtent = mapItems.Max(m => m.ElementGeometry.EnvelopeInternal.MaxY);
            var southExtent = mapItems.Min(m => m.ElementGeometry.EnvelopeInternal.MinY);
            var westExtent = mapItems.Min(m => m.ElementGeometry.EnvelopeInternal.MinX);
            var eastExtent = mapItems.Max(m => m.ElementGeometry.EnvelopeInternal.MaxX);

            //add some white space to the sides of the image
            var NSbuffer = (northExtent - southExtent) * .05;
            var EWbuffer = (eastExtent - westExtent) * .05;

            //Odds are good a player has entries across the whole state, so draw the play boundaries to provide some context to the player.
            mapItems.Insert(0, new DbTables.Place() { ElementGeometry = CreatureCollectorGlobals.playBoundary,  StyleName = "borders" });

            GeoArea fullDrawing = new GeoArea(southExtent - NSbuffer, westExtent - EWbuffer, northExtent + NSbuffer, eastExtent + EWbuffer);
            ImageStats stats = new ImageStats(fullDrawing, 1080, 1920);
            //TODO: scale this to the full drawing area's proportions, not the phone screen's.
            stats = MapTileSupport.ScaleBoundsCheck(stats, Configuration["adminPreviewImageMaxEdge"].ToInt(), 1080);
            stats.filterSize = stats.degreesPerPixelY; //Don't draw circles too small to see on-screen.
            var mapTile = MapTileSupport.MapTiles.DrawAreaAtSize(stats, mapItems, "Cover");

            return File(mapTile, "image/png");
        }
    }
}