using Sanguo.Save;
using UnityEngine;

namespace Sanguo.App
{
    /// <summary>Loads/saves the local <see cref="PlayerProfile"/> under Application.persistentDataPath and applies its settings.</summary>
    public sealed class ProfileService
    {
        public PlayerProfile Profile { get; private set; }

        public void Load()
        {
            Profile = ProfileStore.Load(Application.persistentDataPath);
            Apply();
        }

        public void Save()
        {
            try
            {
                ProfileStore.Save(Application.persistentDataPath, Profile);
            }
            catch (System.IO.IOException ex)
            {
                Debug.LogWarning("[Sanguo] Could not save profile: " + ex.Message);
            }
            Apply();
        }

        public void Apply()
        {
            int levels = QualitySettings.names.Length;
            if (levels > 0)
            {
                // Map low/medium/high onto whatever quality levels the project defines.
                int level = Mathf.Clamp(Mathf.RoundToInt(Profile.Quality / 2f * (levels - 1)), 0, levels - 1);
                QualitySettings.SetQualityLevel(level, true);
            }
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
        }
    }
}
