using UnityEditor;
using UnityEngine;

namespace PackTheTrunk.EditorTools
{
    /// <summary>
    /// Music streams from disk at full Vorbis quality; ambience loops stay compressed in memory so
    /// they loop gaplessly; short effects are decompressed on load for zero-latency playback.
    /// </summary>
    public class AudioImportSettings : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            if (assetPath.Contains("/Resources/Audio/"))
            {
                bool ambience = assetPath.Contains("/Ambience/");
                importer.forceToMono = false;
                importer.loadInBackground = ambience;
                var fx = importer.defaultSampleSettings;
                fx.loadType = ambience ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                fx.compressionFormat = AudioCompressionFormat.Vorbis;
                fx.quality = ambience ? 0.85f : 1f;
                fx.preloadAudioData = true;
                importer.defaultSampleSettings = fx;
                return;
            }
            if (!assetPath.Contains("/Resources/Music/")) return;
            importer.forceToMono = false;
            importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 1f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
        }
    }
}
