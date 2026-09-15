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
