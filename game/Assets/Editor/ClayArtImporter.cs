using UnityEditor;
using UnityEngine;

namespace PetThem.Editor
{
    /// <summary>Consistent mobile-sized imports while keeping generated source PNGs intact.</summary>
    public sealed class ClayArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Art/Clay/", System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            bool background = assetPath.EndsWith("/Arena.png", System.StringComparison.Ordinal);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = !background;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = background ? 2048 : 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
