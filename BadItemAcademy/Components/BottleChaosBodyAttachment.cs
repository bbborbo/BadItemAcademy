using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using static BadItemAcademy.Bindings;

namespace BadItemAcademy.Components
{
    public class BottleChaosBodyAttachment : NetworkBehaviour
    {
        public SyncListInt nextEquipments;
        private CharacterBody _attachedBody;

        public int cachedActivationCount;
        public int cachedGestureCount;
        public CharacterBody attachedBody
        {
            get
            {
                if(_attachedBody == null)
                    attachedBody = this.transform.parent.GetComponent<CharacterBody>();
                return _attachedBody;
            }
            set
            {
                _attachedBody = value;
                if (attachedBody.inventory)
                {
                    attachedBody.inventory.onInventoryChanged += RecalculateEquipmentFireCounts;
                    RecalculateEquipmentFireCounts();
                }
            }
        }

        private void RecalculateEquipmentFireCounts()
        {
            Inventory inv = attachedBody != null ? attachedBody.inventory : null;
            if (inv == null)
                return;
            int cached = BadItemAcademyPlugin.GetBhaosActivationCountFromBhaosStacks(inv.GetItemCountEffective(DLC1Content.Items.RandomEquipmentTrigger));
            if (cachedActivationCount != cached)
                BottleChaosWidget.RefreshAll();
            cachedActivationCount = cached;
            cachedGestureCount = attachedBody.inventory.GetItemCountEffective(RoR2Content.Items.AutoCastEquipment);
        }

        public void Awake()
        {
            nextEquipments = new SyncListInt();
            for(int i = 0; i < ChaosWidgetCount.Value; i++)
                nextEquipments.Add(0);
        }
        public void OnEnable()
        {
            RecalculateEquipmentFireCounts();
            UpdateNextEquipmentDef(true);
        }
        public void OnDisable()
        {
            UpdateNextEquipmentDef(false);
            if (attachedBody && attachedBody.inventory)
                attachedBody.inventory.onInventoryChanged -= RecalculateEquipmentFireCounts;

        }
        void Start()
        {
            if (this.transform.GetParent() != null)
                attachedBody = this.transform.parent.GetComponent<CharacterBody>();
        }

        public EquipmentIndex GetRandomEquipment(Xoroshiro128Plus rng, int offset)
        {
            List<EquipmentIndex> validEquipments = new List<EquipmentIndex>(BadItemAcademyPlugin.bottledChaosWidgetValidEquipment);
            if (validEquipments == null)
                return EquipmentIndex.None;

            TryRemove(attachedBody.equipmentSlot.equipmentIndex);
            foreach(int equip in this.nextEquipments)
                TryRemove((EquipmentIndex)equip);

            void TryRemove(EquipmentIndex index)
            {
                if (validEquipments.Contains(index))
                    validEquipments.Remove(index);
            }
            if (validEquipments.Count == 0)
                return EquipmentIndex.None;

            int count = validEquipments.Count;
            int num = rng.RangeInt(0, count);
            num += offset;
            num %= count;
            return validEquipments[num];
        }

        public void UpdateNextEquipmentDef(bool updateAll = false)
        {
            if ((cachedActivationCount == 0  || nextEquipments == null || nextEquipments.Count <= 0)
                )//&& !updateAll)
            {
                BottleChaosWidget.RefreshAll();
                return;
            }


            //EquipmentIndex randomEquipment = GetRandomEquipment(BadItemAcademyPlugin.globalBottledChaosEquipmentRng, (int)(0 + attachedBody.bodyIndex));
            //nextEquipments = (int)randomEquipment;
            for (int i = 0; i < nextEquipments.Count; i++)
            {
                if (!updateAll && i + cachedActivationCount < nextEquipments.Count)//activation count is less than widget count
                {
                    if (nextEquipments[i + cachedActivationCount] != (int)EquipmentIndex.None)
                    {
                        nextEquipments[i] = nextEquipments[i + cachedActivationCount];
                        continue;
                    }
                }
            
                EquipmentIndex randomEquipment = GetRandomEquipment(BadItemAcademyPlugin.globalBottledChaosEquipmentRng, (int)(i + attachedBody.bodyIndex));
                nextEquipments[i] = (int)randomEquipment;
            }

            BottleChaosWidget.RefreshAll();
        }

        void LateUpdate()
        {
            if (transform.GetParent() == null)
                return;
            EquipmentSlot slot = attachedBody.equipmentSlot;
            if (!slot)
                return;

            //if ((EquipmentIndex)nextEquipments == slot.equipmentIndex)
            //    return;
            //
            //slot.UpdateTargets((EquipmentIndex)nextEquipments, slot.stock + cachedGestureCount > 0);
            for (int i = 0; i < cachedActivationCount && i < nextEquipments.Count; i++)
            {
                if (slot.targetIndicator.active)
                    break;
                if (slot.currentTarget.transformToIndicateAt != null)
                    break;
            
                if (nextEquipments[i] == (int)slot.equipmentIndex)
                    continue;
            
                slot.UpdateTargets((EquipmentIndex)nextEquipments[i], slot.stock + cachedGestureCount > 0);
            }
        }
    }
}
