using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using static BadItemAcademy.BadItemAcademyPlugin;

namespace BadItemAcademy.Modules
{
    public static class Tools
    {
        internal static bool isLoaded(string modguid)
        {
            foreach (KeyValuePair<string, PluginInfo> keyValuePair in Chainloader.PluginInfos)
            {
                string key = keyValuePair.Key;
                PluginInfo value = keyValuePair.Value;
                bool flag = key == modguid;
                if (flag)
                {
                    return true;
                }
            }
            return false;
        }
    }
    public static class Assets
    {
        /// <summary>
        /// Loads an embedded asset bundle
        /// </summary>
        /// <param name="resourceBytes">The bytes returned by Properties.Resources.ASSETNAME</param>
        /// <returns>The loaded bundle</returns>
        internal static Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();

        internal static AssetBundle LoadAssetBundle(string bundleName)
        {
            if (loadedBundles.ContainsKey(bundleName))
            {
                return loadedBundles[bundleName];
            }

            AssetBundle assetBundle = null;
            assetBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(BadItemAcademyPlugin.PInfo.Location), bundleName));

            loadedBundles[bundleName] = assetBundle;

            return assetBundle;
        }
    }
    public static class Bindings
    {
        internal static bool AprilFools;

        public static bool BindSection(string sectionName)
        {
            return CustomConfigFile.Bind<bool>("Bad Item Academy : Full Section Config",
                sectionName,
                true,
                "Vanilla is FALSE. Set to false if you wish to disable changes made to an entire item or group of items.").Value;
        }
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

        internal static ConfigEntry<int> SuplicatorTempExtension   { get; set; }
        internal static ConfigEntry<int> ChaosBonusBase   { get; set; }
        internal static ConfigEntry<int> ChaosBonusStack  { get; set; }
        internal static ConfigEntry<int> ChaosWidgetCount { get; set; }
        internal static ConfigEntry<bool> ChaosBlacklistEgg        { get; set; }
        internal static ConfigEntry<bool> ChaosQueueAllowCapacitor { get; set; }
        internal static ConfigEntry<bool> ChaosQueueAllowRecycler  { get; set; }
        internal static ConfigEntry<bool> ChaosQueueAllowTricorn   { get; set; }
        internal static ConfigEntry<bool> ChaosQueueAllowLunar     { get; set; }
        internal static ConfigEntry<bool> ChaosQueueAllowEgg       { get; set; }
        internal static ConfigEntry<bool> DontUseAprilFools { get; set; }
        internal static ConfigEntry<bool> UseAprilFools { get; set; }
        public static void Init()
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
            #region bottled chaos

            ChaosBonusBase = CustomConfigFile.Bind(
                section + "Bottled Chaos",
                "Bottled Chaos Bonus Activations (Base)",
                _ChaosBonusBase,
                "Vanilla is 1. " +
                    "Determines the amount of random equipments to be activated with one stack of Bottled Chaos. " +
                    "Note that this value can be less than or higher than the number of equipments displayed by BIRs Bottled Chaos Queue-Widget."
                );
            ChaosBonusStack = CustomConfigFile.Bind(
                section + "Bottled Chaos",
                "Bottled Chaos Bonus Activations (Stack)",
                _ChaosBonusStack,
                "Vanilla is 1. " +
                    "Determines the amount of random equipments to be activated for each additional stack of Bottled Chaos. " +
                    "Note that this value can be less than or higher than the number of equipments displayed by BIRs Bottled Chaos Queue-Widget."
                );
            ChaosWidgetCount = CustomConfigFile.Bind(
                section + "Bottled Chaos",
                "Bottled Chaos Queue Display Count",
                _ChaosWidgetCount,
                "Vanilla is 0. Determines the amount of random equipments to be displayed above the equipment slot while Bottled Chaos is held. " +
                    "If the activation count is smaller than this number, the widget will behave as a queue, with equipments to the right being used after. " +
                    "If the activation count is higher than this number, or if the displayed equipment cannot be activated, " +
                    "additional random equipments will be triggered in order to fulfill the activation count. " +
                    "Note that the Widget is allowed to choose ALL equipments that can be triggered at random, " +
                    "as well as some equipments that cannot be triggered at random, such as Recycler."
                );
            ChaosBlacklistEgg = CustomConfigFile.Bind(
                section + "Bottled Chaos : Random Activation Pool",
                "Allow Random Activations Of Volcanic Egg?",
                _ChaosBlacklistEgg,
                "Vanilla is FALSE. If set to TRUE, the equipment Volcanic Egg will not be allowed to activate at random." +
                    "Note that the Widget may be allowed to choose Volcanic Egg unless the relevant setting is changed."
                );
            ChaosQueueAllowLunar = CustomConfigFile.Bind(
                section + "Bottled Chaos : Queued Activation Pool",
                "Allow Queued Activations Of Lunar Equipments?",
                _ChaosQueueAllowLunar,
                "Vanilla is FALSE. If set to TRUE, all Lunar-tier equipments in the Enigma Artifact pool will be " +
                    "added to the pool of equipments that can be chosen by the Bottled Chaos Queue-Widget."
                );
            ChaosQueueAllowEgg = CustomConfigFile.Bind(
                section + "Bottled Chaos : Queued Activation Pool",
                "Allow Queued Activations Of Volcanic Egg?",
                _ChaosQueueAllowEgg,
                "Vanilla is TRUE. If the equipment Volcanic Egg is disallowed from random activations, " +
                    "this setting controls whether it will continue to appear via the Bottled Chaos Queue-Widget. " +
                    "Set both to FALSE if you wish for Volcanic Egg to be eradicated from existence."
                );
            //ChaosQueueAllowCapacitor = CustomConfigFile.Bind(
            //    section + "Bottled Chaos : Queued Activation Pool",
            //    "Allow Queued Activations Of Royal Capacitor?",
            //    _ChaosQueueAllowCapacitor,
            //    "Vanilla is FALSE. If set to TRUE, the equipment Royal Capacitor will be " +
            //        "added to the pool of equipments that can be chosen by the Bottled Chaos Queue-Widget."
            //    );
            ChaosQueueAllowRecycler = CustomConfigFile.Bind(
                section + "Bottled Chaos : Queued Activation Pool",
                "Allow Queued Activations Of Recycler?",
                _ChaosQueueAllowRecycler,
                "Vanilla is FALSE. If set to TRUE, the equipment Recycler will be " +
                    "added to the pool of equipments that can be chosen by the Bottled Chaos Queue-Widget."
                );
            ChaosQueueAllowTricorn = CustomConfigFile.Bind(
                section + "Bottled Chaos : Queued Activation Pool",
                "Allow Queued Activations Of Trophy Hunters Tricorn?",
                _ChaosQueueAllowTricorn,
                "Vanilla is FALSE. If set to TRUE, the equipment Trophy Hunters Tricorn will be " +
                    "added to the pool of equipments that can be chosen by the Bottled Chaos Queue-Widget."
                );
            #endregion
            #region suplicator
            SuplicatorTempExtension = CustomConfigFile.Bind(
                section + "Substandard Duplicator",
                "Bottled Chaos Bonus Activations (Base)",
                _SuplicatorTempExtension,
                "Vanilla is 10. " +
                    "Determines the amount of additional time per temporary item stack granted by Substandard Duplicator."
                );
            #endregion

            AprilFools = DateTime.Now.Month == 4 && DateTime.Now.Day <= 7;
            if (!AprilFools)
            {
                UseAprilFools = CustomConfigFile.Bind("April Fools",
                    "Use April Fools",
                    false,
                    "Use April Fools' Day changes on days that are not April Fools' Day");
                AprilFools = UseAprilFools.Value;
            }
            else
            {
                DontUseAprilFools = CustomConfigFile.Bind("April Fools",
                    "Dont Use April Fools",
                    false,
                    "Dont use April Fools Day changes on days that are April Fools Day");
                AprilFools = !DontUseAprilFools.Value;
            }
        }
        public static void Save()
        {
            CustomConfigFile.SaveOnConfigSet = true;
            CustomConfigFile.Save();
        }
    }
}
