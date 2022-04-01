using BepInEx;
using System.Collections.Generic;
using Amplitude.Mercury.Simulation;
using HarmonyLib;
using Amplitude.Mercury.Sandbox;
using Amplitude.Framework;
using Amplitude.Mercury.UI;
using UnityEngine;
using HumankindModTool;
using Amplitude;
using Amplitude.Framework.Session;
using Amplitude.Mercury.Session;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.Data.Simulation.Costs;
using Amplitude.Mercury;
using Amplitude.Mercury.Avatar;
using Amplitude.Mercury.Presentation;
using Amplitude.Mercury.AI.Brain.Analysis.ArmyBehavior;
using Amplitude.AI.Heuristics;
using System.IO;
using Amplitude.UI.Renderers;
using Amplitude.UI;
using Diagnostics = Amplitude.Diagnostics;

namespace Gedemon.Uchronia
{

	[HarmonyPatch(typeof(DepartmentOfIndustry))]
	public class DepartmentOfIndustry_Patch
	{
		[HarmonyPatch("InvestProductionFor")]
		[HarmonyPrefix]
		public static bool InvestProductionFor(DepartmentOfIndustry __instance, ConstructionQueue constructionQueue)
		{

			if (CultureUnlock.UseTrueCultureLocation())
			{

				FixedPoint left = DepartmentOfIndustry.ComputeProductionIncome(constructionQueue.Settlement);
				Settlement entity = constructionQueue.Settlement.Entity;
				FixedPoint fixedPoint = left + constructionQueue.CurrentResourceStock;
				constructionQueue.CurrentResourceStock = 0;
				bool flag = false;
				if (constructionQueue.Constructions.Count > 0)
				{
					flag = (constructionQueue.Constructions[0].ConstructibleDefinition.ProductionCostDefinition.Type == ProductionCostType.TurnBased);
				}

				bool flag2 = true;
				int num = 0;
				while ((fixedPoint > 0 || (flag2 && flag)) && num < constructionQueue.Constructions.Count)
				{
					int num2 = num++;
					Construction construction = constructionQueue.Constructions[num2];
					construction.Cost = __instance.GetConstructibleProductionCostForSettlement(entity, construction.ConstructibleDefinition);
					construction.Cost = __instance.ApplyPositionCostModifierIfNecessary(construction.Cost, construction.ConstructibleDefinition, construction.WorldPosition.ToTileIndex());
					constructionQueue.Constructions[num2] = construction;
					bool num3 = construction.FailureFlags != ConstructionFailureFlags.None;
					bool hasBeenBoughtOut = construction.HasBeenBoughtOut;
					bool flag3 = construction.InvestedResource >= construction.Cost;
					if (num3 | hasBeenBoughtOut | flag3)
					{
						continue;
					}

					switch (construction.ConstructibleDefinition.ProductionCostDefinition.Type)
					{
						case ProductionCostType.TurnBased:
							if (!flag2)
							{
								continue;
							}

							fixedPoint = 0;
							++construction.InvestedResource;
							break;
						case ProductionCostType.Infinite:
							fixedPoint = 0;
							break;
						case ProductionCostType.Production:
							{
								FixedPoint fixedPoint2 = construction.Cost - construction.InvestedResource;
								if (fixedPoint > fixedPoint2)
								{
									fixedPoint -= fixedPoint2;
									construction.InvestedResource = construction.Cost;
									__instance.NotifyEndedConstruction(constructionQueue, num2, ref construction);
									Amplitude.Framework.Simulation.SimulationController.RefreshAll();
								}
								else
								{
									construction.InvestedResource += fixedPoint;
									fixedPoint = 0;
								}

								break;
							}
						case ProductionCostType.Transfert:
							{
								FixedPoint productionIncome = fixedPoint * entity.EmpireWideConstructionProductionBoost.Value;
								fixedPoint = __instance.TransfertProduction(construction, productionIncome);
								break;
							}
						default:
							Diagnostics.LogError("Invalid production cost type.");
							break;
					}

					flag2 = false;
					constructionQueue.Constructions[num2] = construction;
				}

				__instance.CleanConstructionQueue(constructionQueue);
				if (entity.SettlementStatus == SettlementStatuses.City)
				{
					constructionQueue.CurrentResourceStock = FixedPoint.Max(left, fixedPoint); // was FixedPoint.Min
				}

				return false; // we've replaced the full method
			}
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(OnConstructionCompleted))]
		public static bool OnConstructionCompleted(DepartmentOfIndustry __instance, ConstructionQueue constructionQueue, Construction construction)
		{

			if (!Uchronia.KeepHistoricalDistricts())
				return true;

			//Diagnostics.LogWarning($"[Gedemon][DepartmentOfIndustry] in PresentationPawn, Initialize for construction {construction.ConstructibleDefinition.Name}");
			//Uchronia.Log($"[Gedemon][DepartmentOfIndustry] in OnConstructionCompleted for construction = {construction.ConstructibleDefinition.Name}");

			if(construction.ConstructibleDefinition.ConstructibleType == ConstructibleType.ExtensionDistrict || construction.ConstructibleDefinition.ConstructibleType == ConstructibleType.ExploitationDistrict)
            {
				int tileIndex = construction.WorldPosition.ToTileIndex();
				if (CurrentGame.Data.HistoricVisualAffinity.ContainsKey(tileIndex))
				{
					//Uchronia.Log($"[Gedemon][DepartmentOfIndustry] Remove entry from HistoricalVisualAffinity at {construction.WorldPosition}");
					CurrentGame.Data.HistoricVisualAffinity.Remove(tileIndex); 
				}

			}

			return true;
		}
	}
}
