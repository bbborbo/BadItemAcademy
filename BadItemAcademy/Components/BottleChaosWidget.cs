using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace BadItemAcademy.Components
{
    public class BottleChaosWidget : MonoBehaviour
	{
		private class EquipmentIconSlot
		{
			public GameObject gameObject;

			public EquipmentDef equipmentDef;

			public CanvasGroup canvasGroup;
		}

		private HUD _sourceHud;

		private float _updateTimer;

		private EquipmentIconSlot[] iconSlots;

		private void Awake()
        {
			iconSlots = new EquipmentIconSlot[BadItemAcademyPlugin._ChaosWidgetCount];

			RectTransform rectTransform = this.gameObject.AddComponent<RectTransform>();
			rectTransform.anchorMin = new Vector2(1f, 0f);
			rectTransform.anchorMax = new Vector2(1f, 0f);
			rectTransform.pivot = new Vector2(1f, 0f);
			rectTransform.anchoredPosition = new Vector2(-65f, 175f);
			rectTransform.sizeDelta = new Vector2(0f, 120f);
		}

        internal void Initialize(HUD hud)
        {
			_sourceHud = hud;
        }
    }
}
