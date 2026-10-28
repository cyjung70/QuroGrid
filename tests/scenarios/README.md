# QuroGrid 시나리오 목록 (GS-xxx)

- 기준: `docs/QuroGrid_FeatureMatrix_Spec_v1.pdf` (Draft v0.1, 2026-10-28)
- 문서 상태: 정리본 v0.3 (2026-10-28, GS-007~009 결정 반영) · 스테이블영 검토 대상
- 테스트 이름은 시나리오 ID로 시작한다: `GS_004_ExcelPaste_3x4` (CLAUDE.md 3장)

Spec 5.1절의 목록은 **발췌 10건**이고, Phase 0 완료 기준은 **시나리오 50건 이상**(Spec 6장)입니다.
그래서 이 문서는 두 부분으로 나뉩니다.

1. **확정 시나리오**: Spec 5.1절에 ID가 있는 GS-001~GS-010
2. **후보 시나리오**: Spec 3장(P0 기능)·4장 본문에서 도출했지만 아직 ID가 없는 항목. 승인 후 GS 번호를 매깁니다.

---

## 1. 확정 시나리오 (Spec 5.1)

| ID | 시나리오 | 기대 결과 | 우선 | 테스트 계층 | Spec 근거 | 테스트 이름 (안) |
|---|---|---|---|---|---|---|
| GS-001 | 조회 행의 수량을 바꾼 뒤 원래 값으로 되돌림 | 행 상태 `Normal` 복귀 | P0 | 단위 (xUnit) | 4.4 | `GS_001_RevertQty_ReturnsToNormal`, `GS_001_RevertOneOfTwoFields_StaysUpdated` |
| GS-002 | 추가한 행을 저장 전에 삭제 | 목록에서 제거, 변경분에 없음 | P0 | 단위 (xUnit) | 4.4 | `GS_002_DeleteInsertedRow_NotInChanges`, `GS_002_DeleteInsertedRow_KeepsOtherRows` |
| GS-003 | 마지막 편집 셀에서 Enter | 옵션에 따라 다음 행 첫 셀 또는 새 행 | P0 | E2E (Playwright) | 4.7 | `GS_003_EnterOnLastCell_MovesByOption` |
| GS-004 | 엑셀에서 3행 × 4열 복사 후 붙여넣기 | 값 반영, 부족 행 자동 추가, 서버 호출 1회 | P0 | E2E + 컴포넌트 | 4.7, R3 | `GS_004_ExcelPaste_3x4` |
| GS-005 | 읽기 전용 열이 포함된 범위 붙여넣기 | 읽기 전용 셀은 건너뛰고 안내 | P0 | E2E + 컴포넌트 | 4.5, 4.7 | `GS_005_PasteOverReadOnly_SkipsAndNotifies` |
| GS-006 | Popup 셀에 코드 일부 입력 후 Tab | 1건 일치: 자동 확정 / 복수: 팝업 | P0 | E2E | 4.6, 4.10 | `GS_006_PopupTab_SingleMatch_AutoCommits` 외 |
| GS-007 | 독일어 로캘에서 `1.234,5`·`28.10.2026` 붙여넣기 | `1234.5`, `2026-10-28`로 저장 | P0 | 단위 + E2E | 4.8 | `GS_007_PasteDeLocale_ParsesDecimal`, `GS_007_PasteDeLocale_ParsesDate` 외 |
| GS-008 | 앱이 Minus 재고로 판정한 셀 | 그리드가 앱이 지정한 표시(오류·경고 색, 툴팁)를 그림. 판정·저장 차단은 앱 | P0 | 단위 + 컴포넌트 | 4.9 (변경) | `GS_008_AppMarksMinusStock_MarkIsKept` 외 |
| GS-009 | 다른 사용자가 먼저 수정한 행 저장 | DataSet이 409를 충돌로 반환, 변경분 유지. 안내·재조회는 앱 | P0 | 단위 (가짜 HTTP) + E2E | 4.4 | `GS_009_SaveStaleVersion_Conflict` 외 |
| GS-010 | 편집 중 SignalR 연결 끊김 후 재연결 | 입력 잠금 → 재연결 후 미저장 변경 유지 | P0 | E2E | 4.3 R7 | `GS_010_Reconnect_LocksInput_KeepsChanges` |

### 시나리오별 확인 사항

