using System.Collections.Immutable;
using Chaos.Extensions.Common;
using ChaosAssetManager.ViewModel;

namespace ChaosAssetManager.Definitions;

public static class CONSTANTS
{
    public const string NEW_MAP_NAME = "New";

    public static readonly ImmutableArray<StructureViewModel> FOREGROUND_STRUCTURES;

    static CONSTANTS()
    {
        var fgStructures = new List<StructureViewModel>();

        // @formatter:wrap_arguments_style chop_always
        // @formatter:keep_existing_initializer_arrangement true

        fgStructures.AddRange(
            CreateFromPattern(
                73,
                676,
                lfgPattern: new[,]
                {
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                678,
                725,
                lfgPattern: new[,]
                {
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                727,
                740,
                lfgPattern: new[,]
                {
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                741,
                744,
                lfgPattern: new[,]
                {
                    { 1, 0 },
                    { 0, 3 }
                },
                rfgPattern: new[,]
                {
                    { 2, 0 },
                    { 0, 4 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                745,
                786,
                lfgPattern: new[,]
                {
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                787,
                792,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                793,
                798,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                799,
                804,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                805,
                810,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                811,
                819,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                820,
                828,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                829,
                855,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                856,
                882,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                883,
                885,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                886,
                888,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                889,
                897,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                898,
                906,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                907,
                909,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                910,
                912,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                913,
                918,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                919,
                924,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                925,
                927,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                928,
                930,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                931,
                948,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                949,
                969,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                970,
                990,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                991,
                1011,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1012,
                1014,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1015,
                1017,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1018,
                1023,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1024,
                1029,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1030,
                1032,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1033,
                1035,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1036,
                1041,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1042,
                1065,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1066,
                1119,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1120,
                1155,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1156,
                1164,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1165,
                1173,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1174,
                1182,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1183,
                1191,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1192,
                1197,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1198,
                1203,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1204,
                1221,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1222,
                1224,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1225,
                1236,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1237,
                1248,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1249,
                1257,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1258,
                1266,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1267,
                1269,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1270,
                1272,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1273,
                1275,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1276,
                1278,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1279,
                1287,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1288,
                1296,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1297,
                1323,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1324,
                1350,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1351,
                1359,
                lfgPattern: new[,]
                {
                    { 1, 2 }
                },
                rfgPattern: new[,]
                {
                    { 0, 3 }
                }));

        fgStructures.AddRange(
            CreateFromPattern(
                1360,
                1368,
                lfgPattern: new[,]
                {
                    { 0 },
                    { 1 }
                },
                rfgPattern: new[,]
                {
                    { 3 },
                    { 2 }
                }));

        FOREGROUND_STRUCTURES = [..fgStructures];

        // @formatter:keep_existing_initializer_arrangement restore
        // @formatter:wrap_arguments_style restore
    }

    /// <summary>
    ///     Creates a collection of <see cref="StructureViewModel" /> instances from the specified pattern.
    /// </summary>
    /// <param name="start">
    ///     The id of the first tile in the range
    /// </param>
    /// <param name="end">
    ///     The id of the last tile in the range
    /// </param>
    /// <param name="bgPattern">
    ///     The background tile pattern to fill in
    /// </param>
    /// <param name="lfgPattern">
    ///     The left foreground tile pattern to fill in
    /// </param>
    /// <param name="rfgPattern">
    ///     The right foreground tile pattern to fill in
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
        int start,
        int end,
        int[,]? bgPattern = null,
        int[,]? lfgPattern = null,
        int[,]? rfgPattern = null)
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

        var totalCountPerPattern = bgcount + lfgCount + rfgCount;

        return Enumerable.Range(start, end - start + 1)
                         .Chunk(totalCountPerPattern)
                         .Select(
                             numbers =>
                             {
                                 var width = Math.Max(
                                     bgPattern?.GetLength(1) ?? 0,
                                     Math.Max(lfgPattern?.GetLength(1) ?? 0, rfgPattern?.GetLength(1) ?? 0));

                                 var height = Math.Max(
                                     bgPattern?.GetLength(0) ?? 0,
                                     Math.Max(lfgPattern?.GetLength(0) ?? 0, rfgPattern?.GetLength(0) ?? 0));

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
                                                 bgData[x, y] = num == 0 ? 0 : numbers[num - 1];
                                         }

                                         if (lfgPattern is not null)
                                         {
                                             var num = lfgPattern[y, x];

                                             if (num != 0)

                                                 // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                                 lfgData[x, y] = num == 0 ? 0 : numbers[num - 1];
                                         }

                                         if (rfgPattern is not null)
                                         {
                                             var num = rfgPattern[y, x];

                                             if (num != 0)

                                                 // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                                 rfgData[x, y] = num == 0 ? 0 : numbers[num - 1];
                                         }
                                     }
                                 }

                                 return StructureViewModel.Create(
                                     bgPattern is not null ? bgData : null,
                                     lfgPattern is not null ? lfgData : null,
                                     rfgPattern is not null ? rfgData : null);
                             });
    }
}