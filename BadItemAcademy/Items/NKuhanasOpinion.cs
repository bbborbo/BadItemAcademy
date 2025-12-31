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

namespace BadItemAcademy
{
    public partial class BadItemAcademyPlugin
    {
        public static void RehabNkuhanas()
        {
            if (PoolHealingBeforeModifiers.Value)
                IL.RoR2.HealthComponent.Heal += HealthComponent_Heal;
            IL.RoR2.HealthComponent.ServerFixedUpdate += NkuhanasBuff;

            LanguageAPI.Add("ITEM_NOVAONHEAL_DESC",
                $"Store <style=cIsHealing>100%</style> <style=cStack>(+100% per stack)</style> of healing as <style=cIsHealing>Soul Energy</style>. " +
                $"After your <style=cIsHealing>Soul Energy</style> reaches <style=cIsHealing>10%</style> of your " +
                (ChangeNkuhanaHealthCalculation.Value ? $"<style=cIsHealing>base health</style>, " : $"<style=cIsHealing>maximum health</style>, ") +
                $"<style=cIsDamage>fire a skull</style> that deals <style=cIsDamage>{NkuhanaDamageMultiplier.Value * 100}%</style> " +
                $"of your <style=cIsHealing>Soul Energy</style> as <style=cIsDamage>damage</style>.");
        }


        public static void NkuhanasBuff(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            bool b0 = c.TryGotoNext(MoveType.After,
                x => x.MatchLdfld<HealthComponent>(nameof(HealthComponent.devilOrbHealPool))
                );
            int index = c.Index;

            BuffNkuhanaDamage(c);
            if (ChangeNkuhanaHealthCalculation.Value)
            {
                c.Index = index;
                FixNkuahanHealth(c);
            }
            c.Index = index;
            BuffNkuhanaRange(c);
            c.Index = index;
            BuffNkuhanaProcCoefficient(c);
        }

        public static void BuffNkuhanaProcCoefficient(ILCursor c)
        {
            int index = 9;
            bool b = c.TryGotoNext(MoveType.After,
                x => x.MatchNewobj<DevilOrb>(),
                x => x.MatchStloc(out index)
                );
            if (!b)
            {
                DebugBreakpoint(nameof(BuffNkuhanaProcCoefficient));
                return;
            }
            c.Emit(OpCodes.Ldloc, index);
            c.EmitDelegate<Action<DevilOrb>>((devilOrb) =>
            {
                devilOrb.procCoefficient = NkuhanaProcCoefficient.Value;
            });
        }

        public static void BuffNkuhanaRange(ILCursor c)
        {
            bool b3 = c.TryGotoNext(MoveType.Before,
                x => x.MatchLdcR4(out _),
                x => x.MatchCallOrCallvirt<DevilOrb>(nameof(DevilOrb.PickNextTarget))
                );
            if (!b3)
            {
                DebugBreakpoint(nameof(BuffNkuhanaRange));
                return;
            }

            c.Remove();
            c.Emit(OpCodes.Ldc_R4, NkuhanaMaxRange.Value);
        }

        public static void FixNkuahanHealth(ILCursor c)
        {
            bool b2 = c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt<HealthComponent>("get_fullCombinedHealth")
                );

            if (!b2)
            {
                DebugBreakpoint(nameof(FixNkuahanHealth));
                return;
            }

            c.Remove();
            c.EmitDelegate<Func<HealthComponent, float>>((hc) =>
            {
                CharacterBody body = hc.body;
                if (!body)
                    return hc.fullCombinedHealth;
                float level = body.level - 1;
                float baseMaxHealth = body.baseMaxHealth + body.baseMaxShield + ((body.levelMaxHealth + body.levelMaxShield) * level);
                return baseMaxHealth;
            });
        }

        public static void BuffNkuhanaDamage(ILCursor c)
        {
            bool b1 = c.TryGotoNext(MoveType.Before,
                x => x.MatchStfld<DevilOrb>(nameof(DevilOrb.damageValue))
                );

            if (!b1)
            {
                DebugBreakpoint(nameof(BuffNkuhanaDamage));
                return;
            }
            c.Index -= 2;
            c.Remove();
            c.Emit(OpCodes.Ldc_R4, NkuhanaDamageMultiplier.Value);
        }

