using UnityEngine;

namespace ReadyCheck
{
    /// <summary>
    /// 2.5D presentation for a unit: a camera-facing pixel sprite, a blob shadow on the ground,
    /// a small walk bob, sprite flipping, hit flashes and a "lying down" pose when dead.
    /// </summary>
    public class UnitVisual : MonoBehaviour
    {
        public SpriteRenderer Body;
        public Transform BodyPivot;
        public float HeightOffset;
        public Color BaseTint = Color.white;

        Unit unit;
        Vector3 lastPosition;
        float walkPhase;
        float flashUntil;
        Color flashColor;
        bool faceLeft;

        public static UnitVisual Create(Unit unit, Sprite sprite, float shadowWidth)
        {
            var pivot = new GameObject("Visual").transform;
            pivot.SetParent(unit.transform, false);

            var bodyGo = new GameObject("Sprite");
            bodyGo.transform.SetParent(pivot, false);
            var sr = bodyGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(unit.transform, false);
            shadowGo.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            shadowGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadowGo.transform.localScale = Vector3.one * shadowWidth;
            var shadow = shadowGo.AddComponent<SpriteRenderer>();
            shadow.sprite = PixelArt.Shadow();
            shadow.sortingOrder = -5;

            var visual = unit.gameObject.AddComponent<UnitVisual>();
            visual.unit = unit;
            visual.Body = sr;
            visual.BodyPivot = pivot;
            visual.lastPosition = unit.transform.position;
            return visual;
        }

        public void Flash(Color color, float duration = 0.08f)
        {
            flashColor = color;
            flashUntil = Time.time + duration;
        }

        void LateUpdate()
        {
            var cam = CameraRig.MainCam;
            var camRotation = cam != null ? cam.transform.rotation : Quaternion.identity;

            var pos = transform.position;
            var delta = pos - lastPosition;
            lastPosition = pos;
            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;

            if (cam != null)
            {
                float side = Vector3.Dot(delta, cam.transform.right);
                if (side > 0.0005f) faceLeft = false;
                else if (side < -0.0005f) faceLeft = true;
            }
            Body.flipX = faceLeft;

            if (unit != null && unit.IsDead)
            {
                BodyPivot.localPosition = new Vector3(0f, 0.12f, 0f);
                BodyPivot.rotation = camRotation * Quaternion.Euler(0f, 0f, faceLeft ? -90f : 90f);
                Body.color = new Color(0.55f, 0.55f, 0.6f, 0.9f);
                return;
            }

            float bob = 0f;
            if (speed > 0.3f)
            {
                walkPhase += Time.deltaTime * 14f;
                bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.09f;
            }
            else
            {
                walkPhase = 0f;
            }

            BodyPivot.localPosition = Vector3.up * (bob + HeightOffset);
            BodyPivot.rotation = camRotation;
            Body.color = Time.time < flashUntil ? flashColor : BaseTint;
        }
    }
}
