# 검증 기록

## 2026-09-16 — 첫 전투 프로토타입

### 자동으로 확인한 항목

.NET SDK 10.0.400 / Windows / Release 빌드.

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check.ps1
dotnet run --project balance-lab/simulator -c Release -- --runs 5 --seed 42 --output experiments/verified
~~~

check.ps1은 현재 PowerShell 실행 정책을 시스템 전체에 변경하지 않고 별도 프로세스에서 실행했다.

- 전투 코어 검증 **12개 통과**.
- 시뮬레이터 빌드 **경고 0 / 오류 0**.
- 시드 42~46으로 각 180초 봇 시뮬레이션 완료.
- 각 로그에 시작 이벤트 1개, 종료 이벤트 1개, 상태 기록 180개 확인.
- 로그의 kill 이벤트 합계와 종료 처치 수 일치.
- 손상된 설정·비정상 입력 거부, 대각선 이동 속도 제한, 경기장 경계, 설정 복사, 시드 재현성, 시드별 차이, 쿨다운, 펫 자동 공격, 실제 피해량, 펀치 방향, 승패 및 중도 종료, 적 수 상한을 검증했다.

### 봇 결과

| 시드 | 종료 | 생존 시간 | 처치 | 남은 체력 |
| --- | --- | --- | --- | --- |
| 42 | 승리 | 180초 | 154 | 100 |
| 43 | 승리 | 180초 | 154 | 100 |
| 44 | 승리 | 180초 | 149 | 100 |
| 45 | 승리 | 180초 | 144 | 100 |
| 46 | 승리 | 180초 | 155 | 100 |

정해진 궤도로 이동하고 자동 조준 펀치를 반복하는 봇의 결과다.
이 결과는 게임이 재미있거나 인간 플레이어에게 적절한 난이도라는 증거가 아니다.
현재 설정은 회피하는 봇이 피해 없이 생존할 수 있다. 실제 조작 검증 후 적 속도·등장 위치·웨이브 구성을 검토한다.

### 아직 확인하지 않은 항목

- Unity 6000.3.24f1의 전체 프로젝트 임포트 및 컴파일.
- Unity Play 모드 화면·버튼·입력·로그 저장.
- Android APK 빌드 및 실제 기기 멀티터치·프레임 성능·가독성.
- Unity와 .NET에서 같은 전투 조건을 실행한 결과 비교.
- 실제 플레이 재미와 손의 피로.
- MCP 및 HTML 보고서: 아직 구현하지 않음. (MCP는 아래 실험실 분리 항목에서 진행됨)

### Unity 설치 진행

Unity Hub 3.21.2를 winget으로 설치했다. MSIX 패키지 형태라 일반 Program Files/Unity Hub 경로 대신 WindowsApps에 설치된다.
Hub headless 설치로 Unity 6000.3.24f1 및 Android 모듈 다운로드를 시작했다.
에디터 설치 완료와 계정·라이선스 인증 상태를 확인한 뒤 위 미검증 항목을 진행한다.

## 2026-09-16 — 실험실 분리

봇 시뮬레이터를 별도 저장소 jjjGi/pet-them-balance-lab으로 옮겼다.
이관 전후로 시드 42~46의 이벤트 스트림이 바이트 단위로 동일함을 확인했다 (각 1668~1695 이벤트).
처치 수도 위 표와 같다. 이관이 전투 동작을 바꾸지 않았다.

이 저장소의 scripts/check.ps1은 이제 전투 규칙 검증 12개만 실행한다.
봇 시뮬레이션과 MCP 검증은 실험실 저장소의 scripts/check.ps1에 있다.

### 실험실 쪽에서 새로 측정된 것

제자리에 서서 펀치만 반복하는 봇도 180초 동안 한 번도 맞지 않는다 (시드 42~46, 평균 처치 237.6).
"회피하는 봇이 피해 없이 생존할 수 있다"보다 더 강한 결과다. 회피조차 필요 없다.
자세한 수치는 실험실 저장소의 docs/verification.md에 있다.

### Unity 설치 상태 (재확인)

- 에디터: C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe — 설치됨.
- Android: AndroidPlayer / SDK / NDK / OpenJDK — 모두 설치됨.
- 라이선스: 이 시점에는 미활성으로 판단했다. 아래 "라이선스 활성화" 항목에서 해소됐고,
  `C:/ProgramData/Unity`만 보고 판단한 것이 잘못된 방법이었다는 것도 거기 적었다.

