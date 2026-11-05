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

        public static Sprite Diamond { get { return diamond ?? (diamond = MakeDiamond(false)); } }
        public static Sprite DiamondEdge { get { return diamondEdge ?? (diamondEdge = MakeDiamond(true)); } }
        public static Sprite Block { get { return block ?? (block = MakeBlock()); } }
        public static Sprite Circle { get { return circle ?? (circle = MakeCircle(false)); } }
        public static Sprite Ring { get { return ring ?? (ring = MakeCircle(true)); } }
        public static Sprite Arrow { get { return arrow ?? (arrow = MakeArrow()); } }

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
    }
}