- **GS-003** — **O-05 결정(2026-10-28): 기본 방향 오른쪽**, 아래쪽은 옵션. 또 "마지막 셀에서 새 행을 추가할지"를 정하는 옵션이 Spec에 정의되어 있지 않습니다. 테스트는 방향 옵션 2가지 × 새 행 옵션 조합을 매개변수로 돌리고, 기본값(오른쪽)을 별도로 검증합니다.
- **GS-004** — "부족 행 자동 추가"는 Spec 4.7에서 **옵션**입니다. 옵션 ON/OFF를 모두 검증합니다. "서버 호출 1회"는 JS Interop 호출 횟수로 측정합니다.
- **GS-005** — "안내"의 형태(토스트/툴팁/메시지 바)가 Spec에 없습니다. 메시지는 리소스 키로 내보내는 것만 검증하고 표시 방식은 화면 쪽에 맡기는 안을 제안합니다.
- **GS-006** — Spec 기대 결과에 **0건 일치**가 빠져 있습니다. Spec 4.6("미일치·복수 일치 시 팝업")과 CLAUDE.md 7장에 따라 0건도 팝업으로 보고, 테스트를 3개(1건/0건/복수)로 나눕니다.
- **GS-007** — **결정(2026-10-28): 날짜 포함.** 해석은 DataSet `TrySetText(row, field, text, culture)`로 단위 테스트하고, 붙여넣기 흐름은 E2E로 검증합니다. 해석 실패 시 값은 바뀌지 않고 `false`를 돌려줍니다 (안내 방식은 GS-005와 함께 정함).
- **GS-008** — **결정(2026-10-28): 셀 오류 판정은 그리드가 아니라 앱이 한다. 그리드는 Minus 표시만 한다.** 앱이 `SetCellMark(row, field, CellMark(Error|Warning, 리소스 키))`로 지정하고 그리드는 색·툴팁으로 그립니다. 저장 차단도 앱이 합니다. DataSet 부분은 단위 테스트로 끝났고, 그리드 표시는 Phase 2 bUnit에서 검증합니다. **Spec 4.9("그리드 검증 훅", OFF 시 그리드가 저장 차단)와 다릅니다.**
- **GS-009** — **결정(2026-10-28): 저장 API는 QuroGrid와 분리된 서버이며, DataSet이 저장 API와 통신한다.** `SaveAsync(http, url)`이 변경분을 POST하고, 2xx면 `AcceptChanges` 후 앱이 재조회합니다(응답 본문은 쓰지 않음). 409는 `Conflict`, 그 밖은 `Failed`로 돌려주며 변경분은 유지합니다. 버전 충돌 판정 자체는 저장 API 쪽 테스트 범위입니다.
- **GS-010** — "미저장 변경 유지"는 **같은 회로로 재연결**된 경우만 해당합니다. 회로가 소멸된 경우는 O-03(잠정: 보관 안 함, 경고만) 범위이므로 별도 시나리오로 둡니다(후보 C-38).

## 1-2. 확정 시나리오 — QuroDataSet (후보 C-01~C-10, 2026-10-28 승인)

모두 단위 테스트(xUnit), 우선순위 P0입니다.

| ID | (후보) | 시나리오 | 기대 결과 | Spec 근거 | 테스트 이름 |
|---|---|---|---|---|---|
| GS-011 | C-01 | `Normal` 행의 값 변경 | `Updated`, `GetOriginal`은 원래 값 | 4.4 | `GS_011_ChangeNormalRow_BecomesUpdated_KeepsOriginal` |
| GS-012 | C-02 | `Inserted` 행의 값 변경 | `Inserted` 유지 | 4.4 | `GS_012_ChangeInsertedRow_StaysInserted` |
| GS-013 | C-03 | `Normal` 행 삭제 | `Deleted`, 변경분 `deleted[]`에 포함 | 4.4 | `GS_013_DeleteNormalRow_MarkedDeleted_InChanges` |
| GS-014 | C-04 | `RevertRow` | `Updated`·`Deleted`: 모든 값이 원본으로, `Normal` / `Inserted`: 행 제거 | 4.4 | `GS_014_RevertUpdatedRow_RestoresOriginal` 외 2건 |
| GS-015 | C-05 | 여러 행 변경 후 `RejectChanges` | 전체 원복, 추가 행 제거, 삭제 행 복귀 | 4.4 | `GS_015_RejectChanges_RestoresAll` |
| GS-016 | C-06 | 추가·수정·삭제가 섞인 상태에서 `GetChanges` | 상태별 분류, 수정·삭제 행에 `version` 포함, 추가 행은 임시 키(음수) | 4.4 | `GS_016_GetChanges_Mixed_GroupsByState` |
| GS-017 | C-07 | `AcceptChanges` 호출 | 모든 행 `Normal`, 원본값 갱신, `Deleted` 행 제거 | 4.4 | `GS_017_AcceptChanges_CommitsAll` |
| GS-018 | C-08 | 필수 필드가 빈 행에 `Validate` | 해당 셀 오류, 결과 실패 | 4.4, 4.5 | `GS_018_Validate_RequiredEmpty_Fails` 외 1건 |
| GS-019 | C-09 | `Min`·`Max` 범위 밖 숫자에 `Validate` | 해당 셀 오류 | 4.5, 4.6 | `GS_019_Validate_OutOfRange_Fails` |
| GS-020 | C-10 | 저장 값 직렬화 | 날짜는 ISO, 소수점은 `.` (로캘 무관) | 4.8 | `GS_020_ChangeSetJson_IsCultureInvariant` |

