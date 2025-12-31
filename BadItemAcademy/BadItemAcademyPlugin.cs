using BepInEx;
using BepInEx.Configuration;
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
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BadItemAcademy
{
    [BepInDependency(R2API.LanguageAPI.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]

    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInPlugin(guid, modName, version)]
    public class BadItemAcademyPlugin : BaseUnityPlugin
    {
        public const string guid = "com." + teamName + "." + modName;
        public const string teamName = "BadItemCouncil";
        public const string modName = "BadItemRehabilitation";
        public const string version = "1.0.0";

        private static bool _PoolHealingBeforeModifiers = true;
        private static bool _PoolHealingAfterIncrease = false;
        private static float _VoidBandDamageMult = 2; //1
        private static float _VoidBandProcCoeff = 2; //1
        private static float _NkuhanaDamageMultiplier = 3.5f; //2.5
        private static float _NkuhanaProcCoefficient = 1.0f; //0.2
        private static float _NkuhanaMaxRange = 80f; //40
        private static bool _ChangeNkuhanaHealthCalculation = true;

        internal static ConfigFile CustomConfigFile { get; set; }
        private static ConfigEntry<bool> PoolHealingBeforeModifiers { get; set; }
        private static ConfigEntry<bool> PoolHealingAfterIncrease { get; set; }
        private static ConfigEntry<float> VoidBandDamageMult { get; set; }
        private static ConfigEntry<float> VoidBandProcCoeff { get; set; }
        private static ConfigEntry<float> NkuhanaDamageMultiplier { get; set; }
        private static ConfigEntry<float> NkuhanaProcCoefficient { get; set; }
        private static ConfigEntry<float> NkuhanaMaxRange { get; set; }
        private static ConfigEntry<bool> ChangeNkuhanaHealthCalculation { get; set; }

        void Awake()
        {
            DoConfig();

            AssetReferenceT<GameObject> refSnowballProjectile = new AssetReferenceT<GameObject>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_DLC1_ElementalRingVoid.ElementalRingVoidBlackHole_prefab);
            AssetAsyncReferenceManager<GameObject>.LoadAsset(refSnowballProjectile).Completed += LoadVoidBandBlast;

            if (PoolHealingBeforeModifiers.Value)
                IL.RoR2.HealthComponent.Heal += HealthComponent_Heal;
            IL.RoR2.HealthComponent.ServerFixedUpdate += NkuhanasBuff;

            LanguageAPI.Add("ITEM_NOVAONHEAL_DESC",
                $"Store <style=cIsHealing>100%</style> <style=cStack>(+100% per stack)</style> of healing as <style=cIsHealing>Soul Energy</style>. " +
                $"After your <style=cIsHealing>Soul Energy</style> reaches <style=cIsHealing>10%</style> of your " +
                (ChangeNkuhanaHealthCalculation.Value ? $"<style=cIsHealing>base health</style>, " : $"<style=cIsHealing>maximum health</style>, ") +
                $"<style=cIsDamage>fire a skull</style> that deals <style=cIsDamage>{NkuhanaDamageMultiplier.Value * 100}%</style> " +
                $"of your <style=cIsHealing>Soul Energy</style> as <style=cIsDamage>damage</style>.");
            LanguageAPI.Add("ITEM_ELEMENTALRINGVOID_DESC",
                $"Hits that deal <style=cIsDamage>more than 400% damage</style> also fire a black hole that " +
                $"<style=cIsUtility>draws enemies within 15m into its center</style>. " +
                $"Lasts <style=cIsUtility>5</style> seconds before collapsing, " +
                $"dealing <style=cIsDamage>{100 * VoidBandDamageMult.Value}%</style> " +
                $"<style=cStack>(+{100 * VoidBandDamageMult.Value} per stack)</style> TOTAL damage. " +
                $"Recharges every <style=cIsUtility>20</style> seconds. " +
                $"<style=cIsVoid>Corrupts all Runald's and Kjaro's Bands</style>.");
        }

        private static void DoConfig()
        {
            string section = "Bad Item Academy : ";

            CustomConfigFile = new ConfigFile(Paths.ConfigPath + $"\\{modName}.cfg", true);
            PoolHealingBeforeModifiers = CustomConfigFile.Bind(
                section + "NKuhanas Opinion",
                "Pool Healing Before Modifiers (Affects Corpsebloom)",
                _PoolHealingBeforeModifiers,
                "If set to TRUE, Nkuhanas Opinion and Corpsebloom will be changed " +
                    "to pool their healing before other healing modifiers. " +
                    "In Corpsebloom's case, this removes the double dipping effect with Rejuvenation Rack and Eclipse 5."
                );
            PoolHealingAfterIncrease = CustomConfigFile.Bind(
                section + "NKuhanas Opinion",
                "Pool Healing After Increase (Affects Corpsebloom)",
                _PoolHealingAfterIncrease,
                "(Requires Pool Healing Before Modifiers to be TRUE) If set to TRUE, Nkuhanas Opinion and Corpsebloom will be changed " +
                    "to pool after Rejuvenation Rack is applied, but before Eclipse 5. "
                );
            ChangeNkuhanaHealthCalculation = CustomConfigFile.Bind(
                section + "NKuhanas Opinion",
                "Change NKuhana Base Damage Calculation",
                _ChangeNkuhanaHealthCalculation,
                "If set to TRUE, Nkuhanas Opinion will calculate the base damage of its attacks " +
                    "by using your survivor's base health (scaled with level) rather than max health. "
                );
            NkuhanaDamageMultiplier = CustomConfigFile.Bind(
                section + "NKuhanas Opinion",
                "NKuhanas Damage Coefficient",
                _NkuhanaDamageMultiplier,
                "Vanilla is 2.5. Determines the damage multiplier of skulls fired " +
                    "by healing with NKuhanas Opinion. Represented as a percent. "
                );
            NkuhanaProcCoefficient = CustomConfigFile.Bind(
                section + "NKuhanas Opinion",
                "NKuhanas Proc Coefficient",
                _NkuhanaProcCoefficient,
                "Vanilla is 0.2. Determines the proc effectivness of skulls fired " +
                    "by healing with NKuhanas Opinion. "
                );
            NkuhanaMaxRange = CustomConfigFile.Bind(
                section + "NKuhanas Opinion",
                "NKuhanas Max Range",
                _NkuhanaMaxRange,
                "Vanilla is 40. Determines the maximum range of skulls fired " +
                    "by healing with NKuhanas Opinion. Represented in meters."
                );
            VoidBandDamageMult = CustomConfigFile.Bind(
                section + "Singularity Band",
                "Void Band Damage Coefficient",
                _VoidBandDamageMult,
                "Vanilla is 1. Determines the damage multiplier of the explosion from the black hole " +
                    "created by Singularity Band. Scales linearly, represented as a percent. " 
                );
            VoidBandProcCoeff = CustomConfigFile.Bind(
                section + "Singularity Band",
                "Void Band Proc Coefficient",
                _VoidBandProcCoeff,
                "Vanilla is 1. Determines the proc effectiveness of the explosion from the black hole " +
                    "created by Singularity Band."
                );
        }

        public static void DebugBreakpoint(string methodName, int breakpointNumber = -1)
        {
            string s = $"({modName}) {methodName} IL hook failed!";
            if (breakpointNumber >= 0)
                s += $" (breakpoint {breakpointNumber})";
            Debug.LogError(s);
        }

        #region idc
        private void LoadVoidBandBlast(AsyncOperationHandle<GameObject> obj)
        {
            GameObject prefab = obj.Result;

            ProjectileExplosion explosion = prefab.GetComponent<ProjectileExplosion>();
            if (explosion)
            {
                explosion.blastProcCoefficient = VoidBandProcCoeff.Value;
                explosion.blastDamageCoefficient = VoidBandDamageMult.Value;
            }
        }
        private void NkuhanasBuff(ILContext il)
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

        private void BuffNkuhanaProcCoefficient(ILCursor c)
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

        private void BuffNkuhanaRange(ILCursor c)
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

        private void FixNkuahanHealth(ILCursor c)
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

        private void BuffNkuhanaDamage(ILCursor c)
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
#endregion

        private void HealthComponent_Heal(ILContext il)
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

        private static void HyperSwap(ILCursor c, ILLabel target, ILLabel poolStart, ILLabel poolEnd, 
            out ILLabel newEnd, out ILLabel newStart, out ILLabel newTarget)
        {
            SwapLabel(c, poolEnd, target, out newEnd);

            SwapLabel(c, poolStart, poolEnd, out newStart);

            SwapLabel(c, target, poolStart, out newTarget);
        }

        private void GetOpinionLabels(ILCursor c, ILLabel target, out ILLabel poolStart, out ILLabel poolEnd)
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
        private void GetCorpsebloomLabels(ILCursor c, ILLabel target, out ILLabel poolStart, out ILLabel poolEnd)
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
            for(int i = 0; i < count; i++)
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