## 2026-09-16 — Unity 라이선스 활성화 및 첫 실제 컴파일

사용자가 Unity Hub에서 계정 로그인과 라이선스 활성화를 완료했다.

- 라이선스: **Unity Personal**, Type `Assigned`, Expiration `Unlimited`.
- 라이선스 파일은 Hub가 MSIX라서 가상화된 경로에 있다:
  `%LOCALAPPDATA%/Packages/UnityTechnologies.UnityHub_2vrhnee42bhxm/LocalCache/Local/Unity/licenses/UnityEntitlementLicense.xml`
  `C:/ProgramData/Unity/Unity_lic.ulf`는 존재하지 않는다. 그 경로만 보고 미활성으로 판단하면 안 된다.
  실제 활성 여부는 에디터를 배치 모드로 실행해 확인하는 것이 확실하다.

### 배치 모드 임포트·컴파일

~~~powershell
& "C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe" `
  -batchmode -quit -projectPath D:/Project/PetThemGame/game -logFile <경로>
~~~

결과:

- `Exiting batchmode successfully now! ... return code 0`
- **컴파일 오류 0개** (`error CS` 0건).
- 어셈블리 3개 생성 확인: `PetThem.Combat.dll`, `PetThem.Game.dll`, `PetThem.Editor.dll`.
- 로컬 패키지 `com.petthem.combat-core`가 정상 등록됐다.
  게임 저장소의 전투 코어를 Unity가 패키지로 인식한다는 뜻이다.
- 로그 앞부분에 `[Licensing::Client] Code 10 while verifying Licensing Client signature`와
  `LicensingClient has failed validation; ignoring`가 나오지만, 직후 `Successfully connected`와
  `Successfully resolved entitlement details`로 이어진다. MSIX 서명 검증 경고이며 동작에는 영향이 없다.
- `CreateDirectory 'C:/Users/user/AppData/Local/Unity/Caches' failed`도 나오지만 임포트와 컴파일은 정상 완료됐다.

**이것은 소스가 컴파일된다는 확인이다. 화면·조작·손맛 검증이 아니다.**
Play 모드에서 실제로 움직이고 때려 보는 것은 사람이 해야 한다.

### Android 개발 APK 빌드

~~~powershell
& "C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe" `
  -batchmode -quit -projectPath D:/Project/PetThemGame/game -buildTarget Android `
  -executeMethod PetThem.Editor.PrototypeSetup.BuildAndroid -logFile <경로>
~~~

빌드 도구는 모두 에디터 설치본 안의 것을 사용했다. 별도 SDK 설치가 필요하지 않았다.

- JDK / Android SDK(build-tools 36.0.0) / NDK / Gradle 9.1.0 + Android Gradle Plugin 9.0.0.
- 결과: `Exiting batchmode successfully now! ... return code 0`.
- 산출물: `game/Builds/PetThem-development.apk`, 19,337,619 바이트, 404개 항목.

APK를 aapt2로 검사한 결과:

| 항목 | 값 |
| --- | --- |
| package | `com.petthem.game` |
| versionName | `0.1.0` |
| application-label | `PET THEM!` |
| minSdkVersion / targetSdkVersion | 25 / 36 |
| native-code | `arm64-v8a` (IL2CPP, `libil2cpp.so` 포함) |
| launchable-activity | `com.unity3d.player.UnityPlayerGameActivity` |
| debuggable | 예 (개발 빌드) |

화면 방향은 `ProjectSettings.asset`의 `defaultScreenOrientation: 3` = LandscapeLeft로 설정돼 있다.
`allowedAutorotateToPortrait`가 1이지만 기본 방향이 AutoRotation이 아니므로 적용되지 않는다.

**확인 필요:** 개발 빌드라서 `android.permission.INTERNET`이 들어간다.
Unity 프로파일러 연결용이다. 이 프로젝트는 오프라인 실행을 전제하므로,
출시용 빌드에서 이 권한이 빠지는지 단계 E에서 반드시 확인한다.

### Unity가 생성한 프로젝트 설정 커밋

임포트 과정에서 Unity가 `game/ProjectSettings/` 전체와 `game/Packages/packages-lock.json`을 생성했다.
이 파일들이 없으면 다음에 여는 사람은 기본값을 받게 되고, 위에서 확인한 가로 방향·패키지 이름·입력 방식 설정이 사라진다.
그래서 커밋에 포함했다. `game/.utmp/`는 에디터 임시 폴더라 Git에서 제외했다.

### 여전히 확인하지 않은 항목

- **Play 모드에서의 실제 화면·조작·손맛.** 자동 검증으로 대체할 수 없다. 사람이 해야 한다.
- 실제 Android 기기에 설치해 동시 이동·공격, 프레임 성능, 가독성 확인.
- Unity와 .NET에서 같은 전투 조건을 돌린 결과 대조.
- 실제 플레이 기록 저장 동작 (현재 기록 0건).

## 2026-09-16 — 실험실 쪽 진행 (요약)

실험실 저장소에서 MCP 도구 5개를 추가하고 검사 14개를 통과시켰다.
자세한 내용은 그쪽 docs/verification.md에 있다. 여기서는 게임에 영향을 주는 것만 기록한다.

### 시계열을 그려 보고 드러난 것

**살아 있는 적 수가 첫 30초 평균 4.6에서 마지막 평균 95.2로 늘어난다.** 적 수 상한이 100이므로 거의 상한에 붙는다.
플레이어는 한 번도 맞지 않는데 적은 계속 쌓이는 상태가 동시에 나타난다.
처치 속도가 등장 속도를 따라가지 못한다는 뜻이다.

"봇이 피해를 안 받는다"까지는 이미 알고 있었지만, 적 누적은 시계열을 그리기 전에는 보이지 않았다.
이 둘은 다른 문제이고 수정 방향도 다를 수 있다.

### 밸런스 수치는 바꾸지 않았다

후보 `cand-contact-probe`(펀치 약화 + 적 가속)는 봇을 5회 모두 사망시켰다. 기준의 정반대다.
적정선은 두 극단 사이이고, 그 지점은 사람이 직접 조작해 본 뒤에 정한다.

game/Assets/Resources/balance-default.json은 그대로다. 후보는 실험실 저장소의 candidates/에만 있다.

## 2026-09-16 — Play 모드 기록 재점검

사용자가 변수별 비교 실험보다 Unity 화면·조작 확인을 먼저 선택했다.

- 기존 WindowsEditor 클라이언트 기록 2건을 발견했다. '사람 기록 0건'이라는 기존 현재 상태 안내를 수정했다.
- 29.87초 / 77.48초 기록의 이동 좌표 변화, 수동 펀치 58 / 144회, 펫 공격 33 / 83회와 종료 이벤트를 확인했다.
- 최근 기록은 처치 70, 받은 피해 50, 종료 체력 50이다.
- 두 실행 모두 abandoned이다. 사망 또는 3분 생존 성공으로 집계하지 않았다.
- source는 기록 작성기가 붙인 human 라벨이며, 이 라벨만으로 누가 조작했는지 증명하지 않는다.
- 원본 로그는 업로드·커밋하지 않았다. 요약은 docs/playtest.md에 있다.
- 읽기 전용 scripts/inspect-playtest.ps1을 추가했다.
- 실제 기록 2건의 좁은 진단 항목에서 경고 0. 종료 누락·깨진 JSON·처치 불일치·정상 중도 종료의 합성 사례 4개도 확인했다.
- 현재 Editor.log에서 컴파일 오류(error CS), 대표 런타임 예외 3종, 기록 실패 패턴은 0건이었다. 모든 오류가 없다는 판정은 아니다.
- 이 세션에서는 computer-use의 필수 node_repl 도구가 없어 에이전트가 화면을 직접 보거나 클릭하지 못했다.
- 사용자가 Play 모드의 화면·동시 조작·일시정지·재시작을 확인하도록 순서를 전달했다. 피드백은 아직 대기 중이다.
- 전투 규칙과 기본 밸런스, 실험실 소스는 이번 작업에서 변경하지 않았다.

- 게임 scripts/check.ps1의 전투 코어 검사 12개 통과.

## 2026-09-16 — 게임 우선 전환 및 한 판 성장

사용자가 기존 화면·조작을 확인했다고 답변했고, MCP 개발을 게임 완성 이후로 미뤘다.
실험실 분석 초안은 별도 WIP 브랜치 79d0342에 보관했다. 게임 개발로 전환했다.

- 처치 XP·3택1 강화 7종·선택 중 정지·랭크 상한·XP 이월·재시작 초기화 구현.
- 전투 코어 검사 17개 통과. 같은 시드의 선택지·성장 재현성과 선택하지 않은 강화 거부 확인.
- 설치된 UnityEngine DLL을 참조한 Unity 런타임 C# 소스 컴파일: 경고 0, 오류 0.
  실제 Unity 에디터 임포트·Play 검증을 대체하지는 않는다.
- 공통 코어 변경 후 기존 실험실 main의 회귀 검사 14개 통과. 기본 false인 성장 옵션 덕분에 기존 봇은 기존 규칙으로 실행된다.
- Unity 게임은 성장을 켜므로 기존 봇/옛 플레이 기록을 새 게임의 밸런스 근거로 해석하지 않는다.
- 게임 버전 0.2.0, 기록 스키마 v2. 기본 밸런스 JSON은 변경하지 않았다.
- 신규 강화 화면의 실제 Play·터치 확인, 새 APK 빌드·Android 실행은 미검증이다.
  실행 중인 사용자 에디터를 닫거나 같은 프로젝트를 배치 실행하지 않았다.
## 2026-09-16 — 주무기 3종 (게임 0.3.0)

전투 규칙 검사 **22개 통과** (기존 17개 + 무기 5개). Unity 런타임 소스 컴파일 경고·오류 0.
공통 코어를 바꿨으므로 실험실 회귀 검사 14개도 확인했다.

### 새로 확인한 항목

18. 화살이 날아가고, 관통 상한만큼만 피해를 주며, 쿨다운 동안 버튼을 계속 눌러도 추가 발사되지 않는다.
19. 화살은 누르는 동안 조준만 하고 떼는 순간 한 발만 나간다. 느린 화살이 적에 겹쳐 있어도 같은 적에게 두 번 기록되지 않는다.
20. 레이저는 플레이어 앞쪽만 태운다. 열이 100이 되면 잠기고, 버튼을 계속 눌러도 피해가 없으며, **열이 완전히 0이 될 때까지** 복구되지 않는다.
21. 제시되는 강화는 판을 시작한 무기의 것뿐이다. 세 무기 각각 6레벨까지 확인했다.
22. 무기 수치가 유효성 검사를 받는다. arrowPierce 0, laserRange 0, arrowSpeed NaN은 거부된다.

### 구현 중 고친 것

**릴리스로 발사할 때 자동 조준이 걸리지 않았다.** 조준 보정이 `input.punch`만 보고 있어서,
드래그 없이 탭했다 떼는 경우 화살이 항상 초기 방향(오른쪽)으로 나갔다.
모바일에서 탭으로 쏘는 플레이어에게 그대로 드러나는 문제였다. 발사 여부를 먼저 계산해 조준에 넘기도록 바꿨다.

**일시정지·강화 선택 중 드래그가 끊기면 재개 첫 스텝에서 화살이 저절로 나갔다.**
코어가 "누르고 있다가 뗐다"로 읽기 때문이다. `CancelHeldAttack()`을 추가하고 입력 초기화에서 호출한다.

**강화 후보 목록이 비면 0으로 나누는 경로가 있었다.** 현재 구성에서는 최소 3개가 보장되지만,
나중에 상한을 하나만 더 추가해도 게임이 죽는 코드였다. 빈 목록에서 빠져나가도록 막았다.

### 설계 결정

- 무기는 판 시작 시 1개만 고르고 도중에 바꿀 수 없다. PROJECT.md 3절의 기준 그대로다.
- 강화 선택지는 무기별로 분리했다. 레이저 판에 펀치 강화가 뜨면 선택지 하나가 버려지는 것과 같다.
- 레이저 조준은 자동 보정을 하지 않는다. 빔이 저절로 표적을 옮기면 플레이어가 조준하는 무기가 아니게 된다.
- 화살은 발사한 스텝에 이미 명중할 수 있다. 적이 몰려 있으면 흔한 일이라 검사도 그 전제로 작성했다.
- 과열 중에는 냉각도 느려진다(`laserOverheatPenalty`). 과열의 대가가 실제 공백이 되도록 한 것이다.

### 아직 확인하지 않은 항목

- **Play 모드에서 세 무기의 실제 손맛.** 특히 화살의 드래그·해제와 레이저의 열 관리.
- 모바일 멀티터치에서 화살 드래그가 이동 조작과 충돌하지 않는지.
- 0.3.0 Android APK 빌드와 실기기.
- 무기별 밸런스. 수치는 초기값이며 사람의 플레이 전에는 조정하지 않는다.
- 봇 정책은 여전히 펀치만 사용한다. 화살·레이저의 봇 실험은 정책 추가 후에 가능하다.
