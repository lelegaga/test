using UnityEngine;

namespace Sanguo.App
{
    /// <summary>
    /// Many Android Wi-Fi drivers drop broadcast/multicast packets to save power unless the app holds a
    /// WifiManager.MulticastLock. Held while browsing or hosting LAN rooms. Needs the
    /// CHANGE_WIFI_MULTICAST_STATE permission (declared in Plugins/Android/SanguoNetwork.androidlib).
    /// No-op on other platforms.
    /// </summary>
    public sealed class AndroidNetworkLock : Sanguo.Network.INetworkLock
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _multicastLock;
#endif

        public bool IsHeld { get; private set; }

        public void Acquire()
        {
            if (IsHeld) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                using (var wifi = context.Call<AndroidJavaObject>("getSystemService", "wifi"))
                {
                    _multicastLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "sanguo-lan");
                    _multicastLock.Call("setReferenceCounted", false);
                    _multicastLock.Call("acquire");
                }
            }
            catch (AndroidJavaException ex)
            {
                Debug.LogWarning("[Sanguo] Could not acquire multicast lock: " + ex.Message);
                return;
            }
#endif
            IsHeld = true;
        }

        public void Release()
        {
            if (!IsHeld) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _multicastLock?.Call("release");
            }
            catch (AndroidJavaException ex)
            {
                Debug.LogWarning("[Sanguo] Could not release multicast lock: " + ex.Message);
            }
            _multicastLock?.Dispose();
            _multicastLock = null;
#endif
            IsHeld = false;
        }
    }
}
