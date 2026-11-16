using UnityEditor;
using UnityEngine;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Import settings for the game's sound files (Resources/Audio): everything mono and Vorbis-compressed;
    /// long loops and music stream from disk, short one-shots are kept ready in memory for instant playback.
    /// </summary>
    public class AudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = true;
            bool longSound = assetPath.Contains("/loops/") || assetPath.Contains("/music/");
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = longSound ? 0.55f : 0.7f;
            s.loadType = longSound ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = s;
        }
    }
}
