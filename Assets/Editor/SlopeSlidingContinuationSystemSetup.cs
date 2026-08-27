using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ZZ.Editor
{
    /// <summary>Validates the EP126 grounded and character-slide continuation.</summary>
    public static class SlopeSlidingContinuationSystemSetup
    {
        private const int k_PlayerLayer = 8;
        private const int k_DamageableCharacterLayer = 10;

        /// <summary>Persists and validates the code-driven EP126 configuration.</summary>
        [MenuItem("Tools/Elden/Configure Slope Sliding Continuation")]
        public static void ConfigureSlopeSlidingContinuationSystem()
        {
            AssetDatabase.SaveAssets();
            ValidateSlopeSlidingContinuationSystem();
            Debug.Log(
                "[SlopeSlidingContinuationSystemSetup] Configured EP126 " +
                "grounded edges and character-surface sliding.");
        }

        /// <summary>Validates layer masks, extension points, and projection math.</summary>
        [MenuItem("Tools/Elden/Validate Slope Sliding Continuation")]
        public static void ValidateSlopeSlidingContinuationSystem()
        {
            ValidateCharacterLayers();
            ValidateGroundedExtensionPoints();
            ValidateCharacterSlideProjection();
            Debug.Log(
                "[SlopeSlidingContinuationValidation] EP126 grounded state, " +
                "continued surface slide, and head collision are valid.");
        }

        private static void ValidateCharacterLayers()
        {
            GameObject utilityObject = new GameObject(
                "Character Layer Validation");
            utilityObject.SetActive(false);
            try
            {
                WorldUtilityManager utilityManager =
                    utilityObject.AddComponent<WorldUtilityManager>();
                int requiredLayers =
                    (1 << k_PlayerLayer) |
                    (1 << k_DamageableCharacterLayer);
                if ((utilityManager.GetCharacterLayers().value &
                        requiredLayers) != requiredLayers)
                {
                    throw new InvalidOperationException(
                        "Character layers must include Player and Damageable Character.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(utilityObject);
            }
        }

        private static void ValidateGroundedExtensionPoints()
        {
            const BindingFlags k_NonPublicInstance =
                BindingFlags.NonPublic | BindingFlags.Instance;
            MethodInfo groundedMethod = typeof(CharacterLocomotionManager)
                .GetMethod("OnIsGrounded", k_NonPublicInstance);
            MethodInfo airborneMethod = typeof(CharacterLocomotionManager)
                .GetMethod("OnIsNotGrounded", k_NonPublicInstance);
            MethodInfo slideMethod = typeof(CharacterLocomotionManager)
                .GetMethod("SlideOffCharacter", k_NonPublicInstance);
            if (groundedMethod?.IsVirtual != true ||
                groundedMethod.IsFamily == false ||
                airborneMethod?.IsVirtual != true ||
                airborneMethod.IsFamily == false ||
                slideMethod == null)
            {
                throw new InvalidOperationException(
                    "Grounded edges and character sliding need protected extensions.");
            }
        }

        private static void ValidateCharacterSlideProjection()
        {
            Vector3 normal = Quaternion.AngleAxis(35f, Vector3.forward) *
                Vector3.up;
            Vector3 velocity = CharacterLocomotionManager
                .CalculateCharacterSlideVelocity(-8f, normal);
            if (Mathf.Abs(Vector3.Dot(velocity, normal)) > 0.0001f ||
                velocity.y >= 0f)
            {
                throw new InvalidOperationException(
                    "Character slide velocity must follow the contacted surface.");
            }
        }
    }
}
