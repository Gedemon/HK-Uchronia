using HarmonyLib;
using Amplitude.Mercury.UI;
using UnityEngine;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.Presentation;
using Amplitude.UI;
using Diagnostics = Amplitude.Diagnostics;
using System.Text;
using System;
using Amplitude.Mercury;
using Amplitude.Mercury.Simulation;
using System.Collections.Generic;
using Amplitude.Mercury.Data.Simulation;
using Amplitude;
using Utils = Amplitude.Mercury.Utils;
using Amplitude.Mercury.Sandbox;
using System.Collections;
using Amplitude.UI.Renderers;
using Amplitude.UI.Interactables;
using Amplitude.Mercury.UI.Tooltips;

namespace Gedemon.Uchronia
{
    class UI
	{
		//static UILabel DistrictConstructionLabel;
		//static string DistrictConstructionLabelString;
		//static TitleAndDescription DistrictConstructionLabelTooltip;

		public enum TextColor
		{
			Blue,
			Orange,
			Red,
			Green,
		}
		static bool TryGetColorString(TextColor colorNum, out string color)
		{
			switch(colorNum)
			{
				case TextColor.Blue:
					color = "198CFF";
					return true;
				case TextColor.Green:
					color = "66CC00";
					return true;
				case TextColor.Orange:
					color = "FFB319";
					return true;
				case TextColor.Red:
					color = "FF1919";
					return true;

			}
			color = "";
			return false;
		}
		public static string ColorTo(string text, TextColor colorNum)
		{
			if(TryGetColorString(colorNum, out string textColor))
            {
				return $"<c={textColor}>{text}</c>";
			}
			return text;			
		}

		public static void UpdateObjects()
        {
			//if (DistrictConstructionLabel == null)
			//{
			//	Uchronia.Log($"[UI][UpdateObjects] Try get DistrictConstructionLabel");
			//
			//	var label = GameObject.Find("ConstructibleSectionTitle");
			//	if (label != null)
			//	{
			//		DistrictConstructionLabel = label.GetComponent<UILabel>();
			//	}
			//}

			//if (DistrictConstructionLabel != null)
            {
				//DistrictConstructionLabel.Text = DistrictConstructionLabelString;
				//Uchronia.Log($"[UI][UpdateObjects] DistrictConstructionLabel.Text = {DistrictConstructionLabel.Text}");
				//if (DistrictConstructionLabelTooltip != null)
				//{
				//	Uchronia.Log($"[UI][UpdateObjects] DistrictConstructionLabelTooltip.Title before = {DistrictConstructionLabelTooltip.Title}");
				//
				//	DistrictConstructionLabelTooltip.Title = DistrictConstructionLabelString;
				//
				//	Uchronia.Log($"[UI][UpdateObjects] DistrictConstructionLabelTooltip.Title after = {DistrictConstructionLabelTooltip.Title}");
				//}				
			}
		}


		public static void OnExitSandbox()
        {

        }

		[HarmonyPatch(typeof(DiplomaticBanner))]
		public class DiplomaticBanner_Patch
		{
			[HarmonyPatch("RefreshItemsPerLine")]
			[HarmonyPrefix]
			public static bool RefreshItemsPerLine(DiplomaticBanner __instance)
			{
				__instance.maxNumberOfItemsPerLine = Uchronia.GetEmpireIconsNumColumn(); // default = 4 (75% for 9 items)
				return true;
			}
		}

