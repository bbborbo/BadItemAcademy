using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BadItemAcademy.Modules
{
    public static class CommonAssets
    {
        public static BuffDef AegisFortificationBuff;
        public static BuffDef GloopRechargeBuff;
        public static void Init()
        {
            CreateAegisFortification();
            CreateGloopRechargeIndication();
        }

        private static void CreateGloopRechargeIndication()
        {
            GloopRechargeBuff = ScriptableObject.CreateInstance<BuffDef>();
            GloopRechargeBuff.name = "bdGenesisRecharge";
            GloopRechargeBuff.iconSprite = BadItemAcademyPlugin.mainAssetBundle.LoadAsset<Sprite>("Assets/Textures/Icons/Buff/glooprecharge.png");
            // Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Common/texBuffGenericShield.tif").WaitForCompletion();
            GloopRechargeBuff.buffColor = Color.white;
            GloopRechargeBuff.canStack = true;
            GloopRechargeBuff.isDebuff = false;
            GloopRechargeBuff.stackingDisplayMethod = BuffDef.StackingDisplayMethod.Percentage;

            ContentAddition.AddBuffDef(GloopRechargeBuff);
        }

        private static void CreateAegisFortification()
        {
            AegisFortificationBuff = ScriptableObject.CreateInstance<BuffDef>();
            AegisFortificationBuff.name = "bdBarrierFortification";
            AegisFortificationBuff.iconSprite = BadItemAcademyPlugin.mainAssetBundle.LoadAsset<Sprite>("Assets/Textures/Icons/Buff/aegisbarrier.png");
            // Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Common/texBuffGenericShield.tif").WaitForCompletion();
            AegisFortificationBuff.buffColor = Color.white;
            AegisFortificationBuff.canStack = true;
            AegisFortificationBuff.isDebuff = false;
            AegisFortificationBuff.stackingDisplayMethod = BuffDef.StackingDisplayMethod.Percentage;

            ContentAddition.AddBuffDef(AegisFortificationBuff);
        }
    }
}
