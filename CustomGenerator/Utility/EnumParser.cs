using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomGenerator.Utility
{
    static class EnumParser
    {
        public static Enum GetFilterEnum(string type, List<string> values)
        {
            switch (type)
            {
                case "SplatType":
                    return ParseEnum<TerrainSplat.Enum>(values);
                case "BiomeType":
                    return ParseEnum<TerrainBiome.Enum>(values);
                case "TopologyAny":
                case "TopologyAll":
                case "TopologyNot":
                    return ParseEnum<TerrainTopology.Enum>(values);
                default:
                    throw new ArgumentException($"Unknown type: {type}");
            }
        }

        private static T ParseEnum<T>(List<string> values) where T : struct, Enum
        {
            T result = default;
            foreach (var value in values)
            {
                if (Enum.TryParse(value.Trim(), out T parsedValue))
                {
                    result = (T)(object)(((int)(object)result) | ((int)(object)parsedValue));
                }
                else
                {
                    throw new ArgumentException($"Invalid value: {value}");
                }
            }
            return result;
        }
    }
}
