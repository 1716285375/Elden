using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace ZZ.Editor
{
    /// <summary>Requests the owned Viking Village package through the Editor's native asset service.</summary>
    public static class ArtStoreDownloadSetup
    {
        [MenuItem("Tools/ZZ/Environment/Download Viking Village")]
        public static void DownloadVillage()
        {
            UnityEditor.PackageManager.UI.Window.Open("29140");
            object manager = DownloadManager();
            MethodInfo method = manager.GetType().GetMethod("Download", new[] { typeof(IEnumerable<long>) });
            bool accepted = (bool)method.Invoke(manager, new object[] { new long[] { 29140 } });
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/viking-download-start.txt", "TermsAlreadyChecked=" + accepted);
        }

        [MenuItem("Tools/ZZ/Environment/Check Viking Village Download")]
        public static void CheckVillage()
        {
            object manager = DownloadManager();
            object operation = manager.GetType().GetMethod("GetDownloadOperation")
                .Invoke(manager, new object[] { (long?)29140 });
            string[] names = { "state", "progressPercentage", "errorMessage", "packageNewPath", "isInProgress" };
            File.WriteAllLines(".utmp/viking-download-status.txt", operation == null ? new[] { "No active operation" } :
                names.Select(name => name + "=" + operation.GetType().GetProperty(name).GetValue(operation)));
        }

        private static object DownloadManager()
        {
            // shortcut: Unity 6.3 internal service, recheck the API when upgrading the Editor.
            Assembly assembly = typeof(UnityEditor.PackageManager.UI.Window).Assembly;
            Type containerType = assembly.GetType("UnityEditor.PackageManager.UI.Internal.ServicesContainer", true);
            Type managerType = assembly.GetType("UnityEditor.PackageManager.UI.Internal.IAssetStoreDownloadManager", true);
            object container = containerType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static |
                BindingFlags.FlattenHierarchy).GetValue(null);
            return containerType.GetMethod("Resolve").MakeGenericMethod(managerType).Invoke(container, null);
        }
    }
}
