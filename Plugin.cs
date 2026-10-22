using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SilkWeaverCompanion
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
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
            // Поиск объекта игрока (Хорнет)
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                hornetTransform = player.transform;
            }
            
            // Настройка визуального стиля компаньона (простая светящаяся сфера)
            SetupVisuals();
        }

        private void SetupVisuals()
        {
            // Создаем простой визуал если нет готовой 2D-текстуры
            SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
            
            // Создаем программно круговую текстуру для свечения
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
            sr.sortingOrder = 10; // Отображать поверх большинства элементов
        }

        private void Update()
        {
            if (hornetTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) hornetTransform = player.transform;
                return;
            }

            // 1. Плавное следование за Хорнет с небольшим офсетом и покачиванием
            bobTimer += Time.deltaTime * 3f;
            Vector3 targetPosition = hornetTransform.position + new Vector3(-1.2f, 1.5f + Mathf.Sin(bobTimer) * 0.2f, 0f);
            
            transform.position = Vector3.Lerp(
                transform.position, 
                targetPosition, 
                Time.deltaTime * Plugin.FollowSpeed.Value
            );

            // 2. Периодический сканер и сбор Гео
            scanTimer += Time.deltaTime;
            if (scanTimer >= 0.5f)
            {
                scanTimer = 0f;
                CollectNearbyGeo();
            }
        }

        private void CollectNearbyGeo()
        {
            // Поиск всех объектов Гео (в игре объекты обычно имеют компонент GeoCoin или тег "Geo")
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, Plugin.PickupRadius.Value);
            
            foreach (var hit in hitColliders)
            {
                // Проверяем по имени или компоненту
                if (hit.gameObject.name.Contains("Geo") || hit.gameObject.CompareTag("Geo"))
                {
                    // Притягиваем объект Гео прямо к игроку
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

    // Harmony-патч для автовызова или обработки контроллера игрока
    [HarmonyPatch(typeof(MonoBehaviour), "Update")]
    public static class PlayerUpdatePatch
    {
        private static GameObject activeCompanion;

        public static void Postfix()
        {
            // Отслеживание нажатия горячей клавиши для спавна компаньона
            if (Input.GetKeyDown(Plugin.ToggleKey.Value))
            {
                if (activeCompanion == null)
                {
                    activeCompanion = new GameObject("SilkWeaverCompanion");
                    activeCompanion.AddComponent<CompanionBehaviour>();
                    Plugin.Log.LogInfo("Компаньон 'Шелковый Жнец' призван!");
                }
                else
                {
                    Object.Destroy(activeCompanion);
                    Plugin.Log.LogInfo("Компаньон отзыв!");
                }
            }
        }
    }
}