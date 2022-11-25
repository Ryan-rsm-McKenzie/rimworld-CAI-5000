﻿using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CombatAI
{
	[LoadIf("CETeam.CombatExtended")]
	public class Mod_CE
	{
		[Unsaved]
		private static readonly FlagArray turretsCE = new FlagArray(short.MaxValue);

		public static bool active;

		public static JobDef ReloadWeapon;
		public static JobDef HunkerDown;

		[LoadNamed("CombatExtended.Verb_ShootCE:_isAiming")]
		public static FieldInfo Verb_ShootCE_isAiming;
		[LoadNamed("CombatExtended.Verb_ShootCE")]
		public static Type Verb_ShootCE;

		[LoadNamed("CombatExtended.Building_TurretGunCE")]
		public static Type Building_TurretGunCE;
		[LoadNamed("CombatExtended.Building_TurretGunCE:Active", type: LoadableType.Getter)]
		public static MethodInfo Building_TurretGunCE_Active;
		[LoadNamed("CombatExtended.Building_TurretGunCE:MannedByColonist", type: LoadableType.Getter)]
		public static MethodInfo Building_TurretGunCE_MannedByColonist;
		[LoadNamed("CombatExtended.Building_TurretGunCE:IsMannable", type: LoadableType.Getter)]
		public static MethodInfo Building_TurretGunCE_IsMannable;

		public static bool IsAimingCE(Verb verb)
		{			
			return Verb_ShootCE_isAiming != null && Verb_ShootCE.IsInstanceOfType(verb) && (bool) Verb_ShootCE_isAiming.GetValue(verb);
		}		

		public static bool IsTurretActiveCE(Building_Turret turret)
		{
			bool manable;
			return turretsCE[turret.def.index]				
				&& (((manable = (bool)Building_TurretGunCE_IsMannable.Invoke(turret, new object[0])) && (bool)Building_TurretGunCE_MannedByColonist.Invoke(turret, new object[0])) || (!manable && (bool)Building_TurretGunCE_Active.Invoke(turret, new object[0]))); ;
		}

		[RunIf(loaded: true)]
		private static void OnActive()
		{
			Finder.Settings.LeanCE_Enabled = true;
			foreach(ThingDef def in DefDatabase<ThingDef>.AllDefs)
			{
				if (def.thingClass == Building_TurretGunCE)
				{
					turretsCE[def.index] = true;
				}
			}
			Finder.Harmony.Patch(AccessTools.Method(Building_TurretGunCE, nameof(Building_Turret.SpawnSetup)), postfix: new HarmonyMethod(AccessTools.Method(typeof(Building_TurretGunCE_Patch), nameof(Building_TurretGunCE_Patch.SpawnSetup))));
			Finder.Harmony.Patch(AccessTools.Method(Building_TurretGunCE, nameof(Building_Turret.DeSpawn)), prefix: new HarmonyMethod(AccessTools.Method(typeof(Building_TurretGunCE_Patch), nameof(Building_TurretGunCE_Patch.DeSpawn))));		
		}

		[RunIf(loaded: false)]
		private static void OnInActive() => Finder.Settings.LeanCE_Enabled = false;

		private static class Building_TurretGunCE_Patch
		{
			public static void SpawnSetup(Building_Turret __instance)
			{				
				__instance.Map.GetComp_Fast<TurretTracker>().Register(__instance);
			}

			public static void DeSpawn(Building_Turret __instance)
			{
				__instance.Map.GetComp_Fast<TurretTracker>().DeRegister(__instance);
			}
		}
	}
}

