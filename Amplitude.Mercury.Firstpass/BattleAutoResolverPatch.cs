using System.Collections.Generic;
using Amplitude.Mercury.Simulation;
using HarmonyLib;
using Amplitude.Mercury.Sandbox;
using Amplitude;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury;
using Diagnostics = Amplitude.Diagnostics;
using BattleGroupRoleType = Amplitude.Mercury.Interop.BattleGroupRoleType;
using System;
using Amplitude.Framework.Simulation;
using Amplitude.Mercury.AI;

namespace Gedemon.Uchronia
{
	public class QJM
    {

		public static FixedPoint LowPenalty = (FixedPoint)0.9;
		public static FixedPoint MediumPenalty = (FixedPoint)0.8;
		public static FixedPoint HighPenalty = (FixedPoint)0.7;
		public static FixedPoint VeryHighPenalty = (FixedPoint)0.5;

		public static FixedPoint LowBonus = (FixedPoint)1.1;
		public static FixedPoint MediumBonus = (FixedPoint)1.25;
		public static FixedPoint HighBonus = (FixedPoint)1.5;
		public static FixedPoint VeryHighBonus = (FixedPoint)2.0;

		public static FixedPoint LowReducingModifier = (FixedPoint)0.9;
		public static FixedPoint MediumReducingModifier = (FixedPoint)0.75;
		public static FixedPoint HighReducingModifier = (FixedPoint)0.5;
		public static FixedPoint VeryHighReducingModifier = (FixedPoint)0.25;

		public static FixedPoint VeterancyForMobileAttack = (FixedPoint)1.0;
		public static FixedPoint MaxMoveModifier = (FixedPoint)2.0;
		public static FixedPoint MinMoveModifier = (FixedPoint)0.5;

		public QJM()
        {

        }

		static public FixedPoint GetPostureBaseModifier(Posture posture)
        {
			switch(posture)
            {
				case Posture.Delay:
					return (FixedPoint)1.2;
				case Posture.HastyDefense:
					return (FixedPoint)1.3;
				case Posture.PreparedDefense:
					return (FixedPoint)1.5;
				case Posture.Withdrawal:
					return (FixedPoint)1.15;

				default:
					return FixedPoint.One;
			}
        }

		static public FixedPoint GetVeterancyModifier(FixedPoint veterancyLevel)
        {
			FixedPoint green = (FixedPoint)0.5;
			FixedPoint average = (FixedPoint)0.8;
			FixedPoint good = FixedPoint.One;
			if (veterancyLevel < 1) // green
            {
				return green + (veterancyLevel / 2); // [0.5 - 1.0]
            }
			else if (veterancyLevel < 2) // average - good
			{
				return average + (veterancyLevel / 5); // [1.0 - 1.2]
			}
			else // good - elite
			{
				return good + (veterancyLevel / 6); // [1.3 - 1.5]
			}
		}
		static public FixedPoint GetFortificationModifier(FixedPoint fortificationStrength)
		{
			FixedPoint divider = 100 - fortificationStrength; // max fortificationStrength should be 95 at level 4 (shelter) but can get bonuses from terrain ?

			if (divider == 100)
				return 1;

			if (divider > 0)
				return 1 + (1 - 1 / divider); // [1.01 - 1.99]
			else
				return 2;
		}


		static public FixedPoint GetMoveModifier(FixedPoint attackerMoves, FixedPoint defenderMoves)
		{
			if (defenderMoves == 0 && attackerMoves == 0)
			{
				return FixedPoint.One;
			}

			if (defenderMoves == 0)
			{
				return MaxMoveModifier;
			}
			if (attackerMoves == 0)
			{
				return MinMoveModifier;
			}

			FixedPoint ratio = attackerMoves > defenderMoves ? (attackerMoves / defenderMoves) / 2 : (defenderMoves / attackerMoves) / 2;

			FixedPoint modifier = attackerMoves > defenderMoves ? 1 + ratio : 1 - ratio;

			return FixedPoint.Clamp(modifier, MinMoveModifier, MaxMoveModifier);
		}
	}
	public class UnitGroupStats
    {
		public string Name { get; set; }
		public FixedPoint NumUnits { get; set; }
		public FixedPoint TotalCombat { get; set; }
		public FixedPoint TotalMoves { get; set; }
		public FixedPoint TotalHealth { get; set; }
		public FixedPoint TotalVeterency { get; set; }
		public FixedPoint TotalExperience { get; set; }
		public FixedPoint AverageCombat { get; set; }
		public FixedPoint AverageMoves { get; set; }
		public FixedPoint AverageHealth { get; set; }
		public FixedPoint AverageVeterency { get; set; }
		public FixedPoint AverageExperience { get; set; }
		public FixedPoint Fortification { get; set; }
		public FixedPoint CombatStrength { get; set; }
		public FixedPoint CombatModifier { get; set; }
		public Posture GroupPosture { get; set; }
		public UnitGroupStats(string name)
        {
			Name			= name; 
			NumUnits		= FixedPoint.Zero;
			TotalCombat		= FixedPoint.Zero;
			TotalMoves		= FixedPoint.Zero;
			TotalHealth		= FixedPoint.Zero;
			TotalVeterency	= FixedPoint.Zero;
			TotalExperience = FixedPoint.Zero;
			Fortification	= FixedPoint.Zero;
			CombatModifier	= FixedPoint.One;

		}

		public void ComputeStats()
		{
			if(NumUnits > 0)
			{ 
				AverageCombat		= TotalCombat / NumUnits;
				AverageMoves		= TotalMoves / NumUnits;
				AverageHealth		= TotalHealth / NumUnits;
				AverageVeterency	= TotalVeterency / NumUnits;
				AverageExperience	= TotalExperience / NumUnits;

				CombatStrength		= TotalCombat * QJM.GetVeterancyModifier(AverageVeterency);
			}
			else
            {
				AverageCombat = FixedPoint.Zero;
				AverageMoves = FixedPoint.Zero;
				AverageHealth = FixedPoint.Zero;
				AverageVeterency = FixedPoint.Zero;
				AverageExperience = FixedPoint.Zero;
				CombatStrength = FixedPoint.Zero;
			}
		}
		public bool IsAttacker()
        {
			return Name == "Attacker";
        }

		public void AddUnitData(ListOfStruct<UnitSimplifiedData> listOfunits, bool isRetreating = false)
		{
			//Diagnostics.LogWarning($"[Gedemon] UnitGroupStats: AddUnitData from {Name}");

			for (int j = 0; j < listOfunits.Length; j++)
			{
				UnitSimplifiedData unitData = listOfunits.Data[j];

				if (unitData.IsFortification)
				{
					//Diagnostics.Log($"[Gedemon] unit #{j}: Fortification = {unitData.HealthRatio}");
					Fortification += unitData.HealthRatio;
				}
				else
				{
					//Diagnostics.Log($"[Gedemon] unit #{j}: CombatStrength {unitData.CombatStrength}, HealthRatio {unitData.HealthRatio}, move = {(unitData.IsOnWater ? unitData.Unit.NavalSpeed.Value : unitData.Unit.LandSpeed.Value)}, XP = {unitData.ExperienceToReceive + unitData.Unit.Experience}, has RetreatedUnitStatusName = {unitData.Unit.HasStatus(DepartmentOfDefense.RetreatedUnitStatusName)}");
					NumUnits++;
					TotalCombat += unitData.CombatStrength * unitData.HealthRatio;
					TotalHealth += unitData.HealthRatio;
					FixedPoint baseMoves = unitData.IsOnWater ? unitData.Unit.NavalSpeed.Value : unitData.Unit.LandSpeed.Value;
					TotalMoves += isRetreating ? (FixedPoint)0.5* baseMoves : baseMoves; // unitData.Unit.HasStatus(DepartmentOfDefense.RetreatedUnitStatusName)
					TotalVeterency += unitData.Unit.VeterancyLevel.Value * unitData.HealthRatio;
					TotalExperience += unitData.ExperienceToReceive + unitData.Unit.Experience;
				}
			}
		}
	}
	//*
	[HarmonyPatch(typeof(BattleAbilityHelper))]
	public class BattleAbilityHelper_Patch
	{
		private static Damage[] damageInterval = new Damage[21]
		{
			new Damage(1, 4),		// new Damage(5, 25),
			new Damage(2, 6),		// new Damage(7, 25),
			new Damage(4, 10),		// new Damage(9, 25),
			new Damage(6, 12),		// new Damage(11, 25),
			new Damage(8, 16),		// new Damage(12, 28),
			new Damage(10, 20),		// new Damage(15, 30),
			new Damage(12, 22),		// new Damage(19, 32),
			new Damage(14, 24),		// new Damage(22, 33),
			new Damage(16, 26),		// new Damage(25, 35),
			new Damage(18, 28),		// new Damage(28, 39),
			new Damage(20, 30),		// new Damage(30, 42),
			new Damage(22, 32),		// new Damage(34, 46),
			new Damage(24, 34),		// new Damage(37, 51),
			new Damage(26, 36),		// new Damage(40, 55),
			new Damage(28, 38),		// new Damage(43, 58),
			new Damage(30, 40),		// new Damage(46, 62),
			new Damage(32, 42),		// new Damage(49, 66),
			new Damage(34, 40),		// new Damage(53, 72),
			new Damage(36, 42),		// new Damage(65, 85),
			new Damage(38, 46),		// new Damage(80, 100),
			new Damage(40, 50)		// new Damage(100, 100)
		};