		[HarmonyPatch(typeof(Amplitude.Mercury.UI.Helpers.GameUtils))]
		public class GameUtils_Patch
		{
			[HarmonyPatch("GetTerritoryName")]
			[HarmonyPrefix]
			public static bool GetTerritoryName(Amplitude.Mercury.UI.Helpers.GameUtils __instance, ref string __result, int territoryIndex, EmpireColor useColor = EmpireColor.None)
			{

				if (CultureUnlock.UseTrueCultureLocation())
				{
					//Diagnostics.Log($"[Gedemon] in GameUtils, GetTerritoryName");
					ref TerritoryInfo reference = ref Snapshots.GameSnapshot.PresentationData.TerritoryInfo.Data[territoryIndex];
					bool flag = useColor != EmpireColor.None;
					if (reference.AdministrativeDistrictGUID != 0)
					{
						ref SettlementInfo reference2 = ref Snapshots.GameSnapshot.PresentationData.SettlementInfo.Data[reference.SettlementIndex];
						if (reference2.TileIndex == reference.AdministrativeDistrictTileIndex)
						{
							string text = CultureUnlock.TerritoryHasName(territoryIndex) ? CultureUnlock.GetTerritoryName(territoryIndex, hasName: true) : reference2.EntityName.ToString();// reference2.EntityName.ToString();
							if (flag)
							{
								//Color empireColor = __instance.GetEmpireColor(reference.EmpireIndex, useColor);
								//__result = Amplitude.Mercury.Utils.TextUtils.ColorizeText(text, empireColor);
								//return false;
							}

							__result = text;
							return false;
						}
					}

					string text2 = CultureUnlock.TerritoryHasName(territoryIndex) ? CultureUnlock.GetTerritoryName(territoryIndex, hasName: true) : reference.LocalizedName ?? string.Empty;// reference.LocalizedName ?? string.Empty;
					if (flag && reference.Claimed)
					{
						//Color empireColor2 = __instance.GetEmpireColor(reference.EmpireIndex, useColor);
						//__result = __instance.TextUtils.ColorizeText(text2, empireColor2);
						//return false;
					}

					__result = text2;
					return false;

				}
				return true;

			}
		}


		[HarmonyPatch(typeof(MouseMarkersWindow.CampCreationClient))]
		public class CampCreationClient_Patch
		{
			static string cachedCityLocalizedName = string.Empty;

			[HarmonyPostfix]
			[HarmonyPatch(nameof(OnPresentationDataChanged))]
			static public void OnPresentationDataChanged()
			{
				cachedCityLocalizedName = string.Empty;

				if (Snapshots.SettlementPlacementCursorSnapshot.PresentationData.PlacementEvaluation.SettleFailure != ArmyActionFailureFlags.None)
                {
					return;
                }

				List<string> listedLocalizedNames = new List<string>();
				WorldPosition position = Snapshots.SettlementPlacementCursorSnapshot.nextConstructiblePosition;
				//Diagnostics.LogError($"[Gedemon] in CampCreationClient, OnPresentationDataChanged at position {position}");
				int localEmpireIndex = Amplitude.Mercury.Sandbox.SandboxManager.Sandbox.LocalEmpireIndex;
				MajorEmpire majorEmpire = Amplitude.Mercury.Sandbox.Sandbox.MajorEmpires[localEmpireIndex];
				if (CityMap.TryGetCityNameAt(position, majorEmpire, out string cityLocalizationKey))
				{
					FactionUIMapper empireUIMapper = Utils.DataUtils.GetUIMapper<FactionUIMapper>(majorEmpire.FactionDefinition.Name);
					string localizedName = Utils.TextUtils.Localize(cityLocalizationKey);
					listedLocalizedNames.Add(localizedName);
					cachedCityLocalizedName = "City: " + localizedName + " (" + (empireUIMapper.Adjective != string.Empty ? Utils.TextUtils.Localize(empireUIMapper.Adjective) : "Tribe") + ")" + Environment.NewLine;
				}

				int territoryIndex = Amplitude.Mercury.Sandbox.Sandbox.World.TileInfo.Data[position.ToTileIndex()].TerritoryIndex;

				List<string> listMajorEmpires = CultureUnlock.GetListMajorEmpiresForTerritory(territoryIndex);
                if(listMajorEmpires.Count > 0)
				{
					//Diagnostics.LogError($"[Gedemon] in CampCreationClient, OnPresentationDataChanged at position {position}, listMajorEmpires.Count = {listMajorEmpires.Count}");
					int empireEra = (int)majorEmpire.EraLevel.Value;
					foreach (string factionName in listMajorEmpires)
					{
						//Diagnostics.LogError($"[Gedemon] in CampCreationClient, OnPresentationDataChanged {factionName}");
						FactionDefinition factionDefinition = Utils.GameUtils.GetFactionDefinition(new StaticString(factionName));

						if (factionDefinition == null)
							continue;

						if(factionDefinition.EraIndex == empireEra + 1)
                        {
							if (CityMap.TryGetCityNameAt(position, factionDefinition, factionDefinition.EraIndex, out string newCityLocalizationKey))
							{
								string localizedName = Utils.TextUtils.Localize(newCityLocalizationKey);
								if(!listedLocalizedNames.Contains(localizedName))
								{
									listedLocalizedNames.Add(localizedName);
									FactionUIMapper empireUIMapper = Utils.DataUtils.GetUIMapper<FactionUIMapper>(factionDefinition.Name);
									cachedCityLocalizedName += "City: " + localizedName  + " (" + Utils.TextUtils.Localize(empireUIMapper.Adjective) + ")" + Environment.NewLine;
								}
							}
						}
					}
				}
			}

