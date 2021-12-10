using BepInEx;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using Color = UnityEngine.Color;
namespace UKHudLogger
{
    [BepInPlugin("cap.ultrakill.hudlogger", "UKHudLogger", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private string hudsPath;
        private int loadingPointer;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            
            List<string> folders = Path.GetFullPath(@"ULTRAKILL.exe").Split('\\').ToList();
            folders.RemoveAt(folders.Count - 1);
            folders.AddRange(new string[] {"BepInEx", "plugins", "HUDs"});
            hudsPath = String.Join("\\", folders.ToArray());
            
            Debug.Log($"hudsPath: {hudsPath}");
            Directory.CreateDirectory(hudsPath);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.J))
            {
                LoadHUD(--loadingPointer);
            }
            else if (Input.GetKeyDown(KeyCode.K))
            {
                SaveNewHUD();
            }
            else if (Input.GetKeyDown(KeyCode.L))
            {
                LoadHUD(++loadingPointer);
            }
        }

        private void SaveNewHUD()
        {
            Color[] colors = new Color[14];
            colors[0] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.health);
            colors[1] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.healthAfterImage);
            colors[2] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.antiHp);
            colors[3] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.overheal);
            colors[4] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.healthText);
            colors[5] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.stamina);
            colors[6] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.staminaCharging);
            colors[7] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.staminaEmpty);
            colors[8] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.railcannonFull);
            colors[9] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.railcannonCharging);
            colors[10] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[0];
            colors[11] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[1];
            colors[12] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[2];
            colors[13] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[3];
            for (int i = 0; i < 14; i++)
            {
                colors[i].a = 1;
            }

            Texture2D t2d = new Texture2D(14, 1);
            for (int i = 0; i < 14; i++)
            {
                t2d.SetPixel(i, 0, colors[i]);
            }

            File.WriteAllBytes(hudsPath + $@"\{Directory.GetFiles(hudsPath).Length}.png", t2d.EncodeToPNG());
            Debug.Log("successfully written file, yay");
        }

        private void LoadHUD(int pointer)
        {
            if (!Directory.EnumerateFileSystemEntries(hudsPath).Any())
            {
                loadingPointer = 0;
                Debug.Log("no files");
                return;
            }
            if (pointer < 0)
            {
                pointer = Directory.GetFiles(hudsPath).Length - 1;
                loadingPointer = pointer;
            }
            pointer %= Directory.GetFiles(hudsPath).Length;
            Debug.Log($"pointer: {pointer}");
            Texture2D t2d = new Texture2D(14, 1);
            t2d.LoadImage(File.ReadAllBytes(hudsPath + $@"\{pointer}.png"));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.health, t2d.GetPixel(0, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthAfterImage, t2d.GetPixel(1, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.antiHp, t2d.GetPixel(2, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.overheal, t2d.GetPixel(3, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthText, t2d.GetPixel(4, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.stamina, t2d.GetPixel(5, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaCharging, t2d.GetPixel(6, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaEmpty, t2d.GetPixel(7, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonFull, t2d.GetPixel(8, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonCharging, t2d.GetPixel(9, 0));
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[0] =  t2d.GetPixel(10, 0);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[1] =  t2d.GetPixel(11, 0);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[2] =  t2d.GetPixel(12, 0);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[3] =  t2d.GetPixel(13, 0);
            MonoSingleton<ColorBlindSettings>.Instance.UpdateHudColors();
            MonoSingleton<ColorBlindSettings>.Instance.UpdateWeaponColors();
            Debug.Log("loaded new hud");
        }
    }
}
