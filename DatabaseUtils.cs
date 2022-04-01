using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
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
using HarmonyLib;
using UnityEngine;
using Diagnostics = Amplitude.Diagnostics;

namespace Gedemon.Uchronia
{
    public class CloneHelpers
    {
        public static object DeepClone(object source)
        {
            MemoryStream m = new MemoryStream();
            BinaryFormatter b = new BinaryFormatter();
            b.Serialize(m, source);
            m.Position = 0;
            return b.Deserialize(m);

        }
        public static bool DeepEquals(object objA, object objB)
        {
            MemoryStream serA = serializedStream(objA);
            MemoryStream serB = serializedStream(objB);
            if (serA.Length != serA.Length)
                return false;
            while (serA.Position < serA.Length)
            {
                if (serA.ReadByte() != serB.ReadByte())
                    return false;
            }
            return true;

        }
        public static MemoryStream serializedStream(object source)
        {
            MemoryStream m = new MemoryStream();
            BinaryFormatter b = new BinaryFormatter();
            b.Serialize(m, source);
            m.Position = 0;

            return m;
        }
    }

    class DatabaseUtils
    {
        public static float DistrictPerPopulation;
        public static int FreePopulationDistrict;
        static void SetCachedModDefines()
        {
            TryGetDefine("DistrictPerPopulation", out DistrictPerPopulation, defaultValue : 1.0f);
            TryGetDefine("FreePopulationDistrict", out FreePopulationDistrict, defaultValue: 2);
        }
        static public bool TryGetDefine(string name, out float value, float defaultValue = 1.0f)
        {
            if(ModDefines.TryGetValue(name, out string valueString))
            {
                return float.TryParse(valueString, out value);
            }
            value = defaultValue;
            return false;
        }
        static public bool TryGetDefine(string name, out int value, int defaultValue = 1)
        {
            if (ModDefines.TryGetValue(name, out string valueString))
            {
                return int.TryParse(valueString, out value);
            }
            value = defaultValue;
            return false;
        }
        static public bool TryGetDefine(string name, out string value, string defaultValue = "")
        {
            value = defaultValue;
            return ModDefines.TryGetValue(name, out value);
        }

        public static List<int> GetListIntFromString(string text)
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

