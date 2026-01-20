using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static R2API.RecalculateStatsAPI;
using static BadItemAcademy.Bindings;

using RoR2.Items;
[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace BadItemAcademy
{
    public partial class BadItemAcademyPlugin
    {

        public static BuffDef AegisFortificationBuff;

        internal static float _AegisConversionInterval = 1f;
        internal static float _AegisConversionRate = 1f;
        internal static float _AegisRemovalRate = 1f;
        internal static float _AegisForceConversionThreshold = 0.05f;
        internal static int _AegisMaxFortificationStacks = 100;
        internal static float _AegisMaxStatBonusBase = 0.2f;
        internal static float _AegisMaxStatBonusStack = 0.1f;
        internal static bool _AegisRevertHealingReduction = true;
        internal static bool _AegisUseFortification = true;

        internal static float incomingHealingCache = 0;
        internal static float modifiedHealingCache = 0;
        internal static float barrierDecayedCache = 0;
        public static void RehabAegis()
        {
            AegisFortificationBuff = ScriptableObject.CreateInstance<BuffDef>();
            AegisFortificationBuff.name = "bdBarrierFortification";
            AegisFortificationBuff.iconSprite = mainAssetBundle.LoadAsset<Sprite>("Assets/Textures/Icons/Buff/aegisbarrier");
            // Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Common/texBuffGenericShield.tif").WaitForCompletion();
            AegisFortificationBuff.buffColor = Color.yellow;
            AegisFortificationBuff.canStack = true;
            AegisFortificationBuff.isDebuff = false;
            AegisFortificationBuff.stackingDisplayMethod = BuffDef.StackingDisplayMethod.Percentage;

            ContentAddition.AddBuffDef(AegisFortificationBuff);

            GetStatCoefficients += AegisStatCoefficients;

            LanguageAPI.Add("ITEM_BARRIERONOVERHEAL_PICKUP", "Healing past full grants you a temporary barrier. Decayed barrier boosts all damage stats.");
            LanguageAPI.Add("ITEM_BARRIERONOVERHEAL_DESC", 
                $"Healing past full grants you a <style=cIsHealing>temporary barrier</style> " +
                $"for <style=cIsHealing>50% <style=cStack>(+50% per stack)</style></style> " +
                $"of the amount you <style=cIsHealing>healed</style>. " +
                (AegisUseFortification.Value == false ? "" :
                $"Every <style=cIsHealing>{AegisForceConversionThreshold.Value * 100}%</style> barrier that decays is converted into " +
                $"<style=cIsDamage>Fortification</style>, increasing damage, attack speed, and critical strike chance " +
                $"by up to <style=cIsDamage>{AegisMaxStatBonusBase.Value * 100}%</style> " +
                $"<style=cStack>(+{AegisMaxStatBonusStack.Value * 100}% per stack)</style>. " +
                $"<style=cIsDamage>Fortification</style> is lost when taking damage."));

            IL.RoR2.HealthComponent.ServerFixedUpdate += HealthComponent_ServerFixedUpdate_CumulateBarrierDecay;
            IL.RoR2.HealthComponent.Heal += HealthComponent_Heal_OverhealToBarrier;
        }


        private static void AegisStatCoefficients(CharacterBody sender, StatHookEventArgs args)
        {
            if (AegisUseFortification.Value == false)
                return;
            int itemCount = sender.inventory?.GetItemCountEffective(RoR2Content.Items.BarrierOnOverHeal) ?? 0;
            int buffCount = sender.GetBuffCount(AegisFortificationBuff);
            if(buffCount > 0 && itemCount > 0)
            {
                float aegisMaxStatBonus = AegisMaxStatBonusBase.Value + AegisMaxStatBonusStack.Value * (itemCount - 1);
                float statBonus = aegisMaxStatBonus * ((float)buffCount / (float)AegisMaxFortificationStacks.Value);
                //args.healthMultAdd += statBonus;
                //args.regenMultAdd += statBonus;
                //args.moveSpeedMultAdd += statBonus;
                args.damageMultAdd += statBonus;
                args.attackSpeedMultAdd += statBonus;
                args.critAdd += statBonus * 100;
                //args.armorTotalMult += statBonus;
            }
        }

        private static void HealthComponent_Heal_OverhealToBarrier(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            int modifiedHealingLoc = 2;

            //before any changes are made to healing, store the in value. this wont do anything if the other matches/hooks fail
            bool b1 = c.TryGotoNext(MoveType.After,
                x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.increaseHealing))
                )
                && c.TryGotoNext(MoveType.After,
                x => x.MatchStarg(1)
                );
            if (!b1)
            {
                DebugBreakpoint(nameof(HealthComponent_Heal_OverhealToBarrier), 1);
                c.Index = 0;
            }
            else
            {
                c.Index++;
            }
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Action<float>>((incomingHealing) =>
            {
                incomingHealingCache = incomingHealing;
            });

            //after all modifiers are made to healing, store the modified value before it gets reduced. this wont do anything if the other matches/hooks fail
            bool b2 = c.TryGotoNext(MoveType.After,
                x => x.MatchLdarg(1),
                x => x.MatchStloc(out modifiedHealingLoc)
                );
            if (!b2)
            {
                DebugBreakpoint(nameof(HealthComponent_Heal_OverhealToBarrier), 2);
                return;
            }
            c.Emit(OpCodes.Ldloc, modifiedHealingLoc);
            c.EmitDelegate<Action<float>>((modifiedHealing) =>
            {
                modifiedHealingCache = modifiedHealing;
            });

            //where healing gets added to barrier, we reverse . this is the only match/hook that does anything
            bool b3 = c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt<HealthComponent>(nameof(HealthComponent.AddBarrier))
                )
                && c.TryGotoPrev(MoveType.After, 
                x => x.MatchLdloc(modifiedHealingLoc)
                );
            if (!b3)
            {
                DebugBreakpoint(nameof(HealthComponent_Heal_OverhealToBarrier), 3);
                return;
            }

            c.EmitDelegate<Func<float, float>>((healingRemainder) =>
            {
                if (healingRemainder == 0 || modifiedHealingCache == 0 || AegisRevertHealingReduction.Value == false)
                    return healingRemainder;

                //if healing wasnt cut into health, skip the math
                if (healingRemainder == modifiedHealingCache)
                    return incomingHealingCache;

                float healingCompFactor = incomingHealingCache / modifiedHealingCache;
                if (healingCompFactor <= 1)
                    return healingRemainder;

                return healingRemainder * healingCompFactor;
            });
        }

        private static void HealthComponent_ServerFixedUpdate_CumulateBarrierDecay(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            bool b1 = c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt<HealthComponent>("set_Networkbarrier")
                );
            if (!b1)
            {
                DebugBreakpoint(nameof(HealthComponent_ServerFixedUpdate_CumulateBarrierDecay), 1);
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, HealthComponent, float>>((newBarrier, self) =>
            {
                barrierDecayedCache = self.barrier - newBarrier;
                return newBarrier;
            });

            c.Index++;
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<HealthComponent>>((self) =>
            {
                if (self.itemCounts.barrierOnOverHeal > 0 && NetworkServer.active && AegisUseFortification.Value == true)
                {
                    AegisItemBehavior aegisItemBehavior = self.body.GetComponent<AegisItemBehavior>();
                    if (aegisItemBehavior != null && aegisItemBehavior.stack > 0)
                        aegisItemBehavior.OnBarrierDecayed(barrierDecayedCache);
                }
                //Action<HealthComponent, float> action2 = onHealthComponentBarrierDecayedGlobal;
                //if (action2 != null)
                //{
                //    action2(self, self.barrier - newBarrier);
                //}
            });
        }
    }

    public class AegisItemBehavior : BaseItemBodyBehavior, IOnTakeDamageServerReceiver
    {
        public float decayedBarrierCumulative = 0;
        private float aegisConversionStopwatch = 0;
        private bool isAtFullFortification = false;

        [ItemDefAssociation(useOnServer = true, useOnClient = false)]
        private static ItemDef GetItemDef() => RoR2Content.Items.BarrierOnOverHeal;
        private static BuffDef GetBuffDef() => BadItemAcademyPlugin.AegisFortificationBuff;
        public HealthComponent healthComponent => this.body.healthComponent;

        void Start()
        {
            if(AegisUseFortification.Value == true)
                body?.healthComponent?.AddOnTakeDamageServerReceiver(this);
        }
        void OnDestroy()
        {
            body?.healthComponent?.RemoveOnTakeDamageServerReceiver(this);
            this.body.SetBuffCount(GetBuffDef().buffIndex, 0);
        }

        public void CumulateBarrierDecay(HealthComponent healthComponent, float barrierDecayed)
        {
            if (healthComponent == this.healthComponent)
            {
                OnBarrierDecayed(barrierDecayed);
            }
        }

        public void OnBarrierDecayed(float barrierDecayed)
        {
            if (isAtFullFortification)
                return;
            decayedBarrierCumulative += barrierDecayed * AegisConversionRate.Value;

            aegisConversionStopwatch += Time.fixedDeltaTime;
            if (this.healthComponent.barrier <= float.Epsilon 
                || decayedBarrierCumulative >= healthComponent.fullCombinedHealth * AegisForceConversionThreshold.Value
                || aegisConversionStopwatch >= AegisConversionInterval.Value)
            {
                aegisConversionStopwatch = 0;
                int buffStacks = 0;
                if(this.healthComponent.barrier <= float.Epsilon)
                {
                    buffStacks = Mathf.CeilToInt((100 * decayedBarrierCumulative) / healthComponent.fullCombinedHealth);
                    decayedBarrierCumulative = 0;
                }
                else
                {
                    float healthPerBuff = healthComponent.fullCombinedHealth / 100;
                    buffStacks = Mathf.FloorToInt(decayedBarrierCumulative / healthPerBuff);
                    decayedBarrierCumulative = decayedBarrierCumulative - (healthPerBuff * buffStacks);
                }

                int currentBuffCount = this.body.GetBuffCount(GetBuffDef().buffIndex);
                int newBuffCount = currentBuffCount + buffStacks;
                if (newBuffCount >= AegisMaxFortificationStacks.Value)
                {
                    newBuffCount = AegisMaxFortificationStacks.Value;
                    isAtFullFortification = true;
                }
                this.body.SetBuffCount(GetBuffDef().buffIndex, newBuffCount);
            }
        }

        public void OnTakeDamageServer(DamageReport damageReport)
        {
            HealthComponent victimHealthComponent = damageReport.victimBody?.healthComponent;
            if (damageReport.victimBody?.healthComponent == null)
                return;

            int currentBuffCount = this.body?.GetBuffCount(GetBuffDef().buffIndex) ?? 0;
            if (currentBuffCount == 0)
                return;

            float healthLost = damageReport.combinedHealthBeforeDamage - victimHealthComponent.combinedHealth;
            int buffStacksToLose = Mathf.FloorToInt((100 * healthLost * AegisRemovalRate.Value) / victimHealthComponent.fullCombinedHealth);

            damageReport.victimBody.SetBuffCount(GetBuffDef().buffIndex, Mathf.Max(currentBuffCount - buffStacksToLose, 0));

            isAtFullFortification = false;
        }
    }
}
