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
        public static void RehabSingularityBand()
        {
            AssetReferenceT<GameObject> refSnowballProjectile = new AssetReferenceT<GameObject>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_DLC1_ElementalRingVoid.ElementalRingVoidBlackHole_prefab);
            AssetAsyncReferenceManager<GameObject>.LoadAsset(refSnowballProjectile).Completed += LoadVoidBandBlast;

            LanguageAPI.Add("ITEM_ELEMENTALRINGVOID_DESC",
                $"Hits that deal <style=cIsDamage>more than 400% damage</style> also fire a black hole that " +
                $"<style=cIsUtility>draws enemies within 15m into its center</style>. " +
                $"Lasts <style=cIsUtility>5</style> seconds before collapsing, " +
                $"dealing <style=cIsDamage>{100 * VoidBandDamageMult.Value}%</style> " +
                $"<style=cStack>(+{100 * VoidBandDamageMult.Value} per stack)</style> TOTAL damage. " +
                $"Recharges every <style=cIsUtility>20</style> seconds. " +
                $"<style=cIsVoid>Corrupts all Runald's and Kjaro's Bands</style>.");
        }
        public static void LoadVoidBandBlast(AsyncOperationHandle<GameObject> obj)
        {
            GameObject prefab = obj.Result;

            ProjectileExplosion explosion = prefab.GetComponent<ProjectileExplosion>();
            if (explosion)
            {
                explosion.blastProcCoefficient = VoidBandProcCoeff.Value;
                explosion.blastDamageCoefficient = VoidBandDamageMult.Value;
            }
        }
    }
}
