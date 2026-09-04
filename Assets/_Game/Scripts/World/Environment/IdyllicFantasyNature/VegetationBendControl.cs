using UnityEngine;

namespace IdyllicFantasyNature
{
    [ExecuteInEditMode]
    public class VegetationBendControl : MonoBehaviour
    {
        private const float k_OriginLookupIntervalSeconds = 0.5f;

        private static readonly int s_PlayerPositionProperty =
            Shader.PropertyToID("_Player_Position");
        private static readonly int s_BendStrengthProperty =
            Shader.PropertyToID("_Bend_Strength");
        private static readonly int s_StartBendRangeProperty =
            Shader.PropertyToID("_Start_Bend_Range");

        [SerializeField] private bool _enableBendFeature = false;
        [Tooltip("The origin where the impact on the object starts")]
        [SerializeField] private Transform _bendOrigin;
        [Range(0.3f, 1)]
        [Tooltip("object starts to bend when the player is at a certain distance")]
        [SerializeField] private float _startBendRange;
        [Range(0, 1)]
        [SerializeField] private float _bendStrength;
        [Tooltip("material of the vegetation objects")]
        [SerializeField] private Material[] _material;

        // current world space position of the bending object 
        private Vector3 _currentBendPosition;
        private float m_nextOriginLookupTime;

        private void Update()
        {
            if (!_enableBendFeature)
            {
                return;
            }

            if (_bendOrigin == null && Time.realtimeSinceStartup >= m_nextOriginLookupTime)
            {
                m_nextOriginLookupTime =
                    Time.realtimeSinceStartup + k_OriginLookupIntervalSeconds;
                ResolveBendOrigin();
            }

            if (_bendOrigin != null)
            {
                MoveOnVegetation();
            }
        }

        /// <summary>
        /// Falls back to the local player so vegetation still bends once one spawns,
        /// instead of throwing on an unassigned origin.
        /// </summary>
        private void ResolveBendOrigin()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                _bendOrigin = playerObject.transform;
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                _bendOrigin = mainCamera.transform;
            }
        }

        private void OnValidate()
        {
            BendSettings();
        }

        /// <summary>
        /// the material gets the object position to know when to bend
        /// only updates when player moves
        /// </summary>
        private void MoveOnVegetation()
        {
            Vector3 bendPosition = _bendOrigin.position;
            if (_currentBendPosition == bendPosition || _material == null)
            {
                return;
            }

            for (int i = 0; i < _material.Length; i++)
            {
                _material[i]?.SetVector(s_PlayerPositionProperty, bendPosition);
            }

            _currentBendPosition = bendPosition;
        }

        /// <summary>
        /// the material gets the bend settings
        /// </summary>
        private void BendSettings()
        {
            if (_material == null)
            {
                return;
            }

            for (int i = 0; i < _material.Length; i++)
            {
                Material material = _material[i];
                if (material == null)
                {
                    continue;
                }

                material.SetFloat(s_BendStrengthProperty, _bendStrength);
                material.SetFloat(s_StartBendRangeProperty, _startBendRange);
            }
        }
    }
}
