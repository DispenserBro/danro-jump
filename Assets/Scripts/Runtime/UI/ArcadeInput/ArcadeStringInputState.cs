using System;
using System.Collections.Generic;

namespace DanroJump.UI.ArcadeInput
{
    /// <summary>
    /// Набор правил для ввода строки автоматным управлением: джойстик плюс одна кнопка.
    /// </summary>
    public readonly struct ArcadeStringInputProfile
    {
        public const string PhoneSymbols = " +0123456789()-";
        public const string ShortTextSymbols = " 0123456789+-.,:/()ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        public const string TemplateSymbols = ShortTextSymbols + "{}_!?@#%&=*\"'";

        public ArcadeStringInputProfile(string allowedSymbols, int maxLength)
        {
            AllowedSymbols = NormalizeSymbols(allowedSymbols);
            MaxLength = Math.Max(1, maxLength);
        }

        public string AllowedSymbols { get; }
        public int MaxLength { get; }

        public static ArcadeStringInputProfile Phone(int maxLength = 18)
        {
            return new ArcadeStringInputProfile(PhoneSymbols, maxLength);
        }

        public static ArcadeStringInputProfile ShortText(int maxLength = 12)
        {
            return new ArcadeStringInputProfile(ShortTextSymbols, maxLength);
        }

        public static ArcadeStringInputProfile Text(int maxLength = 28)
        {
            return new ArcadeStringInputProfile(ShortTextSymbols, maxLength);
        }

        public static ArcadeStringInputProfile Template(int maxLength = 96)
        {
            return new ArcadeStringInputProfile(TemplateSymbols, maxLength);
        }

        private static string NormalizeSymbols(string symbols)
        {
            if (string.IsNullOrEmpty(symbols))
            {
                return " ";
            }

            var seen = new HashSet<char>();
            var result = new List<char>(symbols.Length);
            foreach (var symbol in symbols)
            {
                if (seen.Add(symbol))
                {
                    result.Add(symbol);
                }
            }

            return result.Count > 0 ? new string(result.ToArray()) : " ";
        }
    }

    public enum ArcadeStringInputSelectionKind
    {
        Character,
        Action
    }

    public enum ArcadeStringInputAction
    {
        Confirm,
        Delete,
        Cancel
    }

    public enum ArcadeStringInputSubmitResult
    {
        None,
        Confirm,
        Cancel
    }

    /// <summary>
    /// Чистая модель посимвольного ввода для автоматов без физической клавиатуры.
    /// </summary>
    public sealed class ArcadeStringInputState
    {
        public const char BlankCharacter = ' ';

        private static readonly ArcadeStringInputAction[] ActionOrder =
        {
            ArcadeStringInputAction.Confirm,
            ArcadeStringInputAction.Delete,
            ArcadeStringInputAction.Cancel
        };

        private readonly char[] slots;
        private readonly char[] symbols;
        private int selectedSlot;
        private int selectedActionIndex;
        private ArcadeStringInputSelectionKind selectionKind;

        public ArcadeStringInputState(ArcadeStringInputProfile profile, string initialValue)
        {
            MaxLength = Math.Max(1, profile.MaxLength);
            symbols = profile.AllowedSymbols.ToCharArray();
            if (symbols.Length == 0)
            {
                symbols = new[] { BlankCharacter };
            }

            slots = new char[MaxLength];
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index] = BlankCharacter;
            }

