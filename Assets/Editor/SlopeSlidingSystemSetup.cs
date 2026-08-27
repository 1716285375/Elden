using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Configures and validates the EP124 slope sliding foundation.</summary>
    public static class SlopeSlidingSystemSetup
    {
        private const int k_SlipperyLayerIndex = 15;
        private const string k_SlipperyLayerName = "Slippery Default";

        /// <summary>Creates the dedicated slippery environment Physics Layer.</summary>
        [MenuItem("Tools/Elden/Configure Slope Sliding System")]
        public static void ConfigureSlopeSlidingSystem()
        {
            ConfigureSlipperyLayer();
            AssetDatabase.SaveAssets();
            ValidateSlopeSlidingSystem();
            Debug.Log(
                "[SlopeSlidingSystemSetup] Configured EP124 slippery " +
                "surface classification and shared locomotion rules.");
        }

        /// <summary>Validates layer classification, tuning, and projected velocity.</summary>
        [MenuItem("Tools/Elden/Validate Slope Sliding System")]
        public static void ValidateSlopeSlidingSystem()
        {
            if (LayerMask.NameToLayer(k_SlipperyLayerName) !=
                k_SlipperyLayerIndex)
            {
                throw new InvalidOperationException(
                    $"{k_SlipperyLayerName} must use Layer " +
                    $"{k_SlipperyLayerIndex}.");
            }

            ValidateUtilityMasks();
            ValidateLocomotionContract();
            Debug.Log(
                "[SlopeSlidingSystemValidation] EP124 slope probe, layer " +
                "masks, projection, and safety contracts are valid.");
        }

        private static void ConfigureSlipperyLayer()
        {
            UnityEngine.Object tagManager = AssetDatabase
                .LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            SerializedObject serializedTagManager =
                new SerializedObject(tagManager);
            SerializedProperty layers = serializedTagManager.FindProperty(
                "layers");
            SerializedProperty slipperyLayer = layers.GetArrayElementAtIndex(
                k_SlipperyLayerIndex);
            if (!string.IsNullOrEmpty(slipperyLayer.stringValue) &&
                slipperyLayer.stringValue != k_SlipperyLayerName)
            {
                throw new InvalidOperationException(
                    $"Layer {k_SlipperyLayerIndex} is already used by " +
                    $"{slipperyLayer.stringValue}.");
            }

            slipperyLayer.stringValue = k_SlipperyLayerName;
            serializedTagManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tagManager);
        }

        private static void ValidateUtilityMasks()
        {
            GameObject utilityObject = new GameObject("Slope Utility Validation");
            utilityObject.SetActive(false);
            try
            {
                WorldUtilityManager utilityManager =
                    utilityObject.AddComponent<WorldUtilityManager>();
                int slipperyLayerBit = 1 << k_SlipperyLayerIndex;
                if ((utilityManager.GetEnvironmentLayers().value &
                        slipperyLayerBit) == 0 ||
                    (utilityManager.GetGroundLayers().value &
                        slipperyLayerBit) == 0 ||
                    (utilityManager.GetSlipperyEnviroLayers().value &
                        slipperyLayerBit) == 0)
                {
                    throw new InvalidOperationException(
                        "Slippery Default must belong to environment, " +
                        "ground, and slippery masks.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(utilityObject);
            }
        }

        private static void ValidateLocomotionContract()
        {
            const BindingFlags k_InstanceMethods =
                BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo slopeCheck = typeof(CharacterLocomotionManager)
                .GetMethod("HandleSlopeSlideCheck", k_InstanceMethods);
            MethodInfo slopeVelocity = typeof(CharacterLocomotionManager)
                .GetMethod("SetSlopeSlideVelocity", k_InstanceMethods);
            MethodInfo groundedVelocity = typeof(CharacterLocomotionManager)
                .GetMethod("SetGroundedVelocity", k_InstanceMethods);
            Vector3 normal = Quaternion.AngleAxis(30f, Vector3.forward) *
                Vector3.up;
            Vector3 velocity = CharacterLocomotionManager
                .CalculateSlopeSlideVelocity(Vector3.down, normal, 11f);
            if (slopeCheck == null ||
                slopeVelocity == null ||
                groundedVelocity == null ||
                Mathf.Abs(Vector3.Dot(velocity, normal)) > 0.0001f ||
                !Mathf.Approximately(velocity.magnitude, 11f))
            {
                throw new InvalidOperationException(
                    "Slope sliding locomotion contracts are incomplete.");
            }
        }
    }
}
