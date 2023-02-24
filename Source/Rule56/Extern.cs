using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CombatAI.Comps;
using Prepatcher;
using Verse;
namespace CombatAI
{
    [LoadIf("zetrith.prepatcher")]
    public static class Extern
    {
#pragma warning disable CS0649
        private static bool active;
#pragma warning restore CS0649x

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ThingComp_CombatAI AI(this Pawn pawn)
        {
            if (active)
            {
                return CombatAI_ThingComp(pawn);
            }
            else
            {
                return pawn.GetComp_Fast<ThingComp_CombatAI>();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MapComponent_CombatAI AI(this Map map)
        {
            if (active)
            {
                return CombatAI_MapComp(map);
            }
            else
            {
                return map.GetComp_Fast<MapComponent_CombatAI>();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SightTracker Sight(this Map map)
        {
            if (active)
            {
                return CombatAI_Sight(map);
            }
            else
            {
                return map.GetComp_Fast<SightTracker>();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AvoidanceTracker Avoidance(this Map map)
        {
            if (active)
            {
                return CombatAI_Avoidance(map);
            }
            else
            {
                return map.GetComp_Fast<AvoidanceTracker>();
            }
        }


        [PrepatcherField]
        [InjectComponent]
        private static extern ThingComp_CombatAI CombatAI_ThingComp(ThingWithComps thingWithComps);
        [PrepatcherField]
        [InjectComponent]
        private static extern MapComponent_CombatAI CombatAI_MapComp(Map map);
        [PrepatcherField]
        [InjectComponent]
        private static extern  SightTracker CombatAI_Sight(Map map);
        [PrepatcherField]
        [InjectComponent]
        private static extern AvoidanceTracker CombatAI_Avoidance(Map map);
        
    }
}
