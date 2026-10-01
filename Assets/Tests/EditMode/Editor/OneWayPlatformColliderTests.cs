using DanroJump.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace DanroJump.Tests.EditMode
{
    public sealed class OneWayPlatformColliderTests
    {
        private GameObject playerObject;
        private GameObject platformObject;

        [TearDown]
        public void TearDown()
        {
            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }

            if (platformObject != null)
            {
                Object.DestroyImmediate(platformObject);
            }
        }

        [Test]
        public void ShouldCollideWith_ReturnsFalseWhenPlayerBottomIsBelowPlatformTop()
        {
            var player = CreatePlayer(centerY: 0.45f, previousGroundCheckY: 0.5f);
            var oneWayPlatform = CreatePlatform(topY: 0f);

            Assert.That(oneWayPlatform.ShouldCollideWith(player), Is.False);
        }

        [Test]
        public void ShouldCollideWith_ReturnsTrueWhenPlayerBottomIsAtPlatformTop()
        {
            var player = CreatePlayer(centerY: 0.5f, previousGroundCheckY: 0.5f);
            var oneWayPlatform = CreatePlatform(topY: 0f);

            Assert.That(oneWayPlatform.ShouldCollideWith(player), Is.True);
        }

        private JumpPlayerController CreatePlayer(float centerY, float previousGroundCheckY)
        {
            playerObject = new GameObject("OneWay Platform Test Player");
            playerObject.transform.position = new Vector3(0f, centerY, 0f);

            var groundCheckObject = new GameObject("GroundCheck");
            groundCheckObject.transform.SetParent(playerObject.transform, false);
            groundCheckObject.transform.localPosition = new Vector3(0f, -0.5f, 0f);

            var body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.down;
            playerObject.AddComponent<BoxCollider2D>();

            var player = playerObject.AddComponent<JumpPlayerController>();
            TestObjectFactory.SetPrivateField(player, "<PreviousGroundCheckY>k__BackingField", previousGroundCheckY);
            TestObjectFactory.SetPrivateField(player, "<PreviousVerticalVelocity>k__BackingField", -1f);
            return player;
        }

        private OneWayPlatformCollider CreatePlatform(float topY)
        {
            platformObject = new GameObject("OneWay Platform Test Platform");
            platformObject.transform.position = Vector3.zero;

            var platformCollider = platformObject.AddComponent<BoxCollider2D>();
            platformCollider.size = new Vector2(2f, 0.2f);
            platformCollider.offset = new Vector2(0f, topY - platformCollider.size.y * 0.5f);

            var oneWayPlatform = platformObject.AddComponent<OneWayPlatformCollider>();
            oneWayPlatform.Construct(playerObject.GetComponent<JumpPlayerController>());
            oneWayPlatform.UpdateCachedTopY();
            return oneWayPlatform;
        }
    }
}
