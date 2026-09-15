# 인계 문서

다른 에이전트나 새 대화에서 이 프로젝트를 이어받을 때 읽는 문서입니다.
마지막 갱신: 2026-09-16.

기준 문서는 [PROJECT.md](../PROJECT.md)입니다. 이 문서는 **지금 당장 무엇을 해야 하는지**만 짧게 정리합니다.

## 그대로 붙여 넣을 재개 요청

~~~text
D:/Project/PetThemGame 에서 작업해 줘.

1. PROJECT.md, AGENTS.md, docs/handoff.md 를 읽고 실제 파일·git 상태와 대조해.
2. Unity 라이선스는 이미 활성이야 (Unity Personal). 컴파일과 Android APK 빌드도 이미 성공했어.
   다시 확인하려면 배치 모드로 돌려봐. 파일 경로로 판단하지 마.
3. 다음 할 일은 둘 중 하나야. 사용자에게 어느 쪽인지 물어봐.
   - 게임: Play 모드 확인 후 전투 난이도 조정. 손맛은 사람이 봐야 하니 사용자에게 맡기고,
     결과를 들은 뒤에 수치를 바꿔.
   - 실험실(D:/Project/PetThemBalanceLab): compare_experiments 와
     create_balance_candidate 구현, 그다음 Create_Balance_Report.
4. 작업 단위마다 해당 저장소의 scripts/check.ps1 을 돌리고,
   PROJECT.md 현재 상태와 docs/verification.md 를 갱신한 뒤 커밋·푸시해.
   두 저장소는 각각 커밋해.
~~~

## 저장소 두 개

| 저장소 | 로컬 | 담는 것 |
| --- | --- | --- |
| jjjGi/pet-them (비공개) | D:/Project/PetThemGame | Unity 게임, 공통 전투 코어, 밸런스 설정 |
| jjjGi/pet-them-balance-lab (비공개) | D:/Project/PetThemBalanceLab | MCP 서버, 봇 시뮬레이터, 기록 분석 |

최신 커밋은 `git log --oneline -1`로 확인하세요. 이 문서에 적힌 값은 금방 낡습니다.

사용자 지시로 **MCP는 반드시 별도 비공개 저장소**에 둡니다. 게임 저장소로 되돌리지 마세요.

실험실은 게임 저장소의 전투 소스를 `PetThemGameRoot`로 직접 컴파일합니다.
복사본이 없으므로 규칙이 갈라질 수 없습니다. 게임 쪽 전투 규칙을 고치면 **양쪽 검증을 모두** 돌려야 합니다.

## Unity 상태 (해결됨)

- 라이선스: **Unity Personal**, 활성. Type `Assigned`, 무기한.
- 에디터: `C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe`
- Android SDK/NDK/JDK/Gradle: 에디터 설치본 안에 모두 포함. 별도 설치 불필요.
- 배치 모드 임포트·컴파일: 성공, 오류 0.
- Android 개발 APK: 빌드 성공 (19.3 MB, `com.petthem.game` 0.1.0, arm64-v8a, IL2CPP).

**라이선스 파일은 `C:/ProgramData/Unity`에 없습니다.** Hub가 MSIX라 아래로 가상화됩니다.

~~~text
%LOCALAPPDATA%/Packages/UnityTechnologies.UnityHub_2vrhnee42bhxm/LocalCache/Local/Unity/licenses/UnityEntitlementLicense.xml
~~~

경로 존재 여부로 판단하지 말고, 배치 모드로 에디터를 실제 실행해 확인하세요.

## 남은 것 — 사람이 해야 하는 일

빌드가 된다는 것과 재미있다는 것은 다릅니다. 자동 검증으로 대체하지 마세요.

1. **Play 모드 확인** — 화면, 버튼, 이동·공격 동시 입력, 일시정지, 재시작, 기록 저장.
2. **실기기 확인** — APK를 설치해 두 손 조작, 프레임, 가독성, 손의 피로.
3. 위 결과를 들은 **뒤에** 전투 난이도를 조정합니다. 봇 결과만 보고 바꾸지 마세요.

## 실험실 상태 (MCP 도구 10개 동작)

`compare_experiments`, `create_balance_candidate`, `Create_Balance_Report`까지 전부 구현하고 검증했습니다.
검사 14개 통과. 실제로 보고서를 만들어 봤습니다: `reports/balance-01.html`.

한 바퀴 돌려보려면:

~~~powershell
cd D:\Project\PetThemBalanceLab
dotnet build src/McpServer -c Release
./scripts/mcp-call.ps1 run_simulation '{"runs":5,"seed":42,"label":"기준","outputDirectory":"demo"}'
./scripts/mcp-call.ps1 list_experiments '{"limit":5}'
./scripts/mcp-call.ps1 Create_Balance_Report '{"baselineExperimentId":"exp-...","outputPath":"r.html"}'
~~~

**남은 도구는 `analyze_playtests` 하나**이고, 막고 있는 것은 기술이 아니라 데이터입니다.
사람의 플레이 기록이 0건이라 구현해도 읽을 대상이 없습니다.

