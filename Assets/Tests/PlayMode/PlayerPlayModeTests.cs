using System.Collections.Generic;
using DanroJump.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace DanroJump.Tests.PlayMode
{
    public sealed class PlayerPlayModeTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = createdObjects.Count - 1; index >= 0; index--)
            {
                var createdObject = createdObjects[index];
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void Bounce_RaisesVerticalVelocityWithoutLoweringExistingVelocity()
        {
            var player = CreatePlayer(out var body);
            body.linearVelocity = new Vector2(0f, 20f);

            player.Bounce(10f);
            Assert.That(player.VerticalVelocity, Is.EqualTo(20f).Within(0.0001f));

            player.Bounce(30f);
            Assert.That(player.VerticalVelocity, Is.EqualTo(30f).Within(0.0001f));
        }

        [Test]
        public void Kill_ThenRespawn_RaisesLifecycleEventsAndRestoresAliveState()
        {
            var player = CreatePlayer(out _);
            var diedEvents = 0;
            var respawnedEvents = 0;
            player.Died += _ => diedEvents++;
            player.Respawned += _ => respawnedEvents++;

            player.Kill();

            Assert.That(player.IsDead, Is.True);
            Assert.That(diedEvents, Is.EqualTo(1));

            player.Respawn();

            Assert.That(player.IsDead, Is.False);
            Assert.That(respawnedEvents, Is.EqualTo(1));
        }

        [Test]
        public void BlockPendingRespawn_PreventsManualRespawnAfterDeath()
        {
            var player = CreatePlayer(out _);

            player.Kill();
            player.BlockPendingRespawn();
            player.Respawn();

            Assert.That(player.IsDead, Is.True);
        }

        [Test]
        public void AllowRespawn_ReenablesManualRespawnAfterBlock()
        {
            var player = CreatePlayer(out _);

            player.Kill();
            player.BlockPendingRespawn();
            player.AllowRespawn();
            player.Respawn();

            Assert.That(player.IsDead, Is.False);
        }

        [Test]
        public void ApplyHeightLift_ActivatesEnvironmentIgnoreAndSetsLiftVelocity()
        {
            var player = CreatePlayer(out _);

            player.ApplyHeightLift(12f, 3f);

            Assert.That(player.IsHeightLiftActive, Is.True);
            Assert.That(player.IgnoresEnvironmentInteractions, Is.True);
            Assert.That(player.HeightLiftTargetY, Is.EqualTo(player.transform.position.y + 3f).Within(0.0001f));
            Assert.That(player.VerticalVelocity, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(player.IgnoresEnemyContacts, Is.True);
        }

        [Test]
        public void PlayerContactUtility_RejectsContactsWhileLiftIsActiveOrPlayerIsDead()
        {
            var player = CreatePlayer(out _);
            var source = CreateChildContactSource(player.transform);

            Assert.That(PlayerContactUtility.TryGetAlivePlayer(source, out var alivePlayer), Is.True);
            Assert.That(alivePlayer, Is.SameAs(player));

            player.ApplyHeightLift(10f, 2f);
            Assert.That(PlayerContactUtility.TryGetAlivePlayer(source, out _), Is.False);

            player.Kill();
            Assert.That(PlayerContactUtility.TryGetAlivePlayer(source, out _), Is.False);
        }

        [Test]
        public void Kill_ClearsTemporaryLiftImmediately()
        {
            var player = CreatePlayer(out var body);

            player.ApplyHeightLift(10f, 2f);
            Assert.That(player.IsHeightLiftActive, Is.True);
            Assert.That(player.IgnoresEnemyContacts, Is.True);

            player.Kill();

            Assert.That(player.IsDead, Is.True);
            Assert.That(player.IsHeightLiftActive, Is.False);
            Assert.That(player.IgnoresEnemyContacts, Is.False);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        }

        private JumpPlayerController CreatePlayer(out Rigidbody2D body)
        {
            var playerObject = new GameObject("PlayMode Test Player");
            createdObjects.Add(playerObject);
            playerObject.transform.position = new Vector3(0f, 1f, 0f);

            var visualRoot = new GameObject("Body");
            visualRoot.transform.SetParent(playerObject.transform, false);
            visualRoot.AddComponent<SpriteRenderer>();

            var groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(playerObject.transform, false);
            groundCheck.transform.localPosition = new Vector3(0f, -0.5f, 0f);

            body = playerObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            playerObject.AddComponent<BoxCollider2D>();

            var player = playerObject.AddComponent<JumpPlayerController>();
            player.Construct(CreateCamera());
            return player;
        }

        private Component CreateChildContactSource(Transform playerRoot)
        {
            var sourceObject = new GameObject("Contact Source");
            sourceObject.transform.SetParent(playerRoot, false);
            return sourceObject.AddComponent<BoxCollider2D>();
        }

        private Camera CreateCamera()
        {
            var cameraObject = new GameObject("PlayMode Test Camera");
            createdObjects.Add(cameraObject);

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }
    }
}
