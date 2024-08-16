using System;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

namespace BrakemanRadio;

public static class Main
{
	internal static UnityModManager.ModEntry.ModLogger logger;

	// Unity Mod Manage Wiki: https://wiki.nexusmods.com/index.php/Category:Unity_Mod_Manager


	private static bool Load(UnityModManager.ModEntry modEntry)
	{
		logger = modEntry.Logger;
		Harmony harmony = null;

		try
		{
			harmony = new Harmony(modEntry.Info.Id);
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			PlayerManager.CarChanged += PlayerManager_CarChanged;
			// Other plugin startup logic
		}
		catch (Exception ex)
		{
			modEntry.Logger.LogException($"Failed to load {modEntry.Info.DisplayName}:", ex);
			harmony?.UnpatchAll(modEntry.Info.Id);
			return false;
		}

		return true;
	}

	private static void PlayerManager_CarChanged(TrainCar obj)
	{
		TrainReverseMonitor.Instance.Car = obj;
	}
}
