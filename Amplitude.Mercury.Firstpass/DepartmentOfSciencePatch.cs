using Amplitude;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Simulation;
using HarmonyLib;
using Diagnostics = Amplitude.Diagnostics;

namespace Gedemon.Uchronia
{
	public class Science
	{
		[HarmonyPatch(typeof(DepartmentOfScience))]
		public class DepartmentOfScience_Patch
		{
			[HarmonyPatch("EndTurnPass_ClampResearchStock")]
			[HarmonyPrefix]
			public static bool EndTurnPass_ClampResearchStock(DepartmentOfScience __instance, SimulationPasses.PassContext context, string name)
			{
				if (__instance.majorEmpire.DepartmentOfDevelopment.CurrentEraIndex != 0)
				{
					FixedPoint value = __instance.majorEmpire.ResearchNet.Value;
					if (!(value <= 0) && (__instance.TechnologyQueue.CurrentResourceStock < value))
					{
						__instance.TechnologyQueue.CurrentResourceStock = value;
						Amplitude.Mercury.Sandbox.Sandbox.SimulationEntityRepository.SetSynchronizationDirty(__instance.TechnologyQueue);
					}
				}
				return false;
			}
			[HarmonyPatch("BeginTurnPass_ScienceModeUpdate")]
			[HarmonyPrefix]
			public static bool BeginTurnPass_ScienceModeUpdate(DepartmentOfScience __instance, SimulationPasses.PassContext context, string name)
			{
				if (__instance.majorEmpire.DepartmentOfDevelopment.CurrentEraIndex != 0)
				{
					__instance.UpdateTechnologiesCost();
				}
				return true;
			}

			[HarmonyPatch("GetTechnologyCostWithModifiers")]
			[HarmonyPrefix]
			public static bool GetTechnologyCostWithModifiers(DepartmentOfScience __instance, ref FixedPoint __result, ref Technology technology)
			{
				FixedPoint initialCost = technology.InitialCost;
				initialCost = __instance.majorEmpire.DepartmentOfTheTreasury.ApplyCostModifiers(technology.TechnologyDefinition, initialCost);
				initialCost *= Amplitude.Mercury.Sandbox.Sandbox.GameSpeedController.CurrentGameSpeedDefinition.TechnologyCostMultiplier;
				// Gedemon <<<<<
				string eraName = technology.TechnologyDefinition.EraDefinition.Name.ToString();
				string techName = technology.TechnologyDefinition.Name.ToString();
				initialCost *= DatabaseUtils.GetEraCostModifier(eraName);
				initialCost *= DatabaseUtils.GetSpecialTechCostModifier(techName);
				initialCost /= GetSharedKnowledgeModifier(__instance.majorEmpire, technology);
				// Gedemon >>>>>
				__result = FixedPoint.Round(initialCost);
				return false;
			}


			[HarmonyPatch("CheckTechnologyPrerequisite")]
			[HarmonyPrefix]
			public static bool CheckTechnologyPrerequisite(DepartmentOfScience __instance, ref bool __result, TechnologyPrerequisite technologyPrerequisite)
			{
				if(!DatabaseUtils.ResearchNeedAllPrerequisite)
					return true;

				if (technologyPrerequisite.TechnologyNames == null || technologyPrerequisite.TechnologyNames.Length == 0)
				{
					__result = true;
					return false;
				}
				int num = technologyPrerequisite.TechnologyNames.Length;
				for (int i = 0; i < num; i++)
				{
					StaticString staticString = technologyPrerequisite.TechnologyNames[i];
					int technologyIndex = __instance.GetTechnologyIndex(staticString);
					if (technologyIndex < 0)
					{
						Diagnostics.LogError($"Technology '{staticString}' not found.");
					}
					else if (__instance.Technologies.Data[technologyIndex].TechnologyState != TechnologyStates.Completed)
					{
						__result = false;
						return false;
					}
				}
				__result = true;
				return false;
			}

		}

		public static FixedPoint GetSharedKnowledgeModifier(MajorEmpire majorEmpire, Technology technology)
		{
			if (!Uchronia.SandboxInitialized)
				return FixedPoint.One;

			FixedPoint modifier = FixedPoint.One;
			int majorIndex = majorEmpire.Index;
			string log = $"Empire #{majorIndex}: ";
			int numberOfMajorEmpires = Amplitude.Mercury.Sandbox.Sandbox.NumberOfMajorEmpires;
			for (int i = 0; i < numberOfMajorEmpires; i++)
			{
				if (i != majorIndex)
				{
					MajorEmpire otherMajor = Amplitude.Mercury.Sandbox.Sandbox.MajorEmpires[i];

					if (CultureChange.IsSleepingEmpire(otherMajor) || !otherMajor.IsAlive)
						continue;

					if (!otherMajor.DepartmentOfScience.IsTechnologyResearched(technology.TechnologyDefinition.Name))
						continue;


					DiplomaticRelation diplomaticRelation = majorEmpire.DiplomaticRelationByOtherEmpireIndex[i];
					EconomicalAgreements economicalAgreements = diplomaticRelation.CurrentAgreements.EconomicalAgreementLevel;
					if ((((diplomaticRelation.LeftEmpireIndex == majorEmpire.Index) ? diplomaticRelation.LeftAmbassy.Entity : diplomaticRelation.RightAmbassy.Entity).CurrentAbilities & DiplomaticAbility.ScientificAgreement) != 0)
					{
						modifier += (FixedPoint)0.5;
						log += $"[+0.5 #{i}]";
					}
					else if (diplomaticRelation.CurrentState == DiplomaticStateType.Alliance || diplomaticRelation.CurrentState == DiplomaticStateType.VassalToLiege)
					{
						modifier += (FixedPoint)0.3;
						log += $"[+0.3 #{i}]";
					}
					else if (economicalAgreements == EconomicalAgreements.AllResourceTrade)
					{
						modifier += (FixedPoint)0.2;
						log += $"[+0.2 #{i}]";
					}
					else if (economicalAgreements == EconomicalAgreements.LuxuryTrade || diplomaticRelation.CurrentAgreements.InformationAgreementLevel == InformationAgreements.ShareMaps || diplomaticRelation.CurrentAgreements.CulturalAgreementLevel == CulturalAgreements.OpenBorder)
					{
						modifier += (FixedPoint)0.1;
						log += $"[+0.1 #{i}]";
					}
				}
			}
			if (modifier > 1)
			{
				Diagnostics.LogWarning($"[Gedemon] [DepartmentOfScience] GetSharedKnowledgeModifier for {technology.TechnologyDefinition.Name}, {log} = {modifier}");
			}
			return modifier;
		}

		static public void OnSandboxInitialized()
		{

			int numberOfMajorEmpires = Amplitude.Mercury.Sandbox.Sandbox.NumberOfMajorEmpires;
			for (int i = 0; i < numberOfMajorEmpires; i++)
			{
				MajorEmpire majorEmpire = Amplitude.Mercury.Sandbox.Sandbox.MajorEmpires[i];
				majorEmpire.DepartmentOfScience.UpdateTechnologiesCost();
			}
		}
	}


}
