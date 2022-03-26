using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Amplitude;
using Amplitude.Framework;
using Amplitude.Framework.Localization;
using Amplitude.Mercury;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Simulation;
using Amplitude.UI;
using UnityEngine;
using Diagnostics = Amplitude.Diagnostics;

namespace Gedemon.Uchronia
{
    class DatabaseUtils
    {
        static List<int> GetListIntFromString(string text)
        {
            string[] elements = text.Split(',');
            List<int> listValues = new List<int>();
            for(int i = 0; i < elements.Length; i++)
            {
                if(int.TryParse(elements[i], out int value))
                {
                    listValues.Add(value);
                }
            }
            return listValues;
        }

        static bool TryGetAttribute(XmlReader reader, string attribute, out string result)
        {
            result = reader.GetAttribute(attribute);
            return result != null;
        }

        static bool TryGetFactionTerritoriesRow(XmlReader reader, out string civilization, out List<int> territories)
        {
            civilization = reader.GetAttribute("Civilization");
            territories = null;
            if(TryGetAttribute(reader, "Territories", out string territoriesAttribute))
            {
                territories = GetListIntFromString(territoriesAttribute);
            }
            return (civilization != null && territories != null);
        }
        static bool TryGetIndexNameRow(XmlReader reader, out string name, out int index)
        {
            name = reader.GetAttribute("Name");
            index = -1;
            if (TryGetAttribute(reader, "Index", out string indexAttribute))
            {
                index = int.Parse(indexAttribute);
            }
            return (name != null && index != -1);
        }
        static bool TryGetIndexPositionRow(XmlReader reader, out int index, out Hexagon.OffsetCoords position)
        {
            position = new Hexagon.OffsetCoords(-1,-1);
            index = -1;
            if (TryGetAttribute(reader, "Index", out string indexAttribute))
            {
                index = int.Parse(indexAttribute);
            }
            if (TryGetAttribute(reader, "X", out string xAttribute) && TryGetAttribute(reader, "Y", out string yAttribute))
            {
                if(int.TryParse(xAttribute, out int x) && int.TryParse(yAttribute, out int y))
                {
                    position = new Hexagon.OffsetCoords(x, y);
                }
            }
            return (position.Column != -1 && position.Row != -1 && index != -1);
        }
        static bool TryGetCityMapRow(XmlReader reader, out CityPosition cityPosition)
        {
            cityPosition = new CityPosition();
            if (TryGetAttribute(reader, "Tag", out string localizationKey))
            {
                if (TryGetAttribute(reader, "X", out string xAttribute) && TryGetAttribute(reader, "Y", out string yAttribute))
                {
                    if (int.TryParse(xAttribute, out int x) && int.TryParse(yAttribute, out int y))
                    {
                        cityPosition.Name = localizationKey;
                        cityPosition.Column = x;
                        cityPosition.Row = y;
                        return true;
                    }
                }
            }
            return false;
        }

        static bool TryGetUIMapperRow(XmlReader reader, out UIMapper UIMapperRow)
        {
            int updatedField = 0;
            UIMapperRow = ScriptableObject.CreateInstance<UIMapper>(); // new UIMapper();
            if (TryGetAttribute(reader, "Name", out string name))
            {
                // mandatory field
                if (TryGetAttribute(reader, "Title", out string Title)) 
                {
                    UIMapperRow.Name = new StaticString(name);
                    UIMapperRow.Title = Title;
                    updatedField++;
                }
                else
                {
                    return false;
                }
                // optional fields
                if (TryGetAttribute(reader, "Description", out string Description))
                {
                    UIMapperRow.Description = Description;
                    updatedField++;
                }
            }
            return updatedField > 0; ;
        }
        static bool TryGetEraDefinitionRow(XmlReader reader, out EraDefinition DatabaseRow)
        {
            int updatedField = 0;
            DatabaseRow = ScriptableObject.CreateInstance<EraDefinition>(); // new EraDefinition();
            if (TryGetAttribute(reader, "Name", out string name))
            {
                // we're updating the DB directly
                var database = Databases.GetDatabase<EraDefinition>();
                if (database.TryGetValue(new StaticString(name), out DatabaseRow))
                {
                    if (TryGetAttribute(reader, "EraStarsCountEvolutionRequirement", out string EraStarsCountEvolutionRequirement))
                    {
                        if(float.TryParse(EraStarsCountEvolutionRequirement, out float eraStars))
                        {
                            DatabaseRow.EraStarsCountEvolutionRequirement = (FixedPoint)eraStars;
                            updatedField++;
                        }
                    }
                }
            }
            return updatedField > 0;
        }
        static bool TryGetGameSpeedDefinitionRow(XmlReader reader, out GameSpeedDefinition DatabaseRow)
        {
            int updatedField = 0;
            DatabaseRow = ScriptableObject.CreateInstance<GameSpeedDefinition>(); // new GameSpeedDefinition();
            if (TryGetAttribute(reader, "Name", out string name))
            {
                var database = Databases.GetDatabase<GameSpeedDefinition>();
                if (database.TryGetValue(new StaticString(name), out DatabaseRow))
                {
                    if (TryGetAttribute(reader, "EndGameTurnLimitMultiplier", out string EndGameTurnLimitMultiplier))
                    {
                        if (float.TryParse(EndGameTurnLimitMultiplier, out float endTurnMultiplier))
                        {
                            DatabaseRow.EndGameTurnLimitMultiplier = endTurnMultiplier;
                            updatedField++;
                        }
                    }
                }
            }
            return updatedField > 0;
        }

