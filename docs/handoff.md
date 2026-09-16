# 인계 문서

마지막 갱신: 2026-09-16. 기준 문서는 [PROJECT.md](../PROJECT.md).

## 현재 사용자가 선택한 작업

**변수별 밸런스 실험보다 Unity 화면·조작 확인을 먼저 진행한다.**
사용자가 직접 Play 모드로 확인할 순서를 전달했고, 화면·동시 입력·일시정지·재시작에 대한 답변을 기다리는 중이다.
다음 작업 때 새 답변과 실제 로그를 먼저 확인한다. 기본 밸런스 값은 변경하지 않았다.

## 저장소와 현재 상태

| 저장소 | 로컬 경로 | 역할 |
| --- | --- | --- |
| jjjGi/pet-them (비공개) | D:/Project/PetThemGame | Unity 게임, 공통 전투 코어, 기본 설정 |
| jjjGi/pet-them-balance-lab (비공개) | D:/Project/PetThemBalanceLab | MCP 도구 10개, 시뮬레이터, 비교·후보·HTML 보고서 |

MCP는 사용자 지시로 반드시 별도 비공개 저장소에 둔다.
실험실은 게임의 전투 코어를 PetThemGameRoot로 참조한다. 복사본을 만들지 않는다.
양쪽을 수정하면 각 저장소 지침을 읽고 각각 검증·커밋·푸시한다.
최신 커밋은 각 저장소의 git log로 확인한다.

## 해결된 것 — 재설치하지 않는다

- Unity Personal 활성화 완료.
- Unity 6000.3.24f1: C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe.
- Android SDK/NDK/OpenJDK 설치 완료.
- Unity 배치 임포트·컴파일 성공.
- Android 개발 APK 빌드 성공: game/Builds/PetThem-development.apk.
- MCP의 compare_experiments, create_balance_candidate, Create_Balance_Report까지 구현 완료.
- 실험실 검사 14개 통과 이력. 게임 전투 규칙 검사 12개.

라이선스 존재를 C:/ProgramData/Unity 경로만으로 판정하지 않는다.
Hub가 MSIX라 라이선스가 패키지 LocalCache 아래에 있다. 민감한 라이선스 내용은 출력하지 않는다.
현재 Unity가 열려 있으면 같은 프로젝트로 두 번째 배치 에디터를 띄우거나 기존 창을 임의로 종료하지 않는다.

## 새로 확인한 사실

**이전 문서의 '사람 플레이 기록 0건'은 현재 파일 상태와 맞지 않는다.**
WindowsEditor 클라이언트 기록 2건이 있다. source 라벨은 human이지만 조작자의 신원이나 손맛을 증명하지는 않는다.

- 29.87초 기록: 펀치 58회, 펫 공격 33회, 처치 20, 피해 0, abandoned.
- 77.48초 기록: 펀치 144회, 펫 공격 83회, 처치 70, 피해 50, abandoned.
- 두 기록 모두 이동 좌표 변화와 종료 이벤트, 처치 합계 일치를 확인했다.
- 종료가 abandoned이므로 둘 다 패배나 3분 생존 성공으로 집계하지 않는다.
- 기록 저장은 확인했지만 화면·버튼·동시 입력·일시정지 동작은 별도 확인이 필요하다.
- 상세: [playtest.md](playtest.md). 원본 기록은 로컬에만 남기며 커밋하지 않는다.

로그 진단:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/inspect-playtest.ps1 -Latest 5
~~~

현재 세션에는 computer-use 스킬은 있지만 필수 node_repl 도구가 노출되지 않았다.
따라서 에이전트가 화면을 보거나 클릭한 것으로 보고하지 않았다.
다음 세션에 도구가 있으면 스킬을 읽고 새 창 상태부터 확인한다.

## 다음 작업

1. PROJECT.md, AGENTS.md, 이 문서, 실제 Git 상태를 대조한다.
2. 사용자의 Play 모드 피드백과 새 로그를 확인한다.
3. 화면·조작 문제가 보고되면 재현 가능한 범위를 좁혀 수정하고 검증한다.
4. 로컬 기록은 이미 있으므로 analyze_playtests가 '데이터 0건 때문에 불가능'하다고 반복하지 않는다.
   분석 구현 여부는 현재 UI 확인 작업이 끝난 뒤 정한다. 두 건의 중도 종료 기록만으로 사람의 밸런스를 확정하지 않는다.
5. 실제 Android 기기로 동시 이동·공격·가독성·프레임·피로를 확인한다.
6. 사용자가 원할 때, 기존 cand-contact-probe의 네 변경을 한 값씩 분리해 30개 시드로 비교한다.
7. 작업 내용을 PROJECT.md, docs/verification.md, 이 문서에 갱신하고 커밋·푸시한다.

## 밸런스 해석

봇이 피해를 받지 않는 현상과 적이 상한 근처까지 누적되는 현상은 별도로 본다.
기존 복합 후보가 봇을 모두 사망시켰다는 것만으로 어느 변경이 원인인지 판정하지 않는다.
같은 시드는 초기 난수 조건을 맞추지만 **정확히 같은 위치·시점의 적 등장**까지 보장하지 않는다.
등장 위치는 플레이어 위치에, 생성 시점은 적 수 상한에 영향을 받기 때문이다.
사람의 조작 확인 없이 게임 기본 수치를 봇 결과에 맞춰 조정하지 않는다.

## 검증 명령

~~~powershell
# 게임
powershell -NoProfile -ExecutionPolicy Bypass -File D:/Project/PetThemGame/scripts/check.ps1
# 실험실 — 실제로 실험실을 수정했거나 공통 규칙을 바꿨을 때
powershell -NoProfile -ExecutionPolicy Bypass -File D:/Project/PetThemBalanceLab/scripts/check.ps1
~~~

Unity 배치 실행은 해당 프로젝트가 다른 에디터에 열려 있지 않을 때 수행한다.
Start-Process -Wait 또는 프로세스 종료 확인 뒤 로그의 성공·실패를 판정한다.
Editor 로그 기본 위치: %LOCALAPPDATA%/Unity/Editor/Editor.log.
프로세스 전체 명령행에는 인증 값이 포함될 수 있으므로 출력하지 않는다.

## 알려진 함정

- MCP stdio는 응답을 다 받을 때까지 stdin을 열어 둔다.
- 서버 stdout은 프로토콜 메시지 전용, 로그는 stderr.
- WithTools<T>()에는 static 클래스가 아닌 sealed class를 사용한다.
- dotnet 출력을 Select-Object -First로 끊으면 파이프 종료 코드가 생길 수 있다.
- Unity 서명 검증 경고만으로 라이선스 실패를 단정하지 않는다.
- 원본 로그·생성 실험·보고서는 Git 제외. 공유할 수치 요약만 문서에 기록한다.
- 개발 APK의 INTERNET 권한은 출시 빌드에서 실제 필요 여부를 다시 확인한다.
