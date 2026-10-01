using System.IO;
using UnityEditor;
using UnityEngine;

namespace DanroJump.Editor
{
    public static class VfxGeneratorEditor
    {
        private const string VfxFolderPath = "Assets/Resources/Vfx";
        private const string MaterialPath = VfxFolderPath + "/ParticleDefaultMaterial.mat";

        private static readonly string[] PrefabNames = {
            "VFX_Jump",
            "VFX_CoinCollected",
            "VFX_PlayerDeath",
            "VFX_PrizeWin",
            "VFX_ComError"
        };

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            // Запускаем генерацию только если хотя бы одного файла не хватает
            bool missing = false;
            foreach (var name in PrefabNames)
            {
                if (!File.Exists($"{VfxFolderPath}/{name}.prefab"))
                {
                    missing = true;
                    break;
                }
            }

            if (missing)
            {
                GenerateAssets();
            }
        }

        [MenuItem("Tools/Generate VFX Assets")]
        public static void GenerateAssets()
        {
            if (!Directory.Exists(VfxFolderPath))
            {
                Directory.CreateDirectory(VfxFolderPath);
                AssetDatabase.Refresh();
            }

            var material = GetOrCreateParticleMaterial();
            if (material == null)
            {
                Debug.LogError("[VfxGenerator] Failed to find or create a compatible particle material.");
                return;
            }

            foreach (var name in PrefabNames)
            {
                var go = new GameObject(name);
                var ps = go.AddComponent<ParticleSystem>();

                // Настраиваем рендерер и материал
                var psr = go.GetComponent<ParticleSystemRenderer>();
                if (psr != null)
                {
                    psr.sharedMaterial = material;
                    psr.sortingLayerName = "Vfx";
                }

                // Общая базовая настройка
                var main = ps.main;
                main.loop = false;
                main.playOnAwake = false;

                var emission = ps.emission;
                emission.rateOverTime = 0;

                var colorOverLifetime = ps.colorOverLifetime;
                colorOverLifetime.enabled = true;

                // Специфические настройки для каждого эффекта
                switch (name)
                {
                    case "VFX_Jump":
                        main.startColor = new Color(0.9f, 0.9f, 0.9f, 0.5f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
                        main.duration = 0.25f;
                        main.gravityModifier = 0.6f;

                        var shapeCone = ps.shape;
                        shapeCone.shapeType = ParticleSystemShapeType.Cone;
                        shapeCone.angle = 35f;
                        shapeCone.radius = 0.15f;
                        shapeCone.rotation = new Vector3(90f, 0f, 0f);

                        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });

                        var gradientJump = new Gradient();
                        gradientJump.SetKeys(
                            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                            new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) }
                        );
                        colorOverLifetime.color = gradientJump;
                        break;

                    case "VFX_CoinCollected":
                        main.startColor = new Color(1.0f, 0.85f, 0.0f, 0.9f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
                        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
                        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                        main.duration = 0.3f;
                        main.gravityModifier = -0.2f;

                        var shapeSphereCoin = ps.shape;
                        shapeSphereCoin.shapeType = ParticleSystemShapeType.Sphere;
                        shapeSphereCoin.radius = 0.1f;

                        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 18) });

                        var gradientCoin = new Gradient();
                        gradientCoin.SetKeys(
                            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0f), 1f) },
                            new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
                        );
                        colorOverLifetime.color = gradientCoin;
                        break;

                    case "VFX_PlayerDeath":
                        main.startColor = new Color(0.8f, 0.1f, 0.1f, 0.8f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
                        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                        main.duration = 0.5f;
                        main.gravityModifier = 0.8f;

                        var shapeSphereDeath = ps.shape;
                        shapeSphereDeath.shapeType = ParticleSystemShapeType.Sphere;
                        shapeSphereDeath.radius = 0.25f;

                        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 35) });

                        var gradientDeath = new Gradient();
                        gradientDeath.SetKeys(
                            new GradientColorKey[] { new GradientColorKey(new Color(0.9f, 0.2f, 0.2f), 0f), new GradientColorKey(new Color(0.3f, 0.3f, 0.3f), 1f) },
                            new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
                        );
                        colorOverLifetime.color = gradientDeath;
                        break;

                    case "VFX_PrizeWin":
                        main.startColor = new ParticleSystem.MinMaxGradient(
                            new Color(0.2f, 0.6f, 1.0f),
                            new Color(1.0f, 0.2f, 0.6f)
                        );
                        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
                        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                        main.duration = 0.8f;
                        main.gravityModifier = 0.4f;

                        var shapeHemiPrize = ps.shape;
                        shapeHemiPrize.shapeType = ParticleSystemShapeType.Hemisphere;
                        shapeHemiPrize.radius = 0.3f;
                        shapeHemiPrize.rotation = new Vector3(-90f, 0f, 0f);

                        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 50) });

                        var gradientPrize = new Gradient();
                        gradientPrize.SetKeys(
                            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                            new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
                        );
                        colorOverLifetime.color = gradientPrize;
                        break;

                    case "VFX_ComError":
                        main.startColor = new Color(1.0f, 0.3f, 0.0f, 0.9f);
                        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
                        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
                        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                        main.duration = 0.4f;
                        main.gravityModifier = 0.3f;

                        var shapeConeError = ps.shape;
                        shapeConeError.shapeType = ParticleSystemShapeType.Cone;
                        shapeConeError.angle = 60f;
                        shapeConeError.radius = 0.1f;

                        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });

                        var gradientError = new Gradient();
                        gradientError.SetKeys(
                            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.5f, 0f), 0f), new GradientColorKey(new Color(0.8f, 0.1f, 0f), 1f) },
                            new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
                        );
                        colorOverLifetime.color = gradientError;
                        break;
                }

                // Сохраняем как префаб
                var localPath = $"{VfxFolderPath}/{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(go, localPath);
                Object.DestroyImmediate(go);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[VfxGenerator] All particle prefabs generated successfully under Assets/Resources/Vfx!");
        }

        private static Material GetOrCreateParticleMaterial()
        {
            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existingMat != null)
            {
                return existingMat;
            }

            var shader = Shader.Find("Universal Render Pipeline/Particles/2D Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader);
            var tex = Resources.GetBuiltinResource<Texture2D>("Default-Particle.tif");
            if (tex != null)
            {
                material.mainTexture = tex;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", tex);
                }
            }

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
