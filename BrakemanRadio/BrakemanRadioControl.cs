using System;
using System.Reflection;
using dnlib;
using HarmonyLib;
using UnityModManagerNet;

namespace BrakemanRadio;

public static class BrakemanRadioControl
{
	internal static UnityModManager.ModEntry.ModLogger logger;
	public static Settings Settings { get; private set; }

	// Unity Mod Manage Wiki: https://wiki.nexusmods.com/index.php/Category:Unity_Mod_Manager


	private static bool Load(UnityModManager.ModEntry modEntry)
	{
		logger = modEntry.Logger;
		Settings = Settings.Load<Settings>(modEntry);
		modEntry.OnGUI += OnGui;
		modEntry.OnSaveGUI = OnSaveGui;
		Harmony harmony = null;

		try
		{
			harmony = new Harmony(modEntry.Info.Id);
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			WorldStreamingInit.LoadingFinished += Start;
			if (WorldStreamingInit.IsLoaded)
			{
				Start();
			}
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

	private static void Start()
	{
		PlayerManager.CarChanged += PlayerManager_CarChanged;
	}

	private static void OnSaveGui(UnityModManager.ModEntry entry)
	{
		Settings.Save(entry);
	}

	private static void OnGui(UnityModManager.ModEntry entry)
	{
		Settings.Draw(entry);
	}

	internal static void Debug(string message)
	{
		if (Settings.DebugLogging)
		{
			logger.Log(message);
		}
	}

	private static void PlayerManager_CarChanged(TrainCar obj)
	{
		TrainReverseMonitor.Instance.Car = obj;
	}
}
