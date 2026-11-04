using System.Collections.Generic;
using SonTob.Core;
using UnityEngine;

namespace SonTob.Game
{
    /// <summary>
    /// 화면 전체. 규칙은 SonTob.Core에 있고, 여기서는 그리기와 입력만 합니다.
    /// 씬에 아무것도 없어도 Bootstrap이 이 컴포넌트를 만듭니다.
    /// </summary>
    public class GameView : MonoBehaviour
    {
        enum Mode { Title, Interlude, Playing, End }

        // 색 (낮 / 밤)
        static readonly Color DayGround = new Color(0.86f, 0.80f, 0.66f);
        static readonly Color DayWall = new Color(0.55f, 0.47f, 0.40f);
        static readonly Color NightGround = new Color(0.20f, 0.23f, 0.33f);
        static readonly Color NightWall = new Color(0.12f, 0.14f, 0.22f);
        static readonly Color DogHoleCol = new Color(0.35f, 0.30f, 0.25f);
        static readonly Color WellCol = new Color(0.45f, 0.65f, 0.85f);
        static readonly Color HoleCol = new Color(0.08f, 0.06f, 0.05f);
        static readonly Color SightCol = new Color(1f, 0.62f, 0.20f, 0.42f);
        static readonly Color RealSightCol = new Color(0.95f, 0.20f, 0.20f, 0.45f);
        static readonly Color HearCol = new Color(0.75f, 0.35f, 0.90f, 0.30f);
        static readonly Color WalkCol = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color RunCol = new Color(0.35f, 0.60f, 1f, 0.60f);
        static readonly Color RiskCol = new Color(1f, 0.15f, 0.15f, 0.70f);
        static readonly Color ActCol = new Color(1f, 0.85f, 0.2f, 0.9f);
        static readonly Color[] PersonCol =
        {
            new Color(0.20f, 0.45f, 0.85f), // 돌쇠 아빠
            new Color(0.92f, 0.45f, 0.30f), // 분이
            new Color(0.20f, 0.65f, 0.45f), // 훈장 어른
        };

        Mode mode = Mode.Title;
        GameConfig config;
        GameRun run;
        int seed;
        Phase shownPhase;
        int shownDay;

        Camera cam;
        Transform staticRoot, dynamicRoot;
        bool zoomOut;
        Vector3 camTarget;

        string interludeTitle, interludeBody;
        readonly List<string> toasts = new List<string>();
        float toastUntil;

        // OnGUI
        float uiScale = 1f;
        GUIStyle sTitle, sBody, sSmall, sButton, sBig;
        Font font;
        readonly List<Rect> hudRects = new List<Rect>();
        Texture2D white;

        void Awake()
        {
            Application.targetFrameRate = 60;
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0, -2.75f, -10);
            staticRoot = new GameObject("Map").transform;
            dynamicRoot = new GameObject("Pieces").transform;
            font = Resources.Load<Font>("Fonts/HUD");
            white = Texture2D.whiteTexture;
        }

        // ───────────────────────── 판 진행

        void StartRun(GameConfig c)
        {
            config = c;
            seed = System.Environment.TickCount & 0x7fffffff;
            run = new GameRun(config, seed);
            shownPhase = run.Phase; shownDay = run.Day;
            BuildStatic();
            ShowInterlude();
        }

        void ShowInterlude()
        {
            if (run.Phase == Phase.Night)
            {
                interludeTitle = "밤 " + run.Day;
                interludeBody = "쥐구멍에서 나와 손톱 하나를 먹고 돌아오세요.\n보라색은 고양이 잠귀 범위. 그 안에서 뛰거나 손톱을 줍거나 파면 귀가 쫑긋해요. 두 번이면 깹니다.";
            }
            else
            {
                var d = run.DayState;
                interludeTitle = "낮 " + run.Day + " · " + d.As.Name + "(으)로 둔갑";
                interludeBody = "임무 셋을 " + d.BeatLimit + "박자 안에 끝내세요.\n1칸을 누르면 걷기, 2칸을 누르면 네 발로 뛰기. 붉은 칸으로 뛰면 들킵니다.\n진짜 " + d.As.Name + "의 눈에 띄면 걸어도 의심이 올라요.";
            }
            mode = Mode.Interlude;
            Refresh();
        }

