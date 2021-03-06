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
	[BepInPlugin(pluginGuid, "Uchronia", pluginVersion)]
	public class Uchronia : BaseUnityPlugin
	{
		public const string pluginGuid = "gedemon.humankind.uchronia";
		public const string pluginVersion = "1.0.0.1";

		public static bool LoggingStartData = false;

		static public bool SandboxInitialized = false;

		public bool toggleShowTerritory = false;

		public static BepInEx.Logging.ManualLogSource MyLog;

		// Awake is called once when both the game and the plug-in are loaded
		void Awake()
		{
			MyLog = BepInEx.Logging.Logger.CreateLogSource("Uchronia");
			Harmony harmony = new Harmony(pluginGuid);
			Instance = this;
			harmony.PatchAll();
		}
		public static Uchronia Instance;
		

		void Start()
		{
			Logger.LogInfo($"[Gedemon][Uchronia][Started]");

			InvokeRepeating("SlowUpdate", 2.0f, 0.5f);
		}

		private void Update()
		{
			if (Input.GetKeyDown((KeyCode)284)) // press F3 to toggle
			{
				toggleShowTerritory = !toggleShowTerritory;
				int localEmpireIndex = SandboxManager.Sandbox.LocalEmpireIndex;
				UIManager UIManager = Services.GetService<IUIService>() as UIManager;

				// reset to default cursor
				Amplitude.Mercury.Presentation.Presentation.PresentationCursorController.ChangeToDefaultCursor(resetUnitDefinition: false);

				if (toggleShowTerritory)
				{
					// hide UI
					UIManager.IsUiVisible = false;

					// switch to DiplomaticCursor, where territories can be highlighted
					Amplitude.Mercury.Presentation.Presentation.PresentationCursorController.ChangeToDiplomaticCursor(localEmpireIndex);
				}
				else
				{
					// restore UI
					UIManager.IsUiVisible = true;
				}

				Amplitude.Mercury.Presentation.PresentationTerritoryHighlightController HighlightControllerControler = Amplitude.Mercury.Presentation.Presentation.PresentationTerritoryHighlightController;
				HighlightControllerControler.ClearAllTerritoryVisibility();
				int num = HighlightControllerControler.territoryHighlightingInfos.Length;
				for (int i = 0; i < num; i++)
				{
					HighlightControllerControler.SetTerritoryVisibility(i, toggleShowTerritory);
				}
			}
		}

		void SlowUpdate()
		{
			UI.UpdateObjects();
		}



		#region Define Options

		public static readonly GameOptionInfo UseTrueCultureLocation = new GameOptionInfo
		{
			ControlType = UIControlType.Toggle,
			Key = "GameOption_TCL_UseTrueCultureLocation",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			DefaultValue = "True",
			Title = "[TCL] True Culture Location",
			Description = "Toggles unlocking Culture by owned Territories on compatible maps, all options tagged [TCL] are only active when this setting is set to 'ON'",
			States =
			{
				new GameOptionStateInfo
				{
					Title = "On",
					Description = "On",
					Value = "True"
				},
				new GameOptionStateInfo
				{
					Title = "Off",
					Description = "Off",
					Value = "False"
				}
			}
		};

		public static readonly GameOptionInfo CreateTrueCultureLocationOption = new GameOptionInfo
		{
			ControlType = UIControlType.DropList,
			Key = "GameOption_TCL_CreateTrueCultureLocation",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			DefaultValue = "Off",
			Title = "[TCL] Generate TCL for any Map <c=FF0000>*Experimental*</c>",
			Description = "Rename territories on unsupported or generated maps, based on relative distances using the Giant Earth Map coordinates, to use True Culture Location.",
			States =
			{
				new GameOptionStateInfo
				{
					Title = "Disabled",
					Description = "No TCL on unsupported maps",
					Value = "Off"
				},
				new GameOptionStateInfo
				{
					Title = "Grouped by continents",
					Description = "Group Territories by Continents (or by SuperContinents when the map generate a low number of continents)",
					Value = "Continents"
				},
				new GameOptionStateInfo
				{
					Title = "By Coordinates",
					Description = "Use Giant Earth Map territories coordinates for reference",
					Value = "Coordinates"
				},
				new GameOptionStateInfo
				{
					Title = "By Coordinates with shift",
					Description = "Use Giant Earth Map territories coordinates, with Americas shifted back on the West (the Giant Earth use an unconventional presentation, with Americas on the East)",
					Value = "ShiftedCoordinates"
				}
			}
		};

		public static GameOptionStateInfo TerritoryLoss_Full = new GameOptionStateInfo
		{
			Value = "TerritoryLoss_Full",
			Title = "Keep Only New Empire",
			Description = "Lose all territories that were not controlled by the Empire of the new Culture"
		};

		public static GameOptionStateInfo TerritoryLoss_None = new GameOptionStateInfo
		{
			Value = "TerritoryLoss_None",
			Title = "None",
			Description = "Keep control of all your territories when changing Culture"
		};

		public static GameOptionStateInfo TerritoryLoss_KeepAttached = new GameOptionStateInfo
		{
			Value = "TerritoryLoss_KeepAttached",
			Title = "Keep Attached",
			Description = "Territories that are attached to a Settlement that has at least one territory belonging to the new Culture's Empire will not be detached and kept in the Empire, only the other territories will be lost."
		};

		public static GameOptionStateInfo TerritoryLoss_Full_Core = new GameOptionStateInfo
		{
			Value = "TerritoryLoss_Full_Core",
			Title = "Keep Only Core Empire",
			Description = "Lose all territories that were not controlled by the core Empire of the new Culture"
		};
		/*
		public static GameOptionStateInfo TerritoryLoss_ByStability = new GameOptionStateInfo
		{
			Value = "TerritoryLoss_ByStability",
			Title = "By Stability",
			Description = "Territories that were not controlled by the new Culture are kept only if they have a high Stability"
		};
		//*/

		public static GameOptionInfo TerritoryLossOption = new GameOptionInfo
		{
			ControlType = UIControlType.DropList,
			Key = "GameOption_TCL_TerritoryLoss",
			DefaultValue = "TerritoryLoss_None",
			Title = "[TCL] Territory Loss on Culture Change",
			Description = "Determines which territories you may loss when changing Culture",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			States = { TerritoryLoss_None, TerritoryLoss_KeepAttached, TerritoryLoss_Full, TerritoryLoss_Full_Core }//, TerritoryLoss_ByStability }
		};

		private static readonly List<GameOptionStateInfo> ErasCityRequired = new List<GameOptionStateInfo>
		{
			new GameOptionStateInfo
			{
				Title = "Classical",
				Description = "A City or an Administrative Center attached to a City is required in a territory to unlock Cultures of the Classical Era or later",
				Value = "2"
			},
			new GameOptionStateInfo
			{
				Title = "Medieval",
				Description = "A City or an Administrative Center attached to a City is required in a territory to unlock Cultures of the Medieval Era or later",
				Value = "3"
			},
			new GameOptionStateInfo
			{
				Title = "Early Modern",
				Description = "A City or an Administrative Center attached to a City is required in a territory to unlock Cultures of the Early Modern Era or later",
				Value = "4"
			},
			new GameOptionStateInfo
			{
				Title = "Industrial",
				Description = "A City or an Administrative Center attached to a City is required in a territory to unlock Cultures of the Industrial Era or later",
				Value = "5"
			},
			new GameOptionStateInfo
			{
				Title = "Contemporary",
				Description = "A City or an Administrative Center attached to a City is required in a territory to unlock Cultures of the Contemporary Era or later",
				Value = "6"
			},
			new GameOptionStateInfo
			{
				Title = "None",
				Description = "A City or an Administrative Center attached to a City is never required",
				Value = "99"
			}
		};

		public static GameOptionInfo FirstEraRequiringCityToUnlock = new GameOptionInfo
		{
			ControlType = UIControlType.DropList,
			Key = "GameOption_TCL_FirstEraRequiringCityToUnlock",
			DefaultValue = "3",
			Title = "[TCL] First Era for City Requirement",
			Description = "First Era from whitch a City (or an Administrative Center attached to a City) is required on a Culture's territory to unlock it",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			States = ErasCityRequired
		};

		private static readonly List<GameOptionStateInfo> NumEmpireSlots = new List<GameOptionStateInfo>
		{
			new GameOptionStateInfo
			{
				Title = "No Extra",
				Description = "No additional Empires, use the New Game setting for Competitors",
				Value = "0"
			},
			new GameOptionStateInfo
			{
				Title = "11",
				Description = "11 Empires",
				Value = "11"
			},
			new GameOptionStateInfo
			{
				Title = "12",
				Description = "12 Empires",
				Value = "12"
			},
			new GameOptionStateInfo
			{
				Title = "13",
				Description = "13 Empires",
				Value = "13"
			},
			new GameOptionStateInfo
			{
				Title = "14",
				Description = "14 Empires",
				Value = "14"
			},
			new GameOptionStateInfo
			{
				Title = "15",
				Description = "15 Empires",
				Value = "15"
			},
			new GameOptionStateInfo
			{
				Title = "16",
				Description = "16 Empires",
				Value = "16"
			},
			/*
			new GameOptionStateInfo
			{
				Title = "17",
				Description = "17 Empires",
				Value = "17"
			},
			new GameOptionStateInfo
			{
				Title = "18",
				Description = "18 Empires",
				Value = "18"
			},
			new GameOptionStateInfo
			{
				Title = "19",
				Description = "19 Empires",
				Value = "19"
			},
			new GameOptionStateInfo
			{
				Title = "20",
				Description = "20 Empires",
				Value = "20"
			},
			new GameOptionStateInfo
			{
				Title = "22",
				Description = "22 Empires",
				Value = "22"
			},
			new GameOptionStateInfo
			{
				Title = "25",
				Description = "25 Empires",
				Value = "25"
			},
			new GameOptionStateInfo
			{
				Title = "28",
				Description = "28 Empires",
				Value = "28"
			},
			new GameOptionStateInfo
			{
				Title = "32",
				Description = "32 Empires",
				Value = "32"
			},
			//*/
		};

		private static readonly List<GameOptionStateInfo> NumSettlingEmpireSlots = new List<GameOptionStateInfo>
		{
			new GameOptionStateInfo
			{
				Title = "No Limit",
				Description = "No limitation, all AI Empires can create outposts",
				Value = "99"
			},
			//new GameOptionStateInfo
			//{
			//	Title = "0",
			//	Description = "Only Humans",
			//	Value = "0"
			//},
			new GameOptionStateInfo
			{
				Title = "1",
				Description = "Slot 1",
				Value = "1"
			},
			new GameOptionStateInfo
			{
				Title = "2",
				Description = "Slot 1 and 2",
				Value = "2"
			},
			new GameOptionStateInfo
			{
				Title = "3",
				Description = "Slot 1 to 3",
				Value = "3"
			},
			new GameOptionStateInfo
			{
				Title = "4",
				Description = "Slot 1 to 4",
				Value = "4"
			},
			new GameOptionStateInfo
			{
				Title = "5",
				Description = "Slot 1 to 5",
				Value = "5"
			},
			new GameOptionStateInfo
			{
				Title = "6",
				Description = "Slot 1 to 6",
				Value = "6"
			},
			new GameOptionStateInfo
			{
				Title = "7",
				Description = "Slot 1 to 7",
				Value = "7"
			},
			new GameOptionStateInfo
			{
				Title = "8",
				Description = "Slot 1 to 8",
				Value = "8"
			},
			new GameOptionStateInfo
			{
				Title = "9",
				Description = "Slot 1 to 9",
				Value = "9"
			},
			new GameOptionStateInfo
			{
				Title = "10",
				Description = "Slot 1 to 10",
				Value = "10"
			},
		};

		public static GameOptionInfo ExtraEmpireSlots = new GameOptionInfo
		{
			ControlType = UIControlType.DropList,
			Key = "GameOption_TCL_ExtraEmpireSlots",
			DefaultValue = "0",
			Title = "Maximum number of Competitors",
			Description = "Add extra Empire Slots in game (AI only), allowing a maximum of 16 Empires (10 from setup + 6 extra AI slots). This can be used either on custom maps compatible with this mod or any random map, but will cause an error with incompatible custom maps. With [TCL], Nomadic Tribes controlled by those AI players will be able to spawn as a new Empire on the location of a Culture that has not been controlled yet, or take control of some territories of an old Empire during a split. (this setting override the default Starting Positions when using the Giant Earth Map). If you don't use [TCL], you should use (AOM) 'Allow Duplicate Cultures' Mod",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			States = NumEmpireSlots
		};

		public static GameOptionInfo SettlingEmpireSlotsOption = new GameOptionInfo
		{
			ControlType = UIControlType.DropList,
			Key = "GameOption_TCL_SettlingEmpireSlotsOption",
			DefaultValue = "10",
			Title = "[TCL] Number of slot IDs that start in Neolithic",
			Description = "Set how many Slots (from the setup screen) are allowed to spawn in the Neolithic Era (this option ignore humans player slots). Empire controlled by the AI players from higher slots will be able to spawn only as a new Empire on the location of a Culture that has not been taken after changing Era, or take control of some territories of an old Empire during a split.",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			States = NumSettlingEmpireSlots
		};

		public static readonly GameOptionInfo StartingOutpost = new GameOptionInfo
		{
			ControlType = UIControlType.DropList,
			Key = "GameOption_TCL_StartingOutpost",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			DefaultValue = "Off",
			Title = "Start with an Outpost <c=FF0000>(disabled in MP)</c>",
			Description = "Toggle if Empires will start with an Outpost. This setting can be used on any map. Currently disabled in MP as it cause desyncs.",
			States =
			{
				new GameOptionStateInfo
				{
					Title = "On",
					Description = "Everyone start with an Outpost",
					Value = "On"
				},
				new GameOptionStateInfo
				{
					Title = "Off",
					Description = "No starting Outposts",
					Value = "Off"
				},
				new GameOptionStateInfo
				{
					Title = "AI Only",
					Description = "Only AI players will start with an outpost",
					Value = "OnlyAI"
				}
			}
		};

		public static readonly GameOptionInfo HistoricalDistrictsOption = new GameOptionInfo
		{
			ControlType = UIControlType.Toggle,
			Key = "GameOption_TCL_HistoricalDistrictsOption",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			DefaultValue = "True",
			Title = "Historical Districts",
			Description = "Toggle to keep Districts initial visual appearence",
			States =
			{
				new GameOptionStateInfo
				{
					Title = "On",
					Description = "On",
					Value = "True"
				},
				new GameOptionStateInfo
				{
					Title = "Off",
					Description = "Off",
					Value = "False"
				}
			}
		};

		public static readonly GameOptionInfo CityMapOption = new GameOptionInfo
		{
			ControlType = UIControlType.Toggle,
			Key = "GameOption_TCL_CityMapOption",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			DefaultValue = "True",
			Title = "[MAP] Use City Map for naming",
			Description = "Toggle to use the City Map (True Location naming for cities) when possible",
			States =
			{
				new GameOptionStateInfo
				{
					Title = "On",
					Description = "On",
					Value = "True"
				},
				new GameOptionStateInfo
				{
					Title = "Off",
					Description = "Off",
					Value = "False"
				}
			}
		};

		public static readonly GameOptionInfo StartPositionList = new GameOptionInfo
		{
			ControlType = UIControlType.DropList,
			Key = "GameOption_TCL_StartPositionList",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			DefaultValue = "Default",
			Title = "[MAP] Starting Position List",
			Description = "Choose if you want the map's default Starting Positions or one of the alternate list (only active when compatible maps are used)",
			States =
			{
				new GameOptionStateInfo
				{
					Title = "Map Default",
					Description = "Use the Map's default Starting Positions (this setting is overriden if the maximum number of competitor is raised above 10, the Alternate list is then used if available)",
					Value = "Default"
				},
				new GameOptionStateInfo
				{
					Title = "Alternate",
					Description = "Use only the Alternate Starting Positions, ignoring the map's default positions aven with 10 players or less. Some starting positions may be adjacent from each other, even with a low number of players as the ist is made for 16 slots)",
					Value = "ExtraStart"
				},
				new GameOptionStateInfo
				{
					Title = "Old World",
					Description = "Use only the Alternate starting positions (Old World starts only, if the map allows it), ignoring the map's default positions.",
					Value = "OldWorld"
				}
			}
		};

		public static readonly GameOptionInfo CompensationLevel = new GameOptionInfo
		{
			ControlType = UIControlType.DropList,
			Key = "GameOption_TCL_CompensationLevel",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			DefaultValue = "2",
			Title = "[TCL] Level of Compensation",
			Description = "Define the level of compensation an Empire will get per Settlement lost during an Evolution, based on total number of Settlements for Influence, and on yield per turn for Money, Science, Production",
			States =
			{
				new GameOptionStateInfo
				{
					Title = "None",
					Description = "No compensation.",
					Value = "0"
				},
				new GameOptionStateInfo
				{
					Title = "Low",
					Description = "Low compensation (x5)",
					Value = "1"
				},
				new GameOptionStateInfo
				{
					Title = "Average",
					Description = "Average compensation (x10)",
					Value = "2"
				},
				new GameOptionStateInfo
				{
					Title = "High",
					Description = "High compensation (x20)",
					Value = "3"
				}
			}
		};

		public static readonly GameOptionInfo StartingOutpostForMinorOption = new GameOptionInfo
		{
			ControlType = UIControlType.Toggle,
			Key = "GameOption_TCL_StartingOutpostForMinorOption",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			DefaultValue = "False",
			Title = "[TCL] Starting Outpost For Minor Faction",
			Description = "Toggle to set if the minor factions directly spawn an outpost on their starting territory or if they are allowed to settle after moving on the map.",
			States =
			{
				new GameOptionStateInfo
				{
					Title = "On",
					Description = "On",
					Value = "True"
				},
				new GameOptionStateInfo
				{
					Title = "Off",
					Description = "Off",
					Value = "False"
				}
			}
		};

		public static readonly GameOptionInfo LargerSpawnAreaForMinorOption = new GameOptionInfo
		{
			ControlType = UIControlType.Toggle,
			Key = "GameOption_TCL_LargerSpawnAreaForMinorOption",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			DefaultValue = "True",
			Title = "[TCL] Larger Spawn Area For Minor Faction",
			Description = "Toggle to set if the Minor Factions are allowed to spawn in adjacent territories or if they can only spawn in their starting positions territory. As a lot of territories are shared with the Major Empires, allowing a larger spawn area means more Minor Factions will be able to spawn.",
			States =
			{
				new GameOptionStateInfo
				{
					Title = "On",
					Description = "On",
					Value = "True"
				},
				new GameOptionStateInfo
				{
					Title = "Off",
					Description = "Off",
					Value = "False"
				}
			}
		};

		public static readonly GameOptionInfo EmpireIconsNumColumnOption = new GameOptionInfo
		{
			ControlType = UIControlType.DropList,
			Key = "GameOption_TCL_EmpireIconsNumColumnOption",
			GroupKey = "GameOptionGroup_LobbyDifficultyOptions",
			DefaultValue = "4",
			Title = "Max number of columns for Empire Icons",
			Description = "Set the maximum width for the Empire Icons panel displayed at the top left of the screen. You will have to change the UI size accordingly (for example 75% for 9 icons)",
			States =
			{
				new GameOptionStateInfo
				{
					Title = "4 (Any UI size)",
					Description = "4 icons (Can be used with any UI size)",
					Value = "4"
				},
				new GameOptionStateInfo
				{
					Title = "5 (90% UI size)",
					Description = "5 icons (maximum UI size: 90%)",
					Value = "5"
				},
				new GameOptionStateInfo
				{
					Title = "6 (85% UI size)",
					Description = "6 icons (maximum UI size: 85%)",
					Value = "6"
				},
				new GameOptionStateInfo
				{
					Title = "7 (80% UI size)",
					Description = "7 icons (maximum UI size: 80%)",
					Value = "7"
				},
				new GameOptionStateInfo
				{
					Title = "8 (75% UI size)",
					Description = "8 icons (maximum UI size: 75%)",
					Value = "8"
				},
				new GameOptionStateInfo
				{
					Title = "9 (75% UI size)",
					Description = "9 icons (maximum UI size: 75%)",
					Value = "9"
				},
			}
		};

		#endregion

		#region Set options

		public bool Enabled => GameOptionHelper.CheckGameOption(UseTrueCultureLocation, "True");
		public bool OnlyCultureTerritory => !GameOptionHelper.CheckGameOption(TerritoryLossOption, "TerritoryLoss_None");
		public bool HistoricalDistricts => GameOptionHelper.CheckGameOption(HistoricalDistrictsOption, "True");
		public bool UseCityMap => GameOptionHelper.CheckGameOption(CityMapOption, "True");
		public bool KeepAttached => GameOptionHelper.CheckGameOption(TerritoryLossOption, "TerritoryLoss_KeepAttached");
		public bool KeepOnlyCore => GameOptionHelper.CheckGameOption(TerritoryLossOption, "TerritoryLoss_Full_Core");
		public bool OnlyOldWorldStart => GameOptionHelper.CheckGameOption(StartPositionList, "OldWorld");
		public bool OnlyExtraStart => GameOptionHelper.CheckGameOption(StartPositionList, "ExtraStart");
		public bool StartingOutpostForAI => !GameOptionHelper.CheckGameOption(StartingOutpost, "Off");
		public bool StartingOutpostForHuman => GameOptionHelper.CheckGameOption(StartingOutpost, "On");
		public bool StartingOutpostForMinorFaction => GameOptionHelper.CheckGameOption(StartingOutpostForMinorOption, "True");
		public bool LargerSpawnAreaForMinorFaction => GameOptionHelper.CheckGameOption(LargerSpawnAreaForMinorOption, "True");
		public bool CanCreateTrueCultureLocation => !GameOptionHelper.CheckGameOption(CreateTrueCultureLocationOption, "Off");
		public bool UseShiftToCreateTCL => GameOptionHelper.CheckGameOption(CreateTrueCultureLocationOption, "ShiftedCoordinates");
		public bool UseCoordinatesToCreateTCL => GameOptionHelper.CheckGameOption(CreateTrueCultureLocationOption, "Coordinates");
		public int EraIndexCityRequiredForUnlock => int.Parse(GameOptionHelper.GetGameOption(FirstEraRequiringCityToUnlock));
		public int TotalEmpireSlots => int.Parse(GameOptionHelper.GetGameOption(ExtraEmpireSlots));
		public int SettlingEmpireSlots => int.Parse(GameOptionHelper.GetGameOption(SettlingEmpireSlotsOption));
		public int CompensationLevelValue => int.Parse(GameOptionHelper.GetGameOption(CompensationLevel));
		public int EmpireIconsNumColumn => int.Parse(GameOptionHelper.GetGameOption(EmpireIconsNumColumnOption));

		#endregion

		#region Get Options

		public static bool IsEnabled()
		{
			return Instance.Enabled;
		}
		public static int GetEmpireIconsNumColumn()
		{
			return Instance.EmpireIconsNumColumn;
		}
		public static bool KeepHistoricalDistricts()
		{
			return Instance.HistoricalDistricts;
		}
		public static bool CanUseCityMap()
		{
			return Instance.UseCityMap;
		}
		public static bool KeepOnlyCultureTerritory()
		{
			return Instance.OnlyCultureTerritory;
		}
		public static bool KeepOnlyCoreTerritories()
		{
			return Instance.KeepOnlyCore;
		}
		public static bool KeepTerritoryAttached()
		{
			return Instance.KeepAttached;
		}
		public static int GetEraIndexCityRequiredForUnlock()
		{
			return Instance.EraIndexCityRequiredForUnlock;
		}

		public static int GetTotalEmpireSlots()
		{
			return Instance.TotalEmpireSlots;
		}

		public static int GetSettlingEmpireSlots()
		{
			return Instance.SettlingEmpireSlots;
		}

		public static int GetCompensationLevel()
		{
			return Instance.CompensationLevelValue;
		}

		public static bool UseExtraEmpireSlots()
		{
			return Instance.TotalEmpireSlots > 0;
		}
		public static bool UseOnlyOldWorldStart()
		{
			return Instance.OnlyOldWorldStart;
		}
		public static bool UseOnlyExtraStart()
		{
			return Instance.OnlyExtraStart;
		}
		public static bool UseLargerSpawnAreaForMinorFaction()
		{
			return Instance.LargerSpawnAreaForMinorFaction;
		}
		public static bool UseStartingOutpostForMinorFaction()
		{
			return Instance.StartingOutpostForMinorFaction;
		}
		public static bool CanCreateTCL()
		{
			return Instance.CanCreateTrueCultureLocation;
		}
		public static bool UseReferenceCoordinates()
		{
			return Instance.UseCoordinatesToCreateTCL || Instance.UseShiftToCreateTCL;
		}
		public static bool UseShiftedCoordinates()
		{
			return Instance.UseShiftToCreateTCL;
		}

		public static bool HasStartingOutpost(int EmpireIndex, bool IsHuman)
		{

			Diagnostics.LogWarning($"[Gedemon] HasStartingOutpost EmpireIndex = {EmpireIndex}, IsHuman = {IsHuman},  StartingOutpostForAI = {Instance.StartingOutpostForAI}, StartingOutpostForHuman = {Instance.StartingOutpostForHuman}, option = {GameOptionHelper.GetGameOption(StartingOutpost)}");
			if (!IsSettlingEmpire(EmpireIndex, IsHuman))
			{
				return false;
			}
			if (IsHuman)
			{
				return Instance.StartingOutpostForHuman;
			}
			else
			{
				return Instance.StartingOutpostForAI;
			}
		}
		public static bool HasStartingOutpost(int EmpireIndex)
		{
			return HasStartingOutpost(EmpireIndex, IsEmpireHumanSlot(EmpireIndex));
		}

		public static bool IsSettlingEmpire(int EmpireIndex, bool IsHuman)
		{
			int numSlots = GetSettlingEmpireSlots();

			if (numSlots == 0 && !IsHuman)
				return false;

			if (EmpireIndex < numSlots || IsHuman)
			{
				return true;
			}
			return false;
		}
		public static bool IsSettlingEmpire(int EmpireIndex)
		{
			return IsSettlingEmpire(EmpireIndex, IsEmpireHumanSlot(EmpireIndex));
		}

		public static bool IsEmpireHumanSlot(int empireIndex)
		{
			ISessionService service = Services.GetService<ISessionService>();
			ISessionSlotController slots = ((Amplitude.Mercury.Session.Session)service.Session).Slots;
			if (empireIndex >= slots.Count)
			{
				return false;
			}
			return slots[empireIndex].IsHuman;
		}

		#endregion
		
		public static void CreateStartingOutpost()
		{
			ISessionService sessionService = Services.GetService<ISessionService>();
			bool isMultiplayer = sessionService != null && sessionService.Session != null && sessionService.Session.SessionMode == SessionMode.Online;
			if (isMultiplayer)
				return;

			int numMajor = Amplitude.Mercury.Sandbox.Sandbox.MajorEmpires.Length;
			for (int empireIndex = 0; empireIndex < numMajor; empireIndex++)
			{
				MajorEmpire majorEmpire = Sandbox.MajorEmpires[empireIndex];
				bool isHuman = Uchronia.IsEmpireHumanSlot(empireIndex);
				WorldPosition worldPosition = new WorldPosition(World.Tables.SpawnLocations[empireIndex]);
				SimulationEntityGUID GUID = SimulationEntityGUID.Zero;

				Diagnostics.LogWarning($"[Gedemon] [CreateStartingOutpost] for {majorEmpire.majorEmpireDescriptorName}, index = {empireIndex}, IsControlledByHuman = {isHuman}"); // IsEmpireHumanSlot(int empireIndex)

				if (Uchronia.HasStartingOutpost(empireIndex, isHuman)) // 
				{
					majorEmpire.DepartmentOfTheInterior.CreateCampAt(GUID, worldPosition, FixedPoint.Zero, isImmediate : true);
				}
			}
		}

		public static void Log(string text)
		{
			MyLog.LogInfo(text);
		}
	}

    #region Patches


	[HarmonyPatch(typeof(Timeline))]
	public class Timeline_Patch
	{
		[HarmonyPatch("InitializeOnStart")]
		[HarmonyPostfix]
		public static void InitializeOnStart(Timeline __instance, SandboxStartSettings sandboxStartSettings)
		{
			Diagnostics.LogWarning($"[Gedemon] [Timeline] InitializeOnStart");
			// reinitialize globalEraThresholds
			int numSettlingEmpires = Uchronia.GetSettlingEmpireSlots();
			if (CultureUnlock.UseTrueCultureLocation() && numSettlingEmpires < sandboxStartSettings.NumberOfMajorEmpires)
			{
				Diagnostics.LogWarning($"[Gedemon] in Timeline, InitializeOnStart, reset globalEraThresholds for {numSettlingEmpires} Settling Empires / {sandboxStartSettings.NumberOfMajorEmpires} Major Empires");

				__instance.globalEraThresholds[__instance.StartingEraIndex] = __instance.eraDefinitions[__instance.StartingEraIndex].BaseGlobalEraThreshold * numSettlingEmpires;
				for (int l = __instance.StartingEraIndex + 1; l <= __instance.EndingEraIndex; l++)
				{
					__instance.globalEraThresholds[l] = __instance.globalEraThresholds[l - 1] + __instance.eraDefinitions[l].BaseGlobalEraThreshold * numSettlingEmpires;
				}
			}
		}

		[HarmonyPatch("GetGlobalEraIndex")]
		[HarmonyPrefix]
		public static bool GetGlobalEraIndex(Timeline __instance, ref int __result)
		{
			//Diagnostics.LogWarning($"[Gedemon] [Timeline] GetGlobalEraIndex, Sandbox.EndGameController.TurnLimit = {Sandbox.EndGameController.TurnLimit}");

			int sumEras = 0;
			int numActive = 0;
			int topEra = 0;

			int maxNeolithicEraTurns = Sandbox.EndGameController.TurnLimit / 20; // 15 turns at standard
			for (int i = 0; i < Amplitude.Mercury.Sandbox.Sandbox.NumberOfMajorEmpires; i++)
			{
				MajorEmpire majorEmpire = Amplitude.Mercury.Sandbox.Sandbox.MajorEmpires[i];
                if(majorEmpire.IsAlive && !CultureChange.IsSleepingEmpire(majorEmpire))
				{
					int empireEra = majorEmpire.DepartmentOfDevelopment.CurrentEraIndex;
					numActive++;
					sumEras += empireEra;
					topEra = empireEra > topEra ? empireEra : topEra;
				}
			}

			if(numActive > 0)
            {
				__result = System.Math.Max( sumEras / numActive, topEra - 1);

				if (__result == 0 && SandboxManager.Sandbox.Turn > maxNeolithicEraTurns)
				{
					__result = __instance.StartingEraIndex + 1;
					return false;
				}				

				return false;
			}
			//*
            else
            {
                if (SandboxManager.Sandbox.Turn > maxNeolithicEraTurns)
				{
					__result = __instance.StartingEraIndex + 1;
					return false;
				}
			}
			//*/

			__result = __instance.StartingEraIndex;
			return false;
		}
	}


	[HarmonyPatch(typeof(AvatarManager))]
	public class AvatarManager_Patch
	{
		[HarmonyPatch("ForceAvatarSummaryTo")]
		[HarmonyPrefix]
		public static bool ForceAvatarSummaryTo(AvatarManager __instance, AvatarId avatarId, ref Amplitude.Mercury.Avatar.AvatarSummary avatarSummary)
		{
			// compatibility fix for January 2022 patch, seems that now slots > 10 don't get a random avatar summary in session initialization
			if (avatarSummary.ElementKeyBySlots == null || avatarSummary.ElementKeyBySlots.Length == 0)
			{
				Diagnostics.LogError($"[Gedemon] [AvatarManager] ForceAvatarSummaryTo: avatarID #{avatarId.Index} has no avatar summary, calling GetRandomAvatarSummary...");
				__instance.GetRandomAvatarSummary(avatarId.Index, ref avatarSummary);
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(StatisticReporter_EndTurn))]
	public class StatisticReporter_EndTurn_Patch
	{

		[HarmonyPrefix]
		[HarmonyPatch(nameof(Load))]
		public static bool Load()
		{
			CultureChange.Load();
			return true;
		}
		
		[HarmonyPrefix]
		[HarmonyPatch(nameof(Unload))]
		public static bool Unload()
		{
			CultureChange.Unload();
			return true;
		}	
	}


	[HarmonyPatch(typeof(PresentationTerritoryHighlightController))]
	public class PresentationTerritoryHighlightController_Patch
	{

		[HarmonyPatch("InitTerritoryLabels")]
		[HarmonyPostfix]
		public static void InitTerritoryLabelsPost(PresentationTerritoryHighlightController __instance)
		{
			int localEmpireIndex = SandboxManager.Sandbox.LocalEmpireIndex;
			MajorEmpire majorEmpire = Sandbox.MajorEmpires[localEmpireIndex];
			CultureChange.UpdateTerritoryLabels(majorEmpire.DepartmentOfDevelopment.CurrentEraIndex);
		}
	}

	[HarmonyPatch(typeof(ComputeSpecificMissions))]
	public class ComputeSpecificMissions_Patch
	{
		[HarmonyPatch("ComputeTerritoryClaimScore")]
		[HarmonyPostfix]
		public static void ComputeTerritoryClaimScore(ComputeSpecificMissions __instance, ref HeuristicFloat __result, Amplitude.Mercury.Interop.AI.Entities.MajorEmpire majorEmpire, Amplitude.Mercury.Interop.AI.Entities.Army army, Amplitude.Mercury.AI.Brain.AnalysisData.Territory.TerritoryData territoryData)
		{
			bool hasSettlement = majorEmpire.Settlements.Length > 0;

			float unlockMotivation = hasSettlement ? 5.0f : 15.0f;
			//Diagnostics.LogWarning($"[Gedemon] ComputeTerritoryClaimScore by {majorEmpire.FactionName} (has settlement = {majorEmpire.Settlements.Length > 0}) for ({CultureUnlock.GetTerritoryName(army.TerritoryIndex)}), result = {__result.Value}, Era = {majorEmpire.EraDefinitionIndex}");
			if (CultureUnlock.IsNextEraUnlock(army.TerritoryIndex, majorEmpire.EraDefinitionIndex))
			{
				__result.Add(unlockMotivation);
				//Diagnostics.LogWarning($"[Gedemon] IsNextEraUnlock = true - New result = {__result.Value}");
			}
			else
			{
				if (!hasSettlement)
					__result.Divide(2.0f);

				//Diagnostics.LogWarning($"[Gedemon] IsNextEraUnlock = false - New result = {__result.Value}");
			}
		}

		/*
		[HarmonyPatch("ComputeTerritoryClaimScore")]
		[HarmonyPostfix]
		public static void ComputeTerritoryToClaim(ComputeSpecificMissions __instance, ref HeuristicValue<int> __result, Amplitude.Mercury.AI.Brain.MajorEmpireBrain brain, Army army, bool allowRansack)
		{
			
		}
		//*/
	}
	#endregion

	//*
	[HarmonyPatch(typeof(MainMenuScreen))]
	public class MainMenuScreen_Patch
	{
		static bool isInitialized = false;

		[HarmonyPatch("Refresh")]
		[HarmonyPostfix]
		public static void Refresh(MainMenuScreen __instance)
		{
			if(!isInitialized)
            {
				ModLoading.OnMainMenuLoaded();
				isInitialized = true;
			}

			Transform[] children = __instance.transform.GetChildren();
			foreach(Transform child in children)
            {
				Diagnostics.LogWarning($"[Gedemon][MainMenuScreen] {__instance.name} Refresh  - child {child.name}");
                if(child.name == "LogoGroup")
				{
					Diagnostics.Log($"[Gedemon][MainMenuScreen] Found Logogroup !");

					var logoTitle = child.Find("LogoTitle");
					var logo = child.Find("Logo");

					string basefolder = Amplitude.Framework.Application.GameDirectory;
					string path = System.IO.Path.Combine(basefolder, Amplitude.Mercury.Runtime.RuntimeManager.RuntimeModuleFolders.Community.Name);
					Diagnostics.Log($"[Gedemon] searching for .png files in {path}");
					if (Directory.Exists(path))
					{
						DirectoryInfo directoryInfo = new DirectoryInfo(path);
						if (directoryInfo.Exists)
						{
							Diagnostics.Log($"[Gedemon] searching files in {directoryInfo.FullName}");
							string searchPattern = "*UchroniaLogo.png";
							FileInfo[] files = directoryInfo.GetFiles(searchPattern, SearchOption.AllDirectories);
							foreach (FileInfo fileInfo in files)
							{
								Diagnostics.Log($"[Gedemon] found file : ({fileInfo.FullName})");
								UIAbstractImage title = logoTitle.GetComponent<UIAbstractImage>();
								Texture2D logoPictureUnityTexture = new Texture2D(1, 1);
								byte[] byteArray = File.ReadAllBytes(fileInfo.FullName);
								if (!logoPictureUnityTexture.LoadImage(byteArray))
								{
									return;
								}
								UITexture logoPictureTexture = new UITexture(Amplitude.Framework.Guid.NewGuid(), UITextureFlags.AlphaStraight, UITextureColorFormat.Srgb, logoPictureUnityTexture);
								title.texture = logoPictureTexture;
							}

							string searchPattern2 = "*UchroniaTitleLogo.png";
							FileInfo[] files2 = directoryInfo.GetFiles(searchPattern2, SearchOption.AllDirectories);
							foreach (FileInfo fileInfo in files2)
							{
								Diagnostics.Log($"[Gedemon] found file : ({fileInfo.FullName})");
								UIAbstractImage title = logo.GetComponent<UIAbstractImage>();
								Texture2D logoPictureUnityTexture = new Texture2D(1, 1);
								byte[] byteArray = File.ReadAllBytes(fileInfo.FullName);
								if (!logoPictureUnityTexture.LoadImage(byteArray))
								{
									return;
								}
								UITexture logoPictureTexture = new UITexture(Amplitude.Framework.Guid.NewGuid(), UITextureFlags.AlphaStraight, UITextureColorFormat.Srgb, logoPictureUnityTexture);
								title.texture = logoPictureTexture;
							}
						}
					}
				}
			}
		}
	}
	//*/


	[HarmonyPatch(typeof(LobbyScreen_LobbySlotsPanel))]
	public class LobbyScreen_LobbySlotsPanel_Patch
	{

		[HarmonyPrefix]
		[HarmonyPatch(nameof(Refresh))]
		public static bool Refresh(LobbyScreen_LobbySlotsPanel __instance)
		{
			if (__instance.session == null)
			{
				return false;
			}
			for (int i = 0; i < 10; i++)
			{
				__instance.allLobbySlots[i].Unbind();
			}
			int count = System.Math.Min(10, __instance.session.Slots.Count);
			for (int j = 0; j < count; j++)
			{
				LobbySlot lobbySlot = __instance.allLobbySlots[j];
				lobbySlot.Bind(__instance.session.Slots[j], __instance.session, __instance.lobbyScreen, __instance);
				lobbySlot.Show();
				if (__instance.lobbySlotSettingsPanel.IsAttachedTo(lobbySlot))
				{
					__instance.lobbySlotSettingsPanel.Dirtyfy();
				}
			}
			for (int k = count; k < 10; k++)
			{
				__instance.allLobbySlots[k].Hide();
			}
			__instance.addSlotButton.UITransform.VisibleSelf = __instance.session.IsHosting && count < __instance.lobbyScreen.AllowedMaxLobbySlots && !__instance.lobbyScreen.IsMultiplayerSave;

			return false;
		}

		[HarmonyPrefix]
		[HarmonyPatch(nameof(UpdateSlot))]
		public static bool UpdateSlot(LobbyScreen_LobbySlotsPanel __instance, SessionSlot sessionSlot)
		{
			if (sessionSlot.Index >= 10)
			{
				return false;
			}
			return true;
		}
	}

	// bugfix
	[HarmonyPatch(typeof(Amplitude.Mercury.Interop.AI.AIEntity))]
	public class AIEntity_patch
	{
		static SimulationEntityGUID lastGUID = 0;

		[HarmonyPrefix]
		[HarmonyPatch(nameof(Synchronize))]
		public static bool Synchronize(Amplitude.Mercury.Interop.AI.AIEntity __instance, ISimulationEntity simulationEntity)
		{
			if(__instance is Amplitude.Mercury.Interop.AI.Entities.Army)
			{
				Army army = simulationEntity as Army;
                if(army != null)
				{
					//Uchronia.Log($"AIEntitySynchronizer, Synchronize Army, simulationEntity Army GUID = { army.GUID }");

					if(lastGUID == army.GUID)
					{
						Diagnostics.LogError($"[Gedemon] Before AIEntitySynchronizer, Synchronize Army, simulationEntity Army GUID = { army.GUID } was already the last called, checking for error...");

						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .SpawnType = {army.Units[0].UnitDefinition.SpawnType}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .PathfindContext = {PathfindContext.GetArmyPathfindContextForWorld(army)}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .TileIndex = {army.WorldPosition.ToTileIndex()}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .TerritoryIndex = {Amplitude.Mercury.Sandbox.Sandbox.World.TileInfo.Data[army.WorldPosition.ToTileIndex()].TerritoryIndex}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .State =  {army.State}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .IsLocked = {army.IsLocked}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .IsNomadic = {army.IsNomadic}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .IsMercenary = {army.MercenaryIndex >= 0}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .IsTrespassing = {DepartmentOfDefense.IsArmyTrespassing(army, army.WorldPosition.ToTileIndex()) == DepartmentOfDefense.TrespassingState.Trespassing}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .IsRetreating = {army.HasUnitStatus(DepartmentOfDefense.RetreatedUnitStatusName)}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .Flags = {army.Flags}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .HealthRatio = {army.HealthRatio}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .AutoExplore = {army.AutoExplore}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .ArmyMissionIndex ={army.ArmyMissionIndex}");

						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .Empire.Entity.Index ={army.Empire.Entity.Index}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: canMoveOnWater ={(army.MovementAbility & Amplitude.Mercury.Data.World.PathfindingMovementAbility.Water) != 0}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .Units.Count ={army.Units.Count}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: ArmyGoToAction ={Amplitude.Mercury.Sandbox.Sandbox.ActionController.GetActionFor<ArmyGoToAction, Amplitude.Mercury.Simulation.Army>(army)}");
						Diagnostics.Log($"[Gedemon][Synchronize] Check to Synchronize: .LockedByBattles.Count ={army.LockedByBattles.Count}");

						int num2 = army.LockedByBattles.Count;
						List<ulong> RemoveLockedByBattles = new List<ulong>();
						for (int k = 0; k < num2; k++)
						{
							ulong battleGUID = army.LockedByBattles[k];
							Diagnostics.Log($"[Gedemon][Synchronize] LogListbattleGroupArmies: locked by battle guid ={battleGUID}");
							Amplitude.Mercury.Sandbox.Sandbox.BattleRepository.TryGetBattle(battleGUID, out var battle);
							Diagnostics.Log($"[Gedemon][Synchronize] LogListbattleGroupArmies: battle exists ={battle != null}");
							if(battle != null)
                            {
								if (battle.TryGetParticipant(army.GUID, out var participant))
								{
									Diagnostics.Log($"[Gedemon][Synchronize] LogListbattleGroupArmies: participant.Role ={participant.Role}");
									break;
								}
							}
                            else
							{
								Diagnostics.LogError($"[Gedemon][Synchronize] Removing Army #{ army.GUID } reference to non-existing locking battle...");
								RemoveLockedByBattles.Add(battleGUID);
							}
						}

						for (int k = 0; k < RemoveLockedByBattles.Count; k++)
						{
							army.LockedByBattles.Remove(RemoveLockedByBattles[k]);
						}
					}
					lastGUID = army.GUID;
				}

			}
			return true;
		}
	}

	/*
	[HarmonyPatch(typeof(EliminationController))]
	public class EliminationController_Patch
	{
		[HarmonyPatch("Eliminate")]
		[HarmonyPrefix]
		public static bool Eliminate(EliminationController __instance, MajorEmpire majorEmpire)
		{

			if (CultureUnlock.UseTrueCultureLocation())
			{
				//majorEmpire.OnFreeing();
				//majorEmpire.InitializeOnStart(SandboxStartSettings);
				//return false;
            }
			return true;
		}
	}
	//*/

	/*
	[HarmonyPatch(typeof(PresentationPawn))]
	public class CultureUnlock_PresentationPawn
	{

		[HarmonyPrefix]
		[HarmonyPatch(nameof(Initialize))]
		public static bool Initialize(PresentationPawn __instance)
		{
			Diagnostics.LogWarning($"[Gedemon] in PresentationPawn, Initialize for {__instance.name}");
			Diagnostics.Log($"[Gedemon] Transform localScale =  {__instance.Transform.localScale}, gameObject.name =  {__instance.Transform.gameObject.name}, lossyScale =  {__instance.Transform.lossyScale}, childCount =  {__instance.Transform.childCount}");

			__instance.Transform.localScale = new Vector3(0.5f, 0.5f, 0.5f );
			return true;
		}		
	}
	//*/


}