## 사람 없이 진행 가능한 일

1. **한 번에 한 값만 바꾼 후보로 나눠 실험.** 현재 후보 `cand-contact-probe`는 네 값을 동시에 바꿔서
   어느 변경이 얼마나 기여했는지 분리할 수 없습니다. `punchRange`만, `gruntSpeed`만 식으로 나누세요.
2. 표본을 20~50회로 늘려 시드 변동 폭 확인. 현재 5회는 방향만 볼 수 있는 수준입니다.
3. 보고서의 인쇄 레이아웃과 화면 낭독기 동작 확인. 아직 검증 안 했습니다.
4. 단계 E용 확인 항목: 개발 빌드에 `android.permission.INTERNET`이 들어갑니다(프로파일러용).
   오프라인 게임이므로 출시 빌드에서 빠지는지 확인해야 합니다.

## 지금 알고 있는 밸런스 상태

두 가지가 **동시에** 관찰됩니다. 서로 다른 문제이고 수정 방향도 다를 수 있습니다.

1. 봇이 한 번도 맞지 않습니다. 제자리에 서 있는 봇조차 그렇습니다.
2. 살아 있는 적이 첫 30초 평균 4.6에서 마지막 평균 95.2로 쌓입니다. 상한이 100입니다.

1번만 보고 "너무 쉽다"로 결론내지 마세요. 적은 계속 쌓이고 있습니다.

## 검증 명령

~~~powershell
# 게임 저장소 - 전투 규칙 12개
powershell -NoProfile -ExecutionPolicy Bypass -File D:\Project\PetThemGame\scripts\check.ps1

# 실험실 저장소 - 9개 (MCP 실제 통신 검사 포함)
powershell -NoProfile -ExecutionPolicy Bypass -File D:\Project\PetThemBalanceLab\scripts\check.ps1
~~~

Unity 컴파일 확인 (로그 파일을 지정하고 `error CS`를 세는 편이 확실합니다):

~~~powershell
$log = "$env:TEMP\unity-import.log"
& "C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe" `
  -batchmode -quit -projectPath D:/Project/PetThemGame/game -logFile $log
~~~

Android 개발 APK 빌드:

~~~powershell
& "C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe" `
  -batchmode -quit -projectPath D:/Project/PetThemGame/game -buildTarget Android `
  -executeMethod PetThem.Editor.PrototypeSetup.BuildAndroid -logFile $log
~~~

결과물은 `game/Builds/PetThem-development.apk` (Git 제외).

## 넘겨받는 사람이 알아야 할 함정

- **MCP stdio: 요청을 쓰자마자 stdin을 닫으면 안 됩니다.** 서버가 응답을 쓰기 전에 전송 계층이 종료되어
  stdout이 빈 채로 끝납니다. 응답을 다 받은 뒤 닫아야 합니다. `tools/LabChecks/Program.cs`의 `Handshake`가 예시입니다.
- MCP 서버는 stdout에 프로토콜 메시지만 씁니다. 로그는 전부 stderr입니다. `Console.WriteLine`을 추가하지 마세요.
- `WithTools<T>()`는 static 클래스를 받지 못합니다. 도구 클래스는 `sealed class`여야 합니다.
- PowerShell에서 `Select-Object -First N`으로 `dotnet run` 출력을 자르면 파이프가 끊겨 종료 코드 255가 납니다.
  실패가 아닙니다.
- Unity Hub는 MSIX라 `C:/Program Files/WindowsApps`에 설치됩니다. 일반 Program Files 경로만 보면 못 찾습니다.
  라이선스 파일도 같은 이유로 `C:/ProgramData/Unity`가 아니라 패키지 LocalCache에 들어갑니다.
- **Unity.exe를 배치 모드로 띄우면 셸 래퍼가 먼저 반환됩니다.** 종료 코드가 비어 있고 로그도 짧게 보입니다.
  프로세스가 아직 살아 있는 것이니 `Unity.exe`가 사라질 때까지 기다린 뒤 로그를 읽으세요.
- Unity 로그의 `Licensing Client signature ... Code 10`과 `LicensingClient has failed validation; ignoring`은
  MSIX 서명 검증 경고입니다. 바로 뒤에 `Successfully resolved entitlement details`가 나오면 정상입니다.
- 실험실의 출력 경로는 `experiments/` 밖이면 거부됩니다. 의도된 동작입니다.

## 측정된 사실 (해석 주의)

현재 설정 `prototype-0.1`에서 봇 세 정책 모두 180초를 **피해 0으로** 승리합니다.
제자리에 서서 펀치만 반복하는 봇조차 한 번도 맞지 않습니다.

이것은 관찰된 결과입니다. **난이도가 쉽다는 결론이나 수치 변경의 근거로 바로 쓰지 마세요.**
봇은 사람의 반응 속도·조준·피로를 흉내 내지 않습니다.
사람이 직접 만져 본 뒤에 적 속도·등장 위치·웨이브 구성을 검토합니다.
