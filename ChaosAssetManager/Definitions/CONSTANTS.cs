using System.Collections.Immutable;
using Chaos.Extensions.Common;
using ChaosAssetManager.ViewModel;

namespace ChaosAssetManager.Definitions;

public static class CONSTANTS
{
    public const string NEW_MAP_NAME = "New";

    public static readonly ImmutableArray<StructureViewModel> FOREGROUND_STRUCTURES;
    public static readonly ImmutableArray<StructureViewModel> BACKGROUND_STRUCTURES;

    static CONSTANTS()
    {
        // @formatter:wrap_arguments_style chop_always
        // @formatter:keep_existing_initializer_arrangement true
        
        #region Foreground Structures
        var fgStructures = new List<StructureViewModel>();

        // (,),


        //    *
        // * l r *
        //    *
        // NAME: SINGLE TILE
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 2 }
                },
                ranges:
                [
                    (73, 394),
                    (396, 407),
                    (409, 676),
                    (678, 725),
                    (727, 740),
                    (745, 786)
                ]));

        //      *
        //   * l r *
        // *  0 , 0  *
        //   * l r *
        //      *
        // NAME: PURE VERTICAL
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 1, 0 },
                    { 0, 3 }
                },
                rfgPattern: new[,]
                {
                    { 2, 0 },
                    { 0, 4 }
                },
                ranges: [(741, 744)]));

        //    *
        // *  l  *
        //    * l r *
        //       *
        // NAME: llr 2x1
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                },
                ranges:
                [
                    (787, 792),
                    (799, 804),
                    (811, 819),
                    (829, 855),
                    (883, 885),
                    (889, 897),
                    (907, 909),
                    (913, 918),
                    (925, 927),
                    (931, 948),
                    (970, 990),
                    (1012, 1014),
                    (1018, 1023),
                    (1030, 1032),
                    (1036, 1041),
                    (1066, 1119),
                    (1156, 1164),
                    (1174, 1182),
                    (1192, 1197),
                    (1204, 1212),
                    (1219, 1221),
                    (1225, 1257),
                    (1267, 1269),
                    (1273, 1287),
                    (1297, 1323),
                    (1351, 1359),
                    (1369, 1371),
                    (1375, 1380),
                    (1387, 1404),
                    (1423, 1425),
                    (1429, 1437),
                    (1447, 1452),
                    (1459, 1473),
                    (1489, 1515),
                    (1543, 1569),
                    (1597, 1605),
                    (1615, 1623),
                    (1633, 1641),
                    (1651, 1677),
                    (1705, 1710),
                    (1717, 1725),
                    (1735, 1737),
                    (1741, 1749),
                    (1759, 1767),
                    (1777, 1779),
                    (1783, 1791),
                    (1801, 1806),
                    (1813, 1815),
                    (1819, 1833),
                    (1849, 1857),
                    (1867, 1872),
                    (1879, 1884),
                    (1891, 1896),
                    (1903, 1911),
                    (1921, 1929),
                    (1939, 1947),
                    (1957, 1959),
                    (1963, 1965),
                    (1969, 1971),
                    (1975, 1983),
                    (1993, 1998),
                    (2005, 2010),
                    (2017, 2019),
                    (2023, 2025),
                    (2029, 2031),
                    (2035, 2037),
                    (2041, 2043),
                    (2056, 2058),
                    (2062, 2067),
                    (2119, 2121),
                    (2128, 2130)
                ]));

        //       *
        //    *  r  *
        // * l r *
        //    *
        // NAME: lrr 1x2
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                },
                ranges:
                [
                    (793, 798),
                    (805, 810),
                    (820, 828),
                    (856, 882),
                    (898, 906),
                    (910, 912),
                    (919, 924),
                    (928, 930),
                    (949, 969),
                    (991, 1011),
                    (991, 1011),
                    (1015, 1017),
                    (1024, 1029),
                    (1033, 1035),
                    (1042, 1065),
                    (1120, 1155),
                    (1165, 1173),
                    (1183, 1191),
                    (1198, 1203),
                    (1213, 1218),
                    (1222, 1224),
                    (1258, 1266),
                    (1270, 1272),
                    (1288, 1296),
                    (1324, 1350),
                    (1360, 1368),
                    (1372, 1374),
                    (1381, 1386),
                    (1405, 1422),
                    (1426, 1428),
                    (1438, 1446),
                    (1453, 1458),
                    (1474, 1488),
                    (1516, 1542),
                    (1570, 1596),
                    (1606, 1614),
                    (1624, 1632),
                    (1642, 1650),
                    (1678, 1704),
                    (1711, 1716),
                    (1726, 1734),
                    (1738, 1740),
                    (1750, 1758),
                    (1768, 1776),
                    (1780, 1782),
                    (1792, 1800),
                    (1807, 1812),
                    (1816, 1818),
                    (1834, 1848),
                    (1858, 1866),
                    (1873, 1878),
                    (1885, 1890),
                    (1897, 1902),
                    (1912, 1920),
                    (1930, 1938),
                    (1948, 1956),
                    (1960, 1962),
                    (1966, 1968),
                    (1972, 1974),
                    (1984, 1992),
                    (1999, 2004),
                    (2011, 2016),
                    (2020, 2022),
                    (2026, 2028),
                    (2032, 2034),
                    (2038, 2040),
                    (2044, 2055),
                    (2059, 2061),
                    (2068, 2118),
                    (2122, 2127)
                ]));

        //      *
        //   *  0  *
        // * l  ,  r *
        //   * l r *
        //      *
        // NAME: llrr 2x2 (VSHAPE)
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0, 0 },
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 4 },
                    { 0, 3 }
                },
                ranges:
                [
                    (2131, 2138),
                    (2331, 2378),
                    (2467, 2486),
                    (2910, 2913),
                    (2916, 2919),
                    (2932, 2935),
                    (2938, 2941),
                    (2958, 2961),
                    (2964, 2967),
                    (2980, 2983),
                    (2986, 2989)
                ]));

        //    *
        // *  l  *
        //    *  l  *
        //       * l r *
        //          *
        // NAME: lllr 3x1
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 1, 2, 3 }
                },
                rfgPattern: new[,]
                {
                    { 0, 0, 4 }
                },
                ranges:
                [
                    (2139, 2170),
                    (2199, 2234),
                    (2267, 2298),
                    (2379, 2382),
                    (2387, 2390),
                    (2395, 2398),
                    (2403, 2406),
                    (2411, 2438),
                    (2487, 2502),
                    (2744, 2747),
                    (2792, 2795),
                    (2808, 2811),
                    (2818, 2821),
                    (2828, 2831),
                    (2839, 2842),
                    (2844, 2847)
                ]));

        //    *
        // *  l  *
        //    *  l  *
        //       * l r *
        //          *
        // NAME: lllr 3x1 (VARIATION)
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 1, 2, 3 }
                },
                rfgPattern: new[,]
                {
                    { 0, 0, 5 }
                },
                discards: 1,
                ranges:
                [
                    (2744, 2748),
                    (2792, 2796)
                ]));

        //          *
        //       *  r  *
        //    *  r  *
        // * l r *
        //    *
        // NAME: lrrr 1x3
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0 },
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 4 },
                    { 3 },
                    { 2 }
                },
                ranges:
                [
                    (2171, 2198),
                    (2235, 2266),
                    (2299, 2330),
                    (2383, 2386),
                    (2391, 2394),
                    (2399, 2402),
                    (2407, 2410),
                    (2439, 2466),
                    (2503, 2514),
                    (2755, 2758),
                    (2824, 2827),
                    (2834, 2837)
                ]));

        //          *
        //       *  r  *
        //    *  r  *
        // * l r *
        //    *
        // NAME: lrrr 1x3 (VARIATION)
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0 },
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 5 },
                    { 4 },
                    { 3 }
                },
                discards: 1,
                ranges: [(2754, 2758)]));

        //    *
        // *  l  *
        //    *  l  *
        //       *  l  *
        //          * l r *
        //             *
        // NAME: llllr 4x1
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 1, 2, 3, 4 }
                },
                rfgPattern: new[,]
                {
                    { 0, 0, 0, 5 }
                },
                ranges:
                [
                    (2520, 2544),
                    (2886, 2900),
                    (2902, 2906),
                    (2944, 2948),
                    (2950, 2954),
                    (2992, 2996),
                    (2998, 3002)
                ]));

        //             *
        //          *  r  *
        //       *  r  *
        //    *  r  *
        // * l r *
        //    *
        // NAME: lrrrr 1x4
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0 },
                    { 0 },
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 5 },
                    { 4 },
                    { 3 },
                    { 2 }
                },
                ranges:
                [
                    (2550, 2574),
                    (2803, 2807),
                    (2862, 2871),
                    (2921, 2925),
                    (2927, 2931),
                    (2969, 2973),
                    (2975, 2979),
                    (3017, 3021),
                    (3023, 3027)
                ]));

        //             *
        //          *  r  *
        //       *  r  *
        //    *  r  *
        // * l r *
        //    *
        // NAME: lrrrr 1x4 (VARIATION
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0 },
                    { 0 },
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 6 },
                    { 5 },
                    { 4 },
                    { 3 }
                },
                discards: 1,
                ranges: [(2802, 2807)]));

        //       *
        //    *  l  *
        // *  0  ,  l  *
        //    *  0  ,  0  *
        //       *  r  , l r *
        //          *  0  *
        //             *
        // NAME: lll r lr 4x2
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 1, 2, 0, 4 },
                    { 0, 0, 0, 0 }
                },
                rfgPattern: new[,]
                {
                    { 0, 0, 0, 5 },
                    { 0, 0, 3, 0 }
                },
                ranges: [(2515, 2519)]));

        //             *
        //          *  r  *
        //       *  r  ,  0  *
        //    *  0  ,  0  *
        // * l r ,  l  *
        //    *  0  *
        //       *
        // NAME: lr l rr 2x4
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0, 0 },
                    { 0, 0 },
                    { 0, 3 },
                    { 1, 0 }
                },
                rfgPattern: new[,]
                {
                    { 5, 0 },
                    { 4, 0 },
                    { 0, 0 },
                    { 2, 0 }
                },
                ranges: [(2545, 2549)]));

        //          *
        //       *  0  *
        //    *  0  ,  r  *
        // *  l  ,  r  *
        //    * l r *
        //       *
        // NAME: llrrr 2x3 (REVERSE CHECKMARK)
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0, 0 },
                    { 0, 0 },
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 5 },
                    { 0, 4 },
                    { 0, 3 }
                },
                ranges:
                [
                    (2575, 2584),
                    (2595, 2604),
                    (2749, 2753),
                    (2797, 2801),
                    (3162, 3166),
                    (3169, 3173),
                ]));

        //       *
        //    *  0  *
        // *  l  ,  0  *
        //    *  l  ,  r  *
        //       * l r  *
        //          *
        // NAME: lllrr 3x2 (CHECKMARK)
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0, 0, 0 },
                    { 1, 2, 3 }
                },
                rfgPattern: new[,]
                {
                    { 0, 0, 5 },
                    { 0, 0, 4 }
                },
                ranges:
                [
                    (2585, 2594),
                    (2739, 2743),
                    (2787, 2791),
                    (3134, 3138),
                    (3141, 3145)
                ]));

        //       *
        //    *  0  *
        // *  l  ,  0  *
        //    *  l  ,  0  *
        //       *  l  ,  r  *
        //          * l r *
        //             *
        // NAME: llllrr 4x2 (CHECKMARK+)
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0, 0, 0, 0 },
                    { 1, 2, 3, 4 }
                },
                rfgPattern: new[,]
                {
                    { 0, 0, 0, 6 },
                    { 0, 0, 0, 5 }
                },
                ranges:
                [
                    (2605, 2628),
                    (3004, 3015),
                    (3073, 3078),
                    (3080, 3085)
                ]));

        //             *
        //          *  0  *
        //       *  0  ,  r  *
        //    *  0  ,  r  *
        // *  l  ,  r  *
        //    * l r *
        //       *
        // NAME: llrrrr 2x4 (REVERSE CHECKMARK+)
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0, 0 },
                    { 0, 0 },
                    { 0, 0 },
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 6 },
                    { 0, 5 },
                    { 0, 4 },
                    { 0, 3 }
                },
                ranges:
                [
                    (2629, 2652),
                    (3028, 3039),
                    (3102, 3107),
                    (3109, 3114),
                ]));

        //          *
        //       *  0  *
        //    *  0  ,  0  *
        // *  l  ,  0  ,  r  *
        //    *  l  ,  r  *
        //       * l r *
        //          *
        // NAME: lllrrr 3x3 (VSHAPE+)
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0, 0, 0 },
                    { 0, 0, 0 },
                    { 1, 2, 3 }
                },
                rfgPattern: new[,]
                {
                    { 0, 0, 6 },
                    { 0, 0, 5 },
                    { 0, 0, 4 }
                },
                ranges:
                [
                    (2653, 2670),
                    (3041, 3046)
                ]));

        //    *
        // *  l  *
        //    *  l  *
        //       *  l  *
        //          *  l  *
        //             * l r *
        //                *
        // NAME: lllllr 5x1
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 1, 2, 3, 4, 5 }
                },
                rfgPattern: new[,]
                {
                    { 0, 0, 0, 0, 6 }
                },
                ranges:
                [
                    (2725, 2730),
                    (2732, 2737),
                    (2759, 2764),
                    (2766, 2771)
                ]));

        //                *
        //             *  r  *
        //          *  r  *
        //       *  r  *
        //    *  r  *
        // * l r *
        //    *
        // NAME: lrrrrr 1x5
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0 },
                    { 0 },
                    { 0 },
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 6 },
                    { 5 },
                    { 4 },
                    { 3 },
                    { 2 }
                },
                ranges:
                [
                    (2699, 2710),
                    (2712, 2717),
                    (2719, 2724),
                    (2774, 2779),
                    (2781, 2786),
                    (2812, 2817)
                ]));

        //             *
        //          *  0  *
        //       *  0  ,  0  *
        //    *  0  ,  0  ,  0  *
        // *  l  ,  0  ,  0  ,  r  *
        //    *  l  ,  0  ,  r  *
        //       *  l  ,  r  *
        //          * l r *
        //             *
        // NAME: llllrrrr 4x4 (VSHAPE++)
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0, 0, 0, 0 },
                    { 0, 0, 0, 0 },
                    { 0, 0, 0, 0 },
                    { 1, 2, 3, 4 }
                },
                rfgPattern: new[,]
                {
                    { 0, 0, 0, 8 },
                    { 0, 0, 0, 7 },
                    { 0, 0, 0, 6 },
                    { 0, 0, 0, 5 }
                },
                ranges: [(3048, 3055)]));

        //    *
        // *  l  *
        //    *  l  *
        //       *  l  *
        //          *  l  *
        //             *  l  *
        //                * l r *
        //                   *
        // NAME: llllllr 6x1
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 1, 2, 3, 4, 5, 6 }
                },
                rfgPattern: new[,]
                {
                    { 0, 0, 0, 0, 0, 7 }
                },
                ranges:
                [
                    (2685, 2698),
                    (2872, 2885)
                ]));

        //                   *
        //                *  r  *
        //             *  r  *
        //          *  r  *
        //       *  r  *
        //    *  r  *
        // * l r *
        //    *
        // NAME: lrrrrrr 1x6
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0 },
                    { 0 },
                    { 0 },
                    { 0 },
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 7 },
                    { 6 },
                    { 5 },
                    { 4 },
                    { 3 },
                    { 2 }
                },
                ranges:
                [
                    (2671, 2684),
                    (2848, 2861)
                ]));

        //       *
        //    *  0  *
        // *  l  ,  0  *
        //    *  l  ,  0  *
        //       *  l  ,  0  *
        //          *  l  ,  0  *
        //             *  l  ,  r  *
        //                * l r *
        //                   *
        // NAME: llllllrr 6x2 (CHECKMARK+++)
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0, 0, 0, 0, 0, 0 },
                    { 1, 2, 3, 4, 5, 6 }
                },
                rfgPattern: new[,]
                {
                    { 0, 0, 0, 0, 0, 8 },
                    { 0, 0, 0, 0, 0, 7 }
                },
                ranges:
                [
                    (3056, 3071),
                    (3116, 3131)
                ]));

        //                   *
        //                *  0  *
        //             *  0  ,  r  *
        //          *  0  ,  r  *
        //       *  0  ,  r  *
        //    *  0  ,  r  *
        // *  l  ,  r  *
        //    * l r *
        //       *
        // NAME: llrrrrrr 2x6 (REVERSE CHECKMARK+++)
        fgStructures.AddRange(
            CreateFromPattern(
                lfgPattern: new[,]
                {
                    { 0, 0 },
                    { 0, 0 },
                    { 0, 0 },
                    { 0, 0 },
                    { 0, 0 },
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 8 },
                    { 0, 7 },
                    { 0, 6 },
                    { 0, 5 },
                    { 0, 4 },
                    { 0, 3 }
                },
                ranges:
                [
                    (3086, 3101),
                    (3146, 3161)
                ]));
        
        #endregion
        
        #region Background Structures

        //BACKGROUND STRUCTURES
        var bgStructures = new List<StructureViewModel>();
        
        bgStructures.AddRange(
            CreateFromPattern(
                new[,]
                {
                    { 1, 2 },
                    { 3, 4 }
                },
                ranges:
                [
                    (269, 276),
                    (305, 312),
                ]));

        bgStructures.AddRange(
            CreateFromPattern(
                new[,]
                {
                    { 1, 2, 3 },
                    { 4, 5, 6 },
                    { 7, 8, 9 },
                },
                ranges:
                [
                    (313, 321),
                ]));
        
        #endregion
        
        // (,),

        FOREGROUND_STRUCTURES =
        [
            ..fgStructures.OrderBy(
                structure =>
                {
                    var minTileId = int.MaxValue;

                    if (structure.HasBackgroundTiles)
                        minTileId = Math.Min(
                            minTileId,
                            structure.RawBackgroundTiles
                                     .Select(tile => tile.TileId)
                                     .Where(tileId => tileId != 0)
                                     .Min());

                    if (structure.HasLeftForegroundTiles)
                        minTileId = Math.Min(
                            minTileId,
                            structure.RawLeftForegroundTiles
                                     .Select(tile => tile.TileId)
                                     .Where(tileId => tileId != 0)
                                     .Min());

                    if (structure.HasRightForegroundTiles)
                        minTileId = Math.Min(
                            minTileId,
                            structure.RawRightForegroundTiles
                                     .Select(tile => tile.TileId)
                                     .Where(tileId => tileId != 0)
                                     .Min());

                    return minTileId;
                })
        ];
        BACKGROUND_STRUCTURES =
        [
            ..bgStructures.OrderBy(
                structure =>
                {
                    var minTileId = int.MaxValue;

                    if (structure.HasBackgroundTiles)
                        minTileId = Math.Min(
                            minTileId,
                            structure.RawBackgroundTiles
                                     .Select(tile => tile.TileId)
                                     .Where(tileId => tileId != 0)
                                     .Min());

                    if (structure.HasLeftForegroundTiles)
                        minTileId = Math.Min(
                            minTileId,
                            structure.RawLeftForegroundTiles
                                     .Select(tile => tile.TileId)
                                     .Where(tileId => tileId != 0)
                                     .Min());

                    if (structure.HasRightForegroundTiles)
                        minTileId = Math.Min(
                            minTileId,
                            structure.RawRightForegroundTiles
                                     .Select(tile => tile.TileId)
                                     .Where(tileId => tileId != 0)
                                     .Min());

                    return minTileId;
                })
        ];

        // @formatter:keep_existing_initializer_arrangement restore
        // @formatter:wrap_arguments_style restore
    }

    /// <summary>
    ///     Creates a collection of <see cref="StructureViewModel" /> instances from the specified pattern.
    /// </summary>
    /// <param name="bgPattern">
    ///     The background tile pattern to fill in
    /// </param>
    /// <param name="lfgPattern">
    ///     The left foreground tile pattern to fill in
    /// </param>
    /// <param name="rfgPattern">
    ///     The right foreground tile pattern to fill in
    /// </param>
    /// <param name="discards">
    ///     Extra tiles to include in the chunk that are not actually used in the pattern
    /// </param>
    /// <param name="ranges">
    ///     The ranges to fill in the patterns with
    /// </param>
    /// <remarks>
    ///     The patterns are 2D array where each element contains a number that corresponds to the order the tiles should be
    ///     placed in
    ///     <br />
    ///     <br />
    ///     The number zero means there is no tile at that position
    ///     <br />
    ///     Rows are rows, Columns are columns [y, x]... the array will be rotated when it is interpreted into data so that it
    ///     matches up with [x, y]
    /// </remarks>
    private static IEnumerable<StructureViewModel> CreateFromPattern(
        int[,]? bgPattern = null,
        int[,]? lfgPattern = null,
        int[,]? rfgPattern = null,
        int discards = 0,
        params IEnumerable<(int start, int end)> ranges)
    {
        var bgcount = bgPattern?.Flatten()
                               .Count(num => num != 0)
                      ?? 0;

        var lfgCount = lfgPattern?.Flatten()
                                 .Count(num => num != 0)
                       ?? 0;

        var rfgCount = rfgPattern?.Flatten()
                                 .Count(num => num != 0)
                       ?? 0;

        var totalCountPerPattern = bgcount + lfgCount + rfgCount + discards;
        var width = Math.Max(bgPattern?.GetLength(1) ?? 0, Math.Max(lfgPattern?.GetLength(1) ?? 0, rfgPattern?.GetLength(1) ?? 0));
        var height = Math.Max(bgPattern?.GetLength(0) ?? 0, Math.Max(lfgPattern?.GetLength(0) ?? 0, rfgPattern?.GetLength(0) ?? 0));

        foreach (var range in ranges)
        {
            var indexChunks = Enumerable.Range(range.start, range.end - range.start + 1)
                                        .Chunk(totalCountPerPattern);

            foreach (var chunk in indexChunks)
            {
                var bgData = new int[width, height];
                var lfgData = new int[width, height];
                var rfgData = new int[width, height];

                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        if (bgPattern is not null)
                        {
                            var num = bgPattern[y, x];

                            if (num != 0)

                                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                bgData[x, y] = num == 0 ? 0 : chunk[num - 1];
                        }

                        if (lfgPattern is not null)
                        {
                            var num = lfgPattern[y, x];

                            if (num != 0)

                                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                lfgData[x, y] = num == 0 ? 0 : chunk[num - 1];
                        }

                        if (rfgPattern is not null)
                        {
                            var num = rfgPattern[y, x];

                            if (num != 0)

                                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                rfgData[x, y] = num == 0 ? 0 : chunk[num - 1];
                        }
                    }
                }

                yield return StructureViewModel.Create(
                    bgPattern is not null ? bgData : null,
                    lfgPattern is not null ? lfgData : null,
                    rfgPattern is not null ? rfgData : null);
            }
        }
    }
}