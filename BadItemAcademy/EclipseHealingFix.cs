using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Orbs;
using RoR2.Projectile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using static BadItemAcademy.Modules.Bindings;

namespace BadItemAcademy
{
    public partial class BadItemAcademyPlugin
    {

        private static float healingAmountNotReduced = 0;
        private static float modifiedHealingCache = 0;
        internal static void EclipseHealingFix()
        {
            IL.RoR2.HealthComponent.Heal += HealthComponent_Heal_Pooling2;
            if (ScimitarBypassEclipse.Value)
            {
                On.RoR2.Items.GhostBladesBehavior.HealthComponent_onCharacterOverhealServer += GhostBladesOverheal;
                On.RoR2.Items.GhostBladesBehavior.OnCharacterHealed += GhostBladesHeal;
            }
        }


        private static void GhostBladesOverheal(On.RoR2.Items.GhostBladesBehavior.orig_HealthComponent_onCharacterOverhealServer orig, RoR2.Items.GhostBladesBehavior self, HealthComponent healthComponent, float healAmount, float overhealAmount, ProcChainMask procChainMask, bool isRegen)
        {

        }

        private static void GhostBladesHeal(On.RoR2.Items.GhostBladesBehavior.orig_OnCharacterHealed orig, RoR2.Items.GhostBladesBehavior self, HealthComponent healthComponent, float amount, ProcChainMask procChainMask)
        {
            amount = healingAmountNotReduced;
            orig(self, healthComponent, amount, procChainMask);
        }
        private static void HealthComponent_Heal_Pooling2(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            #region part 1: cache unreduced healing
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Action<float>>((amountStarting) =>
            {
                healingAmountNotReduced = amountStarting;
            });

            int index = 0;
            while (c.TryGotoNext(MoveType.Before, x => x.MatchStarg(1)) && c.Index > index)
            {
                CacheHealingNotReduced();
                c.Index++;
                index = c.Index;
            }

            void CacheHealingNotReduced()
            {
                c.Emit(OpCodes.Dup);
                c.EmitDelegate<Action<float>>((amount) =>
                {
                    if (amount > healingAmountNotReduced || amount == 0)
                        healingAmountNotReduced = amount;
                });
            }


            int modifiedHealingLoc = -1; //2
            //after all modifiers are made to healing, store the modified value before it gets reduced. this wont do anything if the other matches/hooks fail
            bool b2 = c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(1),
                x => x.MatchStloc(out modifiedHealingLoc)
                );
            if (!b2)
            {
                DebugBreakpoint(nameof(HealthComponent_Heal_Pooling2), 2);
            }
            else
            {
                c.Emit(OpCodes.Ldloc, modifiedHealingLoc);
                c.EmitDelegate<Action<float>>((modifiedHealing) =>
                {
                    modifiedHealingCache = modifiedHealing;
                });
            }
            #endregion


            #region part 2: replace healing storage with unreduced healing
            if (CorpsebloomBypassEclipse.Value == false)
                FixCorpsebloomReserve(c);
            if(NkuhanaBypassEclipse.Value == false)
                FixOpinionReserve(c);
            if (modifiedHealingLoc != -1 || AegisBypassEclipse.Value == false)
                FixAegisReserve(c, modifiedHealingLoc);
            #endregion
        }

        private static void FixAegisReserve(ILCursor c, int modifiedHealingLoc)
        {
            c.Index = 0;

            bool b3 = c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt<HealthComponent>(nameof(HealthComponent.AddBarrier))
                )
                && c.TryGotoPrev(MoveType.After,
                x => x.MatchLdloc(modifiedHealingLoc)
                );
            if (!b3)
            {
                DebugBreakpoint(nameof(FixAegisReserve), 3);
                return;
            }

            c.EmitDelegate<Func<float, float>>((healingRemainder) =>
            {
                if (healingRemainder == 0 || modifiedHealingCache == 0)
                    return healingRemainder;

                //if healing wasnt cut into health, skip the math
                if (healingRemainder == modifiedHealingCache)
                    return healingAmountNotReduced;

                float healingCompFactor = healingAmountNotReduced / modifiedHealingCache;
                if (healingCompFactor <= 1)
                    return healingRemainder;

                return healingRemainder * healingCompFactor;
            });
        }

        private static float EmitCorrectedReserve(float amountIn)
        {
            //Debug.Log($"Healing In: [{amountIn}] Out: [{healingAmountNotReduced}]");
            return healingAmountNotReduced;
        }

        private static void FixOpinionReserve(ILCursor c)
        {
            c.Index = 0;
            bool b1 = c.TryGotoNext(MoveType.After,
                x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.devilOrbHealPool)),
                x => x.MatchLdarg(1)
                );
            if (b1 == false)
            {
                DebugBreakpoint(nameof(FixOpinionReserve));
                return;
            }
            c.EmitDelegate<Func<float, float>>(EmitCorrectedReserve);
        }

        private static void FixCorpsebloomReserve(ILCursor c)
        {
            c.Index = 0;
            bool b1 = c.TryGotoNext(MoveType.After,
                x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.repeatHealComponent)),
                x => x.MatchLdarg(1)
                );
            if (b1 == false)
            {
                DebugBreakpoint(nameof(FixCorpsebloomReserve));
                return;
            }
            c.EmitDelegate<Func<float, float>>(EmitCorrectedReserve);
        }
    }
}