			[HarmonyPrefix]
			[HarmonyPatch(nameof(Refresh))]
			public static bool Refresh(MouseMarkersWindow.CampCreationClient __instance, StringBuilder stringBuilder)
			{
				if (cachedCityLocalizedName != string.Empty)
				{
					stringBuilder.Append(cachedCityLocalizedName);
				}
				return true;
			}
		}

		[HarmonyPatch(typeof(AvailableConstructionItem))]
		public class AvailableConstructionItem_Patch
		{
			[HarmonyPatch("Refresh")]
			[HarmonyPostfix]
			public static void Refresh(AvailableConstructionItem __instance, ref AvailableConstructionInfo availableConstructionInfo, ref EmpireInfo empireInfo)
			{
				//availableConstructionInfo.
				if (Sandbox.SimulationEntityRepository.TryGetSimulationEntity((SimulationEntityGUID)__instance.settlementScreen.LastSelectedSettlementInfo.LastSelectedSettlementGUID, out Settlement settlement))
                {
					//Uchronia.Log($"ConstructibleDefinitionName = {availableConstructionInfo.ConstructibleDefinitionName} / {__instance.constructibleItemTooltipTarget.Title} / {__instance.constructibleItemTooltipTarget.Description}");
					ExtensionDistrictDefinition extensionDistrictDefinition = __instance.constructibleDefinition as ExtensionDistrictDefinition;
					if(extensionDistrictDefinition != null)
                    {
						//Uchronia.Log($"is extensionDistrictDefinition");
						if (Districts.IsPopulationLimited(extensionDistrictDefinition))
						{
							int numPopulationLimitedDistricts = Districts.GetPopulationLimitedDistrictCount(settlement);
							int maxPopulationlimitedDistricts = Districts.GetMaxPopulationLimitedDistricts(settlement);
							bool IsLimited = numPopulationLimitedDistricts >= maxPopulationlimitedDistricts;
							string districtCountText = IsLimited ? ColorTo(numPopulationLimitedDistricts.ToString(), TextColor.Red) : numPopulationLimitedDistricts.ToString();
							string descriptionEnd = $"This District requires [Population] support.";
							descriptionEnd += Environment.NewLine + $"{districtCountText} Population-dependant Districts are already placed, the settlement can support {maxPopulationlimitedDistricts}.";
							if (IsLimited)
							{
								descriptionEnd += Environment.NewLine + ColorTo("This city has not enough [Population] to create more, but you can replace old ones.", TextColor.Orange);
							}
							else
							{
								descriptionEnd += Environment.NewLine + $"{maxPopulationlimitedDistricts - numPopulationLimitedDistricts} more can be placed, after that they can only replace existing Districts.";
							}

							//Uchronia.Log($"ConstructibleDefinitionName = {availableConstructionInfo.ConstructibleDefinitionName} / {__instance.constructibleItemTooltipTarget.Title} / {__instance.constructibleItemTooltipTarget.Description}");
							//__instance.constructibleItemTooltipTarget.Title = "test title";
							//Uchronia.Log($"descriptionAdd = {descriptionAdd}");
							__instance.constructibleItemTooltipTarget.Description = Utils.TextUtils.Localize(__instance.constructibleUIMapper.Description) + Environment.NewLine + descriptionEnd;

						}
					}

				}
			}
		}

