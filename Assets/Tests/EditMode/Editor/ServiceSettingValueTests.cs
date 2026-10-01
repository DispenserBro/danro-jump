using DanroJump.Settings;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class ServiceSettingValueTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void Bool_RoundTripsThroughCompactFormat(bool value)
        {
            var original = ServiceSettingValue.Bool(value);

            var restored = ServiceSettingValue.FromCompact(original.ToCompact());

            Assert.That(restored, Is.EqualTo(original));
        }

        [TestCase(42)]
        [TestCase(-7)]
        public void Int_RoundTripsThroughCompactFormat(int value)
        {
            var original = ServiceSettingValue.Int(value);

            var restored = ServiceSettingValue.FromCompact(original.ToCompact());

            Assert.That(restored, Is.EqualTo(original));
        }

        [Test]
        public void InvalidCompactNumber_FallsBackToZero()
        {
            var compact = new ServiceSettingValueCompact
            {
                type = (int)ServiceSettingValueType.Float,
                value = "not-a-number",
            };

            var restored = ServiceSettingValue.FromCompact(compact);

            Assert.That(restored.type, Is.EqualTo(ServiceSettingValueType.Float));
            Assert.That(restored.floatValue, Is.EqualTo(0f));
        }
    }
}
