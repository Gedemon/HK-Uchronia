using Amplitude;
using Amplitude.Framework;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Data.Simulation.Prerequisites;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.Presentation;
using Amplitude.Mercury.Simulation;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Linq;
using UnityEngine;
using static Amplitude.Mercury.Simulation.ConstructibleHelper;
using Diagnostics = Amplitude.Diagnostics;
using Amplitude.Mercury.Fx.HgFx;
using Amplitude.Mercury.Data;

namespace Gedemon.Uchronia
{
    class Districts
    {
        public static IDictionary<StaticString, StaticString> VisualReplacementExtension = new Dictionary<StaticString, StaticString>();
        public static IDictionary<StaticString, StaticString> VisualReplacementBuildingVisualAffinity = new Dictionary<StaticString, StaticString>();

        public static bool TryUpdateExtensionDistrictDefinitionRow(XmlReader reader)
        {
            Diagnostics.LogWarning($"[Gedemon][Districts][TryUpdateExtensionDistrictDefinitionRow]");
            int updatedField = 0;
            if (DatabaseUtils.TryGetAttribute(reader, "Name", out string name))
            {
                StaticString ExtensionName = new StaticString(name);
                ConstructibleDefinition row = ScriptableObject.CreateInstance<ConstructibleDefinition>(); // new EraDefinition();
                var database = Databases.GetDatabase<ConstructibleDefinition>();
                if (database.TryGetValue(ExtensionName, out row))
                {
                    Diagnostics.Log($"[Gedemon][Districts][TryUpdateExtensionDistrictDefinitionRow] Update Row: {name}");
                    ExtensionDistrictDefinition DatabaseRow = row as ExtensionDistrictDefinition;
                    if (DatabaseRow != null)
                    {
                        if (DatabaseUtils.TryGetAttribute(reader, "VisualReplacementKey", out string VisualReplacementKey))
                        {
                            if (!VisualReplacementExtension.ContainsKey(ExtensionName) && !VisualReplacementBuildingVisualAffinity.ContainsKey(ExtensionName))
                            {
                                VisualReplacementExtension.Add(ExtensionName, new StaticString("Extension_" + VisualReplacementKey));
                                VisualReplacementBuildingVisualAffinity.Add(ExtensionName, new StaticString("BuildingVisualAffinity_" + VisualReplacementKey));
                                updatedField++;
                            }
                        }
                        if (DatabaseUtils.TryGetAttribute(reader, "AllowConnectionWithOtherBorough", out string AllowConnectionWithOtherBorough))
                        {
                            DatabaseRow.AllowConnectionWithOtherBorough = AllowConnectionWithOtherBorough == "1" ? true: false;
                            updatedField++;
                        }
                        if (DatabaseUtils.TryGetAttribute(reader, "AllowBoroughExtension", out string AllowBoroughExtension))
                        {
                            DatabaseRow.AllowBoroughExtension = AllowBoroughExtension == "1" ? true : false;
                            updatedField++;
                        }
                        if (DatabaseUtils.TryGetAttribute(reader, "ProvidesSecondaryRoad", out string ProvidesSecondaryRoad))
                        {
                            DatabaseRow.ProvidesSecondaryRoad = ProvidesSecondaryRoad == "1" ? true : false;
                            updatedField++;
                        }
                        if (DatabaseUtils.TryGetAttribute(reader, "ProvidesRail", out string ProvidesRail))
                        {
                            DatabaseRow.ProvidesRail = ProvidesRail == "1" ? true : false;
                            updatedField++;
                        }
                        if (DatabaseUtils.TryGetAttribute(reader, "BoroughExtensionRules", out string BoroughExtensionRules))
                        {
                            if (Enum.IsDefined(typeof(BoroughPrerequisite.BoroughExtensionRules), BoroughExtensionRules))
                            {
                                BoroughPrerequisite.BoroughExtensionRules extensionRules = (BoroughPrerequisite.BoroughExtensionRules)Enum.Parse(typeof(BoroughPrerequisite.BoroughExtensionRules), BoroughExtensionRules);
                                DatabaseRow.BoroughPrerequisite = new BoroughPrerequisite { ExtensionRules = extensionRules, HideInUI = false };
                                updatedField++;
                            }
                        }
                        if (DatabaseUtils.TryGetAttributeWithoutSpace(reader, "NeighbourPrerequisiteDistricts", out string NeighbourPrerequisiteDistricts))
                        {
                            NeighbourTilesPrerequisite.Operators tagOperatorneighbour = NeighbourTilesPrerequisite.Operators.AnyTile;
                            if (DatabaseUtils.TryGetAttribute(reader, "NeighbourTilesPrerequisiteOperator", out string NeighbourTilesPrerequisiteOperator))
                            {
                                if (Enum.IsDefined(typeof(NeighbourTilesPrerequisite.Operators), NeighbourTilesPrerequisiteOperator))
                                {
                                    tagOperatorneighbour = (NeighbourTilesPrerequisite.Operators)Enum.Parse(typeof(NeighbourTilesPrerequisite.Operators), NeighbourTilesPrerequisiteOperator);
                                }
                            }

                            DistrictPrerequisite.Operators tagOperatorDistrict = DistrictPrerequisite.Operators.Any;
                            if (DatabaseUtils.TryGetAttribute(reader, "NeighbourPrerequisiteDistrictsOperator", out string NeighbourPrerequisiteDistrictsOperator))
                            {
                                if (Enum.IsDefined(typeof(DistrictPrerequisite.Operators), NeighbourPrerequisiteDistrictsOperator))
                                {
                                    tagOperatorDistrict = (DistrictPrerequisite.Operators)Enum.Parse(typeof(DistrictPrerequisite.Operators), NeighbourPrerequisiteDistrictsOperator);
                                }
                            }
                            DatabaseRow.NeighbourTilesPrerequisite.DistrictPrerequisite = new DistrictPrerequisite { TagOperator = tagOperatorDistrict, serializableTags = NeighbourPrerequisiteDistricts.Split(','), HideInUI = false };
                            DatabaseRow.NeighbourTilesPrerequisite.NeighbourOperator = tagOperatorneighbour;
                            DatabaseRow.NeighbourTilesPrerequisite.DistrictPrerequisite.InitializeStaticStrings();
                            updatedField++;
                        }
                        if (DatabaseUtils.TryGetAttribute(reader, "CampConstraint", out string CampConstraint))
                        {
                            if (Enum.IsDefined(typeof(SettlementStatusPrerequisite.SettlementStatusConstraints), CampConstraint))
                            {
                                DatabaseRow.SettlementStatusPrerequisite.CampConstraint = (SettlementStatusPrerequisite.SettlementStatusConstraints)Enum.Parse(typeof(SettlementStatusPrerequisite.SettlementStatusConstraints), CampConstraint);
                                updatedField++;
                            }
                        }
                    }
                }
            }
            return updatedField > 0;
        }

