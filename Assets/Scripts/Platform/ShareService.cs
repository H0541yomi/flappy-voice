using System;
using System.IO;
using UnityEngine;

namespace FlappyVoice.Platform
{
    public sealed class ShareService : MonoBehaviour
    {
        [SerializeField] private int captureWidthPx = 1080;
        [SerializeField] private int maxCaptureHeightPx = 2048;
        [SerializeField] private Color cardBackground = new Color(0.07f, 0.08f, 0.12f, 1f);
        [SerializeField] private string fileName = "flappyvoice-score.png";

        private INativeShare native;
        private readonly Vector3[] corners = new Vector3[4];

        /// <summary>
        /// How the last share ended. The end screen listens so it can say "Link copied" on the
        /// browsers that have no share sheet, which is the only sign the player gets that a tap
        /// put anything anywhere.
        /// </summary>
        public event Action<ShareOutcome> OnShareResult;

        public INativeShare Native
        {
            get => native ??= CreateDefaultBackend();
            set => native = value;
        }

        /// <summary>
        /// Pick the backend for the platform. On Web the browser is the only thing that can share
        /// at all -- the Variant host bridge carries quit and orientation and nothing else -- so
        /// there is no host action to prefer over it.
        /// </summary>
        private INativeShare CreateDefaultBackend()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebNativeShare(gameObject.name);
#else
            return new LogOnlyNativeShare();
#endif
        }

        public void ShareScoreCard(int score, int bestScore, RectTransform cardRoot, Camera uiCamera)
        {
            string message = bestScore > 0 && score >= bestScore
                ? $"New best! I scored {score} singing Flappy Song."
                : $"I scored {score} singing Flappy Song. Best: {bestScore}.";

            INativeShare backend = Native;
            string path = null;
            if (backend.WantsScoreCard)
            {
                path = CaptureCard(cardRoot, uiCamera);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning("[ShareService] score card capture failed; sharing text only.");
                }
            }

            try
            {
                backend.Share(path, message, ShareLink.Url);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShareService] native share threw: {e}");
                OnShareResult?.Invoke(ShareOutcome.Failed);
                return;
            }

            if (backend.ImmediateOutcome.HasValue)
            {
                OnShareResult?.Invoke(backend.ImmediateOutcome.Value);
            }
        }

        /// <summary>
        /// Called by name from FlappyVoiceShare.jslib once the browser's share promise settles.
        /// Public and loosely typed because `SendMessage` is the only channel back from a jslib,
        /// and it can only carry a single string.
        /// </summary>
        public void OnWebShareResult(string outcome)
        {
            switch (outcome)
            {
                case "shared":
                    OnShareResult?.Invoke(ShareOutcome.Shared);
                    break;
                case "copied":
                    OnShareResult?.Invoke(ShareOutcome.Copied);
                    break;
                case "cancelled":
                    OnShareResult?.Invoke(ShareOutcome.Cancelled);
                    break;
                default:
                    Debug.LogWarning($"[ShareService] share reported \"{outcome}\"; the link is {ShareLink.Url}");
                    OnShareResult?.Invoke(ShareOutcome.Failed);
                    break;
            }
        }

        private string CaptureCard(RectTransform cardRoot, Camera uiCamera)
        {
            if (cardRoot == null)
            {
                return null;
            }

            cardRoot.GetWorldCorners(corners);
            float worldWidth = Vector3.Distance(corners[0], corners[3]);
            float worldHeight = Vector3.Distance(corners[0], corners[1]);
            if (worldWidth <= Mathf.Epsilon || worldHeight <= Mathf.Epsilon)
            {
                return null;
            }

            int width = Mathf.Max(64, captureWidthPx);
            int height = Mathf.Clamp(Mathf.RoundToInt(width * (worldHeight / worldWidth)), 64, maxCaptureHeightPx);

            GameObject rigGo = null;
            RenderTexture rt = null;
            Texture2D readback = null;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

                rigGo = new GameObject("ScoreCardCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
                Camera rig = rigGo.AddComponent<Camera>();

                Vector3 center = (corners[0] + corners[2]) * 0.5f;
                float depth = Mathf.Max(1f, worldHeight);
                rigGo.transform.SetPositionAndRotation(center - cardRoot.forward * depth, cardRoot.rotation);

                rig.orthographic = true;
                rig.orthographicSize = worldHeight * 0.5f;
                rig.aspect = worldWidth / worldHeight;
                rig.nearClipPlane = 0.01f;
                rig.farClipPlane = depth * 4f;
                rig.clearFlags = CameraClearFlags.SolidColor;
                rig.backgroundColor = cardBackground;
                rig.cullingMask = 1 << cardRoot.gameObject.layer;
                rig.allowMSAA = false;
                rig.allowHDR = false;
                rig.useOcclusionCulling = false;
                rig.enabled = false;
                if (uiCamera != null)
                {
                    rig.depth = uiCamera.depth + 1f;
                }

                rig.targetTexture = rt;
                rig.Render();
                rig.targetTexture = null;

                RenderTexture.active = rt;
                readback = new Texture2D(width, height, TextureFormat.RGB24, false);
                readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readback.Apply(false, false);

                byte[] png = readback.EncodeToPNG();
                if (png == null || png.Length == 0)
                {
                    return null;
                }

                string path = Path.Combine(Application.temporaryCachePath, fileName);
                File.WriteAllBytes(path, png);
                return path;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ShareService] capture failed: {e}");
                return null;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (readback != null)
                {
                    Destroy(readback);
                }
                if (rt != null)
                {
                    RenderTexture.ReleaseTemporary(rt);
                }
                if (rigGo != null)
                {
                    Destroy(rigGo);
                }
            }
        }
    }
}