        public static void HealthComponent_Heal(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            ILLabel _target = c.DefineLabel();
            ILLabel target = c.DefineLabel();

            bool labelFound = c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(out _),
                X => X.MatchLdflda<HealthComponent>(nameof(RoR2.HealthComponent.itemCounts)),
                x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(RoR2.HealthComponent.itemCounts.increaseHealing))
                );

            if (!labelFound)
            {
                DebugBreakpoint(nameof(HealthComponent_Heal));
                return;
            }

            if (!PoolHealingAfterIncrease.Value ||
                    c.TryGotoNext(MoveType.After, x => x.MatchBle(out target)) == false
                )
            {
                if (PoolHealingAfterIncrease.Value)
                    Debug.Log($"({modName}) " +
                        $"Error: {nameof(PoolHealingAfterIncrease)} is true, but failed to find label. " +
                        $"Falling back to [{nameof(PoolHealingAfterIncrease)}=false] label.");
                c.MarkLabel(target);
            }
            _target = target;


            int opinionWithinTrue = 1;
            int opinionWithinFalse = 1;

            GetOpinionLabels(c, target, out ILLabel poolStart, out ILLabel poolEnd);

            HyperSwap(c, target, poolStart, poolEnd,
                out ILLabel newEnd, out ILLabel newStart, out ILLabel newTarget);

            //set the target to the new target label so that what comes after knows its there
            target = newTarget;

            RedirectAllBranchesToLabel(c, poolEnd, newEnd, opinionWithinTrue, ScreamingInternally.Brtrue);
            RedirectAllBranchesToLabel(c, poolEnd, newEnd, opinionWithinFalse, ScreamingInternally.Ble);


            int corpseWithinTrue = 1;
            int corpseWithinFalse = 2;
            int corpseBeforeTrue = 1;
            int corpseBeforeFalse = 1;
            //corpsebloom should be second because that puts it first, and corpsebloom can ret
            GetCorpsebloomLabels(c, target, out poolStart, out poolEnd);

            HyperSwap(c, target, poolStart, poolEnd,
                out newEnd, out newStart, out newTarget);

            RedirectAllBranchesToLabel(c, poolEnd, newEnd, corpseWithinTrue, ScreamingInternally.Brtrue);
            RedirectAllBranchesToLabel(c, poolEnd, newEnd, corpseWithinFalse, ScreamingInternally.Brfalse);
            RedirectAllBranchesToLabel(c, poolStart, newStart, 1, ScreamingInternally.Blt);
            RedirectAllBranchesToLabel(c, poolStart, newStart, 1, ScreamingInternally.Bne);

            //this here fixes the if statement before to not skip our newly emitted branches
            Debug.Log("bia redirecting branches before target to acknowledge inserted code. true search");
            RedirectAllBranchesToLabel(c, _target, newTarget, 1);

            c.Method.RecalculateILOffsets();

            //Debug.LogWarning(il.ToString());
        }

        public static void HyperSwap(ILCursor c, ILLabel target, ILLabel poolStart, ILLabel poolEnd,
            out ILLabel newEnd, out ILLabel newStart, out ILLabel newTarget)
        {
            SwapLabel(c, poolEnd, target, out newEnd);

            SwapLabel(c, poolStart, poolEnd, out newStart);

            SwapLabel(c, target, poolStart, out newTarget);
        }

        public static void GetOpinionLabels(ILCursor c, ILLabel target, out ILLabel poolStart, out ILLabel poolEnd)
        {
            poolStart = c.DefineLabel();
            poolEnd = c.DefineLabel();
            Debug.Log("bia opinion");

            c.Index = 0;

            //to find the pool start, first go to nova on heal count. this is a unique match
            bool b1 = c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(out _),
                X => X.MatchLdflda<HealthComponent>(nameof(RoR2.HealthComponent.itemCounts)),
                x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(RoR2.HealthComponent.itemCounts.novaOnHeal))
                );
            if (!b1)
            {
                DebugBreakpoint(nameof(GetOpinionLabels), 1);
                return;
            }
            c.MarkLabel(poolStart);

            //next go back up to nonRegen==true. this gets the end of the if statement and places us at the start
            bool b2 = c.TryGotoNext(MoveType.After,
                x => x.MatchStfld<HealthComponent>(nameof(HealthComponent.devilOrbHealPool))
                );
            if (!b2)
            {
                DebugBreakpoint(nameof(GetOpinionLabels), 2);
                return;
            }
            c.MarkLabel(poolEnd);
        }
        public static void GetCorpsebloomLabels(ILCursor c, ILLabel target, out ILLabel poolStart, out ILLabel poolEnd)
        {
            poolStart = c.DefineLabel();
            poolEnd = c.DefineLabel();
            Debug.Log("bia corpsebloom");

            c.Index = 0;

            //to find the pool start, first go to repeat heal component. this is a unique match
            bool b1 = c.TryGotoNext(MoveType.Before,
                x => x.MatchLdfld<HealthComponent>(nameof(RoR2.HealthComponent.repeatHealComponent))
                );
            if (!b1)
            {
                DebugBreakpoint(nameof(GetCorpsebloomLabels), 1);
                return;
            }

            //next go back up to nonRegen==true. this gets the end of the if statement and places us at the start
            ILLabel end = c.DefineLabel();
            bool b2 = c.TryGotoPrev(MoveType.Before,
                x => x.MatchLdarg(out _),
                x => x.MatchBrfalse(out end)
                );
            if (!b2)
            {
                DebugBreakpoint(nameof(GetCorpsebloomLabels), 2);
                return;
            }
            poolEnd = end;
            c.MarkLabel(poolStart);
        }

        /// <summary>
        /// "Moves" the logic at the destination label to the location label
        /// </summary>
        /// <param name="c">cursor</param>
        /// <param name="location">the label you want to "move" e.g. the start of a block</param>
        /// <param name="destination">the destination you want to "move" to e.g. a spot before some other code runs</param>
        /// <param name="newLabel">a label for the new op code created to act like a "portal". use this to redirect other methods to your new logic</param>
        private static void SwapLabel(ILCursor c, ILLabel location, ILLabel destination, out ILLabel newLabel)
        {
            c.GotoLabel(location, MoveType.Before);
            c.Emit(OpCodes.Br, destination);
            c.Index--;
            newLabel = c.DefineLabel();
            c.MarkLabel(newLabel);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="c">cursor</param>
        /// <param name="labelOld">the label to the old instruction that you need to redirect. this is used as a starting point</param>
        /// <param name="labelNew">the new label that you want branches to point to</param>
        /// <param name="count">the amount of branches to fix</param>
        /// <param name="isFalse">whether to check if the branch matches true or false</param>
        private static void RedirectAllBranchesToLabel(ILCursor c, ILLabel labelOld, ILLabel labelNew, int count, ScreamingInternally screamingInternally = ScreamingInternally.Brfalse)
        {
            if (count <= 0)
                return;
            c.GotoLabel(labelOld, MoveType.Before);

            bool b3 = true;
            for (int i = 0; i < count; i++)
            {
                OpCode opCode = OpCodes.Nop;
                switch (screamingInternally)
                {
                    default:
                        b3 = false;
                        break;
                    case ScreamingInternally.Brfalse:
                        opCode = OpCodes.Brfalse_S;
                        b3 = c.TryGotoPrev(MoveType.Before,
                            x => x.MatchBrfalse(out _)
                            );
                        break;
                    case ScreamingInternally.Brtrue:
                        opCode = OpCodes.Brtrue_S;
                        b3 = c.TryGotoPrev(MoveType.Before,
                            x => x.MatchBrtrue(out _)
                            );
                        break;
                    case ScreamingInternally.Blt:
                        opCode = OpCodes.Blt_S;
                        b3 = c.TryGotoPrev(MoveType.Before,
                            x => x.MatchBlt(out _)
                            );
                        break;
                    case ScreamingInternally.Bne:
                        opCode = OpCodes.Bne_Un_S;
                        b3 = c.TryGotoPrev(MoveType.Before,
                            x => x.MatchBneUn(out _)
                            );
                        break;
                    case ScreamingInternally.Ble:
                        opCode = OpCodes.Ble_S;
                        b3 = c.TryGotoPrev(MoveType.Before,
                            x => x.MatchBle(out _)
                            );
                        break;
                }

                if (b3 == false)
                {
                    Debug.Log("bia redirect exiting");
                    break;
                }

                //c.Next.Operand = labelNew;
                c.Remove();
                c.Emit(opCode, labelNew);
                c.Index--;
                Debug.Log(c.Prev.OpCode.ToString());
            }
        }

        enum ScreamingInternally
        {
            Brfalse,
            Brtrue,
            Blt,
            Bne,
            Ble
        }
    }
}
