using System.IO;
using System.Xml;

namespace Sanguo.Editor
{
    /// <summary>
    /// Minimal Info.plist editor (plain XML, no dependency on Unity's iOS Xcode API) that adds the keys
    /// LAN play needs on iOS 14+. Idempotent: existing values are replaced, never duplicated.
    /// </summary>
    public static class InfoPlistEditor
    {
        public const string LocalNetworkUsage = "用于在局域网内发现并加入好友创建的对局房间。";
        public static readonly string[] BonjourServices = { "_sanguo._tcp", "_sanguo._udp" };

        public static void ApplyLanKeys(string plistPath)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(plistPath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
                doc.Load(reader);
            var dict = doc.SelectSingleNode("/plist/dict") as XmlElement ?? throw new InvalidDataException("Info.plist has no top-level dict.");
            SetString(doc, dict, "NSLocalNetworkUsageDescription", LocalNetworkUsage);
            SetStringArray(doc, dict, "NSBonjourServices", BonjourServices);
            SetBool(doc, dict, "UIRequiresFullScreen", true);
            SetBool(doc, dict, "ITSAppUsesNonExemptEncryption", false);
            var settings = new XmlWriterSettings { Indent = true, IndentChars = "\t" };
            using (var writer = XmlWriter.Create(plistPath, settings)) doc.Save(writer);
        }

        private static XmlElement FindValue(XmlElement dict, string key)
        {
            for (var node = dict.FirstChild; node != null; node = node.NextSibling)
            {
                if (node is XmlElement e && e.Name == "key" && e.InnerText == key)
                {
                    var value = e.NextSibling;
                    while (value != null && !(value is XmlElement)) value = value.NextSibling;
                    return value as XmlElement;
                }
            }
            return null;
        }

        private static void Replace(XmlDocument doc, XmlElement dict, string key, XmlElement newValue)
        {
            var existing = FindValue(dict, key);
            if (existing != null)
            {
                dict.ReplaceChild(newValue, existing);
                return;
            }
            var k = doc.CreateElement("key");
            k.InnerText = key;
            dict.AppendChild(k);
            dict.AppendChild(newValue);
        }

        private static void SetString(XmlDocument doc, XmlElement dict, string key, string value)
        {
            var e = doc.CreateElement("string");
            e.InnerText = value;
            Replace(doc, dict, key, e);
        }

        private static void SetBool(XmlDocument doc, XmlElement dict, string key, bool value)
        {
            Replace(doc, dict, key, doc.CreateElement(value ? "true" : "false"));
        }

        private static void SetStringArray(XmlDocument doc, XmlElement dict, string key, string[] values)
        {
            var array = doc.CreateElement("array");
            foreach (var v in values)
            {
                var s = doc.CreateElement("string");
                s.InnerText = v;
                array.AppendChild(s);
            }
            Replace(doc, dict, key, array);
        }
    }
}
