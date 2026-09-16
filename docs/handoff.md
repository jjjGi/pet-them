# 인계 문서

마지막 갱신: 2026-09-16. 기준 문서: [PROJECT.md](../PROJECT.md).

## 가장 중요한 사용자 지시

**게임을 먼저 다 만들고, MCP는 그 뒤에 진행한다.**
사용자는 기존 Unity 화면·조작을 확인했다고 답변했다. 기존 화면 확인 대기는 끝났다.
MCP 기능 추가·보고서·분석 실험을 다음 작업으로 잡지 않는다.

## 이번 게임 변경 (0.4.0) — 스테이지·보스·보상

- 일반 적 3종째 Brute: 느리고 단단하고 세게 때린다.
- 보스가 120초에 등장한다. 잡으면 제한 시간 전에 승리(`boss_down`), 시간을 다 채우면 `survived`.
- 한 판 코인 보상. 상점은 아직 없고 결과 화면 표시만 한다.
- 설정 버전 `prototype-0.1` → `prototype-0.3`. **이전 실험과 직접 비교 금지.**
- 핵심 코드: packages/com.petthem.combat-core/Runtime/CombatStage.cs.
- 검사 27개 통과.

**보스 체력 1800은 실측으로 정한 값이지 검증된 값이 아니다.**
보스에만 붙는 봇으로 잰 60초 상한이 펀치 5848 / 화살 6216 / 레이저 2765였다.
레이저는 과열 때문에 실가동률이 34%라 가장 불리하다.
생존 압박 아래에서 보스를 잡아 본 봇은 없다. 사람이 해 본 뒤 레이저 쪽을 따로 봐야 한다.

## 이전 게임 변경 (0.3.0) — 주무기 3종

- 판 시작 전에 펀치·화살·레이저 중 하나를 고른다. 도중에 바꿀 수 없다.
- 화살: 드래그 조준 → 떼면 발사. 관통 2. 쿨다운 0.72초.
- 레이저: 누르는 동안 지속 피해. 쿨다운 대신 열 게이지. 100이면 잠기고 0이 될 때까지 안 풀린다.
- 강화 선택지는 고른 무기의 것만 나온다. 무기별 2종 + 공용 5종.
- 핵심 코드: packages/com.petthem.combat-core/Runtime/CombatWeapons.cs.
- 기록에 `weapon`을 남긴다. **무기가 다른 실행은 서로 비교하지 않는다.**
- 검사 22개 통과. Unity 런타임 소스 컴파일 통과. 실험실 회귀 14개 통과.
- **Play 모드에서 세 무기를 실제로 만져본 적이 없다.** 0.3.0 APK도 없다.

## 이전 게임 변경 (0.2.0)

- 0.2.0: 적 처치 1회당 XP 1, 첫 강화는 XP 5, 이후 필요 XP는 5 + (레벨 - 1) × 3.
- 3개 중 1개 선택: 펀치 위력·범위, 펫 위력·공격 주기, 이동 속도, 최대 체력, 회복.
- 선택 중 전투 시간이 멈춘다. 카드 클릭/터치 또는 1·2·3으로 선택.
- 선택 전후 입력 초기화, 다중 처치 XP 이월, 새 판 초기화.
- 범위·이동·펫 주기는 각 5랭크 제한. 최대 레벨 30.
- 기본 밸런스 JSON은 바꾸지 않았다. 강화는 해당 판의 설정 복사본에만 적용한다.
- 핵심 코드: packages/com.petthem.combat-core/Runtime/CombatProgression.cs.
- Unity 연결: game/Assets/Scripts/PrototypeGame.cs.
- 기록 schemaVersion=2, progressionEnabled/progressionVersion과 xp·upgrade_offer·level_up·upgrade 이벤트 추가.

## 검증 상태

