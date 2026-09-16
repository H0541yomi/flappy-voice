using System;
using System.IO;
using FlappyVoice.Audio;
using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using FlappyVoice.Platform;
using FlappyVoice.UI;
using FlappyVoice.Video;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlappyVoice.Editor
{
    public static class SceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string SettingsFolder = "Assets/Settings";
        private const float EdgeNoteMarginUnits = 1f;

        private const string ConfigPath = "Assets/Settings/GameConfig.asset";
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string PipePrefabPath = "Assets/Prefabs/Pipe.prefab";
        // Named because WirePipeSections has to hand Pipe the tube apart from its section.
        private const string TubeChildName = "Tube";
        private const string ArtFolder = "Assets/Art/Placeholder";
        private const string AudioFolder = "Assets/Audio";
        private const string BirdSpritePath = "Assets/Art/Placeholder/Bird.png";
        private const string PipeSpritePath = "Assets/Art/Placeholder/PipeSection.png";
        // Generated art. Each falls back to its procedural placeholder when absent, so a partial
        // art drop still builds a runnable scene.
        private const string TubeSpritePath = "Assets/Art/Trumpets/tube.png";
        private const string BellTopSpritePath = "Assets/Art/Trumpets/bell_top.png";
        private const string BellBottomSpritePath = "Assets/Art/Trumpets/bell_bottom.png";
        private const string BirdIdleSpritePath = "Assets/Art/Bird/bird_idle.png";
        private const string BirdSingSpritePath = "Assets/Art/Bird/bird_sing.png";
        private const string BirdDeadSpritePath = "Assets/Art/Bird/bird_dead.png";
        private const string BirdFlashSpritePath = "Assets/Art/Bird/bird_flash.png";
        private const string SkySpritePath = "Assets/Art/Bg/sky.png";
        private static readonly string[] NoteSpritePaths =
        {
            "Assets/Art/Fx/note_a.png", "Assets/Art/Fx/note_b.png", "Assets/Art/Fx/note_c.png",
        };
        private const string TunerPillSpritePath = "Assets/Art/Ui/tuner_pill.png";
        private const string SafeBandSpritePath = "Assets/Art/Ui/safe_band.png";
        private const string NeedleSpritePath = "Assets/Art/Ui/needle.png";
        private const string SignSpritePath = "Assets/Art/Ui/start_sign.png";
        private const string ButtonSpritePath = "Assets/Art/Ui/button.png";
        private const string SmallSignSpritePath = "Assets/Art/Ui/sign_small.png";
        private const string ButtonPrimarySpritePath = "Assets/Art/Ui/button_primary.png";
        private const string XButtonSpritePath = "Assets/Art/Ui/x_button.png";
        private const string HeartFullSpritePath = "Assets/Art/Ui/heart_full.png";
        private const string HeartEmptySpritePath = "Assets/Art/Ui/heart_empty.png";
        private const string TitleMaterialPath = "Assets/Art/Ui/SignTitle.mat";
        private const string WebCamMaterialPath = "Assets/Art/Ui/WebCamFeed.mat";
        private const int WebCamSortingOrder = -65;

        // Scenery, back to front: sprite, world Y of the layer's centre, vertical scale, scroll
        // factor against the pipe speed, sorting order. Sky is handled separately - it does not
        // tile or scroll.
        //
        // The layers are drawn at their own aspect, so their heights are whatever the art is; the
        // scales exist to make each band overlap the one below it. Both edges are ragged
        // silhouettes, and a layer that merely meets the next one lets the sky show through the
        // notches in between.
        // Wider than any phone and wider than a maximised Game view on a 16:10 laptop. Coverage
        // is cheap - a few extra sprites - and running out of it shows the camera's clear colour
        // down the sides of the screen.
        private const float WidestSupportedAspect = 2.4f;

        private static readonly (string Path, float CenterY, float Scale, float Factor, int Order)[] ParallaxLayers =
        {
            ("Assets/Art/Bg/clouds.png", 3.2f, 1.00f, 0.03f, -95),
            ("Assets/Art/Bg/far.png", 0.55f, 1.10f, 0.06f, -90),
            ("Assets/Art/Bg/mid.png", -1.30f, 1.15f, 0.12f, -80),
            ("Assets/Art/Bg/near.png", -3.50f, 1.30f, 0.22f, -70),
        };
        private const int PixelsPerUnit = 64;

        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        // Hearts sit in the bottom-right corner, clear of the tuner strip at the top and of the
        // score. The margin is generous because phones round that corner off.
        private const float HeartSize = 96f;
        private const float HeartSpacing = 12f;
        private const float HeartMargin = 56f;
        private static readonly Color SkyColor = new Color(0.16f, 0.20f, 0.34f, 1f);

        // start_sign.png is 1024x1866 and is deliberately NOT 9-sliced: the leaf-and-flower
        // clusters sit too far into two of its corners for any border to hold them, so a stretch
        // would smear them. Both panels are therefore authored at the sprite's own aspect.
        private const float SignAspect = 1866f / 1024f;
        private static readonly Vector2 SignSize = new Vector2(860f, 860f * SignAspect);

        // Measured off the sprite, as fractions of the sign's height: the band where the cream
        // face is at least 77% of the sign's width. Outside it the deckled edge is tapering in,
        // so anything placed there hangs off the parchment - which is what put the Share button
        // half over the torn bottom edge. Content lives between these two.
        private const float SignFaceTop = 0.219f * (860f * SignAspect);
        private const float SignFaceBottom = 0.812f * (860f * SignAspect);

        // sign_small.png is the same parchment at a squatter aspect, for the consent panels: a
        // title, a sentence and two buttons would leave the tall sign a third empty. Not
        // 9-sliced either - its corner flowers smear under a stretch exactly like the tall one's.
        private const float SmallSignAspect = 1215f / 1024f;
        private static readonly Vector2 SmallSignSize = new Vector2(820f, 820f * SmallSignAspect);

        // Measured off the sprite the same way as SignFaceTop/Bottom. The leaf clusters cut into
        // the top-right and bottom-left corners of the face, so content here stays both inside
        // this band and narrower than the band is wide.
        private const float SmallSignFaceTop = 0.110f * (820f * SmallSignAspect);
        private const float SmallSignFaceBottom = 0.928f * (820f * SmallSignAspect);

        // The tuner strip is a compact pill at the top-centre, not a full-width bar: at full
        // width it covered the whole top of the screen and the bird disappeared behind it
        // whenever it flew high. These three drive both the strip and the camera - BuildCamera
        // reads TunerScreenFraction to keep the playfield underneath.
        private const float TunerTopMarginPx = 20f;
        private const float TunerBarWidthPx = 600f;
        private const float TunerDialBottomPadPx = 44f;

        // The strip's contents belong to the pill's FACE, not to its rect. tuner_pill.png is a
        // raised rim around a dark face and its ends are strongly rounded, so a ruler drawn to the
        // rect's edge hangs its outer ticks in the sky and the safe band squares off over a rounded
        // end. These are that face, measured off the sprite through its own 9-slice: the borders
        // are 72 sprite px at 256 px/unit against the canvas's 100 reference, so they stay 28 rect
        // px wide whatever the rect is and the inset does not move with the strip's size.
        private const float TunerFaceInsetXPx = 28f;
        private const float TunerFaceInsetYPx = 18f;

        // The face is the content box - ticks, letters, and the pad that centres the pair. The pill
        // is that plus its rim, so the strip's height is derived from the two: a thicker rim can
        // then only make the pill taller, never quietly crop the ruler inside it.
        private const float TunerFaceHeightPx = 180f;
        private const float TunerBarHeightPx = TunerFaceHeightPx + (TunerFaceInsetYPx * 2f);

        // Canvas match mode is height, so canvas pixels ARE a fixed fraction of the view.
        private const float TunerScreenFraction =
            (TunerTopMarginPx + TunerBarHeightPx) / 1920f;

        // Gap between the bottom of the tuner strip and the top of the in-run score. The score is
        // the strip's readout in another form - the note you are holding and what it has won you -
        // so they read as one block at the top of the screen rather than two separate HUDs.
        private const float ScoreLabelGapPx = 40f;
        private const float ScoreLabelTopFromTop =
            TunerTopMarginPx + TunerBarHeightPx + ScoreLabelGapPx;

        // Room demanded between the top of the playfield and the bottom of the strip, on top of
        // whichever is taller there - the bird or the gap opening.
        private const float HudClearanceUnits = 0.25f;

        // Ink on parchment. White text is invisible on the sign, so nothing placed on one may keep
        // the default NewText colour.
        // #501713, the ink the signs are drawn in, and a lifted version of it for the quiet lines.
        // One palette for the whole app: the only text that is NOT ink is the in-run score, which
        // floats over the playfield rather than sitting on parchment and stays white.
        private static readonly Color InkColor = new Color(0.314f, 0.090f, 0.075f, 1f);
        private static readonly Color MutedInkColor = new Color(0.484f, 0.259f, 0.243f, 1f);

        // #FBD97B. Ink on dark timber would be unreadable, so a plaque label is gold instead.
        private static readonly Color ButtonLabelColor = new Color(0.984f, 0.851f, 0.482f, 1f);

        // Widths and type sizes come across from the design frame by simple ratio: 820 of sign
        // for its 626.
        private const float ConsentDesignScale = 820f / 626f;

        // Heights come across as FRACTIONS of the design frame, not as scaled pixels. The design
        // stretches the parchment to a 1.401 aspect where the sprite's own is 1.187, and it is not
        // 9-sliced, so matching the design's pixel heights would mean smearing the corner flowers.
        // The vertical rhythm is preserved, the artwork is not distorted.
        private const float ConsentTitleCenterFromTop = 0.3255f * (820f * SmallSignAspect);
        private const float ConsentBodyTopFromTop = 0.4037f * (820f * SmallSignAspect);
        private const float ConsentButtonRowCenterFromTop = 0.6847f * (820f * SmallSignAspect);

        // The design's button is 228x57, a 4:1 box - which is button.png's own aspect, so the
        // height is derived from the sprite and both stay true at once.
        private const float ButtonAspect = 640f / 160f;
        private static readonly Vector2 ConsentButtonSize =
            new Vector2(228f * ConsentDesignScale, 228f * ConsentDesignScale / ButtonAspect);

        // The quit button lives in the corner of the SCREEN, so its only neighbour is the tuner
        // pill: 600 px wide and centred, which leaves it the outer 240 px of a 1080 canvas. The
        // canvas matches on height, so a narrower phone shrinks that margin rather than the pill -
        // at 9:19.5, the tightest aspect a phone actually ships, the two still clear by ~38 px.
        //
        // It also has to stay clear of the end screen's capture rect, which ShareService fills
        // with whatever UI-layer graphic falls inside it. The rect stops 1743 px up a 1920 px
        // canvas and this button starts at 1796, so the shared card keeps it out.
        private const float QuitButtonSizePx = 88f;
        private const float QuitButtonMarginPx = 36f;

        // The notice that the microphone never arrived: the same parchment as the consent panels,
        // one message and the same button row, laid out on their vertical rhythm so the two do not
        // appear to jump when one replaces the other.
        private const float MicNoticeMessageCenterFromTop = 0.432f * (820f * SmallSignAspect);
        private static readonly Vector2 MicNoticeMessageSize = new Vector2(620f, 330f);

        // Gap between "BEST" and the numeral beside it. The word carries 4 px of tracking, so a
        // wider gap here would read as part of that spacing rather than as a space.
        private const float BestScoreRowSpacingPx = 14f;

        // The ask and the reason for it, in one block. A size of its own rather than the consent
        // title's: this is three lines where the consent panel has one of each, and at the title's
        // 44 it would run past the message box and off the parchment face.
        private const string MicNoticeMessage = "please enable microphone. this is a sound based game!";
        private const float MicNoticeMessageFontSize = 38f * ConsentDesignScale;

        private static TMP_FontAsset cachedFont;
        private static bool fontResolved;
        private static TMP_FontAsset cachedNumberFont;
        private static bool numberFontResolved;
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
            numberFontResolved = false;
            cachedNumberFont = null;
            int namedUiLayer = LayerMask.NameToLayer("UI");
            uiLayer = namedUiLayer >= 0 ? namedUiLayer : 5;

            EnsureFolder(ScenesFolder);
            EnsureFolder(SettingsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(ArtFolder);
            EnsureFolder(AudioFolder);

            GameConfig config = EnsureConfig();
            Sprite birdSprite = LoadSpriteOr(BirdIdleSpritePath, EnsureBirdSprite);
            Sprite pipeSprite = LoadSpriteOr(TubeSpritePath, EnsurePipeSprite);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Sprite import invalidates any reference captured before it, and a stale reference
            // serialises as null without an error. Re-load after every asset write.
            config = ReloadConfig(config);
            GameObject pipePrefab = BuildPipePrefab(config, pipeSprite);
            config = ReloadConfig(config);
            Camera camera = BuildCamera(config);
            ParallaxBackground background = BuildBackground(config, camera);
            WebCam webCamBackground = BuildWebCamBackground(background.transform, camera);
            SingingFx singingFx = BuildSingingFx();
            GameObject managersGo = new GameObject("Managers");
            GameStateManager stateManager = managersGo.AddComponent<GameStateManager>();
            ScoreManager scoreManager = managersGo.AddComponent<ScoreManager>();
            LivesManager livesManager = managersGo.AddComponent<LivesManager>();
            ShareService shareService = managersGo.AddComponent<ShareService>();

            GameObject audioGo = new GameObject("Audio");
            MicrophoneInput microphoneInput = audioGo.AddComponent<MicrophoneInput>();
            PitchTracker pitchTracker = audioGo.AddComponent<PitchTracker>();
            GameAudio gameAudio = BuildGameAudio(audioGo.transform);

            GameObject heightGo = new GameObject("HeightSources");
            VoiceHeightSource voiceHeight = heightGo.AddComponent<VoiceHeightSource>();
            AttractPilot attractPilot = heightGo.AddComponent<AttractPilot>();

            GameObject spawnerGo = new GameObject("PipeSpawner");
            PipeSpawner pipeSpawner = spawnerGo.AddComponent<PipeSpawner>();

            PlayerController player = BuildPlayer(config, birdSprite);

            Canvas canvas = BuildCanvas(camera);
            HudUI hud = BuildHud(canvas.transform);
            LivesUI livesUI = BuildLivesUI(canvas.transform, config);
            TunerBarUI tunerBar = BuildTunerBar(canvas.transform);
            EndScreenUI endScreen = BuildEndScreen(canvas.transform, camera);
            // Last, so it is the topmost thing on the canvas: it is the first screen a player
            // sees and everything else is behind it.
            ConsentFlowUI consentFlow = BuildConsentFlow(canvas.transform);
            MicrophoneNoticeUI microphoneNotice = BuildMicrophoneNotice(canvas.transform);
            // Last of all, so nothing can end up drawn over it - including the consent flow's
            // full-screen blocking dim, which is the one graphic in the scene that eats taps.
            QuitButtonUI quitButton = BuildQuitButton(canvas.transform);

            pitchTracker.Configure(config);
            pitchTracker.SetMicrophoneInput(microphoneInput);
            voiceHeight.Configure(config, pitchTracker);
            player.Configure(config, stateManager, voiceHeight, attractPilot);
            hud.Configure(scoreManager, stateManager);
            // Configured here only so the authored row shows a full set of hearts: LivesUI renders
            // whatever LivesManager currently holds, and an unconfigured one holds zero.
            livesManager.Configure(config, stateManager);
            livesUI.Configure(livesManager, stateManager);
            tunerBar.Configure(config, voiceHeight, pipeSpawner, player, stateManager);
            endScreen.Configure(stateManager, scoreManager, shareService);
            consentFlow.Configure(hud);
            microphoneNotice.Configure(hud);
            quitButton.Configure(stateManager);

            UnityEngine.Object[] candidates =
            {
                config, stateManager, scoreManager, livesManager, shareService, microphoneInput, pitchTracker, gameAudio,
                voiceHeight, attractPilot, pipeSpawner, background, webCamBackground, singingFx, player, camera, hud, livesUI, tunerBar, endScreen,
                consentFlow, microphoneNotice, quitButton,
                pipePrefab, pipePrefab != null ? pipePrefab.GetComponent<Pipe>() : null
            };

            AutoWireByType(stateManager, candidates);
            AutoWireByType(scoreManager, candidates);
            AutoWireByType(shareService, candidates);
            AutoWireByType(microphoneInput, candidates);
            AutoWireByType(pitchTracker, candidates);
            AutoWireByType(gameAudio, candidates);
            AutoWireByType(voiceHeight, candidates);
            AutoWireByType(attractPilot, candidates);
            AutoWireByType(pipeSpawner, candidates);
            AutoWireByType(background, candidates);
            AutoWireByType(webCamBackground, candidates);
            AutoWireByType(singingFx, candidates);
            AutoWireByType(player, candidates);
            AutoWireByType(hud, candidates);
            AutoWireByType(livesUI, candidates);
            AutoWireByType(tunerBar, candidates);
            AutoWireByType(endScreen, candidates);
            AutoWireByType(consentFlow, candidates);
            AutoWireByType(microphoneNotice, candidates);
            AutoWireByType(quitButton, candidates);

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
                pitchTracker, gameAudio, voiceHeight, attractPilot, pipeSpawner, background, webCamBackground, singingFx, livesManager, player, camera, hud,
                livesUI, tunerBar, endScreen, consentFlow, microphoneNotice, quitButton);
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

        private static GameConfig ReloadConfig(GameConfig current)
        {
            GameConfig fresh = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (fresh == null)
            {
                Debug.LogWarning($"[SceneBuilder] could not re-load GameConfig at {ConfigPath}");
                return current;
            }
            return fresh;
        }

        private static Camera BuildCamera(GameConfig config)
        {
            GameObject go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            Camera camera = go.GetComponent<Camera>();
            camera.orthographic = true;

            // Edge notes put a gap centre right on the playfield bound, so the bottom keeps a
            // margin or offset 0 lands on the screen edge with half its gap cut off. The TOP has
            // to clear the tuner strip as well, and how much world that strip covers depends on
            // the camera size we are solving for - HudLayout does that in one step, and
            // HudLayoutTests pins that the playfield really does end below the strip.
            float topClearance = Mathf.Max(config.PlayerBodyRadiusUnits,
                config.PipeGapSizeAtDifficulty(0f) * 0.5f) + HudClearanceUnits;
            HudLayout.CameraForPlayfield(config.PlayfieldMinY, config.PlayfieldMaxY,
                EdgeNoteMarginUnits, topClearance, TunerScreenFraction,
                out float orthographicSize, out float centerY);

            camera.orthographicSize = orthographicSize;
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
            collider.radius = config.PlayerBodyRadiusUnits;

            PlayerController player = go.AddComponent<PlayerController>();

            // The three poses share one silhouette and one PPU, so swapping the sprite moves
            // nothing: the bird changes shape in place.
            SerializedObject so = new SerializedObject(player);
            SetRef(so, "_renderer", renderer);
            SetRef(so, "_idleSprite", birdSprite);
            SetRef(so, "_singSprite", AssetDatabase.LoadAssetAtPath<Sprite>(BirdSingSpritePath));
            SetRef(so, "_deadSprite", AssetDatabase.LoadAssetAtPath<Sprite>(BirdDeadSpritePath));
            SetRef(so, "_flashSprite", AssetDatabase.LoadAssetAtPath<Sprite>(BirdFlashSpritePath));
            so.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        private static GameObject BuildPipePrefab(GameConfig config, Sprite pipeSprite)
        {
            float span = Mathf.Max(2f, config.PlayfieldMaxY - config.PlayfieldMinY);
            float gap = Mathf.Max(0.5f, config.PipeGapSizeAtDifficulty(0f));
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

            // Parented to the root, not to a section: sections are 1x1 quads that Pipe.Setup
            // stretches by localScale, and a child would inherit that stretch.
            GameObject topBell = BuildBell("TopBell", root.transform,
                AssetDatabase.LoadAssetAtPath<Sprite>(BellTopSpritePath), gap * 0.5f);
            GameObject bottomBell = BuildBell("BottomBell", root.transform,
                AssetDatabase.LoadAssetAtPath<Sprite>(BellBottomSpritePath), -gap * 0.5f);

            GameObject gapTrigger = new GameObject("GapTrigger");
            gapTrigger.transform.SetParent(root.transform, false);
            BoxCollider2D gapCollider = gapTrigger.AddComponent<BoxCollider2D>();
            gapCollider.isTrigger = true;
            gapCollider.size = new Vector2(0.25f, gap);

            Pipe pipe = root.AddComponent<Pipe>();
            WirePipeSections(pipe, top, bottom, gapTrigger, topBell, bottomBell,
                top.transform.Find(TubeChildName).gameObject,
                bottom.transform.Find(TubeChildName).gameObject);

            PrefabUtility.SaveAsPrefabAsset(root, PipePrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            // Re-load rather than trusting the value SaveAsPrefabAsset returned: the reference is
            // captured before the asset import settles, and a stale one serialises as null.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PipePrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[SceneBuilder] failed to load pipe prefab at {PipePrefabPath}");
            }
            return prefab;
        }

        // A bell sprite is authored at true world size (256 px per unit) with its pivot on the
        // flare rim, so it is placed at scale 1 straight onto the gap edge.
        private static GameObject BuildBell(string name, Transform parent, Sprite sprite, float y)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = Vector3.one;

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingOrder = 6;
            return go;
        }

        private static SingingFx BuildSingingFx()
        {
            GameObject go = new GameObject("SingingFx");
            SingingFx fx = go.AddComponent<SingingFx>();

            Sprite[] notes = new Sprite[NoteSpritePaths.Length];
            for (int i = 0; i < notes.Length; i++)
            {
                notes[i] = AssetDatabase.LoadAssetAtPath<Sprite>(NoteSpritePaths[i]);
                if (notes[i] == null)
                {
                    Debug.LogWarning($"[SceneBuilder] missing {NoteSpritePaths[i]}");
                }
            }

            SerializedObject so = new SerializedObject(fx);
            SetRefArray(so, "_noteSprites", notes);
            so.ApplyModifiedPropertiesWithoutUndo();
            return fx;
        }

        private static ParallaxBackground BuildBackground(GameConfig config, Camera camera)
        {
            GameObject root = new GameObject("Background");
            ParallaxBackground parallax = root.AddComponent<ParallaxBackground>();

            float halfHeight = camera.orthographicSize;
            // Screen.* is meaningless in batch mode and only ever describes the machine that ran
            // the build, so coverage is sized for the widest aspect the game could be shown at
            // rather than measured. Portrait phones are ~0.5; a laptop Game view can be past 2.
            float halfWidth = halfHeight * WidestSupportedAspect;
            float coverWidth = halfWidth * 2f;

            Sprite sky = AssetDatabase.LoadAssetAtPath<Sprite>(SkySpritePath);
            if (sky != null)
            {
                GameObject skyGo = new GameObject("Sky");
                skyGo.transform.SetParent(root.transform, false);
                SpriteRenderer sr = skyGo.AddComponent<SpriteRenderer>();
                sr.sprite = sky;
                sr.sortingOrder = -100;
                Vector2 size = sky.bounds.size;
                // Centred on the CAMERA, not on the origin: the camera sits above the middle of
                // the playfield to make room for the tuner strip, and a sky hung at y = 0 left a
                // band of the camera's clear colour along the top of the screen.
                skyGo.transform.localPosition = new Vector3(0f, camera.transform.position.y, 0f);
                skyGo.transform.localScale = new Vector3(
                    coverWidth / Mathf.Max(0.001f, size.x),
                    (halfHeight * 2.1f) / Mathf.Max(0.001f, size.y), 1f);
            }

            var layers = new Transform[ParallaxLayers.Length];
            var factors = new float[ParallaxLayers.Length];
            var widths = new float[ParallaxLayers.Length];
            var halfSpans = new float[ParallaxLayers.Length];

            for (int i = 0; i < ParallaxLayers.Length; i++)
            {
                (string path, float centerY, float scale, float factor, int order) = ParallaxLayers[i];
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogWarning($"[SceneBuilder] missing parallax sprite {path}");
                    continue;
                }

                float tileWidth = sprite.bounds.size.x;
                GameObject layerGo = new GameObject("Layer" + i);
                layerGo.transform.SetParent(root.transform, false);
                layerGo.transform.localPosition = new Vector3(0f, centerY, 0f);
                layerGo.transform.localScale = new Vector3(scale, scale, 1f);

                // An odd count laid out from -k to +k keeps the row centred on the camera; an
                // even one is half a tile off to one side, which is half the coverage wasted on
                // the wrong side. The extra tile of reach absorbs the scroll, which only ever
                // moves left and can be a full tile out just before it wraps.
                float step = tileWidth * scale;
                int k = Mathf.CeilToInt((halfWidth + step) / step);
                int copies = 2 * k + 1;
                for (int c = 0; c < copies; c++)
                {
                    GameObject tile = new GameObject("Tile" + c);
                    tile.transform.SetParent(layerGo.transform, false);
                    tile.transform.localPosition = new Vector3((c - k) * tileWidth, 0f, 0f);
                    SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = order;
                }

                layers[i] = layerGo.transform;
                factors[i] = factor;
                widths[i] = tileWidth * scale;
                halfSpans[i] = (k + 0.5f) * step;
            }

            SerializedObject so = new SerializedObject(parallax);
            SetRefArray(so, "_layers", layers);
            SetFloatArray(so, "_layerSpeedFactors", factors);
            SetFloatArray(so, "_layerTileWidths", widths);
            SetFloatArray(so, "_layerHalfSpans", halfSpans);
            so.ApplyModifiedPropertiesWithoutUndo();
            return parallax;
        }

        // Sits inside Background so it travels with the scenery, but it does not scroll: it is
        // pinned to the view, and ParallaxBackground only moves the Layer* children.
        private static WebCam BuildWebCamBackground(Transform backgroundRoot, Camera camera)
        {
            Material feed = EnsureWebCamMaterial();
            if (feed == null)
            {
                return null;
            }

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "WebCamBackground";
            // CreatePrimitive ships a collider; scenery the bird could hit is not scenery.
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<MeshCollider>());
            quad.transform.SetParent(backgroundRoot, false);

            // Centred on the CAMERA, not the origin, for the reason Sky is: the camera sits above
            // the middle of the playfield to clear the tuner strip.
            quad.transform.localPosition = new Vector3(0f, camera.transform.position.y, 0f);
            // Screen.* is meaningless in batch mode, so this is the authored portrait aspect only;
            // WebCam.FitQuadToView replaces it with the real one on the first frame it draws.
            float viewHeight = camera.orthographicSize * 2f;
            quad.transform.localScale = new Vector3(
                viewHeight * (ReferenceResolution.x / ReferenceResolution.y), viewHeight, 1f);

            MeshRenderer quadRenderer = quad.GetComponent<MeshRenderer>();
            // sharedMaterial, never material: the latter instantiates, and an instance created at
            // edit time is written into the scene as a second copy nothing can find again.
            quadRenderer.sharedMaterial = feed;
            quadRenderer.sortingOrder = WebCamSortingOrder;
            quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
            // Off until a frame arrives. A permission the player refuses then leaves the painted
            // sky untouched instead of a white slab across it.
            quadRenderer.enabled = false;

            WebCam webCam = quad.AddComponent<WebCam>();
            SerializedObject so = new SerializedObject(webCam);
            SetRef(so, "feedMaterial", feed);
            so.ApplyModifiedPropertiesWithoutUndo();
            return webCam;
        }

        private static Material EnsureWebCamMaterial()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
            {
                Debug.LogWarning("[SceneBuilder] URP/Unlit missing; no camera background built.");
                return null;
            }

            Material feed = AssetDatabase.LoadAssetAtPath<Material>(WebCamMaterialPath);
            bool created = feed == null;
            if (created)
            {
                feed = new Material(unlit);
            }
            else
            {
                feed.shader = unlit;
            }

            feed.name = "WebCamFeed";
            feed.SetColor("_BaseColor", Color.white);

            // A MeshRenderer only joins the sprites' sorting list once its material is in the
            // transparent queue. Left opaque it draws in the opaque pass, ahead of every sprite
            // in the scene, and sortingOrder is ignored entirely - which looks like the sorting
            // order was wrong rather than the surface type.
            feed.SetFloat("_Surface", 1f);
            feed.SetFloat("_Blend", 0f);
            feed.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            feed.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            feed.SetFloat("_ZWrite", 0f);
            feed.SetOverrideTag("RenderType", "Transparent");
            feed.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            feed.renderQueue = (int)RenderQueue.Transparent;

            if (created)
            {
                AssetDatabase.CreateAsset(feed, WebCamMaterialPath);
            }
            EditorUtility.SetDirty(feed);
            return feed;
        }

        private static void SetFloatArray(SerializedObject so, string field, float[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[SceneBuilder] no float array field '{field}' on {so.targetObject}");
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).floatValue = values[i];
            }
        }

        private static Sprite LoadSpriteOr(string path, System.Func<Sprite> fallback)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite != null ? sprite : fallback();
        }

        // Pipe.Setup sizes each section purely through localScale, so the section must stay a
        // 1x1 unit: any pre-sizing here gets multiplied by that scale into a screen-filling slab.
        //
        // The section itself is the collider, which has to line the gap right up to its edge, and
        // the tube sprite is a child so Pipe.Setup can stop it short of that edge and leave the
        // bell's flare to be what the gap is lined with.
        private static GameObject BuildPipeSection(string name, Transform parent, Sprite sprite, float y)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = Vector3.one;

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;

            GameObject tube = new GameObject(TubeChildName);
            tube.transform.SetParent(go.transform, false);
            tube.transform.localScale = Vector3.one;

            SpriteRenderer renderer = tube.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = Color.white;
            renderer.sortingOrder = 5;
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
            ApplyNumberFace(scoreLabel);
            // Below the tuner strip, which owns the top of the screen - measured from the bottom
            // of the strip rather than authored, so retuning the strip's height or margin carries
            // the score with it instead of quietly closing the gap.
            Place(scoreLabel.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -ScoreLabelTopFromTop), new Vector2(700f, 220f));

            GameObject singGroupGo = NewUI("StartScreen", root.transform);
            Stretch(singGroupGo);
            CanvasGroup singGroup = singGroupGo.AddComponent<CanvasGroup>();
            singGroup.interactable = false;
            singGroup.blocksRaycasts = false;

            GameObject singPlate = NewUI("StartSign", singGroupGo.transform);
            Place(singPlate, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, SignSize);

            Image singBackdrop = NewImage("Parchment", singPlate.transform, new Color(0.93f, 0.88f, 0.74f, 1f));
            Stretch(singBackdrop.gameObject);
            ApplySlicedSprite(singBackdrop, SignSpritePath, Color.white);

            TextMeshProUGUI singLabel = NewText("SingToStartLabel", singPlate.transform, "SING TO PLAY", 70f,
                TextAlignmentOptions.Center);
            // 660 wide, not the sign's 780: the deckled border eats ~7% of each side and a title
            // sized to the full rect runs out over the torn edge.
            Place(singLabel.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f),
                new Vector2(700f, 125f));
            ApplyTitleFace(singLabel);
            singLabel.characterSpacing = 2f;
            singLabel.color = InkColor;

            BuildTunerLegend(singPlate.transform, new Vector2(0f, -580f), new Vector2(640f, 204f));

            TextMeshProUGUI singHint = NewText("Hint", singPlate.transform,
                "Hit the right note\nto keep flying!", 62f, TextAlignmentOptions.Center);
            Place(singHint.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -845f),
                new Vector2(700f, 200f));
            singHint.color = MutedInkColor;

            Image singBird = NewImage("Bird", singPlate.transform, Color.white);
            ApplySlicedSprite(singBird, BirdSingSpritePath, Color.white);
            Place(singBird.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-34f, -1075f),
                new Vector2(250f, 205f));

            SerializedObject so = new SerializedObject(hud);
            SetRef(so, "scoreGroup", scoreGroup);
            SetRef(so, "scoreLabel", scoreLabel);
            SetRef(so, "singToStartGroup", singGroup);
            SetRef(so, "singToStartHint", singHint);
            SetRef(so, "singToStartPulseTarget", (RectTransform)singPlate.transform);
            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        // The count comes from GameConfig, not from a constant here: a retune of PlayerLives must
        // not be able to leave a row of hearts that disagrees with what the run actually grants.
        private static LivesUI BuildLivesUI(Transform parent, GameConfig config)
        {
            GameObject root = NewUI("LivesHud", parent);
            Stretch(root);
            LivesUI lives = root.AddComponent<LivesUI>();
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            int count = config != null ? Mathf.Max(1, config.PlayerLives) : 3;
            float rowWidth = (count * HeartSize) + ((count - 1) * HeartSpacing);

            GameObject row = NewUI("Hearts", root.transform);
            Place(row, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-HeartMargin, HeartMargin),
                new Vector2(rowWidth, HeartSize));

            Sprite full = AssetDatabase.LoadAssetAtPath<Sprite>(HeartFullSpritePath);
            Sprite empty = AssetDatabase.LoadAssetAtPath<Sprite>(HeartEmptySpritePath);
            if (full == null || empty == null)
            {
                Debug.LogWarning("[SceneBuilder] missing heart sprites - run python3 Tools/key-ui-art.py");
            }

            Image[] hearts = new Image[count];
            for (int i = 0; i < count; i++)
            {
                Image heart = NewImage("Heart" + i, row.transform, Color.white);
                // Stepped from the row's left edge, so the row fills leftwards from the corner
                // margin however many lives the config grants.
                Place(heart.gameObject, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(i * (HeartSize + HeartSpacing), 0f), new Vector2(HeartSize, HeartSize));
                heart.sprite = full;
                hearts[i] = heart;
            }

            SerializedObject so = new SerializedObject(lives);
            SetRef(so, "group", group);
            SetRefArray(so, "hearts", hearts);
            SetRef(so, "fullSprite", full);
            SetRef(so, "emptySprite", empty);
            so.ApplyModifiedPropertiesWithoutUndo();
            return lives;
        }

        // One source for both one-shots; PlayOneShot lets them overlap each other. Clips are
        // looked up by path rather than passed in, so adding a real file over a placeholder needs
        // no change here.
        private static GameAudio BuildGameAudio(Transform parent)
        {
            GameObject root = new GameObject("GameAudio");
            root.transform.SetParent(parent, false);
            GameAudio audio = root.AddComponent<GameAudio>();

            AudioSource sfx = NewAudioSource("Sfx", root.transform);

            SerializedObject so = new SerializedObject(audio);
            SetRef(so, "sfxSource", sfx);
            SetRef(so, "scoreSfx", LoadClip("sfx_score.wav"));
            SetRef(so, "crashSfx", LoadClip("sfx_crash.wav"));
            so.ApplyModifiedPropertiesWithoutUndo();
            return audio;
        }

        private static AudioSource NewAudioSource(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            // 2D: nothing in this game is positional, and a 3D source at the origin would pan with
            // the camera as it does not move.
            source.spatialBlend = 0f;
            return source;
        }

        private static AudioClip LoadClip(string fileName)
        {
            string path = $"{AudioFolder}/{fileName}";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning($"[SceneBuilder] no audio clip at {path}; run Tools/make-placeholder-audio.py");
            }
            return clip;
        }

        // Pano-style chromatic tuner across the top: the note letters and the ten-cent ruler sit on
        // ONE sliding dial and the needle is nailed to the middle of the bar. The reading is then
        // unambiguous - the letter by the needle is the note, its distance from the needle is the
        // error - and a frame costs one transform move instead of a relayout of every tick.
        private static TunerBarUI BuildTunerBar(Transform canvas)
        {
            // Everything inside the pill is measured against its face, not against its rect.
            const float faceHeight = TunerFaceHeightPx;
            // Ticks own the top of the strip and the letters sit under them; the rest is the pad
            // that keeps the pair optically centred in the pill rather than resting on its floor.
            const float bottomPad = TunerDialBottomPadPx;
            const float topMargin = TunerTopMarginPx;
            const float dialSemitones = 9f;
            float px = TunerBarUI.PixelsPerSemitone;

            GameObject root = NewUI("TunerBar", canvas);
            TunerBarUI tuner = root.AddComponent<TunerBarUI>();
            CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;

            // A pill at the top-centre, NOT stretched across the width. The dial behind it is
            // still nine semitones wide and still clipped by the viewport, so the strip shows
            // about two and a half notes at a time - which is all a tuner needs to be read.
            Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -topMargin),
                new Vector2(TunerBarWidthPx, TunerBarHeightPx));

            Image backdrop = NewImage("Backdrop", root.transform, new Color(0.04f, 0.05f, 0.09f, 0.72f));
            Stretch(backdrop.gameObject);
            ApplySlicedSprite(backdrop, TunerPillSpritePath, Color.white);

            // The dial is wider than the screen at portrait aspect, so it has to be clipped rather
            // than left to spill its outer letters over the rest of the HUD. Inset to the pill's
            // face rather than to its rect: clipped to the rect the ruler's outer ticks come out
            // past the rounded ends and the safe band rides up over the rim.
            GameObject viewport = NewUI("Viewport", root.transform);
            Stretch(viewport, TunerFaceInsetXPx, TunerFaceInsetYPx);
            viewport.AddComponent<RectMask2D>();

            // Inside the viewport so it is clipped like the dial, but NOT a child of the dial: its
            // width and position are the gap's business, not the current note's. Sized at runtime,
            // so whatever is authored here is only what shows in the editor.
            Image safeBand = NewImage("SafeBand", viewport.transform, new Color(0.36f, 0.85f, 0.51f, 0.22f));
            // Sprite only. The band's WIDTH is the safe-pitch window and TunerBarUI sets it every
            // frame from the gap and the bird's radius - authoring a size here would be a lie.
            ApplySlicedSprite(safeBand, SafeBandSpritePath, new Color(1f, 1f, 1f, 0.3f));
            Place(safeBand.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(px * 2f, faceHeight - bottomPad));

            GameObject dialGo = NewUI("Dial", viewport.transform);
            Place(dialGo, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(px * dialSemitones, faceHeight));
            CanvasGroup dialGroup = dialGo.AddComponent<CanvasGroup>();
            dialGroup.interactable = false;
            dialGroup.blocksRaycasts = false;

            BuildTunerTicks(dialGo.transform, px, dialSemitones);

            TMP_Text[] labels = new TMP_Text[TunerBarUI.NoteSlotCount];
            int center = TunerBarUI.NoteSlotCount / 2;
            for (int i = 0; i < labels.Length; i++)
            {
                TextMeshProUGUI label = NewText("Note" + i, dialGo.transform, "A", 68f,
                    TextAlignmentOptions.Center);
                Place(label.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2((i - center) * px, bottomPad + 4f), new Vector2(px * 0.92f, 88f));
                label.fontStyle = FontStyles.Bold;
                labels[i] = label;
            }

            // Wider than the line it draws: needle.png is a 6 px core inside a soft glow, and at
            // the old 6 px rect the glow would be a single pixel. TunerBarUI tints this every
            // frame, which is why the sprite is white.
            //
            // Inside the viewport, and last, so it is measured against the same face the ticks are
            // and still draws over both them and the band.
            Image needle = NewImage("Needle", viewport.transform, Color.white);
            ApplySlicedSprite(needle, NeedleSpritePath, Color.white);
            Place(needle.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f),
                new Vector2(26f, faceHeight - bottomPad - 12f));

            SerializedObject so = new SerializedObject(tuner);
            SetRef(so, "rootGroup", rootGroup);
            SetRef(so, "dial", (RectTransform)dialGo.transform);
            SetRef(so, "dialGroup", dialGroup);
            SetRef(so, "safeBand", safeBand.rectTransform);
            SetRef(so, "needle", needle);
            SetRefArray(so, "noteLabels", labels);
            so.ApplyModifiedPropertiesWithoutUndo();
            return tuner;
        }

        // 9-slice so the middle stretches and the ends keep their shape. Falls back to the flat
        // colour the panel was authored with when the art is not present.
        private static void ApplySlicedSprite(Image image, string path, Color tint)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[SceneBuilder] missing UI sprite {path}, keeping the flat fill");
                return;
            }
            image.sprite = sprite;
            image.type = sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            image.color = tint;
        }

        // A still life of the tuner strip for the start sign - not the tuner itself, which stays
        // hidden until a run starts because before the anchor exists it has nothing true to say.
        // Built from the same three sprites so the legend cannot drift from what it explains.
        private static void BuildTunerLegend(Transform parent, Vector2 position, Vector2 size)
        {
            GameObject legend = NewUI("TunerLegend", parent);
            Place(legend, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, size);

            Image pill = NewImage("Pill", legend.transform, new Color(0.10f, 0.12f, 0.18f, 0.94f));
            Stretch(pill.gameObject);
            ApplySlicedSprite(pill, TunerPillSpritePath, Color.white);

            Image band = NewImage("SafeBand", legend.transform, new Color(0.36f, 0.85f, 0.51f, 0.35f));
            ApplySlicedSprite(band, SafeBandSpritePath, new Color(0.42f, 0.92f, 0.55f, 0.32f));
            Place(band.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size.x * 0.34f, size.y * 0.78f));

            // One semitone in legend pixels. The ruler below is stepped off the SAME figure, so
            // the tall ticks land under the letters instead of drifting a few pixels wide of them -
            // a legend whose ticks disagree with its letters teaches the dial wrong.
            float semitoneWidth = size.x * 0.30f;

            // Any three adjacent semitones would do; these match the note letters in the mock-up.
            string[] letters = { "F#", "G#", "A#" };
            for (int i = 0; i < letters.Length; i++)
            {
                TextMeshProUGUI letter = NewText("Note" + i, legend.transform, letters[i], 50f,
                    TextAlignmentOptions.Center);
                Place(letter.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((i - 1) * semitoneWidth, size.y * 0.14f), new Vector2(150f, 70f));
                letter.fontStyle = FontStyles.Bold;
                letter.color = new Color(1f, 1f, 1f, i == 1 ? 1f : 0.7f);
            }

            const int legendTicksPerSemitone = 5;
            for (int i = -6; i <= 6; i++)
            {
                bool onNote = i % legendTicksPerSemitone == 0;
                Image tick = NewImage("Tick" + i, legend.transform, new Color(1f, 1f, 1f, onNote ? 0.85f : 0.4f));
                Place(tick.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(i * semitoneWidth / legendTicksPerSemitone, 18f),
                    new Vector2(onNote ? 4f : 3f, onNote ? 26f : 16f));
            }

            Image needle = NewImage("Needle", legend.transform, Color.white);
            ApplySlicedSprite(needle, NeedleSpritePath, Color.white);
            Place(needle.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(24f, size.y * 0.66f));
        }

        // A tick every ten cents, taller on the semitone boundaries and tallest under each letter.
        // That is the granularity a chromatic tuner app shows, and it is what makes a few cents of
        // drift readable instead of merely present.
        private static void BuildTunerTicks(Transform dial, float px, float dialSemitones)
        {
            int halfTicks = Mathf.RoundToInt(dialSemitones * 5f);
            for (int i = -halfTicks; i <= halfTicks; i++)
            {
                int cents = i * 10;
                int fromNote = ((cents % 100) + 100) % 100;
                bool onNote = fromNote == 0;
                bool onBoundary = fromNote == 50;

                float height = onNote ? 34f : onBoundary ? 26f : 14f;
                float alpha = onNote ? 0.95f : onBoundary ? 0.7f : 0.4f;

                Image tick = NewImage("Tick" + i, dial, new Color(1f, 1f, 1f, alpha));
                Place(tick.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(cents / 100f * px, -8f), new Vector2(onNote ? 5f : 3f, height));
            }
        }

        // The two permission asks, microphone first, on ONE parchment whose words change with the
        // step - a second sign would be a second thing to keep in sync and a visible swap as it
        // replaced the first. Nothing here calls for a permission: the component raises an event
        // per answer and leaves the asking to whoever subscribes. The panel exists because
        // getUserMedia needs a user gesture outside desktop Chrome and this game reads no input,
        // so without a button there is no tap to spend.
        //
        // Laid out from the Figma consent panels. Vertical positions are measured from the sign's
        // top edge and all sit inside SmallSignFaceTop..SmallSignFaceBottom; outside that the
        // deckled edge is tapering in and content hangs off the parchment.
        private static ConsentFlowUI BuildConsentFlow(Transform canvas)
        {
            GameObject root = NewUI("ConsentFlow", canvas);
            Stretch(root);
            ConsentFlowUI consentFlow = root.AddComponent<ConsentFlowUI>();

            GameObject panels = NewUI("Panels", root.transform);
            Stretch(panels);

            Image dim = NewImage("Dim", panels.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(dim.gameObject);
            // The one blocking graphic in the scene. The start sign is pulsing underneath, so a
            // tap that misses a button must be swallowed rather than land on the game.
            dim.raycastTarget = true;

            GameObject panel = NewUI("Panel", panels.transform);
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, SmallSignSize);

            Image parchment = NewImage("Parchment", panel.transform, new Color(0.93f, 0.88f, 0.74f, 1f));
            Stretch(parchment.gameObject);
            ApplySlicedSprite(parchment, SmallSignSpritePath, Color.white);

            // 620 wide on an 820 sign: the leaf cluster in the top-right corner reaches about a
            // fifth of the way in, and a title sized to the face would run under it. Regular
            // weight and no tracking, which is how the design draws it - so no ApplyTitleFace.
            TextMeshProUGUI title = NewText("Title", panel.transform, "Enable Mic",
                56f * ConsentDesignScale, TextAlignmentOptions.Center);
            Place(title.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -ConsentTitleCenterFromTop), new Vector2(620f, 110f));
            title.color = InkColor;

            TextMeshProUGUI body = NewText("Body", panel.transform, "this is a sound based game!",
                32f * ConsentDesignScale, TextAlignmentOptions.Top);
            Place(body.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -ConsentBodyTopFromTop),
                new Vector2(357f * ConsentDesignScale, 210f));
            body.color = InkColor;

            // The row is what lets one panel serve both steps: it positions whichever buttons are
            // active, so the microphone step's lone button centres itself without a second set of
            // authored coordinates. Sized for two because two is the most that are ever up at
            // once. The design abuts the pair - the visible gap between them is the transparent
            // margin inside each sprite, which 9-slicing preserves.
            GameObject buttonRow = NewUI("ButtonRow", panel.transform);
            Place(buttonRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -ConsentButtonRowCenterFromTop),
                new Vector2(ConsentButtonSize.x * 2f, ConsentButtonSize.y));
            HorizontalLayoutGroup row = buttonRow.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 0f;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            // One button per answer, each with a fixed label and plaque, in the order the row lays
            // them out: the microphone step's lone button, then the camera step's pair, refuse on
            // the left and accept on the right as the design has them.
            //
            // The design's plaque choice tracks whether there is a competing option, not whether
            // the answer is yes: timber for "OK" and "NO", brass only for the "YES!" that has a
            // "NO" beside it to outweigh.
            Button microphoneAccept = NewButton("MicrophoneAcceptButton", buttonRow.transform, "OK",
                Vector2.zero, ConsentButtonSize, ButtonSpritePath, 32f * ConsentDesignScale);

            Button cameraDecline = NewButton("CameraDeclineButton", buttonRow.transform, "NO",
                Vector2.zero, ConsentButtonSize, ButtonSpritePath, 32f * ConsentDesignScale);

            Button cameraAccept = NewButton("CameraAcceptButton", buttonRow.transform, "YES!",
                Vector2.zero, ConsentButtonSize, ButtonPrimarySpritePath, 32f * ConsentDesignScale);
            // Ink, not gold: the brass plaque is light and a gold label on it would vanish.
            cameraAccept.GetComponentInChildren<TextMeshProUGUI>().color = InkColor;

            // Authored on the microphone step, which is also what ConsentFlowUI opens on. Set here
            // rather than left to Awake so the scene view shows the truth.
            cameraDecline.gameObject.SetActive(false);
            cameraAccept.gameObject.SetActive(false);

            // A headless build never ticks a canvas, so the row has to be rebuilt by hand or the
            // saved scene keeps the authored transforms and both buttons sit at the row's centre.
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)buttonRow.transform);

            SerializedObject so = new SerializedObject(consentFlow);
            SetRef(so, "root", panels);
            SetRef(so, "title", title);
            SetRef(so, "body", body);
            SetRef(so, "microphoneAcceptButton", microphoneAccept);
            SetRef(so, "cameraDeclineButton", cameraDecline);
            SetRef(so, "cameraAcceptButton", cameraAccept);
            so.ApplyModifiedPropertiesWithoutUndo();
            return consentFlow;
        }

        // The microphone notice: same parchment, same rhythm and the same plaques as the consent
        // panels, because it is the reply to the ask they made. Its own object rather than a third
        // consent step - it has to be able to come back after that flow has finished.
        private static MicrophoneNoticeUI BuildMicrophoneNotice(Transform canvas)
        {
            GameObject root = NewUI("MicrophoneNotice", canvas);
            Stretch(root);
            MicrophoneNoticeUI notice = root.AddComponent<MicrophoneNoticeUI>();

            GameObject panels = NewUI("Panels", root.transform);
            Stretch(panels);

            Image dim = NewImage("Dim", panels.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(dim.gameObject);
            // Blocks like the consent dim does, and for the same reason: the notice is up while
            // the game is still in attract, and a tap that misses OK must not reach the playfield.
            // The jslib bridge listens on the window in the capture phase, so the tap it is
            // waiting for still gets through.
            dim.raycastTarget = true;

            GameObject panel = NewUI("Panel", panels.transform);
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, SmallSignSize);

            Image parchment = NewImage("Parchment", panel.transform, new Color(0.93f, 0.88f, 0.74f, 1f));
            Stretch(parchment.gameObject);
            ApplySlicedSprite(parchment, SmallSignSpritePath, Color.white);

            // One block of text, no title: a heading would only say the ask a second time. The
            // reason is part of the message rather than a line under it, because a player who is
            // being asked to leave the game and change a browser setting deserves to be told why
            // in the same breath. Sized to wrap inside the 620 px the corner flowers leave free,
            // and centred in a tall box so two lines or three sit on the same optical centre.
            TextMeshProUGUI message = NewText("Message", panel.transform, MicNoticeMessage,
                MicNoticeMessageFontSize, TextAlignmentOptions.Center);
            Place(message.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -MicNoticeMessageCenterFromTop), MicNoticeMessageSize);
            message.color = InkColor;

            // The consent flow's row, for the same reason it has one: two plaques abutting at the
            // panel's centre, positioned by layout rather than by a pair of authored offsets, so
            // the pair here sits exactly where the camera step's pair does.
            GameObject buttonRow = NewUI("ButtonRow", panel.transform);
            Place(buttonRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -ConsentButtonRowCenterFromTop),
                new Vector2(ConsentButtonSize.x * 2f, ConsentButtonSize.y));
            HorizontalLayoutGroup row = buttonRow.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 0f;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            // Leaving on the left and asking again on the right, as the camera step has its "NO"
            // and "YES!". Timber for the exit and brass for the "OK": the plaque tracks whether
            // there is a competing option to outweigh, and now that there are two answers there is.
            Button exit = NewButton("ExitButton", buttonRow.transform, "EXIT",
                Vector2.zero, ConsentButtonSize, ButtonSpritePath, 32f * ConsentDesignScale);

            // Still "OK", not "TRY AGAIN": the panel is a piece of news before it is a question,
            // and the tap that acknowledges it is the same tap the browser needs to be asked on.
            Button retry = NewButton("RetryButton", buttonRow.transform, "OK",
                Vector2.zero, ConsentButtonSize, ButtonPrimarySpritePath, 32f * ConsentDesignScale);
            // Ink, not gold: the brass plaque is light and a gold label on it would vanish.
            retry.GetComponentInChildren<TextMeshProUGUI>().color = InkColor;

            // A headless build never ticks a canvas, so the row has to be rebuilt by hand or the
            // saved scene keeps the authored transforms and both buttons sit at the row's centre.
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)buttonRow.transform);

            panels.SetActive(false);

            SerializedObject so = new SerializedObject(notice);
            SetRef(so, "root", panels);
            SetRef(so, "exitButton", exit);
            SetRef(so, "retryButton", retry);
            so.ApplyModifiedPropertiesWithoutUndo();
            return notice;
        }

        // Anchored to the canvas corner, not to any panel: the three panels it accompanies are
        // three sizes in three places, and a button that moved with them would read as a different
        // control each time.
        private static QuitButtonUI BuildQuitButton(Transform canvas)
        {
            GameObject root = NewUI("QuitButton", canvas);
            Stretch(root);
            QuitButtonUI quit = root.AddComponent<QuitButtonUI>();

            // A child rather than the component's own object: QuitButtonUI toggles this to hide,
            // and a component that switched itself off could never switch itself back on.
            Image background = NewImage("Button", root.transform, Color.white);
            ApplySlicedSprite(background, XButtonSpritePath, Color.white);
            Place(background.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-QuitButtonMarginPx, -QuitButtonMarginPx),
                new Vector2(QuitButtonSizePx, QuitButtonSizePx));
            background.raycastTarget = true;
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            SerializedObject so = new SerializedObject(quit);
            SetRef(so, "root", background.gameObject);
            SetRef(so, "quitButton", button);
            so.ApplyModifiedPropertiesWithoutUndo();
            return quit;
        }

        private static EndScreenUI BuildEndScreen(Transform canvas, Camera camera)
        {
            GameObject root = NewUI("EndScreen", canvas);
            Stretch(root);
            EndScreenUI endScreen = root.AddComponent<EndScreenUI>();

            GameObject panel = NewUI("Panel", root.transform);
            Stretch(panel);

            Image dim = NewImage("Dim", panel.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(dim.gameObject);

            // ShareService captures whatever is on the UI layer inside this rect, not just its
            // children, so the buttons have to live BELOW the sign rather than on it. That is also
            // why the end screen's sign is the shorter of the two.
            GameObject card = NewUI("EndSign", panel.transform);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, SignSize);

            Image cardBg = NewImage("CardBackground", card.transform, new Color(0.93f, 0.88f, 0.74f, 1f));
            Stretch(cardBg.gameObject);
            ApplySlicedSprite(cardBg, SignSpritePath, Color.white);

            TextMeshProUGUI title = NewText("Title", card.transform, "GAME OVER", 74f, TextAlignmentOptions.Center);
            Place(title.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f),
                new Vector2(700f, 125f));
            ApplyTitleFace(title);
            title.characterSpacing = 2f;
            title.color = InkColor;

            TextMeshProUGUI caption = NewText("ScoreCaption", card.transform, "SCORE", 46f,
                TextAlignmentOptions.Center);
            Place(caption.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -490f),
                new Vector2(620f, 56f));
            caption.characterSpacing = 6f;
            caption.color = MutedInkColor;

            TextMeshProUGUI finalScore = NewText("FinalScore", card.transform, "0", 145f, TextAlignmentOptions.Center);
            ApplyNumberFace(finalScore);
            Place(finalScore.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -534f),
                new Vector2(620f, 165f));
            finalScore.fontStyle = FontStyles.Bold;
            finalScore.color = InkColor;

            // Two labels in a row rather than one "BEST 42" string: the numeral is set in the
            // number face and the word is not, so they cannot be the same TMP_Text. Laid out by
            // the group so the pair stays optically centred as the number grows a digit.
            GameObject bestRow = NewUI("BestScore", card.transform);
            Place(bestRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -706f),
                new Vector2(620f, 62f));
            HorizontalLayoutGroup bestLayout = bestRow.AddComponent<HorizontalLayoutGroup>();
            bestLayout.childAlignment = TextAnchor.MiddleCenter;
            bestLayout.spacing = BestScoreRowSpacingPx;
            bestLayout.childControlWidth = true;
            bestLayout.childControlHeight = true;
            bestLayout.childForceExpandWidth = false;
            bestLayout.childForceExpandHeight = false;

            // Baseline alignment, not centre: the two faces have different vertical metrics, and
            // centring each in its own box would sit the digits a few pixels off the word's feet.
            TextMeshProUGUI bestCaption = NewText("Caption", bestRow.transform, "BEST", 50f,
                TextAlignmentOptions.Baseline);
            bestCaption.characterSpacing = 4f;
            bestCaption.color = MutedInkColor;

            TextMeshProUGUI bestScore = NewText("Value", bestRow.transform, "0", 50f,
                TextAlignmentOptions.Baseline);
            ApplyNumberFace(bestScore);
            bestScore.color = MutedInkColor;

            // A headless build never ticks a canvas, so the row has to be rebuilt by hand or the
            // saved scene keeps both labels stacked at its centre.
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)bestRow.transform);

            // Sits ON the best-score line, and EndScreenUI hides that line while it shows. A row
            // of its own would cost ~60 px of parchment face, and the face runs out before the
            // Share button does - and on a new best "BEST 42" only repeats the 42 above it.
            Image badge = NewImage("NewBestBadge", card.transform, new Color(1f, 0.78f, 0.22f, 1f));
            Place(badge.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -704f),
                new Vector2(330f, 66f));
            TextMeshProUGUI badgeLabel = NewText("Label", badge.transform, "NEW BEST!", 40f,
                TextAlignmentOptions.Center);
            Stretch(badgeLabel.gameObject);
            badgeLabel.color = new Color(0.12f, 0.10f, 0.05f, 1f);
            badge.gameObject.SetActive(false);

            Image deadBird = NewImage("Bird", card.transform, Color.white);
            ApplySlicedSprite(deadBird, BirdDeadSpritePath, Color.white);
            Place(deadBird.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -778f),
                new Vector2(195f, 157f));

            // Centre-relative, because NewButton anchors to the middle. Share is the one that runs
            // out of parchment, so both are measured up from SignFaceBottom rather than down from
            // the score: its lower edge lands 45 px inside the face.
            const float buttonHeight = 135f;
            float shareCenter = SignFaceBottom - 45f - (buttonHeight * 0.5f);
            Button playAgain = NewButton("PlayAgainButton", card.transform, "Play Again",
                FromSignTop(shareCenter - buttonHeight - 18f));
            Button share = NewButton("ShareButton", card.transform, "Share",
                FromSignTop(shareCenter));

            // ShareService points a camera at this rect and renders every UI-layer graphic inside
            // it, children or not. So the shared card is the sign's upper two thirds - everything
            // down to the bird, and nothing of the two buttons below.
            GameObject captureRect = NewUI("ScoreCard", card.transform);
            Place(captureRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(SignSize.x, 920f));

            panel.SetActive(false);

            SerializedObject so = new SerializedObject(endScreen);
            SetRef(so, "panel", panel);
            SetRef(so, "scoreCardRoot", (RectTransform)captureRect.transform);
            SetRef(so, "uiCamera", camera);
            SetRef(so, "finalScoreLabel", finalScore);
            SetRef(so, "bestScoreRow", bestRow);
            SetRef(so, "bestScoreLabel", bestScore);
            SetRef(so, "newBestBadge", badge.gameObject);
            SetRef(so, "playAgainButton", playAgain);
            SetRef(so, "shareButton", share);
            so.ApplyModifiedPropertiesWithoutUndo();
            return endScreen;
        }

        // Everything else on a sign is placed from its top edge; NewButton anchors to the middle,
        // so this is the one conversion between the two.
        private static Vector2 FromSignTop(float centerFromTop)
        {
            return new Vector2(0f, (SignSize.y * 0.5f) - centerFromTop);
        }

        // The same conversion for the squat consent parchment, whose heights are measured from its
        // own top edge.
        private static Vector2 FromSmallSignTop(float centerFromTop)
        {
            return new Vector2(0f, (SmallSignSize.y * 0.5f) - centerFromTop);
        }

        // 9-sliced timber plaque. The sprite's border keeps the bevel and the rounded ends intact
        // however wide the button is authored, so the two here can share one asset.
        private static Button NewButton(string name, Transform parent, string label, Vector2 position)
        {
            return NewButton(name, parent, label, position, new Vector2(620f, 135f), ButtonSpritePath, 56f);
        }

        private static Button NewButton(string name, Transform parent, string label, Vector2 position,
            Vector2 size, string spritePath, float fontSize)
        {
            Image background = NewImage(name, parent, new Color(0.55f, 0.38f, 0.22f, 1f));
            ApplySlicedSprite(background, spritePath, Color.white);
            Place(background.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            background.raycastTarget = true;
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            TextMeshProUGUI text = NewText("Label", background.transform, label, fontSize,
                TextAlignmentOptions.Center);
            Stretch(text.gameObject);
            text.fontStyle = FontStyles.Bold;
            text.color = ButtonLabelColor;
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
            Stretch(go, 0f, 0f);
        }

        /// Fills the parent, held off its edges by the given padding. Used where a sprite's drawn
        /// face is smaller than the rect it is stretched over, so whatever sits on it has to be
        /// inset by the difference rather than by the rect.
        private static void Stretch(GameObject go, float insetX, float insetY)
        {
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(insetX, insetY);
            rect.offsetMax = new Vector2(-insetX, -insetY);
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

        // FontStyles.Bold is as far as TMP goes without a real bold weight in the font asset, and
        // the heavier face is a material property. A material INSTANCE cannot be used: TMP marks
        // the ones it creates HideAndDontSave, so it would vanish on the scene save and the titles
        // would silently go back to normal weight at runtime. Hence a real asset.
        private static Material EnsureTitleMaterial()
        {
            TMP_FontAsset font = ResolveFont();
            if (font == null || font.material == null)
            {
                return null;
            }

            Material source = font.material;
            Material title = AssetDatabase.LoadAssetAtPath<Material>(TitleMaterialPath);
            bool created = title == null;
            if (created)
            {
                title = new Material(source);
            }
            else
            {
                title.shader = source.shader;
                title.CopyPropertiesFromMaterial(source);
            }

            // Dilates the glyph face outward from the SDF edge - a genuinely heavier letterform
            // rather than an outline drawn around a thin one.
            title.SetFloat(ShaderUtilities.ID_FaceDilate, 0.22f);
            title.name = "SignTitle";

            if (created)
            {
                AssetDatabase.CreateAsset(title, TitleMaterialPath);
            }
            EditorUtility.SetDirty(title);
            return title;
        }

        private static void ApplyTitleFace(TMP_Text text)
        {
            text.fontStyle = FontStyles.Bold;
            Material face = EnsureTitleMaterial();
            if (face != null)
            {
                text.fontSharedMaterial = face;
            }
        }

        // The score numerals are Gulzar, not the sign face. Applied after NewText rather than
        // inside it: this is the exception, and every other string in the app is set in the face
        // the art is drawn in.
        private static void ApplyNumberFace(TMP_Text text)
        {
            TMP_FontAsset font = ResolveNumberFont();
            if (font != null)
            {
                text.font = font;
            }
        }

        // Digits only, and deliberately without a fallback: if the asset is missing this leaves
        // the label in the sign face rather than silently drawing a blank score.
        private static TMP_FontAsset ResolveNumberFont()
        {
            if (numberFontResolved)
            {
                return cachedNumberFont;
            }
            numberFontResolved = true;
            try
            {
                cachedNumberFont = FontBuilder.EnsureNumberFontAsset();
            }
            catch (Exception)
            {
                cachedNumberFont = null;
            }
            if (cachedNumberFont == null)
            {
                Debug.LogWarning("[SceneBuilder] No number font asset. Run Flappy Voice/Build Font Asset.");
            }
            return cachedNumberFont;
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
                // Every piece of text in the game comes through NewText, so this one lookup is
                // what puts the whole app in the face the signs are designed in. The TMP default
                // (LiberationSans) is only a backstop for a project that has not built the asset
                // yet - shipping on it would put the HUD in a different typeface to the art.
                cachedFont = FontBuilder.EnsureFontAsset() ?? TMP_Settings.defaultFontAsset;
            }
            catch (Exception)
            {
                cachedFont = null;
            }
            if (cachedFont == null)
            {
                Debug.LogWarning("[SceneBuilder] No TMP font asset. Run Flappy Voice/Build Font Asset.");
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

        private static void SetRefArray(SerializedObject so, string field, UnityEngine.Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null || !property.isArray)
            {
                Debug.LogWarning($"[SceneBuilder] missing serialized array field '{field}' on {so.targetObject.GetType().Name}");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void WireBootstrap(GameBootstrap bootstrap, GameConfig config,
            GameStateManager stateManager, ScoreManager scoreManager, ShareService shareService,
            MicrophoneInput microphoneInput, PitchTracker pitchTracker, GameAudio gameAudio,
            VoiceHeightSource voiceHeight,
            AttractPilot attractPilot, PipeSpawner pipeSpawner, ParallaxBackground background,
            WebCam webCamBackground, SingingFx singingFx, LivesManager livesManager,
            PlayerController player, Camera viewCamera, HudUI hud, LivesUI livesUI, TunerBarUI tunerBar,
            EndScreenUI endScreen, ConsentFlowUI consentFlow, MicrophoneNoticeUI microphoneNotice,
            QuitButtonUI quitButton)
        {
            SerializedObject so = new SerializedObject(bootstrap);
            SetRef(so, "config", AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath));
            SetRef(so, "stateManager", stateManager);
            SetRef(so, "scoreManager", scoreManager);
            SetRef(so, "pipeSpawner", pipeSpawner);
            SetRef(so, "parallaxBackground", background);
            SetRef(so, "webCamBackground", webCamBackground);
            SetRef(so, "singingFx", singingFx);
            SetRef(so, "livesManager", livesManager);
            SetRef(so, "attractPilot", attractPilot);
            SetRef(so, "voiceHeightSource", voiceHeight);
            SetRef(so, "player", player);
            SetRef(so, "viewCamera", viewCamera);
            SetRef(so, "pitchTracker", pitchTracker);
            SetRef(so, "microphoneInput", microphoneInput);
            SetRef(so, "gameAudio", gameAudio);
            SetRef(so, "shareService", shareService);
            SetRef(so, "hud", hud);
            SetRef(so, "livesUI", livesUI);
            SetRef(so, "tunerBar", tunerBar);
            SetRef(so, "endScreen", endScreen);
            SetRef(so, "consentFlow", consentFlow);
            SetRef(so, "microphoneNotice", microphoneNotice);
            SetRef(so, "quitButton", quitButton);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WirePipeSections(Pipe pipe, GameObject top, GameObject bottom, GameObject gap,
            GameObject topBell, GameObject bottomBell, GameObject topTube, GameObject bottomTube)
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
                bool isBell = name.Contains("bell");
                // The tube is a child of its section, so it has to be matched before the plain
                // top/bottom test that would otherwise hand back the section itself.
                bool isTube = name.Contains("tube");
                GameObject source =
                    isBell && (name.Contains("top") || name.Contains("upper")) ? topBell :
                    isBell ? bottomBell :
                    isTube && (name.Contains("top") || name.Contains("upper")) ? topTube :
                    isTube ? bottomTube :
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
