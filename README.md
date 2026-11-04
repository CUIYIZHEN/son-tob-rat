# 손톱 먹은 쥐

옛이야기 「손톱 먹은 쥐」를 바탕으로 한 1인 모바일 미니게임 프로토타입입니다.
밤에는 쥐가 되어 사람이 버린 손톱을 먹고, 낮에는 그 사람으로 둔갑해 임무를 끝냅니다.
기획서: v3 (Claude Doc) · 난이도 실험실: nail-lab-v4.

## 다섯 줄 규칙

1. 밤에 쥐가 되어 사람이 버린 손톱을 먹는다. 고양이를 깨우면 안 된다.
2. 낮에는 그 사람으로 둔갑해, 정해진 박자 안에 그 사람의 일을 모두 한다.
3. 한 칸씩 걸으면 안전하다. 네 발로 뛰면 두 칸이지만 시선에 걸리면 의심이 오른다.
4. 의심이 가득 차거나, 고양이에게 잡히거나, 허기가 바닥나면 쥐불놀이로 끝난다.
5. 대보름 밤(3일째)까지 버티면 그 사람의 자리를 차지한다.

## 조작

- 내 주변 네 방향에 1칸·2칸 거리 칸이 표시됩니다. 1칸을 누르면 걷기, 2칸을 누르면 네 발로 뛰기.
- 붉은 칸은 지금 누군가의 시선(낮)이나 고양이 잠귀 범위(밤)에 걸리는 칸입니다.
- 내 칸을 누르거나 아래 버튼을 누르면 '하기'(임무, 우물에서 씻기, 손톱 파기·먹기).
- 키보드: ↑ ↗, → ↘, ↓ ↙, ← ↖ (Shift를 누르면 뛰기), Space는 하기.

## 폴더

| 경로 | 내용 |
|---|---|
| `Assets/Scripts/Core` | 규칙만 담은 순수 C# (Unity 없이도 돌아감). 수치는 `GameConfig.cs` 한 곳 |
| `Assets/Scripts/Game` | 화면과 입력. 미술 파일 없이 도형을 코드로 그림 |
| `Assets/Editor` | WebGL 빌드 메뉴, 걸음 길 L 재기 메뉴 |
| `Assets/Tests/EditMode` | 규칙 테스트 (지도 L=30, 시선, 쫑긋 규칙 등) |
| `.github/workflows/build.yml` | 테스트 → WebGL 빌드 → GitHub Pages 배포 |

## 처음 열기

1. Unity Hub에서 Unity **6000.0 LTS**로 이 폴더를 엽니다(다른 6000.0.x도 됩니다).
2. 빈 씬에서 Play를 누르면 게임이 뜹니다. 빌드용 씬은 메뉴 `손톱 쥐 → Main 씬 만들기`.
3. 지도를 고쳤으면 `손톱 쥐 → 걸음 길 L 재기`로 세 사람의 L을 확인합니다.

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
