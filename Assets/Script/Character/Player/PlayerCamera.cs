using UnityEngine;

namespace ZZ
{
    public class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera instance;
        public static PlayerCamera Instance => instance;

        [SerializeField] private Camera cameraObject;
        public Camera CameraObject => cameraObject;
        public Vector3 CameraForward => cameraObject != null ? cameraObject.transform.forward : transform.forward;
        public Vector3 CameraRight => cameraObject != null ? cameraObject.transform.right : transform.right;

#if UNITY_EDITOR
        public void SetCameraObject(Camera value)
        {
            cameraObject = value;
        }
#endif

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            if (cameraObject == null)
            {
                cameraObject = GetComponentInChildren<Camera>();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
