using System.Collections.Generic;
using DanroJump.Gameplay;
using DanroJump.Vfx;
using NUnit.Framework;
using UnityEngine;

namespace DanroJump.Tests.EditMode
{
    /// <summary>
    /// Тесты для службы визуальных эффектов IVfxService и GameVfxService.
    /// </summary>
    public sealed class VfxServiceTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in createdObjects)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }
            createdObjects.Clear();
        }

        [Test]
        public void VfxService_NullSafety_PlayerAndCollectibleDoNotThrowWhenVfxServiceIsNull()
        {
            // Тест Null-safety для игрока
            var playerObj = new GameObject("TestPlayer");
            createdObjects.Add(playerObj);
            var body = playerObj.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            playerObj.AddComponent<BoxCollider2D>();
            
            var playerVisual = new GameObject("Body");
            playerVisual.transform.SetParent(playerObj.transform);
            playerVisual.AddComponent<SpriteRenderer>();

            var groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(playerObj.transform);
            groundCheck.transform.localPosition = new Vector3(0f, -0.5f, 0f);

            var player = playerObj.AddComponent<JumpPlayerController>();
            
            var camObj = new GameObject("TestCam");
            createdObjects.Add(camObj);
            var camera = camObj.AddComponent<Camera>();

            // Внедряем null в качестве IVfxService
            player.Construct(camera, null, null);

            // Проверяем, что вызовы Bounce и Die не выбрасывают NullReferenceException
            Assert.DoesNotThrow(() => player.Bounce(10f));
            Assert.DoesNotThrow(() => player.Kill());

            // Тест Null-safety для CoinCollectible
            var coinObj = new GameObject("TestCoin");
            createdObjects.Add(coinObj);
            coinObj.AddComponent<BoxCollider2D>();
            var coin = coinObj.AddComponent<CoinCollectible>();
            coin.Construct(null, null, null);

            // Имитируем сбор монеты
            var method = typeof(CoinCollectible).GetMethod("OnCollected", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            Assert.DoesNotThrow(() => method.Invoke(coin, new object[] { player }));
        }

        [Test]
        public void GameVfxService_SpawnMethods_CreateParticleSystemGameObjects()
        {
            var vfxService = new GameVfxService();
            var spawnPosition = new Vector3(1f, 2f, 3f);

            // 1. Тест SpawnJumpEffect
            vfxService.SpawnJumpEffect(spawnPosition);
            var jumpVfx = GameObject.Find("VFX_Jump");
            Assert.That(jumpVfx, Is.Not.Null);
            createdObjects.Add(jumpVfx);
            Assert.That(jumpVfx.transform.position, Is.EqualTo(spawnPosition));
            Assert.That(jumpVfx.GetComponent<ParticleSystem>(), Is.Not.Null);

            // 2. Тест SpawnCoinCollectedEffect
            vfxService.SpawnCoinCollectedEffect(spawnPosition);
            var coinVfx = GameObject.Find("VFX_CoinCollected");
            Assert.That(coinVfx, Is.Not.Null);
            createdObjects.Add(coinVfx);
            Assert.That(coinVfx.transform.position, Is.EqualTo(spawnPosition));
            Assert.That(coinVfx.GetComponent<ParticleSystem>(), Is.Not.Null);

            // 3. Тест SpawnPlayerDeathEffect
            vfxService.SpawnPlayerDeathEffect(spawnPosition);
            var deathVfx = GameObject.Find("VFX_PlayerDeath");
            Assert.That(deathVfx, Is.Not.Null);
            createdObjects.Add(deathVfx);
            Assert.That(deathVfx.transform.position, Is.EqualTo(spawnPosition));
            Assert.That(deathVfx.GetComponent<ParticleSystem>(), Is.Not.Null);

            // 4. Тест SpawnPrizeEffect
            vfxService.SpawnPrizeEffect(spawnPosition);
            var prizeVfx = GameObject.Find("VFX_PrizeWin");
            Assert.That(prizeVfx, Is.Not.Null);
            createdObjects.Add(prizeVfx);
            Assert.That(prizeVfx.transform.position, Is.EqualTo(spawnPosition));
            Assert.That(prizeVfx.GetComponent<ParticleSystem>(), Is.Not.Null);

            // 5. Тест SpawnComErrorEffect
            vfxService.SpawnComErrorEffect(spawnPosition);
            var comErrorVfx = GameObject.Find("VFX_ComError");
            Assert.That(comErrorVfx, Is.Not.Null);
            createdObjects.Add(comErrorVfx);
            Assert.That(comErrorVfx.transform.position, Is.EqualTo(spawnPosition));
            Assert.That(comErrorVfx.GetComponent<ParticleSystem>(), Is.Not.Null);
        }
    }
}
