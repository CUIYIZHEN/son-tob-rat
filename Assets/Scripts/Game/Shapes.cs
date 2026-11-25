using UnityEngine;

namespace SonTob.Game
{
    /// <summary>
    /// 미술 파일 없이 쓰는 흰색 도형 스프라이트. 색은 SpriteRenderer.color로 입힙니다.
    /// 칸 하나는 가로 1, 세로 0.5 유닛의 마름모입니다.
    /// </summary>
    public static class Shapes
    {
        const int PPU = 64;
        static Sprite diamond, diamondEdge, block, circle, ring, arrow;
        static Sprite diamondDashed, chevron, droplet, edgeSE, edgeSW, hatch;

        public static Sprite Diamond { get { return diamond ?? (diamond = MakeDiamond(false)); } }
        public static Sprite DiamondEdge { get { return diamondEdge ?? (diamondEdge = MakeDiamond(true)); } }
        public static Sprite Block { get { return block ?? (block = MakeBlock()); } }
        public static Sprite Circle { get { return circle ?? (circle = MakeCircle(false)); } }
        public static Sprite Ring { get { return ring ?? (ring = MakeCircle(true)); } }
        public static Sprite Arrow { get { return arrow ?? (arrow = MakeArrow()); } }
        /// <summary>점선 테두리 마름모: 다음 박자에 볼 칸, 고양이가 다음에 갈 칸.</summary>
        public static Sprite DiamondDashed { get { return diamondDashed ?? (diamondDashed = MakeDashed()); } }
        /// <summary>오른쪽을 향한 꺾쇠(›). 시선 띠 위에 칸마다 하나.</summary>
        public static Sprite Chevron { get { return chevron ?? (chevron = MakeChevron()); } }
        /// <summary>오른쪽이 뾰족한 물방울. 발밑 그림자로 보는 쪽을 알려 줌.</summary>
        public static Sprite Droplet { get { return droplet ?? (droplet = MakeDroplet()); } }
        /// <summary>칸의 남동 변(오른쪽 아래 변)에 붙인 낮은 담.</summary>
        public static Sprite EdgeSE { get { return edgeSE ?? (edgeSE = MakeEdge(true)); } }
        /// <summary>칸의 남서 변(왼쪽 아래 변)에 붙인 낮은 담.</summary>
        public static Sprite EdgeSW { get { return edgeSW ?? (edgeSW = MakeEdge(false)); } }
        /// <summary>빗금 무늬 마름모: 막힌 칸의 바닥(길과 다르게).</summary>
        public static Sprite Hatch { get { return hatch ?? (hatch = MakeHatch()); } }

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            var clear = new Color32[w * h];
            t.SetPixels32(clear);
            return t;
        }

        // 마름모 안쪽 정도: 0 가운데, 1 가장자리
        static float DiamondDist(float px, float py, float w, float h)
        {
            float dx = Mathf.Abs(px - w / 2f) / (w / 2f);
            float dy = Mathf.Abs(py - h / 2f) / (h / 2f);
            return dx + dy;
        }

