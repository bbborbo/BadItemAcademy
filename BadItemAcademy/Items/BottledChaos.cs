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
using RoR2.UI;
using static BadItemAcademy.Bindings;
using R2API;

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
        private static GameObject _bottleChaosNetworkedBodyAttachment;
        public static GameObject bottleChaosNetworkedBodyAttachment
        {
            get
            {
                return _bottleChaosNetworkedBodyAttachment;
            }
        }
        internal static readonly Xoroshiro128Plus globalBottledChaosEquipmentRng = new Xoroshiro128Plus(0UL);
        public static List<EquipmentIndex> bottledChaosWidgetValidEquipment = new List<EquipmentIndex>();
        internal static int _ChaosBonusBase = 1;
        internal static int _ChaosBonusStack = 1;
        internal static int _ChaosWidgetCount = 1;
        internal static bool _ChaosBlacklistEgg = true;
        //internal static bool _ChaosQueueAllowSeedOfLife = true;
        internal static bool _ChaosQueueAllowCapacitor = true;
        internal static bool _ChaosQueueAllowRecycler = true;
        internal static bool _ChaosQueueAllowTricorn = true;
        internal static bool _ChaosQueueAllowLunar = false;
        internal static bool _ChaosQueueAllowEgg = true;
        public static void RehabBottledChaos()
        {
            CreateBottleChaosBodyAttachment();

            if(true)//ChaosWidgetCount.Value > 0)
            {
                RoR2.Run.onRunStartGlobal += GenerateChaosWidgetEquipmentList;
                On.RoR2.UI.HUD.Awake += AddBhaosWidget;
                On.RoR2.EquipmentSlot.RpcOnClientEquipmentActivationRecieved += UpdateBhaosWidgetClient;
                IL.RoR2.EquipmentSlot.OnEquipmentExecuted_byte_byte_EquipmentIndex += AlterBhaosFireCount;
                //Stage.onStageStartGlobal += RefreshChaosWidgetsOnStageStart;
                On.RoR2.CharacterMaster.OnBodyStart += OnPlayerBodyStart;
                //On.RoR2.CharacterBody.OnInventoryChanged += OnPlayerBodyInventoryChange;
            }

            IL.RoR2.EquipmentSlot.FireBossHunter += FixBossHunterChaos;

            LoadAsync<EquipmentDef>(RoR2BepInExPack.GameAssetPaths.Version_1_35_0.RoR2_Base_FireBallDash.FireBallDash_asset, (equip) =>
            {
                if (ChaosBlacklistEgg.Value)
                {
                    equip.canBeRandomlyTriggered = false;
                }
            });
        }

        //called from plugin awake
        private static void CreateBottleChaosBodyAttachment()
        {
            _bottleChaosNetworkedBodyAttachment =
                Addressables.LoadAssetAsync<GameObject>(
                        RoR2BepInExPack.GameAssetPaths.Version_1_35_0.RoR2_Base_QuestVolatileBattery.QuestVolatileBatteryAttachment_prefab
                    ).WaitForCompletion()
                    .InstantiateClone("BIA_BottleChaosBodyAttachment", true);

            if (_bottleChaosNetworkedBodyAttachment.TryGetComponent(out NetworkStateMachine nsm))
            {
                Destroy(nsm);
            }
            if (_bottleChaosNetworkedBodyAttachment.TryGetComponent(out EntityStateMachine esm))
            {
                Destroy(esm);
            }

            //NetworkIdentity ni = _bottleChaosNetworkedBodyAttachment.AddComponent<NetworkIdentity>();
            //NetworkedBodyAttachment nba = _bottleChaosNetworkedBodyAttachment.AddComponent<NetworkedBodyAttachment>();
            //nba.networkIdentity = ni;

            BottleChaosBodyAttachment bcba = _bottleChaosNetworkedBodyAttachment.AddComponent<BottleChaosBodyAttachment>();

            R2API.ContentAddition.AddNetworkedObject(_bottleChaosNetworkedBodyAttachment);
            //_bottleChaosNetworkedBodyAttachment.SetActive(false);
        }

        private static void FixBossHunterChaos(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILLabel label = c.DefineLabel();
            bool b1 = c.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<RoR2.CharacterMasterNotificationQueue>(nameof(CharacterMasterNotificationQueue.SendTransformNotification)))
                && c.TryGotoPrev(MoveType.Before,
                x => x.MatchBrfalse(out label))
                && c.TryGotoPrev(MoveType.Before,
                x => x.MatchLdarg(0),
                x => x.MatchCallOrCallvirt<EquipmentSlot>("get_characterBody")
                );
            if (!b1)
            {
                DebugBreakpoint(nameof(FixBossHunterChaos));
                return;
            }
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EquipmentSlot, bool>>((slot) =>
            {
                Debug.Log(EquipmentCatalog.GetEquipmentDef(slot.equipmentIndex).nameToken);
                //false if held eq is not tricorn
                return slot.equipmentIndex == DLC1Content.Equipment.BossHunter.equipmentIndex;
            });
            c.Emit(OpCodes.Brfalse_S, label);
        }

        public static void GenerateChaosWidgetEquipmentList(Run run)
        {
            bottledChaosWidgetValidEquipment = new List<EquipmentIndex>();
            globalBottledChaosEquipmentRng.ResetSeed(run.seed);

            foreach (EquipmentIndex equipmentIndex in EquipmentCatalog.randomTriggerEquipmentList)
            {
                EquipmentDef def = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
                ValidateEquipment(def);
            }
            if (ChaosQueueAllowLunar.Value)
            {
                foreach (EquipmentIndex equipmentIndex in EquipmentCatalog.enigmaEquipmentList)
                {
                    EquipmentDef def = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
                    if (def.isLunar)
                        ValidateEquipment(def);
                }
            }
            if (ChaosBlacklistEgg.Value && ChaosQueueAllowEgg.Value)
            {
                ValidateEquipment(RoR2Content.Equipment.FireBallDash);
            }
            if (false)//ChaosQueueAllowCapacitor.Value)
            {
                ValidateEquipment(RoR2Content.Equipment.Lightning);
            }
            if (ChaosQueueAllowRecycler.Value)
            {
                ValidateEquipment(RoR2Content.Equipment.Recycle);
            }
            if (ChaosQueueAllowTricorn.Value)
            {
                ValidateEquipment(DLC1Content.Equipment.BossHunter);
            }

            void ValidateEquipment(EquipmentDef def)
            {
                if (def
                && (!def.requiredExpansion || run.IsExpansionEnabled(def.requiredExpansion)))
                {
                    bottledChaosWidgetValidEquipment.Add(def.equipmentIndex);
                }
            }
        }

        private static void RefreshChaosWidgetsOnStageStart(Stage obj)
        {
            BottleChaosWidget.RefreshAll();
        }

        private static void OnPlayerBodyStart(On.RoR2.CharacterMaster.orig_OnBodyStart orig, CharacterMaster self, CharacterBody body)
        {
            orig(self, body);
            if (!body.isPlayerControlled)
                return;
            BottleChaosWidget.RefreshAll();
        }

        private static void OnPlayerBodyInventoryChange(On.RoR2.CharacterBody.orig_OnInventoryChanged orig, CharacterBody self)
        {
            orig(self);
            if (!self.isPlayerControlled)
                return;
            BottleChaosWidget.RefreshAll();
        }

        private static void UpdateBhaosWidgetClient(On.RoR2.EquipmentSlot.orig_RpcOnClientEquipmentActivationRecieved orig, EquipmentSlot self)
        {
            orig(self);

            BottleChaosWidget.RefreshAll();
        }

        public static int GetBhaosActivationCountFromBhaosStacks(int stack)
        {
            if (stack == 0)
                return 0;

            int chaosCountOut = ChaosBonusBase.Value + ChaosBonusStack.Value * (stack - 1);// - _ChaosWidgetCount;
            //CharacterBody cb = equipmentSlot.characterBody;

            return chaosCountOut;
        }

        private static void AddBhaosWidget(On.RoR2.UI.HUD.orig_Awake orig, RoR2.UI.HUD self)
        {
            orig(self);
            if (!NetworkServer.active)
                return;

            GameObject chaosHud = new GameObject("BIA_BottledChaosHUD");
            chaosHud.layer = LayerMask.NameToLayer("UI");

            Transform parent = self.mainContainer.transform;
            chaosHud.transform.SetParent(parent);

            BottleChaosWidget widget = chaosHud.AddComponent<BottleChaosWidget>();
            widget.Initialize(self);
            widget.gameObject.SetActive(false);
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
                int totalActivations = GetBhaosActivationCountFromBhaosStacks(stack);
                int remainingActivations = totalActivations;

                if(totalActivations > 0 && equipmentSlot.characterBody.TryGetComponent(out BottleChaosItemBehavior itemBehavior))
                {
                    BottleChaosBodyAttachment bodyAttachment = itemBehavior.bodyAttachmentComponent;
                    //this way of doing things allows a widget-equipment to be substituted for a random one, if the widget-equipment cannot be used
                    //it should not allow 
                    for(int i = 0; i < totalActivations && i < bodyAttachment.nextEquipments.Length; i++)
                    {
                        EquipmentDef def = EquipmentCatalog.GetEquipmentDef((EquipmentIndex)bodyAttachment.nextEquipments[i]);
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
                    bodyAttachment.UpdateNextEquipmentDef();
                }

                if(remainingActivations == 0)
                {
                    EffectData effectData = new EffectData();
                    effectData.origin = equipmentSlot.characterBody.corePosition;
                    effectData.SetNetworkedObjectReference(equipmentSlot.gameObject);
                    EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/RandomEquipmentTriggerProcEffect"), effectData, true);
                }

                return remainingActivations;
            });
        }
    }

    public class BottleChaosItemBehavior : BaseItemBodyBehavior
    {
        [ItemDefAssociation(useOnServer = true, useOnClient = true)]
        private static ItemDef GetItemDef() => DLC1Content.Items.RandomEquipmentTrigger;
        private GameObject bodyAttachmentObject;
        private BottleChaosBodyAttachment _bodyAttachmentComponent;
        public BottleChaosBodyAttachment bodyAttachmentComponent
        {
            get
            {
                if (_bodyAttachmentComponent == null)
                    bodyAttachmentComponent = body.GetComponentInChildren<BottleChaosBodyAttachment>();
                return _bodyAttachmentComponent;
            }
            private set
            {
                _bodyAttachmentComponent = value;
            }
        }
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

        void UpdateNextEquipmentDef(bool updateAll = false)
        {
            if (!NetworkServer.active)
                return;
            if (bodyAttachmentComponent == null)
            {
                Debug.LogError("BIA: Bottled Chaos Networked Nody Attachment not found!");
                return;
            }
            bodyAttachmentComponent.UpdateNextEquipmentDef(updateAll);
        }

        public override void OnInventoryRefresh()
        {
            base.OnInventoryRefresh();

            BottleChaosWidget.RefreshAll();
        }

        void OnEnable()
        {
            Debug.LogError("sdfbsdjhbhjsdbvsdv");
            if (NetworkServer.active)
            {
                Debug.LogError("uuuuu");
                bodyAttachmentObject = Instantiate(BadItemAcademyPlugin.bottleChaosNetworkedBodyAttachment, body.transform);
                bodyAttachmentObject.SetActive(true);
                bodyAttachmentObject.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(body.gameObject);
                bodyAttachmentComponent = bodyAttachmentObject.GetComponent<BottleChaosBodyAttachment>();
                UpdateNextEquipmentDef(true);
            }
            //if (NetworkServer.active)
            //    EquipmentSlot.onServerEquipmentActivated += OnServerEquipmentActivated;
        }

        void OnDisable()
        {
            if(NetworkServer.active)
                UpdateNextEquipmentDef();
            //EquipmentSlot.onServerEquipmentActivated -= OnServerEquipmentActivated;
        }
    }
}
