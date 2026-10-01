using DanroJump.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace DanroJump.Tests.EditMode
{
    public sealed class GameplaySessionScoreTests
    {
        [Test]
        public void Score_UsesAddedScoreWhenHeightIsZero()
        {
            using var fixture = new PlayerSessionFixture(0f);

            fixture.Session.AddScore(7);

            Assert.That(fixture.Session.HeightScore, Is.EqualTo(0));
            Assert.That(fixture.Session.Score, Is.EqualTo(7));
        }

        [Test]
        public void HeightScore_AddsOnePointForEachFullTenMeters()
        {
            using var fixture = new PlayerSessionFixture(29.9f);

            Assert.That(fixture.Session.HeightScore, Is.EqualTo(2));
            Assert.That(fixture.Session.Score, Is.EqualTo(2));
        }

        [Test]
        public void HeightScore_IncludesExactTenMeterBoundaries()
        {
            using var fixture = new PlayerSessionFixture(30f);

            Assert.That(fixture.Session.HeightScore, Is.EqualTo(3));
            Assert.That(fixture.Session.Score, Is.EqualTo(3));
        }

        [Test]
        public void Score_CombinesAddedScoreAndHeightScore()
        {
            using var fixture = new PlayerSessionFixture(42.4f);

            fixture.Session.AddScore(11);

            Assert.That(fixture.Session.HeightScore, Is.EqualTo(4));
            Assert.That(fixture.Session.Score, Is.EqualTo(15));
        }

        private sealed class PlayerSessionFixture : System.IDisposable
        {
            private readonly GameObject playerObject;

            public PlayerSessionFixture(float highestY)
            {
                playerObject = new GameObject("TestPlayer");
                playerObject.AddComponent<Rigidbody2D>();
                playerObject.AddComponent<BoxCollider2D>();
                Player = playerObject.AddComponent<JumpPlayerController>();
                TestObjectFactory.SetPrivateField(Player, "<HighestY>k__BackingField", highestY);
                Session = new GameplaySession(Player);
            }

            public JumpPlayerController Player { get; }
            public GameplaySession Session { get; }

            public void Dispose()
            {
                Session.Dispose();
                Object.DestroyImmediate(playerObject);
            }
        }
    }
}
