// AOM.Humankind.ImprovedAIFactionSelection.NextFactionChoice_Patch
using System;
using System.Linq;
using Amplitude;
using Amplitude.AI;
using Amplitude.AI.Heuristics;
using Amplitude.Framework;
using Amplitude.Mercury;
using Amplitude.Mercury.AI.Brain.Analysis.Economy;
using Amplitude.Mercury.AI.Brain.AnalysisData.MajorEmpire;
using Amplitude.Mercury.Data.AI;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Data.Simulation.Prerequisites;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.Interop.AI;
using Amplitude.Mercury.Interop.AI.Data;
using Amplitude.Mercury.Interop.AI.Entities;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

[HarmonyPatch(typeof(NextFactionChoice))]
public class NextFactionChoice_Patch
{
	[HarmonyPatch("ComputeInterestInNextFactions")]
	[HarmonyPrefix]
	public static bool ComputeInterestInNextFactions(NextFactionChoice __instance, MajorEmpire majorEmpire, MajorEmpireData analysisData)
	{
		IDatabase<FactionDefinition> database = Databases.GetDatabase<FactionDefinition>();
		int num = majorEmpire.NextFactionInfo.Length;
		if (analysisData.InterestInNextFactions == null || analysisData.InterestInNextFactions.Length != num)
		{
			Array.Resize(ref analysisData.InterestInNextFactions, num);
		}
		int num2 = 0;
		for (int i = 0; i < num; i++)
		{
			ref Amplitude.Mercury.Interop.AI.Data.NextFactionInfo reference = ref majorEmpire.NextFactionInfo[i];
			if ((reference.Status & FactionStatus.LockedByOthers) == 0 && reference.EraIndex > num2)
			{
				num2 = reference.EraIndex;
			}
		}
		__instance.ComputeOrientationFeelingsExtrema(analysisData, out var minFeeling, out var maxFeeling);
		bool isStubborn = (majorEmpire.Biases & Bias.Stubborn) != 0;
		float num3 = float.MinValue;
		for (int j = 0; j < num; j++)
		{
			analysisData.InterestInNextFactions[j] = new HeuristicFloat(HeuristicFloat.Type.Motivation, 0f);
			ref HeuristicFloat factionScore = ref analysisData.InterestInNextFactions[j];
			ref Amplitude.Mercury.Interop.AI.Data.NextFactionInfo nextFactionInfo = ref majorEmpire.NextFactionInfo[j];
			if ((nextFactionInfo.Status & FactionStatus.LockedByOthers) != 0)
			{
				factionScore.Set(-1f);
				continue;
			}

			if ((nextFactionInfo.Status & FactionStatus.LockedByEmpireMiscFlags) != 0)
			{
				float penalty = -0.5f;
				factionScore.Boost(penalty);
			}

			if (nextFactionInfo.FactionDefinitionName == majorEmpire.FactionName)
			{
				float penalty = -0.75f;
				factionScore.Boost(penalty);
				//
				//currentFactionScore.Set(-1f);
				//continue;
				//
			}

			if (isStubborn && nextFactionInfo.FactionDefinitionName == majorEmpire.FactionName)
			{
				factionScore.Set(1f);
			}
			else
			{
				FactionDefinition value = database.GetValue(nextFactionInfo.FactionDefinitionName);
				//if ((value.DLCPrerequisite.DownloadableContent & DownloadableContentPrerequisite.DownloadableContents.EastAfricaContentPack) != 0 && __instance.Brain.ControlledEmpire.AnyPlayerBehind(0, humanOnly: true))
				//{
				//	factionScore.Set(-1f);
				//	continue;
				//}
				HeuristicFloat operand = new HeuristicFloat(HeuristicFloat.Type.Value, 0f);
				if (minFeeling != maxFeeling)
				{
					operand.Set(analysisData.OrientationInfo[(int)nextFactionInfo.GameplayOrientation].OrientationFeeling);
					operand.Subtract(minFeeling);
					operand.Divide(maxFeeling - minFeeling);
					operand.Multiply(0.4f);
				}
				factionScore.Boost(operand);
				HeuristicFloat operand2 = new HeuristicFloat(HeuristicFloat.Type.Value, 0f);
				if (nextFactionInfo.EraIndex < num2)
				{
					if (num2 - nextFactionInfo.EraIndex == 1)
					{
						operand2.Set(-0.6f);
					}
					else
					{
						operand2.Set(-0.8f);
					}
				}
				factionScore.Boost(operand2);
				//HeuristicFloat operand3 = new HeuristicFloat(HeuristicFloat.Type.Value, 0f);
				//operand3.Add(__instance.ComputeMotivationForEmblematicUnit(majorEmpire, value?.EmblematicUnitDefinition));
				//operand3.Multiply(0.2f);
				//factionScore.Add(operand3);
				factionScore.Clamp11();
			}
			if (factionScore.Value > num3)
			{
				num3 = factionScore.Value;
				analysisData.WantedFaction = nextFactionInfo;
			}
		}

		return false;
	}


}
