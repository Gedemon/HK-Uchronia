using Amplitude.Mercury.Simulation;
using Amplitude;
using HarmonyLib;
using Amplitude.Framework;
using Amplitude.Mercury.Options;
using Amplitude.Mercury.Data.GameOptions;

namespace Gedemon.Uchronia
{
	[HarmonyPatch(typeof(CollectibleManager))]
	public class TCL_CollectibleManager
	{
		[HarmonyPostfix]
		[HarmonyPatch(nameof(InitializeOnLoad))]
		public static void InitializeOnLoad(CollectibleManager __instance)
		{
			Diagnostics.LogWarning($"[Gedemon] in CollectibleManager, InitializeOnLoad");

			if (Uchronia.LoggingStartData)
			{
				MapUtils.LogTerritoryStats();
				CultureUnlock.logEmpiresTerritories();
				MapUtils.LogTerritoryData();

				// Log all factions
				//*
				var factionDefinitions = Databases.GetDatabase<Amplitude.Mercury.Data.Simulation.FactionDefinition>();
				foreach (Amplitude.Mercury.Data.Simulation.FactionDefinition data in factionDefinitions)
				{
					Diagnostics.LogWarning($"[Gedemon] FactionDefinition name = {data.name}, era = {data.EraIndex}");//, Name = {data.Name}");
				}
				//*/
			}

			/*
			var definition = Databases.GetDatabase<Amplitude.Framework.Localization.LocalizedStringElement>();
			foreach (Amplitude.Framework.Localization.LocalizedStringElement data in definition)
			{
				for (int i = 0; i < data.CompactedNodes.Length; i++ )
                {
					Diagnostics.LogWarning($"[Gedemon] Localization {data.name} CompactedNodes[{i}] = {data.CompactedNodes[i].TextValue}");
				}
			}
			//*/


			// Log all options
			var gameOptionDefinitions = Databases.GetDatabase<GameOptionDefinition>();
			foreach (var option in gameOptionDefinitions)
			{
                IGameOptionsService gameOptions = Services.GetService<IGameOptionsService>();
                Diagnostics.LogWarning($"[Gedemon] gameOptions {option.name} = { gameOptions.GetOption(option.Name).CurrentValue}");
			}
		}
	}

}
