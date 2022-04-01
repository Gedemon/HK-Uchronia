using System.Collections.Generic;
using Amplitude.Mercury.Simulation;
using HarmonyLib;
using Amplitude;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Serialization;
using Amplitude.Mercury;
using Diagnostics = Amplitude.Diagnostics;
using System;
using UnityEngine;
using Amplitude.Mercury.Data;

namespace Gedemon.Uchronia
{

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
						BattleExtension battleExtension = serializer.SerializeElement("battleExtension", new BattleExtension());
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
		public static void Initialize(Battle __instance, ISimulationTargetToBattle attacker, ISimulationTargetToBattle defender)
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


			if (!BattlePosture.UsePosture) { return; }

			BattleExtension battleExtension = new BattleExtension();

			District district = defender as District;
			if (district == null)
			{
				Army army = defender as Army;
				if (army.HasUnitStatus(DepartmentOfDefense.RetreatedUnitStatusName))
				{
					battleExtension.DefenderWasRetreating = true;
				}
			}

			battleExtension.SiegeState = __instance.Siege != null ? __instance.Siege.SiegeState : SiegeStates.None;

			BattleSaveExtension.BattleExensions.Add(__instance.GUID, battleExtension);

		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(Begin))]
		public static void Begin(Battle battle, BattleState oldState, BattleState state)
		{
			Diagnostics.LogWarning($"[Gedemon][Battle] Begin (prefix): GUID = {battle.GUID}, oldState = {oldState}, state = {state}, HasBeenAutoResolve = {battle.HasBeenAutoResolve}");
			if (state == BattleState.Finished)
			{

				// The main Battle function doesn't handle some cases, so we're handling those here

				//BattleExtension battleExtension = BattleSaveExtension.GetExtension(battle.GUID);
				if (battle.VictoryType == BattleVictoryType.Retreat)
				{
					// this is not called for the base game (no battle when retreating)
					// but in the mod Withdrawal, Routed and Retreat happen after combat in most cases
					battle.FillAndSendFinalAftermathInfo();

					// this one also not handled for defenders in cities (always killed in vanilla)
					// attacker retreat is called using attrition defeat type in that case, set in auto-resolve
					if (battle.Siege != null && battle.DefenderGroup.Result == BattleResult.Defeat)
                    {
						battle.ForceMainArmyMovement(battle.DefenderGroup);
                    }
				}
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(nameof(Begin))]
		public static void BeginPost(Battle battle, BattleState oldState, BattleState state)
		{
			Diagnostics.LogWarning($"[Gedemon][Battle] Begin (postfix): GUID = {battle.GUID}, oldState = {oldState}, state = {state}, HasBeenAutoResolve = {battle.HasBeenAutoResolve}");
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(ExecuteBattleComplete))]
		[HarmonyPatch(new Type[] { typeof(BattleGroup), typeof(BattleVictoryType) })]
		public static bool ExecuteBattleComplete(ref Battle __instance, BattleGroup winnerGroup, BattleVictoryType victoryType)
		{
			if (__instance.Siege != null)
			{
				Diagnostics.LogWarning($"[Gedemon][Battle] ExecuteBattleComplete for siege (prefix): SiegeState = {__instance.Siege.SiegeState}, victoryType = {victoryType}, winnerGroup.Role = {winnerGroup.Role}, winnerGroup.LeaderEmpireIndex = {winnerGroup.LeaderEmpireIndex}");
			}
			else
			{
				Diagnostics.LogWarning($"[Gedemon][Battle] ExecuteBattleComplete (prefix): victoryType = {victoryType}, winnerGroup.Role = {winnerGroup.Role}, winnerGroup.LeaderEmpireIndex = {winnerGroup.LeaderEmpireIndex}");
			}

			{
				__instance.VictoryType = victoryType;
				BattleGroup battleGroup = __instance.DefenderGroup;
				if (winnerGroup.Role == Amplitude.Mercury.Interop.BattleGroupRoleType.Defender)
				{
					battleGroup = __instance.AttackerGroup;
				}
				winnerGroup.SetResult(__instance, BattleResult.Victory);
				battleGroup.SetResult(__instance, BattleResult.Defeat);
				__instance.DistributeSpoilsOfWar(winnerGroup);
				if (__instance.VictoryType == BattleVictoryType.Extermination || __instance.VictoryType == BattleVictoryType.Attrition)
				{
					__instance.RegenArmiesAfterBattle(winnerGroup);
					__instance.RegenArmiesAfterBattle(battleGroup);
				}
				SimulationEvent_BattleTerminated.Raise(__instance, __instance);
				__instance.OnBattleTerminated();
				if (__instance.Siege != null && __instance.Siege.SiegeState == SiegeStates.Sortie && (victoryType == BattleVictoryType.Attrition || victoryType == BattleVictoryType.Retreat) && winnerGroup.Role == Amplitude.Mercury.Interop.BattleGroupRoleType.Attacker)
				{
					return false;
				}
				if (victoryType == BattleVictoryType.Extermination || victoryType == BattleVictoryType.Retreat || victoryType == BattleVictoryType.Surrender || victoryType == BattleVictoryType.NoContest)
				{
					Diagnostics.Log($"[Gedemon][Battle] ExecuteBattleComplete (victoryType == BattleVictoryType.Extermination || victoryType == BattleVictoryType.Retreat || victoryType == BattleVictoryType.Surrender || victoryType == BattleVictoryType.NoContest)");
					Empire empire = Amplitude.Mercury.Sandbox.Sandbox.Empires[winnerGroup.LeaderEmpireIndex];
					int length = __instance.CapturePoints.Length;
					for (int i = 0; i < length; i++)
					{
						ref BattleCapturePoint reference = ref __instance.CapturePoints.Data[i];
						Diagnostics.Log($"[Gedemon][Battle] ExecuteBattleComplete CapturePoints.Data[i={i}] ownergroup = {reference.OwnerGroup} != winnerGroup.Role = {winnerGroup.Role} ?, OwnerEmpireIndex = {reference.OwnerEmpireIndex}, empire.Index = {empire.Index}");
						if (reference.OwnerGroup != winnerGroup.Role)
						{
							reference.OwnerGroup = winnerGroup.Role;
							reference.OwnerEmpireIndex = empire.Index;
						}
					}
				}
				int length2 = __instance.CapturePoints.Length;
				for (int j = 0; j < length2; j++)
				{
					ref BattleCapturePoint reference2 = ref __instance.CapturePoints.Data[j];
					Diagnostics.Log($"[Gedemon][Battle] ExecuteBattleComplete CapturePoints.Data[j={j}] ownergroup = {reference2.OwnerGroup}, OwnerEmpireIndex = {reference2.OwnerEmpireIndex}");



					if (Amplitude.Mercury.Sandbox.Sandbox.SimulationEntityRepository.TryGetSimulationEntity(reference2.OriginatorGUID, out SimulationEntity entity))
					{
						Settlement settlement = entity as Settlement;
						if (settlement != null)
						{
							Diagnostics.Log($"[Gedemon][Battle] ExecuteBattleComplete CapturePoints.Data[j={j}] is settlement (owner = {settlement.Empire.Entity.Index})");
							Empire newOwner = Amplitude.Mercury.Sandbox.Sandbox.Empires[reference2.OwnerEmpireIndex];
							DepartmentOfDefense.CaptureCity(settlement, newOwner, winnerGroup);
						}
					}
				}
			}


			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(ForceMainArmyMovement))]
		public static bool ForceMainArmyMovement(Battle __instance, BattleGroup retreatingGroup)
		{
			Diagnostics.LogWarning($"[Gedemon][Battle] ForceMainArmyMovement: GUID = {__instance.GUID}, retreating group role = {retreatingGroup.Role}");

			if (retreatingGroup.Contenders.Count == 0) // || retreatingGroup.Contenders[0].Participants.Count == 0)
			{
				Diagnostics.Log($"[Gedemon][Battle] ForceMainArmyMovement: retreatingGroup.Contenders.Count == 0");
				return false;
			}

			if (__instance.Siege != null)
			{
				Diagnostics.Log($"[Gedemon][Battle] ForceMainArmyMovement: was siege, calling ForceAllArmiesMovement");

				ForceAllArmiesMovement(__instance, retreatingGroup);
			}
			else
			{
				Diagnostics.Log($"[Gedemon][Battle] ForceMainArmyMovement: not siege, calling ForceArmyMovement for main participant");

				Participant mainParticipant = null;
				__instance.TryGetMainParticipantFromBattleGroup(retreatingGroup, out mainParticipant);
				Participant_Army participant_Army = mainParticipant as Participant_Army;
				if (participant_Army == null)
				{
					return false;
				}

				Army army = participant_Army.Army;
				__instance.RemoveParticipant(retreatingGroup, 0, participant_Army);
				participant_Army.Release(raiseSimulationEvent: true);
				ForceArmyMovement(__instance, retreatingGroup, army);
			}

			return false;
			/*
			if (retreatingGroup.Contenders.Count == 0 || retreatingGroup.Contenders[0].Participants.Count == 0)
			{
				return false;
			}

			Participant mainParticipant = null;
			__instance.TryGetMainParticipantFromBattleGroup(retreatingGroup, out mainParticipant);
			Participant_Army participant_Army = mainParticipant as Participant_Army;
			if (participant_Army == null)
			{
				return false;
			}

			Army army = participant_Army.Army;
			__instance.RemoveParticipant(retreatingGroup, 0, participant_Army);
			participant_Army.Release(raiseSimulationEvent: true);
			if (army.Units.Count == 0)
			{
				return false;
			}

			BattleGroup obj = (retreatingGroup.Role == __instance.DefenderGroup.Role) ? __instance.AttackerGroup : __instance.DefenderGroup;
			int num = obj.OriginPosition.ToTileIndex();
			PathfindContext pathfindContext = PathfindContext.GetArmyPathfindContextForWorld(army);
			ReachableResults reachableResults = default(ReachableResults);
			Army army2 = obj.GetMainArmy() as Army;
			if (army2 != null)
			{
				pathfindContext.RetreatZoneOfControlIndex = army2.ZoneOfControlIndex;
			}

			if (Battle.GetReachableRetreatDestinations(ref pathfindContext, ref reachableResults) != PathResultStatus.Success)
			{
				Battle.DestroyFailedRetreatingArmy(army, num, __instance.GUID);
				return false;
			}

			int directionToTileIndex = (int)WorldPosition.GetDirectionToTileIndex(num, army.WorldPosition.ToTileIndex());
			int numberOfTiles = reachableResults.NumberOfTiles;
			int num2 = 1;
			int num3 = int.MaxValue;
			int num4 = -1;
			for (int i = 0; i < numberOfTiles; i++)
			{
				int num5 = reachableResults.ReachableTileIndexes[i];
				int tileIndexDistance = WorldPosition.GetTileIndexDistance(num, num5);
				if (tileIndexDistance >= num2)
				{
					int num6 = Mathf.Abs((int)(directionToTileIndex - WorldPosition.GetDirectionToTileIndex(num, num5)));
					if (tileIndexDistance != num2 || num6 >= num3)
					{
						num2 = tileIndexDistance;
						num3 = num6;
						num4 = num5;
					}
				}
			}

			if (num4 < 0 || num2 <= 1)
			{
				Battle.DestroyFailedRetreatingArmy(army, num, __instance.GUID);
				return false;
			}

			PathfindSetupData pathfindSetupData = default(PathfindSetupData);
			pathfindSetupData.ResetAll();
			pathfindSetupData.StartingWorldPosition = pathfindContext.WorldPosition;
			pathfindSetupData.DestinationTileIndex = num4;
			pathfindContext.MovementRatio = FixedPoint.HalfOne; // FixedPoint.One
			Battle.astarResults.Clear();
			Amplitude.Mercury.Sandbox.Sandbox.PathfindManager.FindPath(ref pathfindContext, ref pathfindSetupData, ref Battle.astarResults);
			if (Battle.astarResults.ResultStatus != PathResultStatus.Success)
			{
				Battle.DestroyFailedRetreatingArmy(army, num, __instance.GUID);
				return false;
			}

			while (Battle.astarResults.StepCount > 0 && Battle.astarResults.Steps[Battle.astarResults.StepCount - 1].Turn > 1)
			{
				Battle.astarResults.RemoveAt(Battle.astarResults.StepCount - 1);
			}

			int tileIndex = Battle.astarResults.Steps[Battle.astarResults.StepCount - 1].TileIndex;
			if (Battle.astarResults.StepCount < 1 || WorldPosition.GetTileIndexDistance(num, tileIndex) <= 1)
			{
				Battle.DestroyFailedRetreatingArmy(army, num, __instance.GUID);
				return false;
			}

			DepartmentOfDefense.SetArmyMovementRatio(army, FixedPoint.HalfOne); // FixedPoint.One
			army.AddUnitStatus(DepartmentOfDefense.RetreatedUnitStatusName, StatusInitiatorType.Battle, -1);
			Amplitude.Mercury.Sandbox.Sandbox.World.AllocateArmyPathNodes(0uL, army, ref Battle.astarResults, 0);
			DepartmentOfTransportation.ExecuteMovement(army, ref Battle.astarResults);
			DepartmentOfDefense.SetArmyMovementRatio(army, FixedPoint.Zero);

			return false;
			//*/
		}

		internal static void ForceAllArmiesMovement(Battle battle, BattleGroup battleGroup)
		{
			List<BattleContender> contenders = new List<BattleContender>(battleGroup.Contenders);
			Diagnostics.Log($"[Gedemon][Battle] ForceAllArmiesMovement: GUID = {battle.GUID}, battleGroup contenders.Count = {contenders.Count}");
			for (int contenderIndex = 0; contenderIndex < contenders.Count; contenderIndex++)
			{
				//Diagnostics.LogWarning($"[Gedemon][Battle] ForceAllArmiesMovement: contenderIndex = {contenderIndex}");
				List<Participant> participants = new List<Participant>(contenders[contenderIndex].Participants);
				Diagnostics.Log($"[Gedemon][Battle] ForceAllArmiesMovement: battleGroup participants.Count = {participants.Count}");
				int count = participants.Count;
				for (int i = 0; i < count; i++)
				{
					//Diagnostics.LogWarning($"[Gedemon][Battle] ForceAllArmiesMovement: participantIndex = {i}");
					Participant participant = participants[i];
					//Diagnostics.LogWarning($"[Gedemon][Battle] ForceAllArmiesMovement: participant exists = {participant != null}");
					Participant_Army participant_Army = participant as Participant_Army;
					if (participant_Army != null)
					{
						Army army = participant_Army.Army;
						battle.RemoveParticipant(battleGroup, contenderIndex, participant_Army);
						participant_Army.Release(raiseSimulationEvent: true);
						ForceArmyMovement(battle, battleGroup, army);
					}
				}
			}
		}

		public static void ForceArmyMovement(Battle battle, BattleGroup retreatingGroup, Army army)
		{
			Diagnostics.LogWarning($"[Gedemon][Battle] ForceArmyMovement: army GUID = {army.GUID}");

			if (army.Units.Count == 0)
			{
				return;
			}

			BattleGroup obj = (retreatingGroup.Role == battle.DefenderGroup.Role) ? battle.AttackerGroup : battle.DefenderGroup;
			int num = obj.OriginPosition.ToTileIndex();
			PathfindContext pathfindContext = PathfindContext.GetArmyPathfindContextForWorld(army);
			ReachableResults reachableResults = default(ReachableResults);
			Army army2 = obj.GetMainArmy() as Army;
			if (army2 != null)
			{
				pathfindContext.RetreatZoneOfControlIndex = army2.ZoneOfControlIndex;
			}

			if (Battle.GetReachableRetreatDestinations(ref pathfindContext, ref reachableResults) != PathResultStatus.Success)
			{
				Battle.DestroyFailedRetreatingArmy(army, num, battle.GUID);
				return;
			}

			int directionToTileIndex = (int)WorldPosition.GetDirectionToTileIndex(num, army.WorldPosition.ToTileIndex());
			int numberOfTiles = reachableResults.NumberOfTiles;
			int num2 = 1;
			int num3 = int.MaxValue;
			int num4 = -1;
			for (int i = 0; i < numberOfTiles; i++)
			{
				int num5 = reachableResults.ReachableTileIndexes[i];
				int tileIndexDistance = WorldPosition.GetTileIndexDistance(num, num5);
				if (tileIndexDistance >= num2)
				{
					int num6 = Mathf.Abs((int)(directionToTileIndex - WorldPosition.GetDirectionToTileIndex(num, num5)));
					if (tileIndexDistance != num2 || num6 >= num3)
					{
						num2 = tileIndexDistance;
						num3 = num6;
						num4 = num5;
					}
				}
			}

			if (num4 < 0 || num2 <= 1)
			{
				Battle.DestroyFailedRetreatingArmy(army, num, battle.GUID);
				return;
			}

			PathfindSetupData pathfindSetupData = default(PathfindSetupData);
			pathfindSetupData.ResetAll();
			pathfindSetupData.StartingWorldPosition = pathfindContext.WorldPosition;
			pathfindSetupData.DestinationTileIndex = num4;
			pathfindContext.MovementRatio = FixedPoint.HalfOne; // FixedPoint.One
			Battle.astarResults.Clear();
			Amplitude.Mercury.Sandbox.Sandbox.PathfindManager.FindPath(ref pathfindContext, ref pathfindSetupData, ref Battle.astarResults);
			if (Battle.astarResults.ResultStatus != PathResultStatus.Success)
			{
				Battle.DestroyFailedRetreatingArmy(army, num, battle.GUID);
				return;
			}

			while (Battle.astarResults.StepCount > 0 && Battle.astarResults.Steps[Battle.astarResults.StepCount - 1].Turn > 1)
			{
				Battle.astarResults.RemoveAt(Battle.astarResults.StepCount - 1);
			}

			int tileIndex = Battle.astarResults.Steps[Battle.astarResults.StepCount - 1].TileIndex;
			if (Battle.astarResults.StepCount < 1 || WorldPosition.GetTileIndexDistance(num, tileIndex) <= 1)
			{
				Battle.DestroyFailedRetreatingArmy(army, num, battle.GUID);
				return;
			}

			DepartmentOfDefense.SetArmyMovementRatio(army, FixedPoint.HalfOne); // FixedPoint.One
			army.AddUnitStatus(DepartmentOfDefense.RetreatedUnitStatusName, StatusInitiatorType.Battle, -1);
			Amplitude.Mercury.Sandbox.Sandbox.World.AllocateArmyPathNodes(0uL, army, ref Battle.astarResults, 0);
			DepartmentOfTransportation.ExecuteMovement(army, ref Battle.astarResults);
			DepartmentOfDefense.SetArmyMovementRatio(army, FixedPoint.Zero);

			return;
		}

	}

}
