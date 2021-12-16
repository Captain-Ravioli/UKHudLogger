using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Color = UnityEngine.Color;
using Random = UnityEngine.Random;

namespace UKHudLogger
{
    [BepInPlugin("cap.ultrakill.hudlogger", "UKHudLogger", "1.0.0")]
    [BepInProcess("ULTRAKILL.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private string hudsPath;
        private int loadingPointer;
        private bool loadingHUD;
        private string newHUDName;
        private Rect windowRect = new Rect(Screen.width - 220, 0, 220, 120);
        private bool saveHUD;
        private bool loadHUDAtStart = true;
        private ConfigEntry<int> pointerConfig;

        private void Awake()
        {
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            pointerConfig = Config.Bind("Pointer", "Value", 0, "Next time the game loads, this will be the HUD that's loaded.");
            loadingPointer = pointerConfig.Value;
            
            List<string> folders = Path.GetFullPath(@"ULTRAKILL.exe").Split('\\').ToList();
            folders.RemoveAt(folders.Count - 1);
            folders.AddRange(new string[] {"BepInEx", "plugins", "HUDs"});
            hudsPath = String.Join("\\", folders.ToArray());
            
            Debug.Log($"hudsPath: {hudsPath}");
            Directory.CreateDirectory(hudsPath);
            Directory.CreateDirectory($@"{hudsPath}\Resources");
            if (Directory.GetFiles(hudsPath).Length == 0)
            {
                loadingPointer = 0;
            }
            SceneManager.activeSceneChanged += OnSceneChanged;
        }
        
        private void OnSceneChanged(Scene from, Scene to)
        {
            loadingHUD = false;
            saveHUD = false;
            newHUDName = RandomString(8);
            loadHUDAtStart = true;
        }

        private void Update()
        {
            if (loadHUDAtStart && 
                (SceneManager.GetActiveScene().name.StartsWith("Level") || SceneManager.GetActiveScene().name.StartsWith("Endless")))
            {
                loadHUDAtStart = false;
                StartCoroutine(LoadHUD(loadingPointer));
            }
            if (Input.GetKeyDown(KeyCode.J) && !loadingHUD)
            {
                loadingPointer--;
                StartCoroutine(LoadHUD(loadingPointer));
            }
            else if (Input.GetKeyDown(KeyCode.I) && !loadingHUD)
                StartCoroutine(DeleteHUD(loadingPointer));
            else if (Input.GetKeyDown(KeyCode.K) && !loadingHUD && MonoSingleton<OptionsManager>.Instance.paused)
            {
                MonoSingleton<OptionsManager>.Instance.pauseMenu.SetActive(false);
                saveHUD = true;
            }
            else if (Input.GetKeyDown(KeyCode.Escape) && saveHUD)
                saveHUD = false;
            else if (Input.GetKeyDown(KeyCode.L) && !loadingHUD)
            {
                loadingPointer++;
                StartCoroutine(LoadHUD(loadingPointer));
            }
        }

        private IEnumerator waitsecs(float val)
        {
            yield return new WaitForSeconds(val);
        }

        private IEnumerator DeleteHUD(int pointer)
        {
            loadingHUD = true;
            string[] files = Directory.GetFiles(hudsPath);
            if (files.Length == 0)
                yield break;
            File.Delete(files[pointer]);
            pointerConfig.Value = loadingPointer;
            StartCoroutine(LoadHUD(--loadingPointer));
            loadingHUD = false;

            yield break;
        }

        private IEnumerator SaveNewHUD()
        {
            loadingHUD = true;
            Color[] colors = new Color[14];
            colors[0] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.health);
            yield return new WaitForSeconds(0.005f);
            colors[1] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.healthAfterImage);
            yield return new WaitForSeconds(0.005f);
            colors[2] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.antiHp);
            yield return new WaitForSeconds(0.005f);
            colors[3] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.overheal);
            yield return new WaitForSeconds(0.005f);
            colors[4] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.healthText);
            yield return new WaitForSeconds(0.005f);
            colors[5] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.stamina);
            yield return new WaitForSeconds(0.005f);
            colors[6] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.staminaCharging);
            yield return new WaitForSeconds(0.005f);
            colors[7] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.staminaEmpty);
            yield return new WaitForSeconds(0.005f);
            colors[8] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.railcannonFull);
            yield return new WaitForSeconds(0.005f);
            colors[9] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.railcannonCharging);
            yield return new WaitForSeconds(0.005f);
            colors[10] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[0];
            yield return new WaitForSeconds(0.005f);
            colors[11] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[1];
            yield return new WaitForSeconds(0.005f);
            colors[12] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[2];
            yield return new WaitForSeconds(0.005f);
            colors[13] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[3];
            yield return new WaitForSeconds(0.005f);
            for (int i = 0; i < 14; i++)
            {
                colors[i].a = 1;
            }

            Texture2D t2d = new Texture2D(14, 1);
            for (int i = 0; i < 14; i++)
            {
                t2d.SetPixel(i, 0, colors[i]);
            }
            string[] files = Directory.GetFiles(hudsPath);
            File.WriteAllBytes(hudsPath + $@"\{newHUDName}.png", t2d.EncodeToPNG());
            Debug.Log("successfully written file, yay");
            loadingPointer = files.Length - 1;
            pointerConfig.Value = loadingPointer;
            loadingHUD = false;
            while (true)
            {
                if (!MonoSingleton<OptionsManager>.Instance.paused)
                {
                    saveHUD = false;
                    drawSuccess = false;
                    newHUDName = RandomString(8);
                    break;
                }
            }

            yield break;
        }

        private IEnumerator LoadHUD(int pointer)
        {
            if (!Directory.EnumerateFileSystemEntries(hudsPath).Any())
            {
                loadingPointer = 0;
                Debug.Log("no files");
                yield break;
            }
            if (!SceneManager.GetActiveScene().name.StartsWith("Level") && !SceneManager.GetActiveScene().name.StartsWith("Endless"))
                yield break;
            
            loadingHUD = true;
            string[] files = Directory.GetFiles(hudsPath);
            Debug.Log($"files count: {files.Length}");
            if (pointer < 0)
                pointer = files.Length - 1;

            if (pointer >= files.Length)
                pointer = 0;
            loadingPointer = pointer;
            pointerConfig.Value = loadingPointer;

            Debug.Log($"pointer: {pointer}");
            Texture2D t2d = new Texture2D(14, 1);
            t2d.LoadImage(File.ReadAllBytes(files[pointer]));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.health, t2d.GetPixel(0, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthAfterImage, t2d.GetPixel(1, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.antiHp, t2d.GetPixel(2, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.overheal, t2d.GetPixel(3, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthText, t2d.GetPixel(4, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.stamina, t2d.GetPixel(5, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaCharging, t2d.GetPixel(6, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaEmpty, t2d.GetPixel(7, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonFull, t2d.GetPixel(8, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonCharging, t2d.GetPixel(9, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[0] =  t2d.GetPixel(10, 0);
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[1] =  t2d.GetPixel(11, 0);
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[2] =  t2d.GetPixel(12, 0);
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[3] =  t2d.GetPixel(13, 0);
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.UpdateHudColors();
            MonoSingleton<ColorBlindSettings>.Instance.UpdateWeaponColors();
            Debug.Log("loaded new hud");
            loadingHUD = false;

            yield break;
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789()-_";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[Random.Range(0, s.Length)]).ToArray());
        }

        private void OnGUI()
        {
            if (saveHUD)
                windowRect = GUILayout.Window(0, windowRect, DrawWindow, string.Empty, GUILayout.MinWidth(220), GUILayout.MinHeight(120));
        }

        private bool drawSuccess;
        private void DrawWindow(int id)
        {
            switch (id)
            {
                case 0:
                    if (!MonoSingleton<OptionsManager>.Instance.paused)
                        return;
                    string changedHUDName = GUILayout.TextField(newHUDName);
                    if (changedHUDName != newHUDName && drawSuccess)
                        drawSuccess = false;
                    newHUDName = changedHUDName;

                    if (GUILayout.Button("Save HUD", GUILayout.MaxWidth(220), GUILayout.MaxHeight(30)))
                    {
                        drawSuccess = true;
                        StartCoroutine(SaveNewHUD());
                    }
                    if (drawSuccess)
                    {
                        GUIStyle style = new GUIStyle(GUI.skin.GetStyle("Label"));
                        style.alignment = TextAnchor.MiddleCenter;
                        GUILayout.Label($"Successfully saved {newHUDName}!", style);
                    }
                    break;
                default:
                    break;
            }
        }
    }
}