- 게임 규칙 검사 17개 통과: 기존 12개 + 성장 관련 5개.
- 설치된 Unity 6000.3.24f1의 실제 UnityEngine DLL을 참조한 런타임 소스 컴파일 통과, 경고·오류 0.
  임시 검사 프로젝트는 experiments/unity-source-check/에 있으며 Git 제외다.
- 공통 코어 변경의 회귀 확인으로 실험실 main 검사 14개도 통과. 실험실 기능 개발을 재개한 것은 아니다.
- **신규 강화 화면의 Unity Play 검증과 새 APK 빌드는 아직 하지 않았다.**
- 기존 0.1.0 APK는 이번 강화가 들어간 빌드가 아니다.
- 기존 Unity 에디터가 열려 있어서 같은 프로젝트에 배치 에디터를 추가로 실행하지 않았다.

## 다음 게임 작업

1. 새 사용자 피드백과 Git 상태를 먼저 확인한다.
2. 강화 선택 화면과 **무기 3종**을 Play 모드에서 확인한다.
   특히 화살의 드래그·해제 타이밍과 레이저의 열 관리가 손에 붙는지.
   이미 확인한 옛 화면을 다시 확인해 달라고 반복하지 않는다.
3. 펫 다양화, 스테이지·적·보스, 한 판 보상, 상점·저장으로 연결한다.
4. 봇 정책은 아직 펀치만 쓴다. 화살·레이저 봇은 실험실 재개 시점에 추가한다.
5. Android 실제 기기에서 멀티터치·손맛·가독성·성능을 확인한다.
6. 게임 완성 후에만 MCP/보고서/밸런스 분석을 재개한다.

## 실험실 보관 상태 — 자동으로 이어서 개발하지 않는다

- 별도 비공개 저장소: jjjGi/pet-them-balance-lab, D:/Project/PetThemBalanceLab.
- 기존 main: MCP 도구 10개와 봇 HTML 보고서.
- 중단한 분석/클라이언트 보고서 초안: wip/playtest-analysis-paused, 커밋 79d0342. 원격 푸시했고 main에는 미병합.
- 현재 실험실 로컬 브랜치는 main이다. 원본 로그와 생성 보고서는 커밋하지 않았다.
- 중단한 WIP는 schemaVersion=1까지만 지원한다. 새 게임의 v2 성장 기록에 바로 적용하지 않는다.
- CombatWorld 생성자의 enableProgression은 기본 false로 기존 봇의 동작을 유지한다.
  Unity만 true로 시작한다. 기존 봇 결과를 새 성장 게임의 밸런스 결과로 해석하지 않는다.
- 재개 시 성장 선택 정책·무기·새 기록 스키마 지원이 먼저다.

## 환경과 주의점

- 게임: D:/Project/PetThemGame, jjjGi/pet-them (비공개).
- Unity Personal 활성화 완료. Unity 6000.3.24f1, Android SDK/NDK/OpenJDK 설치 완료.
- Unity 경로: C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe.
- Unity Editor 로그: %LOCALAPPDATA%/Unity/Editor/Editor.log.
- 기존 컴파일·0.1.0 Android APK 성공 이력은 docs/verification.md에 있다.
- C:/ProgramData/Unity 존재만으로 라이선스 미활성이라고 판단하지 않는다.
- 실행 중인 Unity를 임의로 닫거나 같은 프로젝트를 배치 모드로 중복 실행하지 않는다.
- 프로세스 전체 명령행에는 인증 값이 포함될 수 있으므로 출력하지 않는다.
- 사용자가 의미 있는 작업의 커밋·푸시를 이미 승인했다. 재승인 질문 없이 검사·문서 갱신 후 제출한다.
- 게임 규칙의 원본은 게임 저장소 하나다. 두 저장소 모두 수정하면 각각 커밋한다.
- 원본 기록·빌드·캐시·비밀 정보는 Git 제외.

## 검증 명령

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:/Project/PetThemGame/scripts/check.ps1
~~~

공통 전투 코어를 바꾼 경우에만 실험실 호환성 회귀 검사도 수행한다. 기능 개발은 보류다.