**결정 사항 (2026-10-28)**
- 검증 규칙은 **DataSet 규칙 API**(`SetRule(field, FieldRule)`)로 등록합니다. 그리드는 `QuroColumn`의 `Required`·`Min`·`Max`를 이 API로 넘깁니다.
- `Inserted` 행에 `RevertRow`를 호출하면 **행을 제거**합니다 (`DeleteRow`·`RejectChanges`와 같은 결과).

---

## 2. 후보 시나리오 (Spec 본문에서 도출, 미승인)

ID는 임시(`C-xx`)입니다. 승인되면 다음 GS 번호부터 순서대로 매깁니다. C-01~C-10은 GS-011~GS-020으로 확정되었습니다(1-2절).

### B. QuroGrid 렌더링 — 컴포넌트 (bUnit)

| ID | 시나리오 | 기대 결과 | 근거 |
|---|---|---|---|
| C-11 | `HeaderKey`로 정의한 헤더를 ko/en으로 렌더링 | 리소스 문자열 표시, 하드코딩 없음 | 4.8 |
| C-12 | `Currency` 컬럼, 통화 KRW / USD | 소수 0자리 / 2자리 (로캘과 무관) | 4.6, 4.8 |
| C-13 | `Number` 컬럼 `Format="N3"`, 사용자 로캘 de-DE | `1.234,500` 형식 표시 | 4.5, 4.8 |
| C-14 | `Aggregate.Sum` 컬럼 + `ShowFooter` | 합계 행에 합계 표시, 값 변경 시 갱신 | 3-A, 4.5 |
| C-15 | `FrozenColumns="2"` | 앞 2개 열이 고정 영역에 렌더링 | 3-A, 4.5 |
| C-16 | 한 행의 값 변경 | 그 행만 다시 렌더링 (다른 행 렌더 횟수 불변) | R4 |
| C-17 | 그리드 마크업 검사 | 셀 단위 이벤트 핸들러 없음, ARIA `grid` 역할 적용 | R5, 3-E |
| C-18 | 편집 시작 | 에디터는 편집 중인 셀 하나에만 존재 | 3-B |
| C-19 | `ReadOnly` 컬럼 편집 시도 | 에디터 열리지 않음 | 4.5 |
| C-20 | 조건부 서식: Minus 재고 ON에서 초과 수량 | 셀 경고 표시 + 툴팁, 저장 허용 | 4.9 |
| C-21 | `OnCellValueChanging`에서 취소 | 값 반영 안 됨, `OnCellValueChanged` 미발생 | 4.10 |
| C-22 | `OnCellValueChanged`에서 금액 = 수량 × 단가 재계산 | 금액 셀·합계 갱신 | 4.10 |
| C-23 | `OnRowDeleting`에서 취소 | 행 유지 | 4.10 |
| C-24 | `OnPasting`에서 취소 | 값 반영 없음, 서버 호출 없음 | 4.10 |
| C-25 | `OnRowAdding`에서 기본값 세팅 | 새 행에 기본값 반영, `OnRowAdded` 발생 | 4.10 |
| C-26 | 삭제 표시 행 화면 처리 옵션 | 기본 숨김, 옵션으로 취소선 (O-02 잠정안) | 4.4, O-02 |

### C. 키보드·선택·클립보드 — E2E (Playwright) · Spec 4.7

| ID | 시나리오 | 기대 결과 | 근거 |
|---|---|---|---|
| C-27 | 화살표로 셀 이동 | 서버 왕복 없이 이동 (SignalR 메시지 0건) | 4.7, R1 |
| C-28 | 편집 중 키 입력 | 커밋 전 서버 전송 0건, 셀 이탈 시 1회 | R2 |
| C-29 | Tab / Shift+Tab | 다음/이전 **편집 가능** 셀, 행 끝에서 다음 행 첫 셀 | 4.7 |
| C-30 | F2로 편집 시작 | 기존 값 유지, 커서 끝 | 4.7 |
| C-31 | 문자 입력으로 편집 시작 | 기존 값 덮어쓰기 | 4.7 |
| C-32 | 편집 중 Esc | 원래 값 복원, 서버 전송 없음 | 4.7 |
| C-33 | Shift+화살표 / 마우스 드래그 | 범위 선택 표시 | 4.7 |
| C-34 | 범위 선택 후 Ctrl+C | 클립보드에 탭·줄바꿈 형식 | 4.7 |
| C-35 | Insert / Ctrl+Delete | 행 추가 / 행 삭제 | 4.7 |
| C-36 | Popup 셀에서 F4 | 코드도움 팝업, `OnPopupRequested`에 입력 문자열 전달 | 4.7, 4.10 |
| C-37 | 여러 행 선택 후 일괄 삭제 / 행 복사 | 서버 호출 1회 | R3 |
| C-38 | 회로 소멸 후 재접속 | 미저장 변경 경고 (브라우저 보관 안 함, O-03 잠정안) | R7, O-03 |
| C-39 | 미저장 변경이 있는 상태에서 페이지 이탈 | 경고 표시 | R7 |

