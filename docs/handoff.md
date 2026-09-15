# 인계 문서

다른 에이전트나 새 대화에서 이 프로젝트를 이어받을 때 읽는 문서입니다.
마지막 갱신: 2026-09-16.

기준 문서는 [PROJECT.md](../PROJECT.md)입니다. 이 문서는 **지금 당장 무엇을 해야 하는지**만 짧게 정리합니다.

## 그대로 붙여 넣을 재개 요청

~~~text
D:/Project/PetThemGame 에서 작업해 줘.

1. PROJECT.md, AGENTS.md, docs/handoff.md 를 읽고 실제 파일·git 상태와 대조해.
2. Unity 라이선스가 활성화됐는지 먼저 확인해.
   C:/ProgramData/Unity/Unity_lic.ulf 가 있으면 활성, 없으면 미활성이야.
   - 활성이면: game/ 을 Unity 6000.3.24f1 로 열어 컴파일·Play 모드·기록 저장을 검증해.
   - 미활성이면: Unity 관련 작업은 건너뛰고, D:/Project/PetThemBalanceLab 에서
     compare_experiments 와 create_balance_candidate 구현을 이어가.
3. 작업 단위마다 해당 저장소의 scripts/check.ps1 을 돌리고,
   PROJECT.md 현재 상태와 docs/verification.md 를 갱신한 뒤 커밋·푸시해.
   두 저장소는 각각 커밋해.
~~~

## 저장소 두 개

| 저장소 | 로컬 | 최신 커밋 | 담는 것 |
| --- | --- | --- | --- |
| jjjGi/pet-them (비공개) | D:/Project/PetThemGame | `eadbdc3` | Unity 게임, 공통 전투 코어, 밸런스 설정 |
| jjjGi/pet-them-balance-lab (비공개) | D:/Project/PetThemBalanceLab | `15d9489` | MCP 서버, 봇 시뮬레이터, 기록 분석 |

사용자 지시로 **MCP는 반드시 별도 비공개 저장소**에 둡니다. 게임 저장소로 되돌리지 마세요.

실험실은 게임 저장소의 전투 소스를 `PetThemGameRoot`로 직접 컴파일합니다.
복사본이 없으므로 규칙이 갈라질 수 없습니다. 게임 쪽 전투 규칙을 고치면 **양쪽 검증을 모두** 돌려야 합니다.

## 지금 막혀 있는 것

**Unity 라이선스 미활성.** 에이전트가 대신 할 수 없습니다.

- 에디터와 Android 모듈(SDK/NDK/OpenJDK)은 설치 완료:
  `C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe`
- 라이선스 파일 없음. `C:/ProgramData/Unity` 디렉터리 자체가 없습니다.
- 사용자가 Unity Hub를 열어 계정 로그인 후 라이선스를 활성화해야 합니다.

이것 때문에 아직 못 한 것: Unity 임포트·컴파일·Play 모드, Android APK, 실기기 멀티터치와 손맛,
단계 A 완료 판정, 첫 실제 플레이 기록.

## 지금 할 수 있는 것

라이선스와 무관하게 진행 가능한 작업입니다.

1. `compare_experiments` — 실험 저장·조회 구조부터 필요합니다.
2. `create_balance_candidate` — 설정 후보 버전 관리. 기존 설정을 덮어쓰지 않고 새 후보로 남깁니다.
3. `Create_Balance_Report` HTML 템플릿 뼈대 — 외부 CDN 없이 열리는 단일 파일이 목표입니다.
4. `analyze_playtests`는 **지금 만들어도 읽을 대상이 없습니다.** 사람의 플레이 기록이 0건입니다.

## 검증 명령

~~~powershell
# 게임 저장소 - 전투 규칙 12개
powershell -NoProfile -ExecutionPolicy Bypass -File D:\Project\PetThemGame\scripts\check.ps1

# 실험실 저장소 - 9개 (MCP 실제 통신 검사 포함)
powershell -NoProfile -ExecutionPolicy Bypass -File D:\Project\PetThemBalanceLab\scripts\check.ps1
~~~

라이선스가 생긴 뒤 Unity 컴파일 확인:

~~~powershell
& "C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe" -batchmode -quit -projectPath D:/Project/PetThemGame/game -logFile -
~~~

## 넘겨받는 사람이 알아야 할 함정

- **MCP stdio: 요청을 쓰자마자 stdin을 닫으면 안 됩니다.** 서버가 응답을 쓰기 전에 전송 계층이 종료되어
  stdout이 빈 채로 끝납니다. 응답을 다 받은 뒤 닫아야 합니다. `tools/LabChecks/Program.cs`의 `Handshake`가 예시입니다.
- MCP 서버는 stdout에 프로토콜 메시지만 씁니다. 로그는 전부 stderr입니다. `Console.WriteLine`을 추가하지 마세요.
- `WithTools<T>()`는 static 클래스를 받지 못합니다. 도구 클래스는 `sealed class`여야 합니다.
- PowerShell에서 `Select-Object -First N`으로 `dotnet run` 출력을 자르면 파이프가 끊겨 종료 코드 255가 납니다.
  실패가 아닙니다.
- Unity Hub는 MSIX라 `C:/Program Files/WindowsApps`에 설치됩니다. 일반 Program Files 경로만 보면 못 찾습니다.
- 실험실의 출력 경로는 `experiments/` 밖이면 거부됩니다. 의도된 동작입니다.

## 측정된 사실 (해석 주의)

현재 설정 `prototype-0.1`에서 봇 세 정책 모두 180초를 **피해 0으로** 승리합니다.
제자리에 서서 펀치만 반복하는 봇조차 한 번도 맞지 않습니다.

이것은 관찰된 결과입니다. **난이도가 쉽다는 결론이나 수치 변경의 근거로 바로 쓰지 마세요.**
봇은 사람의 반응 속도·조준·피로를 흉내 내지 않습니다.
사람이 직접 만져 본 뒤에 적 속도·등장 위치·웨이브 구성을 검토합니다.