		[HarmonyPrefix]
		[HarmonyPatch(nameof(GetDamages))]
		public static bool GetDamages(ref Damage __result, FixedPoint attackerStrength, FixedPoint defenderStrength)
		{

			int num = (int)FixedPoint.Clamp(attackerStrength - defenderStrength, -4, 16);
			__result = damageInterval[num + 4];
			FixedPoint fixedPoint = FixedPoint.Ceiling((attackerStrength - defenderStrength) / 16);
			if (fixedPoint < 0)
			{
				FixedPoint left = __result.MaximumDamage + fixedPoint * 5;
				__result.MaximumDamage = FixedPoint.Max(left, __result.MinimumDamage);
			}

			//Diagnostics.LogError($"[Gedemon][BattleAbilityHelper] GetDamages (before mod) : attackerStrength = {attackerStrength}, defenderStrength = {defenderStrength}, [{__result.MaximumDamage}, {__result.MinimumDamage}]");

			return true;
		}
	}

	[HarmonyPatch(typeof(BattleAutoResolver))]
	public class BattleAutoResolver_Patch
	{
		private const int MaxRound = 4;

		[HarmonyPrefix]
		[HarmonyPatch(nameof(AutoResolve))]
		public static bool AutoResolve(Battle battle)
		{

			if (!BattlePosture.UsePosture) { return true; }

			BattleExtension battleExtension = BattleSaveExtension.GetExtension(battle.GUID);
			Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] AutoResolve Postures (before): attacker = {battleExtension.AttackerPosture}, defender = {battleExtension.DefenderPosture}");

			battle.DefenderGroup.DeploymentZone = new List<WorldPosition> { battle.DefenderGroup.OriginPosition };
			battle.AttackerGroup.DeploymentZone = new List<WorldPosition> { battle.AttackerGroup.OriginPosition };

