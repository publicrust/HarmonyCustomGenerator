using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomGenerator.Utility
{
    // Field description shown by the editor (VS Code etc.) on hover, in the config's language
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class DescAttribute : Attribute {
        public readonly string En, Ru;
        public DescAttribute(string en, string ru) { En = en; Ru = ru; }
    }

    // Extra schema constraints. Enum/Values restrict a string (or the items of a string list) to these names
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SchemaAttribute : Attribute {
        public double Min = double.NaN, Max = double.NaN;
        public Type Enum;
        public string[] Values;
    }

    // Builds a JSON Schema (draft-07) of the config, so editors show descriptions, autocomplete keys and values
    // and underline mistakes. Keys come from the same contract resolver as the config file.
    internal static class ConfigSchema {
        public static JObject Build(Type root, IContractResolver resolver, bool ru) {
            var schema = Describe(root, resolver, ru, new HashSet<Type>());
            schema.AddFirst(new JProperty("$schema", "http://json-schema.org/draft-07/schema#"));
            schema["title"] = "CustomGenerator.json";
            return schema;
        }

        private static JObject Describe(Type type, IContractResolver resolver, bool ru, HashSet<Type> stack) {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(string)) return new JObject { ["type"] = "string" };
            if (type == typeof(bool)) return new JObject { ["type"] = "boolean" };
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return new JObject { ["type"] = "number" };
            if (type == typeof(uint) || type == typeof(ulong)) return new JObject { ["type"] = "integer", ["minimum"] = 0 };
            if (type.IsPrimitive) return new JObject { ["type"] = "integer" };
            if (type.IsEnum) return new JObject { ["type"] = "string", ["enum"] = new JArray(Enum.GetNames(type)) };

            var dictionary = FindGeneric(type, typeof(IDictionary<,>));
            if (dictionary != null)
                return new JObject { ["type"] = "object", ["additionalProperties"] = Describe(dictionary[1], resolver, ru, stack) };

            var list = FindGeneric(type, typeof(IEnumerable<>));
            if (list != null)
                return new JObject { ["type"] = "array", ["items"] = Describe(list[0], resolver, ru, stack) };

            if (!(resolver.ResolveContract(type) is JsonObjectContract contract) || !stack.Add(type))
                return new JObject();

            object defaults = null;
            try { defaults = Activator.CreateInstance(type); } catch { }

            var properties = new JObject();
            foreach (var property in contract.Properties) {
                // Skip ignored fields and the other-language aliases (never serialized)
                if (property.Ignored || property.ShouldSerialize?.Invoke(null) == false) continue;

                var node = Describe(property.PropertyType, resolver, ru, stack);
                var attributes = property.AttributeProvider?.GetAttributes(true) ?? new List<Attribute>();

                var desc = attributes.OfType<DescAttribute>().FirstOrDefault();
                if (desc != null) node.AddFirst(new JProperty("description", ru ? desc.Ru : desc.En));

                var extra = attributes.OfType<SchemaAttribute>().FirstOrDefault();
                if (extra != null) ApplyExtra(node, extra);

                if (defaults != null && property.ValueProvider != null && IsSimple(property.PropertyType)) {
                    object value = property.ValueProvider.GetValue(defaults);
                    if (value != null) node["default"] = property.PropertyType.IsEnum ? new JValue(value.ToString()) : JToken.FromObject(value);
                }
                properties[property.PropertyName] = node;
            }

            stack.Remove(type);
            return new JObject { ["type"] = "object", ["properties"] = properties };
        }

        private static void ApplyExtra(JObject node, SchemaAttribute extra) {
            // For a list or a dictionary the constraints apply to its values
            var target = node["items"] as JObject ?? node["additionalProperties"] as JObject ?? node;
            string[] names = extra.Values ?? (extra.Enum != null ? Enum.GetNames(extra.Enum) : null);
            if (names != null) target["enum"] = new JArray(names.Distinct());
            if (!double.IsNaN(extra.Min)) target["minimum"] = extra.Min;
            if (!double.IsNaN(extra.Max)) target["maximum"] = extra.Max;
        }

        private static bool IsSimple(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);

        private static Type[] FindGeneric(Type type, Type definition) {
            if (type == typeof(string)) return null;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == definition) return type.GetGenericArguments();
            return type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == definition)?.GetGenericArguments();
        }
    }
}
