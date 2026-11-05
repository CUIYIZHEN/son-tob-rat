using UnityEngine;

namespace SonTob.Game
{
    /// <summary>씬에 GameView가 없으면 만들어 줍니다. 빈 씬에서 Play만 눌러도 게임이 뜹니다.</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindFirstObjectByType<GameView>() != null) return;
            new GameObject("Game").AddComponent<GameView>();
        }
    }
}
