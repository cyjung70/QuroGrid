---
paths:
  - "src/Quro.Grid/**"
  - "src/Quro.Grid.Sample/**"
  - "tests/**"
---

# QuroGrid 기능 규칙

## 셀 타입 (P0)
`Text`, `Number`, `Currency`, `Date`, `Combo`, `Check`, `Popup`, `Button`
- 편집 중인 셀 **하나에만** 에디터를 띄운다.
- `Currency`의 소수 자릿수는 통화 마스터를 따른다 (로캘과 분리). 통화 정보는 앱이 주입한다.
- `Popup`: 코드 입력 후 Tab → 1건 일치면 자동 확정, 복수면 팝업, **0건이면 팝업 없음**(2026-10-28, Spec 4.6과 다름). 0건 입력값은 잠정으로 확정하지 않고 편집 유지. F4는 항상 팝업.
- 코드 목록·코드도움 데이터는 앱이 제공한다(`ICodeListSource`, `ILookupSource` 성격의 추상화 — 이름은 consumer-contract.md에서 확정). 그리드는 HTTP를 직접 호출하지 않는다.

## 키보드 맵 (P0)
Enter(확정·이동, 기본 오른쪽 O-05, 마지막 셀 새 행은 옵션·기본 끔), Tab/Shift+Tab, 화살표, F2(편집, 값 유지), 문자 입력(덮어쓰기), Esc(취소), Shift+화살표/드래그(범위 선택), Ctrl+C/V(엑셀 형식), Insert(행 추가), Ctrl+Delete(행 삭제), F4(코드도움).

## 다국어 · 로캘
- 헤더·메시지는 하드코딩하지 않고 **리소스 키**(`HeaderKey`). 리소스 해석은 앱의 `IStringLocalizer`로.
- 표시는 사용자 `CultureInfo`, 저장 값은 언어 무관 형식(ISO 날짜, 소수점 `.`).
- 붙여넣기는 사용자 로캘로 해석한다 (`1.234,5` vs `1,234.5`).

## Minus 재고 표시 (Spec 4.9 개정)
- 재고 판정·셀 오류·저장 차단은 **앱**이 한다. 그리드는 판정하지 않는다.
- 그리드는 앱이 `SetCellMark(row, field, CellMark(Error|Warning, 리소스 키))`로 지정한 셀을 색과 툴팁으로 그린다.

## 합계 · 계산
- 합계(`summary`)는 그리드 기능. 행 변경 시 재계산.
- 그리드는 식을 평가하지 않는다 (A-44 ④·A-59). 저장할 계산(금액)은 앱이 `OnCellValueChanged`에서 한다. 표시 전용 계산 열은 앱 함수 `Compute`, 조건부 스타일은 앱 함수 `CellStyle`.

## 이벤트 API
`OnCellValueChanging`(취소 가능), `OnCellValueChanged`, `OnRowAdding`/`OnRowAdded`, `OnRowDeleting`(취소 가능), `OnPopupRequested`, `OnPasting`(취소 가능), `OnSelectionChanged`

## v1 범위 외
그리드가 평가하는 셀 수식, 피벗, 인쇄 레이아웃, 본문 셀 병합, 행 끌어 놓기 순서 변경. 요청이 오면 Spec 범위 밖임을 알린다. 트리 그리드는 v1 포함 (A-61).

## 성능 목표 (초안, 벤치로 확정)
| 항목 | 목표 |
|---|---|
| 셀 이동 키 반응 | 서버 왕복 없이 즉시 |
| 셀 편집 커밋 왕복 | 150ms 이하 (사내망) |
| 10,000행 조회 후 첫 화면 | 1초 이하 (서버 페이징) |
| 500행 × 10열 붙여넣기 | 2초 이하, 서버 호출 1회 |

성능에 영향을 주는 변경은 벤치 결과를 PR에 첨부한다.
