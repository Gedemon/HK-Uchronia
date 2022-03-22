using System.Collections.Generic;
using Amplitude.Mercury.Simulation;
using HarmonyLib;
using Amplitude.Mercury.Sandbox;
using Amplitude.Mercury.UI;
using Amplitude;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Serialization;
using Amplitude.Mercury;
using Amplitude.Mercury.Presentation;
using Amplitude.Mercury.AI.Brain.Analysis.ArmyBehavior;
using Amplitude.AI.Heuristics;
using Amplitude.Mercury.AI.Brain.Actuators;
using Amplitude.Mercury.AI.Brain.AnalysisData.Army;
using Amplitude.Mercury.AI.Brain.Analysis.Military;
using Amplitude.Mercury.AI.Battle;
using Amplitude.Mercury.AI;
using Amplitude.UI.Interactables;
using Amplitude.UI.Renderers;
using Amplitude.UI;
using Amplitude.UI.Tooltips;
using Amplitude.Mercury.UI.Helpers;
using Diagnostics = Amplitude.Diagnostics;
using System;
using UnityEngine;

namespace Gedemon.Uchronia
{
	//*
	public enum Posture
	{
		// Orders
		Pursuit				= 1000,
		AllOutAttack		= 2000,
		MobileAttack		= 3000,
		DeliberateAttack	= 4000,
		PreparedDefense		= 5000,
		HastyDefense		= 6000,
		Delay				= 7000,
		Retreat				= 8000,
		// Result
		Withdrawal			= 9000, // back to starting position
		Advance				= 9001, // capture defender's position
		// Disorganized
		Routed				= 10000,
		//DisorganizedDefense = 10002,
		//DisorganizedAttack	= 10003,
		// Default
		DefaultAttack		= 20000,
		DefaultDefense		= 20001,
		//
		None				= 30000,
	}

	public class BattleExtension : ISerializable
	{
		public Posture AttackerPosture { get; set; }
		public Posture DefenderPosture { get; set; }
		public string BattleSummary { get; set; }
		public int RoundNum { get; set; }

		public BattleVictoryType VictoryType { get; set; } 

		public BattleExtension()
		{
			AttackerPosture = Posture.DefaultAttack;
			DefenderPosture = Posture.DefaultDefense;
			BattleSummary = string.Empty;
		}

		public void Serialize(Serializer serializer)
		{
			AttackerPosture = (Posture)serializer.SerializeElement("AttackerPosture", (int)AttackerPosture);
			DefenderPosture = (Posture)serializer.SerializeElement("DefenderPosture", (int)DefenderPosture);
			BattleSummary = serializer.SerializeElement("BattleSummary", BattleSummary);
		}
	}
	public static class BattleSaveExtension
	{
		public static IDictionary<SimulationEntityGUID, BattleExtension> BattleExensions;

		public static void OnSandboxStart()
		{
			BattleExensions = new Dictionary<SimulationEntityGUID, BattleExtension>();
		}

		public static void OnExitSandbox()
		{
			BattleExensions = null;
		}

		public static BattleExtension GetExtension(SimulationEntityGUID battleGUID)
		{
			return BattleExensions[battleGUID];
		}
	}

	class BattlePosture
    {
		public static bool UsePosture = true;
		public const int MaximalRealIndex = 999;
		public static Posture GetPosture(int value)
        {
			if(value < 1000)
            {
				Diagnostics.LogError($"[Gedemon][BattleOrder] GetPosture: Can't get Posture from value < 1000 (value = {value})");
				return value == MaximalRealIndex ? Posture.Pursuit : Posture.DeliberateAttack;
			}
			string thousand = value.ToString();
			int postureValue = int.Parse(thousand[0].ToString()) * 1000;
			return value - postureValue == MaximalRealIndex ? (Posture)postureValue + 1000 : (Posture)postureValue;
        }

		public static int GetIndex(int value)
		{
			if (value < 1000)
			{
				//Diagnostics.LogError($"[Gedemon][BattleOrder] GetIndex: Can't get index from value < 1000 (value = {value})");
				return value == MaximalRealIndex ? -1 : value;
			}
			string thousand = value.ToString();
			int index = value - (int.Parse(thousand[0].ToString()) * 1000);
			return index == MaximalRealIndex ? -1 : index;
		}

		public static string GetPostureString(Posture posture)
        {
			string postureString = string.Empty;
			switch(posture)
            {
				case Posture.AllOutAttack:
					postureString = "All Out Attack";
					break;
				case Posture.Delay:
					postureString = "Delay";
					break;
				case Posture.DeliberateAttack:
					postureString = "Deliberate Attack";
					break;
				case Posture.HastyDefense:
					postureString = "Hasty Defense";
					break;
				case Posture.MobileAttack:
					postureString = "Mobile Attack";
					break;
				case Posture.PreparedDefense:
					postureString = "Prepared Defense";
					break;
				case Posture.Pursuit:
					postureString = "Pursuit";
					break;
				case Posture.Retreat:
					postureString = "Retreat";
					break;
				case Posture.Routed:
					postureString = "Routed";
					break;
				case Posture.Withdrawal:
					postureString = "Withdrawal";
					break;
				default:
					postureString = posture.ToString();
					break;
			}
			
			return postureString;
		}
		public static bool IsDefensive(Posture posture)
		{
			switch (posture)
			{
				case Posture.Delay:
				case Posture.HastyDefense:
				case Posture.PreparedDefense:
				case Posture.Retreat:
				case Posture.Withdrawal:
				case Posture.DefaultDefense:
				case Posture.Routed:
					return true;
				default:
					return false;

			}
		}
		public static bool IsOffensive(Posture posture)
		{
			switch (posture)
			{
				case Posture.AllOutAttack:
				case Posture.DefaultAttack:
				case Posture.DeliberateAttack:
				case Posture.MobileAttack:
				case Posture.Pursuit:
					return true;
				default:
					return false;

			}
		}
		public static bool IsAbandonCombat(Posture posture)
		{
			switch (posture)
			{
				//case Posture.Delay:
				case Posture.Retreat:
				case Posture.Withdrawal:
					return true;
				default:
					return false;

			}
		}


