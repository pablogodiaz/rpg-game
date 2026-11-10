using UnityEngine;
using UnityEngine.Rendering;

namespace ReadyCheck
{
    /// <summary>The boss room: a round stone floor ringed by pillars and braziers. Built from code.</summary>
    public static class Arena
    {
        public const float Radius = 12.5f;

        public static Vector3 Clamp(Vector3 p)
        {
            p.y = 0f;
            var flat = new Vector2(p.x, p.z);
            if (flat.magnitude > Radius)
            {
                flat = flat.normalized * Radius;
                p.x = flat.x;
                p.z = flat.y;
            }
            return p;
        }

        public static void Build()
        {
            var root = new GameObject("Arena").transform;

            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root, false);
            floor.transform.localScale = new Vector3(6f, 1f, 6f); // 60 x 60 units
            Object.Destroy(floor.GetComponent<Collider>());
            var floorMat = floor.GetComponent<MeshRenderer>().material;
            floorMat.mainTexture = PixelArt.FloorTexture();
            floorMat.mainTextureScale = new Vector2(30f, 30f);
            floorMat.SetFloat("_Glossiness", 0.05f);

            // Inner circle, slightly raised and lighter, so the fight area reads clearly
            var dais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dais.name = "Dais";
            dais.transform.SetParent(root, false);
            dais.transform.localPosition = new Vector3(0f, -0.49f, 0f);
            dais.transform.localScale = new Vector3(Radius * 2f + 1f, 0.5f, Radius * 2f + 1f);
            Object.Destroy(dais.GetComponent<Collider>());
            var daisMat = dais.GetComponent<MeshRenderer>().material;
            daisMat.mainTexture = PixelArt.FloorTexture();
            daisMat.mainTextureScale = new Vector2(13f, 13f);
            daisMat.color = new Color(1.15f, 1.1f, 1.2f);
            daisMat.SetFloat("_Glossiness", 0.05f);

            // Pillars
            var pillarTex = PixelArt.WallTexture();
            const int pillarCount = 16;
            for (int i = 0; i < pillarCount; i++)
            {
                float a = i * Mathf.PI * 2f / pillarCount;
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Pillar";
                pillar.transform.SetParent(root, false);
                pillar.transform.localPosition = new Vector3(Mathf.Sin(a) * (Radius + 1.6f), 1.5f, Mathf.Cos(a) * (Radius + 1.6f));
                pillar.transform.localScale = new Vector3(1.1f, 3f, 1.1f);
                pillar.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                Object.Destroy(pillar.GetComponent<Collider>());
                var mat = pillar.GetComponent<MeshRenderer>().material;
                mat.mainTexture = pillarTex;
                mat.mainTextureScale = new Vector2(1f, 3f);
                mat.SetFloat("_Glossiness", 0f);
            }

            // Braziers with warm point lights
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                var pos = new Vector3(Mathf.Sin(a) * (Radius + 0.6f), 0f, Mathf.Cos(a) * (Radius + 0.6f));
                var brazier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                brazier.name = "Brazier";
                brazier.transform.SetParent(root, false);
                brazier.transform.localPosition = pos + Vector3.up * 0.5f;
                brazier.transform.localScale = new Vector3(0.7f, 0.5f, 0.7f);
                Object.Destroy(brazier.GetComponent<Collider>());
                brazier.GetComponent<MeshRenderer>().material.color = new Color(0.25f, 0.2f, 0.2f);

                var flame = new GameObject("Flame");
                flame.transform.SetParent(brazier.transform, false);
                flame.transform.localPosition = Vector3.up * 1.2f;
                var sr = flame.AddComponent<SpriteRenderer>();
                sr.sprite = PixelArt.FireFrames()[i % 2];
                flame.transform.localScale = new Vector3(0.9f, 1.6f, 0.9f);
                flame.AddComponent<FaceCamera>();

                var lightGo = new GameObject("BrazierLight");
                lightGo.transform.SetParent(root, false);
                lightGo.transform.localPosition = pos + Vector3.up * 2f;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.6f, 0.3f);
                light.range = 9f;
                light.intensity = 1.6f;
            }
        }

        public static void SetupLighting()
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.85f, 0.82f, 1f);
            sun.intensity = 0.75f;
            sun.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.33f, 0.45f);
        }
    }

    /// <summary>Simple billboard for decoration sprites.</summary>
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = CameraRig.MainCam;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