        public static bool TryGetAttribute(XmlReader reader, string attribute, out string result)
        {
            result = reader.GetAttribute(attribute);
            return result != null;
        }
        public static bool TryGetAttributeWithoutSpace(XmlReader reader, string attribute, out string result)
        {
            result = reader.GetAttribute(attribute);
            if (result != null)
            {
                result = result.Replace(" ", string.Empty);
                return true;
            }
            return false;
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
        static bool TryUpdateEraDefinitionRow(XmlReader reader, out EraDefinition DatabaseRow)
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
        static bool TryUpdateGameSpeedDefinitionRow(XmlReader reader, out GameSpeedDefinition DatabaseRow)
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

        public class Variable
        {
            public object Value { get; set; }
        }
        /*
        Dictionary<string, Variable> dict = new Dictionary<string, Variable>()
            {
                {"Transit", new Variable()}
            }

            dict["Transit"].Value = "transit"
        //*/
        static public void ShowObject(object source)
        {
            Diagnostics.LogWarning($"[Gedemon] ShowObject: source.GetType() = {source.GetType()}");

            foreach (var prop in source.GetType().GetProperties())
            {
                Diagnostics.Log($"[Gedemon] {prop.Name} = {prop.GetValue(source, null)}");
            }
            foreach (var prop in source.GetType().GetFields())
            {
                Diagnostics.Log($"[Gedemon] {prop.Name} = {prop.GetValue(source)}");
            }
            foreach (var prop in source.GetType().GetFields((BindingFlags.Instance | BindingFlags.NonPublic)))
            {
                Diagnostics.Log($"[Gedemon] {prop.Name} = {prop.GetValue(source)}");
            }
            /*
                    type.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)

                     foreach (var prop in ch.GetType().GetProperties())
                    {
                        this.GetType().GetProperty(prop.Name).SetValue(this, prop.GetValue(ch, null), null);
                    }
            //*/
        }
        static public void CloneObject(object source, ref object destination)
        {
            Diagnostics.LogWarning($"[Gedemon] CloneObject: source.GetType() = {source.GetType()}");

            foreach (var prop in source.GetType().GetProperties())
            {
                Diagnostics.Log($"[Gedemon] {prop.Name} = {prop.GetValue(source, null)}");
            }
            foreach (var prop in source.GetType().GetFields())
            {
                Diagnostics.Log($"[Gedemon] {prop.Name} = {prop.GetValue(source)}");
            }
            foreach (var prop in source.GetType().GetFields((BindingFlags.Instance | BindingFlags.NonPublic)))
            {
                Diagnostics.Log($"[Gedemon] {prop.Name} = {prop.GetValue(source)}");
            }
            /*
                    type.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)

                     foreach (var prop in ch.GetType().GetProperties())
                    {
                        this.GetType().GetProperty(prop.Name).SetValue(this, prop.GetValue(ch, null), null);
                    }
            //*/
        }


        public static IDictionary<string, string> TranslationTable = new Dictionary<string, string>();
        public static IDictionary<string, string> ModDefines = new Dictionary<string, string>();
        public static List<UIMapper> UIMapperList = new List<UIMapper>();
        public static List<EraDefinition> EraDefinitionList = new List<EraDefinition>();
        public static List<GameSpeedDefinition> GameSpeedDefinitionList = new List<GameSpeedDefinition>();

        static readonly List<string> SupportedTablesXML = new List<string> { "ExtensionDistrictDefinition", "ModDefines", "EraDefinition", "GameSpeedDefinition", "UIMapper", "CivilizationCityAliases", "LocalizedText", "CityMap", "MajorEmpireTerritories", "MajorEmpireCoreTerritories", "MinorFactionTerritories", "ExtraPositions", "ExtraPositionsNewWorld", "ContinentNames", "TerritoryNames", "NoCapital", "NomadCultures" };
        public static void LoadXML(string input, string provider, bool inputIsText = false, bool preLoad = true)
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
                    if (preLoad)
                    {
                        switch (xmlReader.Name)
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

                    }

                    if (SupportedTablesXML.Contains(xmlReader.Name))
                    {
                        Diagnostics.LogWarning($"[Gedemon] [LoadXML] [Element] Switch current Table to {xmlReader.Name}");
                        // to do : add check for load order attribute here if we implement it
                        currentTable = xmlReader.Name;
                    }

                    if(currentTable != null)
                    {
                        if (xmlReader.HasAttributes)
                        {
                            //Diagnostics.Log($"[Gedemon] [LoadXML] [Element] Has Attibute, name is {xmlReader.Name}"); // name is Row -> to do: update, insert, ...
                            bool Insert = xmlReader.Name.ToUpper() == "INSERT" || xmlReader.Name.ToUpper() == "ROW";
                            bool Update = xmlReader.Name.ToUpper() == "UPDATE" || xmlReader.Name.ToUpper() == "REPLACE";
                            if(preLoad)
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
                                        if (TryGetAttribute(xmlReader, "Civilization", out string Civilization) && TryGetAttributeWithoutSpace(xmlReader, "Aliases", out string Aliases) && !TranslationTable.ContainsKey(Civilization))
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
                                        if (TryUpdateEraDefinitionRow(xmlReader, out EraDefinition EraDefinitionRow))
                                        {
                                            //EraDefinitionList.Add(EraDefinitionRow);
                                            rowLoaded = true;
                                        }
                                        break;
                                    case "GameSpeedDefinition":
                                        if (TryUpdateGameSpeedDefinitionRow(xmlReader, out GameSpeedDefinition GameSpeedDefinitionRow))
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
                                    case "ExtensionDistrictDefinition": // post load
                                        rowLoaded = true;
                                        break;

                                }
                                if (!rowLoaded)
                                {
                                    IXmlLineInfo xmlInfo = xmlReader as IXmlLineInfo;
                                    Diagnostics.LogError($"[Gedemon] [LoadXML] [Preload] Can't add Row (current table = {currentTable}) at line = {xmlInfo.LineNumber}, position = {xmlInfo.LinePosition}");

                                }

                            }
                            else
                            {
                                bool rowLoaded = false;
                                switch (currentTable)
                                {
                                    // ignore pre-load (to do : cache post-load during pre-load then udate)
                                    case "MajorEmpireTerritories":
                                    case "MajorEmpireCoreTerritories":
                                    case "MinorFactionTerritories":
                                    case "ContinentNames":
                                    case "TerritoryNames":
                                    case "ExtraPositions":
                                    case "ExtraPositionsNewWorld":
                                    case "NoCapital":
                                    case "NomadCultures":
                                    case "CityMap":
                                    case "CivilizationCityAliases":
                                    case "LocalizedText":
                                    case "UIMapper":
                                    case "EraDefinition":
                                    case "GameSpeedDefinition":
                                    case "ModDefines":
                                        rowLoaded = true;
                                        break;
                                    case "ExtensionDistrictDefinition":
                                        if (Update ? Districts.TryUpdateExtensionDistrictDefinitionRow(xmlReader) : true)
                                        {
                                            rowLoaded = true;
                                        }
                                        break;

                                }
                                if (!rowLoaded)
                                {
                                    IXmlLineInfo xmlInfo = xmlReader as IXmlLineInfo;
                                    Diagnostics.LogError($"[Gedemon] [LoadXML] [Postload] Can't add Row (current table = {currentTable}) at line = {xmlInfo.LineNumber}, position = {xmlInfo.LinePosition}");
                                }
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

        public static bool TryGetEndTurnLimit(out int endTurn)
        {
            if (ModDefines.TryGetValue("EndTurnBaseValue", out string endTurnString))
            {
                return int.TryParse(endTurnString, out endTurn);
            }
            endTurn = -1;
            return false;
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
            SetCachedModDefines();

            Districts.UpdateDistrictDB();

        }
        public static void OnMapLoaded()
        {

        }

        public static void AfterSandboxStarted()
        {
            //ShowDatabaseContent();
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
        //*
        [HarmonyPatch(typeof(EndGameController))]
        public class EndGameController_Patch
        {

            [HarmonyPostfix]
            [HarmonyPatch(nameof(SetEndGameCondition))]
            public static void SetEndGameCondition(EndGameController __instance, ref Amplitude.Mercury.Data.Simulation.EndGameCondition[] endGameConditions)
            {
                if (TryGetEndTurnLimit(out int baseTurnLimit))
                {

                    Diagnostics.LogError($"[Gedemon] [EndGameController] SetEndGameCondition postfix: __instance.TurnLimit = {__instance.TurnLimit}, baseTurnLimit = {baseTurnLimit}, .CurrentGameSpeedDefinition.EndGameTurnLimitMultiplier = {Amplitude.Mercury.Sandbox.Sandbox.GameSpeedController.CurrentGameSpeedDefinition.EndGameTurnLimitMultiplier}");

                    int num = endGameConditions.Length;
                    for (int i = 0; i < num; i++)
                    {
                        Amplitude.Mercury.Data.Simulation.EndGameCondition endGameCondition = endGameConditions[i];
                        bool flag = __instance.EndGameConditionActivation[i];
                        Amplitude.Mercury.Data.Simulation.EndGameCondition_TurnLimit endGameCondition_TurnLimit = endGameCondition as Amplitude.Mercury.Data.Simulation.EndGameCondition_TurnLimit;

                        Diagnostics.LogError($"[Gedemon] [EndGameController] SetEndGameCondition postfix: endGameCondition_TurnLimit[{i}].TurnLimit = {endGameCondition_TurnLimit.TurnLimit}");

                        if (endGameCondition_TurnLimit != null)
                        {
                            if (endGameCondition_TurnLimit.TurnLimit != baseTurnLimit)
                            {
                                // update EndGameCondition_TurnLimit value, as it seems to be the one used for endgame checks
                                endGameCondition_TurnLimit.TurnLimit = baseTurnLimit;
                                __instance.TurnLimit = (flag ? ((int)((float)endGameCondition_TurnLimit.TurnLimit * Amplitude.Mercury.Sandbox.Sandbox.GameSpeedController.CurrentGameSpeedDefinition.EndGameTurnLimitMultiplier)) : (-1));
                            }
                            break;
                        }
                    }
                    Diagnostics.LogError($"[Gedemon] [EndGameController] SetEndGameCondition postfix: __instance.TurnLimit = {__instance.TurnLimit}");
                }
            }
        }
        //*/

        public static void ShowDatabaseContent()
        {

            // stability level	= PublicOrderEffectDefinition, EmpireStabilityDefinition
            // units			= PresentationUnitDefinition
            // cultures			= FactionDefinition
            // eras				= EraDefinition
            // 
            // BuildingVisualAffinityDefinition
            // UnitVisualAffinityDefinition
            // EmpireSymbolDefinition
            // Amplitude.Framework.Localization.LocalizedStringElement
            // GameSpeedDefinition
            // EraDefinition
            //
            //DistrictDefinition <- ConstructibleDefinition ExtensionDistrictDefinition
            // ExploitationRuleDefinition

            Diagnostics.LogWarning($"[Gedemon] in ShowDatabaseContent Explore Database for ConstructibleDefinition");
            var database1 = Databases.GetDatabase<ConstructibleDefinition>();
            if (database1 != null)
            {
                int rowNum = 0;
                foreach (var data in database1)
                {
                    Diagnostics.LogWarning($"[Gedemon] Row num#{rowNum}");//, Name = {data.Name}");
                    rowNum++;
                    ShowObject(data);
                }
            }
            else
            {
                Diagnostics.LogWarning($"[Gedemon] in ShowDatabaseContent: Null Database");
            }
        }
    }
}
