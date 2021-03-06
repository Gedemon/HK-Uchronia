using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Amplitude;
using Amplitude.Framework;
using Amplitude.Framework.Localization;
using Amplitude.Mercury;
using Amplitude.Mercury.Data.Simulation;
using Amplitude.Mercury.Sandbox;
using Amplitude.Mercury.Simulation;

namespace Gedemon.Uchronia
{
    public class CityPosition
    {
        public int TerritoryIndex { get; set; }
        public string Name { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public int Size { get; set; }
        public Hexagon.OffsetCoords Position { get; set; }
    }
    class CityMap
    {
        enum MatchLevel
        {
            // higher is better
            Generic = 0,
            Era = 100,
            CultureList = 200,
            Culture = 300
        }

        public static List<CityPosition> WorldCityMap = new List<CityPosition>();

        public static IDictionary<int, List<CityPosition>> TerritoryCityMap = new Dictionary<int, List<CityPosition>>();

        public static IDictionary<string, List<string>> CivilizationAliases = new Dictionary<string, List<string>>();  

        static bool HasLocalization(string key)
        {
            return key != Utils.TextUtils.Localize(key);
        }
        public static void BuildTerritoryCityMap(World currentWorld)
        {
            foreach (CityPosition cityPosition in WorldCityMap)
            {
                //Diagnostics.Log($"[Gedemon] [BuildTerritoryCityMap] Adding City {cityPosition.Name} at ({cityPosition.Column},{cityPosition.Row})");
                WorldPosition position = new WorldPosition(cityPosition.Column, cityPosition.Row);
                int territoryIndex = currentWorld.TileInfo.Data[position.ToTileIndex()].TerritoryIndex;

                if (TerritoryCityMap.TryGetValue(territoryIndex, out List<CityPosition> cityList))
                {
                    cityList.Add(cityPosition);
                }
                else
                {
                    TerritoryCityMap.Add(territoryIndex, new List<CityPosition> { cityPosition });
                }

            }

            foreach (KeyValuePair<int, List<CityPosition>> kvp in TerritoryCityMap)
            {
                int territoryIndex = kvp.Key;
                List<CityPosition> cityList = kvp.Value;
                if (cityList.Count > 0)
                {
                    //Diagnostics.LogWarning($"[Gedemon] [BuildTerritoryCityMap] Created list of {cityList.Count} cities for Territory #{territoryIndex} ({CultureUnlock.GetTerritoryName(territoryIndex)})");
                    foreach (CityPosition cityPosition in cityList)
                    {
                        //Diagnostics.Log($"[Gedemon] City {cityPosition.Name} at ({cityPosition.Column},{cityPosition.Row})");
                    }
                }
            }

        }

        static public void UpdateSettlementName(Settlement settlement)
        {
            if (CityMap.TryGetCityNameAt(settlement.WorldPosition, settlement.Empire, out string cityLocalizationKey))
            {
                settlement.EntityName.cachedLocalizedName = Utils.TextUtils.Localize(cityLocalizationKey);
                settlement.EntityName.LocalizationKey = cityLocalizationKey;
            }
        }

        static bool TryGetAliasCity(string civilization, string localizationKey, out string aliasCityKey, out int entryNum)
        {
            aliasCityKey = null;
            entryNum = 0;
            if (CivilizationAliases.TryGetValue(civilization, out List<string> listAliases))
            {
                for(int i = 0; i < listAliases.Count; i++)
                {
                    string alias = listAliases[i];
                    entryNum = i;
                    string testCityKey = localizationKey + "_" + alias;
                    //Diagnostics.Log($"[Gedemon] [CityMap] Check localization for a city name specific to alias {testCityKey}");
                    if (HasLocalization(testCityKey))
                    {
                        aliasCityKey = testCityKey;
                        return true;
                    }
                }
            }
            return false;
        }

        static bool TryGetTerritoryCultureCity(FactionDefinition factionDefinition, int territoryIndex, out string territoryCityKey, out bool IsGameEra)
        {
            territoryCityKey = null;
            IsGameEra = false;
            List<string> CivilizationList = CultureUnlock.TerritoriesUnlockingMajorEmpires[territoryIndex];
            if(CivilizationList != null)
            {

            }

            return territoryCityKey != null;
        }

