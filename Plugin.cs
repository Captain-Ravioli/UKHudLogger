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
        private int loadingPointer, hudSettingsPointer;
        private bool loadingHUD;
        private Rect windowRect = new Rect(Screen.width / 2 - 330, Screen.height / 2 - 350, 660, 700);
        private bool inMenu, newHUD;
        private bool saveHUD;
        private bool loadHUDAtStart = true;
        private ConfigEntry<int> pointerConfig;
        private Texture2D arrow, flippedArrow, dreamed;
        private Dictionary<int, object[]> previewHUDSettings = new Dictionary<int, object[]>
        {
            {0, new object[] {"HEALTH", 1, 0, 0}},
            {1, new object[] {"HEALTH NUMBER", 1, 1, 1}},
            {2, new object[] {"SOFT DAMAGE", 1, .39f, 0}},
            {3, new object[] {"HARD DAMAGE", .35f, .35f, .35f}},
            {4, new object[] {"OVERHEAL", 0, 1, 0}},
            {5, new object[] {"STAMINA (FULL)", 0, .87f, 1}},
            {6, new object[] {"STAMINA (CHARGING)", 0, .87f, 1}},
            {7, new object[] {"STAMINA (EMPTY)", 1, 0, 0}},
            {8, new object[] {"RAILCANNON (FULL)", .25f, .91f, 1}},
            {9, new object[] {"RAILCANNON (CHARGING)", 1, 0, 0}},
            {10, new object[] {"BLUE VARIATION", .25f, .91f, 1}},
            {11, new object[] {"GREEN VARIATION", .27f, 1, .27f}},
            {12, new object[] {"RED VARIATION", 1, .24f, .24f}},
            {13, new object[] {"GOLD VARIATION", 1, .88f, .24f}}
        };
        private Color newColor = new Color(1, 1, 1, 1);
        private string hexColor = "#FF0000", rgbColor = "255,0,0";
        private GUIStyle labelStyle, buttonStyle, textFieldStyle;
        private float[] settingsCache = {0, 0, 0};
        private string newHUDName = "NEW HUD NAME", saveHUDButton;
        private Font vcrFont;
        bool saveHUDText;

        private void Awake()
        {
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            pointerConfig = Config.Bind("Pointer", "Value", 0, "Next time the game loads, this will be the HUD that's loaded.");
            loadingPointer = pointerConfig.Value;
            
            List<string> folders = Path.GetFullPath(@"ULTRAKILL.exe").Split('\\').ToList();
            folders.RemoveAt(folders.Count - 1);
            folders.AddRange(new string[] {"BepInEx", "plugins", "ULTRAHUD", "HUDs"});
            hudsPath = String.Join("\\", folders.ToArray());
            
            Debug.Log($"hudsPath: {hudsPath}");
            Directory.CreateDirectory(hudsPath);
            if (Directory.GetFiles(hudsPath).Length == 0)
            {
                loadingPointer = 0;
            }
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void Start()
        {
            arrow = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
            arrow.LoadImage(File.ReadAllBytes($@"{Directory.GetCurrentDirectory()}\BepInEx\plugins\ULTRAHUD\Assets\Arrow.png"));
            flippedArrow = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
            flippedArrow.LoadImage(File.ReadAllBytes($@"{Directory.GetCurrentDirectory()}\BepInEx\plugins\ULTRAHUD\Assets\FlippedArrow.png"));
            dreamed = new Texture2D(990, 990, TextureFormat.RGBA32, false);
            dreamed.LoadImage(File.ReadAllBytes(@$"{Directory.GetCurrentDirectory()}\BepInEx\plugins\ULTRAHUD\Assets\dreamed.jpg"));

            vcrFont = AssetBundle.LoadFromFile(@$"{Directory.GetCurrentDirectory()}\BepInEx\plugins\ULTRAHUD\Assets\Asset Bundles\font")
                .LoadAllAssets<Font>()[0];
        }
        
        private void OnSceneChanged(Scene from, Scene to)
        {
            loadingHUD = false;
            saveHUD = false;
            newHUDName = RandomString(8);
            loadHUDAtStart = true;
            inMenu = false;
            newHUD = false;
            saveHUDText = false;
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
            {
                saveHUD = false;
                inMenu = false;
                newHUD = false;
                saveHUDText = false;
            }
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
                    saveHUDText = false;
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

        private void ApplyHUDColors()
        {
            Color col = new Color(float.Parse(previewHUDSettings[0][1].ToString()), float.Parse(previewHUDSettings[0][2].ToString()), float.Parse(previewHUDSettings[0][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.health, col);
            col = new Color(float.Parse(previewHUDSettings[1][1].ToString()), float.Parse(previewHUDSettings[1][2].ToString()), float.Parse(previewHUDSettings[1][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthText, col);
            col = new Color(float.Parse(previewHUDSettings[2][1].ToString()), float.Parse(previewHUDSettings[2][2].ToString()), float.Parse(previewHUDSettings[2][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthAfterImage, col);
            col = new Color(float.Parse(previewHUDSettings[3][1].ToString()), float.Parse(previewHUDSettings[3][2].ToString()), float.Parse(previewHUDSettings[3][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.antiHp, col);
            col = new Color(float.Parse(previewHUDSettings[4][1].ToString()), float.Parse(previewHUDSettings[4][2].ToString()), float.Parse(previewHUDSettings[4][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.overheal, col);
            col = new Color(float.Parse(previewHUDSettings[5][1].ToString()), float.Parse(previewHUDSettings[5][2].ToString()), float.Parse(previewHUDSettings[5][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.stamina, col);
            col = new Color(float.Parse(previewHUDSettings[6][1].ToString()), float.Parse(previewHUDSettings[6][2].ToString()), float.Parse(previewHUDSettings[6][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaCharging, col);
            col = new Color(float.Parse(previewHUDSettings[7][1].ToString()), float.Parse(previewHUDSettings[7][2].ToString()), float.Parse(previewHUDSettings[7][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaEmpty, col);
            col = new Color(float.Parse(previewHUDSettings[8][1].ToString()), float.Parse(previewHUDSettings[8][2].ToString()), float.Parse(previewHUDSettings[8][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonFull, col);
            col = new Color(float.Parse(previewHUDSettings[9][1].ToString()), float.Parse(previewHUDSettings[9][2].ToString()), float.Parse(previewHUDSettings[9][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonCharging, col);
            col = new Color(float.Parse(previewHUDSettings[10][1].ToString()), float.Parse(previewHUDSettings[10][2].ToString()), float.Parse(previewHUDSettings[10][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[0] =  col;
            col = new Color(float.Parse(previewHUDSettings[11][1].ToString()), float.Parse(previewHUDSettings[11][2].ToString()), float.Parse(previewHUDSettings[11][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[1] =  col;
            col = new Color(float.Parse(previewHUDSettings[12][1].ToString()), float.Parse(previewHUDSettings[12][2].ToString()), float.Parse(previewHUDSettings[12][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[2] =  col;
            col = new Color(float.Parse(previewHUDSettings[13][1].ToString()), float.Parse(previewHUDSettings[13][2].ToString()), float.Parse(previewHUDSettings[13][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[3] =  col;

            MonoSingleton<ColorBlindSettings>.Instance.UpdateHudColors();
            MonoSingleton<ColorBlindSettings>.Instance.UpdateWeaponColors();
        }

        private void OnGUI()
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            buttonStyle = new GUIStyle(GUI.skin.button);
            textFieldStyle = new GUIStyle(GUI.skin.textField);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.font = vcrFont;
            labelStyle.fontSize = 40;
            buttonStyle.font = vcrFont;
            buttonStyle.fontSize = 40;
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            textFieldStyle.font = vcrFont;
            textFieldStyle.fontSize = 20;
            textFieldStyle.alignment = TextAnchor.MiddleCenter;

            if (saveHUD)
            {
                GUI.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1);
                windowRect = GUILayout.Window(0, windowRect, DrawWindow, "", GUILayout.MaxWidth(660), GUILayout.MaxHeight(700));
            }
        }

        private void DrawWindow(int id)
        {
            switch (id)
            {
                case 0:
                    if (!inMenu)
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("NEW HUD", buttonStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60)))
                        {
                            inMenu = true;
                            newHUD = true;
                        }
                        if (GUILayout.Button("EDIT HUD", buttonStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60)))
                        {}
                        GUILayout.FlexibleSpace();
                    }
                    if (newHUD)
                    {
                        if (hudSettingsPointer < 0)
                            hudSettingsPointer = previewHUDSettings.Count - 1;
                        if (hudSettingsPointer >= previewHUDSettings.Count)
                            hudSettingsPointer = 0;

                        newColor = new Color
                            (float.Parse(previewHUDSettings[hudSettingsPointer][1].ToString()),
                            float.Parse(previewHUDSettings[hudSettingsPointer][2].ToString()),
                            float.Parse(previewHUDSettings[hudSettingsPointer][3].ToString()));

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        buttonStyle.fontSize = 30;
                        if (GUILayout.Button("BACK", buttonStyle, GUILayout.MaxWidth(100), GUILayout.MaxHeight(40)))
                        {
                            inMenu = false;
                            newHUD = false;
                        }
                        buttonStyle.fontSize = 40;
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (newHUDName == "NEW HUD NAME")
                        {
                            GUI.contentColor = Color.gray;
                            newHUDName = "NEW HUD NAME";
                        }
                        string newHUDNameCache = newHUDName;
                        newHUDName = GUILayout.TextField(
                            newHUDName, 25, textFieldStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60))
                            .ToUpper();

                        GUI.contentColor = Color.white;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(10);

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(flippedArrow, GUILayout.MaxWidth(50), GUILayout.MaxHeight(50)))
                        {
                            hudSettingsPointer--;
                            newColor = new Color
                                (float.Parse(previewHUDSettings[hudSettingsPointer][1].ToString()),
                                float.Parse(previewHUDSettings[hudSettingsPointer][2].ToString()),
                                float.Parse(previewHUDSettings[hudSettingsPointer][3].ToString()));
                            hexColor = $"#{ColorUtility.ToHtmlStringRGB(newColor)}";
                            rgbColor = $"{Mathf.FloorToInt(newColor.r * 255)},{Mathf.FloorToInt(newColor.g * 255)},{Mathf.FloorToInt(newColor.b * 255)}";
                        }
                        GUI.contentColor = newColor;
                        GUILayout.Label((string)previewHUDSettings[hudSettingsPointer][0], labelStyle, GUILayout.MaxWidth(490), GUILayout.MaxHeight(50));
                        GUI.contentColor = Color.white;
                        if (GUILayout.Button(arrow, buttonStyle, GUILayout.MaxWidth(50), GUILayout.MaxHeight(50)))
                        {
                            hudSettingsPointer++;
                            newColor = new Color
                                (float.Parse(previewHUDSettings[hudSettingsPointer][1].ToString()),
                                float.Parse(previewHUDSettings[hudSettingsPointer][2].ToString()),
                                float.Parse(previewHUDSettings[hudSettingsPointer][3].ToString()));
                            hexColor = $"#{ColorUtility.ToHtmlStringRGB(newColor)}";
                            rgbColor = $"{Mathf.FloorToInt(newColor.r * 255)},{Mathf.FloorToInt(newColor.g * 255)},{Mathf.FloorToInt(newColor.b * 255)}";
                        }   
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(15);

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.BeginVertical();
                        GUI.contentColor = Color.red;
                        GUILayout.Label("R ", labelStyle, GUILayout.MaxHeight(40));
                        GUI.contentColor = Color.green;
                        GUILayout.Label("G ", labelStyle, GUILayout.MaxHeight(40));
                        GUI.contentColor = Color.blue;
                        GUILayout.Label("B ", labelStyle, GUILayout.MaxHeight(40));
                        if (true)
                            GUI.contentColor = Color.white;
                        GUILayout.EndVertical();
                        GUILayout.BeginVertical();
                        GUILayout.Space(20);
                        GUI.backgroundColor = Color.red;
                        previewHUDSettings[hudSettingsPointer][1] = GUILayout.HorizontalSlider(float.Parse(previewHUDSettings[hudSettingsPointer][1].ToString()), 0, 1, GUILayout.MaxHeight(40), GUILayout.MaxWidth(420));
                        GUI.backgroundColor = Color.green;
                        previewHUDSettings[hudSettingsPointer][2] = GUILayout.HorizontalSlider(float.Parse(previewHUDSettings[hudSettingsPointer][2].ToString()), 0, 1, GUILayout.MaxHeight(40), GUILayout.MaxWidth(420));
                        GUI.backgroundColor = Color.blue;
                        previewHUDSettings[hudSettingsPointer][3] = GUILayout.HorizontalSlider(float.Parse(previewHUDSettings[hudSettingsPointer][3].ToString()), 0, 1, GUILayout.MaxHeight(40), GUILayout.MaxWidth(420));
                        GUI.backgroundColor = Color.white;
                        if (float.Parse(previewHUDSettings[hudSettingsPointer][1].ToString()) != settingsCache[0] || float.Parse(previewHUDSettings[hudSettingsPointer][2].ToString()) != settingsCache[1] || float.Parse(previewHUDSettings[hudSettingsPointer][3].ToString()) != settingsCache[2])
                        {
                            newColor = new Color
                                (float.Parse(previewHUDSettings[hudSettingsPointer][1].ToString()),
                                float.Parse(previewHUDSettings[hudSettingsPointer][2].ToString()),
                                float.Parse(previewHUDSettings[hudSettingsPointer][3].ToString()));
                            hexColor = $"#{ColorUtility.ToHtmlStringRGB(newColor)}";
                            rgbColor = $"{Mathf.FloorToInt(newColor.r * 255)},{Mathf.FloorToInt(newColor.g * 255)},{Mathf.FloorToInt(newColor.b * 255)}";
                        }
                        settingsCache[0] = float.Parse(previewHUDSettings[hudSettingsPointer][1].ToString());
                        settingsCache[1] = float.Parse(previewHUDSettings[hudSettingsPointer][2].ToString());
                        settingsCache[2] = float.Parse(previewHUDSettings[hudSettingsPointer][3].ToString());

                        GUILayout.EndVertical();

                        GUILayout.Space(20);

                        GUILayout.BeginVertical();
                        GUILayout.Label(newColor.r.ToString("0.00"), labelStyle, GUILayout.MaxHeight(40));
                        GUILayout.Label(newColor.g.ToString("0.00"), labelStyle, GUILayout.MaxHeight(40));
                        GUILayout.Label(newColor.b.ToString("0.00"), labelStyle, GUILayout.MaxHeight(40));
                        GUILayout.EndVertical();
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(2);

                        GUILayout.BeginHorizontal();
                        bool pressed = false;
                        GUILayout.FlexibleSpace();
                        try
                        {
                            hexColor = GUILayout.TextField($"#{hexColor.Substring(1)}", 7, textFieldStyle, GUILayout.MaxHeight(50), GUILayout.MaxWidth(100)).ToUpper();
                        }   
                        catch
                        {
                            hexColor = "#";
                        }
                        GUILayout.BeginVertical();
                        GUILayout.Space(15f);
                        buttonStyle.fontSize = 20;
                        pressed = GUILayout.Button("APPLY", buttonStyle, GUILayout.MaxWidth(75), GUILayout.MaxHeight(30));
                        buttonStyle.fontSize = 40;
                        GUILayout.Space(15f);
                        GUILayout.EndVertical();
                        if (hexColor.Length == 7 && ColorUtility.TryParseHtmlString(hexColor, out Color nc) && pressed)
                        {
                            newColor = nc;
                            previewHUDSettings[hudSettingsPointer][1] = newColor.r;
                            previewHUDSettings[hudSettingsPointer][2] = newColor.g;
                            previewHUDSettings[hudSettingsPointer][3] = newColor.b;
                        }

                        GUILayout.Space(15);

                        bool isRgbValid = true;
                        rgbColor = GUILayout.TextField(rgbColor, 11, textFieldStyle, GUILayout.MaxHeight(50), GUILayout.MaxWidth(150));
                        string[] rgbs = rgbColor.Split(',');
                        float[] vals = new float[3] {-1, -1, -1};
                        if (rgbs.Length == 3)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                if (int.TryParse(rgbs[i], out int x))
                                    vals[i] = x;
                                else
                                {
                                    isRgbValid = false;
                                }
                            }

                            for (int i = 0; i < 3; i++)
                                if (vals[i] == -1)
                                    isRgbValid = false;
                        }
                        else
                            isRgbValid = false;

                        pressed = false;
                        GUILayout.BeginVertical();
                        GUILayout.Space(15f);
                        buttonStyle.fontSize = 20;
                        pressed = GUILayout.Button("APPLY", buttonStyle, GUILayout.MaxWidth(75), GUILayout.MaxHeight(30));
                        buttonStyle.fontSize = 40;
                        GUILayout.Space(15f);
                        GUILayout.EndVertical();
                        if (isRgbValid && pressed)
                        {
                            newColor = new Color(float.Parse((vals[0]/255).ToString("F2")), float.Parse((vals[1]/255).ToString("F2")), float.Parse((vals[2]/255).ToString("F2")));
                            previewHUDSettings[hudSettingsPointer][1] = newColor.r;
                            previewHUDSettings[hudSettingsPointer][2] = newColor.g;
                            previewHUDSettings[hudSettingsPointer][3] = newColor.b;
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(10);

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        buttonStyle.fontSize = 30;
                        if (GUILayout.Button("PREVIEW", buttonStyle, GUILayout.MaxHeight(60), GUILayout.MaxWidth(150)))
                            ApplyHUDColors();
                        buttonStyle.fontSize = 40;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        buttonStyle.fontSize = 30;
                        if (GUILayout.Button($"SAVE AS {newHUDName}?", buttonStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60)))
                        {
                            if (newHUDName == string.Empty
                                || newHUDName.Contains("\\")
                                || newHUDName.Contains("/")
                                || newHUDName.Contains("*")
                                || newHUDName.Contains("?")
                                || newHUDName.Contains("|")
                                || newHUDName.Contains("\"")
                                || newHUDName.Contains(":")
                                || newHUDName.Contains("<")
                                || newHUDName.Contains(">"))
                                saveHUDButton = $"COULDN'T SAVE {newHUDName}, ONLY THE ALPHABET, NUMBERS AND SPACE ARE VALID CHARACTERS";
                            else
                            {
                                saveHUDButton = "SAVED {newHUDName}!";
                                ApplyHUDColors();
                                StartCoroutine(SaveNewHUD());
                            }
                            saveHUDText = true;
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (newHUDNameCache == newHUDName && saveHUDText)
                        {
                            labelStyle.fontSize = 20;
                            GUILayout.Label(saveHUDButton, labelStyle, GUILayout.MinWidth(600), GUILayout.MinHeight(25));
                            labelStyle.fontSize = 40;
                        }
                        else
                            saveHUDText = false;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.FlexibleSpace();

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUI.backgroundColor = new Color(0, 0, 0, 0);
                        GUI.contentColor = newColor;
                        GUILayout.Box(dreamed, GUILayout.MaxHeight(98), GUILayout.MaxWidth(635));
                        GUI.backgroundColor = Color.black;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }

                    break;
                default:
                    break;
            }
        }
    }
}