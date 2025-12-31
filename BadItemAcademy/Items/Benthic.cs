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
        public static void RehabBenthic()
        {
            if (ShouldBenthicWeighSelection.Value)
                IL.RoR2.CharacterMaster.TryCloverVoidUpgrades += CloverWeightedSelection;
        }

        public static void CloverWeightedSelection(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            bool b = c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt("RoR2.Util", nameof(Util.ShuffleList))
                );
            if (!b)
            {
                DebugBreakpoint(nameof(CloverWeightedSelection));
                return;
            }
            c.Remove();
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<List<ItemIndex>, Xoroshiro128Plus, CharacterMaster>>(
                (itemList, rng, master) =>
                CreateShuffledListFromWeightedSelection(ref itemList, rng, master));
        }
        public static void CreateShuffledListFromWeightedSelection(ref List<ItemIndex> list, Xoroshiro128Plus rng, CharacterMaster master)
        {
            if (master.inventory == null
                || master.inventory.GetItemCountEffective(DLC1Content.Items.CloverVoid.itemIndex) <= 0
                || list == null || list.Count < 0)
            {
                return;
            }

            int itemCountCommon = master.inventory.GetTotalItemCountOfTier(ItemTier.Tier1);
            int itemCountUncommon = master.inventory.GetTotalItemCountOfTier(ItemTier.Tier2);
            if (itemCountCommon + itemCountUncommon == 0)
            {
                Debug.Log("Could not create a Benthic weighted selection: No Tier 1/2 Items");
                return;
            }
            WeightedSelection<ItemIndex> weightedSelection = new WeightedSelection<ItemIndex>(list.Count);
            foreach (ItemIndex itemIndex in list)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                if (itemDef == null || itemDef.canRemove == false)
                    continue;

                int baseWeightStrength = 1;
                switch (itemDef.tier)
                {
                    default:
                        continue;
                    case ItemTier.Tier1:
                        baseWeightStrength = itemCountUncommon / (itemCountCommon + itemCountUncommon);
                        break;
                    case ItemTier.Tier2:
                        baseWeightStrength = itemCountCommon / (itemCountCommon + itemCountUncommon);
                        break;
                }

                int countInInventory = master.inventory.GetItemCountEffective(itemIndex);
                float weightInverse = 1 / countInInventory;
                float weight = InvertBenthicWeightedSelection.Value ? weightInverse : countInInventory;
                if (BiasBenthicWeightedSelection.Value)
                    weight *= baseWeightStrength;
                weightedSelection.AddChoice(itemIndex, weight);
            }

            list = new List<ItemIndex>(weightedSelection.Count);
            for (int i = 0; i < list.Count; i++)
            {
                if (weightedSelection.Count <= 0 || weightedSelection.totalWeight == 0)
                    break;
                int index = weightedSelection.EvaluateToChoiceIndex(rng.nextNormalizedFloat);
                if (index >= weightedSelection.Count)
                    continue;
                list[i] = weightedSelection.choices[index].value;
                weightedSelection.ModifyChoiceWeight(index, 0);
            }
        }
    }
}
