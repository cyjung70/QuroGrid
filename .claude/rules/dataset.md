---
paths:
  - "src/Quro.Grid.Data/**"
  - "tests/Quro.Grid.Data.Tests/**"
---

# QuroDataSet 규칙

`Quro.Grid.Data`는 Blazor·MudBlazor·JS를 참조하지 않는다. UI 없이 단위 테스트가 가능해야 한다.

| 상태 | 규칙 |
|---|---|
| `Normal` | 값 변경 → `Updated` |
| `Inserted` | 값 변경해도 `Inserted` 유지. 삭제 시 목록에서 즉시 제거 (서버 전송 안 함) |
| `Updated` | 모든 값이 원본과 같아지면 `Normal` 복귀 |
| `Deleted` | 저장 시 DELETE. 화면 처리는 그리드 옵션, 기본 숨김 (O-02) |

- 필수 API: `GetState`, `GetOriginal`, `SetValue`, `AddRow`, `AddRows`(일괄, R3), `DeleteRow`, `RevertRow`, `Validate`, `GetChanges`, `AcceptChanges`, `RejectChanges`
- 저장 payload: `{ inserted[], updated[], deleted[] }`. 수정·삭제 행은 `version` 포함
- `SaveAsync(http, url)`: 2xx → `AcceptChanges`, 409 → `Conflict`, 그 밖 → `Failed` (변경분 유지). 성공 후 재조회는 앱이 한다
- URL·엔드포인트 이름은 앱(QuroERP)이 넘긴다. 이 프로젝트에 하드코딩하지 않는다
- 서버 저장은 한 트랜잭션에서 삭제 → 수정 → 추가 순서, `version` 불일치 시 충돌 (저장 API 쪽 책임)
- `AcceptChanges()`는 저장 성공 응답을 받은 뒤에만 호출한다
- 스키마(`DataSetSchema`: 필드명·타입·키·필수·최대 길이)는 화면 정의에서 잘라 받을 수 있지만, 이 프로젝트는 `Quro.Grid.Definition`을 참조하지 않는다 (방향: Definition → Data)
