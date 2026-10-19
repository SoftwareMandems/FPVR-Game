using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FPVR
{
    // Dresses the demo scene when it starts: sky, sunlight, ambient light, haze,
    // post-processing (tonemapping, bloom, colour grading) and a mown-grass field.
    // Runs automatically in any scene that contains a TrainingDemo, so nothing needs wiring up.
    public static class SceneVisuals
    {
        const float FieldSize = 600f, StripeWidth = 4f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Object.FindAnyObjectByType<TrainingDemo>() == null) return;
            Sky(); Lighting(); PostProcessing(); Field();
        }

        static void Sky()
        {
            Material source = Resources.Load<Material>("FPVR_Sky");
            Shader shader = source && source.shader.isSupported ? source.shader : Shader.Find("Skybox/Procedural");
            if (!shader) return;
            var sky = new Material(shader) { name = "FPVR Sky" };
            sky.SetFloat("_SunDisk", 2);
            sky.SetFloat("_SunSize", .035f);
            sky.SetFloat("_SunSizeConvergence", 6);
            sky.SetFloat("_AtmosphereThickness", .85f);
            sky.SetFloat("_Exposure", 1.25f);
            sky.SetColor("_SkyTint", new Color(.48f, .52f, .58f));
            sky.SetColor("_GroundColor", new Color(.42f, .48f, .40f));
            RenderSettings.skybox = sky;
            foreach (var cam in Camera.allCameras) cam.clearFlags = CameraClearFlags.Skybox;
        }

        static void Lighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.56f, .68f, .86f);
            RenderSettings.ambientEquatorColor = new Color(.60f, .62f, .58f);
            RenderSettings.ambientGroundColor = new Color(.20f, .24f, .15f);
            RenderSettings.ambientIntensity = 1;
            // Distance haze that matches the sky horizon so the field fades out instead of ending.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.68f, .77f, .86f);
            RenderSettings.fogDensity = .0085f;

            Light sun = RenderSettings.sun;
            if (!sun)
                foreach (var light in Object.FindObjectsByType<Light>())
                    if (light.type == LightType.Directional) { sun = light; break; }
            if (!sun) return;
            // A lower, warmer sun gives long readable shadows across the course.
            sun.transform.rotation = Quaternion.Euler(40, -35, 0);
            sun.color = new Color(1f, .93f, .82f);
            sun.intensity = 1.9f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .85f;
            RenderSettings.sun = sun;
        }

        static void PostProcessing()
        {
            foreach (var cam in Camera.allCameras)
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "FPVR Post";
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f); bloom.intensity.Override(.9f); bloom.scatter.Override(.65f);
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(.35f); grade.contrast.Override(14f); grade.saturation.Override(14f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(.2f); vignette.smoothness.Override(.45f);
            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;
        }

        static void Field()
        {
            Material paint = null;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>())
            {
                string n = r.gameObject.name;
                if (n == "Ground")
                {
                    // Widen the ground so the horizon is grass fading into haze, not the edge of a box.
                    var s = r.transform.localScale;
                    r.transform.localScale = new Vector3(FieldSize, s.y, FieldSize);
                    var grass = new Material(r.sharedMaterial) { name = "Grass" };
                    grass.SetTexture("_BaseMap", GrassTexture(512));
                    grass.SetTextureScale("_BaseMap", Vector2.one * (FieldSize / (StripeWidth * 2)));
                    grass.SetColor("_BaseColor", Color.white);
                    grass.SetFloat("_Smoothness", .06f);
                    r.sharedMaterial = grass;
                }
                else if (n.StartsWith("Grid "))
                {
                    // The grid becomes painted field lines, slightly wider so they don't shimmer at distance.
                    if (!paint)
                    {
                        paint = new Material(r.sharedMaterial) { name = "Field Paint" };
                        paint.SetColor("_BaseColor", new Color(.9f, .93f, .88f));
                        paint.SetFloat("_Smoothness", .1f);
                        paint.DisableKeyword("_EMISSION");
                        paint.SetColor("_EmissionColor", Color.black);
                    }
                    r.sharedMaterial = paint;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    var s = r.transform.localScale;
                    if (s.x < s.z) s.x = .09f; else s.z = .09f;
                    r.transform.localScale = s;
                }
            }
        }

        // Tileable grass: two mowing stripes, soft colour patches and fine grain.
        static Texture2D GrassTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            { name = "Grass", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            var pixels = new Color32[size * size];
            Color dark = new Color(.19f, .34f, .11f), light = new Color(.32f, .50f, .17f), dry = new Color(.47f, .50f, .26f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    Color c = Color.Lerp(dark, light, Fbm(u, v, 4, 11));
                    c = Color.Lerp(c, dry, Mathf.Clamp01((Fbm(u, v, 2, 29) - .55f) * 2.5f) * .45f);
                    c *= u < .5f ? 1.07f : .93f;
                    c *= .88f + .24f * Hash(x, y, size, 53);
                    pixels[y * size + x] = c;
                }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        static float Fbm(float u, float v, int period, int seed)
        {
            float sum = 0, amplitude = .5f, total = 0;
            for (int octave = 0; octave < 5; octave++, period *= 2, amplitude *= .5f)
            {
                sum += amplitude * ValueNoise(u * period, v * period, period, seed + octave);
                total += amplitude;
            }
            return sum / total;
        }

        static float ValueNoise(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(xi, yi, period, seed), b = Hash(xi + 1, yi, period, seed);
            float c = Hash(xi, yi + 1, period, seed), d = Hash(xi + 1, yi + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y, int period, int seed)
        {
            x = (x % period + period) % period; y = (y % period + period) % period;
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }
}
