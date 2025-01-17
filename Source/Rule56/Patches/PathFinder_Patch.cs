﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using CombatAI.Comps;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace CombatAI.Patches
{
    public static class PathFinder_Patch
    {
        public static bool FlashSearch;

        [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.FindPath), typeof(IntVec3), typeof(LocalTargetInfo), typeof(TraverseParms), typeof(PathEndMode), typeof(PathFinderCostTuning))]
        private static class PathFinder_FindPath_Patch
        {
            // debugging only
#if DEBUG
			private static PathFinder  flashInstance;
#endif
            private static ThingComp_CombatAI               comp;
            private static FloatRange                       temperatureRange;
            private static bool                             checkAvoidance;
            private static bool                             checkVisibility;
            private static bool                             dig;
            private static Pawn                             pawn;
            private static Map                              map;
            private static IntVec3                          destPos;
            private static PathFinder                       instance;
            private static SightTracker.SightReader         sightReader;
            private static WallGrid                         walls;
            private static AvoidanceTracker.AvoidanceReader avoidanceReader;
//			private static          int                              counter;
            private static          float         threatAtDest;
            private static          float         availabilityAtDest;
            private static          float         visibilityAtDest;
            private static          float         multiplier = 1.0f;
            private static          bool          flashCost;
            private static          bool          isRaider;
            private static          bool          isPlayer;
            private static readonly List<IntVec3> blocked = new List<IntVec3>(128);
            private static          bool          fallbackCall;
            private static          ISGrid<float> f_grid;

            private static TraverseParms original_traverseParms;
            private static PathEndMode   origina_peMode;

            [HarmonyPriority(int.MaxValue)]
            internal static void Prefix(PathFinder __instance, ref PawnPath __result, IntVec3 start, LocalTargetInfo dest, ref TraverseParms traverseParms, PathEndMode peMode, ref PathFinderCostTuning tuning, out bool __state)
            {
                if (fallbackCall)
                {
                     __state = true;
                     return;
                }
#if DEBUG
				flashInstance = flashCost ? __instance : null;
#endif
                // only allow factioned pawns.
                if (Finder.Settings.Pather_Enabled && (pawn = traverseParms.pawn) != null && pawn.Faction != null && (pawn.RaceProps.Humanlike || pawn.RaceProps.IsMechanoid || pawn.RaceProps.Insect))
                {
                    destPos                = dest.Cell;
                    original_traverseParms = traverseParms;
                    origina_peMode         = peMode;
                    // prepare the modifications
                    instance = __instance;
                    map      = __instance.map;
                    walls    = __instance.map.GetComp_Fast<WallGrid>();
                    f_grid   = __instance.map.GetComp_Fast<MapComponent_CombatAI>().f_grid;
                    f_grid.Reset();
                    // get temperature data.
                    temperatureRange = new FloatRange(pawn.GetStatValue_Fast(StatDefOf.ComfyTemperatureMin, 1600), pawn.GetStatValue_Fast(StatDefOf.ComfyTemperatureMax, 1600));
                    temperatureRange = temperatureRange.ExpandedBy(12);
                    if (temperatureRange.min >= temperatureRange.max)
                    {
                        temperatureRange.min = -32;
                        temperatureRange.max = 128;
                    }
                    // set faction params.
                    isPlayer = pawn.Faction.IsPlayerSafe();
                    isRaider = !isPlayer && (pawn.HostileTo(Faction.OfPlayerSilentFail) || map.ParentFaction != null && pawn.Faction.HostileTo(map.ParentFaction));
                    // grab different readers.
                    pawn.TryGetAvoidanceReader(out avoidanceReader);
                    pawn.TryGetSightReader(out sightReader);
                    // prepare sapping data
                    if (sightReader != null)
                    {
                        threatAtDest       = sightReader.GetThreat(dest.Cell) * Finder.Settings.Pathfinding_DestWeight;
                        availabilityAtDest = sightReader.GetEnemyAvailability(dest.Cell) * Finder.Settings.Pathfinding_DestWeight;
                        visibilityAtDest   = sightReader.GetVisibilityToEnemies(dest.Cell) * Finder.Settings.Pathfinding_DestWeight;
                        comp               = pawn.AI();
                        if (dig = Finder.Settings.Pather_KillboxKiller
                                  && isRaider
                                  && comp != null && comp.CanSappOrEscort && !comp.IsSapping
                                  && !pawn.mindState.duty.Is(DutyDefOf.Sapper) && !pawn.CurJob.Is(JobDefOf.Mine) && !pawn.mindState.duty.Is(DutyDefOf.ExitMapRandom) && !pawn.mindState.duty.Is(DutyDefOf.Escort))
                        {
                            float costMultiplier = 1;
                            costMultiplier *= comp.TookDamageRecently(360) ? 4 : 1;
                            float miningSkill = pawn.skills?.GetSkill(SkillDefOf.Mining)?.Level ?? 0f;
                            isRaider = true;
                            TraverseParms parms = traverseParms;
                            parms.maxDanger     = Danger.Deadly;
                            parms.canBashDoors  = true;
                            parms.canBashFences = true;
                            bool humanlike = pawn.RaceProps.Humanlike;
                            if (humanlike)
                            {
                                parms.mode = TraverseMode.PassAllDestroyableThings;
                            }
                            else
                            {
                                parms.mode = TraverseMode.PassDoors;
                            }
                            traverseParms = parms;
                            if (tuning == null)
                            {
                                tuning                            = new PathFinderCostTuning();
                                tuning.costBlockedDoor            = (int)(15f * costMultiplier);
                                tuning.costBlockedDoorPerHitPoint = costMultiplier - 1;
                                if (humanlike)
                                {
                                    tuning.costBlockedWallBase                 = (int)(32f * costMultiplier);
                                    tuning.costBlockedWallExtraForNaturalWalls = (int)(32f * costMultiplier);
                                    tuning.costBlockedWallExtraPerHitPoint     = (20f - Mathf.Clamp(miningSkill, 5, 15)) / 100 * Finder.Settings.Pathfinding_SappingMul * costMultiplier;
                                    tuning.costOffLordWalkGrid                 = 0;
                                }
                            }
                        }
                    }
                    checkAvoidance  = Finder.Settings.Flank_Enabled && avoidanceReader != null && !isPlayer;
                    checkVisibility = sightReader != null;
//					counter         = 0;
                    flashCost = Finder.Settings.Debug_DebugPathfinding && Find.Selector.SelectedPawns.Contains(pawn);
                    __state = true;
                    return;
                }
                __state = false;
                Reset();
                return;
            }

            [HarmonyPriority(int.MinValue)]
            public static void Postfix(PathFinder __instance, ref PawnPath __result, bool __state, IntVec3 start, LocalTargetInfo dest, TraverseParms traverseParms, PathEndMode peMode, PathFinderCostTuning tuning)
            {
                if (fallbackCall)
                {
                    return;
                }
                if (__state)
                {
//                    if (__result == null || __result == PawnPath.NotFound || !__result.Found)
//                    {
//                        try
//                        {
//                            __result?.Dispose();
//                            fallbackCall = true;
//                            dig          = false;
//                            __result     = __instance.FindPath(start, dest, original_traverseParms, origina_peMode, tuning);
//                        }
//                        catch (Exception er)
//                        {
//                            Log.Error($"ISMA: Error occured in FindPath fallback call {er}");
//                        }
//                        finally
//                        {
//                            fallbackCall = false;
//                        }
//                    }
                    if (dig && !(__result?.nodes.NullOrEmpty() ?? true))
                    {
                        blocked.Clear();
                        Thing blocker;
                        if (__result.TryGetSapperSubPath(pawn, blocked, 15, 3, out IntVec3 cellBefore, out IntVec3 cellAhead, out bool enemiesAhead, out bool enemiesBefore) && blocked.Count > 0 && (blocker = blocked[0].GetEdifice(map)) != null)
                        {
                            if (tuning != null && (!enemiesAhead || enemiesBefore || map.fogGrid.IsFogged(cellAhead) || cellAhead.HeuristicDistanceTo(cellBefore, map, 2) <= 8))
                            {
                                try
                                {
                                    __result.Dispose();
                                    fallbackCall                               = true;
                                    dig                                        = false;
                                    tuning.costBlockedWallBase                 = Maths.Max(tuning.costBlockedWallBase * 3, 128);
                                    tuning.costBlockedWallExtraForNaturalWalls = Maths.Max(tuning.costBlockedWallExtraForNaturalWalls * 3, 128);
                                    tuning.costBlockedWallExtraPerHitPoint     = Maths.Max(tuning.costBlockedWallExtraPerHitPoint * 4, 4);
                                    __result                                   = __instance.FindPath(start, dest, original_traverseParms, origina_peMode, tuning);
                                }
                                catch (Exception er)
                                {
                                    Log.Error($"ISMA: Error occured in FindPath fallback call {er}");
                                }
                                finally
                                {
                                    fallbackCall = false;
                                }
                            }
                            else
                            {
                                comp.StartSapper(blocked, cellBefore, enemiesAhead);
                            }
                        }
                    }
                    AvoidanceTracker tracker = pawn.Map.GetComp_Fast<AvoidanceTracker>();
                    if (tracker != null)
                    {
                        tracker.Notify_PathFound(pawn, __result);
                    }
                }
                Reset();
            }

            public static void Reset()
            {
                avoidanceReader  = null;
                isRaider         = false;
                isPlayer         = false;
                multiplier       = 1f;
                sightReader      = null;
                dig              = false;
                threatAtDest     = 0;
                instance         = null;
                visibilityAtDest = 0f;
                map              = null;
                walls            = null;
                flashCost        = false;
                pawn             = null;
                f_grid           = null;
            }

            /*
             * Search for the vairable that is initialized by the value from the avoid grid or search for
             * ((i > 3) ? num9 : num8) + num15;
             *          
             */
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                List<CodeInstruction> codes     = instructions.ToList();
                bool                  finished1 = false;
                for (int i = 0; i < codes.Count; i++)
                {
                    if (!finished1)
                    {
                        if (codes[i].opcode == OpCodes.Stloc_S && codes[i].operand is LocalBuilder builder1 && builder1.LocalIndex == 48)
                        {
                            finished1 = true;
                            yield return new CodeInstruction(OpCodes.Ldloc_S, 45).MoveLabelsFrom(codes[i]).MoveBlocksFrom(codes[i]); // index of cell around curIndex
                            yield return new CodeInstruction(OpCodes.Ldloc_S, 3); // curIndex
                            yield return new CodeInstruction(OpCodes.Ldloc_S, 15); // open cell num (after enqueue)
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PathFinder_FindPath_Patch), nameof(GetCostOffsetAt)));
                            yield return new CodeInstruction(OpCodes.Add);
                        }
                    }
                    yield return codes[i];
                }
            }

            private static int GetCostOffsetAt(int index, int parentIndex, int openNum)
            {
                if (FlashSearch && map != null && !fallbackCall)
				{
					map.debugDrawer.FlashCell(map.cellIndices.IndexToCell(parentIndex), Mathf.Clamp(PathFinder.calcGrid[parentIndex].knownCost / 1200f,0.001f, 0.99f), $"{PathFinder.calcGrid[parentIndex].knownCost}", duration:50);
				}
                if (map != null)
                {
                    float value = 0;
                    if (f_grid.IsSet(index))
                    {
                        // cache found so skip most of the math.
                        value = f_grid[index];
                    }
                    else
                    {
                        // find the cell cost offset
                        if (Finder.Settings.Temperature_Enabled)
                        {
                            float temperature = GenTemperature.TryGetTemperature(index, map);
                            if (!temperatureRange.Includes(temperature))
                            {
                                if (temperatureRange.min > temperature)
                                {
                                    value += Maths.Min(temperatureRange.min - temperature, 32) * 3;
                                }
                                else
                                {
                                    value += Maths.Min(temperature - temperatureRange.max, 32) * 3;
                                }
                            }
                        }
                        if (checkVisibility)
                        {
                            float visibility = Maths.Max((sightReader.GetVisibilityToEnemies(index) - visibilityAtDest) * 0.5f + (sightReader.GetEnemyAvailability(index) - availabilityAtDest) * 0.5f, 0);
                            if (visibility > 0)
                            {
                                // this allows us to reduce the cost as we approach the target.
                                int   dist = (destPos - map.cellIndices.IndexToCell(index)).LengthManhattan;
                                float mul  = dist < 15 ? dist / 15 : 1f;
                                value += (int)(visibility * 11f * mul);
                                float threat = Maths.Max(sightReader.GetThreat(index) - threatAtDest, 0);
                                if (threat > 0)
                                {
                                    value += threat * 22f * mul;
                                }
                            }
                            if (dig)
                            {
                                if (walls.GetFillCategory(index) == FillCategory.Full)
                                {
                                    float visibilityParent = sightReader.GetAbsVisibilityToEnemies(parentIndex);
                                    if (visibilityParent > 0)
                                    {
                                        // we do this to prevent sapping where there is enemies.
                                        value = 1000 * visibilityParent;
                                    }
                                }
                            }
                        }
                        if (checkAvoidance)
                        {
                            value += avoidanceReader.GetDanger(index) * 8 + avoidanceReader.GetProximity(index) * 6;
                        }
                        f_grid[index] = value;
                    }
                    if (checkAvoidance)
                    {
                        // we only care if the paths are parallel to each others.
                        value += Maths.Min(avoidanceReader.GetPath(index), avoidanceReader.GetPath(parentIndex)) * 11;
                    }
                    if (value > 0)
                    {
                        //
                        // TODO make this into a maxcost -= something system
                        value = value * multiplier * Finder.P75 * Mathf.Clamp(1 - PathFinder.calcGrid[parentIndex].knownCost / 2500, 0.1f, 1);
                    }
                    return (int)value;
                }
                return 0;
            }
        }
    }
}
