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
using static BadItemAcademy.Modules.Bindings;
using BadItemAcademy.Modules;
using System.Runtime.CompilerServices;

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
    [BepInDependency("com.RiskOfBrainrot.RiskierRain", BepInDependency.DependencyFlags.SoftDependency)]

    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [R2APISubmoduleDependency(nameof(ContentAddition))]
    [BepInPlugin(guid, modName, version)]
    public partial class BadItemAcademyPlugin : BaseUnityPlugin
    {
        public static PluginInfo PInfo;
        public const string guid = "com." + teamName + "." + modName;
        public const string teamName = "BadItemCouncil";
        public const string modName = "BadItemRehabilitation";
        public const string version = "1.3.4";

        private static AssetBundle _mainAssetBundle;
        public static AssetBundle mainAssetBundle
        {
            get
            {
                if (_mainAssetBundle == null)
                    _mainAssetBundle = Modules.Assets.LoadAssetBundle("baditemrehab");
                return _mainAssetBundle;
            }
            set
            {
                _mainAssetBundle = value;
            }
        }

        public static bool isExperimentalMode => Tools.isLoaded("com.RiskOfBrainrot.RiskierRain");

        void Awake()
        {
            PInfo = this.Info;
            Bindings.Init();
            CommonAssets.Init();

            if(Bindings.BindSection("NKuhanas Opinion"))
                RehabNkuhanas();
            if (Bindings.BindSection("Singularity Band"))
                RehabSingularityBand();
            if (Bindings.BindSection("Benthic Bloom"))
                RehabBenthic();
            if (Bindings.BindSection("Aegis") && !isExperimentalMode)
                RehabAegis();
            if (Bindings.BindSection("Bottled Chaos"))
                RehabBottledChaos();

            if(Bindings.AprilFools ? Bindings.DontUseAprilFools.Value : Bindings.UseAprilFools.Value)
                CloverChanges();

            Bindings.Save();
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
