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
            CreatePattern(
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
            CreatePattern(
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
            CreatePattern(
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
            CreatePattern(
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
            CreatePattern(
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
            CreatePattern(
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

        FOREGROUND_STRUCTURES = [..fgStructures];

        // @formatter:keep_existing_initializer_arrangement restore
        // @formatter:wrap_arguments_style restore

    }
    
    private static IEnumerable<StructureViewModel> CreatePattern(int start, int end, int[,]? bgPattern = null, int[,]? lfgPattern = null, int[,]? rfgPattern = null)
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
                                 var width = Math.Max(bgPattern?.GetLength(1) ?? 0, Math.Max(lfgPattern?.GetLength(1) ?? 0, rfgPattern?.GetLength(1) ?? 0));
                                 var height = Math.Max(bgPattern?.GetLength(0) ?? 0, Math.Max(lfgPattern?.GetLength(0) ?? 0, rfgPattern?.GetLength(0) ?? 0));

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