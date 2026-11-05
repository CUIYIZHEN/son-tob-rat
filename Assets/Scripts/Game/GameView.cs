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
        static readonly Color DayBlocked = new Color(0.62f, 0.66f, 0.48f);   // 막힌 칸 바닥: 풀숲
        static readonly Color DayWall = new Color(0.55f, 0.47f, 0.40f);
        static readonly Color NightGround = new Color(0.20f, 0.23f, 0.33f);
        static readonly Color NightBlocked = new Color(0.14f, 0.17f, 0.22f);
        static readonly Color NightWall = new Color(0.30f, 0.32f, 0.42f);
        static readonly Color DogHoleCol = new Color(0.35f, 0.30f, 0.25f);
        static readonly Color WellCol = new Color(0.45f, 0.65f, 0.85f);
        static readonly Color HoleCol = new Color(0.08f, 0.06f, 0.05f);
        static readonly Color JarCol = new Color(0.42f, 0.26f, 0.18f);
        static readonly Color SightCol = new Color(1f, 0.70f, 0.15f);
        static readonly Color RealSightCol = new Color(0.92f, 0.18f, 0.18f);
        static readonly Color CatRingCol = new Color(0.75f, 0.35f, 0.90f, 0.30f);
        static readonly Color CatChaseCol = new Color(0.95f, 0.25f, 0.30f, 0.30f);
        static readonly Color CatCol = new Color(0.65f, 0.45f, 0.80f);
        static readonly Color WalkCol = new Color(1f, 1f, 1f, 0.55f);
        static readonly Color RunCol = new Color(0.35f, 0.60f, 1f, 0.60f);
        static readonly Color RiskCol = new Color(1f, 0.15f, 0.15f, 0.70f);
        static readonly Color ActCol = new Color(1f, 0.85f, 0.2f, 0.9f);
        static readonly Color ShadowCol = new Color(0.10f, 0.08f, 0.06f, 0.45f);
        static readonly Color[] PersonCol =
        {
            new Color(0.20f, 0.45f, 0.85f), // 돌쇠 아빠
            new Color(0.92f, 0.45f, 0.30f), // 분이
            new Color(0.20f, 0.65f, 0.45f), // 훈장 어른
        };
        static readonly float[] BandAlpha = { 0.42f, 0.28f, 0.14f }; // 시선 띠: 멀수록 옅게

        const float DoubleTapSeconds = 0.3f;

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

        // 해 뜨고 지는 바뀜(화면 덮개가 서서히 걷힘)
        float fadeStart = -10f;
        Color fadeCol;
        const float FadeSeconds = 1.4f;

        // 두 번 누르기 판정을 기다리는 터치
        Dir? pendingDir;
        float pendingTime;

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

        void StartRun()
        {
            config = new GameConfig();
            seed = System.Environment.TickCount & 0x7fffffff;
            run = new GameRun(config, seed);
            shownPhase = run.Phase; shownDay = run.Day;
            pendingDir = null;
            BuildStatic();
            ShowInterlude(false);
        }

        void ShowInterlude(bool afterDusk)
        {
            var today = run.Today;
            if (run.Phase == Phase.Night)
            {
                interludeTitle = (afterDusk ? "해 질 녘 · " : "") + "밤 " + run.Day;
                interludeBody = (afterDusk ? "사람들이 하나둘 집으로 들어갔어요. 다시 손톱을 찾으러 나갑니다.\n" : "")
                    + "해가 뜨기 전(쥐 시간 " + today.NightBeats + "박자) 손톱 하나를 찾아 먹으세요.\n"
                    + "손톱은 보이지 않아요. " + today.SniffRange + "칸 안에 들어가면 킁킁 냄새로 찾습니다.\n"
                    + "보라색은 고양이 냄새 고리(" + today.CatRing + "칸). 들어가면 쫓아와요. 쥐구멍으로 숨으면 놓칩니다.";
            }
            else
            {
                var d = run.DayState;
                interludeTitle = "해가 떴다 · 낮 " + run.Day + " · " + d.As.Name + "(으)로 둔갑";
                interludeBody = "해가 지기 전(둔갑 시간 " + d.BeatLimit + "박자) 임무 셋을 끝내세요.\n"
                    + "나를 중심으로 가고 싶은 쪽을 한 번 누르면 1칸, 빠르게 두 번 누르면 네 발로 2칸.\n"
                    + "뛰다 노란 시선 띠에 걸리면 의심 +" + config.RunSeenSuspicion + ". 붉은 띠(진짜 " + d.As.Name + ")는 걸어도 +" + config.RealPersonSuspicion + ".";
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
                bool dusk = run.Phase == Phase.Night;
                shownPhase = run.Phase; shownDay = run.Day;
                fadeStart = Time.time;
                fadeCol = dusk ? new Color(0.05f, 0.06f, 0.14f) : new Color(1f, 0.93f, 0.75f);
                pendingDir = null;
                BuildStatic();
                ShowInterlude(dusk);
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

        void Toast(string line) { Toast(new List<string> { line }); }

        // ───────────────────────── 좌표

        static Vector3 ToWorld(Pos p, float lift = 0f)
        {
            return new Vector3((p.X - p.Y) * 0.5f, -(p.X + p.Y) * 0.25f + lift, 0f);
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

        static float Angle(Dir d)
        {
            var v = ScreenVector(d);
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
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

        Pos PlayerPos()
        {
            return run.Phase == Phase.Night ? run.Night.Player : run.DayState.Player;
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
                    if (tile == Tile.Wall)
                    {
                        // 막힌 칸: 길과 다른 무늬의 바닥 + 앞쪽 두 변(남동·남서)에만 낮은 담 (기획서 8장)
                        Put(staticRoot, Shapes.Hatch, ToWorld(p), night ? NightBlocked : DayBlocked, Order(p, 0));
                        if (mark == Village.Cat) { DrawJarStand(p, night); continue; }
                        var wallCol = night ? NightWall : DayWall;
                        if (map.TileAt(p + Dir.PlusX.Step()) != Tile.Wall) Put(staticRoot, Shapes.EdgeSE, ToWorld(p), wallCol, Order(p, 5));
                        if (map.TileAt(p + Dir.PlusY.Step()) != Tile.Wall) Put(staticRoot, Shapes.EdgeSW, ToWorld(p), wallCol, Order(p, 5));
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

        /// <summary>장독대: 낮과 밤 모두 보이는 막힌 칸. 낮에는 고양이가 그 위에서 잡니다.</summary>
        void DrawJarStand(Pos p, bool night)
        {
            var col = night ? JarCol * 0.6f + Color.black * 0.4f : JarCol;
            col.a = 1f;
            Put(staticRoot, Shapes.Circle, ToWorld(p, 0.10f) + new Vector3(-0.16f, 0, 0), col, Order(p, 5), 0.30f);
            Put(staticRoot, Shapes.Circle, ToWorld(p, 0.08f) + new Vector3(0.14f, -0.02f, 0), col, Order(p, 6), 0.34f);
            Put(staticRoot, Shapes.Circle, ToWorld(p, 0.22f), col * 0.9f + Color.white * 0.1f, Order(p, 7), 0.26f);
            if (!night)
            {
                Put(staticRoot, Shapes.Circle, ToWorld(p, 0.40f), CatCol, Order(p, 8), 0.24f);
                Put(staticRoot, Shapes.Circle, ToWorld(p, 0.47f) + new Vector3(0.09f, 0, 0), CatCol, Order(p, 9), 0.14f);
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

        /// <summary>시선 띠: 사람에게서 멀어질수록 옅게, 칸마다 바깥쪽 꺾쇠. 진짜는 붉게, 꺾쇠 두 겹(»).</summary>
        void DrawSightBand(Npc n, MapData map, int length, bool real)
        {
            var col = real ? RealSightCol : SightCol;
            var tiles = n.Sight(map, length);
            float angle = Angle(n.Facing);
            for (int i = 0; i < tiles.Count; i++)
            {
                var p = tiles[i];
                float a = BandAlpha[Mathf.Min(i, BandAlpha.Length - 1)];
                Put(dynamicRoot, Shapes.Diamond, ToWorld(p), new Color(col.r, col.g, col.b, a), Order(p, 2), 0.96f);
                var dir = ScreenVector(n.Facing).normalized;
                var chevCol = new Color(col.r * 0.8f, col.g * 0.6f, col.b * 0.4f, 0.95f);
                var c1 = Put(dynamicRoot, Shapes.Chevron, ToWorld(p) + dir * (real ? 0.05f : 0f), chevCol, Order(p, 4), 0.55f);
                c1.transform.rotation = Quaternion.Euler(0, 0, angle);
                if (real)
                {
                    var c2 = Put(dynamicRoot, Shapes.Chevron, ToWorld(p) - dir * 0.09f, chevCol, Order(p, 4), 0.55f);
                    c2.transform.rotation = Quaternion.Euler(0, 0, angle);
                }
            }
            // 다음 박자에 볼 칸: 점선 테두리
            var now = new HashSet<Pos>(tiles);
            foreach (var p in n.NextSight(map, length))
                if (!now.Contains(p))
                    Put(dynamicRoot, Shapes.DiamondDashed, ToWorld(p), new Color(col.r, col.g, col.b, 0.9f), Order(p, 3), 0.94f);
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
            foreach (var n in d.Npcs)
            {
                bool real = n.Spec.RealPerson.HasValue && n.Spec.RealPerson.Value == d.As.Id;
                DrawSightBand(n, map, d.Config.SightLength, real);
            }

            DrawCandidates(d.Candidates(), d.ActHere(), d.Player);

            // 사람들: 물방울 그림자(뾰족한 쪽 = 보는 쪽) + 몸 + 머리 위 다음 방향 화살표
            foreach (var n in d.Npcs)
            {
                var shadow = Put(dynamicRoot, Shapes.Droplet, ToWorld(n.Pos), ShadowCol, Order(n.Pos, 9), 0.55f);
                shadow.transform.rotation = Quaternion.Euler(0, 0, Angle(n.Facing));
                Color c = new Color(0.45f, 0.42f, 0.40f);
                if (n.Spec.RealPerson.HasValue) c = PersonCol[(int)n.Spec.RealPerson.Value] * 0.75f + Color.black * 0.25f;
                c.a = 1f;
                var body = Put(dynamicRoot, Shapes.Circle, ToWorld(n.Pos, 0.28f), c, Order(n.Pos, 10), 0.42f);
                if (n.Spec.RealPerson.HasValue && n.Spec.RealPerson.Value == d.As.Id)
                    Put(dynamicRoot, Shapes.Ring, ToWorld(n.Pos, 0.28f), RealSightCol, Order(n.Pos, 11), 0.5f);
                var next = n.NextFacing();
                if (next != n.Facing)
                {
                    var arrow = Put(dynamicRoot, Shapes.Arrow, ToWorld(n.Pos, 0.62f) + ScreenVector(next) * 0.25f, new Color(0.15f, 0.12f, 0.1f, 0.85f), Order(n.Pos, 12), 0.45f);
                    arrow.transform.rotation = Quaternion.Euler(0, 0, Angle(next));
                }
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
            // 고양이 냄새 고리(담장으로 막히지 않음)
            int ring = n.Config.CatRing;
            var ringCol = n.CatChasing ? CatChaseCol : CatRingCol;
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var p = new Pos(x, y);
                    if (map.TileAt(p) == Tile.Wall) continue;
                    if (n.InCatRing(p, ring)) Put(dynamicRoot, Shapes.Diamond, ToWorld(p), ringCol, Order(p, 2), 0.96f);
                }
            // 고양이가 다음에 갈 칸
            var catNext = n.CatNextPos();
            if (catNext != n.Cat)
                Put(dynamicRoot, Shapes.DiamondDashed, ToWorld(catNext), n.CatChasing ? RiskCol : new Color(CatCol.r, CatCol.g, CatCol.b, 0.95f), Order(catNext, 3), 0.94f);

            // 냄새로 찾은 손톱만 보임
            if (n.Eaten == null)
                foreach (var nail in n.Nails)
                {
                    if (!nail.Found) continue;
                    Color c;
                    if (nail.Method == NailMethod.Throw) c = new Color(1f, 0.97f, 0.75f);
                    else if (nail.Method == NailMethod.Bury) c = new Color(0.55f, 0.42f, 0.3f);
                    else c = new Color(1f, 0.55f, 0.2f);
                    Put(dynamicRoot, Shapes.Diamond, ToWorld(nail.Tile), new Color(1f, 0.95f, 0.6f, 0.30f), Order(nail.Tile, 2), 0.9f);
                    Put(dynamicRoot, Shapes.Circle, ToWorld(nail.Tile, 0.05f), c, Order(nail.Tile, 4), 0.22f);
                    Put(dynamicRoot, Shapes.Ring, ToWorld(nail.Tile, 0.05f), PersonCol[(int)nail.Owner], Order(nail.Tile, 4), 0.34f);
                }

            DrawCandidates(n.Candidates(), n.ActHere(), n.Player);

            // 고양이
            var catCol = n.CatChasing ? new Color(0.95f, 0.25f, 0.3f) : CatCol;
            var cs = Put(dynamicRoot, Shapes.Droplet, ToWorld(n.Cat), ShadowCol, Order(n.Cat, 9), 0.5f);
            Dir toward;
            if (Dirs.TryFromDelta(catNext - n.Cat, out toward)) cs.transform.rotation = Quaternion.Euler(0, 0, Angle(toward));
            Put(dynamicRoot, Shapes.Circle, ToWorld(n.Cat, 0.25f), catCol, Order(n.Cat, 10), n.CatChasing ? 0.5f : 0.42f);
            Put(dynamicRoot, Shapes.Circle, ToWorld(n.Cat, 0.42f), catCol, Order(n.Cat, 11), 0.24f);

            // 쥐
            Put(dynamicRoot, Shapes.Circle, ToWorld(n.Player, 0.15f), new Color(0.62f, 0.60f, 0.58f), Order(n.Player, 13), 0.30f);
            camTarget = ToWorld(n.Player);
        }

        // ───────────────────────── 입력과 카메라

        void Update()
        {
            FitCamera();
            if (mode != Mode.Playing || run == null || run.Phase == Phase.Over) return;

            // 한 번 누른 뒤 0.3초가 지나도록 두 번째가 없으면 1칸 걷기
            if (pendingDir.HasValue && Time.time - pendingTime > DoubleTapSeconds)
            {
                var d = pendingDir.Value;
                pendingDir = null;
                Step(d, false);
                if (mode != Mode.Playing || run.Phase == Phase.Over) return;
            }

            if (Input.GetMouseButtonDown(0) && !PointerOverHud(Input.mousePosition))
            {
                Dir dir;
                if (QuadrantAt(Input.mousePosition, out dir)) Tap(dir);
            }
            HandleKeys();
        }

        /// <summary>나를 중심으로 가로선·세로선을 그어 화면을 네 구역으로 나눕니다(기획서 1장).</summary>
        bool QuadrantAt(Vector3 mouse, out Dir dir)
        {
            dir = Dir.MinusY;
            var me = cam.WorldToScreenPoint(ToWorld(PlayerPos(), 0.2f));
            float dx = mouse.x - me.x, dy = mouse.y - me.y;
            float dead = 18f * uiScale;
            if (Mathf.Abs(dx) < dead && Mathf.Abs(dy) < dead) return false; // 내 몸은 무시(아래 버튼을 씀)
            if (dx >= 0 && dy >= 0) dir = Dir.MinusY;      // ↗
            else if (dx >= 0) dir = Dir.PlusX;             // ↘
            else if (dy < 0) dir = Dir.PlusY;              // ↙
            else dir = Dir.MinusX;                         // ↖
            return true;
        }

        void Tap(Dir dir)
        {
            if (pendingDir.HasValue && pendingDir.Value == dir && Time.time - pendingTime <= DoubleTapSeconds)
            {
                pendingDir = null;
                Step(dir, true);
                return;
            }
            if (pendingDir.HasValue)
            {
                var first = pendingDir.Value;
                pendingDir = null;
                Step(first, false);
                if (mode != Mode.Playing || run.Phase == Phase.Over) return;
            }
            pendingDir = dir;
            pendingTime = Time.time;
        }

        void Step(Dir dir, bool running)
        {
            var moves = run.Phase == Phase.Night ? run.Night.Candidates() : run.DayState.Candidates();
            var want = running ? MoveKind.Run : MoveKind.Walk;
            foreach (var m in moves)
                if (m.Kind == want && m.Dir == dir) { Play(m); return; }
            if (!running && run.Phase == Phase.Day && run.Map.TileAt(PlayerPos() + dir.Step()) == Tile.DogHole)
                Toast("개구멍은 빠르게 두 번 눌러 네 발로 지나요");
            else
                Toast(running ? "그쪽으로는 두 칸을 뛸 수 없어요" : "그쪽은 막혀 있어요");
        }

        void ActOrWait(bool act)
        {
            pendingDir = null;
            if (run.Phase == Phase.Night)
                Play(act ? run.Night.ActHere() : new Move { Kind = MoveKind.Wait, Target = run.Night.Player, Middle = run.Night.Player });
            else
                Play(act ? run.DayState.ActHere() : new Move { Kind = MoveKind.Wait, Target = run.DayState.Player, Middle = run.DayState.Player });
        }

        bool CanAct()
        {
            var m = run.Phase == Phase.Night ? run.Night.ActHere() : run.DayState.ActHere();
            return m.Kind == MoveKind.Act;
        }

        void HandleKeys()
        {
            Dir? dir = null;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.E)) dir = Dir.MinusY;   // ↗
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) dir = Dir.PlusX; // ↘
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Z)) dir = Dir.PlusY;  // ↙
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q)) dir = Dir.MinusX; // ↖
            if (Input.GetKeyDown(KeyCode.Space)) { ActOrWait(CanAct()); return; }
            if (!dir.HasValue) return;
            bool runKey = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            pendingDir = null;
            Step(dir.Value, runKey);
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

        /// <summary>게이지: 0~100을 10칸으로(1칸 = 10). 숫자도 함께.</summary>
        void Gauge(Rect r, float v, float max, Color c, string label)
        {
            int cells = Mathf.Max(1, config.GaugeCells);
            float labelW = 40f;
            GUI.Label(new Rect(r.x, r.y - 1, labelW, r.height + 2), label, sSmall);
            float x0 = r.x + labelW, w = r.width - labelW - 30f;
            float cw = w / cells;
            float per = max / cells;
            var old = GUI.color;
            for (int i = 0; i < cells; i++)
            {
                var cell = new Rect(x0 + i * cw + 1, r.y + 2, cw - 2, r.height - 4);
                GUI.color = new Color(0, 0, 0, 0.12f); GUI.DrawTexture(cell, white);
                float fill = Mathf.Clamp01((v - i * per) / per);
                if (fill > 0) { GUI.color = c; GUI.DrawTexture(new Rect(cell.x, cell.y, cell.width * fill, cell.height), white); }
            }
            GUI.color = old;
            GUI.Label(new Rect(x0 + w + 4, r.y - 1, 30, r.height + 2), Mathf.RoundToInt(v).ToString(), sSmall);
        }

        void OnGUI()
        {
            Styles();
            hudRects.Clear();
            uiScale = Mathf.Max(1f, Mathf.Min(Screen.width / 400f, Screen.height / 720f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            float W = Screen.width / uiScale, H = Screen.height / uiScale;
            var paper = new Color(0.98f, 0.96f, 0.90f, 0.94f);

            DrawFade(W, H);

            if (mode == Mode.Title)
            {
                var r = Panel(new Rect(16, H * 0.10f, W - 32, 440), paper);
                GUILayout.BeginArea(new Rect(r.x + 16, r.y + 14, r.width - 32, r.height - 28));
                GUILayout.Label("손톱 먹은 쥐", sTitle);
                GUILayout.Space(8);
                GUILayout.Label("1. 첫날 밤에 시작한다. 밤에는 쥐가 되어, 해가 뜨기 전에 고양이를 피해 숨은 손톱을 냄새로 찾아 먹는다.\n2. 낮에는 그 사람으로 둔갑해, 해가 지기 전(둔갑 시간 안)에 그 사람의 일을 모두 한다. 다 하면 해가 지고 다시 밤이 된다.\n3. 가고 싶은 쪽을 한 번 누르면 한 칸 걷고, 빠르게 두 번 누르면 네 발로 두 칸 뛴다. 뛰다 시선에 걸리면 의심이 오른다.\n4. 의심이 100이 되거나 허기가 바닥나면 쥐불놀이로 끝난다. 밤에 고양이에게 잡히거나 해 뜰 때까지 손톱을 못 먹어도 끝난다.\n5. 대보름 밤(3일째)까지 버티면 그 사람의 자리를 차지한다. 날이 갈수록 어려워진다.", sBody);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("시작", sButton, GUILayout.Height(48))) StartRun();
                GUILayout.EndArea();
                return;
            }

            if (mode == Mode.Interlude)
            {
                var r = Panel(new Rect(16, H * 0.28f, W - 32, 260), paper);
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
                if (GUILayout.Button("다시 하기", sButton, GUILayout.Height(44))) StartRun();
                if (GUILayout.Button("처음 화면", sButton, GUILayout.Height(44))) { mode = Mode.Title; run = null; Clear(dynamicRoot); Clear(staticRoot); }
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            DrawQuadrantGuide(W, H);
            DrawHud(W, H, paper);
        }

        /// <summary>해가 뜨고 질 때 화면 덮개가 서서히 걷힙니다.</summary>
        void DrawFade(float W, float H)
        {
            float t = (Time.time - fadeStart) / FadeSeconds;
            if (t < 0 || t >= 1) return;
            var old = GUI.color;
            GUI.color = new Color(fadeCol.r, fadeCol.g, fadeCol.b, 1f - t);
            GUI.DrawTexture(new Rect(0, 0, W, H), white);
            GUI.color = old;
        }

        /// <summary>나를 지나는 가로선·세로선을 옅게 그려 네 구역을 보여 줍니다.</summary>
        void DrawQuadrantGuide(float W, float H)
        {
            if (zoomOut) return;
            var me = cam.WorldToScreenPoint(ToWorld(PlayerPos(), 0.2f));
            float gx = me.x / uiScale, gy = (Screen.height - me.y) / uiScale;
            var old = GUI.color;
            GUI.color = run.Phase == Phase.Night ? new Color(1, 1, 1, 0.12f) : new Color(0, 0, 0, 0.10f);
            for (float x = 0; x < W; x += 14) GUI.DrawTexture(new Rect(x, gy, 8, 1.5f), white);
            for (float y = 0; y < H; y += 14) GUI.DrawTexture(new Rect(gx, y, 1.5f, 8), white);
            GUI.color = old;
        }

        void DrawHud(float W, float H, Color paper)
        {
            bool night = run.Phase == Phase.Night;
            var top = Panel(new Rect(8, 8, W - 16, night ? 120 : 150), paper);
            float x = top.x + 10, y = top.y + 6, w = top.width - 20;
            string head = night ? "밤 " + run.Day + " · 해 뜨기 전에 손톱 찾기" : "낮 " + run.Day + " · " + run.DayState.As.Name + "(으)로 둔갑";
            GUI.Label(new Rect(x, y, w, 24), head, sBig); y += 28;
            Gauge(new Rect(x, y, w / 2 - 4, 18), run.Hunger, config.MaxHunger, new Color(0.85f, 0.6f, 0.2f), "허기");
            Gauge(new Rect(x + w / 2 + 4, y, w / 2 - 4, 18), run.Suspicion, config.MaxSuspicion, new Color(0.85f, 0.25f, 0.2f), "의심");
            y += 24;
            if (night)
            {
                var n = run.Night;
                string cat = n.CatChasing ? "<color=#c0392b><b>고양이가 쫓아온다! 쥐구멍으로!</b></color>" : "고양이 냄새 고리 " + n.Config.CatRing + "칸";
                GUI.Label(new Rect(x, y, w, 20), "쥐 시간 <b>" + n.BeatsLeft + "</b>/" + n.BeatLimit + " · " + cat, sSmall); y += 20;
                GUI.Label(new Rect(x, y, w, 40), NailLine(n), sSmall);
            }
            else
            {
                var d = run.DayState;
                GUI.Label(new Rect(x, y, w, 20), "둔갑 시간 <b>" + d.BeatsLeft + "</b>/" + d.BeatLimit + " · 냄새 고리 " + d.ScentRing + "칸", sSmall); y += 22;
                for (int i = 0; i < d.As.Missions.Length; i++)
                {
                    string mark = d.MissionDone(i) ? "■ " : "□ ";
                    string prog = d.MissionDone(i) ? "" : " (" + d.WorkDone[i] + "/" + config.WorkBeatsPerMission + ")";
                    GUI.Label(new Rect(x, y, w, 20), mark + d.As.MissionNames[i] + prog, sSmall); y += 19;
                }
            }

            // 화면 아래 버튼: 기다리기는 늘, 하기는 할 일이 있는 칸에서만(기획서 1장)
            float bh = 52;
            var bottom = Panel(new Rect(8, H - bh - 16, W - 16, bh + 8), new Color(0, 0, 0, 0));
            bool canAct = CanAct();
            float mapW = bottom.width * 0.26f;
            float left = bottom.width - mapW - 6;
            if (canAct)
            {
                string act = night ? run.Night.ActLabel() : run.DayState.ActLabel();
                if (GUI.Button(new Rect(bottom.x, bottom.y, left * 0.62f, bh), act, sButton)) ActOrWait(true);
                if (GUI.Button(new Rect(bottom.x + left * 0.62f + 6, bottom.y, left * 0.38f - 6, bh), "기다리기", sButton)) ActOrWait(false);
            }
            else if (GUI.Button(new Rect(bottom.x, bottom.y, left, bh), "기다리기", sButton)) ActOrWait(false);
            if (GUI.Button(new Rect(bottom.x + bottom.width - mapW, bottom.y, mapW, bh), zoomOut ? "가까이" : "지도", sButton))
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
            var parts = new List<string>();
            foreach (var nail in n.Nails)
            {
                if (!nail.Found) continue;
                string how = nail.Method == NailMethod.Throw ? "던짐 · 바로 먹음"
                    : nail.Method == NailMethod.Bury ? "묻음 · 파기 " + n.Config.DigBeats + "박자"
                    : "태움 · 내일 의심 +" + n.Config.BurnSuspicion;
                parts.Add(Village.Get(nail.Owner).Name + " " + how);
            }
            if (parts.Count == 0) return "손톱 냄새를 찾아 돌아다니세요. 사람마다 버리는 곳이 달라요.";
            return "찾은 손톱: " + string.Join(" / ", parts.ToArray());
        }

        static string EndText(EndReason r)
        {
            switch (r)
            {
                case EndReason.Win: return "대보름 밤, 쥐는 그 사람의 자리를 차지했다.";
                case EndReason.Suspicion: return "마을 사람들이 눈치챘다. 의심 100.";
                case EndReason.Hunger: return "허기를 못 견디고 쓰러졌다.";
                case EndReason.BeatsOut: return "임무를 다 못 했는데 해가 져서 둔갑이 풀려 버렸다.";
                case EndReason.CaughtByCat: return "고양이에게 잡혔다.";
                case EndReason.Sunrise: return "손톱을 못 먹었는데 해가 떴다.";
                default: return "";
            }
        }
    }
}