        static bool TryGetEraCity(Empire empire, string localizationKey, out string eraCityKey, out int eraDiff)
        {
            int empireEra = (int)empire.EraLevel.Value;
            int minEraIndex = System.Math.Min(Sandbox.Timeline.GetGlobalEraIndex(), empireEra);
            eraDiff = 0;
            eraCityKey = null;
            for(int eraIndex = minEraIndex; eraIndex < Sandbox.Timeline.eraDefinitions.Length; eraIndex++)
            {
                string eraTag = "Era" + eraIndex.ToString();
                string testCityKey = localizationKey + "_" + eraTag;
                eraDiff = System.Math.Abs(empireEra - eraIndex);
                //Diagnostics.Log($"[Gedemon] [CityMap] Check localization for a city name specific to era {testCityKey}");
                if (HasLocalization(testCityKey))
                {
                    eraCityKey = testCityKey;
                    return true;
                }
            }

            return eraCityKey != null;
        }

        static bool TryGetEraCity(int empireEra, string localizationKey, out string eraCityKey, out int eraDiff)
        {
            int minEraIndex = System.Math.Min(Sandbox.Timeline.GetGlobalEraIndex(), empireEra);
            eraDiff = 0;
            eraCityKey = null;
            for (int eraIndex = minEraIndex; eraIndex < Sandbox.Timeline.eraDefinitions.Length; eraIndex++)
            {
                string eraTag = "Era" + eraIndex.ToString();
                string testCityKey = localizationKey + "_" + eraTag;
                eraDiff = System.Math.Abs(empireEra - eraIndex);
                //Diagnostics.Log($"[Gedemon] [CityMap] Check localization for a city name specific to era {testCityKey}");
                if (HasLocalization(testCityKey))
                {
                    eraCityKey = testCityKey;
                    return true;
                }
            }

            return eraCityKey != null;
        }

        public static bool TryGetCityNameAt(WorldPosition position, FactionDefinition factionDefinition, int eraIndex, out string cityLocalizationKey)
        {
            cityLocalizationKey = null;

            if (!Uchronia.CanUseCityMap())
                return false;

            Diagnostics.LogWarning($"[Gedemon] [CityMap] Try get City Name for position {position}");
            int tileIndex = position.ToTileIndex();
            int territoryIndex = Amplitude.Mercury.Sandbox.Sandbox.World.TileInfo.Data[tileIndex].TerritoryIndex;
            if (TerritoryCityMap.TryGetValue(territoryIndex, out List<CityPosition> cityList))
            {
                int bestDistance = int.MaxValue;
                int bestmatch = (int)MatchLevel.Generic;
                string civilizationTag = factionDefinition.name.Split('_').Last();
                foreach (CityPosition cityPosition in cityList)
                {
                    int currentMatch = (int)MatchLevel.Generic;
                    int column = cityPosition.Column;
                    int row = cityPosition.Row;
                    WorldPosition namePosition = new WorldPosition(column, row);
                    string currentLocalizationKey = $"%{cityPosition.Name}";
                    string cultureCityKey = currentLocalizationKey + "_" + civilizationTag;
                    int distance = namePosition.GetDistance(tileIndex);

                    //Diagnostics.Log($"[Gedemon] [CityMap] Check position of {currentLocalizationKey} at distance = {distance}");

                    // Check if there is a localization for a city name specific to that culture
                    //Diagnostics.Log($"[Gedemon] [CityMap] Check localization for a city name specific to culture {cultureCityKey}");
                    if (HasLocalization(cultureCityKey))
                    {
                        currentMatch = (int)MatchLevel.Culture;
                        currentLocalizationKey = cultureCityKey;
                    }
                    // Else check if there is a localization for a city name specific to an aliase culture
                    else if (TryGetAliasCity(factionDefinition.name, currentLocalizationKey, out string aliasCityKey, out int entryNum))
                    {
                        currentMatch = (int)MatchLevel.CultureList - entryNum;
                        currentLocalizationKey = aliasCityKey;
                    }
                    // Else check if there is a localization for a city name matching the current global era or the Empire Era
                    else if (TryGetEraCity(eraIndex, currentLocalizationKey, out string eraCityKey, out int eraDiff))
                    {
                        currentMatch = (int)MatchLevel.Era - eraDiff;
                        currentLocalizationKey = eraCityKey;
                    }

                    if ((distance < bestDistance && currentMatch == bestmatch) || currentMatch > bestmatch)
                    {
                        //Diagnostics.Log($"[Gedemon] [CityMap] Set new best match for {currentLocalizationKey} at distance = {distance}, match level = {currentMatch}");
                        cityLocalizationKey = currentLocalizationKey;
                        bestDistance = distance;
                        bestmatch = currentMatch;
                    }
                }
                if (cityLocalizationKey != null)
                {
                    Diagnostics.Log($"[Gedemon] [CityMap] Returning best match ({cityLocalizationKey}) at distance = {bestDistance}, match level = {bestmatch}");
                    return true;
                }

            }

            return false;
        }