        public static IDictionary<string, string> TranslationTable = new Dictionary<string, string>();
        public static IDictionary<string, string> ModDefines = new Dictionary<string, string>();
        public static List<UIMapper> UIMapperList = new List<UIMapper>();
        public static List<EraDefinition> EraDefinitionList = new List<EraDefinition>();
        public static List<GameSpeedDefinition> GameSpeedDefinitionList = new List<GameSpeedDefinition>();

        static readonly List<string> SupportedTablesXML = new List<string> { "ModDefines", "EraDefinition", "GameSpeedDefinition", "UIMapper", "CivilizationCityAliases", "LocalizedText", "CityMap", "MajorEmpireTerritories", "MajorEmpireCoreTerritories", "MinorFactionTerritories", "ExtraPositions", "ExtraPositionsNewWorld", "ContinentNames", "TerritoryNames", "NoCapital", "NomadCultures" };
        public static void LoadXML(string input, string provider, bool inputIsText = false)
        {
            XmlReader xmlReader;

            if(inputIsText)
            {
                xmlReader = XmlReader.Create(new System.IO.StringReader(input));
            }
            else
            {
                xmlReader = XmlReader.Create(input);
            }

            IList<MapTCL> moddedTCL = new List<MapTCL>();
            MapTCL currentMapTCL = null;
            string currentMapName = null;

            string currentTable = null;
            while (xmlReader.Read())
            {
                if ((xmlReader.NodeType == XmlNodeType.Element))
                {
                    switch(xmlReader.Name)
                    {
                        case "Map":
                            TryGetAttribute(xmlReader, "Name", out currentMapName);
                            Diagnostics.LogWarning($"[Gedemon] [LoadXML] [Element] Switch current Map (Name = {currentMapName})");
                            if (TryGetAttribute(xmlReader, "MapTerritoryHash", out string hashList))
                            {
                                List<int> mapTerritoryHash = GetListIntFromString(hashList);
                                if (currentMapTCL != null)
                                {
                                    moddedTCL.Add(currentMapTCL);
                                }
                                currentMapTCL = new MapTCL { MapTerritoryHash = mapTerritoryHash };
                                if (TryGetAttribute(xmlReader, "LoadOrder", out string loadOrderAttribute))
                                {
                                    int loadOrder = int.Parse(loadOrderAttribute);
                                    currentMapTCL.LoadOrder = loadOrder;
                                }
                            }
                            else
                            {
                                Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't initialize MapTCL, missing attribute (currentMapName = {currentMapName}) (MapTerritoryHash = {hashList})");
                            }
                            break;
                    }

                    if (SupportedTablesXML.Contains(xmlReader.Name))
                    {
                        Diagnostics.LogWarning($"[Gedemon] [LoadXML] [Element] Switch current Table to {xmlReader.Name}");
                        currentTable = xmlReader.Name;
                    }

                    if(currentTable != null)
                    {
                        if (xmlReader.HasAttributes)
                        {
                            bool rowLoaded = false;
                            switch (currentTable)
                            {
                                #region MapTCL
                                case "MajorEmpireTerritories":
                                    if (currentMapTCL != null)
                                    {
                                        if (currentMapTCL.MajorEmpireTerritories == null)
                                            currentMapTCL.MajorEmpireTerritories = new Dictionary<string, List<int>>();

                                        if (TryGetFactionTerritoriesRow(xmlReader, out string civilization, out List<int> territories) && !currentMapTCL.MajorEmpireTerritories.ContainsKey(civilization))
                                        {
                                            currentMapTCL.MajorEmpireTerritories.Add(civilization, territories);
                                            rowLoaded = true;
                                        }
                                    }
                                    else
                                    {
                                        Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't register row (current table = {currentTable}): MapTCL is not initialized");
                                    }
                                    break;
                                case "MajorEmpireCoreTerritories":
                                    if (currentMapTCL != null)
                                    {
                                        if (currentMapTCL.MajorEmpireCoreTerritories == null)
                                            currentMapTCL.MajorEmpireCoreTerritories = new Dictionary<string, List<int>>();

                                        if (TryGetFactionTerritoriesRow(xmlReader, out string civilization, out List<int> territories) && !currentMapTCL.MajorEmpireCoreTerritories.ContainsKey(civilization))
                                        {
                                            currentMapTCL.MajorEmpireCoreTerritories.Add(civilization, territories);
                                            rowLoaded = true;
                                        }
                                    }
                                    else
                                    {
                                        Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't register row (current table = {currentTable}): MapTCL is not initialized");
                                    }
                                    break;
                                case "MinorFactionTerritories":
                                    if (currentMapTCL != null)
                                    {
                                        if (currentMapTCL.MinorFactionTerritories == null)
                                            currentMapTCL.MinorFactionTerritories = new Dictionary<string, List<int>>();

                                        if (TryGetFactionTerritoriesRow(xmlReader, out string civilization, out List<int> territories) && !currentMapTCL.MinorFactionTerritories.ContainsKey(civilization))
                                        {
                                            currentMapTCL.MinorFactionTerritories.Add(civilization, territories);
                                            rowLoaded = true;
                                        }
                                    }
                                    else
                                    {
                                        Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't register row (current table = {currentTable}): MapTCL is not initialized");
                                    }
                                    break;
                                case "ContinentNames":
                                    if (currentMapTCL != null)
                                    {
                                        if (currentMapTCL.ContinentNames == null)
                                            currentMapTCL.ContinentNames = new Dictionary<int, string>();

                                        if (TryGetIndexNameRow(xmlReader, out string name, out int index) && !currentMapTCL.ContinentNames.ContainsKey(index))
                                        {
                                            currentMapTCL.ContinentNames.Add(index, name);
                                            rowLoaded = true;
                                        }
                                    }
                                    else
                                    {
                                        Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't register row (current table = {currentTable}): MapTCL is not initialized");
                                    }
                                    break;
                                case "TerritoryNames":
                                    if (currentMapTCL != null)
                                    {
                                        if (currentMapTCL.TerritoryNames == null)
                                            currentMapTCL.TerritoryNames = new Dictionary<int, string>();

                                        if (TryGetIndexNameRow(xmlReader, out string name, out int index) && !currentMapTCL.TerritoryNames.ContainsKey(index))
                                        {
                                            currentMapTCL.TerritoryNames.Add(index, name);
                                            rowLoaded = true;
                                        }
                                    }
                                    else
                                    {
                                        Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't register row (current table = {currentTable}): MapTCL is not initialized");
                                    }
                                    break;
                                case "ExtraPositions":
                                    if (currentMapTCL != null)
                                    {
                                        if (currentMapTCL.ExtraPositions == null)
                                            currentMapTCL.ExtraPositions = new Dictionary<int, Hexagon.OffsetCoords>();

                                        if (TryGetIndexPositionRow(xmlReader, out int index, out Hexagon.OffsetCoords position) && !currentMapTCL.ExtraPositions.ContainsKey(index))
                                        {
                                            currentMapTCL.ExtraPositions.Add(index, position);
                                            rowLoaded = true;
                                        }
                                    }
                                    else
                                    {
                                        Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't register row (current table = {currentTable}): MapTCL is not initialized");
                                    }
                                    break;
                                case "ExtraPositionsNewWorld":
                                    if (currentMapTCL != null)
                                    {
                                        if (currentMapTCL.ExtraPositionsNewWorld == null)
                                            currentMapTCL.ExtraPositionsNewWorld = new Dictionary<int, Hexagon.OffsetCoords>();

                                        if (TryGetIndexPositionRow(xmlReader, out int index, out Hexagon.OffsetCoords position) && !currentMapTCL.ExtraPositionsNewWorld.ContainsKey(index))
                                        {
                                            currentMapTCL.ExtraPositionsNewWorld.Add(index, position);
                                            rowLoaded = true;
                                        }
                                    }
                                    else
                                    {
                                        Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't register row (current table = {currentTable}): MapTCL is not initialized");
                                    }
                                    break;
                                case "NoCapital":
                                    if (currentMapTCL != null)
                                    {
                                        if (currentMapTCL.NoCapital == null)
                                            currentMapTCL.NoCapital = new List<string>();

                                        string civilization = xmlReader.GetAttribute("Civilization");
                                        if (civilization != null && !currentMapTCL.NoCapital.Contains(civilization))
                                        {
                                            currentMapTCL.NoCapital.Add(civilization);
                                            rowLoaded = true;
                                        }
                                    }
                                    else
                                    {
                                        Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't register row (current table = {currentTable}): MapTCL is not initialized");
                                    }
                                    break;
                                case "NomadCultures":
                                    if (currentMapTCL != null)
                                    {
                                        if (currentMapTCL.NomadCultures == null)
                                            currentMapTCL.NomadCultures = new List<string>();

                                        string civilization = xmlReader.GetAttribute("Civilization");
                                        if (civilization != null && !currentMapTCL.NomadCultures.Contains(civilization))
                                        {
                                            currentMapTCL.NomadCultures.Add(civilization);
                                            rowLoaded = true;
                                        }
                                    }
                                    else
                                    {
                                        Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't register row (current table = {currentTable}): MapTCL is not initialized");
                                    }
                                    break;
                                case "CityMap":
                                    if (currentMapTCL != null)
                                    {
                                        if (currentMapTCL.CityMap == null)
                                            currentMapTCL.CityMap = new List<CityPosition>();

                                        if (TryGetCityMapRow(xmlReader, out CityPosition cityPosition))
                                        {
                                            currentMapTCL.CityMap.Add(cityPosition);
                                            rowLoaded = true;
                                        }
                                    }
                                    else
                                    {
                                        Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't register row (current table = {currentTable}): MapTCL is not initialized");
                                    }
                                    break;
                                #endregion
                                case "CivilizationCityAliases":
                                    if (TryGetAttribute(xmlReader, "Civilization", out string Civilization) && TryGetAttribute(xmlReader, "Aliases", out string Aliases) && !TranslationTable.ContainsKey(Civilization))
                                    {                                        
                                        CityMap.CivilizationAliases.Add(Civilization, Aliases.Split(',').ToList());
                                        rowLoaded = true;
                                    }
                                    break;
                                case "LocalizedText":
                                    if (TryGetAttribute(xmlReader, "Tag", out string Tag) && TryGetAttribute(xmlReader, "Text", out string Text))
                                    {
                                        if (!TranslationTable.ContainsKey(Tag))
                                        {
                                            TranslationTable.Add(Tag, Text);
                                            rowLoaded = true;
                                        }
                                        else
                                        {
                                            //Diagnostics.LogError($"[Gedemon] [LoadXML] Can't add LocalizedText Row with Tag = {Tag}, Text = {Text}), Tag already added with Text = {TranslationTable[Tag]}");
                                            rowLoaded = true; // don't log those
                                        }
                                    }
                                    break;
                                case "UIMapper":
                                    if (TryGetUIMapperRow(xmlReader, out UIMapper UIMapperRow))
                                    {
                                        UIMapperList.Add(UIMapperRow);
                                        rowLoaded = true;
                                    }
                                    break;
                                case "EraDefinition":
                                    if (TryGetEraDefinitionRow(xmlReader, out EraDefinition EraDefinitionRow))
                                    {
                                        //EraDefinitionList.Add(EraDefinitionRow);
                                        rowLoaded = true;
                                    }
                                    break;
                                case "GameSpeedDefinition":
                                    if (TryGetGameSpeedDefinitionRow(xmlReader, out GameSpeedDefinition GameSpeedDefinitionRow))
                                    {
                                        //GameSpeedDefinitionList.Add(GameSpeedDefinitionRow);
                                        rowLoaded = true;
                                    }
                                    break;
                                case "ModDefines":
                                    if (TryGetAttribute(xmlReader, "Name", out string defineName) && TryGetAttribute(xmlReader, "Value", out string defineValue))
                                    {
                                        if (!ModDefines.ContainsKey(defineName))
                                        {
                                            ModDefines.Add(defineName, defineValue);
                                            rowLoaded = true;
                                        }
                                    }
                                    break;

                            }
                            if (!rowLoaded)
                            {
                                IXmlLineInfo xmlInfo = xmlReader as IXmlLineInfo;
                                Diagnostics.LogError($"[Gedemon] [LoadXML] [Element] Can't add Row (current table = {currentTable}) at line = {xmlInfo.LineNumber}, position = {xmlInfo.LinePosition}");

                            }
                        }
                    }
                }
                // to do : add data as MapTCL class

            }

            if (currentMapTCL != null)
            {
                moddedTCL.Add(currentMapTCL);
            }

            if (moddedTCL.Count > 0)
            {
                ModLoading.AddModdedTCL(moddedTCL, provider);
            }
        }
        public static void UpdateTranslationDB()
        {
            var localizedStrings = Databases.GetDatabase<LocalizedStringElement>();
            foreach (KeyValuePair<string, string> kvp in TranslationTable)
            {
                localizedStrings.Touch(new LocalizedStringElement()
                {
                    LineId = $"%{kvp.Key}",
                    LocalizedStringElementFlag = LocalizedStringElementFlag.None,
                    CompactedNodes = new LocalizedNode[] {
                        new LocalizedNode{ Id= LocalizedNodeType.Terminal, TextValue=kvp.Value}
                    },
                    TagCodes = new[] { 0 }
                });
            }
        }
        public static void UpdateUIMapperDB()
        {
            var database = Databases.GetDatabase<UIMapper>();
            foreach (var moddedDatabaseRow in UIMapperList)
            {
                database.Touch(moddedDatabaseRow);
            }
        }
        public static FixedPoint GetEraCostModifier(string eraName)
        {
            if (ModDefines.TryGetValue(eraName+"CostModifier", out string costModifier))
            {
                if (float.TryParse(costModifier, out float modifier))
                {
                    return (FixedPoint)modifier;
                }
            }
            return FixedPoint.One;
        }
        public static FixedPoint GetSpecialTechCostModifier(string techName)
        {
            if (ModDefines.TryGetValue(techName + "CostModifier", out string costModifier))
            {
                if (float.TryParse(costModifier, out float modifier))
                {
                    return (FixedPoint)modifier;
                }
            }
            return FixedPoint.One;
        }
        public static void UpdateEndTurnLimit()
        {
            if (ModDefines.TryGetValue("EndTurnBaseValue", out string endTurnString))
            {
                if (int.TryParse(endTurnString, out int endTurn))
                {
                    ref EndGameController endGameController = ref Amplitude.Mercury.Sandbox.Sandbox.EndGameController;
                    endGameController.TurnLimit = endTurn;
                }
            }
        }
        public static void OnModPreloaded()
        {
            // for DB that need to be loaded before Sandbox is started

        }
        public static void OnSandboxStart()
        {
            //OffsetMapXML();
            UpdateTranslationDB();
            UpdateUIMapperDB();
        }

