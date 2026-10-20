using BadItemAcademy.Modules;
using EntityStates.VagrantNovaItem;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BadItemAcademy
{
    public partial class BadItemAcademyPlugin
    {
        public static bool _UseAlternateGloop = false;
        public static float _GloopSimpleHealthThreshold = 0.5f;
        public static float _GloopSimpleRechargeTimeBase  = 30f;
        public static float _GloopAlternateRechargeTimeBase = 60f;
        public static void RehabGloop()
        {
            if(Bindings.UseAlternateGloop.Value == false)
            {
                DoSimpleGloop();
                LanguageAPI.Add("ITEM_NOVAONLOWHEALTH_DESC", $"Falling below <style=cIsHealth>{_GloopSimpleHealthThreshold * 100}% health</style> causes you to explode, " +
                    $"dealing <style=cIsDamage>6000% base damage</style>. " +
                    $"Recharges every <style=cIsUtility>{Bindings.GloopSimpleRechargeTimeBase.Value} seconds</style> <style=cStack>(-50% per stack)</style>.");
            }
            else
            {
                DoAlternateGloop();
                LanguageAPI.Add("ITEM_NOVAONLOWHEALTH_PICKUP", "Avoiding damage will fire an electric nova.");
                LanguageAPI.Add("ITEM_NOVAONLOWHEALTH_DESC", $"Staying out of danger for <style=cIsUtility>{Bindings.GloopAlternateRechargeTimeBase.Value} seconds</style> <style=cStack>(-50% per stack)</style> causes you to explode, " +
                    $"dealing <style=cIsDamage>6000% base damage</style>. "
                    );
            }
        }
        #region simple
        internal static bool IsBelowGloopThreshold(HealthComponent healthComponent)
        {
            return healthComponent.combinedHealthFraction <= Bindings.GloopSimpleHealthThreshold.Value;
        }

        private static void DoSimpleGloop()
        {
            IL.EntityStates.VagrantNovaItem.ReadyState.OnDamaged += GloopSimpleOnDamaged;
            IL.EntityStates.VagrantNovaItem.ReadyState.FixedUpdate += GloopSimpleFixedUpdate;
            IL.EntityStates.VagrantNovaItem.RechargeState.OnEnter += GloopSimpleRecharge;
        }

        private static void GloopSimpleRecharge(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            bool b1 = c.TryGotoNext(MoveType.Before,
                x => x.MatchLdsfld<RechargeState>("baseDuration")
                );
            if (b1 == false)
            {
                DebugBreakpoint(nameof(GloopSimpleRecharge), 1);
                return;
            }
            c.Remove();
            c.Emit(OpCodes.Ldc_R4, Bindings.GloopSimpleRechargeTimeBase.Value);

            bool b2 = c.TryGotoNext(MoveType.Before,
                x => x.MatchLdloc(out _),
                x => x.MatchLdcI4(1),
                x => x.MatchAdd()
                );
            if(b2 == false)
            {
                DebugBreakpoint(nameof(GloopSimpleRecharge), 2);
                return;
            }
            c.Index++;
            c.RemoveRange(2);
        }

        private static void GloopSimpleOnDamaged(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            bool b1 = c.TryGotoNext(MoveType.Before,
                x => x.MatchLdfld<DamageReport>("hitLowHealth")
                );
            if (b1 == false)
            {
                DebugBreakpoint(nameof(GloopSimpleOnDamaged));
                return;
            }
            c.Remove();
            c.EmitDelegate<Func<DamageReport, bool>>((damageReport) => IsBelowGloopThreshold(damageReport.victim));
        }

        private static void GloopSimpleFixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            bool b1 = c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt<HealthComponent>("get_isHealthLow")
                );
            if(b1 == false)
            {
                DebugBreakpoint(nameof(GloopSimpleFixedUpdate));
                return;
            }
            c.Remove();
            c.EmitDelegate<Func<HealthComponent, bool>>(IsBelowGloopThreshold);
        }
        #endregion

        #region alternate
        private static void DoAlternateGloop()
        {
            On.EntityStates.VagrantNovaItem.RechargeState.FixedUpdate += GloopAltRecharge;
            On.RoR2.Items.NovaOnLowHealthBodyBehavior.OnDestroy += GloopAltReset;
            On.EntityStates.VagrantNovaItem.DetonateState.OnEnter += GloopAltFire;
        }

        private static void GloopAltFire(On.EntityStates.VagrantNovaItem.DetonateState.orig_OnEnter orig, DetonateState self)
        {
            orig(self);
            self.attachedBody.SetBuffCount(CommonAssets.GloopRechargeBuff.buffIndex, 0);
        }

        private static void GloopAltReset(On.RoR2.Items.NovaOnLowHealthBodyBehavior.orig_OnDestroy orig, RoR2.Items.NovaOnLowHealthBodyBehavior self)
        {
            orig(self);
            self.body.SetBuffCount(CommonAssets.GloopRechargeBuff.buffIndex, 0);
        }

        private static void GloopAltRecharge(On.EntityStates.VagrantNovaItem.RechargeState.orig_FixedUpdate orig, EntityStates.VagrantNovaItem.RechargeState self)
        {
            if (self.attachedBody.outOfDangerStopwatch >= 1f)
                self.fixedAge += Time.fixedDeltaTime;
            else
                self.fixedAge = 0;
            if (self.isAuthority)
            {
                int itemStack = self.GetItemStack();
                float rechargeTime = Bindings.GloopAlternateRechargeTimeBase.Value / (float)(itemStack);
                float rechargeFraction = self.fixedAge / rechargeTime;
                if (rechargeFraction >= 1f)
                {
                    rechargeFraction = 1f;
                    self.outer.SetNextState(new ChargeState());
                }
                self.SetChargeSparkEmissionRateMultiplier(RechargeState.particleEmissionCurve.Evaluate(rechargeFraction));
                self.attachedBody.SetBuffCount(CommonAssets.GloopRechargeBuff.buffIndex, Mathf.Clamp(Mathf.RoundToInt(rechargeFraction * 100), 0, 100));
            }
            self.SetChargeSparkEmissionRateMultiplier(RechargeState.particleEmissionCurve.Evaluate(self.fixedAge));
        }
        #endregion
    }
}
