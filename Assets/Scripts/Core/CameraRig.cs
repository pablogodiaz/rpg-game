using UnityEngine;

namespace ReadyCheck
{
    /// <summary>Fixed 3/4 camera that follows the leader and leans toward the boss.</summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }
        public static Camera MainCam { get; private set; }

        public Transform Target;
        public Transform SecondaryTarget;
        public Vector3 Offset = new Vector3(0f, 13.5f, -11.5f);
        [Range(0f, 1f)] public float SecondaryWeight = 0.3f;

        float shakeUntil;
        float shakeStrength;

        void Awake()
        {
            Instance = this;
            MainCam = GetComponent<Camera>();
            transform.rotation = Quaternion.LookRotation(-Offset.normalized, Vector3.up);
        }

        public static void Shake(float strength, float duration = 0.3f)
        {
            if (Instance == null) return;
            Instance.shakeStrength = strength;
            Instance.shakeUntil = Time.unscaledTime + duration;
        }

        Vector3 Focus()
        {
            if (Target == null) return Vector3.zero;
            var focus = Target.position;
            if (SecondaryTarget != null) focus = Vector3.Lerp(focus, SecondaryTarget.position, SecondaryWeight);
            focus.y = 0f;
            return focus;
        }

        public void Snap()
        {
            transform.position = Focus() + Offset;
        }

        void LateUpdate()
        {
            var desired = Focus() + Offset;
            float t = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
            var pos = Vector3.Lerp(transform.position, desired, t);
            if (Time.unscaledTime < shakeUntil)
            {
                var r = Random.insideUnitCircle * shakeStrength;
                pos += new Vector3(r.x, r.y, 0f);
            }
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(-Offset.normalized, Vector3.up);
        }
    }
}
