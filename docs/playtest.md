# Unity Play 모드 확인

## 최신 상태

사용자가 기존 화면·조작을 확인했다고 답변했다. 아래 옛 화면 확인 대기는 종료되었다.
게임 완성을 우선하며 MCP 분석은 보류한다. 새 0.2.0 강화 화면은 별도 확인이 필요하다.

### 새 강화 확인 순서

1. 새 판을 시작해 적을 5마리 처치한다. XP가 오르고 강화 카드 3개가 나타나는지 본다.
2. 카드가 열린 동안 적과 게임 시간이 멈추는지 확인한다.
3. 카드 또는 1·2·3으로 선택하고 이동·공격이 다시 되는지 확인한다.
4. 선택 중 일시정지·복귀가 가능하고, 재시작하면 레벨 1·XP 0으로 돌아가는지 확인한다.


## 현재 확인된 기록 — 2026-09-16

기록 폴더의 WindowsEditor 실행 2건을 읽었다. 기록 작성기가 source를 human으로 표시하지만,
이 표만으로 누가 조작했는지나 화면 품질을 입증하지 않는다.

| 기록 시작 (한국 시간) | 게임 시간 | 펀치 | 펫 공격 | 처치 | 받은 피해 | 종료 체력 | 종료 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 2026-09-16 04:24:29 | 29.87초 | 58 | 33 | 20 | 0 | 100 | abandoned |
| 2026-09-16 17:19:43 | 77.48초 | 144 | 83 | 70 | 50 | 50 | abandoned |

- 각 기록에 시작·종료 이벤트가 하나씩 있고, 종료 처치 수와 kill 이벤트 수가 일치한다.
- 서로 다른 플레이어 좌표가 각각 22개 / 65개 기록됐다.
- 이동·수동 공격·펫 공격·피격·기록 저장이 실행된 흔적이다.
- 둘 다 중도 종료다. 패배 또는 3분 생존 완료로 집계하지 않는다.
- 샘플링된 이동과 공격은 동시 입력 자체를 증명하지 않는다.
- pause/resume 이벤트가 없으므로 일시정지 버튼 동작을 기록만으로 판정하지 않는다.
- 원본 기록은 로컬에 두고 GitHub에 올리지 않았다.
- 현재 Editor.log에서 error CS, NullReferenceException, MissingReferenceException,
  InvalidOperationException, Recording unavailable/stopped 패턴은 0건이었다.
  이 검사는 모든 종류의 오류가 없다는 보증이 아니다.

## 사용자가 확인할 순서

1. Unity 상단 Play를 누르고 Game 탭에 PET THEM! 시작 화면이 보이는지 확인한다.
2. LET'S PLAY를 누른다.
3. WASD로 이동하면서 마우스 클릭 또는 Space로 공격한다. 펫이 자동으로 공격하는지도 본다.
4. 우측 II를 누르고 게임 시간과 적 이동이 멈추는지 확인한다.
5. KEEP GOING으로 재개한 뒤 이동이나 공격 입력이 계속 눌린 채 남지 않는지 확인한다.
6. 다시 일시정지하고 RESTART를 누른다. 시간·체력·처치가 초기화되는지 확인한다.
7. 시작 버튼·문구·체력 표시가 잘리거나 겹치지 않는지 확인한다.
8. 편한 부분과 불편한 부분을 기록한다. 특히 펀치 방향·범위, 반복 클릭 피로, 회피와 공격의 충돌.
9. 테스트 후 Play를 꺼 기록을 종료한다.

에이전트가 확인한 로그와 사용자가 확인한 화면·조작 결과를 따로 기록한다.
PC 검증을 Android 두 손 조작이나 손맛 검증의 대체로 처리하지 않는다.

## 다시 기록 확인하기

저장소 루트에서:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/inspect-playtest.ps1 -Latest 5
~~~

진단 스크립트는 읽기 전용이며 파일을 업로드하거나 수정하지 않는다.
진행 중인 기록·JSON이 잘린 기록·처치 수 불일치를 경고하며, 누락된 종료를 사망으로 취급하지 않는다.
MCP의 analyze_playtests 구현을 대신하는 도구가 아니라 Play 모드 확인용 로컬 진단이다.

