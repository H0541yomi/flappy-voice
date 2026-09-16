using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace FlappyVoice.Editor
{
    // Generates the TMP font assets the app draws with: IM FELL Great Primer SC, the face the
    // signs are designed in, and Gulzar for the score numerals. Like the scene and the keyed art
    // these are output - the .ttf files and this script are the source, the .asset files are
    // rebuilt from them.
    //
    // Two assets rather than a fallback list. Gulzar is only ever asked for digits, and a
    // fallback would mean either shipping its whole Nastaliq glyph set in the atlas or letting
    // TMP decide per character which face a numeral belongs to.
    //
    // The atlases are baked and the assets left in Static population mode on purpose. Dynamic
    // mode rasterises missing glyphs at runtime, which on Web means shipping the font data and
    // doing the work on the player's main thread the first time a new character appears; both
    // vocabularies are known up front, so there is nothing to discover.
    public static class FontBuilder
    {
        public const string FontAssetPath = "Assets/Art/Fonts/IMFellGreatPrimerSC SDF.asset";
        public const string NumberFontAssetPath = "Assets/Art/Fonts/Gulzar SDF.asset";
        private const string SourceFontPath = "Assets/Art/Fonts/IMFellGreatPrimerSC-Regular.ttf";
        private const string SourceNumberFontPath = "Assets/Art/Fonts/Gulzar-Regular.ttf";

        // TMP's own defaults for a hand-built asset. 90pt sampling into a 1024 atlas leaves the
        // SDF enough gradient to survive the title sizes without a second atlas page.
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasSize = 1024;

        // Ten glyphs, so a quarter of the page is plenty: Gulzar's digits are tall and 256 would
        // spill onto a second atlas texture.
        private const int NumberAtlasSize = 512;
        private const string Digits = "0123456789";

        [MenuItem("Flappy Voice/Build Font Asset")]
        public static void BuildFontAsset()
        {
            bool built = BuildSignFont() != null;
            built |= BuildNumberFont() != null;
            if (!built)
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
            return existing != null ? existing : BuildSignFont();
        }

        /// <summary>
        /// The digits-only face the scores are set in, built on the same terms as the sign face.
        /// </summary>
        public static TMP_FontAsset EnsureNumberFontAsset()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NumberFontAssetPath);
            return existing != null ? existing : BuildNumberFont();
        }

        private static TMP_FontAsset BuildSignFont()
        {
            return Build(SourceFontPath, FontAssetPath, "IMFellGreatPrimerSC SDF", PrintableAscii(),
                AtlasSize);
        }

        private static TMP_FontAsset BuildNumberFont()
        {
            return Build(SourceNumberFontPath, NumberFontAssetPath, "Gulzar SDF", Digits,
                NumberAtlasSize);
        }

        private static TMP_FontAsset Build(string sourceFontPath, string fontAssetPath, string assetName,
            string characters, int atlasSize)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
            if (source == null)
            {
                Debug.LogError($"[FontBuilder] no font at {sourceFontPath}");
                return null;
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, SamplingPointSize, AtlasPadding,
                GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic);
            if (font == null)
            {
                Debug.LogError("[FontBuilder] CreateFontAsset failed. Check 'Include Font Data' on the .ttf.");
                return null;
            }

            font.name = assetName;
            if (!font.TryAddCharacters(characters, out string missing))
            {
                // Not fatal: a glyph the face genuinely lacks would fall back at runtime. Worth
                // saying out loud, because a missing digit would show up as a blank score.
                Debug.LogWarning($"[FontBuilder] {assetName} has no glyph for: {missing}");
            }

            // Baked, so freeze it. Done after TryAddCharacters - Static refuses to add glyphs.
            font.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(font, fontAssetPath);

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
                font.material.name = $"{assetName} Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
            }

            // No ImportAsset here: CreateAsset has already imported, and asking again while the
            // sub-objects are still being attached makes the importer report an inconsistent
            // result. The instance in hand IS the asset.
            EditorUtility.SetDirty(font);
            Debug.Log($"[FontBuilder] built {fontAssetPath}");
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
