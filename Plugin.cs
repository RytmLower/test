using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections;
using UnityEngine;

namespace SilkWeaverCompanion
{
    [BepInPlugin("com.yourname.silkweavercompanion", "Silk Weaver Companion", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.yourname.silkweavercompanion";
        public const string PLUGIN_NAME = "Silk Weaver Companion";
        public const string PLUGIN_VERSION = "1.0.0";

        public static ManualLogSource Log;
        private Harmony harmony;

        // Конфигурационные параметры
        public static ConfigEntry<float> PickupRadius;
        public static ConfigEntry<float> FollowSpeed;
        public static ConfigEntry<KeyCode> ToggleKey;

        private GameObject activeCompanion;

        private void Awake()
        {
            Log = Logger;

            // 1. Инициализация конфигурации
            PickupRadius = Config.Bind("General", "PickupRadius", 8.0f, "Радиус автосбора Гео вокруг компаньона");
            FollowSpeed = Config.Bind("General", "FollowSpeed", 5.0f, "Скорость следования компаньона за Хорнет");
            ToggleKey = Config.Bind("General", "ToggleKey", KeyCode.F5, "Клавиша спавна/перезагрузки компаньона");

            // 2. Применение Harmony патчей
            harmony = new Harmony(PLUGIN_GUID);
            harmony.PatchAll();

            Log.LogInfo($"Плагин {PLUGIN_NAME} версии {PLUGIN_VERSION} успешно загружен!");
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey.Value))
            {
                if (activeCompanion == null)
                {
                    activeCompanion = new GameObject("SilkWeaverCompanion");
                    DontDestroyOnLoad(activeCompanion);
                    activeCompanion.AddComponent<CompanionBehaviour>();
                    Log.LogInfo("Компаньон 'Шелковый Жнец' призван!");
                }
                else
                {
                    Destroy(activeCompanion);
                    Log.LogInfo("Компаньон отозван!");
                }
            }
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    // Компонент логики самого компаньона
    public class CompanionBehaviour : MonoBehaviour
    {
        private Transform hornetTransform;
        private float bobTimer = 0f;
        private float scanTimer = 0f;
        private float attackTimer = 0f;
        private LineRenderer attackLine;

        private void Start()
        {
            FindPlayer();
            SetupVisuals();
            SetupAttackLine();
        }

        private void FindPlayer()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                hornetTransform = player.transform;
            }
        }

        private void SetupVisuals()
        {
            SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
            
            Texture2D tex = new Texture2D(32, 32);
            for (int x = 0; x < 32; x++)
            {
                for (int y = 0; y < 32; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                    float alpha = Mathf.Clamp01(1.0f - (dist / 16.0f));
                    tex.SetPixel(x, y, new Color(0.8f, 0.9f, 1.0f, alpha));
                }
            }
            tex.Apply();

            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            sr.sortingOrder = 10;
        }

        // Создаем видимую линию атаки
        private void SetupAttackLine()
        {
            attackLine = gameObject.AddComponent<LineRenderer>();
            attackLine.startWidth = 0.08f;
            attackLine.endWidth = 0.02f;
            attackLine.positionCount = 2;
            attackLine.useWorldSpace = true;
            attackLine.enabled = false; // По умолчанию скрыта
            attackLine.sortingOrder = 11;

            // Настройка белого светящегося цвета
            Material lineMat = new Material(Shader.Find("Sprites/Default"));
            attackLine.material = lineMat;
            attackLine.startColor = new Color(0.9f, 0.95f, 1.0f, 0.9f);
            attackLine.endColor = new Color(0.8f, 0.9f, 1.0f, 0.3f);
        }

        private void Update()
        {
            if (hornetTransform == null)
            {
                FindPlayer();
                return;
            }

            // 1. Плавное следование за Хорнет
            bobTimer += Time.deltaTime * 3f;
            Vector3 targetPosition = hornetTransform.position + new Vector3(-1.2f, 1.5f + Mathf.Sin(bobTimer) * 0.2f, 0f);
            
            transform.position = Vector3.Lerp(
                transform.position, 
                targetPosition, 
                Time.deltaTime * Plugin.FollowSpeed.Value
            );

            // 2. Сканер и притягивание Гео
            scanTimer += Time.deltaTime;
            if (scanTimer >= 0.3f)
            {
                scanTimer = 0f;
                PullGeoToCompanion();
            }

            // 3. Атака врагов
            attackTimer += Time.deltaTime;
            if (attackTimer >= 2.0f)
            {
                attackTimer = 0f;
                AttackNearbyEnemies();
            }
        }

        private void PullGeoToCompanion()
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, Plugin.PickupRadius.Value);
            
            foreach (var hit in hitColliders)
            {
                if (hit.gameObject.name.Contains("Geo") || hit.gameObject.CompareTag("Geo"))
                {
                    Rigidbody2D rb = hit.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        Vector2 direction = (transform.position - hit.transform.position).normalized;
                        rb.velocity = direction * 14f;
                    }
                }
            }
        }

        private void AttackNearbyEnemies()
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, 6.0f);
            
            foreach (var hit in hitColliders)
            {
                if (hit.CompareTag("Enemy") || hit.gameObject.name.Contains("Enemy") || hit.GetComponent("HealthManager") != null)
                {
                    Plugin.Log.LogInfo("Шелковый Жнец атаковал врага: " + hit.gameObject.name);

                    // Показываем визуальную нить атаки
                    StartCoroutine(ShowAttackThread(hit.transform.position));

                    Rigidbody2D enemyRb = hit.GetComponent<Rigidbody2D>();
                    if (enemyRb != null)
                    {
                        enemyRb.velocity *= 0.5f; 
                    }

                    var healthManager = hit.GetComponent("HealthManager");
                    if (healthManager != null)
                    {
                        var hitMethod = healthManager.GetType().GetMethod("Hit");
                        if (hitMethod != null)
                        {
                            try {
                                hitMethod.Invoke(healthManager, new object[] { 5 });
                            } catch { }
                        }
                    }

                    break;
                }
            }
        }

        // Анимация вспышки шелковой нити
        private IEnumerator ShowAttackThread(Vector3 targetPos)
        {
            if (attackLine == null) yield break;

            attackLine.SetPosition(0, transform.position);
            attackLine.SetPosition(1, targetPos);
            attackLine.enabled = true;

            yield return new WaitForSeconds(0.15f);

            attackLine.enabled = false;
        }
    }
}
