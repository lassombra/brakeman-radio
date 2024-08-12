using DV;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BrakemanRadio
{
	[HarmonyPatch(typeof(CommsRadioController))]
	public class CommRadioPatch
	{
		[HarmonyPostfix]
		[HarmonyPatch(nameof(CommsRadioController.UpdateModesAvailability))]
		public static void RemoveSwitchMode(CommsRadioController __instance)
		{
			var index = __instance.allModes.IndexOf(__instance.switchControl);
			__instance.disabledModeIndices.Add(index);
			if (__instance.activeModeIndex == index)
			{
				__instance.SetNextMode();
			}
		}
		[HarmonyPostfix]
		[HarmonyPatch("Awake")]
		public static void AwakeRadio(CommsRadioController __instance)
		{
			var switcher = new GameObject();
			switcher.name = "BrakemanSwitcher";
			switcher.transform.parent = __instance.transform;
			var brakeManSwtichComponent = switcher.AddComponent<BrakeManSwitch>();
			__instance.allModes.Insert(0, brakeManSwtichComponent);
			__instance.UpdateModesAvailability();
		}
	}
}
