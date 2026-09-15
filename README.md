# PET THEM!

**귀여우면 쓰다듬고, 덤비면 두드려라.**

가로형 모바일 생존 액션 로그라이트와 게임 밸런스 실험실 프로젝트.
플레이어는 직접 펀치를 날리고, 펫 Mochi는 자동으로 적을 공격합니다.

## 현재 구현

- 60 Hz 공통 C# 전투 코어: 이동, 방향 펀치, 밀치기, 자동 공격 펫, 추격형·돌진형 적, 웨이브, 승리·패배·중도 종료.
- Unity 프로토타입 소스: 모바일 멀티터치, PC 입력, 시작·일시정지·재시작, 임시 도형 그래픽.
- 외부 JSON 밸런스 설정과 JSONL 실행 기록.
- 같은 전투 코드를 사용하는 .NET 시뮬레이터와 핵심 규칙 검증 도구.

**Unity 에디터 실행·Android 실기기 검증은 아직 완료하지 않았습니다.**
화살·레이저·레벨업 선택·상점·MCP·HTML 보고서는 후속 단계입니다.

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
dotnet run --project tools/CoreChecks -c Release
dotnet run --project balance-lab/simulator -c Release -- --runs 5 --seed 42
~~~

시뮬레이터 설정:

~~~powershell
dotnet run --project balance-lab/simulator -c Release -- --config game/Assets/Resources/balance-default.json --output experiments/smoke --runs 5 --seed 42
~~~

생성되는 JSONL과 요약 JSON은 experiments/에 저장되며 Git에서 제외됩니다.
봇은 정해진 궤도로 이동하며 자동 조준 펀치를 반복합니다. **실제 사람의 실력·조작감·재미를 측정하는 도구가 아닙니다.**

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
