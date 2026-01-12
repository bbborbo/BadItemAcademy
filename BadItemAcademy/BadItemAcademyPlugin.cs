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
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete
[module: UnverifiableCode]
#pragma warning disable 
namespace BadItemAcademy
{
    [BepInDependency(R2API.LanguageAPI.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(R2API.RecalculateStatsAPI.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(R2API.ContentManagement.R2APIContentManager.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]

    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [R2APISubmoduleDependency(nameof(ContentAddition))]
    [BepInPlugin(guid, modName, version)]
    public partial class BadItemAcademyPlugin : BaseUnityPlugin
    {
        public const string guid = "com." + teamName + "." + modName;
        public const string teamName = "BadItemCouncil";
        public const string modName = "BadItemRehabilitation";
        public const string version = "1.1.0";

        internal static ConfigFile CustomConfigFile { get; set; }
        internal static ConfigEntry<bool> PoolHealingBeforeModifiers { get; set; }
        internal static ConfigEntry<bool> PoolHealingAfterIncrease { get; set; }
        internal static ConfigEntry<float> VoidBandDamageMult { get; set; }
        internal static ConfigEntry<float> VoidBandProcCoeff { get; set; }
        internal static ConfigEntry<float> NkuhanaDamageMultiplier { get; set; }
        internal static ConfigEntry<float> NkuhanaProcCoefficient { get; set; }
        internal static ConfigEntry<float> NkuhanaMaxRange { get; set; }
        internal static ConfigEntry<bool> ChangeNkuhanaHealthCalculation { get; set; }
        internal static ConfigEntry<bool> ShouldBenthicWeighSelection { get; set; }
        internal static ConfigEntry<bool> InvertBenthicWeightedSelection { get; set; }
        internal static ConfigEntry<bool> BiasBenthicWeightedSelection { get; set; }
        internal static ConfigEntry<float> AegisConversionInterval { get; set; }
        internal static ConfigEntry<float> AegisConversionRate { get; set; }
        internal static ConfigEntry<float> AegisRemovalRate { get; set; }
        internal static ConfigEntry<float> AegisForceConversionThreshold { get; set; }
        internal static ConfigEntry<int> AegisMaxFortificationStacks { get; set; }
        internal static ConfigEntry<float> AegisMaxStatBonusBase { get; set; }
        internal static ConfigEntry<float> AegisMaxStatBonusStack { get; set; }
        internal static ConfigEntry<bool> AegisRevertHealingReduction { get; set; }
        internal static ConfigEntry<bool> AegisUseFortification { get; set; }


        void Awake()
        {
            DoConfig();

            RehabNkuhanas();
            RehabSingularityBand();
            RehabBenthic();
            RehabAegis();

            SaveConfig();
        }

        private static void DoConfig()
        {
            string section = "Bad Item Rehabilitation : ";

            CustomConfigFile = new ConfigFile(Paths.ConfigPath + $"\\{modName}.cfg", true);
            CustomConfigFile.SaveOnConfigSet = false;
            #region aegis
            AegisConversionInterval = CustomConfigFile.Bind(
                section + "Aegis",
                "Aegis Conversion to Fortification Interval",
                _AegisConversionInterval,
                "Determines the rate at which cumulated barrier decay is converted to Fortification. " +
                    "By default, this is made to be a fairly high value in order to reduce the performance cost of changing stats frequently. " +
                    "However, the Force Conversion Threshold makes sure that Fortification is not added too slowly for the changing stats."
                );
            AegisConversionRate = CustomConfigFile.Bind(
                section + "Aegis",
                "Aegis Rate Of Conversion To Fortification",
                _AegisConversionRate,
                "Determines the multiplier of barrier converted to Fortification. " +
                    "For example, with a value of 0.5, 100% barrier decay only gives 50% Fortification. "
                );
            AegisRemovalRate = CustomConfigFile.Bind(
                section + "Aegis",
                "Aegis Rate Of Conversion To Fortification",
                _AegisRemovalRate,
                "Determines the multiplier of Fortification removal when taking damage, proportional to maximum health. " +
                    "For example, with a value of 2, losing 50% of your max health will take away 100% Fortification. "
                );
            AegisForceConversionThreshold = CustomConfigFile.Bind(
                section + "Aegis",
                "Aegis Force Conversion To Fortification Threshold",
                _AegisForceConversionThreshold,
                "Determines the threshold at which cumulated barrier decay is forced to be converted to Fortification, " +
                    "bypassing the cooldown and adding all cumulated barrier to Fortification instantly. " +
                    "This is shown in the item's full description as the minimum value to convert to Fortification. "
                );
            AegisMaxFortificationStacks = CustomConfigFile.Bind(
                section + "Aegis",
                "Aegis Max Fortification Stacks",
                _AegisMaxFortificationStacks,
                "Determines the maximum stacks of Fortification required for the full bonus. "
                );
            AegisMaxStatBonusBase = CustomConfigFile.Bind(
                section + "Aegis",
                "Aegis Fortification Max Stat Bonus BASE",
                _AegisMaxStatBonusBase,
                "Determines the amount Aegis increases ALL STATS with full Fortification with your first stack of Aegis. " +
                    "Scales linearly, represented as a percent. "
                );
            AegisMaxStatBonusStack = CustomConfigFile.Bind(
                section + "Aegis",
                "Aegis Fortification Max Stat Bonus PER STACK",
                _AegisMaxStatBonusStack,
                "Determines the amount Aegis increases ALL STATS with full Fortification with additional stacks of Aegis. " +
                    "Scales linearly, represented as a percent. "
                );
            AegisRevertHealingReduction = CustomConfigFile.Bind(
                section + "Aegis",
                "Should Aegis Revert Healing Reductions When Converting To Barrier?",
                _AegisRevertHealingReduction,
                "Vanilla is FALSE. If set to TRUE, Aegis will revert any healing cuts made by modifiers like Eclipse 5 when " +
                    "applying overheal to barrier."
                );
            AegisUseFortification = CustomConfigFile.Bind(
                section + "Aegis",
                "Should Aegis Use Fortification?",
                _AegisUseFortification,
                "Vanilla is FALSE. If set to TRUE, Aegis will turn all decayed barrier into a stat buff called Fortification. " +
                    "Fortification boosts ALL stats, but is reduced when taking damage by an amount proportional to your max health."
                );
            #endregion
            #region benthic
            ShouldBenthicWeighSelection = CustomConfigFile.Bind(
                section + "Benthic Bloom",
                "Should Benthic Bloom Weigh Selection?",
                _ShouldBenthicWeighSelection,
                "Vanilla is FALSE. If set to TRUE, Benthic Bloom will be biased towards selecting item stacks with higher lower values. " +
                    "Otherwise, it will prefer item stacks with higher values. " +
                    "Neither of these options resemble vanilla behavior, but you can choose to configure it anyways!"
                );
            InvertBenthicWeightedSelection = CustomConfigFile.Bind(
                section + "Benthic Bloom",
                "Invert Benthic Bloom Weighted Selection",
                _InvertBenthicWeightedSelection,
                "If set to TRUE, Benthic Bloom will be biased towards selecting item stacks with lower values. " +
                    "Otherwise, it will prefer item stacks with higher values. " +
                    "Neither of these options resemble vanilla behavior, so do what you want!"
                );
            BiasBenthicWeightedSelection = CustomConfigFile.Bind(
                section + "Benthic Bloom",
                "Bias Benthic Bloom Weighted Selection",
                _BiasBenthicWeightedSelection,
                "If set to TRUE, Benthic Bloom will try to maintain equal ratios of upgrades between Common and Uncommon items. " +
                    "Due to selection weighting, Benthic will more often pick Uncommon-to-Rare upgrades than Common-to-Uncommon due to Uncommon items being harder to stack. " +
                    "This config presents a choice, if you would like to make Benthic " +
                    "adjust its weighted selection to account for the size of your inventory or allow it to choose whatever it wants. " +
                    "Neither of these options resemble vanilla behavior, so do what you want!"
                );
            #endregion
            #region nkuhanas
            PoolHealingBeforeModifiers = CustomConfigFile.Bind(
                section + "NKuhanas Opinion",
                "Pool Healing Before Modifiers (Affects Corpsebloom)",
                _PoolHealingBeforeModifiers,
                "Vanilla is FALSE. If set to TRUE, Nkuhanas Opinion and Corpsebloom will be changed " +
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
                "Vanilla is FALSE. If set to TRUE, Nkuhanas Opinion will calculate the base damage of its attacks " +
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
            #endregion
            #region singularity band
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
            #endregion
        }

        private static void SaveConfig()
        {
            CustomConfigFile.SaveOnConfigSet = true;
            CustomConfigFile.Save();
        }

        public static AssetReferenceT<T> LoadAsync<T>(string guid, Action<T> callback) where T : UnityEngine.Object
        {
            void onCompleted(AsyncOperationHandle<T> handle)
            {
                if (!(handle.Result is T) || handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"Failed to load asset [{handle.DebugName}] : {handle.OperationException}");
                    return;
                }

                callback(handle.Result);
            }

            AssetReferenceT<T> ref1 = new AssetReferenceT<T>(guid);
            AsyncOperationHandle<T> handle = AssetAsyncReferenceManager<T>.LoadAsset(ref1);

            if (callback == null)
            {
                return ref1;
            }

            if (handle.IsDone)
            {
                onCompleted(handle);
                return ref1;
            }

            handle.Completed += onCompleted;
            return ref1;
        }
        public static void DebugBreakpoint(string methodName, int breakpointNumber = -1)
        {
            string s = $"({modName}) {methodName} IL hook failed!";
            if (breakpointNumber >= 0)
                s += $" (breakpoint {breakpointNumber})";
            Debug.LogError(s);
        }
    }
}
