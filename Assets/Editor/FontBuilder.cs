using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace FlappyVoice.Editor
{
    // Generates the TMP font asset for IM FELL Great Primer SC, the face the signs are designed
    // in. Like the scene and the keyed art, this is output: the .ttf and this script are the
    // source, the .asset is rebuilt from them.
    //
    // The atlas is baked and the asset left in Static population mode on purpose. Dynamic mode
    // rasterises missing glyphs at runtime, which on Web means shipping the font data and doing
    // the work on the player's main thread the first time a new character appears; the game's
    // whole vocabulary is ASCII and known up front, so there is nothing to discover.
    public static class FontBuilder
    {
        public const string FontAssetPath = "Assets/Art/Fonts/IMFellGreatPrimerSC SDF.asset";
        private const string SourceFontPath = "Assets/Art/Fonts/IMFellGreatPrimerSC-Regular.ttf";

        // TMP's own defaults for a hand-built asset. 90pt sampling into a 1024 atlas leaves the
        // SDF enough gradient to survive the title sizes without a second atlas page.
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasSize = 1024;

        [MenuItem("Flappy Voice/Build Font Asset")]
        public static void BuildFontAsset()
        {
            if (Build() == null)
            {
                return;
            }
            AssetDatabase.SaveAssets();
        }

        // Returns the existing asset untouched unless it is missing, so a scene build does not
        // churn the atlas (and its GUIDs) on every run.
        public static TMP_FontAsset EnsureFontAsset()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            return existing != null ? existing : Build();
        }

        private static TMP_FontAsset Build()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError($"[FontBuilder] no font at {SourceFontPath}");
                return null;
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, AtlasPadding,
                GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic);
            if (font == null)
            {
                Debug.LogError("[FontBuilder] CreateFontAsset failed. Check 'Include Font Data' on the .ttf.");
                return null;
            }

            font.name = "IMFellGreatPrimerSC SDF";
            if (!font.TryAddCharacters(PrintableAscii(), out string missing))
            {
                // Not fatal: a glyph the face genuinely lacks would fall back at runtime. Worth
                // saying out loud, because a missing digit would show up as a blank score.
                Debug.LogWarning($"[FontBuilder] font has no glyph for: {missing}");
            }

            // Baked, so freeze it. Done after TryAddCharacters - Static refuses to add glyphs.
            font.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(font, FontAssetPath);

            // The atlas texture and material are created in memory by CreateFontAsset and would be
            // lost on domain reload if they were not parented into the asset file.
            if (font.atlasTextures != null)
            {
                for (int index = 0; index < font.atlasTextures.Length; index++)
                {
                    Texture2D atlas = font.atlasTextures[index];
                    if (atlas == null)
                    {
                        continue;
                    }
                    atlas.name = index == 0 ? "Atlas" : $"Atlas {index}";
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
            }
            if (font.material != null)
            {
                font.material.name = "IMFellGreatPrimerSC SDF Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
            }

            // No ImportAsset here: CreateAsset has already imported, and asking again while the
            // sub-objects are still being attached makes the importer report an inconsistent
            // result. The instance in hand IS the asset.
            EditorUtility.SetDirty(font);
            Debug.Log($"[FontBuilder] built {FontAssetPath}");
            return font;
        }

        private static string PrintableAscii()
        {
            StringBuilder characters = new StringBuilder(95);
            for (char character = ' '; character <= '~'; character++)
            {
                characters.Append(character);
            }
            return characters.ToString();
        }
    }
}