            SetInitialValue(initialValue);
        }

        public int MaxLength { get; }
        public int SelectedSlot => selectedSlot;
        public ArcadeStringInputSelectionKind SelectionKind => selectionKind;
        public ArcadeStringInputAction SelectedAction => ActionOrder[selectedActionIndex];

        public string Value => new string(slots).TrimEnd();

        public char GetSlot(int index)
        {
            return index >= 0 && index < slots.Length ? slots[index] : BlankCharacter;
        }

        public bool IsSlotSelected(int index)
        {
            return selectionKind == ArcadeStringInputSelectionKind.Character && selectedSlot == index;
        }

        public bool IsActionSelected(ArcadeStringInputAction action)
        {
            return selectionKind == ArcadeStringInputSelectionKind.Action && SelectedAction == action;
        }

        public void MoveLeft()
        {
            if (selectionKind == ArcadeStringInputSelectionKind.Action)
            {
                if (selectedActionIndex > 0)
                {
                    selectedActionIndex--;
                    return;
                }

                selectionKind = ArcadeStringInputSelectionKind.Character;
                selectedSlot = MaxLength - 1;
                return;
            }

            if (selectedSlot <= 0)
            {
                selectionKind = ArcadeStringInputSelectionKind.Action;
                selectedActionIndex = ActionOrder.Length - 1;
                return;
            }

            selectedSlot--;
        }

        public void MoveRight()
        {
            if (selectionKind == ArcadeStringInputSelectionKind.Action)
            {
                if (selectedActionIndex < ActionOrder.Length - 1)
                {
                    selectedActionIndex++;
                    return;
                }

                selectionKind = ArcadeStringInputSelectionKind.Character;
                selectedSlot = 0;
                return;
            }

            if (selectedSlot >= MaxLength - 1)
            {
                selectionKind = ArcadeStringInputSelectionKind.Action;
                selectedActionIndex = 0;
                return;
            }

            selectedSlot++;
        }

        public void MoveUp()
        {
            if (selectionKind == ArcadeStringInputSelectionKind.Action)
            {
                selectedActionIndex = Wrap(selectedActionIndex - 1, ActionOrder.Length);
                return;
            }

            CycleSelectedCharacter(-1);
        }

        public void MoveDown()
        {
            if (selectionKind == ArcadeStringInputSelectionKind.Action)
            {
                selectedActionIndex = Wrap(selectedActionIndex + 1, ActionOrder.Length);
                return;
            }

            CycleSelectedCharacter(1);
        }

        public ArcadeStringInputSubmitResult Submit()
        {
            if (selectionKind == ArcadeStringInputSelectionKind.Character)
            {
                MoveRight();
                return ArcadeStringInputSubmitResult.None;
            }

            switch (SelectedAction)
            {
                case ArcadeStringInputAction.Confirm:
                    return ArcadeStringInputSubmitResult.Confirm;
                case ArcadeStringInputAction.Cancel:
                    return ArcadeStringInputSubmitResult.Cancel;
                case ArcadeStringInputAction.Delete:
                    DeleteCurrentCharacter();
                    return ArcadeStringInputSubmitResult.None;
                default:
                    return ArcadeStringInputSubmitResult.None;
            }
        }

        public void DeleteCurrentCharacter()
        {
            slots[selectedSlot] = BlankCharacter;
            selectionKind = ArcadeStringInputSelectionKind.Character;
        }

        private void SetInitialValue(string initialValue)
        {
            if (string.IsNullOrEmpty(initialValue))
            {
                return;
            }

            var writeIndex = 0;
            foreach (var symbol in initialValue)
            {
                if (writeIndex >= slots.Length)
                {
                    break;
                }

                if (Array.IndexOf(symbols, symbol) < 0)
                {
                    continue;
                }

                slots[writeIndex] = symbol;
                writeIndex++;
            }
        }

        private void CycleSelectedCharacter(int direction)
        {
            var symbolIndex = Array.IndexOf(symbols, slots[selectedSlot]);
            if (symbolIndex < 0)
            {
                symbolIndex = 0;
            }

            slots[selectedSlot] = symbols[Wrap(symbolIndex + direction, symbols.Length)];
        }

        private static int Wrap(int value, int length)
        {
            if (length <= 0)
            {
                return 0;
            }

            var wrapped = value % length;
            return wrapped < 0 ? wrapped + length : wrapped;
        }
    }
}
