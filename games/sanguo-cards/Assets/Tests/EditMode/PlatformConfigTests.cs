using System.IO;
using System.Linq;
using System.Xml;
using NUnit.Framework;
using Sanguo.Editor;

namespace Sanguo.Tests
{
    public class PlatformConfigTests
    {
        private const string MinimalPlist = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
	<key>CFBundleName</key>
	<string>Sanguo</string>
	<key>UIRequiresFullScreen</key>
	<false/>
</dict>
</plist>";

        [Test]
        public void InfoPlistGetsLocalNetworkKeysExactlyOnce()
        {
            string path = Path.Combine(Path.GetTempPath(), "sanguo-test-" + System.Guid.NewGuid().ToString("N") + ".plist");
            File.WriteAllText(path, MinimalPlist);
            try
            {
                InfoPlistEditor.ApplyLanKeys(path);
                InfoPlistEditor.ApplyLanKeys(path);
                var doc = new XmlDocument { XmlResolver = null };
                using (var r = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore })) doc.Load(r);
                var keys = doc.SelectNodes("/plist/dict/key").Cast<XmlNode>().Select(n => n.InnerText).ToList();
                Assert.That(keys.Count(k => k == "NSLocalNetworkUsageDescription"), Is.EqualTo(1));
                Assert.That(keys.Count(k => k == "NSBonjourServices"), Is.EqualTo(1));
                Assert.That(keys.Count(k => k == "UIRequiresFullScreen"), Is.EqualTo(1));
                Assert.That(keys, Does.Contain("CFBundleName"));
                var services = doc.SelectNodes("/plist/dict/array/string").Cast<XmlNode>().Select(n => n.InnerText).ToList();
                Assert.That(services, Is.EquivalentTo(InfoPlistEditor.BonjourServices));
                string xml = File.ReadAllText(path);
                Assert.That(xml, Does.Contain(InfoPlistEditor.LocalNetworkUsage));
                Assert.That(xml, Does.Contain("<true />").Or.Contain("<true/>"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void AndroidLibraryDeclaresLanPermissions()
        {
            string dir = Path.GetDirectoryName(TestContent.DataDirectory); // Assets/Resources
            string manifest = Path.Combine(Path.GetDirectoryName(dir), "Plugins", "Android", "SanguoNetwork.androidlib", "AndroidManifest.xml");
            string xml = File.ReadAllText(manifest);
            foreach (var permission in new[] { "INTERNET", "ACCESS_NETWORK_STATE", "ACCESS_WIFI_STATE", "CHANGE_WIFI_MULTICAST_STATE" })
                Assert.That(xml, Does.Contain("android.permission." + permission));
        }
    }
}
