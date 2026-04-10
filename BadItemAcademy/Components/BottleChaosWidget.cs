using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace BadItemAcademy.Components
{
    public class BottleChaosWidget : MonoBehaviour
	{
		private const float bgIconValue = 0.3f;
		private const float bgIconAlpha = 0.8f;
		private static float bgIconScale = (iconScale * 2) + iconSpacing;
		private const float iconScale = 48f;
		private const float iconSpacing = 12f;

		private static Sprite _bgSprite;
		internal static Sprite bgSprite
		{
			get
			{
				if (!_bgSprite)
					_bgSprite = Addressables.LoadAssetAsync<Sprite>(RoR2BepInExPack.GameAssetPaths.Version_1_35_0.RoR2_DLC1_RandomEquipmentTrigger.texBottledChaosIcon_png).WaitForCompletion();
				return _bgSprite;
			}
		}

		private class EquipmentIconSlot
		{
			public GameObject gameObject;

			public CanvasGroup canvasGroup;

			public Image image;

			public void UpdateIcon(EquipmentDef def)
			{
				if(image)
					image.sprite = def.pickupIconSprite;
			}
		}

		private static List<BottleChaosWidget> instancesList = new List<BottleChaosWidget>();
		public static ReadOnlyCollection<BottleChaosWidget> readOnlyInstancesList = new ReadOnlyCollection<BottleChaosWidget>(BottleChaosWidget.instancesList);
		public static void RefreshAll()
        {
			if (BottleChaosWidget.readOnlyInstancesList == null || BottleChaosWidget.readOnlyInstancesList.Count == 0)
				return;
			foreach (BottleChaosWidget widget in BottleChaosWidget.readOnlyInstancesList)
			{
				widget.UpdateDisplay();
			}
		}

		private CharacterBody cachedBody = null;
		private BodyIndex cachedBodyIndex = BodyIndex.None;
		private int cachedChaosCount = int.MinValue;

		private HUD _sourceHud;

		private EquipmentIconSlot[] iconSlots;

		private void Awake()
		{
			instancesList.Add(this);
			iconSlots = new EquipmentIconSlot[Bindings.ChaosWidgetCount.Value];

			RectTransform rectTransform = this.gameObject.AddComponent<RectTransform>();
			//bottom right
			rectTransform.anchorMin = new Vector2(1f, 0f);
			rectTransform.anchorMax = new Vector2(1f, 0f);
			rectTransform.pivot = new Vector2(1f, 0f);
			rectTransform.anchoredPosition = new Vector2(-80f, 180f);
			rectTransform.sizeDelta = new Vector2(bgIconScale, bgIconScale);

			HorizontalLayoutGroup group = this.gameObject.AddComponent<HorizontalLayoutGroup>();
			group.childControlWidth = false;
			group.childControlHeight = false;
			group.spacing = iconSpacing;
			group.childAlignment = TextAnchor.MiddleCenter;
			group.padding = new RectOffset(0,0,0,0);

			//ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
			//fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			//fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			Image backgroundImage = gameObject.AddComponent<Image>();
			backgroundImage.sprite = bgSprite;
			backgroundImage.color = new Color(bgIconValue, bgIconValue, bgIconValue, bgIconAlpha);

			for(int i = 0; i < iconSlots.Length; i++)
            {
				iconSlots[i] = CreateEquipmentIconSlot(i, this.transform);
			}
		}
		void OnDestroy()
        {
			instancesList.Remove(this);
        }

		private static EquipmentIconSlot CreateEquipmentIconSlot(int index, Transform parentContainer)
        {
			GameObject slot = new GameObject("EquipmentIconSlot_" + index);
			slot.transform.SetParent(parentContainer);
			slot.SetActive(true);

			CanvasGroup group = slot.AddComponent<CanvasGroup>();
			Image image = slot.AddComponent<Image>();
			image.sprite = null;
			RectTransform rect = slot.GetComponent<RectTransform>();
			rect.sizeDelta = Vector2.one * iconScale;
			rect.localScale = Vector3.one;

			return new EquipmentIconSlot
			{
				gameObject = slot,
				canvasGroup = group,
				image = image
			};
        }

		public void UpdateDisplay()
        {
			bool refreshed = false;
			if (_sourceHud)
			{
				if (cachedBody == null || _sourceHud.targetBodyObject != cachedBody.gameObject)
				{
					RefreshCachedBody();
				}

				if (!refreshed && cachedBody.bodyIndex != cachedBodyIndex)
				{
					RefreshCachedBody();
				}
			}
			else
				RefreshCachedBody();

			void RefreshCachedBody()
            {
				refreshed = true;
				if(_sourceHud && _sourceHud.targetBodyObject != null)
				{
					cachedBody = _sourceHud.targetBodyObject.GetComponent<CharacterBody>();
					cachedBodyIndex = cachedBody.bodyIndex;
				}
                else
                {
					cachedBody = null;
					cachedBodyIndex = BodyIndex.None;
                }
            }

			cachedChaosCount = cachedBody != null && cachedBody.inventory != null ? cachedBody.inventory.GetItemCountEffective(DLC1Content.Items.RandomEquipmentTrigger) : 0;
			if (cachedChaosCount == 0)
			{
				gameObject.SetActive(false);
				return;
			}
			else
				gameObject.SetActive(true);

			BottleChaosItemBehavior behavior = cachedBody.GetComponent<BottleChaosItemBehavior>();
			if (behavior == null)
				return;
			BottleChaosBodyAttachment bcba = behavior.bodyAttachmentComponent;
			if(bcba == null)
            {
				Debug.LogError("BIA: Bottled Chaos Body Attachment not found when refreshing HUD!");
				return;
            }

			//EquipmentDef nextDef = EquipmentCatalog.GetEquipmentDef((EquipmentIndex)behavior.bodyAttachmentComponent.nextEquipments);
			//iconSlots[0].UpdateIcon(nextDef);
			for (int i = 0; i < iconSlots.Length && i < behavior.bodyAttachmentComponent.nextEquipments.Length; i++)
            {
				EquipmentDef nextDef = EquipmentCatalog.GetEquipmentDef((EquipmentIndex)behavior.bodyAttachmentComponent.nextEquipments[i]);
				iconSlots[i].UpdateIcon(nextDef);
            }
        }
		internal void Initialize(HUD hud)
		{
			UpdateDisplay();
			_sourceHud = hud;
		}
    }
}
