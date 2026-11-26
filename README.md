# 손톱 먹은 쥐

옛이야기 「손톱 먹은 쥐」를 바탕으로 한 1인 모바일 미니게임 프로토타입입니다.
밤에는 쥐가 되어 사람이 버린 손톱을 먹고, 낮에는 그 사람으로 둔갑해 임무를 끝냅니다.
기획서: 「손톱 먹은 쥐 — 프로토타입 기획서」 (Claude Doc), 요약은 `docs/game-concept.md`. 프로토타입은 밤 1번 + 낮 1번입니다.

## 규칙

1. 밤에 쥐가 되어 어둠 속에서 고양이 눈(붉은 칸)을 피해 도령의 손톱을 먹는다. 쥐는 앞 3칸만 보인다.
2. 먹은 걸음이 빠를수록 낮 걸음이 최대 3 늘어난다.
3. 낮에는 도령으로 둔갑해, 남은 걸음 안에 임무 3개(서당 · 정자 · 장승)를 끝낸다.
4. 모든 행동은 1걸음. 눈에 든 칸을 달리면 의심 +30, 진짜 도령과 마주치면 걸어도 +40, 의심 100이면 실패.

## 조작

- 나를 중심으로 화면을 네 구역으로 나눈 곳을 한 번 누르면 걷기, 0.3초 안에 두 번 누르면 두 칸 달리기.
- 붉은 칸은 지금 고양이(밤)나 사람(낮)의 눈에 걸리는 칸, 점선 칸은 다음 걸음에 눈이 닿을 칸입니다.
- 아래 버튼 「기다리기」는 늘, 「먹기」(밤) · 「임무하기」(낮)는 손톱 칸 · 임무 칸 위에서만 눌립니다.
- 키보드: ↑ ↗, → ↘, ↓ ↙, ← ↖ (Shift를 누르면 달리기), Space는 먹기 · 임무하기 · 기다리기.

## 폴더

| 경로 | 내용 |
|---|---|
| `Assets/Scripts/Core` | 규칙만 담은 순수 C# (Unity 없이도 돌아감). 수치는 `GameConfig.cs` 한 곳 |
| `Assets/Scripts/Game` | 화면과 입력. 그림은 `Assets/Resources/Art`의 PNG를 쓰고, 없으면 도형을 코드로 그림 |
| `tools/make_art.py` | 그림(캐릭터 · 칸 · 물건 · 컷 2장)을 그리는 스크립트. `python3 tools/make_art.py`로 다시 그림 |
| `Assets/Editor` | WebGL 빌드 메뉴, 걸음 길 L 재기 메뉴 |
| `Assets/Tests/EditMode` | 규칙 테스트 (지도 L=30, 시선, 고양이 길, 밤 보너스, 의심 등) |
| `.github/workflows/build.yml` | 테스트 → WebGL 빌드 → GitHub Pages 배포 |

## 처음 열기

1. Unity Hub에서 Unity **6000.0 LTS**로 이 폴더를 엽니다(다른 6000.0.x도 됩니다).
2. 빈 씬에서 Play를 누르면 게임이 뜹니다. 빌드용 씬은 메뉴 `손톱 쥐 → Main 씬 만들기`.
3. 지도를 고쳤으면 `손톱 쥐 → 걸음 길 L 재기`로 L=30을 확인합니다.

## 자동 빌드와 배포 (GameCI)

저장소 Settings → Secrets and variables → Actions에 Unity 계정을 넣습니다.

- `UNITY_EMAIL`, `UNITY_PASSWORD`: Unity 계정. Personal(무료) 라이선스는 이 둘이면 됩니다.
- `UNITY_SERIAL`: Pro/Plus 라이선스일 때만.
- `UNITY_LICENSE`: Enterprise용 `.ulf` 파일을 쓸 때만. 자세한 내용은 https://game.ci/docs/github/activation

그리고 Settings → Pages → Source를 **GitHub Actions**로 바꿉니다.
`main`에 올릴 때마다 테스트 → WebGL 빌드 → Pages 배포가 돌고, 휴대폰 브라우저에서
`https://<계정>.github.io/<저장소>/`로 바로 할 수 있습니다.

## 폰트

`Assets/Resources/Fonts/HUD.ttf`는 나눔고딕(SIL OFL 1.1)입니다. WebGL에는 시스템 글꼴이 없어서
한글을 보이려면 꼭 필요합니다. 자세한 내용은 같은 폴더의 `FONT-LICENSE.md`.
