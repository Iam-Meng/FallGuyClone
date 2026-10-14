using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace FallGuyClone
{
    /// <summary>Uses the provided art and sound assets when available, then falls back to generated versions.</summary>
    public class GameFeedback : MonoBehaviour
    {
        const int SampleRate = 22050;
        const string SoundsFolder = "Sounds";
        const string DocImagesFolder = "Docs/Images";

        static Sprite catSprite;

        AudioSource source;
        AudioClip clang;
        AudioClip catLaugh;
        PlayerController player;

        public static Sprite CatSprite
        {
            get
            {
                if (catSprite == null) catSprite = LoadCatSprite();
                return catSprite;
            }
        }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.65f;
            clang = LoadAudioClipOrFallback("metal-pipe-clang.mp3", CreateClang);
            catLaugh = LoadAudioClipOrFallback("cat-laughing-at-you.mp3", CreateCatLaugh);
        }

        public void Bind(PlayerController controller)
        {
            if (player != null) player.Collided -= PlayClang;
            player = controller;
            if (player != null) player.Collided += PlayClang;
        }

        void OnDestroy()
        {
            if (player != null) player.Collided -= PlayClang;
        }

        public void PlayClang()
        {
            source.PlayOneShot(clang);
        }

        public void PlayCatLaugh()
        {
            source.PlayOneShot(catLaugh);
        }

        static AudioClip LoadAudioClipOrFallback(string fileName, System.Func<AudioClip> fallback)
        {
            var clip = TryLoadAudioClip(fileName);
            return clip != null ? clip : fallback();
        }

        static AudioClip TryLoadAudioClip(string fileName)
        {
            var root = GetProjectRoot();
            string path = Path.Combine(root, SoundsFolder, fileName);
            if (!File.Exists(path)) return null;

            try
            {
                using var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.MPEG);
                request.SendWebRequest();
                while (!request.isDone)
                {
                    // Keep the file load synchronous at startup so the audio is ready immediately.
                }

                if (request.result != UnityWebRequest.Result.Success)
                    return null;

                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                    return null;

                clip.name = fileName;
                return clip;
            }
            catch
            {
                return null;
            }
        }

        static Sprite LoadCatSprite()
        {
            foreach (string candidate in new[]
            {
                Path.Combine(GetProjectRoot(), DocImagesFolder, "menu.jpg"),
                Path.Combine(GetProjectRoot(), DocImagesFolder, "gameplay.jpg"),
                Path.Combine(GetProjectRoot(), DocImagesFolder, "course.jpg"),
                Path.Combine(GetProjectRoot(), DocImagesFolder, "finish.jpg")
            })
            {
                if (!File.Exists(candidate)) continue;
                var bytes = File.ReadAllBytes(candidate);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                {
                    Object.Destroy(texture);
                    continue;
                }

                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = Path.GetFileName(candidate);
                return sprite;
            }

            return CreateCatSprite();
        }

        static string GetProjectRoot() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static AudioClip CreateClang()
        {
            const float duration = 0.55f;
            int count = Mathf.CeilToInt(SampleRate * duration);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-7f * t) * (1f - Mathf.Exp(-100f * t));
                float sweep = Mathf.Exp(-1.8f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * 1040f * sweep * t)
                    + 0.62f * Mathf.Sin(2f * Mathf.PI * 2190f * sweep * t)
                    + 0.28f * Mathf.Sin(2f * Mathf.PI * 3470f * sweep * t);
                samples[i] = tone * envelope * 0.28f;
            }
            return CreateClip("Cartoon Clang", samples);
        }

        static AudioClip CreateCatLaugh()
        {
            float[] notes = { 660f, 820f, 710f, 930f, 760f, 1010f, 680f };
            const float syllableLength = 0.18f;
            const float gap = 0.055f;
            float duration = notes.Length * (syllableLength + gap);
            int count = Mathf.CeilToInt(SampleRate * duration);
            var samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                int note = Mathf.FloorToInt(t / (syllableLength + gap));
                if (note >= notes.Length) continue;
                float local = t - note * (syllableLength + gap);
                if (local >= syllableLength) continue;

                float progress = local / syllableLength;
                float startPitch = notes[note] * 1.35f;
                float endPitch = notes[note] * 0.78f;
                float phase = 2f * Mathf.PI * (startPitch * local
                    + (endPitch - startPitch) * local * progress * 0.5f);
                float envelope = Mathf.Min(1f, local * 55f) * Mathf.Exp(-7f * local);
                float meow = Mathf.Sin(phase) + 0.42f * Mathf.Sin(phase * 2f)
                    + 0.18f * Mathf.Sin(phase * 3f);
                samples[i] = meow * envelope * 0.3f;
            }
            return CreateClip("Laughing Cat", samples);
        }

        static AudioClip CreateClip(string clipName, float[] samples)
        {
            var clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static Sprite CreateCatSprite()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Laughing Cat Illustration",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            Color32 outline = new Color32(67, 42, 73, 255);
            Color32 fur = new Color32(255, 170, 92, 255);
            Color32 face = new Color32(255, 239, 208, 255);
            Color32 pink = new Color32(244, 113, 147, 255);

            DrawTriangle(pixels, size, new Vector2(40, 158), new Vector2(54, 244), new Vector2(120, 193), fur);
            DrawTriangle(pixels, size, new Vector2(136, 193), new Vector2(202, 244), new Vector2(216, 158), fur);
            DrawTriangle(pixels, size, new Vector2(59, 178), new Vector2(63, 218), new Vector2(98, 190), pink);
            DrawTriangle(pixels, size, new Vector2(158, 190), new Vector2(193, 218), new Vector2(197, 178), pink);
            DrawCircle(pixels, size, 128, 124, 101, fur);
            DrawCircle(pixels, size, 128, 120, 89, face);

            DrawLine(pixels, size, 76, 148, 92, 133, outline, 5);
            DrawLine(pixels, size, 92, 133, 108, 148, outline, 5);
            DrawLine(pixels, size, 148, 148, 164, 133, outline, 5);
            DrawLine(pixels, size, 164, 133, 180, 148, outline, 5);
            DrawCircle(pixels, size, 128, 105, 8, pink);
            DrawEllipse(pixels, size, 128, 77, 22, 27, outline);
            DrawEllipse(pixels, size, 128, 66, 12, 10, pink);

            DrawLine(pixels, size, 59, 113, 22, 122, outline, 3);
            DrawLine(pixels, size, 60, 99, 20, 99, outline, 3);
            DrawLine(pixels, size, 197, 113, 234, 122, outline, 3);
            DrawLine(pixels, size, 196, 99, 236, 99, outline, 3);

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static void DrawCircle(Color32[] pixels, int size, int centerX, int centerY, int radius, Color32 color)
        {
            DrawEllipse(pixels, size, centerX, centerY, radius, radius, color);
        }

        static void DrawEllipse(Color32[] pixels, int size, int centerX, int centerY, int radiusX, int radiusY, Color32 color)
        {
            for (int y = Mathf.Max(0, centerY - radiusY); y <= Mathf.Min(size - 1, centerY + radiusY); y++)
            for (int x = Mathf.Max(0, centerX - radiusX); x <= Mathf.Min(size - 1, centerX + radiusX); x++)
            {
                float dx = (x - centerX) / (float)radiusX;
                float dy = (y - centerY) / (float)radiusY;
                if (dx * dx + dy * dy <= 1f) pixels[y * size + x] = color;
            }
        }

        static void DrawTriangle(Color32[] pixels, int size, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x, y);
                float ab = Cross(b - a, p - a);
                float bc = Cross(c - b, p - b);
                float ca = Cross(a - c, p - c);
                if ((ab >= 0f && bc >= 0f && ca >= 0f) || (ab <= 0f && bc <= 0f && ca <= 0f))
                    pixels[y * size + x] = color;
            }
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static void DrawLine(Color32[] pixels, int size, int x0, int y0, int x1, int y1, Color32 color, int width)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                DrawCircle(pixels, size, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)),
                    Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), width, color);
            }
        }
    }
}
