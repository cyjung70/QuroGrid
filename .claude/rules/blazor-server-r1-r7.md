---
paths:
  - "src/Quro.Grid/**"
  - "tests/Quro.Grid.Tests/**"
  - "tests/Quro.Grid.E2E/**"
---

# Blazor Server 설계 원칙 R1~R7 — 리뷰 필수 항목

| # | 원칙 | 구현 규칙 |
|---|---|---|
| R1 | 소유권 분리 | JS: 포커스·선택·스크롤·리사이즈 상태 / 서버: 데이터 값·행 상태·검증. JS로는 열 순서·폭·편집 가능·필수·고정 열 여부만 보낸다 (queryId·코드 출처 금지) |
| R2 | 커밋 시점에만 전송 | 편집 중 키 입력은 브라우저에서만 처리. 셀 이탈(Enter·Tab·클릭) 때 1회 전송. 대문자 변환·길이 제한도 브라우저에서, 커밋 때 서버 재검증 |
| R3 | 일괄 전송 | 붙여넣기·행 복사·일괄 삭제·참조(refs) 끌어오기는 호출 1회 |
| R4 | 렌더 범위 최소화 | 셀마다 컴포넌트 생성 금지. 행 단위 `RenderFragment` + `@key` + `ShouldRender`. ColumnDef는 불변이며 행 렌더에서 캐시 |
| R5 | 이벤트 위임 | 셀마다 `@onclick` 금지. 그리드 단위로 받고 JS가 행·열 좌표 해석 → `columns[i].Field` |
| R6 | 서버 메모리 보호 | 대량 조회는 서버측 페이징(`Virtualize` ItemsProvider). DataSet에는 적재 범위 + 변경 행만. 화면 정의는 Singleton 캐시, 회로마다 파싱 금지 |
| R7 | 연결 끊김 대비 | 재연결 중 입력 잠금 + 안내. 미저장 변경 경고 (브라우저 보관 안 함, O-03) |

## 리뷰에서 반려하는 코드
- 키 입력(`@onkeydown`, `@oninput`)마다 서버를 호출하는 코드
- 셀 단위 컴포넌트 또는 셀 단위 이벤트 핸들러
- 붙여넣기를 셀마다 JS Interop으로 보내는 코드
- 불필요한 `StateHasChanged()` 반복 호출
