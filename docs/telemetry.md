# 전투 기록 v1

## 저장 형식

한 줄에 JSON 객체 하나인 JSONL 형식입니다. 인간 플레이와 시뮬레이션 모두 같은 CombatEvent 필드를 사용합니다.

첫 행 run_start:

- schemaVersion: 1.
- source: human 또는 simulation.
- runId, seed, fixedStep, buildVersion.
- configJson: 실행 시점의 BalanceConfig 전체를 직렬화한 문자열.
- 실제 플레이에는 startedUtc, platform, unityVersion.
- 시뮬레이션에는 policy. 현재 orbit-auto-punch-v1.

이후 이벤트:

| 필드 | 의미 |
| --- | --- |
| type | wave / spawn / attack / damage / kill / hurt / snapshot / run_end |
| tick, time | 고정 스텝 번호 및 시뮬레이션 시간(초) |
| wave | 현재 웨이브 |
| source | 공격 주체·적 종류·종료 이유 |
| targetId | 대상 적 ID, 없으면 0 |
| value | 이벤트별 수치 |
| x, y | 이벤트별 좌표 또는 방향 |
| health, alive | 이벤트 시점 플레이어 체력과 살아 있는 적 수 |

- wave.value: 웨이브 번호.
- spawn.value: 생성된 적 최대 체력; x/y는 위치.
- attack: punch는 x/y가 공격 방향, pet은 대상 위치.
- damage.value: 초과 피해를 제외한 실제 적용 피해; x/y는 적 위치.
- kill.value: 1.
- hurt.value: 실제 체력 감소량; x/y는 플레이어 위치.
- snapshot.value: 누적 처치 수; x/y는 플레이어 위치.
- run_end.source: survived / death / abandoned; value는 누적 처치 수.

snapshot은 매 60스텝 및 정상 승패 판정 시 남깁니다.
정상적인 재시작·종료는 abandoned로 구분합니다. 강제 종료나 저장 오류로 run_end가 없으면 불완전 기록으로 취급하고, 사망으로 해석하지 않습니다.
포커스 상실과 일시정지 동안 시뮬레이션 시간은 흐르지 않습니다.

## 재현 범위

- 같은 설정·시드·입력 시퀀스는 같은 .NET 실행 환경에서 동일한 결과를 만드는지 자동 검증합니다.
- 현재 인간 플레이 로그는 완전한 프레임별 입력 녹화가 아닙니다. 이 로그만으로 전체 플레이를 재생할 수 있다고 주장하지 않습니다.
- 봇의 입력은 policy와 시간으로 생성되므로 같은 버전의 정책으로 반복할 수 있습니다.
- 플랫폼 간 부동소수점 차이는 별도 검증 전입니다.
- 렌더링과 Unity 물리는 전투 판정에 사용하지 않습니다. 전투 코어의 2D 좌표와 거리 판정을 공유합니다.

## 분석 시 주의

- human과 simulation을 합쳐 실제 유저 통계를 만들지 않습니다.
- 구간별 사망 비율의 분모는 해당 구간에 도달한 실행 수입니다.
- 임의의 초보자·숙련자 라벨을 붙이지 않습니다.
- 전투 외 성장과 경제는 아직 구현 전이므로 관련 정체 분석도 아직 불가능합니다.
- 체력·웨이브 등은 규칙 데이터이며, 원인 가설과 구분해 보고해야 합니다.

## v2 — 한 판 성장 (게임 0.2.0)

Unity 클라이언트 헤더의 schemaVersion은 이제 2다. progressionEnabled=true,
progressionVersion=kill-xp-1을 함께 남긴다. configJson은 판 시작 시 기본 설정이다.

- xp: 처치 경험치 획득. value=1, source=kill.
- upgrade_offer: 제시한 카드마다 한 이벤트. source=UpgradeId, value=선택 시 랭크.
- level_up: 실제 선택 성공. source=선택한 UpgradeId, value=새 레벨.
- upgrade: 실제 적용. source=UpgradeId, value=선택 후 랭크.
- 선택 화면 동안 고정 스텝·게임 시간은 진행하지 않는다.
- ChooseUpgrade는 Step과 마찬가지로 이벤트 목록을 교체한다. 호출마다 즉시 한 번 기록한다.
- 같은 스텝의 다중 처치 XP가 남으면 다음 선택지를 즉시 제시한다.
- 레벨·강화는 해당 판에만 적용한다. 다시 시작하면 초기화된다.

기존 실험실 봇은 성장 비활성 상태로 실행하며 v1 기록을 남긴다.
보류한 analyze_playtests 초안도 v1 전용이다. 게임 완성 후 v2·무기·선택 정책 지원부터 갱신한다.
이전 기록과 새 성장 게임 기록의 결과를 바로 합쳐 비교하지 않는다.
## v2.1 — 주무기 (게임 0.3.0)

run_start 헤더에 `weapon`을 남긴다. 값은 `Punch` / `Arrow` / `Laser`다.

**무기가 다른 실행은 서로 비교하지 않는다.** 무기가 제시되는 강화 선택지를 결정하므로,
같은 설정·같은 시드라도 무기가 다르면 다른 실험이다.

새 이벤트와 source:

| type | source | 의미 |
| --- | --- | --- |
| attack | `arrow` | 화살 발사. value=발사 시점 화살 피해량, x/y=발사 방향. targetId는 0 |
| damage | `arrow` | 화살 명중. 한 화살은 같은 적에게 한 번만 기록된다 |
| attack | `laser` | 그 스텝에 빔이 무언가를 태움. value=해당 스텝 피해량, x/y=빔 방향 |
| damage | `laser` | 빔 피해. 매 스텝 기록되므로 건수가 많다 |
| overheat | `laser` | 열이 100에 도달해 잠김. value=100 |

- 화살은 관통 한도만큼만 피해를 준다. `damage`/`arrow` 건수는 발사 수 × 관통 상한을 넘지 않는다.
- 레이저는 쿨다운이 아니라 열로 제한된다. `attack`/`laser` 건수를 공격 횟수로 세면 안 된다.
  스텝마다 한 건씩 나오므로 발사 시간(초)에 비례한다.
- 과열 중에는 피해가 전혀 없다. `overheat` 이후 열이 0이 될 때까지의 구간은 공백이다.
- 화살은 발사한 스텝에 이미 명중할 수 있다. 발사와 명중이 같은 tick에 기록된다.
