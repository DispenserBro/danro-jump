using System;
using System.Globalization;
using UnityEngine;

namespace DanroJump.Settings
{
    /// <summary>
    /// Тип значения сервисной настройки.
    /// </summary>
    public enum ServiceSettingValueType
    {
        Bool,
        Int,
        Float,
        String,
        Button,
        Option
    }

    /// <summary>
    /// Универсальное значение сервисной настройки с явным типом.
    /// </summary>
    [Serializable]
    public struct ServiceSettingValue : IEquatable<ServiceSettingValue>
    {
        /// <summary>
        /// Активный тип значения.
        /// </summary>
        public ServiceSettingValueType type;

        /// <summary>
        /// Значение для bool-настроек.
        /// </summary>
        public bool boolValue;

        /// <summary>
        /// Значение для int и option-настроек.
        /// </summary>
        public int intValue;

        /// <summary>
        /// Значение для float-настроек.
        /// </summary>
        public float floatValue;

        /// <summary>
        /// Значение для string-настроек.
        /// </summary>
        public string stringValue;

        /// <summary>
        /// Создает bool-значение.
        /// </summary>
        public static ServiceSettingValue Bool(bool value)
        {
            return new ServiceSettingValue { type = ServiceSettingValueType.Bool, boolValue = value };
        }

        /// <summary>
        /// Создает int-значение.
        /// </summary>
        public static ServiceSettingValue Int(int value)
        {
            return new ServiceSettingValue { type = ServiceSettingValueType.Int, intValue = value };
        }

        /// <summary>
        /// Создает float-значение.
        /// </summary>
        public static ServiceSettingValue Float(float value)
        {
            return new ServiceSettingValue { type = ServiceSettingValueType.Float, floatValue = value };
        }

        /// <summary>
        /// Создает string-значение.
        /// </summary>
        public static ServiceSettingValue String(string value)
        {
            return new ServiceSettingValue { type = ServiceSettingValueType.String, stringValue = value ?? string.Empty };
        }

        /// <summary>
        /// Создает marker-значение для кнопки действия.
        /// </summary>
        public static ServiceSettingValue Button()
        {
            return new ServiceSettingValue { type = ServiceSettingValueType.Button };
        }

        /// <summary>
        /// Создает option-значение по индексу варианта.
        /// </summary>
        public static ServiceSettingValue Option(int optionIndex)
        {
            return new ServiceSettingValue { type = ServiceSettingValueType.Option, intValue = optionIndex };
        }

        /// <summary>
        /// Упаковывает значение в компактный строковый формат для JSON.
        /// </summary>
        public ServiceSettingValueCompact ToCompact()
        {
            return new ServiceSettingValueCompact
            {
                type = (int)type,
                value = type switch
                {
                    ServiceSettingValueType.Bool => boolValue ? "1" : "0",
                    ServiceSettingValueType.Int => intValue.ToString(CultureInfo.InvariantCulture),
                    ServiceSettingValueType.Float => floatValue.ToString("R", CultureInfo.InvariantCulture),
                    ServiceSettingValueType.String => stringValue ?? string.Empty,
                    ServiceSettingValueType.Button => string.Empty,
                    ServiceSettingValueType.Option => intValue.ToString(CultureInfo.InvariantCulture),
                    _ => string.Empty
                }
            };
        }

        /// <summary>
        /// Восстанавливает typed-значение из компактного JSON-представления.
        /// </summary>
        public static ServiceSettingValue FromCompact(ServiceSettingValueCompact compact)
        {
            var valueType = Enum.IsDefined(typeof(ServiceSettingValueType), compact.type)
                ? (ServiceSettingValueType)compact.type
                : ServiceSettingValueType.String;

            return valueType switch
            {
                ServiceSettingValueType.Bool => Bool(compact.value == "1" ||
                    bool.TryParse(compact.value, out var boolValue) && boolValue),
                ServiceSettingValueType.Int => Int(ParseInt(compact.value)),
                ServiceSettingValueType.Float => Float(ParseFloat(compact.value)),
                ServiceSettingValueType.String => String(compact.value),
                ServiceSettingValueType.Button => Button(),
                ServiceSettingValueType.Option => Option(ParseInt(compact.value)),
                _ => String(compact.value)
            };
        }

        /// <summary>
        /// Сравнивает значения настроек с учетом типа и приблизительного float-сравнения.
        /// </summary>
        public bool Equals(ServiceSettingValue other)
        {
            return type == other.type &&
                boolValue == other.boolValue &&
                intValue == other.intValue &&
                Mathf.Approximately(floatValue, other.floatValue) &&
                stringValue == other.stringValue;
        }

        /// <summary>
        /// Сравнивает значение с boxed-объектом.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is ServiceSettingValue other && Equals(other);
        }

        /// <summary>
        /// Возвращает hash code для использования значения в словарях и наборах.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(type, boolValue, intValue, floatValue, stringValue);
        }

        /// <summary>
        /// Возвращает строковое представление активного typed-значения.
        /// </summary>
        public override string ToString()
        {
            return type switch
            {
                ServiceSettingValueType.Bool => boolValue.ToString(),
                ServiceSettingValueType.Int => intValue.ToString(CultureInfo.InvariantCulture),
                ServiceSettingValueType.Float => floatValue.ToString(CultureInfo.InvariantCulture),
                ServiceSettingValueType.String => stringValue ?? string.Empty,
                ServiceSettingValueType.Button => string.Empty,
                ServiceSettingValueType.Option => intValue.ToString(CultureInfo.InvariantCulture),
                _ => string.Empty
            };
        }

        /// <summary>
        /// Безопасно читает int из invariant-строки.
        /// </summary>
        private static int ParseInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : 0;
        }

        /// <summary>
        /// Безопасно читает float из invariant-строки.
        /// </summary>
        private static float ParseFloat(string value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? result
                : 0f;
        }
    }

    /// <summary>
    /// Компактное сериализуемое представление значения настройки.
    /// </summary>
    [Serializable]
    public struct ServiceSettingValueCompact
    {
        /// <summary>
        /// Числовой код типа значения.
        /// </summary>
        public int type;

        /// <summary>
        /// Значение, упакованное в строку для JSON.
        /// </summary>
        public string value;
    }
}