        public static bool TryUpdateExploitationRuleDefinitionRow(XmlReader reader, out ExploitationRuleDefinition DatabaseRow)
        {
            int updatedField = 0;
            DatabaseRow = ScriptableObject.CreateInstance<ExploitationRuleDefinition>(); // new EraDefinition();
            if (DatabaseUtils.TryGetAttribute(reader, "Name", out string name))
            {
                // we're updating the DB directly
                var database = Databases.GetDatabase<ExploitationRuleDefinition>();
                if (database.TryGetValue(new StaticString(name), out DatabaseRow))
                {

                }
            }
            return updatedField > 0;
        }

        public static void TestUpdate()
        {
            Diagnostics.Log($"[Gedemon][Districts][TestUpdate]");

            // In case we want to do changes in multiple entries
            {
                var database = Databases.GetDatabase<ConstructibleDefinition>();
                foreach (var data in database)
                {
                    ExtensionDistrictDefinition DatabaseRow = data as ExtensionDistrictDefinition;
                    if (DatabaseRow != null)
                    {
                        switch (DatabaseRow.Name.ToString())
                        {
                            case "Extension_Base_TrainStation":
                                break;
                        }


                        switch (DatabaseRow.Category)
                        {
                            case ConstructibleCategory.Industry:
                                break;
                        }
                    }
                }
            }

            {
                // bug fix
                var database = Databases.GetDatabase<Amplitude.Mercury.EffectMapper.DescriptorMapper>();
                if (database.TryGetValue(new StaticString("Effect_Extension_Base_TrainStation"), out var row))
                {
                    row.LocalizedName = "%Effect_Extension_Base_TrainStation";
                }
            }
        }

