using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using System.Security;
using System.Security.Permissions;
using System.Text;
using static R2API.RecalculateStatsAPI;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete
[module: UnverifiableCode]
#pragma warning disable 
namespace BadItemAcademy
{
    public partial class BadItemAcademyPlugin
    {
        public static void CloverChanges()
        {
            LanguageAPI.Add("ITEM_CLOVER_NAME", "67 Leaf Clover");
            LanguageAPI.Add("ITEM_CLOVER_DESC", "Doubles the listed probabilities of ALL random effects <style=cStack>(+100% per stack)</style>.");

            On.RoR2.Util.CheckRoll_float_float_CharacterMaster += FixCloverMath;
            IL.RoR2.HealthComponent.TakeDamageProcess += LuckForTeddies;
            GetStatCoefficients += CloverCrit;
        }

        private static void CloverCrit(CharacterBody sender, StatHookEventArgs args)
        {
            if (!sender.inventory)
                return;
            int cloverCount = sender.inventory.GetItemCountEffective(RoR2Content.Items.Clover);
            if (cloverCount <= 0)
                return;
            args.critAdd += 10;
            args.bleedChanceAdd += 1;
        }

        private static void LuckForTeddies(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            bool b = c.TryGotoNext(MoveType.After,
                x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.bear)))
                && c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt("RoR2.Util", nameof(RoR2.Util.CheckRoll)))
                && c.TryGotoPrev(MoveType.After,
                x => x.MatchLdcR4(0));

            if (!b)
            {
                DebugBreakpoint(nameof(LuckForTeddies));
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, HealthComponent, float>>((luckIn, healthComponent) =>
            {
                if (healthComponent.body && healthComponent.body.master)
                    return healthComponent.body.master.luck;
                return luckIn;
            });
        }

        private static bool FixCloverMath(On.RoR2.Util.orig_CheckRoll_float_float_CharacterMaster orig, float percentChance, float luck, RoR2.CharacterMaster effectOriginMaster)
        {
            if (luck == 0)
                return orig(percentChance, 0, effectOriginMaster);
            //multiplies chance if luck is positive, divides chance if luck is negative
            float thingy = luck > 0 ? luck + 1 : 1 / (luck + 1);
            return orig(percentChance * (luck + 1), 0, effectOriginMaster);
        }
    }
}
