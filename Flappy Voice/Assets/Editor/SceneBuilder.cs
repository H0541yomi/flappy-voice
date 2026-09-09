using System;
using System.IO;
using FlappyVoice.Audio;
using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using FlappyVoice.Platform;
using FlappyVoice.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlappyVoice.Editor
{
    public static class SceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string SettingsFolder = "Assets/Settings";
        private const string ConfigPath = "Assets/Settings/GameConfig.asset";
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string PipePrefabPath = "Assets/Prefabs/Pipe.prefab";
        private const string ArtFolder = "Assets/Art/Placeholder";
        private const string BirdSpritePath = "Assets/Art/Placeholder/Bird.png";
        private const string PipeSpritePath = "Assets/Art/Placeholder/PipeSection.png";
        private const int PixelsPerUnit = 64;

        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
        private static readonly Color SkyColor = new Color(0.16f, 0.20f, 0.34f, 1f);
        private static readonly Color CardColor = new Color(0.10f, 0.12f, 0.20f, 1f);

        private static TMP_FontAsset cachedFont;
        private static bool fontResolved;
        private static int uiLayer = 5;

        [MenuItem("Flappy Voice/Build Game Scene")]
        public static void BuildGameScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            fontResolved = false;
            cachedFont = null;
            int namedUiLayer = LayerMask.NameToLayer("UI");
            uiLayer = namedUiLayer >= 0 ? namedUiLayer : 5;

            EnsureFolder(ScenesFolder);
            EnsureFolder(SettingsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(ArtFolder);

            GameConfig config = EnsureConfig();
            Sprite birdSprite = EnsureBirdSprite();
            Sprite pipeSprite = EnsurePipeSprite();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject pipePrefab = BuildPipePrefab(config, pipeSprite);
            Camera camera = BuildCamera(config);
            GameObject managersGo = new GameObject("Managers");
            GameStateManager stateManager = managersGo.AddComponent<GameStateManager>();
            ScoreManager scoreManager = managersGo.AddComponent<ScoreManager>();
            ShareService shareService = managersGo.AddComponent<ShareService>();

            GameObject audioGo = new GameObject("Audio");
            MicrophoneInput microphoneInput = audioGo.AddComponent<MicrophoneInput>();
            PitchTracker pitchTracker = audioGo.AddComponent<PitchTracker>();

            GameObject heightGo = new GameObject("HeightSources");
            VoiceHeightSource voiceHeight = heightGo.AddComponent<VoiceHeightSource>();
            AttractPilot attractPilot = heightGo.AddComponent<AttractPilot>();

            GameObject spawnerGo = new GameObject("PipeSpawner");
            PipeSpawner pipeSpawner = spawnerGo.AddComponent<PipeSpawner>();

            PlayerController player = BuildPlayer(config, birdSprite);

            Canvas canvas = BuildCanvas(camera);
            HudUI hud = BuildHud(canvas.transform);
            PitchMeterUI pitchMeter = BuildPitchMeter(canvas.transform);
            EndScreenUI endScreen = BuildEndScreen(canvas.transform, camera);

            pitchTracker.Configure(config);
            pitchTracker.SetMicrophoneInput(microphoneInput);
            pipeSpawner.Configure(config, stateManager);
            voiceHeight.Configure(config, pitchTracker);
            attractPilot.Configure(config, pipeSpawner);
            player.Configure(config, stateManager, voiceHeight, attractPilot);
            hud.Configure(scoreManager, pitchTracker, stateManager);
            pitchMeter.Configure(voiceHeight, pitchTracker);
            endScreen.Configure(stateManager, scoreManager, shareService);

            UnityEngine.Object[] candidates =
            {
                config, stateManager, scoreManager, shareService, microphoneInput, pitchTracker,
                voiceHeight, attractPilot, pipeSpawner, player, camera, hud, pitchMeter, endScreen,
                pipePrefab, pipePrefab != null ? pipePrefab.GetComponent<Pipe>() : null
            };

            AutoWireByType(stateManager, candidates);
            AutoWireByType(scoreManager, candidates);
            AutoWireByType(shareService, candidates);
            AutoWireByType(microphoneInput, candidates);
            AutoWireByType(pitchTracker, candidates);
            AutoWireByType(voiceHeight, candidates);
            AutoWireByType(attractPilot, candidates);
            AutoWireByType(pipeSpawner, candidates);
            AutoWireByType(player, candidates);
            AutoWireByType(hud, candidates);
            AutoWireByType(pitchMeter, candidates);
            AutoWireByType(endScreen, candidates);

            foreach (UnityEngine.Object candidate in candidates)
            {
                if (candidate is Component sceneComponent && sceneComponent.gameObject.scene.IsValid())
                {
                    EditorUtility.SetDirty(sceneComponent);
                }
            }

            // Configure(...) above only lives until the scene is saved, because every recipient
            // keeps its dependencies in non-serialized fields. GameBootstrap carries explicit
            // serialized references and replays the whole sequence at runtime.
            GameObject bootstrapGo = new GameObject("GameBootstrap");
            GameBootstrap bootstrap = bootstrapGo.AddComponent<GameBootstrap>();
            WireBootstrap(bootstrap, config, stateManager, scoreManager, shareService, microphoneInput,
                pitchTracker, voiceHeight, attractPilot, pipeSpawner, player, hud, pitchMeter, endScreen);
            EditorUtility.SetDirty(bootstrap);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SceneBuilder] built {ScenePath}");
        }

        private static GameConfig EnsureConfig()
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            config = GameConfig.CreateDefault();
            if (config == null)
            {
                throw new InvalidOperationException("GameConfig.CreateDefault() returned null.");
            }
            if (!AssetDatabase.Contains(config))
            {
                AssetDatabase.CreateAsset(config, ConfigPath);
                AssetDatabase.SaveAssets();
            }
            return AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) ?? config;
        }

        private static Camera BuildCamera(GameConfig config)
        {
            GameObject go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            Camera camera = go.GetComponent<Camera>();
            float minY = config.PlayfieldMinY;
            float maxY = config.PlayfieldMaxY;
            float centerY = (minY + maxY) * 0.5f;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(1f, (maxY - minY) * 0.5f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyColor;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            go.transform.position = new Vector3(0f, centerY, -10f);
            return camera;
        }

        private static PlayerController BuildPlayer(GameConfig config, Sprite birdSprite)
        {
            GameObject go = new GameObject("Player");
            go.transform.position = new Vector3(-1.6f, (config.PlayfieldMinY + config.PlayfieldMaxY) * 0.5f, 0f);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = birdSprite;
            renderer.sortingOrder = 10;

            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            body.gravityScale = 0f;
            // Kinematic bodies only raise trigger/collision callbacks against other
            // kinematic/static colliders when full kinematic contacts are enabled.
            body.useFullKinematicContacts = true;

            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.42f;

            return go.AddComponent<PlayerController>();
        }

        private static GameObject BuildPipePrefab(GameConfig config, Sprite pipeSprite)
        {
            float span = Mathf.Max(2f, config.PlayfieldMaxY - config.PlayfieldMinY);
            float gap = Mathf.Max(0.5f, config.PipeGapSize);
            float sectionHeight = Mathf.Max(0.5f, (span - gap) * 0.5f);

            GameObject root = new GameObject("Pipe");
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
            body.gravityScale = 0f;
            body.useFullKinematicContacts = true;

            GameObject top = BuildPipeSection("TopSection", root.transform, pipeSprite,
                (gap * 0.5f) + (sectionHeight * 0.5f));
            GameObject bottom = BuildPipeSection("BottomSection", root.transform, pipeSprite,
                -((gap * 0.5f) + (sectionHeight * 0.5f)));

            GameObject gapTrigger = new GameObject("GapTrigger");
            gapTrigger.transform.SetParent(root.transform, false);
            BoxCollider2D gapCollider = gapTrigger.AddComponent<BoxCollider2D>();
            gapCollider.isTrigger = true;
            gapCollider.size = new Vector2(0.25f, gap);

            Pipe pipe = root.AddComponent<Pipe>();
            WirePipeSections(pipe, top, bottom, gapTrigger);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PipePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // Pipe.Setup sizes each section purely through localScale, so the section must stay a
        // 1x1 unit: any pre-sizing here gets multiplied by that scale into a screen-filling slab.
        private static GameObject BuildPipeSection(string name, Transform parent, Sprite sprite, float y)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = Vector3.one;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = new Color(0.36f, 0.78f, 0.44f, 1f);
            renderer.sortingOrder = 5;

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;
            return go;
        }

        private static Canvas BuildCanvas(Camera camera)
        {
            GameObject go = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = uiLayer;

            Canvas canvas = go.GetComponent<Canvas>();
            // Screen Space - Camera (not Overlay) so ShareService can render the score card with its own camera.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
                Type inputModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputModule != null)
                {
                    eventSystemGo.AddComponent(inputModule);
                }
                else
                {
                    eventSystemGo.AddComponent<StandaloneInputModule>();
                }
            }

            return canvas;
        }

        private static HudUI BuildHud(Transform canvas)
        {
            GameObject root = NewUI("HUD", canvas);
            Stretch(root);
            HudUI hud = root.AddComponent<HudUI>();

            GameObject scoreGroupGo = NewUI("ScoreGroup", root.transform);
            Stretch(scoreGroupGo);
            CanvasGroup scoreGroup = scoreGroupGo.AddComponent<CanvasGroup>();
            scoreGroup.interactable = false;
            scoreGroup.blocksRaycasts = false;

            TextMeshProUGUI scoreLabel = NewText("ScoreLabel", scoreGroupGo.transform, "0", 170f,
                TextAlignmentOptions.Center);
            Place(scoreLabel.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f),
                new Vector2(700f, 220f));

            GameObject meterGroupGo = NewUI("MeterGroup", root.transform);
            Stretch(meterGroupGo);
            CanvasGroup meterGroup = meterGroupGo.AddComponent<CanvasGroup>();
            meterGroup.interactable = false;
            meterGroup.blocksRaycasts = false;

            GameObject meterGo = NewUI("MicMeter", meterGroupGo.transform);
            Place(meterGo, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f),
                new Vector2(660f, 46f));

            Image track = NewImage("Track", meterGo.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(track.gameObject);

            Image fill = NewImage("Fill", meterGo.transform, new Color(0.87f, 0.29f, 0.33f, 1f));
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image gateMarker = NewImage("GateMarker", meterGo.transform, new Color(1f, 1f, 1f, 0.9f));
            RectTransform gateRect = gateMarker.rectTransform;
            gateRect.anchorMin = new Vector2(0.1f, -0.35f);
            gateRect.anchorMax = new Vector2(0.1f, 1.35f);
            gateRect.pivot = new Vector2(0.5f, 0.5f);
            gateRect.sizeDelta = new Vector2(6f, 0f);
            gateRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI hint = NewText("MicHint", meterGroupGo.transform, "Sing louder - I can't hear you", 36f,
                TextAlignmentOptions.Center);
            Place(hint.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f),
                new Vector2(900f, 50f));
            hint.color = new Color(1f, 0.78f, 0.4f, 1f);

            SerializedObject so = new SerializedObject(hud);
            SetRef(so, "scoreGroup", scoreGroup);
            SetRef(so, "scoreLabel", scoreLabel);
            SetRef(so, "meterGroup", meterGroup);
            SetRef(so, "micLevelFill", fill);
            SetRef(so, "micGateMarker", gateRect);
            SetRef(so, "micHintLabel", hint);
            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        private static PitchMeterUI BuildPitchMeter(Transform canvas)
        {
            GameObject root = NewUI("PitchMeter", canvas);
            Place(root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-48f, 40f),
                new Vector2(112f, 1150f));
            PitchMeterUI meter = root.AddComponent<PitchMeterUI>();

            GameObject bandGroupGo = NewUI("BandGroup", root.transform);
            Stretch(bandGroupGo);
            CanvasGroup bandGroup = bandGroupGo.AddComponent<CanvasGroup>();
            bandGroup.interactable = false;
            bandGroup.blocksRaycasts = false;

            Image bandBg = NewImage("BandBackground", bandGroupGo.transform, new Color(0f, 0f, 0f, 0.5f));
            Stretch(bandBg.gameObject);

            const int segments = 12;
            for (int i = 0; i < segments; i++)
            {
                float mid = (i + 0.5f) / segments;
                Color color = PitchMeterUI.ColorForHeight(mid);
                color.a = 0.55f;
                Image segment = NewImage($"Segment{i:00}", bandGroupGo.transform, color);
                RectTransform rect = segment.rectTransform;
                rect.anchorMin = new Vector2(0f, (float)i / segments);
                rect.anchorMax = new Vector2(1f, (i + 1f) / segments);
                rect.offsetMin = new Vector2(0f, 1f);
                rect.offsetMax = new Vector2(0f, -1f);
            }

            Image wrapLineTop = NewImage("WrapLineTop", bandGroupGo.transform, Color.white);
            RectTransform wrapLineTopRect = wrapLineTop.rectTransform;
            wrapLineTopRect.anchorMin = new Vector2(0f, 1f);
            wrapLineTopRect.anchorMax = new Vector2(1f, 1f);
            wrapLineTopRect.pivot = new Vector2(0.5f, 1f);
            wrapLineTopRect.sizeDelta = new Vector2(16f, 8f);
            wrapLineTopRect.anchoredPosition = Vector2.zero;

            Image wrapLineBottom = NewImage("WrapLineBottom", bandGroupGo.transform, Color.white);
            RectTransform wrapLineBottomRect = wrapLineBottom.rectTransform;
            wrapLineBottomRect.anchorMin = new Vector2(0f, 0f);
            wrapLineBottomRect.anchorMax = new Vector2(1f, 0f);
            wrapLineBottomRect.pivot = new Vector2(0.5f, 0f);
            wrapLineBottomRect.sizeDelta = new Vector2(16f, 8f);
            wrapLineBottomRect.anchoredPosition = Vector2.zero;

            Image wrapFlashTop = NewImage("WrapFlashTop", bandGroupGo.transform, new Color(1f, 1f, 1f, 0f));
            RectTransform flashTopRect = wrapFlashTop.rectTransform;
            flashTopRect.anchorMin = new Vector2(0f, 0.9f);
            flashTopRect.anchorMax = new Vector2(1f, 1f);
            flashTopRect.offsetMin = Vector2.zero;
            flashTopRect.offsetMax = Vector2.zero;

            Image wrapFlashBottom = NewImage("WrapFlashBottom", bandGroupGo.transform, new Color(1f, 1f, 1f, 0f));
            RectTransform flashBottomRect = wrapFlashBottom.rectTransform;
            flashBottomRect.anchorMin = new Vector2(0f, 0f);
            flashBottomRect.anchorMax = new Vector2(1f, 0.1f);
            flashBottomRect.offsetMin = Vector2.zero;
            flashBottomRect.offsetMax = Vector2.zero;

            Image indicator = NewImage("Indicator", bandGroupGo.transform, Color.white);
            RectTransform indicatorRect = indicator.rectTransform;
            indicatorRect.anchorMin = new Vector2(0f, 0.5f);
            indicatorRect.anchorMax = new Vector2(1f, 0.5f);
            indicatorRect.pivot = new Vector2(0.5f, 0.5f);
            indicatorRect.sizeDelta = new Vector2(28f, 26f);
            indicatorRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI noteLabel = NewText("NoteLabel", root.transform, "--", 48f, TextAlignmentOptions.Center);
            Place(noteLabel.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 16f),
                new Vector2(260f, 64f));

            TextMeshProUGUI statusLabel = NewText("StatusLabel", root.transform, "sing to set your low note", 32f,
                TextAlignmentOptions.Right);
            Place(statusLabel.gameObject, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, -18f),
                new Vector2(620f, 60f));
            statusLabel.color = new Color(1f, 1f, 1f, 0.8f);

            SerializedObject so = new SerializedObject(meter);
            SetRef(so, "indicator", indicatorRect);
            SetRef(so, "indicatorGraphic", indicator);
            SetRef(so, "wrapFlashTop", wrapFlashTop);
            SetRef(so, "wrapFlashBottom", wrapFlashBottom);
            SetRef(so, "bandGroup", bandGroup);
            SetRef(so, "noteLabel", noteLabel);
            SetRef(so, "statusLabel", statusLabel);
            so.ApplyModifiedPropertiesWithoutUndo();
            return meter;
        }

        private static EndScreenUI BuildEndScreen(Transform canvas, Camera camera)
        {
            GameObject root = NewUI("EndScreen", canvas);
            Stretch(root);
            EndScreenUI endScreen = root.AddComponent<EndScreenUI>();

            GameObject panel = NewUI("Panel", root.transform);
            Stretch(panel);

            Image dim = NewImage("Dim", panel.transform, new Color(0f, 0f, 0f, 0.72f));
            Stretch(dim.gameObject);

            GameObject card = NewUI("ScoreCard", panel.transform);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f),
                new Vector2(860f, 880f));

            Image cardBg = NewImage("CardBackground", card.transform, CardColor);
            Stretch(cardBg.gameObject);

            TextMeshProUGUI title = NewText("Title", card.transform, "FLAPPY VOICE", 56f, TextAlignmentOptions.Center);
            Place(title.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f),
                new Vector2(800f, 80f));

            TextMeshProUGUI caption = NewText("ScoreCaption", card.transform, "SCORE", 40f,
                TextAlignmentOptions.Center);
            Place(caption.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f),
                new Vector2(800f, 60f));
            caption.color = new Color(1f, 1f, 1f, 0.7f);

            TextMeshProUGUI finalScore = NewText("FinalScore", card.transform, "0", 220f, TextAlignmentOptions.Center);
            Place(finalScore.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -290f),
                new Vector2(800f, 280f));

            TextMeshProUGUI bestScore = NewText("BestScore", card.transform, "Best 0", 48f,
                TextAlignmentOptions.Center);
            Place(bestScore.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f),
                new Vector2(800f, 70f));

            Image badge = NewImage("NewBestBadge", card.transform, new Color(1f, 0.78f, 0.22f, 1f));
            Place(badge.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f),
                new Vector2(420f, 80f));
            TextMeshProUGUI badgeLabel = NewText("Label", badge.transform, "NEW BEST!", 44f,
                TextAlignmentOptions.Center);
            Stretch(badgeLabel.gameObject);
            badgeLabel.color = new Color(0.12f, 0.10f, 0.05f, 1f);
            badge.gameObject.SetActive(false);

            Button playAgain = NewButton("PlayAgainButton", panel.transform, "Play Again",
                new Color(0.30f, 0.78f, 0.52f, 1f), new Vector2(0f, -520f));
            Button share = NewButton("ShareButton", panel.transform, "Share",
                new Color(0.32f, 0.52f, 0.92f, 1f), new Vector2(0f, -700f));

            panel.SetActive(false);

            SerializedObject so = new SerializedObject(endScreen);
            SetRef(so, "panel", panel);
            SetRef(so, "scoreCardRoot", (RectTransform)card.transform);
            SetRef(so, "uiCamera", camera);
            SetRef(so, "finalScoreLabel", finalScore);
            SetRef(so, "bestScoreLabel", bestScore);
            SetRef(so, "newBestBadge", badge.gameObject);
            SetRef(so, "playAgainButton", playAgain);
            SetRef(so, "shareButton", share);
            so.ApplyModifiedPropertiesWithoutUndo();
            return endScreen;
        }

        private static Button NewButton(string name, Transform parent, string label, Color color, Vector2 position)
        {
            Image background = NewImage(name, parent, color);
            Place(background.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
                new Vector2(620f, 140f));
            background.raycastTarget = true;
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            TextMeshProUGUI text = NewText("Label", background.transform, label, 56f, TextAlignmentOptions.Center);
            Stretch(text.gameObject);
            return button;
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = uiLayer;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            GameObject go = NewUI(name, parent);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string content, float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject go = NewUI(name, parent);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = ResolveFont();
            if (font != null)
            {
                text.font = font;
            }
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(GameObject go)
        {
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Place(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static TMP_FontAsset ResolveFont()
        {
            if (fontResolved)
            {
                return cachedFont;
            }
            fontResolved = true;
            try
            {
                cachedFont = TMP_Settings.defaultFontAsset;
            }
            catch (Exception)
            {
                cachedFont = null;
            }
            if (cachedFont == null)
            {
                Debug.LogWarning("[SceneBuilder] No TMP default font asset. Import TMP Essential Resources.");
            }
            return cachedFont;
        }

        private static void SetRef(SerializedObject so, string field, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[SceneBuilder] missing serialized field '{field}' on {so.targetObject.GetType().Name}");
                return;
            }
            property.objectReferenceValue = value;
        }

        private static void WireBootstrap(GameBootstrap bootstrap, GameConfig config,
            GameStateManager stateManager, ScoreManager scoreManager, ShareService shareService,
            MicrophoneInput microphoneInput, PitchTracker pitchTracker, VoiceHeightSource voiceHeight,
            AttractPilot attractPilot, PipeSpawner pipeSpawner, PlayerController player,
            HudUI hud, PitchMeterUI pitchMeter, EndScreenUI endScreen)
        {
            SerializedObject so = new SerializedObject(bootstrap);
            SetRef(so, "config", AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath));
            SetRef(so, "stateManager", stateManager);
            SetRef(so, "scoreManager", scoreManager);
            SetRef(so, "pipeSpawner", pipeSpawner);
            SetRef(so, "attractPilot", attractPilot);
            SetRef(so, "voiceHeightSource", voiceHeight);
            SetRef(so, "player", player);
            SetRef(so, "pitchTracker", pitchTracker);
            SetRef(so, "microphoneInput", microphoneInput);
            SetRef(so, "shareService", shareService);
            SetRef(so, "hud", hud);
            SetRef(so, "pitchMeter", pitchMeter);
            SetRef(so, "endScreen", endScreen);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WirePipeSections(Pipe pipe, GameObject top, GameObject bottom, GameObject gap)
        {
            SerializedObject so = new SerializedObject(pipe);
            SerializedProperty property = so.GetIterator();
            bool changed = false;
            while (property.NextVisible(true))
            {
                if (!IsWirableReference(property))
                {
                    continue;
                }
                string name = property.name.ToLowerInvariant();
                GameObject source =
                    name.Contains("top") || name.Contains("upper") ? top :
                    name.Contains("bottom") || name.Contains("lower") ? bottom :
                    name.Contains("gap") || name.Contains("trigger") || name.Contains("score") ? gap : null;
                if (source == null)
                {
                    continue;
                }
                UnityEngine.Object value = ResolveOn(source, FieldTypeName(property));
                if (value == null)
                {
                    continue;
                }
                property.objectReferenceValue = value;
                changed = true;
            }
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AutoWireByType(Component target, UnityEngine.Object[] candidates)
        {
            if (target == null)
            {
                return;
            }
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.GetIterator();
            bool changed = false;
            while (property.NextVisible(true))
            {
                if (!IsWirableReference(property))
                {
                    continue;
                }
                string typeName = FieldTypeName(property);
                if (string.IsNullOrEmpty(typeName) || IsAmbiguousFieldType(typeName))
                {
                    continue;
                }
                if (typeName == "GameObject" && !property.name.ToLowerInvariant().Contains("prefab"))
                {
                    continue;
                }
                foreach (UnityEngine.Object candidate in candidates)
                {
                    if (candidate == null || ReferenceEquals(candidate, target) || !MatchesType(candidate, typeName))
                    {
                        continue;
                    }
                    property.objectReferenceValue = candidate;
                    changed = true;
                    break;
                }
            }
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool IsAmbiguousFieldType(string typeName)
        {
            switch (typeName)
            {
                case "Transform":
                case "RectTransform":
                case "Component":
                case "Behaviour":
                case "MonoBehaviour":
                case "ScriptableObject":
                case "Object":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsWirableReference(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.ObjectReference
                && property.name != "m_Script"
                && property.objectReferenceValue == null
                && !property.propertyPath.Contains("Array.data");
        }

        private static string FieldTypeName(SerializedProperty property)
        {
            string raw = property.type;
            int start = raw.IndexOf('$');
            int end = raw.LastIndexOf('>');
            return start >= 0 && end > start ? raw.Substring(start + 1, end - start - 1) : raw;
        }

        private static bool MatchesType(UnityEngine.Object candidate, string typeName)
        {
            for (Type type = candidate.GetType(); type != null; type = type.BaseType)
            {
                if (type.Name == typeName)
                {
                    return true;
                }
            }
            return false;
        }

        private static UnityEngine.Object ResolveOn(GameObject go, string typeName)
        {
            if (typeName == "GameObject")
            {
                return go;
            }
            if (typeName == "Transform" || typeName == "RectTransform")
            {
                return go.transform;
            }
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component != null && MatchesType(component, typeName))
                {
                    return component;
                }
            }
            return null;
        }

        private static void RegisterInBuildSettings()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path != ScenePath)
                {
                    continue;
                }
                if (!existing[i].enabled)
                {
                    existing[i].enabled = true;
                    EditorBuildSettings.scenes = existing;
                }
                return;
            }

            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[existing.Length + 1];
            updated[0] = new EditorBuildSettingsScene(ScenePath, true);
            Array.Copy(existing, 0, updated, 1, existing.Length);
            EditorBuildSettings.scenes = updated;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "Assets" || AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Sprite EnsureBirdSprite()
        {
            return EnsureSprite(BirdSpritePath, 64, 64, PaintBird, SpriteMeshType.FullRect);
        }

        private static Sprite EnsurePipeSprite()
        {
            return EnsureSprite(PipeSpritePath, 64, 64, PaintPipe, SpriteMeshType.FullRect);
        }

        private static Color32 PaintBird(int x, int y, int width, int height)
        {
            float cx = (width - 1) * 0.5f;
            float cy = (height - 1) * 0.5f;
            float radius = width * 0.45f;
            float distance = Mathf.Sqrt(((x - cx) * (x - cx)) + ((y - cy) * (y - cy)));

            bool beak = x > cx + radius * 0.55f && x < cx + radius * 1.5f
                && Mathf.Abs(y - (cy + 2f)) < (radius * 1.5f - (x - cx)) * 0.45f;
            if (beak)
            {
                return new Color32(240, 150, 40, 255);
            }
            if (distance > radius)
            {
                return new Color32(0, 0, 0, 0);
            }
            float eyeDistance = Mathf.Sqrt(((x - (cx + radius * 0.35f)) * (x - (cx + radius * 0.35f)))
                + ((y - (cy + radius * 0.30f)) * (y - (cy + radius * 0.30f))));
            if (eyeDistance < radius * 0.14f)
            {
                return new Color32(25, 25, 35, 255);
            }
            if (distance > radius - 3f)
            {
                return new Color32(180, 110, 30, 255);
            }
            return new Color32(255, 205, 80, 255);
        }

        private static Color32 PaintPipe(int x, int y, int width, int height)
        {
            bool border = x < 3 || y < 3 || x >= width - 3 || y >= height - 3;
            return border ? new Color32(210, 220, 230, 255) : new Color32(255, 255, 255, 255);
        }

        private static Sprite EnsureSprite(string path, int width, int height,
            Func<int, int, int, int, Color32> painter, SpriteMeshType meshType)
        {
            if (!File.Exists(path))
            {
                Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Color32[] pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        pixels[(y * width) + x] = painter(x, y, width, height);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                bool dirty = importer.textureType != TextureImporterType.Sprite
                    || importer.spriteImportMode != SpriteImportMode.Single
                    || !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit)
                    || importer.mipmapEnabled
                    || !importer.alphaIsTransparency
                    || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || settings.spriteMeshType != meshType;

                if (dirty)
                {
                    settings.textureType = TextureImporterType.Sprite;
                    settings.spriteMode = (int)SpriteImportMode.Single;
                    settings.spriteMeshType = meshType;
                    settings.spriteExtrude = 0;
                    settings.spritePixelsPerUnit = PixelsPerUnit;
                    settings.alphaIsTransparency = true;
                    settings.mipmapEnabled = false;
                    settings.filterMode = FilterMode.Bilinear;
                    settings.wrapMode = TextureWrapMode.Clamp;
                    importer.SetTextureSettings(settings);
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[SceneBuilder] failed to load placeholder sprite at {path}");
            }
            return sprite;
        }
    }
}