### D. 조회·출력

| ID | 시나리오 | 기대 결과 | 계층 | 근거 |
|---|---|---|---|---|
| C-40 | 헤더 클릭 / Shift+클릭 | 단일·다중 컬럼 정렬 | 컴포넌트 + E2E | 3-D |
| C-41 | 10,000행 조회 | `ItemsProvider`로 보이는 범위만 적재, 첫 화면 1초 이하 | E2E + 성능 | 3-C, 4.11 |
| C-42 | 스크롤 이동 후 변경 | DataSet에 적재 범위 + 변경 행만 유지 | 단위 + 컴포넌트 | R6 |
| C-43 | 컬럼 경계 드래그 | 리사이즈 (JS 층 처리, 서버는 완료 시점만) | E2E | 3-A, R1 |
| C-44 | 엑셀 내보내기 | 서버에서 .xlsx 생성, 헤더는 사용자 언어 | API 통합 (생성 위치 재검토) | 3-E |

### E. 저장 API — 이 저장소 범위 밖 (2026-10-28 결정)

저장 API는 QuroGrid와 분리된 서버입니다. 아래 항목은 저장 API 쪽에서 검증하며, QuroGrid에는 GS-009의 DataSet 호출 동작만 남깁니다.

| ID | 시나리오 | 기대 결과 | 근거 |
|---|---|---|---|
| C-45 | 추가·수정·삭제가 섞인 변경분 저장 | 한 트랜잭션에서 삭제 → 수정 → 추가 순서 | 4.4 |
| C-46 | 트랜잭션 중간 실패 | 전체 롤백, 클라이언트는 `AcceptChanges` 호출 안 함 | 4.4 |
| C-47 | 동시 출고로 서버 재검사 시 재고 부족 (Minus OFF) | 저장 거부, 클라이언트 검증 통과와 무관 | 4.9 |

### F. 성능 — Playwright + 측정 스크립트 · Spec 4.11

| ID | 시나리오 | 목표 |
|---|---|---|
| C-48 | 셀 이동 키 반응 | 프레임 1개 이내 |
| C-49 | 셀 편집 커밋 왕복 | 150ms 이하 (사내망) |
| C-50 | 500행 × 10열 붙여넣기 | 2초 이하, 서버 호출 1회 |
| C-51 | 회로당 서버 메모리 | 측정 후 상한 설정 |

---

## 3. 정리

| 구분 | 건수 |
|---|---|
| 확정 (GS-001~020) | 20 |
| 후보 (C-11~C-51, C-45~C-47 제외) | 38 |
| 저장 API 쪽으로 이관 (C-45~C-47) | 3 |
| 합계 | 61 (QuroGrid 범위 58, Phase 0 기준 50건 이상) |

### 결정이 필요한 항목

1. ~~O-05 Enter 기본 방향~~ — **결정: 오른쪽** (2026-10-28)
2. GS-003 "마지막 셀 Enter 시 새 행 추가" 옵션을 Spec에 추가할지
3. GS-006에 0건 일치(팝업)를 넣는 것 — Spec 4.6과 맞추는 보완
4. ~~GS-007에 날짜 붙여넣기도 포함할지~~ — **결정: 포함** (2026-10-28)
5. 후보 C-11~C-51 승인 범위와 GS 번호 부여 (C-01~C-10은 승인됨)
6. ~~API 통합 테스트 프로젝트 추가~~ — **결정: 추가하지 않음.** 저장 API는 분리된 서버이고, DataSet 호출은 가짜 HTTP 핸들러로 단위 테스트 (2026-10-28)
7. GS-008 결정에 따른 Spec 4.9 개정 — 현재 Spec은 "그리드 검증 훅"으로 되어 있음
8. C-44 엑셀 내보내기를 어느 서버에서 만들지 (저장 API와 같은 서버인지)

### 착수 순서 (Spec 6장 로드맵 기준)

- **Phase 1 (DataSet)**: GS-001, GS-002, GS-007(해석), GS-008(셀 표시 보관), GS-009(저장 호출), GS-011~GS-020 — **완료**
- **저장 API (별도 서버)**: C-45~C-47, 버전 충돌 판정, Minus 재고 최종 판정
- **Phase 2 (Grid P0)**: 나머지 컴포넌트·E2E·성능 시나리오
