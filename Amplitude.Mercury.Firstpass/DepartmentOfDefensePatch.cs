using Amplitude.Mercury.Simulation;
using HarmonyLib;
using Amplitude;

namespace Gedemon.Uchronia
{

	[HarmonyPatch(typeof(DepartmentOfDefense))]
	public class TCL_DepartmentOfDefense
	{

		//*
		[HarmonyPrefix]
		[HarmonyPatch(nameof(TryGetInitialArmyPosition))]
		public static bool TryGetInitialArmyPosition(DepartmentOfDefense __instance, ref bool __result, MajorEmpire majorEmpire, ref int spawnTileIndex)
		{
			int spawnLocationsLength = World.Tables.SpawnLocations.Length;
			bool oldWorldOnly = Uchronia.UseOnlyOldWorldStart();

			Diagnostics.LogWarning($"[Gedemon] in DepartmentOfDefense, TryGetInitialArmyPosition for MajorEmpire #{majorEmpire.Index}, num spawn = {spawnLocationsLength}, Only OldWorld = {oldWorldOnly}, IsCompatibleMap = {CultureUnlock.IsCompatibleMap()}, UseExtraEmpireSlots = {Uchronia.UseExtraEmpireSlots()}, UseOnlyExtraStart = {Uchronia.UseOnlyExtraStart()} ");

			if (CultureUnlock.IsCompatibleMap() && (Uchronia.UseExtraEmpireSlots() || oldWorldOnly || Uchronia.UseOnlyExtraStart()))
			{

				if (majorEmpire.Index >= spawnLocationsLength)
				{

					Hexagon.OffsetCoords[] copy = new Hexagon.OffsetCoords[majorEmpire.Index + 1];
					World.Tables.SpawnLocations.CopyTo(copy, 0);

					Hexagon.OffsetCoords extraPosition = CultureUnlock.GetExtraStartingPosition(majorEmpire.Index, oldWorldOnly);

					Diagnostics.LogWarning($"[Gedemon] Set position {extraPosition}");

					copy[majorEmpire.Index] = extraPosition;
					World.Tables.SpawnLocations = copy;
				}
				else if (CultureUnlock.HasExtraStartingPosition(majorEmpire.Index, oldWorldOnly) && (Uchronia.UseExtraEmpireSlots() || oldWorldOnly || Uchronia.UseOnlyExtraStart()))
				{

					Hexagon.OffsetCoords extraPosition = CultureUnlock.GetExtraStartingPosition(majorEmpire.Index, oldWorldOnly);

					Diagnostics.LogWarning($"[Gedemon] Replace position {World.Tables.SpawnLocations[majorEmpire.Index]} => {extraPosition}");

					World.Tables.SpawnLocations[majorEmpire.Index] = extraPosition;
				}
			}

			// Now that we have set the mandatory starting position, we can return false for the Army spawning location for the reserved pool of AI Empires
			if (!Uchronia.IsSettlingEmpire(majorEmpire.Index))
			{
				Diagnostics.LogWarning($"[Gedemon] return False for InitialArmyPosition");
				spawnTileIndex = -1;
				__result = false;
				return false;
            }

			Diagnostics.LogWarning($"[Gedemon] Calling original method...");
			return true;
		}
		//*/

		[HarmonyPatch("BeginTurnPass_SpawnNomadsIfNeeded")]
		[HarmonyPrefix]
		public static bool BeginTurnPass_SpawnNomadsIfNeeded(DepartmentOfDefense __instance, SimulationPasses.PassContext passContext, string name) //(Amplitude.Mercury.Sandbox.Sandbox __instance, object parameter)
		{
			if (!Uchronia.IsSettlingEmpire(__instance.Empire.Index))
			{
				Diagnostics.LogWarning($"[Gedemon] abort for BeginTurnPass_SpawnNomadsIfNeeded for Empire #{__instance.Empire.Index}");
				return false;
			}
			return true;
		}
	}
}
