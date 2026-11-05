# 프로젝트 작업 기준

## 프로젝트
- 옛이야기 「손톱 먹은 쥐」를 바탕으로 한 1인 모바일 미니게임 프로토타입 (밤: 쥐가 되어 손톱 먹기, 낮: 그 사람으로 둔갑해 임무)
- 기획 의도와 규칙 요약: docs/game-concept.md
- 작업 기록: progress.md (작업을 시작할 때 읽고, 마칠 때 고친다)

## 구현 환경
- Unity 6, 버전은 ProjectSettings/ProjectVersion.txt 기준
- 미술 파일 없이 코드로 도형을 그림 (Assets/Scripts/Game), 화면 글자는 OnGUI + Resources/Fonts/HUD.ttf
- 규칙은 Unity 없는 순수 C# (Assets/Scripts/Core)
- 배포: main에 올리면 GitHub Actions(.github/workflows/build.yml)가 테스트 → WebGL 빌드 → GitHub Pages

## 수정 범위
- 난이도 수치는 Assets/Scripts/Core/GameConfig.cs 한 곳에서만 정의
- 지도는 Village.MapRows만 고치고, 고친 뒤 세 사람의 걸음 길 L=30 유지
- 엔진 · 패키지 · 워크플로 버전 변경은 따로 제안
- 한 번에 기능 하나만 변경, main에 바로 올리지 않고 브랜치 + PR
- 게임 안 글자는 한국어

## 확인
- Assets/Tests/EditMode 테스트 통과 (Unity Test Runner 또는 PR의 CI)
- 직접 플레이 확인은 기획자(jin)가 한다. 확인하지 못한 항목은 따로 보고
- 비밀값(UNITY_EMAIL, UNITY_PASSWORD 등)은 GitHub Secrets에만 둔다