## 이전 작업의 대기 항목 (기존 화면·조작은 사용자 확인으로 종료)

- 위 순서에 대한 사용자의 화면·조작 확인 결과.
- 일시정지·재개·재시작의 실제 동작 확인.
- 실제 Android 기기의 입력·가독성·성능·피로도.
- 컴퓨터 조작 플러그인의 node_repl 도구가 이번 세션에 노출되지 않아 에이전트 직접 UI 확인은 수행하지 못했다.
  다음 세션에는 실제 도구 가용성을 다시 확인한다.

## 폰에서 확인하기

### 1. APK 만들기

Unity 에디터가 열려 있으면 메뉴에서 바로 뽑는 게 빠르다.

**PET THEM > Build Android development APK**

결과물: `game/Builds/PetThem-development.apk` (Git 제외).
첫 빌드는 5~10분 걸린다. 에디터를 닫은 상태라면 배치 모드로도 가능하다.

~~~powershell
& "C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe" `
  -batchmode -quit -projectPath D:/Project/PetThemGame/game -buildTarget Android `
  -executeMethod PetThem.Editor.PrototypeSetup.BuildAndroid -logFile $env:TEMP/unity-android.log
~~~

**실행 중인 에디터가 있으면 배치 모드를 같이 돌리지 않는다.** 프로젝트가 잠겨 있어 실패한다.

### 2. 폰에 넣기

USB 케이블이 있으면 이 방법이 제일 빠르다.

1. 폰: 설정 > 휴대전화 정보 > 빌드 번호를 7번 눌러 개발자 옵션을 켠다.
2. 설정 > 개발자 옵션 > **USB 디버깅**을 켠다.
3. 케이블로 연결한다. 폰에 뜨는 "USB 디버깅을 허용하시겠습니까?"를 허용한다.
4. PC에서:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File D:/Project/PetThemGame/scripts/install-apk.ps1
~~~

설치하고 앱까지 실행한다. 이미 설치돼 있으면 덮어쓰되 **저장 파일(코인·해금)은 유지**된다.

케이블이 없으면 APK 파일을 클라우드나 메일로 폰에 보내고 파일 관리자에서 탭한다.
이때 "출처를 알 수 없는 앱 설치"를 허용해야 한다.

### 3. 폰에서 먼저 볼 것

| 확인 | 왜 |
| --- | --- |
| **한글이 네모(□)로 깨지지 않는지** | IMGUI 기본 폰트에 한글이 없어 OS 폰트로 대체한다. PC는 되지만 Android는 미확인이다 |
| 글자가 카드·버튼 밖으로 넘치지 않는지 | 한국어가 영어보다 긴 곳이 있다 |
| 왼손 이동과 오른손 공격이 **동시에** 되는지 | 멀티터치. 프로토타입의 핵심 가정이다 |
| 화살: 끌어서 조준 → 떼면 발사가 손에 붙는지 | |
| 레이저: 드래그 없이 누르면 빔이 직전 방향에 멈춘다 | 자동 조준을 일부러 넣지 않았다. 어색한지 판단이 필요하다 |
| 프레임이 버티는지 | 적이 상한(100)까지 쌓이는 후반 |
| 코인이 앱을 껐다 켜도 남는지 | Android의 persistentDataPath 쓰기 확인 |

### 4. 로그 보기

플레이 중 문제가 생기면 PC에서:

~~~powershell
& "C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe" logcat -s Unity
~~~

### 알아둘 것

- 개발 빌드라 `android.permission.INTERNET`이 들어간다. 프로파일러용이며 게임은 오프라인이다.
  출시 빌드에서 빠지는지는 단계 E에서 확인한다.
- arm64-v8a 전용이다. 요즘 폰은 모두 해당한다.
- 저장 파일과 플레이 기록은 폰에 따로 생긴다. PC의 코인과 합쳐지지 않는다.
