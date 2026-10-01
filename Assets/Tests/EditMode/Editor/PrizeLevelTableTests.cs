using DanroJump.Prizes;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class PrizeLevelTableTests
    {
        [Test]
        public void ResolveRewardKind_UsesHighestReachedScoreThreshold()
        {
            var table = new PrizeLevelTable();
            table.Upsert(100, PrizeRewardKind.Small);
            table.Upsert(300, PrizeRewardKind.Big);
            table.Upsert(200, PrizeRewardKind.None);

            Assert.That(table.ResolveRewardKind(99), Is.EqualTo(PrizeRewardKind.None));
            Assert.That(table.ResolveRewardKind(100), Is.EqualTo(PrizeRewardKind.Small));
            Assert.That(table.ResolveRewardKind(250), Is.EqualTo(PrizeRewardKind.None));
            Assert.That(table.ResolveRewardKind(300), Is.EqualTo(PrizeRewardKind.Big));
        }

        [Test]
        public void Upsert_ReplacesExistingScoreThreshold()
        {
            var table = new PrizeLevelTable();

            table.Upsert(100, PrizeRewardKind.Small);
            table.Upsert(100, PrizeRewardKind.Big);

            Assert.That(table.Count, Is.EqualTo(1));
            Assert.That(table.ResolveRewardKind(100), Is.EqualTo(PrizeRewardKind.Big));
        }

        [Test]
        public void ToJson_RoundTripsConfiguredLevels()
        {
            var table = new PrizeLevelTable();
            table.Upsert(300, PrizeRewardKind.Big);
            table.Upsert(100, PrizeRewardKind.Small);

            var restored = PrizeLevelTable.FromJson(table.ToJson());

            Assert.That(restored.Count, Is.EqualTo(2));
            Assert.That(restored.Levels[0].Score, Is.EqualTo(100));
            Assert.That(restored.Levels[1].RewardKind, Is.EqualTo(PrizeRewardKind.Big));
        }

        [Test]
        public void ResolveRewardKind_SupportsLegacyPrizeTypes()
        {
            var table = new PrizeLevelTable();
            table.Upsert(100, PrizeRewardKind.QRCode);
            table.Upsert(200, PrizeRewardKind.Hopper);
            table.Upsert(300, PrizeRewardKind.PrizeStand);

            Assert.That(table.ResolveRewardKind(100), Is.EqualTo(PrizeRewardKind.QRCode));
            Assert.That(table.ResolveRewardKind(200), Is.EqualTo(PrizeRewardKind.Hopper));
            Assert.That(table.ResolveRewardKind(300), Is.EqualTo(PrizeRewardKind.PrizeStand));
        }
    }
}
