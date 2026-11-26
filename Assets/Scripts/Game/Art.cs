using System.Collections.Generic;
using UnityEngine;

namespace SonTob.Game
{
    /// <summary>
    /// Resources/Art의 그림 파일을 스프라이트로 바꿔 줍니다(tools/make_art.py가 그림).
    /// 파일이 없으면 null을 돌려주고, 부르는 쪽이 예전 도형 그림으로 대신합니다.
    /// 1유닛 = 64픽셀이라 64 × 32 그림이 마름모 칸 하나입니다.
    /// </summary>
    public static class Art
    {
        const float PixelsPerUnit = 64f;
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static Texture2D Tex(string name)
        {
            Texture2D t;
            if (!textures.TryGetValue(name, out t))
            {
                t = Resources.Load<Texture2D>("Art/" + name);
                textures[name] = t;
            }
            return t;
        }

        /// <summary>pivotX · pivotY는 그림 안의 비율(0~1, 왼쪽 아래가 0). 발 밑에 놓으려면 (0.5, 0.08).</summary>
        public static Sprite Get(string name, float pivotX = 0.5f, float pivotY = 0.5f)
        {
            string key = name + "|" + pivotX + "|" + pivotY;
            Sprite s;
            if (sprites.TryGetValue(key, out s)) return s;
            var t = Tex(name);
            if (t != null)
            {
                s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(pivotX, pivotY), PixelsPerUnit);
                s.name = name;
            }
            sprites[key] = s;
            return s;
        }
    }
}