		public static void ResolvePosture(ref UnitGroupStats currentGroup, ref UnitGroupStats opponentGroup, ref BattleExtension battleExtension)
        {
			// note: CombatStrength already include Veterancy and HealthRatio factor

			Posture currentGroupPosture = currentGroup.GroupPosture;
			Posture opponentGroupPosture = opponentGroup.GroupPosture;

			void ChangePosture(UnitGroupStats unitGroup, Posture newPosture, ref BattleExtension extension)
			{
				extension.BattleSummary += Environment.NewLine + $"{unitGroup.Name} Posture changed to {newPosture}";
				if (unitGroup.IsAttacker())
				{
					extension.AttackerPosture = newPosture;
				}
				else
				{
					extension.DefenderPosture = newPosture;
				}
			}

			// Slower group may be immediatly pushed in defensive posture when both side attacks
			if (BattlePosture.IsOffensive(currentGroupPosture) && BattlePosture.IsOffensive(opponentGroupPosture))
			{
				if (currentGroup.AverageMoves < opponentGroup.AverageMoves * QJM.MediumReducingModifier)
				{
					currentGroup.CombatModifier *= QJM.LowPenalty;
					battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} attack aborted, faster opponent: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.AverageMoves} vs {opponentGroup.AverageMoves} [MovementSpeed]";
					ChangePosture(currentGroup, Posture.HastyDefense, ref battleExtension);
					return;
				}
            }


			//
			switch (currentGroupPosture)
			{
				case Posture.AllOutAttack:

					switch (opponentGroupPosture)
					{
						case Posture.DeliberateAttack:
						case Posture.MobileAttack:

							if (currentGroup.CombatStrength < opponentGroup.CombatStrength * QJM.LowReducingModifier)
                            {
								currentGroup.CombatModifier *= QJM.LowPenalty;
								battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} All out Attack is countered: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.CombatStrength} vs {opponentGroup.CombatStrength} [CombatStrength]";
								ChangePosture(currentGroup, Posture.HastyDefense, ref battleExtension);
							}

							break;

						case Posture.PreparedDefense:

