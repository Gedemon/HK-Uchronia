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
			Uchronia.Log("Initializing GameOptionDefinition...");
			GameOptionHelper.Initialize(Uchronia.EmpireIconsNumColumnOption, Uchronia.HistoricalDistrictsOption, Uchronia.ExtraEmpireSlots, Uchronia.StartingOutpost, Uchronia.CityMapOption, Uchronia.StartPositionList, Uchronia.UseTrueCultureLocation, Uchronia.CreateTrueCultureLocationOption, Uchronia.SettlingEmpireSlotsOption, Uchronia.FirstEraRequiringCityToUnlock, Uchronia.StartingOutpostForMinorOption, Uchronia.LargerSpawnAreaForMinorOption, Uchronia.TerritoryLossOption,  Uchronia.CompensationLevel);
			return true;
		}
	}
}