			AutoResolveBattleContext workingContext = BattleAutoResolver.WorkingContext;
			BattleAutoResolver.WorkingContext.Clear();
			bool flag = battle.Siege != null && battle.Siege.SiegeState == SiegeStates.Sortie;
			BattleAutoResolver.GetAutoResolveResultForBattle(battle, workingContext, flag, isPreview: false);
			BattleGroup battleGroup = (flag ? battle.DefenderGroup : battle.AttackerGroup);
			BattleGroup battleGroup2 = (flag ? battle.AttackerGroup : battle.DefenderGroup);
			BattleAutoResolver.ApplyAutoResolveUnitsData(battle, battleGroup, workingContext.AttackerUnits);
			BattleAutoResolver.ApplyAutoResolveUnitsData(battle, battleGroup2, workingContext.DefenderUnits);
			if (battle.Siege != null)
			{
				BattleAutoResolver.ApplyAutoResolveFortificationData(workingContext.TotalFortificationDamage, workingContext.DistrictWorkList, battle.DefenderGroup.OriginPosition);
				workingContext.DistrictWorkList.Clear();
			}
			_ = MercuryPreferences.VerboseInstantResolveLogs;

			Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] AutoResolve Postures (after): attacker = {battleExtension.AttackerPosture}, defender = {battleExtension.DefenderPosture}, victoryType = {battleExtension.VictoryType}, winner = {workingContext.Winner}");
			Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] AutoResolve Postures (after): winner battleGroup.Role = {battleGroup.Role} empire #{battleGroup.LeaderEmpireIndex}, loser battleGroup2.Role = {battleGroup2.Role} empire #{battleGroup2.LeaderEmpireIndex}, was sortie = {flag}");

			if (workingContext.Winner == Amplitude.Mercury.Interop.BattleGroupRoleType.Attacker)
			{
				battle.ExecuteBattleComplete(battleGroup, battleExtension.VictoryType);
			}
			else if (workingContext.Winner == Amplitude.Mercury.Interop.BattleGroupRoleType.Defender)
			{
				battle.ExecuteBattleComplete(battleGroup2, battleExtension.VictoryType);
			}
			else
			{
				battle.ExecuteBattleComplete();
			}
			SimulationController.RefreshAll();

			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(CompareUnitByStrength))]
		public static bool CompareUnitByStrength(ref UnitSimplifiedData left, ref UnitSimplifiedData right, ref int __result)
		{
			int num = left.IsFortification.CompareTo(right.IsFortification); //-left.IsFortification.CompareTo(right.IsFortification);
			if (num == 0)
			{
				FixedPoint combatStrength = left.CombatStrength * left.HealthRatio;
				num = combatStrength.CompareTo(right.CombatStrength * right.HealthRatio);

				//Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] CompareUnitByStrength compare left #{left.GUID} [{left.CombatStrength}x{left.HealthRatio}] to right #{right.GUID} [{right.CombatStrength}x{right.HealthRatio}] = {num}");

				if (num == 0)
				{
					num = left.GUID.CompareTo(right.GUID);
				}
			}
            else
            {
				//Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] CompareUnitByStrength compare left #{left.GUID} [IsFortification={left.IsFortification}] to right #{right.GUID} [IsFortification={right.IsFortification}]={num}");
			}

			__result = -num;
			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(GetAutoResolveResultForBattle))]
		internal static bool GetAutoResolveResultForBattle(Battle battle, AutoResolveBattleContext context, bool isSortie, bool isPreview)
		{
			// remove or change apply terrain bonus ? remove fortification and do calculation based on health left & value ?
			using (new PerformanceSample("Auto resolve battle", PerformanceSampleCategory.BattleResult))
			{
				// Gedemon <<<<<
				LogListBattleGroupArmies(battle.AttackerGroup);
				LogListBattleGroupArmies(battle.DefenderGroup);
				// Gedemon >>>>>
				bool num = battle.Siege != null;
				BattleGroup obj = (isSortie ? battle.DefenderGroup : battle.AttackerGroup);
				BattleGroup battleGroup = (isSortie ? battle.AttackerGroup : battle.DefenderGroup);
				int fortificationCount = 0;
				BattleAutoResolver.FillUnitsData(obj, ref context.AttackerUnits);
				BattleAutoResolver.FillUnitsData(battleGroup, ref context.DefenderUnits);
				BattleAutoResolver.AutoResolveTerrainData attackerTerrainData = BattleAutoResolver.ComputeTerrainBonus(obj.DeploymentZone);
				BattleAutoResolver.AutoResolveTerrainData defenderTerrainData = BattleAutoResolver.ComputeTerrainBonus(battleGroup.DeploymentZone);
				//BattleAutoResolver.ApplyTerrainDataBonus(context.AttackerUnits, ref attackerTerrainData, context.DefenderUnits, ref defenderTerrainData);
				ApplyTerrainDataBonus(context.AttackerUnits, ref attackerTerrainData, context.DefenderUnits, ref defenderTerrainData);
				if (num)
				{
					ListOfStruct<UnitSimplifiedData> listOfStruct = ((!isSortie) ? context.DefenderUnits : context.AttackerUnits);
					if (listOfStruct != null)
					{
						context.DistrictWorkList.Clear();
						UnitSimplifiedData fortificationUnit = UnitSimplifiedData.Empty;
						BattleAutoResolver.CreateSimplifiedFortificationUnit(battle.Siege.BesiegedCity.Entity, out fortificationUnit, out fortificationCount, context.DistrictWorkList);
						if (fortificationCount > 0)
						{
							listOfStruct.Add(ref fortificationUnit);
						}
					}
				}
				bool isOnWaterOnly = true;
				int count = battle.Arena.Area.Positions.Count;
				for (int i = 0; i < count; i++)
				{
					if (!Amplitude.Mercury.Sandbox.Sandbox.World.IsPositionInWater(battle.Arena.Area.Positions[i]))
					{
						isOnWaterOnly = false;
						break;
					}
				}
				context.Winner = BattleAutoResolver.ComputeAutoResolve(ref context.AttackerUnits, ref context.DefenderUnits, fortificationCount, ref context.TotalFortificationDamage, isOnWaterOnly, context.UnitProperties, isPreview, battle);
			}

			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(ComputeAutoResolve))]
		public static bool ComputeAutoResolve(ref BattleGroupRoleType __result, ref ListOfStruct<UnitSimplifiedData> attackerUnits, ref ListOfStruct<UnitSimplifiedData> defenderUnits, int fortificationCount, ref FixedPoint totalFortificationDamage, bool isOnWaterOnly, UnitPropertiesEvaluation unitProperties, bool isPreview, Battle battle)
		{

			if (!BattlePosture.UsePosture) { return true; }
			if (isPreview) { return true; }

			if (battle == null)
            {
				Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] ComputeAutoResolve with battle = null");
				return true;
			}

			BattleExtension battleExtension = BattleSaveExtension.GetExtension(battle.GUID);

			bool IsAssault = battle.Siege != null && battle.Siege.SiegeState != SiegeStates.Sortie;
			bool IsSortie = battle.Siege != null && battle.Siege.SiegeState == SiegeStates.Sortie;
			bool IsRetreating = battleExtension.DefenderWasRetreating;

			// to get/set correct attacker/defender posture
			battleExtension.SiegeState = battle.Siege != null ? battle.Siege.SiegeState : SiegeStates.None;

			Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : battle GUID = {battle.GUID}, fortificationCount = {fortificationCount}, IsAssault = {IsAssault}, DefenderWasRetreating = {IsRetreating}");

			int attackerCount = attackerUnits.Length;
			int defenderCount = defenderUnits.Length;
			attackerUnits.Sort(BattleAutoResolver.CompareUnitByStrengthCached);
			defenderUnits.Sort(BattleAutoResolver.CompareUnitByStrengthCached);

			UnitGroupStats attackerGroupStats = new UnitGroupStats("Attacker");
			attackerGroupStats.AddUnitData(attackerUnits);

			UnitGroupStats defenderGroupStats = new UnitGroupStats("Defender");
			defenderGroupStats.AddUnitData(defenderUnits, IsRetreating);

			attackerGroupStats.ComputeStats();
			defenderGroupStats.ComputeStats();

			FixedPoint attackerInitialHealth = attackerGroupStats.TotalHealth;
			FixedPoint defenderInitialHealth = defenderGroupStats.TotalHealth;

			#region AI POSTURE DECISION

			// to do : Defending AI should not retreat from cities
			Empire attackerEmpire = Sandbox.Empires[battle.InstigatorLeaderEmpireIndex];
			ResolvePostureForAttackingAI(attackerEmpire, battle, ref battleExtension, attackerUnits, defenderUnits, attackerGroupStats, defenderGroupStats);

			Empire defenderEmpire = Sandbox.Empires[battle.NotInstigatorLeaderEmpireIndex];
			ResolvePostureForDefendingAI(defenderEmpire, battle, ref battleExtension, attackerUnits, defenderUnits, attackerGroupStats, defenderGroupStats);

			attackerGroupStats.GroupPosture = battleExtension.AttackerPosture;
			defenderGroupStats.GroupPosture = battleExtension.DefenderPosture;

			#endregion

			#region COMBAT LOOP

			for (int roundNum = 0; roundNum < MaxRound; roundNum++)
			{
				Posture attackerCurrentPosture = battleExtension.AttackerPosture;
				Posture defenderCurrentPosture = battleExtension.DefenderPosture;

				battleExtension.RoundNum = roundNum;

				Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : Round #{roundNum + 1}, attacker [{attackerCurrentPosture}, count={attackerCount}], defender [{defenderCurrentPosture}, count ={defenderCount}], fortificationCount={fortificationCount}, FortificationDamage={totalFortificationDamage}");

				string phasePresentation = $"Round {roundNum + 1} [Turn]";

				if (attackerCount == 0 && defenderCount == 0)
				{
					phasePresentation += Environment.NewLine + $"Battle ended before combat with the destruction of all units";
					battleExtension.BattleSummary += roundNum == 0 ? phasePresentation : Environment.NewLine + Environment.NewLine + phasePresentation;
					break;
				}
				if (attackerCount == 0)
				{
					phasePresentation += Environment.NewLine + $"Battle ended before combat with the destruction of all attacker units";
					battleExtension.BattleSummary += roundNum == 0 ? phasePresentation : Environment.NewLine + Environment.NewLine + phasePresentation;
					break;
				}
				if (defenderCount == 0)
				{
					phasePresentation += Environment.NewLine + $"Battle ended before combat with the destruction of all defender units";
					battleExtension.BattleSummary += roundNum == 0 ? phasePresentation : Environment.NewLine + Environment.NewLine + phasePresentation;
					break;
				}
				if (BattlePosture.IsDefensive(attackerCurrentPosture) && BattlePosture.IsDefensive(defenderCurrentPosture))
				{
					if(roundNum == 0)
					{
						battleExtension.VictoryType = IsAssault ? BattleVictoryType.Attrition : BattleVictoryType.Retreat;
						battleExtension.BattleSummary += Environment.NewLine + Environment.NewLine + $"Defender wins the battle: Attacker refused to engage combat.";
						__result = BattleGroupRoleType.Defender;
						return false;
					}

					phasePresentation += Environment.NewLine + $"Battle ended before combat with both opponents in defensive posture";
					battleExtension.BattleSummary += roundNum == 0 ? phasePresentation : Environment.NewLine + Environment.NewLine + phasePresentation;
					break;
				}

				if (!isPreview)
				{
					_ = MercuryPreferences.VerboseInstantResolveLogs;
				}


				if (roundNum > 0)
				{
					attackerGroupStats = new UnitGroupStats("Attacker");
					attackerGroupStats.AddUnitData(attackerUnits);
					attackerGroupStats.GroupPosture = battleExtension.AttackerPosture;

					defenderGroupStats = new UnitGroupStats("Defender");
					defenderGroupStats.AddUnitData(defenderUnits, IsRetreating);
					defenderGroupStats.GroupPosture = battleExtension.DefenderPosture;

					attackerGroupStats.ComputeStats();
					defenderGroupStats.ComputeStats();
				}

				FixedPoint attackerStartRoundHealth = attackerGroupStats.TotalHealth;
				FixedPoint defenderStartRoundHealth = defenderGroupStats.TotalHealth;

				phasePresentation += Environment.NewLine + $"Att:[{attackerCurrentPosture}][{attackerCount}[Population]][{attackerGroupStats.AverageMoves}[MovementSpeed]][{attackerGroupStats.AverageCombat}[CombatStrength]][{(int)(attackerGroupStats.AverageHealth * 100)}%[Health]][{attackerGroupStats.AverageVeterency}[Veterancy]]";
				phasePresentation += Environment.NewLine + $"Def:[{defenderCurrentPosture}][{defenderCount}[Population]][{defenderGroupStats.AverageMoves}[MovementSpeed]][{defenderGroupStats.AverageCombat}[CombatStrength]][{(int)(defenderGroupStats.AverageHealth * 100)}%[Health]][{defenderGroupStats.AverageVeterency}[Veterancy]][{fortificationCount}[FortificationShadowed]]";
				battleExtension.BattleSummary += roundNum == 0 ? phasePresentation : Environment.NewLine + Environment.NewLine + phasePresentation;

				// checks for abandonning battlefield before battle start
				if (BattlePosture.IsAbandonCombat(battleExtension.DefenderPosture) && BattlePosture.IsAbandonCombat(battleExtension.AttackerPosture))
				{
					battleExtension.BattleSummary += Environment.NewLine + $"Battle ended before combat with the both armies leaving the battlefield";
					ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
					break;
				}
				if (BattlePosture.IsAbandonCombat(battleExtension.AttackerPosture))
				{
					bool canEscape = false;
					FixedPoint attackerMobilityFactor = attackerGroupStats.AverageMoves * QJM.GetVeterancyModifier(attackerGroupStats.AverageVeterency);
					FixedPoint defenderMobilityFactor = defenderGroupStats.AverageMoves * QJM.GetVeterancyModifier(defenderGroupStats.AverageVeterency);
					switch (battleExtension.DefenderPosture)
					{
						case Posture.Pursuit:
						case Posture.AllOutAttack:
						case Posture.MobileAttack:
							canEscape = attackerMobilityFactor >= defenderMobilityFactor;
							if (canEscape)
								battleExtension.BattleSummary += Environment.NewLine + $"Battle ended before combat with the attacker escaping: {attackerMobilityFactor} vs {defenderMobilityFactor} [MovementSpeed][Veterancy]";
							break;

						default:
							canEscape = true;
							battleExtension.BattleSummary += Environment.NewLine + $"Battle ended before combat with the attacker escaping: not pursued";
							break;
					}
					if (canEscape)
					{
						ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
						break;
					}
				}
				if (BattlePosture.IsAbandonCombat(battleExtension.DefenderPosture))
				{
					bool canEscape = false;
					FixedPoint attackerMobilityFactor = attackerGroupStats.AverageMoves * QJM.GetVeterancyModifier(attackerGroupStats.AverageVeterency);
					FixedPoint defenderMobilityFactor = defenderGroupStats.AverageMoves * QJM.GetVeterancyModifier(defenderGroupStats.AverageVeterency);
					switch (battleExtension.AttackerPosture)
					{
						case Posture.Pursuit:
						case Posture.AllOutAttack:
						case Posture.MobileAttack:
							canEscape = defenderMobilityFactor >= attackerMobilityFactor;
							if (canEscape)
								battleExtension.BattleSummary += Environment.NewLine + $"Battle ended before combat with the defender escaping: {defenderMobilityFactor} vs {attackerMobilityFactor} [MovementSpeed][Veterancy]";
							break;

						default:
							battleExtension.BattleSummary += Environment.NewLine + $"Battle ended before combat with the defender escaping: not pursued";
							canEscape = true;
							break;
					}
					if (canEscape)
					{
						ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
						break;
					}
				}

				// COMBAT START !

				BattlePosture.ResolvePosture(ref attackerGroupStats, ref defenderGroupStats, ref battleExtension);
				BattlePosture.ResolvePosture(ref defenderGroupStats, ref attackerGroupStats, ref battleExtension);

				//
				// (1+(1-x))*ratio

				// attacker combats
				//Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] Sorting <attacker units>");
				attackerUnits.Sort(BattleAutoResolver.CompareUnitByStrengthCached);
				for (int i = 0; i < attackerUnits.Length; i++)
				{
					UnitSimplifiedData unit = attackerUnits.Data[i];
					//Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] Unit #{unit.GUID} [{unit.CombatStrength}x{unit.HealthRatio}] = {unit.CombatStrength * unit.HealthRatio}");
				}
				//Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] Sorting <defender units>");
				defenderUnits.Sort(BattleAutoResolver.CompareUnitByStrengthCached);
				for (int i = 0; i < defenderUnits.Length; i++)
				{
					UnitSimplifiedData unit = defenderUnits.Data[i];
					//Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] Unit #{unit.GUID} [{unit.CombatStrength}x{unit.HealthRatio}] = {unit.CombatStrength * unit.HealthRatio}");
				}

				Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] Calling ResolveDamage: <attacker> = ATTACKER SIDE");

				bool hadCombats = ResolveDamage(attackerUnits, attackerGroupStats.CombatModifier, defenderUnits, defenderGroupStats.CombatModifier, ref attackerCount, ref defenderCount, ref fortificationCount, ref totalFortificationDamage, isPreview, battle);
				if (attackerCount == 0 || defenderCount == 0)
				{
					if (attackerCount == 0 && defenderCount == 0)
					{
						battleExtension.BattleSummary += Environment.NewLine + $"Battle ended with the destruction of all units";
						ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
						break;
					}
					if (attackerCount == 0)
					{
						battleExtension.BattleSummary += Environment.NewLine + $"Battle ended with the destruction of all attacker units";
						ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
						break;
					}
					if (defenderCount == 0)
					{
						battleExtension.BattleSummary += Environment.NewLine + $"Battle ended with the destruction of all defender units";
						ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
						break;
					}
					break;
				}
				if (!isPreview)
				{
					_ = MercuryPreferences.VerboseInstantResolveLogs;
				}

				// defender combats
				Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] Sorting <attacker units>");
				attackerUnits.Sort(BattleAutoResolver.CompareUnitByStrengthCached);
				for (int i = 0; i < attackerUnits.Length; i++)
				{
					ref UnitSimplifiedData unit = ref attackerUnits.Data[i];
					Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] Unit #{unit.GUID} [{unit.CombatStrength}x{unit.HealthRatio}] = {unit.CombatStrength* unit.HealthRatio}");
				}
				Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] Sorting <defender units>");
				defenderUnits.Sort(BattleAutoResolver.CompareUnitByStrengthCached);
				for (int i = 0; i < defenderUnits.Length; i++)
				{
					ref UnitSimplifiedData unit = ref defenderUnits.Data[i];
					Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] Unit #{unit.GUID} [{unit.CombatStrength}x{unit.HealthRatio}] = {unit.CombatStrength * unit.HealthRatio}");
				}

				Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] Calling ResolveDamage: <attacker> = DEFENDER SIDE");
				if (!(hadCombats | ResolveDamage(defenderUnits, defenderGroupStats.CombatModifier, attackerUnits, attackerGroupStats.CombatModifier, ref defenderCount, ref attackerCount, ref fortificationCount, ref totalFortificationDamage, isPreview, battle)))
				{
					break;
				}

				// COMBAT RESULT

				// check again for eliminated side
				if (attackerCount == 0 && defenderCount == 0)
				{
					battleExtension.BattleSummary += Environment.NewLine + $"Battle ended with the destruction of all units";
					ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
					break;
				}
				if (attackerCount == 0)
				{
					battleExtension.BattleSummary += Environment.NewLine + $"Battle ended with the destruction of all attacker units";
					ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
					break;
				}
				if (defenderCount == 0)
				{
					battleExtension.BattleSummary += Environment.NewLine + $"Battle ended with the destruction of all defender units";
					ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
					break;
				}

				// checks again for abandonning battlefield
				if (BattlePosture.IsAbandonCombat(battleExtension.DefenderPosture) && BattlePosture.IsAbandonCombat(battleExtension.AttackerPosture))
				{
					battleExtension.BattleSummary += Environment.NewLine + $"Battle ended with the both armies leaving the battlefield";
					ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
					break;
				}

				if (BattlePosture.IsAbandonCombat(battleExtension.AttackerPosture))
				{
					battleExtension.BattleSummary += Environment.NewLine + $"Battle ended with the attacker leaving the battlefield";
					ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
					break;
				}

				if (BattlePosture.IsAbandonCombat(battleExtension.DefenderPosture))
				{
					battleExtension.BattleSummary += Environment.NewLine + $"Battle ended with the defender leaving the battlefield";
					ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
					break;
				}

				if (BattlePosture.IsDefensive(attackerCurrentPosture) && BattlePosture.IsDefensive(defenderCurrentPosture))
				{
					battleExtension.BattleSummary += Environment.NewLine + $"Battle ended with both opponents in defensive posture";
					ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits, checkPosture: false);
					break;
				}

				ResolveEndRound(ref battleExtension, attackerInitialHealth, attackerStartRoundHealth, attackerUnits, defenderInitialHealth, defenderStartRoundHealth, defenderUnits);

				if (roundNum == 0 && !isOnWaterOnly && unitProperties != null)
				{
					BattleAutoResolver.UseLandCombatStrength(attackerUnits, unitProperties);
					BattleAutoResolver.UseLandCombatStrength(defenderUnits, unitProperties);
				}
			}

			#endregion

			#region BATTLE RESULTS

			attackerGroupStats = new UnitGroupStats("Attacker");
			defenderGroupStats = new UnitGroupStats("Defender");

			attackerGroupStats.AddUnitData(attackerUnits);
			defenderGroupStats.AddUnitData(defenderUnits, IsRetreating);

			attackerGroupStats.ComputeStats();
			defenderGroupStats.ComputeStats();


			FixedPoint attackerLossRatio = FixedPoint.One - (attackerGroupStats.TotalHealth / attackerInitialHealth);
			FixedPoint defenderLossRatio = FixedPoint.One - (defenderGroupStats.TotalHealth / defenderInitialHealth);

			if (attackerCount > 0 && defenderCount > 0)
			{
				// whatever the numbers, the defender leaving its position is a retreat and a win for the attacker 
				if (battleExtension.DefenderPosture == Posture.Withdrawal || battleExtension.DefenderPosture == Posture.Retreat || battleExtension.DefenderPosture == Posture.Routed)
				{
					battleExtension.VictoryType = BattleVictoryType.Retreat;
					__result = BattleGroupRoleType.Attacker;
					battleExtension.BattleSummary += Environment.NewLine + Environment.NewLine + $"Attacker wins the battle: Defender abandonning its position";
					return false;
				}
				// whatever the numbers, the attacker retreating or being routed is a win for the defender 
				if (battleExtension.AttackerPosture == Posture.Retreat || battleExtension.AttackerPosture == Posture.Routed)
				{
					battleExtension.VictoryType = IsAssault ? BattleVictoryType.Attrition : BattleVictoryType.Retreat;
					__result = BattleGroupRoleType.Defender;
					battleExtension.BattleSummary += Environment.NewLine + Environment.NewLine + $"Defender wins the battle: Attacker retreating or routed";
					return false;
				}
			}

			if (attackerCount > 0 && (attackerLossRatio < defenderLossRatio * QJM.MediumReducingModifier || defenderCount == 0))
			{
				if (defenderCount > 0)
				{
					if (battleExtension.DefenderPosture == Posture.Withdrawal || battleExtension.DefenderPosture == Posture.Retreat || battleExtension.DefenderPosture == Posture.Routed)
					{
						battleExtension.VictoryType = BattleVictoryType.Retreat;
						battleExtension.BattleSummary += Environment.NewLine + Environment.NewLine + $"Attacker wins the battle: Defender abandonning its position";

					}
					else
					{
						if(IsSortie)
						{
							battleExtension.VictoryType = BattleVictoryType.Retreat;
							battleExtension.BattleSummary += Environment.NewLine + Environment.NewLine + $"Attacker breaks the siege: -{(int)(attackerLossRatio * 100)}% vs -{(int)(defenderLossRatio * 100)}% [Health]";
						}
                        else
						{
							battleExtension.VictoryType = BattleVictoryType.Attrition;
							battleExtension.BattleSummary += Environment.NewLine + Environment.NewLine + $"Attacker wins the battle by Attrition: -{(int)(attackerLossRatio * 100)}% vs -{(int)(defenderLossRatio * 100)}% [Health]";
						}
					}
				}
				else
				{
					battleExtension.VictoryType = BattleVictoryType.Extermination;
					battleExtension.BattleSummary += Environment.NewLine + Environment.NewLine + $"Attacker wins the battle by annihilation of the opponent.";
				}

				__result = BattleGroupRoleType.Attacker;
				return false;
			}
			if (defenderCount > 0 && (defenderLossRatio < attackerLossRatio * QJM.MediumReducingModifier || attackerCount == 0))
			{
				if (attackerCount > 0)
				{
					if (battleExtension.AttackerPosture == Posture.Retreat || battleExtension.AttackerPosture == Posture.Routed)
					{
						battleExtension.VictoryType = IsAssault ? BattleVictoryType.Attrition : BattleVictoryType.Retreat; // Main battle function doesn't handle correctly attacker retreat on assault, so use attrition (which will also cause the attacked to retreat)
						battleExtension.BattleSummary += Environment.NewLine + Environment.NewLine + $"Defender wins the battle: Attacker retreating or routed";
					}
					else
					{
						battleExtension.VictoryType = BattleVictoryType.Attrition;
						battleExtension.BattleSummary += Environment.NewLine + Environment.NewLine + $"Defender wins the battle by Attrition: -{(int)(defenderLossRatio * 100)}% vs -{(int)(attackerLossRatio * 100)}% [Health]";
					}
				}
				else
				{
					battleExtension.VictoryType = BattleVictoryType.Extermination;
					battleExtension.BattleSummary += Environment.NewLine + Environment.NewLine + $"Defender wins the battle by annihilation of the opponent.";
				}
				__result = BattleGroupRoleType.Defender;
				return false;
			}

			battleExtension.BattleSummary += Environment.NewLine + $"No victory, Battle Total Casualties: Attacker = -{(int)(attackerLossRatio * 100)}% [Health] | Defender = -{(int)(defenderLossRatio * 100)}% [Health]";
			__result = BattleGroupRoleType.None;
			return false;
			#endregion

		}

		public static bool ResolveDamage(ListOfStruct<UnitSimplifiedData> attackerUnits, FixedPoint attackerModifier, ListOfStruct<UnitSimplifiedData> defenderUnits, FixedPoint defenderModifier, ref int attackerCount, ref int defenderCount, ref int fortificationCount, ref FixedPoint totalFortificationDamage, bool isPreview, Battle battle)
		{

			if (!BattlePosture.UsePosture)
			{
				return BattleAutoResolver.AutoResolveDamage(attackerUnits, defenderUnits, ref attackerCount, ref defenderCount, ref fortificationCount, ref totalFortificationDamage, isPreview, battle);
			}

			BattleExtension battleExtension = BattleSaveExtension.GetExtension(battle.GUID);
			Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] ResolveDamage: attackerModifier = {attackerModifier}, defenderModifier = {defenderModifier} ");

			FixedPoint damageDivider = 800; // was 200
			bool hadCombats = false;
			int maxAttacks = defenderUnits.Length * 2;
			int numAttacks = 0;
			for (int i = 0; i < attackerUnits.Length; i++)
			{
				if(numAttacks >= maxAttacks)
                {
					break;
                }
				ref UnitSimplifiedData attacker = ref attackerUnits.Data[i];
				if (attacker.HealthRatio <= FixedPoint.Zero || (!attacker.CanAttackUnits && !attacker.CanAttackFortifications))
				{
					continue;
				}

				if (defenderCount == 1 && defenderUnits.Data[0].IsFortification)
				{
					break;
				}

				bool combatsEnded = false;

				//Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] ResolveDamage: Sorting <defender units>");
				defenderUnits.Sort(BattleAutoResolver.CompareUnitByStrengthCached);
				for (int u = 0; u < defenderUnits.Length; u++)
				{
					UnitSimplifiedData unit = defenderUnits.Data[u];
					//Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] Unit #{unit.GUID} [{unit.CombatStrength}x{unit.HealthRatio}] = {unit.CombatStrength * unit.HealthRatio}");
				}

				for (int j = 0; j < defenderUnits.Length; j++)
				{
					ref UnitSimplifiedData defender = ref defenderUnits.Data[j];
					if (defender.HealthRatio <= FixedPoint.Zero || ((!attacker.CanAttackUnits || defender.IsFortification) && (!attacker.CanAttackFortifications || !defender.IsFortification)))
					{
						Diagnostics.LogWarning($"[Gedemon][BattleAutoResolver] ignoring Defender#{defender.GUID} [j={j}]: (defender.HealthRatio[{defender.HealthRatio}] <= FixedPoint.Zero || ((!attacker.CanAttackUnits[{attacker.CanAttackUnits}] || defender.IsFortification[{defender.IsFortification}]) && (!attacker.CanAttackFortifications[{attacker.CanAttackFortifications}] || !defender.IsFortification[{defender.IsFortification}])))");
						continue;
					}

					hadCombats = true;

					FixedPoint attackerStrength = attacker.CombatStrength * attacker.HealthRatio * attackerModifier;
					FixedPoint defenderStrength = defender.CombatStrength * defender.HealthRatio * defenderModifier;


					Diagnostics.Log($"[Gedemon][BattleAutoResolver] ResolveDamage (str*healthRatio*mod) attacker[{attacker.GUID}] vs defender[{defender.GUID}] : attStr = {attacker.CombatStrength}*{attacker.HealthRatio}*{attackerModifier} = {attackerStrength} , defStr = {defender.CombatStrength}*{defender.HealthRatio}*{defenderModifier} = {defenderStrength}, defender.IsFortification = {defender.IsFortification}");

					Damage damages = BattleAbilityHelper.GetDamages(attackerStrength, defenderStrength);
					FixedPoint damageToDefender = (damages.MaximumDamage + damages.MinimumDamage) / damageDivider;
					damageToDefender = FixedPoint.Min(damageToDefender, defender.HealthRatio);
					attacker.ExperienceToReceive += damageToDefender * defender.CombatStrength;
					if (defender.IsFortification)
					{
						totalFortificationDamage += damageToDefender;
					}
                    else
                    {
						numAttacks++;
					}

					Diagnostics.Log($"[Gedemon][BattleAutoResolver] ResolveDamage: defender.HealthRatio = {defender.HealthRatio}, damage = {damageToDefender} ");
					defender.HealthRatio -= damageToDefender;
					if (!isPreview)
					{
						_ = MercuryPreferences.VerboseInstantResolveLogs;
					}

					if (defender.CanRetaliate && !attacker.IsRanged)
					{
						damages = BattleAbilityHelper.GetDamages(defender.CombatStrength, attacker.CombatStrength);
						FixedPoint damageToattacker = (damages.MaximumDamage + damages.MinimumDamage) / damageDivider;
						damageToattacker = FixedPoint.Min(damageToattacker, attacker.HealthRatio);
						defender.ExperienceToReceive += damageToattacker * attacker.CombatStrength;
						Diagnostics.Log($"[Gedemon][BattleAutoResolver] ResolveDamage: attacker.HealthRatio = {attacker.HealthRatio}, damage = {damageToattacker} ");
						attacker.HealthRatio -= damageToattacker;
						if (!isPreview)
						{
							_ = MercuryPreferences.VerboseInstantResolveLogs;
						}
					}

					if (attacker.HealthRatio <= FixedPoint.Zero)
					{
						attackerCount--;
						if (!isPreview)
						{
							if (defender.SpoilsActions != null)
							{
								BattleAutoResolver.AccumulateSpoilsOfWar(ref defender, ref attacker, battle);
							}

							if (battle != null && attacker.Unit != null && defender.Unit != null)
							{
								defender.Unit.UnitsKilled.Value++;
								SimulationEvent_UnitKilled.Raise(battle, attacker.Unit, defender.Unit);
								if (attacker.Unit.IsAnimal())
								{
									SimulationEvent_AnimalKilled.Raise(battle, attacker.Unit, defender.Unit);
								}
								else
								{
									SimulationEvent_MilitaryUnitKilled.Raise(battle, attacker.Unit, defender.Unit);
								}
							}

							_ = MercuryPreferences.VerboseInstantResolveLogs;
						}
					}

					if (defender.HealthRatio <= FixedPoint.Zero)
					{
						bool flag2 = true;
						if (!isPreview && attacker.SpoilsActions != null)
						{
							BattleAutoResolver.AccumulateSpoilsOfWar(ref attacker, ref defender, battle);
						}

						if (defender.IsFortification && fortificationCount > 1)
						{
							fortificationCount /= 2;
							defender.HealthRatio = FixedPoint.One;
							flag2 = false;
						}

						if (flag2)
						{
							defenderCount--;
							if (!isPreview)
							{
								if (battle != null && attacker.Unit != null && defender.Unit != null)
								{
									attacker.Unit.UnitsKilled.Value++;
									SimulationEvent_UnitKilled.Raise(battle, defender.Unit, attacker.Unit);
									if (defender.Unit.IsAnimal())
									{
										SimulationEvent_AnimalKilled.Raise(battle, defender.Unit, attacker.Unit);
									}
									else
									{
										SimulationEvent_MilitaryUnitKilled.Raise(battle, defender.Unit, attacker.Unit);
									}
								}

								_ = MercuryPreferences.VerboseInstantResolveLogs;
							}

							if (defenderCount == 1 && defenderUnits.Data[0].IsFortification)
							{
								combatsEnded = true;
								break;
							}
						}
					}

					if (attackerCount == 0 || defenderCount == 0)
					{
						combatsEnded = true;
					}

					break;
				}

				if (combatsEnded)
				{
					break;
				}
			}

			if (defenderCount == 1 && defenderUnits.Data[0].IsFortification)
			{
				defenderCount = 0;
			}

			return hadCombats;
		}
		public static bool HasAnimal(ListOfStruct<UnitSimplifiedData> listUnits)
		{
			for (int j = 0; j < listUnits.Length; j++)
			{
				UnitSimplifiedData unitData = listUnits.Data[j];

				if (unitData.IsFortification)
					continue;

				if (unitData.Unit.IsAnimal())
					return true;
			}
			return false;
        }
		public static Posture ResolvePostureChange(ref BattleExtension battleExtension, FixedPoint currentGroupInitialHealth, UnitGroupStats currentGroupStats, FixedPoint opponentGroupInitialHealth, UnitGroupStats opponentGroupStats)
		{
			FixedPoint factorForRoundNumDelay = (FixedPoint)1.75;
			Posture currentPosture = currentGroupStats.IsAttacker() ? battleExtension.AttackerPosture : battleExtension.DefenderPosture;
			Posture opponentPosture = currentGroupStats.IsAttacker() ? battleExtension.DefenderPosture : battleExtension.AttackerPosture;
			if(currentPosture == Posture.Delay)
			{

				FixedPoint opponentGroupMobilityFactor = opponentGroupStats.AverageMoves * QJM.GetVeterancyModifier(opponentGroupStats.AverageVeterency);
				FixedPoint currentGroupMobilityFactor = currentGroupStats.AverageMoves * QJM.GetVeterancyModifier(currentGroupStats.AverageVeterency);

				if (opponentGroupMobilityFactor - currentGroupMobilityFactor <= battleExtension.RoundNum * factorForRoundNumDelay)
				{
					battleExtension.BattleSummary += Environment.NewLine + $"{currentGroupStats.Name} posture [MovementSpeed] {Posture.Withdrawal}: has delayed long enough";
					return Posture.Withdrawal;
				}
				else
				{
					FixedPoint turnsLeft = ((opponentGroupMobilityFactor - currentGroupMobilityFactor) / factorForRoundNumDelay) - battleExtension.RoundNum;
					battleExtension.BattleSummary += Environment.NewLine + $"{currentGroupStats.Name} {turnsLeft.Format()}[Turn] before withdraw: {currentGroupMobilityFactor} vs {opponentGroupMobilityFactor} [MovementSpeed][Veterancy]";
					return currentPosture;
				}
			}			

			if (BattlePosture.IsOffensive(currentPosture))
			{
				FixedPoint currentGroupLossRatio = FixedPoint.One - (currentGroupStats.TotalHealth / currentGroupInitialHealth);
				if (BattlePosture.IsOffensive(opponentPosture)) // losing a phase with both attack posture push the loser in defensive posture
				{
					FixedPoint opponentGroupLossRatio = FixedPoint.One - (opponentGroupStats.TotalHealth / opponentGroupInitialHealth);
					if(currentGroupLossRatio > opponentGroupLossRatio * QJM.LowReducingModifier)
					{
						battleExtension.BattleSummary += Environment.NewLine + $"{currentGroupStats.Name} posture [MovementSpeed] {Posture.HastyDefense}: total casualties: -{(int)(currentGroupLossRatio * 100)}% vs -{(int)(opponentGroupLossRatio * 100)}% [Health]";
						return Posture.HastyDefense;
					}
                }

				// very high casualties
				if (currentGroupStats.TotalHealth < currentGroupInitialHealth * QJM.VeryHighReducingModifier)
				{
					battleExtension.BattleSummary += Environment.NewLine + $"{currentGroupStats.Name} posture [MovementSpeed] {Posture.Withdrawal}: heavy total casualties: -{(int)(currentGroupLossRatio * 100)}%[Health]";
					return Posture.Withdrawal;
				}
				// high casualties and lower strength
				if (currentGroupStats.TotalHealth < currentGroupInitialHealth * QJM.HighReducingModifier && currentGroupStats.CombatStrength < opponentGroupStats.CombatStrength * QJM.LowReducingModifier)
				{
					battleExtension.BattleSummary += Environment.NewLine + $"{currentGroupStats.Name} posture [MovementSpeed] {Posture.Withdrawal}: total casualties: -{(int)(currentGroupLossRatio * 100)}%[Health] | {currentGroupStats.AverageCombat}vs{opponentGroupStats.AverageCombat} [CombatStrength]";
					return Posture.Withdrawal;
				}
			}
			else if (BattlePosture.IsDefensive(currentPosture) && !BattlePosture.IsAbandonCombat(currentPosture))
			{
				FixedPoint attackerLossRatio = FixedPoint.One - (currentGroupStats.TotalHealth / currentGroupInitialHealth);

				if (currentGroupStats.TotalHealth < currentGroupInitialHealth * QJM.VeryHighReducingModifier && currentGroupStats.CombatStrength < opponentGroupStats.CombatStrength * QJM.HighReducingModifier)
				{
					battleExtension.BattleSummary += Environment.NewLine + $"{currentGroupStats.Name} posture [MovementSpeed] {Posture.Withdrawal}: total casualties: -{(int)(attackerLossRatio * 100)}% [Health] | {currentGroupStats.AverageCombat}vs{opponentGroupStats.AverageCombat} [CombatStrength])";
					return Posture.Withdrawal;
				}
			}

			return currentPosture;
		}
		public static void ResolveEndRound(ref BattleExtension battleExtension, FixedPoint attackerInitialHealth, FixedPoint attackerStartRoundHealth, ListOfStruct<UnitSimplifiedData> attackerUnits, FixedPoint defenderInitialHealth, FixedPoint defenderStartRoundHealth, ListOfStruct<UnitSimplifiedData> defenderUnits, bool checkPosture = true)
		{
			UnitGroupStats attackerGroupStats = new UnitGroupStats("Attacker");
			UnitGroupStats defenderGroupStats = new UnitGroupStats("Defender");

			attackerGroupStats.AddUnitData(attackerUnits);
			defenderGroupStats.AddUnitData(defenderUnits, battleExtension.DefenderWasRetreating);

			attackerGroupStats.ComputeStats();
			defenderGroupStats.ComputeStats();

			FixedPoint attackerLossRatio = attackerStartRoundHealth > 0 ? FixedPoint.One - (attackerGroupStats.TotalHealth / attackerStartRoundHealth) : FixedPoint.One;
			FixedPoint defenderLossRatio = defenderStartRoundHealth > 0 ? FixedPoint.One - (defenderGroupStats.TotalHealth / defenderStartRoundHealth) : FixedPoint.One;

			battleExtension.BattleSummary += Environment.NewLine + $"Round Casualties: Attacker = -{(int)(attackerLossRatio * 100)}% [Health] | Defender = -{(int)(defenderLossRatio * 100)}% [Health]";

			if (checkPosture)
			{
				battleExtension.AttackerPosture = ResolvePostureChange(ref battleExtension, attackerInitialHealth, attackerGroupStats, defenderInitialHealth, defenderGroupStats);
				battleExtension.DefenderPosture = ResolvePostureChange(ref battleExtension, defenderInitialHealth, defenderGroupStats, attackerInitialHealth, attackerGroupStats);
			}

		}
		public static void ResolvePostureForAttackingAI(Empire attackerEmpire, Battle battle, ref BattleExtension battleExtension, ListOfStruct<UnitSimplifiedData> attackerUnits, ListOfStruct<UnitSimplifiedData> defenderUnits, UnitGroupStats attackerGroupStats, UnitGroupStats defenderGroupStats)
        {
			if (!attackerEmpire.IsControlledByHuman)
			{
				FixedPoint postureRNG100 = RandomHelper.Next((int)(ulong)battle.GUID + (int)(ulong)attackerEmpire.GUID + SandboxManager.Sandbox.Turn, 0, 100);
				FixedPoint ratioRNG = postureRNG100 / 100;
				Diagnostics.LogError($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : attackerEmpire with {battleExtension.AttackerPosture} called RandomHelper.Next [0-100]: RNG = {postureRNG100}, ratioRNG = {ratioRNG}");
				if (HasAnimal(attackerUnits))
				{
					battleExtension.AttackerPosture = postureRNG100 > 50 ? Posture.Pursuit : postureRNG100 > 25 ? Posture.AllOutAttack : Posture.DeliberateAttack;
					Diagnostics.LogError($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : attackerAnimal AI chosed {battleExtension.AttackerPosture}");
					goto DecisionTaken;
				}
				else
				{
					if (HasAnimal(defenderUnits))
					{
						battleExtension.AttackerPosture = postureRNG100 > 25 ? Posture.Pursuit : Posture.AllOutAttack;
						Diagnostics.LogError($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : attackerEmpire AI chosed {battleExtension.AttackerPosture} against animals");
						goto DecisionTaken;
					}

					// logical decisions
					FixedPoint logicalDecisionValue = (attackerGroupStats.AverageVeterency * 10) + 50; // [50-80]
					if (logicalDecisionValue >= postureRNG100)
					{
						ratioRNG = (postureRNG100 - logicalDecisionValue) / 100;
						// confirm attack if stronger
						if (attackerGroupStats.CombatStrength > defenderGroupStats.CombatStrength)
						{
							// very specific decision
							FixedPoint veryLogicalDecisionValue = (attackerGroupStats.AverageVeterency * 10) + 10; // [10-40]
							if (veryLogicalDecisionValue >= postureRNG100)
							{
								FixedPoint MoveModifier = QJM.GetMoveModifier(attackerGroupStats.AverageMoves, defenderGroupStats.AverageMoves);
								if (attackerGroupStats.AverageVeterency > QJM.VeterancyForMobileAttack && attackerGroupStats.AverageVeterency * MoveModifier > defenderGroupStats.AverageVeterency)
								{
									battleExtension.AttackerPosture = Posture.MobileAttack;
									Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : AttackerEmpire AI chosed {battleExtension.AttackerPosture}, veryLogicalDecisionValue = {veryLogicalDecisionValue}, MoveModifier = {MoveModifier}, Veterency = {attackerGroupStats.AverageVeterency} vs {defenderGroupStats.AverageVeterency}");
									goto DecisionTaken;
								}
							}
							// other decisions
							if (attackerGroupStats.CombatStrength * QJM.VeryHighReducingModifier > defenderGroupStats.CombatStrength)
							{
								battleExtension.AttackerPosture = ratioRNG > (FixedPoint)0.5 ? Posture.Pursuit : Posture.AllOutAttack;
								Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : AttackerEmpire AI chosed {battleExtension.AttackerPosture}, logicalDecisionValue = {logicalDecisionValue}, ratioRNG = {ratioRNG}, CombatStrength = {attackerGroupStats.CombatStrength} vs {defenderGroupStats.CombatStrength}");
								goto DecisionTaken;
							}
							battleExtension.AttackerPosture = Posture.DeliberateAttack;
							Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : AttackerEmpire AI chosed {battleExtension.AttackerPosture}, logicalDecisionValue = {logicalDecisionValue}, CombatStrength = {attackerGroupStats.CombatStrength} vs {defenderGroupStats.CombatStrength}");
							goto DecisionTaken;
						}
						// Other posture
						else
						{
							// very specific decisions
							FixedPoint veryLogicalDecisionValue = (attackerGroupStats.AverageVeterency * 10) + 10; // [10-40]
							if (veryLogicalDecisionValue >= postureRNG100)
							{
								FixedPoint MoveModifier = QJM.GetMoveModifier(attackerGroupStats.AverageMoves, defenderGroupStats.AverageMoves);
								if (attackerGroupStats.AverageVeterency > QJM.VeterancyForMobileAttack && attackerGroupStats.AverageVeterency * MoveModifier * QJM.MediumReducingModifier > defenderGroupStats.AverageVeterency) // case where a weaker force can inflict more damage to a stronger attacking oponent
								{
									battleExtension.AttackerPosture = Posture.MobileAttack;
									Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : AttackerEmpire AI chosed {battleExtension.AttackerPosture}, veryLogicalDecisionValue = {veryLogicalDecisionValue}, MoveModifier = {MoveModifier}, Veterency = {attackerGroupStats.AverageVeterency} vs {defenderGroupStats.AverageVeterency}");
									goto DecisionTaken;
								}

								if (attackerGroupStats.CombatModifier < defenderGroupStats.CombatModifier * QJM.MediumReducingModifier && attackerGroupStats.AverageMoves < defenderGroupStats.AverageMoves) // delaying for more than one turn
								{
									battleExtension.AttackerPosture = Posture.Delay;
									Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : AttackerEmpire AI chosed {battleExtension.AttackerPosture}, veryLogicalDecisionValue = {veryLogicalDecisionValue}, CombatStrength = {attackerGroupStats.CombatStrength} vs {defenderGroupStats.CombatStrength}, Moves = {attackerGroupStats.AverageMoves} vs {defenderGroupStats.AverageMoves}");
									goto DecisionTaken;
								}
							}

							// very specific decision
							if (veryLogicalDecisionValue >= postureRNG100)
							{
								if (attackerGroupStats.CombatModifier < defenderGroupStats.CombatModifier * QJM.HighReducingModifier && attackerGroupStats.AverageMoves > defenderGroupStats.AverageMoves)
								{
									battleExtension.AttackerPosture = Posture.Delay;
									Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : AttackerEmpire AI chosed {battleExtension.AttackerPosture}, veryLogicalDecisionValue = {veryLogicalDecisionValue}, CombatStrength = {attackerGroupStats.CombatStrength} vs {defenderGroupStats.CombatStrength}, Moves = {attackerGroupStats.AverageMoves} vs {defenderGroupStats.AverageMoves}");
									goto DecisionTaken;
								}
							}

							// other decisions
							if (attackerGroupStats.CombatStrength < defenderGroupStats.CombatStrength * QJM.VeryHighReducingModifier)
							{
								battleExtension.AttackerPosture = ratioRNG > (FixedPoint)0.5 ? Posture.Delay : Posture.Retreat;
								Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : AttackerEmpire AI chosed {battleExtension.AttackerPosture}, logicalDecisionValue = {logicalDecisionValue}, ratioRNG = {ratioRNG}, CombatStrength = {attackerGroupStats.CombatStrength} vs {defenderGroupStats.CombatStrength}");
								goto DecisionTaken;
							}


							if (attackerGroupStats.CombatStrength < defenderGroupStats.CombatStrength * QJM.MediumReducingModifier)
							{
								battleExtension.AttackerPosture = ratioRNG > (FixedPoint)0.5 ? Posture.PreparedDefense : Posture.HastyDefense;
								Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : AttackerEmpire AI chosed {battleExtension.AttackerPosture}, logicalDecisionValue = {logicalDecisionValue}, ratioRNG = {ratioRNG}, CombatStrength = {attackerGroupStats.CombatStrength} vs {defenderGroupStats.CombatStrength}");
								goto DecisionTaken;
							}
						}
					}
					// unexpected/risky decisions
					else
					{

					}
					Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : AttackerEmpire AI kept {battleExtension.AttackerPosture}, logicalDecisionValue = {logicalDecisionValue}");


				}

			DecisionTaken:;
			}
		}
		public static void ResolvePostureForDefendingAI(Empire defenderEmpire, Battle battle, ref BattleExtension battleExtension, ListOfStruct<UnitSimplifiedData> attackerUnits, ListOfStruct<UnitSimplifiedData> defenderUnits, UnitGroupStats attackerGroupStats, UnitGroupStats defenderGroupStats)
		{
			if (!defenderEmpire.IsControlledByHuman)
			{
				bool IsAssault = battle.Siege != null && battle.Siege.SiegeState != SiegeStates.Sortie;

				FixedPoint postureRNG100 = RandomHelper.Next((int)(ulong)battle.GUID + (int)(ulong)defenderEmpire.GUID + SandboxManager.Sandbox.Turn, 0, 100);
				FixedPoint ratioRNG = postureRNG100 / 100;
				Diagnostics.LogError($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : defenderEmpire with {battleExtension.DefenderPosture} called RandomHelper.Next [0-100]: RNG = {postureRNG100}, ratioRNG = {ratioRNG}");
				if (HasAnimal(defenderUnits))
				{
					battleExtension.DefenderPosture = postureRNG100 > 50 ? Posture.Retreat : Posture.Pursuit;
					Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : defenderAnimal AI chosed {battleExtension.DefenderPosture}");
					goto DecisionTaken;
				}
				else
				{
					// logical decisions
					FixedPoint logicalDecisionValue = (defenderGroupStats.AverageVeterency * 10) + 50; // [50-80]
					if (logicalDecisionValue >= postureRNG100)
					{
						ratioRNG = (postureRNG100 - logicalDecisionValue) / 100;

						// very specific decision
						FixedPoint veryLogicalDecisionValue = (defenderGroupStats.AverageVeterency * 10) + 10; // [10-40]
						if (veryLogicalDecisionValue >= postureRNG100)
						{
							FixedPoint MoveModifier = QJM.GetMoveModifier(defenderGroupStats.AverageMoves, attackerGroupStats.AverageMoves);
							if (defenderGroupStats.AverageVeterency > QJM.VeterancyForMobileAttack && defenderGroupStats.AverageVeterency * MoveModifier > attackerGroupStats.AverageVeterency)
							{
								battleExtension.DefenderPosture = Posture.MobileAttack;
								Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : defenderEmpire AI chosed {battleExtension.DefenderPosture} (veryLogicalDecisionValue = {veryLogicalDecisionValue}, MoveModifier = {MoveModifier}, Veterency = {defenderGroupStats.AverageVeterency} vs {attackerGroupStats.AverageVeterency}");
								goto DecisionTaken;
							}
						}

						// counter-attack if much stronger
						if (defenderGroupStats.CombatStrength * QJM.HighReducingModifier > attackerGroupStats.CombatStrength)
						{
							// other decisions
							if (defenderGroupStats.CombatStrength * QJM.VeryHighReducingModifier > attackerGroupStats.CombatStrength)
							{
								battleExtension.DefenderPosture = ratioRNG > (FixedPoint)0.5 ? Posture.Pursuit : Posture.AllOutAttack;
								Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : defenderEmpire AI chosed {battleExtension.DefenderPosture} (logicalDecisionValue = {logicalDecisionValue}, ratioRNG = {ratioRNG}, CombatStrength = {defenderGroupStats.CombatStrength} vs {attackerGroupStats.CombatStrength}");
								goto DecisionTaken;
							}
							battleExtension.DefenderPosture = Posture.DeliberateAttack;
							Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : defenderEmpire AI chosed {battleExtension.DefenderPosture} (logicalDecisionValue = {logicalDecisionValue}, CombatStrength = {defenderGroupStats.CombatStrength} vs {attackerGroupStats.CombatStrength}");
							goto DecisionTaken;
						}
						// Defensive posture
						else
						{
							bool canWithdraw = !IsAssault || defenderGroupStats.AverageHealth + ratioRNG < FixedPoint.One;

							// very specific decision
							if (canWithdraw && veryLogicalDecisionValue >= postureRNG100)
							{
								FixedPoint attackerMobilityFactor = attackerGroupStats.AverageMoves * QJM.GetVeterancyModifier(attackerGroupStats.AverageVeterency);
								FixedPoint defenderMobilityFactor = defenderGroupStats.AverageMoves * QJM.GetVeterancyModifier(defenderGroupStats.AverageVeterency);

								if (defenderGroupStats.CombatModifier < attackerGroupStats.CombatModifier * QJM.MediumReducingModifier && defenderMobilityFactor > attackerMobilityFactor * QJM.MediumReducingModifier && defenderGroupStats.AverageHealth > ratioRNG)
								{
									battleExtension.DefenderPosture = Posture.Delay;
									Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : defenderEmpire AI chosed {battleExtension.DefenderPosture} (Health = {defenderGroupStats.AverageHealth}, veryLogicalDecisionValue = {veryLogicalDecisionValue}, CombatStrength = {defenderGroupStats.CombatStrength} vs {attackerGroupStats.CombatStrength}, Mobility = {defenderMobilityFactor} vs {attackerMobilityFactor}");
									goto DecisionTaken;
								}
							}

							// other decisions
							if (canWithdraw && defenderGroupStats.CombatStrength < attackerGroupStats.CombatStrength * QJM.MediumReducingModifier)
							{
								battleExtension.DefenderPosture = ratioRNG < (FixedPoint)0.5 ? Posture.Delay : Posture.Retreat; // low ratioRNG here means average health can still be relatively high in case of assault
								Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : defenderEmpire AI chosed {battleExtension.DefenderPosture} (logicalDecisionValue = {logicalDecisionValue}, ratioRNG = {ratioRNG}, CombatStrength = {defenderGroupStats.CombatStrength} vs {attackerGroupStats.CombatStrength}");
								goto DecisionTaken;
							}

							battleExtension.DefenderPosture = ratioRNG > (FixedPoint)0.5 ? Posture.PreparedDefense : Posture.HastyDefense;
							Diagnostics.Log($"[Gedemon][BattleAutoResolver] ComputeAutoResolve : defenderEmpire AI chosed {battleExtension.DefenderPosture} (logicalDecisionValue = {logicalDecisionValue}, ratioRNG = {ratioRNG}, CombatStrength = {defenderGroupStats.CombatStrength} vs {attackerGroupStats.CombatStrength}");
							goto DecisionTaken;
						}
					}
					// unexpected/risky decisions
					else
					{

					}
				}

			DecisionTaken:;
			}
		}
		public static void ApplyTerrainDataBonus(ListOfStruct<UnitSimplifiedData> attackerUnitsData, ref BattleAutoResolver.AutoResolveTerrainData attackerTerrainData, ListOfStruct<UnitSimplifiedData> defenderUnitsData, ref BattleAutoResolver.AutoResolveTerrainData defenderTerrainData)
		{
			FixedPoint zero = FixedPoint.Zero;
			FixedPoint zero2 = FixedPoint.Zero;
			if (attackerTerrainData.Elevation != defenderTerrainData.Elevation)
			{
				if (attackerTerrainData.Elevation > defenderTerrainData.Elevation)
				{
					zero += BattleAutoResolver.ElevationBonus;
				}
				else if (attackerTerrainData.Elevation < defenderTerrainData.Elevation)
				{
					zero2 += BattleAutoResolver.ElevationBonus;
				}
			}

			if (attackerTerrainData.Rivers != defenderTerrainData.Rivers)
			{
				if (attackerTerrainData.Rivers > defenderTerrainData.Rivers)
				{
					zero2 += BattleAutoResolver.RiversBonus;
				}
				else if (attackerTerrainData.Rivers < defenderTerrainData.Rivers)
				{
					zero += BattleAutoResolver.RiversBonus;
				}
			}

			if (attackerTerrainData.Covers != defenderTerrainData.Covers)
			{
				if (attackerTerrainData.Covers > defenderTerrainData.Covers)
				{
					zero += BattleAutoResolver.CoverBonus;
				}
				else if (attackerTerrainData.Covers < defenderTerrainData.Covers)
				{
					zero2 += BattleAutoResolver.CoverBonus;
				}
			}

			if (attackerTerrainData.Fortifications != defenderTerrainData.Fortifications)
			{
				if (attackerTerrainData.Fortifications > defenderTerrainData.Fortifications)
				{
					zero += BattleAutoResolver.FortificationBonus;
				}
				else if (attackerTerrainData.Fortifications < defenderTerrainData.Fortifications)
				{
					zero2 += BattleAutoResolver.FortificationBonus;
				}
			}

			_ = MercuryPreferences.VerboseInstantResolveLogs;
			int length = attackerUnitsData.Length;
			for (int i = 0; i < length; i++)
			{
				attackerUnitsData.Data[i].CombatStrength += zero;
			}

			length = defenderUnitsData.Length;
			for (int j = 0; j < length; j++)
			{
				defenderUnitsData.Data[j].CombatStrength += zero2;
			}
		}

		public static void LogListBattleGroupArmies(BattleGroup battleGroup)
		{
			List<BattleContender> contenders = new List<BattleContender>(battleGroup.Contenders);
			Diagnostics.LogWarning($"[Gedemon][Battle] LogListbattleGroupArmies: battleGroup.Role = {battleGroup.Role}, battleGroup contenders.Count = {contenders.Count}");
			for (int contenderIndex = 0; contenderIndex < contenders.Count; contenderIndex++)
			{
				List<Participant> participants = new List<Participant>(contenders[contenderIndex].Participants);
				int count = participants.Count;
				for (int i = 0; i < count; i++)
				{
					Participant participant = participants[i];
					Participant_Army participant_Army = participant as Participant_Army;
					if (participant_Army != null)
					{
						Army army = participant_Army.Army;
						Diagnostics.Log($"[Gedemon][Battle] LogListbattleGroupArmies: contender [{contenderIndex}] participant army [{i}] GUID = {army.GUID}");
					}
				}
			}
		}
	}
	//*/
}
