using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
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

            // 2. Применение Harmony патчей (если понадобятся в будущем)
            harmony = new Harmony(PLUGIN_GUID);
            harmony.PatchAll();

            Log.LogInfo($"Плагин {PLUGIN_NAME} версии {PLUGIN_VERSION} успешно загружен!");
        }

        // Прямое отслеживание нажатия клавиши в самом плагине
        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey.Value))
            {
                if (activeCompanion == null)
                {
                    activeCompanion = new GameObject("SilkWeaverCompanion");
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

        private void Start()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                hornetTransform = player.transform;
            }
            
            SetupVisuals();
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

        private void Update()
        {
            if (hornetTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) hornetTransform = player.transform;
                return;
            }

            bobTimer += Time.deltaTime * 3f;
            Vector3 targetPosition = hornetTransform.position + new Vector3(-1.2f, 1.5f + Mathf.Sin(bobTimer) * 0.2f, 0f);
            
            transform.position = Vector3.Lerp(
                transform.position, 
                targetPosition, 
                Time.deltaTime * Plugin.FollowSpeed.Value
            );

            scanTimer += Time.deltaTime;
            if (scanTimer >= 0.5f)
            {
                scanTimer = 0f;
                CollectNearbyGeo();
            }
        }

        private void CollectNearbyGeo()
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, Plugin.PickupRadius.Value);
            
            foreach (var hit in hitColliders)
            {
                if (hit.gameObject.name.Contains("Geo") || hit.gameObject.CompareTag("Geo"))
                {
                    Rigidbody2D rb = hit.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        Vector2 direction = (hornetTransform.position - hit.transform.position).normalized;
                        rb.velocity = direction * 12f;
                    }
                }
            }
        }
    }
}
