using System.Collections.Generic;
using UnityEngine;

namespace ZZ
{
    public static class PlayerLockOnTargetSelector
    {
        /// <summary>Prefers view alignment when supplied; otherwise selects the nearest target.</summary>
        public static CharacterManager SelectClosestTarget(
            IReadOnlyList<CharacterManager> possibleTargets,
            Vector3 origin,
            Transform directionReference = null)
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
                if (directionReference != null)
                {
                    Vector3 direction = (candidate.transform.position - origin).normalized;
                    float alignment = Vector3.Dot(directionReference.forward, direction);
                    if (alignment <= 0f)
                    {
                        continue;
                    }
                    candidateDistance = 1f - alignment + Mathf.Sqrt(candidateDistance) * 0.005f;
                }
                if (candidateDistance < closestDistance)
                {
                    closestDistance = candidateDistance;
                    closestTarget = candidate;
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// Returns the adjacent target by horizontal view angle on the requested side.
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
            Vector3 localCurrent = directionReference.InverseTransformDirection(currentDirection);
            float currentHorizontalPosition = Mathf.Atan2(localCurrent.x, localCurrent.z);
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
                Vector3 localCandidate = directionReference.InverseTransformDirection(candidateDirection);
                if (localCandidate.z <= 0f)
                {
                    continue;
                }
                float candidateHorizontalPosition = Mathf.Atan2(localCandidate.x, localCandidate.z);
                float horizontalOffset =
                    candidateHorizontalPosition - currentHorizontalPosition;
                if (horizontalOffset * directionSign <= 0f)
                {
                    continue;
                }

                float candidateDistance = Mathf.Abs(horizontalOffset);
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
