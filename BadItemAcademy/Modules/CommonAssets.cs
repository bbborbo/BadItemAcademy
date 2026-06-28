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
        public static void Init()
        {
            CreateAegisFortification();
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
