using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    public static class PlayerLockOnTargetSelector
    {
        /// <summary>Returns the target with the smallest squared distance to the origin.</summary>
        public static CharacterManager SelectClosestTarget(
            IReadOnlyList<CharacterManager> possibleTargets,
            Vector3 origin)
        {
            CharacterManager closestTarget = null;
            float closestDistance = float.PositiveInfinity;
            if (possibleTargets == null)
            {
                return null;
            }

            for (int targetIndex = 0;
                targetIndex < possibleTargets.Count;
                targetIndex++)
            {
                CharacterManager candidate = possibleTargets[targetIndex];
                if (candidate == null)
                {
                    continue;
                }

                float candidateDistance =
                    (candidate.transform.position - origin).sqrMagnitude;
                if (candidateDistance < closestDistance)
                {
                    closestDistance = candidateDistance;
                    closestTarget = candidate;
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// Returns the nearest target lying left or right of the current target in reference space.
        /// </summary>
        public static CharacterManager SelectDirectionalTarget(
            IReadOnlyList<CharacterManager> possibleTargets,
            CharacterManager currentTarget,
            Transform directionReference,
            Vector3 origin,
            float switchDirection)
        {
            if (possibleTargets == null ||
                currentTarget == null ||
                directionReference == null ||
                Mathf.Approximately(switchDirection, 0f))
            {
                return null;
            }

            Vector3 currentDirection =
                (currentTarget.transform.position - origin).normalized;
            float currentHorizontalPosition = directionReference
                .InverseTransformDirection(currentDirection).x;
            float directionSign = Mathf.Sign(switchDirection);
            CharacterManager closestDirectionalTarget = null;
            float closestDistance = float.PositiveInfinity;

            for (int targetIndex = 0;
                targetIndex < possibleTargets.Count;
                targetIndex++)
            {
                CharacterManager candidate = possibleTargets[targetIndex];
                if (candidate == null || candidate == currentTarget)
                {
                    continue;
                }

                Vector3 candidateDirection =
                    (candidate.transform.position - origin).normalized;
                float candidateHorizontalPosition = directionReference
                    .InverseTransformDirection(candidateDirection).x;
                float horizontalOffset =
                    candidateHorizontalPosition - currentHorizontalPosition;
                if (horizontalOffset * directionSign <= 0f)
                {
                    continue;
                }

                float candidateDistance =
                    (candidate.transform.position - origin).sqrMagnitude;
                if (candidateDistance < closestDistance)
                {
                    closestDistance = candidateDistance;
                    closestDirectionalTarget = candidate;
                }
            }

            return closestDirectionalTarget;
        }
    }
}
