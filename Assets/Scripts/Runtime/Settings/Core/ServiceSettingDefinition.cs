using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanroJump.Settings
{
    /// <summary>
    /// Предпочитаемый способ отображения настройки в сервисном UI.
    /// </summary>
    public enum ServiceSettingDisplayMode
    {
        Auto,
        Toggle,
        Slider,
        Stepper,
        TextInput,
        Button,
        OptionPicker
    }

    /// <summary>
    /// Описание одной сервисной настройки: ключ, UI-метаданные, тип, дефолт и ограничения.
    /// </summary>
    [Serializable]
    public sealed class ServiceSettingDefinition
    {
        [SerializeField] private string key;
        [SerializeField] private string displayName;
        [SerializeField] [TextArea] private string description;
        [SerializeField] private string group = "General";
        [SerializeField] private bool shownInServiceMenu = true;
        [SerializeField] private bool exposedForExternalChanges;
        [SerializeField] private ServiceSettingValueType valueType;
        [SerializeField] private ServiceSettingDisplayMode displayMode = ServiceSettingDisplayMode.Auto;

        [Header("Default values")]
        [SerializeField] private bool defaultBool = true;
        [SerializeField] private int defaultInt;
        [SerializeField] private float defaultFloat = 1f;
        [SerializeField] private string defaultString = "";
        [SerializeField] private int defaultOptionIndex;

        [Header("Ranges")]
        [SerializeField] private int minInt;
        [SerializeField] private int maxInt = 10;
        [SerializeField] private int intStep = 1;
        [SerializeField] private float minFloat;
        [SerializeField] private float maxFloat = 1f;
        [SerializeField] private float floatStep = 0.1f;
        [SerializeField] private bool displayFloatAsPercentage;

        [Header("Options")]
        [SerializeField] private List<string> options = new();

        [Header("Button")]
        [SerializeField] private string buttonText;
        [SerializeField] private string buttonData;
        [SerializeField] private Color buttonColor = Color.white;
        [SerializeField] private Sprite buttonSprite;

        /// <summary>
        /// Уникальный ключ настройки.
        /// </summary>
        public string Key => key;

        /// <summary>
        /// Название настройки для сервисного UI.
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// Описание настройки для подсказок обслуживающему персоналу.
        /// </summary>
        public string Description => description;

        /// <summary>
        /// Группа настройки в сервисном меню.
        /// </summary>
        public string Group => group;

        /// <summary>
        /// Показывать ли настройку в сервисном меню.
        /// </summary>
        public bool ShownInServiceMenu => shownInServiceMenu;

        /// <summary>
        /// Можно ли менять настройку через внешний JSON.
        /// </summary>
        public bool ExposedForExternalChanges => exposedForExternalChanges;

        /// <summary>
        /// Тип значения настройки.
        /// </summary>
        public ServiceSettingValueType ValueType => valueType;

        /// <summary>
        /// Предпочитаемый вид UI-контрола для настройки.
        /// </summary>
        public ServiceSettingDisplayMode DisplayMode => displayMode;

        /// <summary>
        /// Минимальное int-значение с защитой от перепутанных границ.
        /// </summary>
        public int MinInt => Mathf.Min(minInt, maxInt);

        /// <summary>
        /// Максимальное int-значение с защитой от перепутанных границ.
        /// </summary>
        public int MaxInt => Mathf.Max(minInt, maxInt);

        /// <summary>
        /// Шаг изменения int-значения.
        /// </summary>
        public int IntStep => Mathf.Max(1, intStep);

        /// <summary>
        /// Минимальное float-значение с защитой от перепутанных границ.
        /// </summary>
        public float MinFloat => Mathf.Min(minFloat, maxFloat);

        /// <summary>
        /// Максимальное float-значение с защитой от перепутанных границ.
        /// </summary>
        public float MaxFloat => Mathf.Max(minFloat, maxFloat);

        /// <summary>
        /// Шаг изменения float-значения.
        /// </summary>
        public float FloatStep => Mathf.Max(0.0001f, floatStep);

        /// <summary>
        /// Отображать ли float-значение как проценты.
        /// </summary>
        public bool DisplayFloatAsPercentage => displayFloatAsPercentage;

        /// <summary>
        /// Список вариантов для option-настройки.
        /// </summary>
        public IReadOnlyList<string> Options => options;

        /// <summary>
        /// Текст кнопки действия. Если не задан, UI использует название настройки.
        /// </summary>
        public string ButtonText => buttonText;

        /// <summary>
        /// Дополнительные данные кнопки: URL, имя окна, идентификатор команды или другая payload-строка.
        /// </summary>
        public string ButtonData => buttonData;

        /// <summary>
        /// Цвет кнопки действия, перенесенный из legacy-базы.
        /// </summary>
        public Color ButtonColor => buttonColor;

        /// <summary>
        /// Опциональный спрайт кнопки действия.
        /// </summary>
        public Sprite ButtonSprite => buttonSprite;

        /// <summary>
        /// Значение по умолчанию, собранное из полей под выбранный тип.
        /// </summary>
        public ServiceSettingValue DefaultValue => valueType switch
        {
            ServiceSettingValueType.Bool => ServiceSettingValue.Bool(defaultBool),
            ServiceSettingValueType.Int => ServiceSettingValue.Int(defaultInt),
            ServiceSettingValueType.Float => ServiceSettingValue.Float(defaultFloat),
            ServiceSettingValueType.String => ServiceSettingValue.String(defaultString),
            ServiceSettingValueType.Button => ServiceSettingValue.Button(),
            ServiceSettingValueType.Option => ServiceSettingValue.Option(GetDefaultOptionIndex()),
            _ => default
        };

        /// <summary>
        /// Приводит входное значение к типу и диапазонам настройки.
        /// </summary>
        public ServiceSettingValue Sanitize(ServiceSettingValue value)
        {
            if (valueType == ServiceSettingValueType.Button)
            {
                return ServiceSettingValue.Button();
            }

            if (value.type != valueType && !(valueType == ServiceSettingValueType.Option && value.type == ServiceSettingValueType.Int))
            {
                return Sanitize(DefaultValue);
            }

            return valueType switch
            {
                ServiceSettingValueType.Bool => ServiceSettingValue.Bool(value.boolValue),
                ServiceSettingValueType.Int => ServiceSettingValue.Int(Mathf.Clamp(value.intValue, MinInt, MaxInt)),
                ServiceSettingValueType.Float => ServiceSettingValue.Float(Mathf.Clamp(value.floatValue, MinFloat, MaxFloat)),
                ServiceSettingValueType.String => ServiceSettingValue.String(value.stringValue),
                ServiceSettingValueType.Option => ServiceSettingValue.Option(SanitizeOptionIndex(value.intValue)),
                _ => DefaultValue
            };
        }

        /// <summary>
        /// Возвращает безопасный индекс варианта по умолчанию.
        /// </summary>
        private int GetDefaultOptionIndex()
        {
            return SanitizeOptionIndex(defaultOptionIndex);
        }

        /// <summary>
        /// Ограничивает option-index доступным диапазоном.
        /// </summary>
        private int SanitizeOptionIndex(int index)
        {
            return options is { Count: > 0 } ? Mathf.Clamp(index, 0, options.Count - 1) : 0;
        }
    }
}