        void Play(Move m)
        {
            run.Apply(m);
            Toast(run.LastEvents);
            if (run.Phase == Phase.Over) { mode = Mode.End; Refresh(); return; }
            if (run.Phase != shownPhase || run.Day != shownDay)
            {
                shownPhase = run.Phase; shownDay = run.Day;
                BuildStatic();
                ShowInterlude();
                return;
            }
            Refresh();
        }

        void Toast(List<string> lines)
        {
            if (lines == null || lines.Count == 0) return;
            toasts.Clear(); toasts.AddRange(lines);
            toastUntil = Time.time + 2.2f;
        }

        // ───────────────────────── 좌표

        static Vector3 ToWorld(Pos p, float lift = 0f)
        {
            return new Vector3((p.X - p.Y) * 0.5f, -(p.X + p.Y) * 0.25f + lift, 0f);
        }

        static Pos FromWorld(Vector3 w)
        {
            float fx = w.x - 2f * w.y;
            float fy = -2f * w.y - w.x;
            return new Pos(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy));
        }

        static int Order(Pos p, int layer) { return (p.X + p.Y) * 20 + layer; }

        static Vector3 ScreenVector(Dir d)
        {
            switch (d)
            {
                case Dir.PlusX: return new Vector3(0.5f, -0.25f, 0);
                case Dir.MinusX: return new Vector3(-0.5f, 0.25f, 0);
                case Dir.PlusY: return new Vector3(-0.5f, -0.25f, 0);
                default: return new Vector3(0.5f, 0.25f, 0);
            }
        }

        SpriteRenderer Put(Transform parent, Sprite s, Vector3 pos, Color c, int order, float scale = 1f)
        {
            var go = new GameObject(s.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s; r.color = c; r.sortingOrder = order;
            return r;
        }

        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }

        // ───────────────────────── 그리기

        void BuildStatic()
        {
            Clear(staticRoot);
            var map = run.Map;
            bool night = run.Phase == Phase.Night;
            cam.backgroundColor = night ? new Color(0.06f, 0.07f, 0.12f) : new Color(0.93f, 0.91f, 0.84f);
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var p = new Pos(x, y);
                    var tile = map.TileAt(p);
                    char mark = map.MarkAt(p);
                    if (tile == Tile.Wall && mark != Village.Cat)
                    {
                        Put(staticRoot, Shapes.Block, ToWorld(p), night ? NightWall : DayWall, Order(p, 5));
                        continue;
                    }
                    var ground = night ? NightGround : DayGround;
                    if (tile == Tile.DogHole) ground = DogHoleCol;
                    if (mark == Village.Well) ground = WellCol;
                    Put(staticRoot, Shapes.Diamond, ToWorld(p), ground, Order(p, 0));
                    if (mark == Village.RatHole) Put(staticRoot, Shapes.Circle, ToWorld(p), HoleCol, Order(p, 1), 0.35f);
                    if (tile == Tile.DogHole) Put(staticRoot, Shapes.Circle, ToWorld(p), new Color(0.1f, 0.08f, 0.06f), Order(p, 1), 0.28f);
                }
        }

        void Refresh()
        {
            Clear(dynamicRoot);
            if (run == null) return;
            if (run.Phase == Phase.Night || (run.Phase == Phase.Over && run.DayState == null)) DrawNight(run.Night);
            else if (run.DayState != null) DrawDay(run.DayState);
        }

        void DrawCandidates(List<Move> moves, Move act, Pos player)
        {
            if (mode != Mode.Playing) return;
            foreach (var m in moves)
            {
                var c = m.Risky ? RiskCol : (m.Kind == MoveKind.Run ? RunCol : WalkCol);
                Put(dynamicRoot, Shapes.DiamondEdge, ToWorld(m.Target), c, Order(m.Target, 3), 0.92f);
                Put(dynamicRoot, Shapes.Diamond, ToWorld(m.Target), new Color(c.r, c.g, c.b, c.a * 0.35f), Order(m.Target, 2), 0.8f);
            }
            Put(dynamicRoot, Shapes.DiamondEdge, ToWorld(player), act.Kind == MoveKind.Act ? ActCol : new Color(1, 1, 1, 0.25f), Order(player, 3), 0.92f);
        }

        void DrawDay(DayPhase d)
        {
            var map = d.Map;
            // 임무 칸
            for (int i = 0; i < d.As.Missions.Length; i++)
                foreach (var p in map.Marks(d.As.Missions[i]))
                {
                    var c = d.MissionDone(i) ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : PersonCol[(int)d.As.Id];
                    Put(dynamicRoot, Shapes.Diamond, ToWorld(p), new Color(c.r, c.g, c.b, 0.75f), Order(p, 1), 0.7f);
                }
            // 시선
            var realSight = d.RealSelfSight();
            foreach (var p in d.WatchedTiles())
                Put(dynamicRoot, Shapes.Diamond, ToWorld(p), realSight.Contains(p) ? RealSightCol : SightCol, Order(p, 2), 0.96f);

            DrawCandidates(d.Candidates(), d.ActHere(), d.Player);

            // 사람들
            foreach (var n in d.Npcs)
            {
                Color c = new Color(0.45f, 0.42f, 0.40f);
                if (n.Spec.RealPerson.HasValue) c = PersonCol[(int)n.Spec.RealPerson.Value] * 0.75f + Color.black * 0.25f;
                var body = Put(dynamicRoot, Shapes.Circle, ToWorld(n.Pos, 0.28f), c, Order(n.Pos, 10), 0.42f);
                if (n.Spec.RealPerson.HasValue && n.Spec.RealPerson.Value == d.As.Id)
                    Put(dynamicRoot, Shapes.Ring, ToWorld(n.Pos, 0.28f), new Color(0.95f, 0.15f, 0.15f), Order(n.Pos, 11), 0.5f);
                // 다음 박자에 볼 방향
                var next = n.NextFacing();
                var arrow = Put(dynamicRoot, Shapes.Arrow, ToWorld(n.Pos, 0.62f) + ScreenVector(next) * 0.25f, new Color(0.15f, 0.12f, 0.1f, 0.85f), Order(n.Pos, 12), 0.45f);
                var v = ScreenVector(next);
                arrow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
                body.name = n.Name;
            }

            // 나(둔갑한 쥐)
            Put(dynamicRoot, Shapes.Circle, ToWorld(d.Player, 0.30f), PersonCol[(int)d.As.Id], Order(d.Player, 13), 0.46f);
            Put(dynamicRoot, Shapes.Ring, ToWorld(d.Player, 0.30f), new Color(1f, 0.95f, 0.5f), Order(d.Player, 14), 0.56f);
            camTarget = ToWorld(d.Player);
        }

        void DrawNight(NightPhase n)
        {
            var map = n.Map;
            // 잠귀 범위
            if (!n.CatAwake)
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                    {
                        var p = new Pos(x, y);
                        if (map.TileAt(p) == Tile.Wall) continue;
                        if (n.InHearing(p)) Put(dynamicRoot, Shapes.Diamond, ToWorld(p), HearCol, Order(p, 2), 0.96f);
                    }
            // 손톱
            if (n.Eaten == null)
                foreach (var nail in n.Nails)
                {
                    Color c;
                    if (nail.Method == NailMethod.Throw) c = new Color(1f, 0.97f, 0.75f);
                    else if (nail.Method == NailMethod.Bury) c = nail.Dug ? new Color(0.95f, 0.9f, 0.7f) : new Color(0.55f, 0.42f, 0.3f);
                    else c = new Color(1f, 0.55f, 0.2f);
                    Put(dynamicRoot, Shapes.Circle, ToWorld(nail.Tile, 0.05f), c, Order(nail.Tile, 4), 0.22f);
                    Put(dynamicRoot, Shapes.Ring, ToWorld(nail.Tile, 0.05f), PersonCol[(int)nail.Owner], Order(nail.Tile, 4), 0.34f);
                }

            DrawCandidates(n.Candidates(), n.ActHere(), n.Player);

            // 고양이
            var catCol = n.CatAwake ? new Color(0.95f, 0.25f, 0.3f) : new Color(0.65f, 0.45f, 0.8f, 0.9f);
            Put(dynamicRoot, Shapes.Circle, ToWorld(n.Cat, 0.25f), catCol, Order(n.Cat, 10), n.CatAwake ? 0.5f : 0.42f);
            if (n.Twitches > 0 && !n.CatAwake)
                Put(dynamicRoot, Shapes.Ring, ToWorld(n.Cat, 0.25f), new Color(1f, 0.85f, 0.2f), Order(n.Cat, 11), 0.55f);

            // 쥐
            Put(dynamicRoot, Shapes.Circle, ToWorld(n.Player, 0.15f), new Color(0.62f, 0.60f, 0.58f), Order(n.Player, 13), 0.30f);
            if (n.Eaten != null)
                Put(dynamicRoot, Shapes.Ring, ToWorld(n.Player, 0.15f), PersonCol[(int)n.Eaten.Owner], Order(n.Player, 14), 0.40f);
            camTarget = ToWorld(n.Player);
        }

        // ───────────────────────── 입력과 카메라

        void Update()
        {
            FitCamera();
            if (mode != Mode.Playing || run == null || run.Phase == Phase.Over) return;

            if (Input.GetMouseButtonDown(0) && !PointerOverHud(Input.mousePosition))
            {
                var w = cam.ScreenToWorldPoint(Input.mousePosition);
                TapTile(FromWorld(w));
            }
            HandleKeys();
        }

        void TapTile(Pos p)
        {
            List<Move> moves; Move act; Pos player;
            if (run.Phase == Phase.Night) { moves = run.Night.Candidates(); act = run.Night.ActHere(); player = run.Night.Player; }
            else { moves = run.DayState.Candidates(); act = run.DayState.ActHere(); player = run.DayState.Player; }
            if (p == player) { Play(act); return; }
            foreach (var m in moves) if (m.Target == p) { Play(m); return; }
        }

        void HandleKeys()
        {
            Dir? dir = null;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.E)) dir = Dir.MinusY;   // ↗
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dir = Dir.PlusX; // ↘
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Z)) dir = Dir.PlusY;  // ↙
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q)) dir = Dir.MinusX; // ↖
            if (Input.GetKeyDown(KeyCode.Space)) { TapTile(run.Phase == Phase.Night ? run.Night.Player : run.DayState.Player); return; }
            if (!dir.HasValue) return;
            bool runKey = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var from = run.Phase == Phase.Night ? run.Night.Player : run.DayState.Player;
            TapTile(from + dir.Value.Step() * (runKey ? 2 : 1));
        }

        void FitCamera()
        {
            float aspect = Mathf.Max(0.3f, cam.aspect);
            // 가까이: 마름모 7칸쯤이 가로에 들어오게. 지도 보기: 마을 전체.
            float near = Mathf.Max(2.4f, 3.6f / aspect);
            float far = Mathf.Max(3.6f, 6.6f / aspect);
            float size = (zoomOut || mode != Mode.Playing) ? far : near;
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, 0.2f);
            var center = (zoomOut || mode != Mode.Playing) ? new Vector3(0, -2.75f, 0) : camTarget;
            var target = new Vector3(center.x, center.y - 0.4f, -10f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, 0.2f);
        }

        bool PointerOverHud(Vector3 mouse)
        {
            var gp = new Vector2(mouse.x / uiScale, (Screen.height - mouse.y) / uiScale);
            foreach (var r in hudRects) if (r.Contains(gp)) return true;
            return false;
        }

        // ───────────────────────── 글자 화면 (OnGUI)

        void Styles()
        {
            if (sBody != null) return;
            sBody = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, richText = true };
            sBody.normal.textColor = new Color(0.12f, 0.1f, 0.08f);
            sSmall = new GUIStyle(sBody) { fontSize = 13 };
            sTitle = new GUIStyle(sBody) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sBig = new GUIStyle(sBody) { fontSize = 19, fontStyle = FontStyle.Bold };
            sButton = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            if (font != null) { sBody.font = sSmall.font = sTitle.font = sBig.font = sButton.font = font; }
        }

        Rect Panel(Rect r, Color c)
        {
            hudRects.Add(r);
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
            return r;
        }

        void Bar(Rect r, float v, float max, Color c, string label)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.12f); GUI.DrawTexture(r, white);
            GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v / max), r.height), white);
            GUI.color = old;
            GUI.Label(new Rect(r.x + 6, r.y - 1, r.width, r.height + 2), label + " " + Mathf.RoundToInt(v), sSmall);
        }

        void OnGUI()
        {
            Styles();
            hudRects.Clear();
            uiScale = Mathf.Max(1f, Mathf.Min(Screen.width / 400f, Screen.height / 720f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            float W = Screen.width / uiScale, H = Screen.height / uiScale;
            var paper = new Color(0.98f, 0.96f, 0.90f, 0.94f);

            if (mode == Mode.Title)
            {
                var r = Panel(new Rect(16, H * 0.12f, W - 32, 420), paper);
                GUILayout.BeginArea(new Rect(r.x + 16, r.y + 14, r.width - 32, r.height - 28));
                GUILayout.Label("손톱 먹은 쥐", sTitle);
                GUILayout.Space(8);
                GUILayout.Label("1. 밤에 쥐가 되어 사람이 버린 손톱을 먹는다. 고양이를 깨우면 안 된다.\n2. 낮에는 그 사람으로 둔갑해, 정해진 박자 안에 그 사람의 일을 모두 한다.\n3. 한 칸씩 걸으면 안전하다. 네 발로 뛰면 두 칸이지만 시선에 걸리면 의심이 오른다.\n4. 의심이 가득 차거나, 고양이에게 잡히거나, 허기가 바닥나면 쥐불놀이로 끝난다.\n5. 대보름 밤(3일째)까지 버티면 그 사람의 자리를 차지한다.", sBody);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("쉬움", sButton, GUILayout.Height(44))) StartRun(GameConfig.Easy());
                if (GUILayout.Button("보통", sButton, GUILayout.Height(44))) StartRun(GameConfig.Normal());
                if (GUILayout.Button("어려움", sButton, GUILayout.Height(44))) StartRun(GameConfig.Hard());
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            if (mode == Mode.Interlude)
            {
                var r = Panel(new Rect(16, H * 0.30f, W - 32, 230), paper);
                GUILayout.BeginArea(new Rect(r.x + 16, r.y + 14, r.width - 32, r.height - 28));
                GUILayout.Label(interludeTitle, sBig);
                GUILayout.Space(6);
                GUILayout.Label(interludeBody, sBody);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("시작", sButton, GUILayout.Height(44))) { mode = Mode.Playing; Refresh(); }
                GUILayout.EndArea();
                return;
            }

            if (mode == Mode.End)
            {
                var r = Panel(new Rect(16, H * 0.25f, W - 32, 260), paper);
                GUILayout.BeginArea(new Rect(r.x + 16, r.y + 14, r.width - 32, r.height - 28));
                GUILayout.Label(run.Ended == EndReason.Win ? "대보름까지 버텼다" : "쥐불놀이", sTitle);
                GUILayout.Space(6);
                GUILayout.Label(EndText(run.Ended), sBody);
                GUILayout.Label("판 번호 " + seed + " · " + run.Day + "일째", sSmall);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("다시 하기", sButton, GUILayout.Height(44))) StartRun(config);
                if (GUILayout.Button("난이도 고르기", sButton, GUILayout.Height(44))) { mode = Mode.Title; run = null; Clear(dynamicRoot); Clear(staticRoot); }
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            DrawHud(W, H, paper);
        }

        void DrawHud(float W, float H, Color paper)
        {
            bool night = run.Phase == Phase.Night;
            var top = Panel(new Rect(8, 8, W - 16, night ? 112 : 150), paper);
            float x = top.x + 10, y = top.y + 6, w = top.width - 20;
            string head = night ? "밤 " + run.Day + " · 손톱 하나 먹고 쥐구멍으로" : "낮 " + run.Day + " · " + run.DayState.As.Name + "(으)로 둔갑";
            GUI.Label(new Rect(x, y, w, 24), head, sBig); y += 28;
            Bar(new Rect(x, y, w / 2 - 4, 18), run.Hunger, config.MaxHunger, new Color(0.85f, 0.6f, 0.2f), "허기");
            Bar(new Rect(x + w / 2 + 4, y, w / 2 - 4, 18), run.Suspicion, config.MaxSuspicion, new Color(0.85f, 0.25f, 0.2f), "의심");
            y += 24;
            if (night)
            {
                var n = run.Night;
                string cat = n.CatAwake ? "<color=#c0392b><b>고양이가 깼다! 쥐구멍으로!</b></color>" : "고양이 쫑긋 " + n.Twitches + "/" + config.TwitchesToWake;
                GUI.Label(new Rect(x, y, w, 20), cat + " · 냄새 고리 " + n.ScentRing + "칸", sSmall); y += 20;
                GUI.Label(new Rect(x, y, w, 40), NailLine(n), sSmall);
            }
            else
            {
                var d = run.DayState;
                GUI.Label(new Rect(x, y, w, 20), "남은 박자 <b>" + d.BeatsLeft + "</b>/" + d.BeatLimit + " · 냄새 고리 " + d.ScentRing + "칸", sSmall); y += 22;
                for (int i = 0; i < d.As.Missions.Length; i++)
                {
                    string mark = d.MissionDone(i) ? "■ " : "□ ";
                    string prog = d.MissionDone(i) ? "" : " (" + d.WorkDone[i] + "/" + config.WorkBeatsPerMission + ")";
                    GUI.Label(new Rect(x, y, w, 20), mark + d.As.MissionNames[i] + prog, sSmall); y += 19;
                }
            }

            // 아래 버튼
            float bh = 52;
            var bottom = Panel(new Rect(8, H - bh - 16, W - 16, bh + 8), new Color(0, 0, 0, 0));
            string act = night ? run.Night.ActLabel() : run.DayState.ActLabel();
            if (GUI.Button(new Rect(bottom.x, bottom.y, bottom.width * 0.62f, bh), act, sButton))
                Play(night ? run.Night.ActHere() : run.DayState.ActHere());
            if (GUI.Button(new Rect(bottom.x + bottom.width * 0.64f, bottom.y, bottom.width * 0.36f, bh), zoomOut ? "가까이" : "지도 보기", sButton))
                zoomOut = !zoomOut;

            if (Time.time < toastUntil && toasts.Count > 0)
            {
                var tr = new Rect(16, H - bh - 30 - 22 * toasts.Count, W - 32, 22 * toasts.Count + 8);
                var old = GUI.color; GUI.color = new Color(0.1f, 0.08f, 0.06f, 0.8f); GUI.DrawTexture(tr, white); GUI.color = old;
                var st = new GUIStyle(sSmall); st.normal.textColor = Color.white;
                for (int i = 0; i < toasts.Count; i++) GUI.Label(new Rect(tr.x + 8, tr.y + 4 + 22 * i, tr.width - 16, 22), toasts[i], st);
            }
        }

        static string NailLine(NightPhase n)
        {
            if (n.Eaten != null) return Village.Get(n.Eaten.Owner).Name + "의 손톱을 먹었다. 쥐구멍으로 돌아가면 아침.";
            var parts = new List<string>();
            foreach (var nail in n.Nails)
            {
                string how = nail.Method == NailMethod.Throw ? "던짐" : nail.Method == NailMethod.Bury ? (nail.Dug ? "파냄" : "묻음") : "태움(아궁이)";
                parts.Add(Village.Get(nail.Owner).Name + " " + how);
            }
            return "손톱: " + string.Join(" · ", parts.ToArray());
        }

        static string EndText(EndReason r)
        {
            switch (r)
            {
                case EndReason.Win: return "대보름 밤, 쥐는 그 사람의 자리를 차지했다.";
                case EndReason.Suspicion: return "마을 사람들이 눈치챘다. 의심 100.";
                case EndReason.Hunger: return "허기를 못 견디고 쓰러졌다.";
                case EndReason.BeatsOut: return "임무를 다 못 했는데 둔갑이 풀려 버렸다.";
                case EndReason.CaughtByCat: return "깬 고양이에게 잡혔다.";
                default: return "";
            }
        }
    }
}