        public static void UpdateDistrictDB()
        {
            TestUpdate();
        }

        public static int GetMaxPopulationLimitedDistricts(Settlement settlement)
        {
            int freeDistricts = DatabaseUtils.FreePopulationDistrict;
            float districtPerPopulation = DatabaseUtils.DistrictPerPopulation; // number of district that can be placed per population size
            //Diagnostics.Log($"[Gedemon][Districts][GetMaxPopulationLimitedDistricts] ExtensionDistrictsCount = {settlement.ExtensionDistrictsCount.Value}, Population = {settlement.Population.Value}, limit = {settlement.Population.Value} * {districtPerPopulation} + {freeDistricts} = {settlement.Population.Value * districtPerPopulation + freeDistricts}");
            return (int)(settlement.Population.Value * districtPerPopulation) + freeDistricts;
        }

        public static int GetPopulationLimitedDistrictCount(Settlement settlement)
        {
            int count = 0;
            for(int i = 0; i < settlement.Districts.Count; i ++)
            {
                ExtensionDistrictDefinition extensionDistrictDefinition = settlement.Districts[i].DistrictDefinition as ExtensionDistrictDefinition;
                if(extensionDistrictDefinition?.Category != null)
                {
                    if(IsPopulationLimited(extensionDistrictDefinition))
                      count++;
                }
            }
            return count;
        }

        public static bool IsPopulationLimited(ExtensionDistrictDefinition extensionDistrictDefinition)
        {
            switch(extensionDistrictDefinition.Category)
            {
                case ConstructibleCategory.Faith:
                case ConstructibleCategory.None:
                case ConstructibleCategory.Resource:
                    return false;
                case ConstructibleCategory.City:
                    // Common Quarter is city but requires Population
                    if (extensionDistrictDefinition.name == "Extension_Base_PublicOrder")
                    {
                        return true;
                    }
                    return false;
                case ConstructibleCategory.Money:
                    // harbor and Airport doesn't require Population
                    if(extensionDistrictDefinition.name == "Extension_Base_Harbour" || extensionDistrictDefinition.name == "Extension_Base_Airport")
                    {
                        return false;
                    }
                    break; // other "Money" require Population
                case ConstructibleCategory.Military:
                    // Garrison require Population (to prevent spamming), but not any of the others
                    if (extensionDistrictDefinition.name != "Extension_Base_Military")
                    {
                        return false;
                    }
                    break;
                case ConstructibleCategory.Industry:
                    // Train Station doesn't require Population 
                    if (extensionDistrictDefinition.name == "Extension_Base_TrainStation")
                    {
                        return false;
                    }
                    break; // other "Industry" require Population
            }
            return true;
        }

        //[HarmonyPatch(typeof(District))]
        public class District_patch
        {

            [HarmonyPrefix]
            [HarmonyPatch(nameof(OnRefreshed))]
            public static bool OnRefreshed(District __instance) // 
            {

                return true;
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(OnRefreshed))]
            public static void OnRefreshedPost(District __instance) // 
            {

            }
        }

        [HarmonyPatch(typeof(ConstructibleHelper))]
        public class ConstructibleHelper_Patch
        {
            // to do : allow project to renove city center (update graphic)

