using System.IO;
using Sanguo.Data;
using UnityEngine;

namespace Sanguo.App
{
    /// <summary>
    /// Loads content JSON from Resources/Data (TextAssets). Works on every platform, including Android
    /// where StreamingAssets live inside the APK and cannot be read with System.IO.
    /// </summary>
    public sealed class ResourcesContentSource : IContentSource
    {
        private readonly string _folder;

        public ResourcesContentSource(string folder = "Data")
        {
            _folder = folder;
        }

        public string ReadText(string fileName)
        {
            string path = _folder + "/" + Path.GetFileNameWithoutExtension(fileName);
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) return null;
            string text = asset.text;
            Resources.UnloadAsset(asset);
            return text;
        }
    }

    /// <summary>Unity time source for the game session (unscaled, keeps running while paused).</summary>
    public sealed class UnityClock : Utils.IClock
    {
        public long NowMs => (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
    }
}