        public static bool TryGetCityNameAt(WorldPosition position, Empire empire, out string cityLocalizationKey)
        {
            cityLocalizationKey = null;

            if (!Uchronia.CanUseCityMap())
                return false;

            //Diagnostics.LogWarning($"[Gedemon] [CityMap] Try get City Name for position {position}");
            FactionDefinition factionDefinition = empire.FactionDefinition;
            int empireEra = (int)empire.EraLevel.Value;
            return TryGetCityNameAt(position, factionDefinition, empireEra, out cityLocalizationKey);
            /*
            int tileIndex = position.ToTileIndex();
            int territoryIndex = Amplitude.Mercury.Sandbox.Sandbox.World.TileInfo.Data[tileIndex].TerritoryIndex;
            if (TerritoryCityMap.TryGetValue(territoryIndex, out List<CityPosition> cityList))
            {
                int bestDistance = int.MaxValue;
                int bestmatch = (int)MatchLevel.Generic;
                string civilizationTag = factionDefinition.name.Split('_').Last();
                foreach (CityPosition cityPosition in cityList)
                {
                    int currentMatch = (int)MatchLevel.Generic;
                    int column = cityPosition.Column;
                    int row = cityPosition.Row;
                    WorldPosition namePosition = new WorldPosition(column, row);
                    string currentLocalizationKey = $"%{cityPosition.Name}";
                    string cultureCityKey = currentLocalizationKey + "_" + civilizationTag;
                    int distance = namePosition.GetDistance(tileIndex);

                    //Diagnostics.Log($"[Gedemon] [CityMap] Check position of {currentLocalizationKey} at distance = {distance}");

                    // Check if there is a localization for a city name specific to that culture
                    //Diagnostics.Log($"[Gedemon] [CityMap] Check localization for a city name specific to culture {cultureCityKey}");
                    if (HasLocalization(cultureCityKey)) 
                    {
                        currentMatch = (int)MatchLevel.Culture;
                        currentLocalizationKey = cultureCityKey;
                    }
                    // Else check if there is a localization for a city name specific to an aliase culture
                    else if(TryGetAliasCity(factionDefinition.name, currentLocalizationKey, out string aliasCityKey, out int entryNum))
                    {
                        currentMatch = (int)MatchLevel.CultureList - entryNum;
                        currentLocalizationKey = aliasCityKey;
                    }
                    // Else check if there is a localization for a city name matching the current global era or the Empire Era
                    else if (TryGetEraCity(empire, currentLocalizationKey, out string eraCityKey, out int eraDiff))
                    {
                        currentMatch = (int)MatchLevel.Era - eraDiff;
                        currentLocalizationKey = eraCityKey;
                    }

                    if ((distance < bestDistance && currentMatch == bestmatch) || currentMatch > bestmatch )
                    {
                        //Diagnostics.Log($"[Gedemon] [CityMap] Set new best match for {currentLocalizationKey} at distance = {distance}, match level = {currentMatch}");
                        cityLocalizationKey = currentLocalizationKey;
                        bestDistance = distance;
                        bestmatch = currentMatch;
                    }
                }
                if (cityLocalizationKey != null)
                {
                    Diagnostics.Log($"[Gedemon] [CityMap] Returning best match ({cityLocalizationKey}) at distance = {bestDistance}, match level = {bestmatch}");
                    return true;
                }

            }

            return false;
            //*/
        }

        public static void OnExitSandbox()
        {
            WorldCityMap.Clear();
            TerritoryCityMap.Clear();
            CivilizationAliases.Clear();
        }
    }
}
