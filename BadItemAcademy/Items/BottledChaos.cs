using BadItemAcademy.Components;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RoR2;

using RoR2.Items;
using UnityEngine.Networking;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using RoR2.Artifacts;
using UnityEngine.AddressableAssets;

[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace BadItemAcademy
{
    /// <summary>
    /// Bottled Chaos design notes:
    /// - The item is a little too unpredictable to play around, and its activations can often be difficult enough to notice and tell what happened
    /// - Because of this, it feels very low impact, especially when using equipments which already have longer cooldowns
    /// - The item is also dragged down by "inconvenient" equipments like Volcanic Egg, enough for some people to call it a "Lunar item"
    /// 
    /// Fixes:
    /// - Remove "inconvenient" equipments like Volcanic Egg
    /// - Bottled Chaos will add a new element to the HUD which will display a random equipment to be triggered on the next equipment activation
    ///     * This HUD element will likely not scale with additional stacks, only displaying the first random equipment. This is to reduce clutter and complexity
    ///     * Functionally, this will be like a pseudo-equipment slot, likely as an ItemBehavior
    ///         + If vanilla's Bottled Chaos bonus activations are decided on the server rather than the client, I can make the client's pseudo-slot "override" the first bonus
    ///     * This HUD would also allow Bottled Chaos to selectively use targeted equipments like Recycler or Royal Capacitor
    /// - If it still feels difficult to notice the effects of Bottled Chaos, I can add a local chat message or some kind of effect showing the equipments activated
    /// - If Bottled Chaos still feels low impact, the number of bonus activations provided by Bottled Chaos can be increased, both in its base amount and stacking
    /// </summary>
    public partial class BadItemAcademyPlugin
    {
        internal static readonly Xoroshiro128Plus globalBottledChaosEquipmentRng = new Xoroshiro128Plus(0UL);
        internal static List<EquipmentIndex> bottledChaosWidgetValidEquipment = new List<EquipmentIndex>();
        internal static int _ChaosBonusBase = 1;
        internal static int _ChaosBonusStack = 1;
        internal static int _ChaosWidgetCount = 1;
        public static void RehabBottledChaos()
        {
            RoR2.Run.onRunStartGlobal += (run) => 
            {
                globalBottledChaosEquipmentRng.ResetSeed(run.seed);

                foreach (EquipmentIndex equipmentIndex in EquipmentCatalog.enigmaEquipmentList)
                {
                    EquipmentDef equipmentDef = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
                    if (equipmentDef && (!equipmentDef.requiredExpansion || run.IsExpansionEnabled(equipmentDef.requiredExpansion)))
                    {
                        bottledChaosWidgetValidEquipment.Add(equipmentIndex);
                    }
                }
            };
            On.RoR2.UI.HUD.Awake += AddBhaosWidget;
            On.RoR2.EquipmentSlot.RpcOnClientEquipmentActivationRecieved += UpdateBhaosWidget;
            IL.RoR2.EquipmentSlot.OnEquipmentExecuted += AlterBhaosFireCount;
        }

        private static void UpdateBhaosWidget(On.RoR2.EquipmentSlot.orig_RpcOnClientEquipmentActivationRecieved orig, EquipmentSlot self)
        {
            orig(self);

            if(self.characterBody.TryGetComponent(out BottleChaosItemBehavior behavior))
            {
                behavior.UpdateNextEquipmentDef();
            }
        }

        public static int GetBhaosActivationCountFromBhaosStacks(int stack, EquipmentSlot equipmentSlot)
        {
            if (stack == 0)
                return 0;

            int chaosCountOut = _ChaosBonusBase + _ChaosBonusStack * (stack - 1);// - _ChaosWidgetCount;
            //CharacterBody cb = equipmentSlot.characterBody;

            return chaosCountOut;
        }

        private static void AddBhaosWidget(On.RoR2.UI.HUD.orig_Awake orig, RoR2.UI.HUD self)
        {
            orig(self);

            GameObject chaosHud = new GameObject("BIA_BottledChaosHUD");
            chaosHud.layer = LayerMask.NameToLayer("UI");

            Transform parent = self.mainContainer.transform;
            chaosHud.transform.SetParent(parent);

            BottleChaosWidget widget = chaosHud.AddComponent<BottleChaosWidget>();
            widget.Initialize(self);
        }


        private static void AlterBhaosFireCount(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            bool b1 = c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld("RoR2.DLC1Content/Items", "RandomEquipmentTrigger"),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCountEffective))
                );
            if (!b1)
            {
                DebugBreakpoint(nameof(AlterBhaosFireCount), 1);
                return;
            }

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<int, EquipmentSlot, int>>((stack, equipmentSlot) => 
            {
                int totalActivations = GetBhaosActivationCountFromBhaosStacks(stack, equipmentSlot);
                int remainingActivations = totalActivations;

                if(totalActivations > 0 && equipmentSlot.characterBody.TryGetComponent(out BottleChaosItemBehavior itemBehavior))
                {
                    //this way of doing things allows a widget-equipment to be substituted for a random one, if the widget-equipment cannot be used
                    //it should not allow 
                    for(int i = 0; i < totalActivations && i < itemBehavior.nextEquipments.Length; i++)
                    {
                        EquipmentDef def = EquipmentCatalog.GetEquipmentDef(itemBehavior.nextEquipments[i]);
                        if (equipmentSlot.PerformEquipmentAction(def))
                        {
                            remainingActivations--;

                            if(def.equipmentIndex == RoR2Content.Equipment.BFG.equipmentIndex)
                            {
                                ModelLocator component = equipmentSlot.GetComponent<ModelLocator>();
                                if (component)
                                {
                                    Transform modelTransform = component.modelTransform;
                                    if (modelTransform)
                                    {
                                        CharacterModel component2 = modelTransform.GetComponent<CharacterModel>();
                                        if (component2)
                                        {
                                            List<GameObject> itemDisplayObjects = component2.GetItemDisplayObjects(DLC1Content.Items.RandomEquipmentTrigger.itemIndex);
                                            if (itemDisplayObjects.Count > 0)
                                            {
                                                UnityEngine.Object.Instantiate<GameObject>(Addressables.LoadAssetAsync<GameObject>("RoR2/Base/BFG/ChargeBFG.prefab").WaitForCompletion(), itemDisplayObjects[0].transform);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return remainingActivations;
            });
        }
    }

    public class BottleChaosItemBehavior : BaseItemBodyBehavior
    {
        [ItemDefAssociation(useOnServer = true, useOnClient = true)]
        private static ItemDef GetItemDef() => DLC1Content.Items.RandomEquipmentTrigger;

        public EquipmentIndex[] nextEquipments;
        int cachedActivationCount;
        int cachedGestureCount;
        private EquipmentSlot equipmentSlot
        {
            get
            {
                if (body.inventory == null)
                    return null;
                return body.equipmentSlot;
                //uint activeSlot = body.inventory.activeEquipmentSlot;
                //return body.inventory.GetEquipment(activeSlot).;
            }
        }


        public static EquipmentIndex GetRandomEquipment(Xoroshiro128Plus rng, int offset)
        {
            int count = BadItemAcademyPlugin.bottledChaosWidgetValidEquipment.Count;
            int num = rng.RangeInt(0, count);
            num += offset;
            num %= count;
            return BadItemAcademyPlugin.bottledChaosWidgetValidEquipment[num];
        }

        internal void UpdateNextEquipmentDef()
        {
            if (stack == 0 || nextEquipments == null || nextEquipments.Length == 0)
                return;

            for (int i = 0; i < nextEquipments.Length; i--)
            {
                if (i + cachedActivationCount < nextEquipments.Length)//activation count is less than widget count
                {
                    nextEquipments[i] = nextEquipments[i + cachedActivationCount];
                    continue;
                }

                EquipmentIndex randomEquipment = GetRandomEquipment(BadItemAcademyPlugin.globalBottledChaosEquipmentRng, (int)(i + body.bodyIndex));
                nextEquipments[i] = randomEquipment;
            }
        }


        void Start()
        {
            nextEquipments = new EquipmentIndex[BadItemAcademyPlugin._ChaosWidgetCount];
        }

        void Update()
        {
            EquipmentSlot slot = equipmentSlot;
            if (slot)
                return;
            for(int i = 0; i < cachedActivationCount && i < nextEquipments.Length; i++)
            {
                slot.UpdateTargets(nextEquipments[i], slot.stock + cachedGestureCount > 0);
            }
        }
        public override void OnInventoryRefresh()
        {
            base.OnInventoryRefresh();
            cachedActivationCount = BadItemAcademyPlugin.GetBhaosActivationCountFromBhaosStacks(stack, equipmentSlot);
            cachedGestureCount = body.inventory != null ? body.inventory.GetItemCountEffective(RoR2Content.Items.AutoCastEquipment) : 0;
        }

        void OnEnable()
        {
            if (NetworkServer.active)
                EquipmentSlot.onServerEquipmentActivated += OnServerEquipmentActivated;
        }

        void OnDisable()
        {
            EquipmentSlot.onServerEquipmentActivated -= OnServerEquipmentActivated;
        }

        private void OnServerEquipmentActivated(EquipmentSlot slot, EquipmentIndex indexFired)
        {
            //not our equipment slot!
            if (body.equipmentSlot == null || slot != body.equipmentSlot)
                return;
        }
    }
}
