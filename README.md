# PET THEM!

**귀여우면 쓰다듬고, 덤비면 두드려라.**

가로형 모바일 생존 액션 로그라이트와 게임 밸런스 실험실 프로젝트.
플레이어는 직접 펀치를 날리고, 펫 Mochi는 자동으로 적을 공격합니다.

## 현재 구현

- 60 Hz 공통 C# 전투 코어: 이동, 방향 펀치, 밀치기, 자동 공격 펫, 추격형·돌진형 적, 웨이브, 승리·패배·중도 종료.
- Unity 프로토타입 소스: 모바일 멀티터치, PC 입력, 시작·일시정지·재시작, 임시 도형 그래픽.
- 외부 JSON 밸런스 설정과 JSONL 실행 기록.
- 전투 코어를 UnityEngine 없이 실행하는 핵심 규칙 검증 도구 (tools/CoreChecks).

**게임 완성을 먼저 진행하고 MCP 개발은 그 뒤에 재개합니다.** 기존 프로토타입은 Unity 컴파일·APK 빌드와 사용자 화면 확인을 마쳤습니다. 새 강화 UI와 0.2.0 APK·실기기는 아직 검증 전입니다.
0.2.0에서는 처치 XP와 3택1 강화 7종을 추가했습니다. 화살·레이저·펫 확장·상점·저장이 다음 단계입니다. MCP 도구 10개와 기본 HTML 보고서는 별도 실험실 저장소에 구현되어 있습니다.
Play 모드 확인 순서와 기록 진단은 [docs/playtest.md](docs/playtest.md)를 참고하세요.

## Unity 실행

1. Unity Hub에서 Unity **6000.3.24f1**과 Android Build Support / SDK & NDK / OpenJDK를 설치합니다.
2. Unity 계정으로 로그인하고 사용할 수 있는 라이선스를 활성화합니다.
3. 저장소 안의 **game** 폴더를 Unity Hub 프로젝트로 추가합니다.
4. 에디터 메뉴 **PET THEM > Create or open prototype scene**을 실행합니다.
5. Play를 누릅니다. Game 뷰는 16:9 가로형을 권장합니다.

| 환경 | 이동 | 공격 |
| --- | --- | --- |
| 모바일 | 왼쪽 영역에서 드래그 | 오른쪽 탭으로 자동 조준 펀치, 드래그 후 해제로 방향 지정 |
| PC | WASD 또는 방향키 | 마우스 위치 조준 + 클릭, SPACE는 자동 조준 |

일시정지는 우측 상단 버튼 또는 Escape입니다. 앱이 포커스를 잃으면 자동으로 일시정지합니다.
Android 개발 APK: **PET THEM > Build Android development APK**. 개발 빌드이며 스토어 배포용 서명은 포함하지 않습니다.

## Unity 없이 전투 규칙 검증

.NET SDK 10.0.400 이상 같은 패치 계열이 필요합니다. 저장소 루트에서 실행합니다.

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check.ps1
~~~

전투 규칙 검증 12개를 실행합니다.

## 밸런스 실험실은 별도 저장소입니다

봇 시뮬레이터, MCP 서버, 기록 분석, HTML 보고서 생성기는
[jjjGi/pet-them-balance-lab](https://github.com/jjjGi/pet-them-balance-lab) (비공개)에 있습니다.

그 저장소는 이 저장소의 전투 코어와 밸런스 설정을 **복사하지 않고 참조**합니다.
두 폴더를 나란히 두거나 `PETTHEM_GAME_ROOT` 환경 변수로 이 저장소의 경로를 알려주면 됩니다.

~~~powershell
# 실험실 저장소에서
dotnet run --project src/Simulator -c Release -- --runs 5 --seed 42
~~~

봇은 정해진 정책으로 이동하며 자동 조준 펀치를 반복합니다. **실제 사람의 실력·조작감·재미를 측정하는 도구가 아닙니다.**

## 밸런스 설정과 기록

- 설정: game/Assets/Resources/balance-default.json
- 공통 전투 소스: packages/com.petthem.combat-core/Runtime
- 실제 플레이 기록: Unity Application.persistentDataPath 아래 runs/
- Windows 기본 기록 위치: %USERPROFILE%/AppData/LocalLow/PetThem/PET THEM!/runs
- 실행 종료 화면에도 기록 경로를 표시합니다.
- 로그 스키마: [docs/telemetry.md](docs/telemetry.md)

프로토타입 기록은 로컬에만 저장합니다. 서버로 전송하지 않습니다.

## 이어서 개발하기

[PROJECT.md](PROJECT.md)에 합의한 기획, 단계별 완료 기준, MCP 도구와 Create_Balance_Report 상세 요구사항이 있습니다.
[AGENTS.md](AGENTS.md)에 검증·문서 갱신·자동 커밋과 푸시 지침이 있습니다.
밸런스 실험실 쪽 작업은 [jjjGi/pet-them-balance-lab](https://github.com/jjjGi/pet-them-balance-lab)의 README와 AGENTS.md를 따릅니다.
다른 대화나 다른 에이전트로 이어받을 때는 [docs/handoff.md](docs/handoff.md)를 먼저 읽습니다.
