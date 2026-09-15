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
