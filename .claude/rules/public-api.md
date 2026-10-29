---
paths:
  - "src/Quro.Grid/**/*.cs"
  - "src/Quro.Grid/**/*.razor"
  - "src/Quro.Grid.Data/**/*.cs"
  - "src/Quro.Grid.Definition/**/*.cs"
---

# 공개 API 규칙 — 소비자(QuroERP) 보호

## 공개 범위
- `public`은 소비자가 쓰는 것만. 내부 구현은 `internal` (테스트는 `InternalsVisibleTo`).
- 공개 API = public 타입·멤버, 컴포넌트 `[Parameter]`, 이벤트 `EventCallback`, CSS 클래스·CSS 변수 이름, TOON 스키마 키·열거값.

## 바꿀 때
1. `docs/integration/consumer-contract.md`를 같은 PR에서 고친다 (정본).
2. `CHANGELOG.md`의 `Unreleased`에 적는다. 깨지는 변경은 `BREAKING:` 접두사 + 소비자 수정 방법.
3. 깨지는 변경은 MINOR를 올린다 (1.0 전). 가능하면 한 버전 동안 `[Obsolete("…")]`로 유지한 뒤 제거한다.
4. 새 공개 API 신설·이름 변경은 **Critical**이다. 스테이블영 확인 후 진행한다.

## 금지
- 공개 API에 QuroERP 타입·업무 용어(Invoice, SalesOrder 등) 노출
- 공개 API에 MudBlazor 타입 노출 (소비자가 MudBlazor 버전에 묶임). 필요하면 자체 타입으로 감싼다
- 같은 버전 번호로 재배포
