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

    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInPlugin(guid, modName, version)]
    public partial class BadItemAcademyPlugin : BaseUnityPlugin
    {
        public const string guid = "com." + teamName + "." + modName;
        public const string teamName = "BadItemCouncil";
        public const string modName = "BadItemRehabilitation";
        public const string version = "1.0.0";

        private static bool _ShouldBenthicWeighSelection = true;
        private static bool _InvertBenthicWeightedSelection = true;
        private static bool _BiasBenthicWeightedSelection = true;

        internal static ConfigFile CustomConfigFile { get; set; }
        private static ConfigEntry<bool> ShouldBenthicWeighSelection { get; set; }
        private static ConfigEntry<bool> InvertBenthicWeightedSelection { get; set; }
        private static ConfigEntry<bool> BiasBenthicWeightedSelection { get; set; }
        internal static ConfigEntry<bool> PoolHealingBeforeModifiers { get; set; }
        internal static ConfigEntry<bool> PoolHealingAfterIncrease { get; set; }
        internal static ConfigEntry<float> VoidBandDamageMult { get; set; }
        internal static ConfigEntry<float> VoidBandProcCoeff { get; set; }
        internal static ConfigEntry<float> NkuhanaDamageMultiplier { get; set; }
        internal static ConfigEntry<float> NkuhanaProcCoefficient { get; set; }
        internal static ConfigEntry<float> NkuhanaMaxRange { get; set; }
        internal static ConfigEntry<bool> ChangeNkuhanaHealthCalculation { get; set; }


        void Awake()
        {
            DoConfig();

            RehabNkuhanas();
            RehabSingularityBand();
            RehabBenthic();
        }
        private static void DoConfig()
        {
            string section = "Bad Item Rehabilitation : ";

            CustomConfigFile = new ConfigFile(Paths.ConfigPath + $"\\{modName}.cfg", true);
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