            /*
            [HarmonyPatch("CheckPrerequisite")] // every constructible
            [HarmonyPatch(new Type[] { typeof(SettlementStatusPrerequisite), typeof(Settlement) })]
            [HarmonyPrefix]
            public static bool CheckPrerequisite(ref StatusPrerequisiteFailureFlags __result, SettlementStatusPrerequisite settlementStatusPrerequisite, Settlement settlement)
            {
                Diagnostics.LogError($"[Gedemon][Districts][CheckPrerequisite] StatusPrerequisiteFailureFlags = {__result}, settlement.ExtensionDistrictsCount.Value = {settlement.ExtensionDistrictsCount.Value}");
                int freeDistricts = 2;
                if(settlement.ExtensionDistrictsCount.Value > (settlement.Population.Value / 2) + freeDistricts)
                {
                    __result = StatusPrerequisiteFailureFlags.WrongSettlementStatus;
                    return false;
                }
                return true;
            }
            //*/

            //public static bool CheckTerritoryUnicityPrerequisite(ConstructibleDefinition constructible, Settlement settlement, int territoryIndex, bool checkAgainstConstruction = true) // <- better ? or keep tile and check if replacing to allow
            // public static ConstructionFailureFlags CheckPrerequisite(ConstructibleActionDefinition constructibleActionDefinition, Settlement settlement, int tileIndex, int constructionIndex = -1)
            //private static bool CheckTileValidity(int tileIndex, Empire empire, Settlement settlement, ExtensionDistrictDefinition extensionDistrictDefinition, bool needNeighbourCheck)


