using DanroJump.UI.ArcadeInput;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class ArcadeStringInputStateTests
    {
        [Test]
        public void Constructor_KeepsOnlyAllowedCharactersAndAppliesMaxLength()
        {
            var profile = new ArcadeStringInputProfile(" ABC", 3);

            var state = new ArcadeStringInputState(profile, "A!BC");

            Assert.That(state.Value, Is.EqualTo("ABC"));
        }

        [Test]
        public void Constructor_NormalizesEmptySymbolsDuplicatesAndInvalidLength()
        {
            var profile = new ArcadeStringInputProfile("1122", 0);

            var state = new ArcadeStringInputState(profile, "2211");

            Assert.That(profile.AllowedSymbols, Is.EqualTo("12"));
            Assert.That(state.MaxLength, Is.EqualTo(1));
            Assert.That(state.Value, Is.EqualTo("2"));
        }

        [Test]
        public void TemplateProfile_KeepsQrPlaceholdersAndPunctuation()
        {
            var state = new ArcadeStringInputState(
                ArcadeStringInputProfile.Template(),
                "QR! Score: {score}. Height: {height}");

            Assert.That(state.Value, Is.EqualTo("QR! Score: {score}. Height: {height}"));
        }

        [Test]
        public void MoveUpAndDown_CyclesCurrentCharacter()
        {
            var state = new ArcadeStringInputState(new ArcadeStringInputProfile(" ABC", 4), "");

            state.MoveDown();
            Assert.That(state.Value, Is.EqualTo("A"));

            state.MoveUp();
            Assert.That(state.Value, Is.EqualTo(""));
        }

        [Test]
        public void SubmitOnCharacter_MovesCursorRightUntilActions()
        {
            var state = new ArcadeStringInputState(new ArcadeStringInputProfile(" 12", 2), "");

            Assert.That(state.SelectionKind, Is.EqualTo(ArcadeStringInputSelectionKind.Character));
            Assert.That(state.SelectedSlot, Is.EqualTo(0));

            state.Submit();
            Assert.That(state.SelectionKind, Is.EqualTo(ArcadeStringInputSelectionKind.Character));
            Assert.That(state.SelectedSlot, Is.EqualTo(1));

            state.Submit();
            Assert.That(state.SelectionKind, Is.EqualTo(ArcadeStringInputSelectionKind.Action));
            Assert.That(state.SelectedAction, Is.EqualTo(ArcadeStringInputAction.Confirm));
        }

        [Test]
        public void SubmitOnDeleteAction_ClearsSelectedSlotWithoutConfirming()
        {
            var state = new ArcadeStringInputState(new ArcadeStringInputProfile(" 12", 2), "12");

            state.MoveRight();
            state.MoveRight();
            state.MoveDown();
            var result = state.Submit();

            Assert.That(result, Is.EqualTo(ArcadeStringInputSubmitResult.None));
            Assert.That(state.SelectionKind, Is.EqualTo(ArcadeStringInputSelectionKind.Character));
            Assert.That(state.SelectedSlot, Is.EqualTo(1));
            Assert.That(state.Value, Is.EqualTo("1"));
        }

        [Test]
        public void SubmitOnConfirmAndCancel_ReturnsTerminalResults()
        {
            var confirmState = new ArcadeStringInputState(new ArcadeStringInputProfile(" 12", 1), "1");
            confirmState.MoveRight();

            Assert.That(confirmState.Submit(), Is.EqualTo(ArcadeStringInputSubmitResult.Confirm));

            var cancelState = new ArcadeStringInputState(new ArcadeStringInputProfile(" 12", 1), "1");
            cancelState.MoveRight();
            cancelState.MoveDown();
            cancelState.MoveDown();

            Assert.That(cancelState.Submit(), Is.EqualTo(ArcadeStringInputSubmitResult.Cancel));
        }

        [Test]
        public void DeleteCurrentCharacter_DoesNotChangeOtherSlots()
        {
            var state = new ArcadeStringInputState(new ArcadeStringInputProfile(" ABC", 3), "ABC");

            state.MoveRight();
            state.DeleteCurrentCharacter();

            Assert.That(state.GetSlot(0), Is.EqualTo('A'));
            Assert.That(state.GetSlot(1), Is.EqualTo(ArcadeStringInputState.BlankCharacter));
            Assert.That(state.GetSlot(2), Is.EqualTo('C'));
            Assert.That(state.Value, Is.EqualTo("A C"));
        }

        [Test]
        public void Value_TrimsOnlyTrailingBlanks()
        {
            var state = new ArcadeStringInputState(new ArcadeStringInputProfile(" ABC", 3), "A C");

            Assert.That(state.Value, Is.EqualTo("A C"));
        }

        [Test]
        public void MoveLeftFromActions_ReturnsToLastSlot()
        {
            var state = new ArcadeStringInputState(new ArcadeStringInputProfile(" 12", 2), "12");

            state.MoveRight();
            state.MoveRight();
            state.MoveLeft();

            Assert.That(state.SelectionKind, Is.EqualTo(ArcadeStringInputSelectionKind.Character));
            Assert.That(state.SelectedSlot, Is.EqualTo(1));
        }

        [Test]
        public void MoveRight_CyclesAcrossSlotsActionsAndBackToFirstSlot()
        {
            var state = new ArcadeStringInputState(new ArcadeStringInputProfile(" 12", 2), "12");

            state.MoveRight();
            Assert.That(state.SelectedSlot, Is.EqualTo(1));

            state.MoveRight();
            Assert.That(state.SelectionKind, Is.EqualTo(ArcadeStringInputSelectionKind.Action));
            Assert.That(state.SelectedAction, Is.EqualTo(ArcadeStringInputAction.Confirm));

            state.MoveRight();
            Assert.That(state.SelectedAction, Is.EqualTo(ArcadeStringInputAction.Delete));

            state.MoveRight();
            Assert.That(state.SelectedAction, Is.EqualTo(ArcadeStringInputAction.Cancel));

            state.MoveRight();
            Assert.That(state.SelectionKind, Is.EqualTo(ArcadeStringInputSelectionKind.Character));
            Assert.That(state.SelectedSlot, Is.EqualTo(0));
        }

        [Test]
        public void MoveLeft_CyclesAcrossSlotsActionsAndBackToLastSlot()
        {
            var state = new ArcadeStringInputState(new ArcadeStringInputProfile(" 12", 2), "12");

            state.MoveLeft();
            Assert.That(state.SelectionKind, Is.EqualTo(ArcadeStringInputSelectionKind.Action));
            Assert.That(state.SelectedAction, Is.EqualTo(ArcadeStringInputAction.Cancel));

            state.MoveLeft();
            Assert.That(state.SelectedAction, Is.EqualTo(ArcadeStringInputAction.Delete));

            state.MoveLeft();
            Assert.That(state.SelectedAction, Is.EqualTo(ArcadeStringInputAction.Confirm));

            state.MoveLeft();
            Assert.That(state.SelectionKind, Is.EqualTo(ArcadeStringInputSelectionKind.Character));
            Assert.That(state.SelectedSlot, Is.EqualTo(1));
        }
    }
}
