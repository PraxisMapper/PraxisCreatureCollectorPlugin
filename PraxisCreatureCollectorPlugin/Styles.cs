using static PraxisCore.DbTables;
using PraxisCore;

namespace CreatureCollectorAPI
{
    public static class Styles
    {
        //NOTE: the first 2 character are the alpha values. Imagesharp will move them to the end correctly as it requires if the server is using that pluging for maps instead..
        public static readonly List<StyleEntry> TCstyle = new List<StyleEntry>() {
            new StyleEntry() { MatchOrder = 5, Name = "0", StyleSet = "TC",  //transparent.
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "00ec1919", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "teamOwner", Value = "0", MatchType = "equals" },
                }
            },
            new StyleEntry() { MatchOrder = 10, Name = "1", StyleSet = "TC",  //red
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "42ff0000", FillOrStroke = "stroke", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 98 },
                    new StylePaint() { HtmlColorCode = "42ec1919", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "teamOwner", Value = "1", MatchType = "equals" },
                }
            },
            new StyleEntry() { MatchOrder = 20, Name = "2", StyleSet = "TC",  //green
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "4200ff00", FillOrStroke = "stroke", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 98 },
                    new StylePaint() { HtmlColorCode = "4219ec19", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "teamOwner", Value = "2", MatchType = "equals" },
                }
            },
            new StyleEntry() { MatchOrder = 30, Name = "3", StyleSet = "TC",  //purple
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "4236177e", FillOrStroke = "stroke", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 98 },
                    new StylePaint() { HtmlColorCode = "4226076e", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "teamOwner", Value = "3", MatchType = "equals" },
                }
            },
            new StyleEntry() { MatchOrder = 40, Name = "4", StyleSet = "TC",  //Grey
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "424d4d4d", FillOrStroke = "stroke", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 98 },
                    new StylePaint() { HtmlColorCode = "423d3d3d", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "teamOwner", Value = "4", MatchType = "equals" },
                }
            },
            new StyleEntry() { MatchOrder = 10000, Name ="background",  StyleSet = "TC",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "00000000", FillOrStroke = "fill", LineWidthDegrees=0.00000625F, LinePattern= "solid", LayerId = 101 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() {Key = "*", Value = "*", MatchType = "default"},
            }},
        };

        //biome style draws gameplay areas in bright, saturated colors and everything else in muted tones.
        //Dull commmon core:ffffff, Dull common outline: 8f8f8f
        public static List<StyleEntry> biomes = new List<StyleEntry>()
        {
            new StyleEntry() { MatchOrder = 10, Name ="tertiary", StyleSet = "biomes", //This is MatchOrder 1 because its one of the most common entries, is the correct answer 30% of the time.
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "stroke", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 98, MaxDrawRes = ConstantValues.zoom10DegPerPixelX / 2},
                    new StylePaint() { HtmlColorCode = "8f8f8f", FillOrStroke = "stroke", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99, MaxDrawRes = ConstantValues.zoom10DegPerPixelX / 2}
                },
                StyleMatchRules = new List<StyleMatchRule>()
            {
                new StyleMatchRule() { Key = "highway", Value = "tertiary|unclassified|residential|tertiary_link|service|road", MatchType = "any" },
                new StyleMatchRule() { Key="footway", Value="sidewalk|crossing", MatchType="not"},
            }},
            new StyleEntry() { IsGameElement = true, MatchOrder = 20, Name ="university", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "fffd8c", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "amenity", Value = "university|college", MatchType = "any" }}
            },
            new StyleEntry() { MatchOrder = 30, Name ="retail", StyleSet = "biomes", IsGameElement = true,
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ff8373", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "landuse", Value = "retail|commercial", MatchType = "or"},
                    new StyleMatchRule() {Key="building", Value="retail|commercial", MatchType="or" },
                    new StyleMatchRule() {Key="shop", Value="*", MatchType="or" }
            }},
            new StyleEntry() { IsGameElement = true, MatchOrder = 40, Name ="tourism", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "a20033", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "tourism", Value = "*", MatchType = "equals" }}
            },
            new StyleEntry() { IsGameElement = true, MatchOrder = 50, Name ="historical", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "5e5e5e", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "historic", Value = "*", MatchType = "equals" }}
            },
            new StyleEntry() { IsGameElement = true, MatchOrder = 60, Name ="artsCulture", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "3b25b5", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "amenity", Value = "theatre|concert hall|arts centre|planetarium", MatchType = "or" }}
            },
            new StyleEntry() { MatchOrder = 69, Name ="namedBuilding", StyleSet = "biomes", IsGameElement = true,
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 },
                    new StylePaint() { HtmlColorCode = "8f8f8f", FillOrStroke = "stroke", LineWidthDegrees=0.00000625F, LinePattern= "solid", LayerId = 99 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "building", Value = "*", MatchType = "equals" },
                    new StyleMatchRule() { Key = "name", Value = "*", MatchType = "equals" }
                }
            },
            new StyleEntry() { MatchOrder = 70, Name ="building", StyleSet = "biomes", //NOTE: making this matchOrder=2 makes map tiles draw faster, but hides some gameplay-element colors.
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 },
                    new StylePaint() { HtmlColorCode = "8f8f8f", FillOrStroke = "stroke", LineWidthDegrees=0.00000625F, LinePattern= "solid", LayerId = 99 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "building", Value = "*", MatchType = "equals" }} 
            },
            new StyleEntry() { MatchOrder = 80, Name ="water", StyleSet = "biomes", IsGameElement = true,
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "52c0de", FillOrStroke = "fill", LineWidthDegrees=0.0000625F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() {Key = "natural", Value = "water|strait|bay|coastline", MatchType = "or"},
                    new StyleMatchRule() {Key = "waterway", Value ="*", MatchType="or" },
                    new StyleMatchRule() {Key = "landuse", Value ="basin", MatchType="or" },
                    new StyleMatchRule() {Key = "leisure", Value ="swimming_pool", MatchType="or" },
                    new StyleMatchRule() {Key = "place", Value ="sea", MatchType="or" }, //stupid Labrador sea value.
                }},
            new StyleEntry() {IsGameElement = true,  MatchOrder = 90, Name ="wetland", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "034021", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 }
                },
                 StyleMatchRules = new List<StyleMatchRule>() {
                     new StyleMatchRule() { Key = "natural", Value = "wetland", MatchType = "equals" }
                 }},
            new StyleEntry() { IsGameElement = true, MatchOrder = 100, Name ="park", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "3cfa49", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "leisure", Value = "park", MatchType = "or" },
                    new StyleMatchRule() { Key = "leisure", Value = "playground", MatchType = "or" },
            }},
            new StyleEntry() { IsGameElement = true, MatchOrder = 110, Name ="beach", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffcc14", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() {Key = "natural", Value = "beach|shoal", MatchType = "or" },
                    new StyleMatchRule() {Key = "leisure", Value="beach_resort", MatchType="or"}
            } },

            new StyleEntry() { IsGameElement = true, MatchOrder = 120, Name ="natureReserve", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "5f8b04", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "leisure", Value = "nature_reserve", MatchType = "equals" }}
            },
            new StyleEntry() {IsGameElement = true, MatchOrder = 130, Name ="cemetery", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "AACBAF", FillOrStroke = "fill", FileName="Landuse-cemetery.png", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "landuse", Value = "cemetery", MatchType = "or" },
                    new StyleMatchRule() {Key="amenity", Value="grave_yard", MatchType="or" } }
            },
            new StyleEntry() { MatchOrder = 140, Name ="trailFilled", StyleSet = "biomes", IsGameElement = false, //This exists to make the map look correct, but these are so few removing them as game elements should not impact games.
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "b3a11b", FillOrStroke = "fill", LineWidthDegrees=0.000025F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() {Key="highway", Value="path|bridleway|cycleway|footway|living_street", MatchType="any"},
                    new StyleMatchRule() { Key="footway", Value="sidewalk|crossing", MatchType="not"},
                    new StyleMatchRule() { Key="area", Value="yes", MatchType="equals"}
            }},
            new StyleEntry() { IsGameElement = true, MatchOrder = 150, Name ="trail", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "b3a11b", FillOrStroke = "stroke", LineWidthDegrees=0.000025F, LinePattern= "solid", LayerId = 100, MaxDrawRes = ConstantValues.zoom14DegPerPixelX }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() {Key="highway", Value="path|bridleway|cycleway|footway|living_street", MatchType="any"},
                    new StyleMatchRule() { Key="footway", Value="sidewalk|crossing", MatchType="not"}
            }},
            new StyleEntry() { MatchOrder = 170, Name ="parking", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "EEEEEE", FillOrStroke = "fill", LineWidthDegrees=0.00000625F, LinePattern= "solid", LayerId = 100, MinDrawRes = ConstantValues.zoom12DegPerPixelX}
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "amenity", Value = "parking", MatchType = "equals" }} },

            new StyleEntry() { MatchOrder = 190, Name ="alsobeach",  StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffcc14", FillOrStroke = "fill", LineWidthDegrees=0.00000625F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "natural", Value = "sand|shingle|dune|scree", MatchType = "or" },
                    new StyleMatchRule() { Key = "surface", Value = "sand", MatchType = "or" }
            }},           
            //Transparents: Explicitly things that don't help when drawn in one color.
            new StyleEntry() { MatchOrder = 250, Name ="donotdraw",  StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "00000000", FillOrStroke = "fill", LineWidthDegrees=0.00000625F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "place", Value = "locality|islet", MatchType = "any" },
            }},
            //Roads of varying sizes and colors to match OSM colors
            new StyleEntry() { MatchOrder = 270, Name ="motorwayFilled", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "fill", LineWidthDegrees=0.000125F, LinePattern= "solid", LayerId = 92},
                    new StylePaint() { HtmlColorCode = "8f8f8f", FillOrStroke = "fill", LineWidthDegrees=0.000155F, LinePattern= "solid", LayerId = 93}
                },
                StyleMatchRules = new List<StyleMatchRule>()
            {
                new StyleMatchRule() { Key = "highway", Value = "motorway|trunk|motorway_link|trunk_link", MatchType = "any" },
                new StyleMatchRule() { Key="footway", Value="sidewalk|crossing", MatchType="not"},
                new StyleMatchRule() { Key="area", Value="yes", MatchType="equals"}
            }},
            new StyleEntry() { MatchOrder = 280, Name ="motorway", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "fill", LineWidthDegrees=0.000125F, LinePattern= "solid", LayerId = 92},
                    new StylePaint() { HtmlColorCode = "8f8f8f", FillOrStroke = "fill", LineWidthDegrees=0.000155F, LinePattern= "solid", LayerId = 93}
                },
                StyleMatchRules = new List<StyleMatchRule>()
            {
                new StyleMatchRule() { Key = "highway", Value = "motorway|trunk|motorway_link|trunk_link", MatchType = "any" },
                new StyleMatchRule() { Key="footway", Value="sidewalk|crossing", MatchType="not"},
            }},
            new StyleEntry() { MatchOrder = 290, Name ="primaryFilled", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "fill", LineWidthDegrees=0.000025F, LinePattern= "solid", LayerId = 94, },
                    new StylePaint() { HtmlColorCode = "8f8f8f", FillOrStroke = "fill", LineWidthDegrees=0.00004275F, LinePattern= "solid", LayerId = 95, }
                },
                StyleMatchRules = new List<StyleMatchRule>()
            {
                new StyleMatchRule() { Key = "highway", Value = "primary|primary_link", MatchType = "any" },
                new StyleMatchRule() { Key="footway", Value="sidewalk|crossing", MatchType="not"},
                new StyleMatchRule() { Key="area", Value="yes", MatchType="equals"}
            }},
            new StyleEntry() { MatchOrder = 300, Name ="primary", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "stroke", LineWidthDegrees=0.00005F, LinePattern= "solid", LayerId = 94, MaxDrawRes = ConstantValues.zoom6DegPerPixelX /2 },
                    new StylePaint() { HtmlColorCode = "8f8f8f", FillOrStroke = "stroke", LineWidthDegrees=0.000085F, LinePattern= "solid", LayerId = 95, MaxDrawRes = ConstantValues.zoom6DegPerPixelX /2}
                },
                StyleMatchRules = new List<StyleMatchRule>()
            {
                new StyleMatchRule() { Key = "highway", Value = "primary|primary_link", MatchType = "any" },
                new StyleMatchRule() { Key="footway", Value="sidewalk|crossing", MatchType="not"},
            }},
            new StyleEntry() { MatchOrder = 310, Name ="secondaryFilled",  StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 96, MaxDrawRes = ConstantValues.zoom8DegPerPixelX,},
                    new StylePaint() { HtmlColorCode = "8f8f8f", FillOrStroke = "fill", LineWidthDegrees=0.0000625F, LinePattern= "solid", LayerId = 97, MaxDrawRes = ConstantValues.zoom8DegPerPixelX,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
            {
                new StyleMatchRule() { Key = "highway", Value = "secondary|secondary_link", MatchType = "any" },
                new StyleMatchRule() { Key="footway", Value="sidewalk|crossing", MatchType="not"},
                new StyleMatchRule() { Key="area", Value="yes", MatchType="equals"}
            }},
            new StyleEntry() { MatchOrder = 320, Name ="secondary",  StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "stroke", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 96, MaxDrawRes = ConstantValues.zoom8DegPerPixelX,},
                    new StylePaint() { HtmlColorCode = "8f8f8f", FillOrStroke = "stroke", LineWidthDegrees=0.0000625F, LinePattern= "solid", LayerId = 97, MaxDrawRes = ConstantValues.zoom8DegPerPixelX,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
            {
                new StyleMatchRule() { Key = "highway", Value = "secondary|secondary_link", MatchType = "any" },
                new StyleMatchRule() { Key="footway", Value="sidewalk|crossing", MatchType="not"},
            }},
            new StyleEntry() { MatchOrder = 330, Name ="tertiaryFilled", StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "fill", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 98, MaxDrawRes = ConstantValues.zoom10DegPerPixelX / 2},
                    new StylePaint() { HtmlColorCode = "8f8f8f", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99, MaxDrawRes = ConstantValues.zoom10DegPerPixelX / 2}
                },
                StyleMatchRules = new List<StyleMatchRule>()
            {
                new StyleMatchRule() { Key = "highway", Value = "tertiary|unclassified|residential|tertiary_link|service|road", MatchType = "any" },
                new StyleMatchRule() { Key="footway", Value="sidewalk|crossing", MatchType="not"},
                new StyleMatchRule() { Key="area", Value="yes", MatchType="equals"}
            }},

            //NOTE: hiding elements of a given type is done by drawing those elements in a transparent color
            //My default set wants to draw things that haven't yet been identified, so I can see what needs improvement or matched by a rule.
            new StyleEntry() { MatchOrder = 9999, Name ="background",  StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "F2EFE9", FillOrStroke = "fill", LineWidthDegrees=0.00000625F, LinePattern= "solid", LayerId = 101 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "*", Value = "*", MatchType = "none" }} },

            new StyleEntry() { MatchOrder = 10000, Name ="unmatched",  StyleSet = "biomes",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "00000000", FillOrStroke = "fill", LineWidthDegrees=0.00000625F, LinePattern= "solid", LayerId = 100 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() { Key = "*", Value = "*", MatchType = "default" }}
            }
        };

        //Compete style handles Compete mode. Draws area borders and team colors.
        public static readonly List<StyleEntry> CompeteStyle = new List<StyleEntry>() {
            new StyleEntry() { MatchOrder = 5, Name = "0", StyleSet = "Compete",  //transparent.
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ec191900", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "teamOwner", Value = "0", MatchType = "equals" },
                }
            },
            new StyleEntry() { MatchOrder = 10, Name = "1", StyleSet = "Compete",  //red
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "42ff0000", FillOrStroke = "stroke", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 98 },
                    new StylePaint() { HtmlColorCode = "42ec1919", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "teamOwner", Value = "1", MatchType = "equals" },
                }
            },
            new StyleEntry() { MatchOrder = 20, Name = "2", StyleSet = "Compete",  //green
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "4200ff00", FillOrStroke = "stroke", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 98 },
                    new StylePaint() { HtmlColorCode = "4219ec19", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "teamOwner", Value = "2", MatchType = "equals" },
                }
            },
            new StyleEntry() { MatchOrder = 30, Name = "3", StyleSet = "Compete",  //purple
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "4236177e", FillOrStroke = "stroke", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 98 },
                    new StylePaint() { HtmlColorCode = "4226076e", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "teamOwner", Value = "3", MatchType = "equals" },
                }
            },
            new StyleEntry() { MatchOrder = 40, Name = "4", StyleSet = "Compete",  //Grey
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "424d4d4d", FillOrStroke = "stroke", LineWidthDegrees=0.0000125F, LinePattern= "solid", LayerId = 98 },
                    new StylePaint() { HtmlColorCode = "423d3d3d", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "teamOwner", Value = "4", MatchType = "equals" },
                }
            },
           new StyleEntry() { MatchOrder = 9995, Name = "borders", StyleSet = "Compete",   //draw the state border in black at Cell8 thickness, filled with solid white
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "000000", FillOrStroke = "stroke", LineWidthDegrees=.0025F, LinePattern= "solid", LayerId = 101,},
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "fill", LineWidthDegrees=.0025F, LinePattern= "solid", LayerId = 102,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "admin_level", Value = "*", MatchType = "any" },
                }
            },
            new StyleEntry() { MatchOrder = 10000, Name ="background",  StyleSet = "Compete",
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "00000000", FillOrStroke = "fill", LineWidthDegrees=0.00000625F, LinePattern= "solid", LayerId = 101 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() {Key = "*", Value = "*", MatchType = "default"},
            }},
        };

        //Cover style for PvE. Using CC for transparency so its more visible than other overlays.
        public static readonly List<StyleEntry> coverStyle = new List<StyleEntry>() {
             //NOTE: should generally use primary color of creature. Can use others if primary is shared by Elite version, or other creatures.
             new StyleEntry() { MatchOrder = 1, Name = "reel", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccb3bcc9", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,} 
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "1", MatchType = "equals" }, }
            },
             new StyleEntry() { MatchOrder = 2, Name = "toreel", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cce6f7f7", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,} 
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "2", MatchType = "equals" }, }
            },
             new StyleEntry() { MatchOrder = 3, Name = "merman", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc104a96", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,} 
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "3", MatchType = "equals" }, }
            },
             new StyleEntry() { MatchOrder = 4, Name = "merwoman", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc1e62f7", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "4", MatchType = "equals" }, }
            },
             new StyleEntry() { MatchOrder = 5, Name = "shadetree", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc0e6017", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "5", MatchType = "equals" }, }
            },
             new StyleEntry() { MatchOrder = 6, Name = "swordfall", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccfa5816", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "6", MatchType = "equals" }, }
            },
             new StyleEntry() { MatchOrder = 7, Name = "armedbear", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc663931", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "7", MatchType = "equals" }, }
            },
             new StyleEntry() { MatchOrder = 8, Name = "leggedbear", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccf92e04", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "8", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 9, Name = "jinky", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccecf72f", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "9", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 10, Name = "ladyinblue", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc1c3ed1", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "10", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 11, Name = "caladbolg", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc888692", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "11", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 12, Name = "agauaucuau", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccb9558c", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "12", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 13, Name = "tableaux", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc363636", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "13", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 14, Name = "mortebleaux", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc313b32", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "14", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 15, Name = "boxturtle", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc8f563b", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "15", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 16, Name = "octortoise", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc126d14", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "16", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 17, Name = "doublebat", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc924e2d", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "17", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 18, Name = "triplebat", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccd1b15a", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "18", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 19, Name = "registarf", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc0e150d", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "19", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 20, Name = "registarrr", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccb15207", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "20", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 21, Name = "fsh", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cce55114", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "21", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 22, Name = "fiiish", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccffaa2f", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "22", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 23, Name = "loafer", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc1a7a86", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "23", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 24, Name = "beever", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccdbf600", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "24", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 25, Name = "evep", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc0b9fb1", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "25", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 26, Name = "bumper", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccdc1212", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "26", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 27, Name = "glitterrati", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cce9b841", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "27", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 28, Name = "centerguard", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccc41e3a", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "28", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 29, Name = "bearocle", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc13141e", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "29", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 30, Name = "hiburnator", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc860c0c", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "30", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 31, Name = "buckeye", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc582319", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "31", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 32, Name = "bugeye", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cca97d47", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "32", MatchType = "equals" }, }
            },
            //33 and 34 are skipped/removed.
            new StyleEntry() { MatchOrder = 35, Name = "cactuscat", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc0b8628", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "35", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 36, Name = "maplecat", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccff9b00", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "36", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 37, Name = "gdradgon", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc06a01f", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "37", MatchType = "equals" }, }
            },
            new StyleEntry() { MatchOrder = 38, Name = "rdragon", StyleSet = "Cover", PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "ccd81212", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>() { new StyleMatchRule() { Key = "creatureId", Value = "38", MatchType = "equals" }, }
            },

             //Fallback entry for a creature exists but not a creature-specific style.
            new StyleEntry() { MatchOrder = 9990, Name = "anyCreature", StyleSet = "Cover",
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "cc1bca33", FillOrStroke = "fill", LineWidthDegrees=0.0000375F, LinePattern= "solid", LayerId = 99,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "generated", Value = "true", MatchType = "equals" },
                }
            },
            new StyleEntry() { MatchOrder = 9995, Name = "borders", StyleSet = "Cover",   //draw the state border in black at Cell8 thickness, filled with solid white
            PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "000000", FillOrStroke = "stroke", LineWidthDegrees=.0025F, LinePattern= "solid", LayerId = 101,},
                    new StylePaint() { HtmlColorCode = "ffffff", FillOrStroke = "fill", LineWidthDegrees=.0025F, LinePattern= "solid", LayerId = 102,}
                },
                StyleMatchRules = new List<StyleMatchRule>()
                {
                    new StyleMatchRule() { Key = "admin_level", Value = "*", MatchType = "any" },
                }
            },
            new StyleEntry() { MatchOrder = 10000, Name ="background",  StyleSet = "Cover", //transparent background for overlay
                PaintOperations = new List<StylePaint>() {
                    new StylePaint() { HtmlColorCode = "00000000", FillOrStroke = "fill", LineWidthDegrees=0.00000625F, LinePattern= "solid", LayerId = 110 }
                },
                StyleMatchRules = new List<StyleMatchRule>() {
                    new StyleMatchRule() {Key = "*", Value = "*", MatchType = "default"},
            }},
        };

        public static List<List<StyleEntry>> StyleList = new List<List<StyleEntry>>() { TCstyle, CompeteStyle, coverStyle, biomes };
    }
}
