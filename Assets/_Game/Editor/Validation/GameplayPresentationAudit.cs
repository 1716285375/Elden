using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZZ.Editor
{
    /// <summary>Reports authored presentation contracts before applying content changes.</summary>
    public static class GameplayPresentationAudit
    {
        [MenuItem("Tools/ZZ/Audit Gameplay Presentation")]
        public static void Audit()
        {
            var report = new StringBuilder();
            foreach (FrontendSelectableVisual visual in Object.FindObjectsByType<FrontendSelectableVisual>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                report.AppendLine($"VISUAL {visual.name} {EditorJsonUtility.ToJson(visual)}");
            }
            GameObject ui = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/World/Managers/Player UI Manager.prefab");
            foreach (Button button in ui.GetComponentsInChildren<Button>(true))
            {
                report.AppendLine($"BUTTON {AnimationUtility.CalculateTransformPath(button.transform, ui.transform)} " +
                    $"graphic={button.targetGraphic?.name} text={button.GetComponentInChildren<TMPro.TMP_Text>(true)?.text}");
            }
            const string root = "Assets/_Game/Art/Characters/Shared/Humanoid/AnimationControllers/";
            var runtime = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(root + "Runtime/Humanoid Runtime.controller");
            var imported = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(root + "Overrides/Overrides/Greatsword.overrideController");
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            imported.GetOverrides(pairs);
            foreach (AnimationClip clip in runtime.animationClips.Distinct())
            {
                AnimationClip replacement = pairs.FirstOrDefault(pair => pair.Key == clip || pair.Key.name == clip.name).Value;
                report.AppendLine($"CLIP {clip.name} -> {replacement?.name} events=" +
                    string.Join(",", AnimationUtility.GetAnimationEvents(clip).Select(entry => entry.functionName)));
            }
            foreach (var pair in pairs.Where(pair => pair.Value != null))
            {
                report.AppendLine($"IMPORTED {pair.Key.name} -> {pair.Value.name}");
            }
            Directory.CreateDirectory(".utmp");
            File.WriteAllText(".utmp/gameplay-presentation-audit.txt", report.ToString());
            Debug.Log("Presentation audit written to .utmp/gameplay-presentation-audit.txt");
        }
    }
}
