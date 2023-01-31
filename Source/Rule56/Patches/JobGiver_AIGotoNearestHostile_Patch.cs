﻿using System.Collections.Generic;
using System.Drawing;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace CombatAI.Patches
{
	public static class JobGiver_AIGotoNearestHostile_Patch
	{
		[HarmonyPatch(typeof(JobGiver_AIGotoNearestHostile), nameof(JobGiver_AIGotoNearestHostile.TryGiveJob))]
		private static class JobGiver_AIGotoNearestHostile_TryGiveJob_Patch
		{
			public static bool Prefix(Pawn pawn, ref Job __result)
			{
				if (pawn.TryGetSightReader(out SightTracker.SightReader reader))
				{
					//if (pawn.Faction.HostileTo(pawn.Map.ParentFaction))
					//{
					//	Thing nearestBuildingEnemy = null;
					//	RegionFlooder.Flood(pawn.Position, pawn.mindState.enemyTarget == null ? pawn.Position : pawn.mindState.enemyTarget.Position, pawn.Map,
					//		(region, depth) =>
					//		{
					//			if (reader.GetRegionAbsVisibilityToEnemies(region) > 0)
					//			{
					//				List<Thing> things = region.ListerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
					//				if (things != null)
					//				{
					//					for (int i = 0; i < things.Count; i++)
					//					{
					//						Thing thing = things[i];
					//						if (TrashUtility.CanTrash(pawn, thing))
					//						{

					//						}
					//						//if(thing.Faction == F)
					//						//Pawn other;
					//						//if (thing is IAttackTarget target && !target.ThreatDisabled(pawn) && AttackTargetFinder.IsAutoTargetable(target) && thing.HostileTo(pawn) && ((other = thing as Pawn) == null || other.IsCombatant()))
					//						//{
					//						//	nearestEnemy = thing;
					//						//	return true;
					//						//}
					//					}
					//				}
					//				things = region.ListerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
					//			}
					//			return false;
					//		},
					//		cost: region =>
					//		{
					//			return Maths.Min(reader.GetRegionAbsVisibilityToEnemies(region), 8 * Finder.P50) * 10 * Mathf.Clamp(reader.GetRegionThreat(region) + 0.5f, 1.0f, 2.0f);
					//		},
					//		validator: region =>
					//		{
					//			return reader.GetRegionAbsVisibilityToEnemies(region) == 0;
					//		}, maxRegions: 512);
					//	if (nearestBuildingEnemy != null)
					//	{
					//		__result = TrashUtility.TrashJob(pawn, nearestBuildingEnemy, true);
					//		return false;
					//	}
					//}
					Thing nearestEnemy = null;
					RegionFlooder.Flood(pawn.Position, pawn.mindState.enemyTarget == null ? pawn.Position : pawn.mindState.enemyTarget.Position, pawn.Map,
					    (region, depth) =>
					    {
						    if (reader.GetRegionAbsVisibilityToEnemies(region) > 0)
						    {
							    List<Thing> things = region.ListerThings.ThingsInGroup(ThingRequestGroup.Pawn);
							    if (things != null)
							    {
								    for (int i = 0; i < things.Count; i++)
								    {
									    Thing thing = things[i];
									    Pawn  other;
									    if (thing is IAttackTarget target && !target.ThreatDisabled(pawn) && AttackTargetFinder.IsAutoTargetable(target) && thing.HostileTo(pawn) && ((other = thing as Pawn) == null || other.IsCombatant()))
									    {
										    nearestEnemy = thing;
										    return true;
									    }
								    }
							    }
								things = region.ListerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
							}
						    return false;
					    },
					    cost: region =>
					    {
						    return Maths.Min(reader.GetRegionAbsVisibilityToEnemies(region), 8 * Finder.P50) * 10 * Mathf.Clamp(reader.GetRegionThreat(region) + 0.5f, 1.0f, 2.0f);
					    }, maxRegions:512);					
					if (nearestEnemy != null)
					{
						Job job = JobMaker.MakeJob(JobDefOf.Goto, nearestEnemy);
						job.checkOverrideOnExpire = true;
						job.expiryInterval        = 500;
						job.collideWithPawns      = true;
						__result                  = job;
						return false;
					}
				}
				return true;
			}
		} 
	}
}