        static Sprite MakeDiamond(bool edgeOnly)
        {
            int w = 64, h = 32;
            var t = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = DiamondDist(x + 0.5f, y + 0.5f, w, h);
                    if (d > 1f) continue;
                    float a = Mathf.Clamp01((1f - d) * 24f);
                    if (edgeOnly) a *= d > 0.86f ? 1f : 0f;
                    else a *= d > 0.92f ? 0.55f : 1f; // 가장자리를 살짝 어둡게 해서 칸이 보이게
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PPU);
        }

        static Sprite MakeBlock()
        {
            // 위 마름모 + 높이 0.35 유닛의 옆면. 피벗은 바닥 마름모의 가운데.
            int w = 64, top = 32, side = 22, h = top + side;
            var t = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    // 윗면: side만큼 올린 마름모
                    if (DiamondDist(fx, fy - side, w, top) <= 1f) { t.SetPixel(x, y, Color.white); continue; }
                    // 옆면: 바닥 마름모를 0..side만큼 밀어 올린 자취
                    bool body = false;
                    for (int k = 0; k <= side && !body; k++) body = DiamondDist(fx, fy - k, w, top) <= 1f;
                    if (!body) continue;
                    float shade = fx < w / 2f ? 0.78f : 0.62f;
                    t.SetPixel(x, y, new Color(shade, shade, shade, 1));
                }
            t.Apply();
            float pivotY = (top / 2f) / h;
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, pivotY), PPU);
        }

        static Sprite MakeCircle(bool ringOnly)
        {
            int s = 64;
            var t = NewTex(s, s);
            float r = s / 2f - 1;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f));
                    float a = Mathf.Clamp01(r - d);
                    if (ringOnly) a *= Mathf.Clamp01(d - (r - 5f));
                    if (a > 0) t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), PPU);
        }

        static Sprite MakeArrow()
        {
            // 오른쪽을 가리키는 삼각형
            int s = 32;
            var t = NewTex(s, s);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float half = (s - fx) / 2f;
                    if (Mathf.Abs(fy - s / 2f) <= half && fx > 4) t.SetPixel(x, y, Color.white);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), PPU);
        }

        static Sprite MakeDashed()
        {
            int w = 64, h = 32;
            var t = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = DiamondDist(x + 0.5f, y + 0.5f, w, h);
                    if (d > 1f || d < 0.84f) continue;
                    // 가로 8픽셀마다 켜고 끄기
                    if ((x / 6) % 2 == 1) continue;
                    t.SetPixel(x, y, Color.white);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PPU);
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
            return Vector2.Distance(p, a + ab * k);
        }

        static Sprite MakeChevron()
        {
            int s = 32;
            var t = NewTex(s, s);
            var tip = new Vector2(24, 16);
            var top = new Vector2(10, 28);
            var bottom = new Vector2(10, 4);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(SegDist(p, top, tip), SegDist(p, bottom, tip));
                    float a = Mathf.Clamp01(4.5f - d);
                    if (a > 0) t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), PPU);
        }

        static Sprite MakeDroplet()
        {
            int w = 64, h = 32;
            var t = NewTex(w, h);
            var c = new Vector2(24, 16);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    // 둥근 몸통 + 오른쪽으로 좁아지는 꼬리
                    float body = Vector2.Distance(new Vector2(p.x, c.y + (p.y - c.y) * 1.6f), c);
                    bool inside = body <= 14f;
                    if (!inside && p.x > c.x && p.x < 60)
                    {
                        float half = 14f * (60 - p.x) / (60 - c.x) / 1.6f;
                        inside = Mathf.Abs(p.y - c.y) <= half;
                    }
                    if (inside) t.SetPixel(x, y, Color.white);
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PPU);
        }

        static Sprite MakeEdge(bool southEast)
        {
            // 바닥 마름모(64×32) 위에 높이 side만큼 세운 얇은 담. 피벗은 바닥 마름모의 가운데.
            int w = 64, top = 32, side = 18, h = top + side;
            var t = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    bool onSide = southEast ? fx >= 32 : fx <= 32;
                    if (!onSide) continue;
                    float edgeY = southEast ? (fx - 32f) / 2f : (32f - fx) / 2f;
                    float up = fy - edgeY;
                    if (up < 0 || up > side) continue;
                    float shade = up > side - 3 ? 1f : (southEast ? 0.62f : 0.78f);
                    t.SetPixel(x, y, new Color(shade, shade, shade, 1));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, (top / 2f) / h), PPU);
        }

        static Sprite MakeHatch()
        {
            int w = 64, h = 32;
            var t = NewTex(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = DiamondDist(x + 0.5f, y + 0.5f, w, h);
                    if (d > 1f) continue;
                    bool stripe = ((x + 2 * y) / 5) % 2 == 0;
                    float v = stripe ? 1f : 0.82f;
                    t.SetPixel(x, y, new Color(v, v, v, 1));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PPU);
        }
    }
}