        public static void AfterSandboxStarted()
        {
            UpdateEndTurnLimit();
        }

        public static void OnExitSandbox()
        {
            TranslationTable.Clear();
            UIMapperList.Clear();
            EraDefinitionList.Clear();
            GameSpeedDefinitionList.Clear();
        }

        public static void OffsetMapXML()
        {
            // fix non-encoded "&" in file
            string text = File.ReadAllText(@"path");
            Diagnostics.LogError($"[Gedemon] [OffsetMapXML] loading (text Length = {text.Length})");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"&(\W|$)", match => "&amp;" + match.Value.Substring(1));

            XDocument xmlDoc = XDocument.Parse(text); 
            var items = from item in xmlDoc.Descendants("Row")
                        where item.Attribute("X").Value != null
                        select item;

            foreach (XElement itemElement in items)
            {
                if (int.TryParse(itemElement.Attribute("Y").Value, out int value))
                {
                    value++;
                    itemElement.SetAttributeValue("Y", value.ToString());
                }
            }

            xmlDoc.Save("path2");
        }



        /*
        [HarmonyPatch(typeof(EndGameCondition_TurnLimit))]
        public class CultureUnlock_PresentationPawn
        {

            [HarmonyPrefix]
            [HarmonyPatch(nameof(GetTurnLimit))]
            public static bool GetTurnLimit(EndGameCondition_TurnLimit __instance, ref int __result)
            {
                __result = 1000;
                return false;
            }
        }
                public FixedPoint GetTechnologyCostWithModifiers(ref Technology technology)
        {
            FixedPoint initialCost = technology.InitialCost;
            initialCost = majorEmpire.DepartmentOfTheTreasury.ApplyCostModifiers(technology.TechnologyDefinition, initialCost);
            initialCost *= Amplitude.Mercury.Sandbox.Sandbox.GameSpeedController.CurrentGameSpeedDefinition.TechnologyCostMultiplier;
            return FixedPoint.Round(initialCost);
        }
        //*/
    }
}
