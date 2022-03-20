using Amplitude.Framework.Options;
using Amplitude.Mercury.Data.GameOptions;
using HarmonyLib;
using HumankindModTool;

namespace Gedemon.Uchronia
{
	[HarmonyPatch(typeof(OptionsManager<GameOptionDefinition>))]
	public class TCL_GameOptions
	{
		[HarmonyPatch("Load")]
		[HarmonyPrefix]
		public static bool Load(OptionsManager<GameOptionDefinition> __instance)
		{
			var myLogSource = BepInEx.Logging.Logger.CreateLogSource("True Culture Location");
			myLogSource.LogInfo("Initializing GameOptionDefinition...");
			GameOptionHelper.Initialize(Uchronia.EmpireIconsNumColumnOption, Uchronia.HistoricalDistrictsOption, Uchronia.ExtraEmpireSlots, Uchronia.StartingOutpost, Uchronia.CityMapOption, Uchronia.StartPositionList, Uchronia.UseTrueCultureLocation, Uchronia.CreateTrueCultureLocationOption, Uchronia.SettlingEmpireSlotsOption, Uchronia.FirstEraRequiringCityToUnlock, Uchronia.StartingOutpostForMinorOption, Uchronia.LargerSpawnAreaForMinorOption, Uchronia.TerritoryLossOption, Uchronia.NewEmpireSpawningOption, Uchronia.RespawnDeadPlayersOption, Uchronia.EliminateLastEmpiresOption, Uchronia.CompensationLevel, Uchronia.TerritoryLossIgnoreAI, Uchronia.TerritoryLossLimitDecisionForAI);
			myLogSource.LogInfo("GameOptionDefinition Initialized");
			BepInEx.Logging.Logger.Sources.Remove(myLogSource);
			return true;
		}
	}
}