		[HarmonyPatch(typeof(SettlementScreen_ConstructibleSectionItem))]
		public class SettlementScreen_ConstructibleSectionItem_Patch
		{
			[HarmonyPatch("Refresh")]
			[HarmonyPostfix]
			public static void Refresh(ref SettlementScreen_ConstructibleSectionItem __instance, ListOfStruct<AvailableConstructionInfo> availableConstructionInfos, ref EmpireInfo empireInfo, bool instant = false)
			{

				if (__instance.constructibleSectionUIMapper.Description != "%ConstructibleSectionExtensionsDescription")
					return;

				if (Sandbox.SimulationEntityRepository.TryGetSimulationEntity((SimulationEntityGUID)Snapshots.SettlementCursorSnapshot.settlementGuid, out Settlement settlement))
				{
					int freeDistricts = DatabaseUtils.FreePopulationDistrict;
					float districtPerPopulation = DatabaseUtils.DistrictPerPopulation;
					int numPopulationLimitedDistricts = Districts.GetPopulationLimitedDistrictCount(settlement);
					int maxPopulationlimitedDistricts = Districts.GetMaxPopulationLimitedDistricts(settlement);
					int settlementPopulation = (int)settlement.Population.Value;
					bool IsLimited = numPopulationLimitedDistricts >= maxPopulationlimitedDistricts;

					string districtCountText = IsLimited ? ColorTo(numPopulationLimitedDistricts.ToString(), TextColor.Red) : numPopulationLimitedDistricts.ToString();
					string descriptionEnd = $"Some Districts require [Population] and the amount you can build is limited.";
					descriptionEnd += Environment.NewLine + $"Districts requiring [Population] are Garrison and the Farmers, Makers, Market, Common, Research and Emblematic Quarters";
					descriptionEnd += Environment.NewLine + $"Districts not requiring [Population] are Wonders, Mines, Harbour, Train Station, Aerodrome, Natural Reserve, Airport, Silo, City Center, Administrative Center and Hamlet";
					descriptionEnd += Environment.NewLine + $"The first {freeDistricts} Population-dependant Districts are support-free.";
					descriptionEnd += Environment.NewLine + $"Each [Population] increases the limit by {districtPerPopulation}, this settlement allows a maximum of {maxPopulationlimitedDistricts}.";
					descriptionEnd += Environment.NewLine + $"There are {districtCountText} Population-dependant Districts already built.";
					if (IsLimited)
                    {
						descriptionEnd += Environment.NewLine + ColorTo("This city has not enough [Population] to create more, but you can replace old ones.", TextColor.Orange);
					}
                    else
                    {
						descriptionEnd += Environment.NewLine + $"{maxPopulationlimitedDistricts- numPopulationLimitedDistricts} more can be placed, after that they can only replace existing Districts.";
                    }
					
					string titleText = $"[[population]] Districts: {districtCountText} placed ({maxPopulationlimitedDistricts} max at {settlementPopulation}[Population])";
					string description = Utils.TextUtils.Localize(__instance.constructibleSectionUIMapper.Description) + Environment.NewLine + descriptionEnd;

					__instance.title.Text = titleText;
					__instance.titleTooltipTarget.Description = description;
				}
			}
		}
	}
}