							if (currentGroup.CombatStrength < opponentGroup.CombatStrength * QJM.LowReducingModifier)
							{
								currentGroup.CombatModifier *= QJM.MediumPenalty;
								battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} not strong enough for All out Attack: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.CombatStrength} vs {opponentGroup.CombatStrength} [CombatStrength]";
								ChangePosture(currentGroup, Posture.DeliberateAttack, ref battleExtension);
							}
							break;

						default:

							break;
					}
					break;

				case Posture.Delay:

					currentGroup.CombatModifier *= QJM.GetPostureBaseModifier(currentGroupPosture);
					battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} is delaying: x{currentGroup.CombatModifier} [CombatStrength]";
					break;

				case Posture.DeliberateAttack:
					switch (opponentGroupPosture)
					{
						case Posture.AllOutAttack:
						case Posture.MobileAttack:
						case Posture.Pursuit:

							currentGroup.CombatModifier *= QJM.LowPenalty;
							battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} is pushed into defense by the enemy initiative : x{currentGroup.CombatModifier} [CombatStrength]";
							ChangePosture(currentGroup, Posture.HastyDefense, ref battleExtension);
							break;

						default:

							break;
					}
					break;

				case Posture.HastyDefense:

					currentGroup.CombatModifier *= QJM.GetPostureBaseModifier(currentGroupPosture);
					battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} has hasty defense: x{currentGroup.CombatModifier} [CombatStrength]";
					break;

				case Posture.MobileAttack:

					if (currentGroup.AverageVeterency < QJM.VeterancyForMobileAttack)
					{
						currentGroup.CombatModifier *= QJM.LowPenalty;
						battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} Mobile Attack is failing: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.AverageVeterency}/{QJM.VeterancyForMobileAttack} [Veterancy] required";
						ChangePosture(currentGroup, Posture.DeliberateAttack, ref battleExtension);
						break;
					}

					FixedPoint MoveModifier = QJM.GetMoveModifier(currentGroup.AverageMoves,opponentGroup.AverageMoves);

					if (currentGroup.AverageVeterency * MoveModifier < opponentGroup.AverageVeterency * QJM.MediumReducingModifier)
					{
						currentGroup.CombatModifier *= QJM.MediumPenalty;
						battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} Mobile Attack is failing: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.AverageVeterency} [Veterancy] vs {opponentGroup.AverageVeterency} [Veterancy], {currentGroup.AverageMoves} vs {opponentGroup.AverageMoves} [MovementSpeed]";
						ChangePosture(currentGroup, Posture.DeliberateAttack, ref battleExtension);
						break;
					}

					// Ordered from higher to lower here
					if (currentGroup.AverageVeterency * MoveModifier * QJM.HighReducingModifier > opponentGroup.AverageVeterency + QJM.VeterancyForMobileAttack)
					{
						currentGroup.CombatModifier *= QJM.VeryHighBonus;
						battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} Mobile Attack is perfectly executed: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.AverageVeterency} vs {opponentGroup.AverageVeterency} [Veterancy], {currentGroup.AverageMoves} vs {opponentGroup.AverageMoves} [MovementSpeed]";
						break;
					}
					if (currentGroup.AverageVeterency * MoveModifier * QJM.MediumReducingModifier > opponentGroup.AverageVeterency)
					{
						currentGroup.CombatModifier *= QJM.MediumBonus;
						battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} Mobile Attack is well executed: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.AverageVeterency} vs {opponentGroup.AverageVeterency} [Veterancy], {currentGroup.AverageMoves} vs {opponentGroup.AverageMoves} [MovementSpeed]";
						break;
					}
					break;

				case Posture.PreparedDefense:
					switch (opponentGroupPosture)
					{
						case Posture.Pursuit:
							if (currentGroup.AverageMoves < opponentGroup.AverageMoves)
							{
								currentGroup.CombatModifier *= QJM.LowPenalty;
								battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} is caught unprepared: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.AverageMoves}/{opponentGroup.AverageMoves} [MovementSpeed]";
								ChangePosture(currentGroup, Posture.HastyDefense, ref battleExtension);
								break;
							}
							goto default;

						case Posture.AllOutAttack:
							if (currentGroup.AverageMoves < opponentGroup.AverageMoves * QJM.LowReducingModifier)
							{
								currentGroup.CombatModifier *= QJM.LowPenalty;
								battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} is caught unprepared: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.AverageMoves}/{opponentGroup.AverageMoves} [MovementSpeed]";
								ChangePosture(currentGroup, Posture.HastyDefense, ref battleExtension);
								break;
							}
							goto default;

						case Posture.MobileAttack:
							if (currentGroup.AverageMoves < opponentGroup.AverageMoves * QJM.MediumReducingModifier)
							{
								currentGroup.CombatModifier *= QJM.LowPenalty;
								battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} is caught unprepared: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.AverageMoves}/{opponentGroup.AverageMoves} [MovementSpeed]";
								ChangePosture(currentGroup, Posture.HastyDefense, ref battleExtension);
								break;
							}
							goto default;

						default:
							{
								currentGroup.CombatModifier *= QJM.GetPostureBaseModifier(currentGroupPosture);
								battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} has a solid defense (x{currentGroup.CombatModifier} [CombatStrength])";
								break;
							}
					}

					break;

				case Posture.Pursuit:

					switch (opponentGroupPosture)
					{
						case Posture.AllOutAttack:
						case Posture.MobileAttack:

							currentGroup.CombatModifier *= QJM.LowPenalty;
							battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} Pursuit is failing against an advancing opponent: x{currentGroup.CombatModifier} [CombatStrength]";
							ChangePosture(currentGroup, Posture.AllOutAttack, ref battleExtension);
							break;

						case Posture.DeliberateAttack:

							currentGroup.CombatModifier *= QJM.MediumPenalty;
							battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} Pursuit is failing against an organized opponent: x{currentGroup.CombatModifier} [CombatStrength]";
							ChangePosture(currentGroup, Posture.AllOutAttack, ref battleExtension);
							break;

						case Posture.Delay:
						case Posture.HastyDefense:
						case Posture.PreparedDefense:

							currentGroup.CombatModifier *= QJM.VeryHighPenalty;
							battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} Pursuit is failing catastrophically against defense: x{currentGroup.CombatModifier} [CombatStrength]";
							ChangePosture(currentGroup, Posture.AllOutAttack, ref battleExtension);
							break;

						default:

							break;
					}

					break;

				case Posture.Retreat:

					FixedPoint RetreatMoveModifier = QJM.GetMoveModifier(currentGroup.AverageMoves, opponentGroup.AverageMoves);

					switch (opponentGroupPosture)
					{
						case Posture.AllOutAttack:

							if (currentGroup.CombatStrength * RetreatMoveModifier < opponentGroup.CombatStrength * QJM.MediumReducingModifier)
							{
								currentGroup.CombatModifier *= QJM.MediumPenalty;
								battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} Retreat is turning into panic: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.AverageMoves}/{opponentGroup.AverageMoves} [MovementSpeed]";
								ChangePosture(currentGroup, Posture.Routed, ref battleExtension);
							}
							break;

						case Posture.Pursuit:

							if (currentGroup.CombatStrength * RetreatMoveModifier < opponentGroup.CombatStrength * QJM.LowReducingModifier)
							{
								currentGroup.CombatModifier *= QJM.HighPenalty;
								battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} Retreat is turning into a total disaster: x{currentGroup.CombatModifier} [CombatStrength], {currentGroup.AverageMoves}/{opponentGroup.AverageMoves} [MovementSpeed]";
								ChangePosture(currentGroup, Posture.Routed, ref battleExtension);
							}
							break;

						default:

							break;
					}

					break;
				case Posture.Routed:
					switch (opponentGroupPosture)
					{
						case Posture.AllOutAttack:
						case Posture.MobileAttack:
						case Posture.Pursuit:

							currentGroup.CombatModifier *= QJM.HighPenalty;
							battleExtension.BattleSummary += Environment.NewLine + $"{currentGroup.Name} Routed units are pursued: x{currentGroup.CombatModifier} [CombatStrength]";

							break;
						default:

							break;
					}

					break;
				case Posture.Withdrawal:
					// to do: faster attacker could prevent and switch to hasty defense ?
					switch (opponentGroupPosture)
					{
						case Posture.Pursuit:
							currentGroup.CombatModifier *= QJM.GetPostureBaseModifier(currentGroupPosture);

							break;
						case Posture.AllOutAttack:
							currentGroup.CombatModifier *= QJM.GetPostureBaseModifier(currentGroupPosture);

							break;
						case Posture.MobileAttack:
							currentGroup.CombatModifier *= QJM.GetPostureBaseModifier(currentGroupPosture);

							break;
						default:
							currentGroup.CombatModifier *= QJM.GetPostureBaseModifier(currentGroupPosture);

							break;
					}
					break;
				default:
					break;
			}
		}
	}

	[HarmonyPatch(typeof(ConfirmOrRetreatFromBattle))]
	public class ConfirmOrRetreatFromBattle_Patch
	{
		[HarmonyPrefix]
		[HarmonyPatch(nameof(CreateOrder))]
		public static bool CreateOrder(ConfirmOrRetreatFromBattle __instance, ref Order __result)
		{
			Diagnostics.LogWarning($"[Gedemon][ConfirmOrRetreatFromBattle] CreateOrder empire #{__instance.Brain.ControlledEmpire.EmpireIndex}");

			if (!BattlePosture.UsePosture) { return true; }

			ref Amplitude.Mercury.Interop.AI.Data.Battle battle = ref Amplitude.Mercury.Interop.AI.Snapshots.Battle.Battles[__instance.battleIndex];
			bool isAttacker = battle.AttackerEmpireIndex == __instance.Brain.ControlledEmpire.EmpireIndex;
			RequestInstantBattleResult instantBattleRequestForBattle = Amplitude.Mercury.Interop.AI.Snapshots.Battle.GetInstantBattleRequestForBattle(battle.SimulationEntityGUID, RequestInstantBattleResult.InstantBattleType.Normal);
			HeuristicBool heuristicBool = new HeuristicBool(value: false);

			// Gedemon <<<<<
			Amplitude.Mercury.Interop.AI.Entities.Empire otherEmpire = (isAttacker ? Amplitude.Mercury.Interop.AI.Snapshots.Game.Empires[battle.DefenderEmpireIndex] : Amplitude.Mercury.Interop.AI.Snapshots.Game.Empires[battle.AttackerEmpireIndex]);
			BattleBehaviour battleBehaviourFromBattle = Helpers.GetBattleBehaviourFromBattle(__instance.Brain, ref battle);
			float prediction = Helpers.GetBattlePredictionScore(instantBattleRequestForBattle.Result.AutoResolveContext, isAttacker, __instance.Brain, otherEmpire, battleBehaviourFromBattle).Value;
			Posture posture;
			Diagnostics.LogError($"[Gedemon][ConfirmOrRetreatFromBattle] prediction = {prediction}");
			if (prediction >= 1)
			{
				posture = Posture.AllOutAttack;
			}
			else if(prediction > 0)
			{
				posture = Posture.DeliberateAttack;
			}
			else
			{
				posture = Posture.HastyDefense;
			}
			// Gedemon >>>>>

			if (Amplitude.Mercury.AI.Preferences.PreventRetreat)
			{
				heuristicBool.Set(operand: true);
			}
			else if (isAttacker)
			{
				heuristicBool.Set(operand: true);
			}
			else if ((isAttacker && battle.AttackerRetreatFailureFlags != 0) || (!isAttacker && battle.DefenderRetreatFailureFlags != 0))
			{
				heuristicBool.Set(operand: true);
			}
			else if (battle.Type == BattleType.Siege)
			{
				heuristicBool.Set(operand: true);
			}
			else
			{
				if (!instantBattleRequestForBattle.Result.RequestSuccess)
				{
					__result = new OrderBattleConfirmation
					{
						BattleGUID = __instance.Task.BattleGuid,
						EmpireIndex = __instance.Brain.EmpireIndex + (int)posture
					};
					return false;
				}
				//Amplitude.Mercury.Interop.AI.Entities.Empire otherEmpire = (isAttacker ? Amplitude.Mercury.Interop.AI.Snapshots.Game.Empires[battle.DefenderEmpireIndex] : Amplitude.Mercury.Interop.AI.Snapshots.Game.Empires[battle.AttackerEmpireIndex]);
				//BattleBehaviour battleBehaviourFromBattle = Helpers.GetBattleBehaviourFromBattle(__instance.Brain, ref battle);
				if (Helpers.GetBattlePredictionScore(instantBattleRequestForBattle.Result.AutoResolveContext, isAttacker, __instance.Brain, otherEmpire, battleBehaviourFromBattle).Value <= -1f)
				{
					heuristicBool.Set(operand: false);
				}
				else
				{
					heuristicBool.Set(operand: true);
				}
			}
			if (heuristicBool.Value)
			{
				__result = new OrderBattleConfirmation
				{
					BattleGUID = __instance.Task.BattleGuid,
					EmpireIndex = __instance.Brain.EmpireIndex + (int)posture
				};
				return false;
			}
			__result = new OrderBattleConfirmation
			{
				BattleGUID = __instance.Task.BattleGuid,
				EmpireIndex = __instance.Brain.EmpireIndex + (int)Posture.Retreat
			};
			return false;
		}
	}

	[HarmonyPatch(typeof(AIController))]
	public class AIController_Patch
	{
		[HarmonyPrefix]
		[HarmonyPatch(nameof(LateUpdate))]
		public static bool LateUpdate(AIController __instance)
		{
			//Diagnostics.LogWarning($"[Gedemon][AIController] LateUpdate {__instance.state}, num decisions #{__instance.decisionCount}");

			return true;
		}
	}


	[HarmonyPatch(typeof(BattleController))]
	public class BattleController_Patch
	{

		[HarmonyPrefix]
		[HarmonyPatch(nameof(Run))]
		public static bool Run(BattleController __instance)
		{
			//Diagnostics.LogWarning($"[Gedemon][BattleController] Run (prefix) : BattleBrainPoolSize = {__instance.BattleBrainPoolSize}");
			for (int i = 0; i < __instance.BattleBrainPoolSize; i++)
			{
				if (__instance.BattleBrains[i].SimulationBattle != null)
				{
					//Diagnostics.LogError($"[Gedemon][BattleController] Run (prefix) : brain [{i}] BattleGUID = {__instance.BattleBrains[i].SimulationBattle.GUID}, Empire = {__instance.BattleBrains[i].CurrentContender.EmpireIndex}");
				}
			}
			return true;
		}
		public static int lastBattleBrainCreated = -1;
		[HarmonyPrefix]
		[HarmonyPatch(nameof(GetOrCreateBrain))]
		public static bool GetOrCreateBrain(BattleController __instance, Battle simulationBattle)
		{
			//Diagnostics.LogWarning($"[Gedemon][BattleController] GetOrCreateBrain (prefix) : BattleBrainPoolSize = {__instance.BattleBrainPoolSize}, battle.GUID = {simulationBattle.GUID}");

			return true;
		}

		[HarmonyPostfix]
		[HarmonyPatch(nameof(GetOrCreateBrain))]
		public static void GetOrCreateBrainPost(BattleController __instance, Battle simulationBattle, ref int __result)
		{
			if(__result != lastBattleBrainCreated)
			{
				//Diagnostics.LogWarning($"[Gedemon][BattleController] GetOrCreateBrain (postfix): New brain created, BattleBrainPoolSize = {__instance.BattleBrainPoolSize}, battle.GUID = {simulationBattle.GUID}, brain ID = {__result}");
				lastBattleBrainCreated = __result;
			}
		}
	}

	[HarmonyPatch(typeof(BattleBrain))]
	public class BattleBrain_Patch
	{
		[HarmonyPrefix]
		[HarmonyPatch(nameof(Run))]
		public static bool Run(ref BattleBrain __instance, BattleController controller)
		{
			//Diagnostics.LogWarning($"[Gedemon][BattleBrain] Run (prefix): CurrentContender empire index = {__instance.CurrentContender.EmpireIndex}, BrainState = {__instance.BrainState}, Battle = {__instance.SimulationBattle.GUID})");

			if (__instance.BrainState == BattleBrainState.Sleep && __instance.SimulationBattle.BattleState == BattleState.Confirmation)
			{
				//Diagnostics.Log($"[Gedemon][BattleBrain] Run: Sleep && Confirmation (instigatorLeaderEmpireIndex = {__instance.SimulationBattle.InstigatorLeaderEmpireIndex}, notInstigatorLeaderEmpireIndex = {__instance.SimulationBattle.NotInstigatorLeaderEmpireIndex})");
			}

			Empire empire = Amplitude.Mercury.Sandbox.Sandbox.Empires[__instance.CurrentContender.EmpireIndex];
			//Diagnostics.Log($"[Gedemon][BattleBrain] Run: CurrentContender Empire is major = {(empire is MajorEmpire)}, IsAIBrainActivated = {empire.IsAIBrainActivated}, DebugControl.StepByStepBattles = {DebugControl.StepByStepBattles}, DoNextStep = {__instance.DoNextStep}");

			if (!BattlePosture.UsePosture) { return true; }

			if (__instance.BrainState == BattleBrainState.BattleConfirmation)
			{
				if (DebugControl.StepByStepBattles)
				{
					if (!__instance.DoNextStep)
					{
						return false;
					}
					__instance.DoNextStep = false;
				}
				__instance.SimulationBattle.GetContender(__instance.CurrentContender.EmpireIndex);
				Empire empire2 = Amplitude.Mercury.Sandbox.Sandbox.Empires[__instance.CurrentContender.EmpireIndex];

				//Diagnostics.Log($"[Gedemon][BattleBrain] Run: CurrentContender Empire2 is major = {(empire2 is MajorEmpire)}, IsAIBrainActivated = {empire2.IsAIBrainActivated}, CurrentContender.EmpireIndex = {__instance.CurrentContender.EmpireIndex}");
				if (!(empire2 is MajorEmpire) || !empire2.IsAIBrainActivated)
				{
					// Gedemon <<<<<
					
					// Gedemon >>>>>

					__instance.LatestOrder = new OrderBattleConfirmation
					{
						BattleGUID = __instance.SimulationBattle.GUID,
						EmpireIndex = __instance.CurrentContender.EmpireIndex + (int)Posture.DeliberateAttack
					};
					__instance.LatestOrderTicket = SandboxManager.PostAndTrackOrder(__instance.LatestOrder, __instance.CurrentContender.EmpireIndex);
					__instance.BrainState = BattleBrainState.OrderValidation;
					//Diagnostics.Log($"[Gedemon][BattleBrain] Run: __instance.BrainState = {__instance.BrainState}");
				}
				else
				{
					__instance.BrainState = BattleBrainState.Sleep;
					//Diagnostics.Log($"[Gedemon][BattleBrain] Run: __instance.BrainState = {__instance.BrainState}");
				}
				return false;
			}

			return true;
		}

		[HarmonyPostfix]
		[HarmonyPatch(nameof(Assign))]
		public static void Assign(BattleBrain __instance, Battle simulationBattle)
		{
			//Diagnostics.LogWarning($"[Gedemon][BattleBrain] Assign (postfix): CurrentContender.EmpireIndex = {__instance.CurrentContender.EmpireIndex}, battle.GUID = {simulationBattle.GUID}");
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(SetContender))]
		public static bool SetContender(BattleBrain __instance, BattleController controller, ref BattleContender contender)
		{
			//Diagnostics.LogError($"[Gedemon][BattleBrain] SetContender: new contender.EmpireIndex = {contender.EmpireIndex}, current empire index = {__instance.CurrentContender.EmpireIndex}, battle.GUID = {__instance.SimulationBattle.GUID}");

			return true;
		}
	}


	[HarmonyPatch(typeof(Battle))]
	public class Battle_Patch
	{
				
		[HarmonyPrefix]
		[HarmonyPatch(nameof(SetAndReportAutoResolve))]
		public static bool SetAndReportAutoResolve(Battle __instance, int empireIndex, ref bool autoResolve)
		{
			//Diagnostics.LogError($"[Gedemon][Battle] SetAndReportAutoResolve Prefix: GUID = {__instance.GUID}, state = {__instance.BattleState}, HasBeenAutoResolve = {__instance.HasBeenAutoResolve}, empireIndex = {empireIndex}, autoResolve = {autoResolve}");

			if (!BattlePosture.UsePosture) { return true; }
			
			//int initialEmpireIndex = empireIndex;
			//Posture battlePosture = BattlePosture.GetPosture(empireIndex);
			//empireIndex = BattlePosture.GetIndex(empireIndex);
			//BattleExtension battleExtension = BattleSaveExtension.GetExtension(__instance.GUID);
			//Battle battle = Amplitude.Mercury.Sandbox.Sandbox.BattleRepository.GetBattle(__instance.GUID);
			//Diagnostics.LogWarning($"[Gedemon][Battle] SetAndReportAutoResolve: orderBattle.EmpireIndex = {initialEmpireIndex}, GetIndex = {empireIndex}, Posture = {battlePosture}, battle GUID = {battle.GUID}, is attacker = { battle.AttackerGroup.GetContenderIndex(empireIndex) != -1}");
			//
			//
			//int contenderIndex = battle.AttackerGroup.GetContenderIndex(empireIndex);
			//if (contenderIndex != -1)
			//{
			//	battleExtension.AttackerPosture = battlePosture;
			//}
			//else
			//{
			//	battleExtension.DefenderPosture = battlePosture;
			//}

			autoResolve = true;
			return false;
		}


		[HarmonyPrefix]
		[HarmonyPatch(nameof(ExecuteBattleConfirmation))]
		public static bool ExecuteBattleConfirmation(Battle __instance, ref int empireIndex)
		{

			//Diagnostics.LogWarning($"[Gedemon][Battle] ExecuteBattleConfirmation Prefix: GUID = {__instance.GUID}, state = {__instance.BattleState}, HasBeenAutoResolve = {__instance.HasBeenAutoResolve}, empireIndex = {empireIndex}");

			if (!BattlePosture.UsePosture) { return true; }

			int fakeEmpireIndex = empireIndex;
			Posture battlePosture = BattlePosture.GetPosture(empireIndex);
			empireIndex = BattlePosture.GetIndex(empireIndex);
			BattleExtension battleExtension = BattleSaveExtension.GetExtension(__instance.GUID);
			Battle battle = Amplitude.Mercury.Sandbox.Sandbox.BattleRepository.GetBattle(__instance.GUID);
			Diagnostics.LogWarning($"[Gedemon][Battle] ExecuteBattleConfirmation PREFIX : fakeEmpireIndex = {fakeEmpireIndex}, GetIndex = {empireIndex}, Posture = {battlePosture}, battle GUID = {battle.GUID}, is attacker = { battle.AttackerGroup.GetContenderIndex(empireIndex) != -1}");
			
			int contenderIndex = battle.AttackerGroup.GetContenderIndex(empireIndex);
			if (contenderIndex != -1)
			{
				battleExtension.AttackerPosture = battlePosture;
			}
			else
			{
				battleExtension.DefenderPosture = battlePosture;
			}

			return true;
		}

		[HarmonyPostfix]
		[HarmonyPatch(nameof(ExecuteBattleConfirmation))]
		public static void ExecuteBattleConfirmationPost(Battle __instance, int empireIndex)
		{
			Diagnostics.LogError($"[Gedemon][Battle] ExecuteBattleConfirmation POSTFIX: GUID = {__instance.GUID}, state = {__instance.BattleState}, HasBeenAutoResolve = {__instance.HasBeenAutoResolve}, empireIndex = {empireIndex}");

			foreach (KeyValuePair<int, bool> item in __instance.ConfirmationReadyByEmpire)
			{
				if (!item.Value)
				{
					Diagnostics.Log($"[Gedemon][Battle] ExecuteBattleConfirmation Empire # {item.Key} not ready (wants autoresolve = {__instance.GetContender(item.Key).WantsAutoResolve}) -> Abort Autoresolve");
					return;
				}
                else
				{
					Diagnostics.Log($"[Gedemon][Battle] ExecuteBattleConfirmation Empire # {item.Key} ready (wants autoresolve = {__instance.GetContender(item.Key).WantsAutoResolve})");
				}
			}

			if (!BattlePosture.UsePosture) { return; }

			// launch when everyone confirmed
			__instance.StateMachine.ChangeState(BattleState.AutoResolve);
		}

		[HarmonyPatch("Serialize")]
		[HarmonyPostfix]
		public static void Serialize(Battle __instance, Serializer serializer)
		{

			switch (serializer.SerializationMode)
			{
				case SerializationMode.Read:
					{
						BattleExtension battleExtension  = serializer.SerializeElement("battleExtension", new BattleExtension());
						BattleSaveExtension.BattleExensions.Add(__instance.GUID, battleExtension);
						break;
					}
				case SerializationMode.Write:
					{
						BattleExtension battleExtension = BattleSaveExtension.BattleExensions[__instance.GUID];
						serializer.SerializeElement("battleExtension", battleExtension);
						break;
					}
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(nameof(Initialize))]
		public static void Initialize(Battle __instance)
		{
			Diagnostics.LogWarning($"[Gedemon][Battle] Initialize: GUID  = {__instance.GUID}");

			foreach (BattleContender battleContender in __instance.AttackerGroup.Contenders)
			{
				Diagnostics.Log($"[Gedemon][Battle] AttackerGroup:  {battleContender.EmpireIndex}");
			}
			foreach (BattleContender battleContender in __instance.DefenderGroup.Contenders)
			{
				Diagnostics.Log($"[Gedemon][Battle] DefenderGroup:  {battleContender.EmpireIndex}");
			}

			BattleExtension battleExtension = new BattleExtension();
			BattleSaveExtension.BattleExensions.Add(__instance.GUID, battleExtension);

			if (!BattlePosture.UsePosture) { return; }

		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(Begin))]
		public static void Begin(Battle battle, BattleState oldState, BattleState state)
		{
			Diagnostics.LogWarning($"[Gedemon][Battle] Begin (prefix): GUID = {battle.GUID}, oldState = {oldState}, state = {state}, HasBeenAutoResolve = {battle.HasBeenAutoResolve}");
			if(state == BattleState.Finished)
            {

				//BattleExtension battleExtension = BattleSaveExtension.GetExtension(battle.GUID);
				if (battle.VictoryType == BattleVictoryType.Retreat)
				{
					battle.FillAndSendFinalAftermathInfo();
				}
			}			
		}

		[HarmonyPostfix]
		[HarmonyPatch(nameof(Begin))]
		public static void BeginPost(Battle battle, BattleState oldState, BattleState state)
		{
			Diagnostics.LogWarning($"[Gedemon][Battle] Begin (postfix): GUID = {battle.GUID}, oldState = {oldState}, state = {state}, HasBeenAutoResolve = {battle.HasBeenAutoResolve}");
		}
	}

	[HarmonyPatch(typeof(DepartmentOfBattles))]
	public class DepartmentOfBattles_Patch
	{
		[HarmonyPrefix]
		[HarmonyPatch(nameof(ValidateOrderBattleConfirmation))]
		public static bool ValidateOrderBattleConfirmation(DepartmentOfBattles __instance, OrderBattleConfirmation orderBattle)
		{

			if (!BattlePosture.UsePosture) { return true; }

			int empireIndex = BattlePosture.GetIndex(orderBattle.EmpireIndex);
			Posture battlePosture = BattlePosture.GetPosture(orderBattle.EmpireIndex);
			BattleExtension battleExtension = BattleSaveExtension.GetExtension(orderBattle.BattleGUID);
			Battle battle = Amplitude.Mercury.Sandbox.Sandbox.BattleRepository.GetBattle(orderBattle.BattleGUID);

			//if (empireIndex <= 16)
				//Diagnostics.LogWarning($"[Gedemon][DepartmentOfBattles] ValidateOrderBattleConfirmation: orderBattle.EmpireIndex = {orderBattle.EmpireIndex}, GetIndex = {empireIndex}, Posture = {battlePosture}, battle GUID = {battle.GUID}, is attacker = { battle.AttackerGroup.GetContenderIndex(empireIndex) != -1}");

			// changing back Order to real empire index before calling validation 
			orderBattle.EmpireIndex = empireIndex;

			// Set the posture from AI calls to ValidateOrderBattleConfirmation on host
			int contenderIndex = battle.AttackerGroup.GetContenderIndex(empireIndex);
			if (contenderIndex != -1)
			{
				battleExtension.AttackerPosture = battlePosture;
			}
			else
			{
				battleExtension.DefenderPosture = battlePosture;
			}
			
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(ProcessOrderBattleConfirmation))]
		public static bool ProcessOrderBattleConfirmation(DepartmentOfBattles __instance, OrderBattleConfirmation orderBattle)
		{

			if (!BattlePosture.UsePosture) { return true; }

			BattleExtension battleExtension = BattleSaveExtension.GetExtension(orderBattle.BattleGUID);
			Battle battle = Amplitude.Mercury.Sandbox.Sandbox.BattleRepository.GetBattle(orderBattle.BattleGUID);

			// add back BattlePosture in the index if needed after being removed from ValidateOrderBattleConfirmation
			if (orderBattle.EmpireIndex < BattlePosture.MaximalRealIndex)
            {
				int contenderIndex = battle.AttackerGroup.GetContenderIndex(orderBattle.EmpireIndex);
				if (contenderIndex != -1)
				{
					orderBattle.EmpireIndex += (int)battleExtension.AttackerPosture;
				}
				else
				{
					orderBattle.EmpireIndex += (int)battleExtension.DefenderPosture;
				}
			}

			//int initialEmpireIndex = orderBattle.EmpireIndex;

			int empireIndex = BattlePosture.GetIndex(orderBattle.EmpireIndex);
			Posture battlePosture = BattlePosture.GetPosture(orderBattle.EmpireIndex);

			//if(empireIndex<=16)
				//Diagnostics.LogWarning($"[Gedemon][DepartmentOfBattles] ProcessOrderBattleConfirmation: orderBattle.EmpireIndex = {orderBattle.EmpireIndex}, GetIndex = {empireIndex}, Posture = {battlePosture}, battle GUID = {battle.GUID}, is attacker = { battle.AttackerGroup.GetContenderIndex(empireIndex) != -1}");



			Amplitude.Mercury.Sandbox.Sandbox.BattleRepository.GetBattle(orderBattle.BattleGUID).OrderProcessor.ProcessOrderBattleConfirmation(orderBattle);

			//orderBattle.EmpireIndex = initialEmpireIndex;
			return false;
		}
		

		//
		[HarmonyPrefix]
		[HarmonyPatch(nameof(ProcessOrderCreateBattle))]
		public static bool ProcessOrderCreateBattle(DepartmentOfBattles __instance, OrderCreateBattle order)
		{
			Diagnostics.LogWarning($"[Gedemon][DepartmentOfBattles] ProcessOrderCreateBattle: autoResolve = {order.UseAutoResolve}");
			return true;
		}
	}



	[HarmonyPatch(typeof(BattleScreen_BattleActions))]
	public class BattleScreen_BattleActions_Patch
	{

		[HarmonyPrefix]
		[HarmonyPatch(nameof(ConfirmButton_LeftClick))]
		public static bool ConfirmButton_LeftClick(BattleScreen_BattleActions __instance)
		{
			Diagnostics.LogWarning($"[Gedemon][BattleScreen_BattleActions] ConfirmButton_LeftClick");

			if (!BattlePosture.UsePosture) { return true; }

			if (__instance.battleScreen.PresentationBattle != null)
			{
				SelectOffensivePosture(__instance);
			}
			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(InstantResolveButton_LeftClick))]
		public static bool InstantResolveButton_LeftClick(BattleScreen_BattleActions __instance)
		{
			Diagnostics.LogWarning($"[Gedemon][BattleScreen_BattleActions] InstantResolveButton_LeftClick");

			if (!BattlePosture.UsePosture) { return true; }

			if (__instance.battleScreen.PresentationBattle != null)
			{
				SelectDefensivePosture(__instance);
			}
			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(RetreatButton_LeftClick))]
		public static bool RetreatButton_LeftClick(BattleScreen_BattleActions __instance)
		{
			Diagnostics.LogWarning($"[Gedemon][BattleScreen_BattleActions] RetreatButton_LeftClick");

			if (!BattlePosture.UsePosture) { return true; }

			if (__instance.battleScreen.PresentationBattle != null)
			{
				int num = ((BattleDebug.GetCheat(BattleCheatType.IgnoreEmpirePlaying) || GodMode.Enabled) ? (-1) : __instance.empireIndex);
				num += (int)Posture.Retreat;
				ConfirmPostureOrder(__instance, num);
			}
			return false;
		}

		[HarmonyPostfix]
		[HarmonyPatch(nameof(Refresh))]
		public static void Refresh(BattleScreen_BattleActions __instance, PresentationBattle presentationBattle, ref PresentationBattleContender presentationBattleContender, bool instant = false)
		{

			if (!BattlePosture.UsePosture) { return; }

			if (presentationBattle.CurrentBattleState != PresentationBattleStatus.Sieging)
			{
				Diagnostics.LogWarning($"[Gedemon][BattleScreen_BattleActions] Refresh: CurrentBattleState = {presentationBattle.CurrentBattleState}");

				__instance.confirmButton.label.Text = "Offensive postures";
				__instance.confirmButton.TooltipTarget.Title = "Offensive posture";
				__instance.confirmButton.TooltipTarget.Description = "Select an offensive posture for your army";

				__instance.instantResolveButton.label.Text = "Defensive postures";
				__instance.instantResolveButton.TooltipTarget.Title = "Defensive posture";
				__instance.instantResolveButton.TooltipTarget.Description = "Select a defensive posture for your army";

				__instance.confirmButton.Appendix<ActionItem_Blink>()?.Refresh(false);

				//if (__instance.continueSiegeToggle.UpdateVisibility(UIBattleActionsFailureFlags.None))
				//{
				//	__instance.continueSiegeToggle.SetFailures(UIBattleActionsFailureFlags.None);
				//}

			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(nameof(CanRetreatOperation_UponCompletion))]
		public static void CanRetreatOperation_UponCompletion(BattleScreen_BattleActions __instance, RequestAsyncOperation operation)
		{

			if (!BattlePosture.UsePosture) { return; }

			__instance.retreatButton.UITransform.VisibleSelf = false;

		}

		private static void SelectOffensivePosture(BattleScreen_BattleActions battleScreen)
		{
			int num = ((BattleDebug.GetCheat(BattleCheatType.IgnoreEmpirePlaying) || GodMode.Enabled) ? (-1) : battleScreen.empireIndex);
			var message = new MessageModalWindow.Message
			{
				Title = "Offensive Postures",
				Description = "Chose your Army Posture",
				Buttons = new[]
				{
					new MessageBoxButton.Data(new StaticString("DeliberateAttack"), () =>  ConfirmPostureOrder(battleScreen, num +  (int)Posture.DeliberateAttack), isDismiss: true),
					new MessageBoxButton.Data(new StaticString("MobileAttack"), () =>  ConfirmPostureOrder(battleScreen, num +  (int)Posture.MobileAttack), isDismiss: true),
					new MessageBoxButton.Data(new StaticString("AllOutAttack"), () =>  ConfirmPostureOrder(battleScreen, num +  (int)Posture.AllOutAttack), isDismiss: true),
					new MessageBoxButton.Data(new StaticString("Pursuit"), () =>  ConfirmPostureOrder(battleScreen, num +  (int)Posture.Pursuit), isDismiss: true),

					new MessageBoxButton.Data(MessageBox.Choice.Cancel, null, isDismiss: true)
				}
			};
			MessageModalWindow.ShowMessage(message);
		}

		private static void SelectDefensivePosture(BattleScreen_BattleActions battleScreen)
		{
			int num = ((BattleDebug.GetCheat(BattleCheatType.IgnoreEmpirePlaying) || GodMode.Enabled) ? (-1) : battleScreen.empireIndex);
			var message = new MessageModalWindow.Message
			{
				Title = "Defensive Postures",
				Description = "Chose your Army Posture",
				Buttons = new[]
				{
					new MessageBoxButton.Data(new StaticString("PreparedDefense"), () =>  ConfirmPostureOrder(battleScreen, num +  (int)Posture.PreparedDefense), isDismiss: true),
					new MessageBoxButton.Data(new StaticString("HastyDefense"), () =>  ConfirmPostureOrder(battleScreen, num +  (int)Posture.HastyDefense), isDismiss: true),
					new MessageBoxButton.Data(new StaticString("Delay"), () =>  ConfirmPostureOrder(battleScreen, num +  (int)Posture.Delay), isDismiss: true),
					new MessageBoxButton.Data(new StaticString("Retreat"), () =>  ConfirmPostureOrder(battleScreen, num +  (int)Posture.Retreat), isDismiss: true),

					new MessageBoxButton.Data(MessageBox.Choice.Cancel, null, isDismiss: true)
				}
			};
			MessageModalWindow.ShowMessage(message);
		}

		private static bool ConfirmPostureOrder(BattleScreen_BattleActions battleScreen, int num)
		{
			int empireIndex = BattlePosture.GetIndex(num);
			Posture battlePosture = BattlePosture.GetPosture(num);
			BattleExtension battleExtension = BattleSaveExtension.GetExtension(battleScreen.battleGUID);
			Battle battle = Amplitude.Mercury.Sandbox.Sandbox.BattleRepository.GetBattle(battleScreen.battleGUID);
			Diagnostics.LogWarning($"[Gedemon][DepartmentOfBattles] ConfirmPostureOrder: num (fake empireIndex) = {num}, GetIndex = {empireIndex}, Posture = {battlePosture}, battle GUID = {battle.GUID}, is attacker = { battle.AttackerGroup.GetContenderIndex(empireIndex) != -1}");

			//*
			// need to do that locally as ValidateOrderBattleConfirmation is called only by host
			int contenderIndex = battle.AttackerGroup.GetContenderIndex(empireIndex);
			if (contenderIndex != -1)
			{
				battleExtension.AttackerPosture = battlePosture;
			}
			else
			{
				battleExtension.DefenderPosture = battlePosture;
			}

			SandboxManager.PostOrder(new OrderBattleConfirmation(battleScreen.battleGUID, num), battleScreen.empireIndex);
			return true;
		}

		[HarmonyPostfix]
		[HarmonyPatch(nameof(PostLoad))]
		public static void PostLoad(BattleScreen_BattleActions __instance, IBattleScreen battleScreen)
		{
			//Diagnostics.LogError($"[Gedemon][BattleScreen_BattleActions] PostLoad");

			if (!BattlePosture.UsePosture) { return; }

			//BattleActionsExtension extension = new BattleActionsExtension();
			//extension.Pursuit.Bind(UIBattleActionsType.UnitMoveTo, ActionItemTooltipAutoFill.All, UIBattleActionsFailureFlags.None, BattleScreen_BattleActions.ConfirmHidingFailureFlags);
			//extension.AllOutAttack.Bind(UIBattleActionsType.UnitSkipOneRound, ActionItemTooltipAutoFill.All, UIBattleActionsFailureFlags.None, BattleScreen_BattleActions.ConfirmHidingFailureFlags);
			//
			//BattleActionsExtensions.Add(__instance.battleGUID, extension);
		}
	}

	[HarmonyPatch(typeof(BattleAftermathScreen_BattleStatus))]
	public class BattleAftermathScreen_BattleStatus_Patch : MonoBehaviour
	{

		//static UILabel summary = new UILabel();

		static string labelName = "SummaryLabel";

		[HarmonyPostfix]
		[HarmonyPatch(nameof(RefreshTurns))]
		public static void RefreshTurns(BattleAftermathScreen_BattleStatus __instance, ref BattleAftermathInfo aftermathInfo)
		{
			Diagnostics.LogWarning($"[Gedemon][BattleAftermathScreen_BattleStatus] RefreshTurns");

			if(!BattlePosture.UsePosture) { return; }

			if (!(aftermathInfo.BattleGUID == SimulationEntityGUID.Zero))
			{
				BattleExtension battleExtension = BattleSaveExtension.GetExtension(aftermathInfo.BattleGUID);

				var labelParent = __instance.titleLabel.transform.parent;
				var labelContainer = labelParent.transform.gameObject;

				// add empty object to instance, to show summary once per instance
				var summaryObject = labelContainer.transform.Find(labelName)?.gameObject;
				if(summaryObject == null) // 
				{
					summaryObject = new GameObject(labelName) { };					
					summaryObject.AddComponent<UILabel>();
					summaryObject.transform.parent = labelContainer.transform;

					ShowSummary(battleExtension.BattleSummary);
				}

				__instance.turnsLabel.Text = Environment.NewLine + "Attacker posture : " + BattlePosture.GetPostureString(battleExtension.AttackerPosture) + Environment.NewLine + "Defender posture : " + BattlePosture.GetPostureString(battleExtension.DefenderPosture);
				__instance.turnsLabel.SetHorizontalAlignment(HorizontalAlignment.Left);
				__instance.turnsLabel.SetVerticalAlignment(VerticalAlignment.Top);

			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(Unbind))]
		public static bool Unbind(BattleAftermathScreen_BattleStatus __instance)
		{
			Diagnostics.LogWarning($"[Gedemon][className] Unbind");

			var labelParent = __instance.titleLabel.transform.parent;
			var labelContainer = labelParent.transform.gameObject;

			// add empty object to instance, to show summary once per instance
			var summaryObject = labelContainer.transform.Find(labelName)?.gameObject;
			if (summaryObject != null)
            {
				summaryObject.transform.SetParent(null);
				Destroy(summaryObject);
			}

			return true;
		}

		static void ShowSummary(string summary)
		{
			Diagnostics.LogWarning($"[Gedemon][BattleAftermathScreen_BattleStatus] ShowSummary");

			if (summary == string.Empty)
				return;

			var message = new MessageModalWindow.Message
			{
				Title = "Battle Summary",
				Description = summary,
				Buttons = new[]	{ new MessageBoxButton.Data(MessageBox.Choice.Acknowledge, null, isDismiss: true) }
			};
			MessageModalWindow.ShowMessage(message);
		}

	}

	[HarmonyPatch(typeof(PresentationBattle))]
	public class PresentationBattle_Patch
	{
		[HarmonyPrefix]
		[HarmonyPatch(nameof(OnSetAutoResolveFor))]
		public static bool OnSetAutoResolveFor(PresentationBattle __instance, ref BattleReportAction battleReportAction)
		{
			//Diagnostics.LogError($"[Gedemon][PresentationBattle] OnSetAutoResolveFor Empire Index = {battleReportAction.EmpireIndex}");

			return true;
		}
	}

	[HarmonyPatch(typeof(BattleArena))]
	public class BattleArena_Patch
	{
		[HarmonyPrefix]
		[HarmonyPatch(nameof(GetDeploymentWidth))]
		public static bool GetDeploymentWidth(BattleArena __instance, ref int __result)
		{
			//Diagnostics.LogWarning($"[Gedemon][BattleArena] GetDeploymentWidth");
			__result = 1;
			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(GetDeploymentHeight))]
		public static bool GetDeploymentHeight(BattleArena __instance, ref int __result)
		{
			//Diagnostics.LogWarning($"[Gedemon][BattleArena] GetDeploymentHeight");
			__result = 1;
			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(GetBattleArenaWidth))]
		public static bool GetBattleArenaWidth(ref int __result)
		{
			Diagnostics.LogWarning($"[Gedemon][BattleArena] GetBattleArenaWidth (global era = {Sandbox.Timeline.GetGlobalEraIndex()})");
			__result = 2 + Sandbox.Timeline.GetGlobalEraIndex();
			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(GetBattleArenaHeight))]
		public static bool GetBattleArenaHeight(ref int __result)
		{
			//Diagnostics.LogWarning($"[Gedemon][BattleArena] GetBattleArenaHeight");
			__result = 2 + Sandbox.Timeline.GetGlobalEraIndex();
			return false;
		}
	}

	/*
	[HarmonyPatch(typeof(className))]
	public class Class_Patch
	{
		[HarmonyPrefix]
		[HarmonyPatch(nameof(method))]
		public static bool method(className __instance)
		{
			Diagnostics.LogWarning($"[Gedemon][className] method");

			return true;
		}		
	}
	//*/
}
