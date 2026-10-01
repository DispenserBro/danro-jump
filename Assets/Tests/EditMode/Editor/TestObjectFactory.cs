using System;
using System.Collections.Generic;
using System.Reflection;
using DanroJump.Gameplay;
using DanroJump.Settings;
using UnityEngine;

namespace DanroJump.Tests.EditMode
{
    internal static class TestObjectFactory
    {
        public static ServiceSettingsDatabase CreateSettingsDatabase(params ServiceSettingDefinition[] definitions)
        {
            var database = ScriptableObject.CreateInstance<ServiceSettingsDatabase>();
            SetPrivateField(database, "entries", new List<ServiceSettingDefinition>(definitions));
            return database;
        }

        public static ServiceSettingDefinition CreateSettingDefinition(
            string key,
            ServiceSettingValueType type,
            ServiceSettingValue defaultValue,
            bool exposedForExternalChanges = false,
            string group = "General",
            int minInt = 0,
            int maxInt = 10,
            float minFloat = 0f,
            float maxFloat = 1f,
            IReadOnlyList<string> options = null)
        {
            var definition = new ServiceSettingDefinition();
            SetPrivateField(definition, "key", key);
            SetPrivateField(definition, "displayName", key);
            SetPrivateField(definition, "group", group);
            SetPrivateField(definition, "exposedForExternalChanges", exposedForExternalChanges);
            SetPrivateField(definition, "valueType", type);
            SetPrivateField(definition, "minInt", minInt);
            SetPrivateField(definition, "maxInt", maxInt);
            SetPrivateField(definition, "minFloat", minFloat);
            SetPrivateField(definition, "maxFloat", maxFloat);

            switch (defaultValue.type)
            {
                case ServiceSettingValueType.Bool:
                    SetPrivateField(definition, "defaultBool", defaultValue.boolValue);
                    break;
                case ServiceSettingValueType.Int:
                    SetPrivateField(definition, "defaultInt", defaultValue.intValue);
                    break;
                case ServiceSettingValueType.Float:
                    SetPrivateField(definition, "defaultFloat", defaultValue.floatValue);
                    break;
                case ServiceSettingValueType.String:
                    SetPrivateField(definition, "defaultString", defaultValue.stringValue);
                    break;
                case ServiceSettingValueType.Option:
                    SetPrivateField(definition, "defaultOptionIndex", defaultValue.intValue);
                    break;
            }

            if (options != null)
            {
                SetPrivateField(definition, "options", new List<string>(options));
            }

            return definition;
        }

        public static GameplayDifficultyStage CreateDifficultyStage(
            string stageName,
            float startY,
            float verticalLength,
            float horizontalDistribution)
        {
            var stage = ScriptableObject.CreateInstance<GameplayDifficultyStage>();
            SetPrivateField(stage, "stageName", stageName);
            SetPrivateField(stage, "startY", startY);
            SetPrivateField(stage, "verticalLength", verticalLength);
            SetPrivateField(stage, "horizontalDistribution", horizontalDistribution);
            return stage;
        }

        public static WeightedPrefabOption CreateWeightedPrefabOption(GameObject prefab, float weight)
        {
            var option = new WeightedPrefabOption();
            SetPrivateField(option, "prefab", prefab);
            SetPrivateField(option, "weight", weight);
            return option;
        }

        public static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }
    }
}