            [HarmonyPatch("FillValidPositionInSettlement")]
            [HarmonyPostfix]
            public static void FillValidPositionInSettlement(ExtensionDistrictDefinition extensionDistrictDefinition, Settlement settlement, CustomTileIndexArea validTileIndexes)
            {
                List<int> listInvalidIndexes = new List<int>();

                if (IsPopulationLimited(extensionDistrictDefinition) && Districts.GetPopulationLimitedDistrictCount(settlement) >= Districts.GetMaxPopulationLimitedDistricts(settlement))
                {
                    for (int i = 0; i < validTileIndexes.TileIndexesCount; i++)
                    {
                        int tileIndex = validTileIndexes.TileIndexes[i];
                        District districtAt = settlement.GetDistrictAt(tileIndex);
                        if (districtAt != null)
                        {
                            if (districtAt.DistrictType == DistrictTypes.Extension)
                            {
                                //Diagnostics.Log($"[Gedemon][Districts][CheckPrerequisite] keep tileIndex = {tileIndex} ({districtAt.DistrictDefinition.Name})");
                                continue; // using vanilla checks (don't check vs existing number if we're replacing)
                            }
                        }
                        //Diagnostics.Log($"[Gedemon][Districts][CheckPrerequisite] Add tileIndex to remove = {tileIndex}");
                        listInvalidIndexes.Add(tileIndex);
                    }
                    foreach (int tileIndex in listInvalidIndexes)
                    {
                        //Diagnostics.Log($"[Gedemon][Districts][CheckPrerequisite] Remove tileIndex = {tileIndex}");
                        for (int i = 0; i < validTileIndexes.TileIndexesCount; i++)
                        {
                            if(validTileIndexes.TileIndexes[i] == tileIndex)
                            {
                                //Diagnostics.Log($"[Gedemon][Districts][CheckPrerequisite] Found at i = {i}");
                                validTileIndexes.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
                //Diagnostics.LogWarning($"[Gedemon][Districts][CheckPrerequisite] valid tileIndexes");
                //foreach (int tileIndex in validTileIndexes.TileIndexes)
                //{
                //    Diagnostics.Log($"[Gedemon][Districts][CheckPrerequisite] tileIndex = {tileIndex}");
                //}

            }

            /*
            [HarmonyPatch("CheckTilePrerequisite")]
            [HarmonyPrefix]
            public static bool CheckTilePrerequisite(ref bool __result, DistrictDefinition districtDefinition, Settlement settlement, int tileIndex, CheckPrerequisiteOptions options = CheckPrerequisiteOptions.None)
            {

                District districtAt = settlement.GetDistrictAt(tileIndex);
                if (districtAt != null)
                {
                    return true; // continue with vanilla checks (don't check vs existing number if we're replacing)
                }

                int freeDistricts = 2;
                float districtPerPopulation = 0.5f; // number of district that can be placed per population size

                Diagnostics.LogError($"[Gedemon][Districts][CheckPrerequisite] StatusPrerequisiteFailureFlags = {__result}, ExtensionDistrictsCount = {settlement.ExtensionDistrictsCount.Value}, Population = {settlement.Population.Value}, limit = {settlement.Population.Value * districtPerPopulation + freeDistricts}");

                if (!(districtDefinition.Category == ConstructibleCategory.Resource || districtDefinition.Category == ConstructibleCategory.Faith) && settlement.ExtensionDistrictsCount.Value > (settlement.Population.Value * districtPerPopulation) + freeDistricts)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
            //*/

            /*
            [HarmonyPatch("CheckTileValidity")]
            [HarmonyPrefix]
            public static bool CheckTileValidity(ref bool __result, int tileIndex, Empire empire, Settlement settlement, ExtensionDistrictDefinition extensionDistrictDefinition, bool needNeighbourCheck)//CheckTilePrerequisite(ref bool __result, DistrictDefinition districtDefinition, Settlement settlement, int tileIndex, CheckPrerequisiteOptions options = CheckPrerequisiteOptions.None)
            {

                District districtAt = settlement.GetDistrictAt(tileIndex);
                if (districtAt != null)
                {
                    return true; // continue with vanilla checks (don't check vs existing number if we're replacing)
                }

                int freeDistricts = 2;
                float districtPerPopulation = 0.5f; // number of district that can be placed per population size

                Diagnostics.LogError($"[Gedemon][Districts][CheckPrerequisite] StatusPrerequisiteFailureFlags = {__result}, ExtensionDistrictsCount = {settlement.ExtensionDistrictsCount.Value}, Population = {settlement.Population.Value}, limit = {settlement.Population.Value * districtPerPopulation + freeDistricts}");

                if (!(extensionDistrictDefinition.Category == ConstructibleCategory.Resource || extensionDistrictDefinition.Category == ConstructibleCategory.Faith) && settlement.ExtensionDistrictsCount.Value > (settlement.Population.Value * districtPerPopulation) + freeDistricts)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
            //*/

        }

        // to do: try to move to PresentationEntityFactoryController_Patch
        [HarmonyPatch(typeof(PresentationDistrict))]
        public class PresentationDistrict_Patch
        {

            [HarmonyPrefix]
            [HarmonyPatch(nameof(UpdateFromDistrictInfo))]
            public static bool UpdateFromDistrictInfo(PresentationDistrict __instance, ref DistrictInfo districtInfo, bool isStartOrEmpireChange, bool isNewDistrict)
            {

                if (!Uchronia.KeepHistoricalDistricts())
                    return true;

                if (districtInfo.DistrictType == DistrictTypes.Exploitation)
                {
                    return true;
                }
                if (isNewDistrict || districtInfo.DistrictDefinitionName == DepartmentOfTheInterior.CityCenterDistrictDefinitionName)
                {
                    if (CurrentGame.Data.HistoricVisualAffinity.TryGetValue(districtInfo.TileIndex, out DistrictVisual cachedVisualAffinity))
                    {
                        //Diagnostics.LogError($"[Gedemon] UpdateFromDistrictInfo {districtInfo.DistrictDefinitionName}, isNewDistrict = {isNewDistrict}), TileIndex = {districtInfo.TileIndex}, VisualAffinityName = {districtInfo.VisualAffinityName}, InitialVisualAffinityName = {districtInfo.InitialVisualAffinityName}, cached = ({cachedVisualAffinity.VisualAffinity}) ");

                    }
                    if (isNewDistrict)
                    {
                        if (CurrentGame.Data.HistoricVisualAffinity.ContainsKey(districtInfo.TileIndex))
                        {
                            //CurrentGame.Data.HistoricVisualAffinity.Remove(districtInfo.TileIndex); // need to clean somewhere else, "isNewDistrict" is true on capture)
                        }
                        ref TileInfo reference = ref Amplitude.Mercury.Sandbox.Sandbox.World.TileInfo.Data[districtInfo.TileIndex];
                        Diagnostics.LogWarning($"[Gedemon] UpdateFromDistrictInfo (new district)  {districtInfo.DistrictDefinitionName}, TileIndex = {districtInfo.TileIndex} ({CultureUnlock.GetTerritoryName(reference.TerritoryIndex)}), VisualAffinityName = {districtInfo.VisualAffinityName}, InitialVisualAffinityName = {districtInfo.InitialVisualAffinityName}");
                    }
                    return true;
                }
                else
                {

                    if (CurrentGame.Data.HistoricVisualAffinity.TryGetValue(districtInfo.TileIndex, out DistrictVisual historicDistrict) && districtInfo.VisualAffinityName != historicDistrict.VisualAffinity)
                    {
                        //Uchronia.Log($"[Gedemon] UpdateFromDistrictInfo districtInfo = {districtInfo.VisualAffinityName}, isStartOrEmpireChange = {isStartOrEmpireChange}, isNewDistrict = {isNewDistrict})");

                        ref TileInfo reference = ref Amplitude.Mercury.Sandbox.Sandbox.World.TileInfo.Data[districtInfo.TileIndex];
                        districtInfo.VisualAffinityName = historicDistrict.VisualAffinity;
                        districtInfo.InitialVisualAffinityName = historicDistrict.VisualAffinity;
                        districtInfo.EraIndex = historicDistrict.EraIndex;

                    }
                }

                return true;
            }
        }

        // Use Custom Unique Visual on Generic Districts
        [HarmonyPatch(typeof(PresentationEntityFactoryController))]
        public class PresentationEntityFactoryController_Patch
        {

            [HarmonyPrefix]
            [HarmonyPatch(nameof(SynchronizeDistrictInfo))]
            public static bool SynchronizeDistrictInfo(PresentationEntityFactoryController __instance)
            {

                if ((Snapshots.GameSnapshot.PresentationData.CameraSequenceScope & CameraSequenceScopeType.District) != 0 && __instance.lastFrame > 0)
                {
                    return false;
                }
                ref ArrayWithFrame<DistrictInfo> localEmpireDistrictInfo = ref Snapshots.GameSnapshot.PresentationData.LocalEmpireInfo.DistrictInfo;
                if (!__instance.districtSynchronisationNotFinished && localEmpireDistrictInfo.Frame < __instance.lastFrame && Snapshots.GameSnapshot.PresentationData.LocalEmpireInfo.EmpireIndex == __instance.lastEmpireIndex)
                {
                    return false;
                }
                bool isStartOrEmpireChange = __instance.lastFrame == -1 || Snapshots.GameSnapshot.PresentationData.LocalEmpireInfo.EmpireIndex != __instance.lastEmpireIndex;
                int length = localEmpireDistrictInfo.Length;
                __instance.districtPresentationEntities.ResizeIFN(length);
                int num = 0;
                for (int i = 0; i < length; i++)
                {
                    if (num >= PresentationEntityFactoryController.MaxDistrictChangePerFrame)
                    {
                        break;
                    }
                    ref DistrictInfo districtInfo = ref localEmpireDistrictInfo.Data[i];

                    // Gedemon <<<<<
                    if (VisualReplacementExtension.TryGetValue(districtInfo.DistrictDefinitionName, out StaticString replacementExtension) && VisualReplacementBuildingVisualAffinity.TryGetValue(districtInfo.DistrictDefinitionName, out StaticString replacementVisualAffinity))
                    //if (districtInfo.DistrictDefinitionName.ToString() == "Extension_Base_HeavyIndustry") // || districtInfo.DistrictDefinitionName.ToString() == "Extension_Era1_Nubia")
                    {

                        var database = Databases.GetDatabase<ConstructibleDefinition>();
                        if (database.TryGetValue(replacementExtension, out var constructibleDefinition))
                        {
                            ExtensionDistrictDefinition districtDefinition = constructibleDefinition as ExtensionDistrictDefinition;
                            if(districtDefinition !=null)
                            {
                                //Uchronia.Log($"[Gedemon][PresentationEntityFactoryController] SynchronizeDistrictInfo : VisualReplacementExtension for DistrictDefinition = {replacementExtension}, VisualReplacementBuildingVisualAffinity = {replacementVisualAffinity}");
                                //Uchronia.Log($"[Gedemon][PresentationEntityFactoryController] SynchronizeDistrictInfo : districtInfo.DistrictDefinitionName = {districtInfo.DistrictDefinitionName}, districtInfo.VisualLevel = {districtInfo.VisualLevel}, DistrictDefinition.AdditionalVisualLevels.Length = {districtDefinition.AdditionalVisualLevels.Length}");
                                //Uchronia.Log($"[Gedemon][PresentationEntityFactoryController] SynchronizeDistrictInfo : districtInfo.VisualAffinityName = {districtInfo.VisualAffinityName}, districtInfo.InitialVisualAffinityName = {districtInfo.InitialVisualAffinityName}, DistrictDefinition.ConstructibleVisualAffinity = {districtDefinition.ConstructibleVisualAffinity.ElementName}");

                                districtInfo.DistrictDefinitionName = replacementExtension;
                                districtInfo.VisualLevel = districtInfo.VisualLevel > districtDefinition.AdditionalVisualLevels.Length ? districtDefinition.AdditionalVisualLevels.Length : districtInfo.VisualLevel;
                                districtInfo.InitialVisualAffinityName = replacementVisualAffinity;
                                //DatabaseUtils.ShowObject(districtInfo);
                            }
                        }
                    }
                    // Gedemon >>>>>


                    ref PresentationEntityAccess<PresentationDistrict>.EntityAccess reference2 = ref __instance.districtPresentationEntities.Content[i];
                    PresentationDistrict entity = reference2.Entity;
                    if (districtInfo.PoolAllocationIndex < 0 || districtInfo.SimulationEntityGUID == 0L || districtInfo.HasConstructionOver)
                    {
                        if (entity != null)
                        {
                            if ((ulong)entity.SettlementGUID >= 0)
                            {
                                __instance.GetPresentationSettlement(entity.SettlementGUID)?.UnregisterDistrict(entity);
                            }
                            reference2.ReleaseEntity(PresentationEntity.ReleasePolicy.Default);
                        }
                        continue;
                    }
                    bool isNewDistrict = districtInfo.ConstructionFrame >= __instance.lastFrame;
                    if (entity == null)
                    {
                        entity = PresentationDistrict.CreatePresentationDistrict(ref districtInfo, __instance.entityFactoryTransform, isStartOrEmpireChange, isNewDistrict);
                        reference2.Init(entity);
                        if ((ulong)entity.SettlementGUID >= 0)
                        {
                            __instance.GetPresentationSettlement(entity.SettlementGUID)?.RegisterDistrict(entity);
                        }
                        num++;
                        continue;
                    }
                    bool num2 = districtInfo.SettlementSimulationEntityGUID != entity.SettlementGUID;
                    if (num2)
                    {
                        __instance.GetPresentationSettlement(entity.SettlementGUID)?.UnregisterDistrict(entity);
                    }
                    bool flag = entity.UpdateFromDistrictInfo(ref districtInfo, isStartOrEmpireChange, isNewDistrict);
                    reference2.TileIndex = districtInfo.TileIndex;
                    reference2.Guid = districtInfo.SimulationEntityGUID;
                    if (num2)
                    {
                        __instance.GetPresentationSettlement(entity.SettlementGUID)?.RegisterDistrict(entity);
                    }
                    num += (flag ? 1 : 0);
                }
                __instance.districtSynchronisationNotFinished = num >= PresentationEntityFactoryController.MaxDistrictChangePerFrame;

                return false;
            }
        }

    }

}
