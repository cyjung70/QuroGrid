# QuroGrid Spec v1 보완 문서 (Addendum A)

- 대상: `docs/QuroGrid_FeatureMatrix_Spec_v1.pdf` (Spec v1 초안 Draft v0.2, 2026-10-28 — 4.9·5장 본문 개정)
- 문서 상태: A.26 (2026-10-31) · 스테이블영 결정 사항 반영
- 이 문서는 Spec v1의 일부입니다. **아래 항목은 Spec 본문보다 우선합니다.** 본문에 없는 내용은 "보완", 본문과 다른 내용은 "변경"으로 표시합니다.

---

## 1. 결정 사항

| ID | Spec 절 | 구분 | 내용 | 결정일 |
|---|---|---|---|---|
| A-01 | 4.7, 7.2 O-05 | 결정 | Enter 확정 후 기본 이동 방향은 **오른쪽**. 그리드 옵션 `EnterDirection`으로 아래쪽 선택 | 2026-10-28 |
| A-02 | 4.7 | 보완 | 마지막 편집 가능 셀에서 Enter 시 **새 행 추가**는 그리드 옵션 `AddRowOnLastEnter` | 2026-10-28 |
| A-03 | 4.7 | 보완 | 붙여넣기 시 **부족 행 자동 추가** 옵션 `AddRowsOnPaste`의 기본값은 **켬** | 2026-10-28 |
| A-04 | 4.5, 4.7 | 보완 | 붙여넣기 범위에 읽기 전용 셀이 있으면 **건너뛰고**, 그리드는 건너뛴 셀 목록과 사유 리소스 키를 `OnPasted` 이벤트로 앱에 넘긴다. **안내 표시 방식(토스트·메시지 바 등)은 앱이 정한다** | 2026-10-28 |
| A-05 | 4.6 | 변경 | Popup 셀 코드 입력 후 Tab: 1건 일치는 자동 확정, 복수 일치는 팝업, **0건 일치는 팝업을 띄우지 않는다** (본문: "미일치·복수 일치 시 팝업") | 2026-10-28 |
| A-06 | 4.8 | 보완 | 붙여넣기 로캘 해석은 숫자와 **날짜**에 모두 적용 (예: de-DE `28.10.2026` → `2026-10-28`) | 2026-10-28 |
| A-07 | 4.9 | 변경 | Minus 재고 판정·셀 오류·저장 차단은 **앱(윗단 애플리케이션)**이 한다. 그리드는 앱이 지정한 셀 표시(`CellMark`: 오류·경고 + 툴팁 리소스 키)만 그린다 (구 본문: "그리드 검증 훅"). **Spec 본문 4.9 개정 완료 (Draft v0.2)** | 2026-10-28 |
| A-08 | 4.2, 4.4 | 변경 | **저장 API는 QuroGrid와 분리된 서버**이며, QuroDataSet이 저장 API와 통신한다 (`SaveAsync`). 2xx면 `AcceptChanges`, 409는 충돌, 그 밖은 실패로 돌려주고 변경분을 유지한다. 저장 성공 후 키·버전은 **앱이 재조회**해 갱신한다 | 2026-10-28 |
| A-09 | 5 | 변경 | API 통합 테스트(xUnit + PostgreSQL)는 저장 API 쪽 범위. QuroGrid 저장소는 DataSet의 저장 호출을 가짜 HTTP 핸들러로 단위 테스트한다. **Spec 본문 5장 개정 완료 (Draft v0.2)** | 2026-10-28 |
| A-10 | 4.4, 4.5 | 보완 | 검증 규칙은 DataSet 규칙 API `SetRule(field, FieldRule)`로 등록하고, `QuroColumn`의 `Required`·`Min`·`Max`는 이 API로 넘긴다 | 2026-10-28 |
| A-11 | 4.4 | 보완 | `Inserted` 행에 `RevertRow`를 호출하면 행을 제거한다 | 2026-10-28 |
| A-12 | 3-B, R3 | 보완 | **행 복사**: 선택한 행을 같은 값의 새 행(`Inserted`)으로 복제하고, 새 행은 **각 원래 행 바로 아래**에 넣는다. 여러 행도 서버 호출 1회. 복사 행도 `OnRowAdding`을 거치며(`SourceRowKey`로 원본 전달) 앱이 키·버전 등을 정리한다 | 2026-10-28 |
| A-13 | 4.10 | 보완 | `OnSelectionChanged`는 **활성 행이 바뀔 때만** 발생한다 (같은 행 안의 셀 이동은 제외). 앱이 구독한 경우에만 서버로 보낸다. 활성 행이 멈춘 뒤 100ms에 보낸다 (여러 행을 빠르게 지나가면 마지막 행만) | 2026-10-28 |
| A-14 | 4.10 | 보완 | 이벤트 `OnRowDeleted` 추가 (`OnRowAdded`와 짝). 삭제 호출 1회에 한 번, 실제 삭제된 행 키 목록(취소 행 제외)을 넘긴다. 이유: `OnRowDeleting`은 삭제 전이라, JS에서 시작한 삭제(Ctrl+Delete) 뒤 앱이 화면(변경 수·합계 등)을 갱신할 시점이 없다 | 2026-10-28 |
| A-15 | 4.7 | 보완 | Insert 키는 **활성 행 바로 아래**에 새 행을 추가하고, 새 행의 첫 편집 가능 셀로 이동한다 (마지막 셀 Enter 행 추가(A-02)는 목록 끝) | 2026-10-28 |
| A-16 | 4.6, 4.10 | 보완 | **Popup 셀 판정은 그리드가 한다.** 컬럼 `Lookup`(입력 문자열 → 일치 코드 목록)을 앱이 넘기고, 확정(Tab·Enter·셀 이탈) 시 1건이면 그 코드로 자동 확정, 여러 건이면 `OnPopupRequested`, 0건이면 팝업 없이 편집 유지(A-05). F4는 건수와 무관하게 `OnPopupRequested`. 앱은 팝업(MudDialog, 화면별 구현)에서 고른 코드를 `args.Value`로 돌려주고, 값이 없으면 확정하지 않는다 | 2026-10-28 |
| A-17 | 4.6, R2 | 변경 | **Date·Combo 직접 입력은 브라우저 에디터**(셀 이탈 시 1회 전송, R2 유지). 달력·목록은 Alt+↓로 열 때만 띄우고, 고르면 바로 확정한다. Spec 4.6의 Combo 에디터 `MudSelect`는 목록으로 대신한다. (달력·목록 구현은 A-67로 변경: MudBlazor 대신 그리드 자체) Combo 직접 입력은 코드나 명칭(대소문자 무시) | 2026-10-28 |
| A-18 | 4.6 | 보완 | **Check 셀**: 셀 안 체크 표시, 클릭·Space로 토글(서버 호출 1회), 에디터 없음. 저장 값은 컬럼 `CheckedValue`·`UncheckedValue`(기본 true/false, Y/N 매핑 가능). 복사는 저장 값, 붙여넣기는 매핑 값이나 true/false | 2026-10-28 |
| A-19 | 4.6, 4.10 | 보완 | **Button 셀**: 문구는 `TextKey` 리소스 키, 바인딩 값 없음(Field는 식별자), 편집·붙여넣기 대상 아님. 클릭·Enter·Space → 이벤트 `OnCellButtonClick`(행 키·필드·행 객체). 삭제 표시 행에서는 발생하지 않음 | 2026-10-28 |
| A-20 | 4.3 R7 | 보완 | **페이지 이탈 경고**: 그리드 옵션 `ConfirmLeave`(기본 켬). 미저장 변경(`DataSet.HasChanges`)이 있으면 창 닫기·새로고침은 브라우저 기본 경고, 앱 안 이동(링크·`NavigateTo`)은 확인창(문구 리소스 키 `quro.leave.unsaved`). `OnLeaving`을 구독하면 확인창 대신 앱이 묻는다(`Cancel`로 이동 막음) | 2026-10-28 |
| A-21 | 4.3 R7, O-03 | 보완 | **연결 끊김 안내**: 끊김 중 그리드 입력 잠금(키·마우스·붙여넣기, 편집 중 값은 유지) + 안내(`quro.connection.offline`), 미저장 변경이 있으면 유실 경고(`quro.connection.unsaved`). 같은 회로로 재연결되면 잠금 해제·변경 유지. 회로가 소멸해 다시 열린 화면에는 `quro.connection.changes_lost` 안내(누르면 닫힘). 브라우저에는 **표시(플래그)만** sessionStorage에 두고 데이터는 보관하지 않음 (O-03 잠정안과 일치) | 2026-10-28 |
| A-22 | 3-D | 보완 | **정렬**: 그리드가 적재된 행을 메모리에서 정렬(화면 순서만, DataSet 순서·상태는 그대로). 헤더 클릭 = 그 컬럼만 오름 → 내림 → 해제, Shift+클릭 = 2차·3차 추가·순환. 헤더에 ▲▼와 순번(다중일 때). 정렬 중 값을 고치거나 행을 추가해도 **위치 유지**(추가 행은 넣은 자리), 다시 조회(Load)하면 재정렬. 서버 페이징(4-2)에서는 정렬 조건을 조회로 넘긴다 | 2026-10-28 |
| A-23 | 3-A, R1 | 보완 | **컬럼 리사이즈**: 헤더 오른쪽 경계 드래그. 드래그 중에는 브라우저에서만 그리고, 끝날 때 서버 1회 → `OnColumnResized(필드, 너비)`. 최소 40px. 더블클릭 자동 맞춤·레이아웃 저장은 P1 | 2026-10-28 |
| A-24 | 3-A, 3-C, R6, O-04 | 보완 | **서버측 페이징**: O-04 = **화면별 설정**. 그리드 파라미터 `ItemsProvider`(요청: 시작 행·건수·정렬 → 결과: 행·전체 건수·합계)를 주면 Blazor `Virtualize`로 보이는 범위만 `PageSize`(기본 100) 단위로 조회·적재하고, 범위에서 먼 페이지는 놓는다(변경 행은 유지). DataSet은 행 ID 선택자로 만든다(`QuroDataSet(idSelector)`, `LoadPage`·`Release`). **합계** = 서버 합계 + 저장 전 변경분(수정 차이, 추가 행 값, 삭제 행 원래 값). **붙여넣기**가 적재되지 않은 행까지 닿으면 붙여넣기 호출 1회 안에서 필요한 범위를 조회한 뒤 채운다. **정렬**은 조회로 넘겨 다시 조회하고, 변경 행은 변경 값을 유지하며 **추가 행은 맨 위**에 모은다 | 2026-10-28 |
| A-25 | 4.7, R3, A-24 | 보완 | **페이징 중 일괄 삭제**: 선택 범위는 화면 위치(`aria-rowindex`)로 기억해, 끝 행이 화면 밖으로 나가도 범위가 유지된다. Ctrl+Delete·툴바 일괄 삭제는 위치 범위를 서버에 1회 보내고(`DeleteRowRange`), 서버가 적재되지 않은 범위를 조회한 뒤 삭제한다. 범위 선택·복사(Ctrl+C)는 그려진 행까지 (요청 범위: 일괄 삭제) | 2026-10-28 |
| A-26 | 4.11, R3, R4 | 보완 | **큰 붙여넣기·일괄 처리 성능**: 붙여넣기 값(JSON)이 16KB를 넘으면 JS가 값을 보관하고 서버가 스트림(`IJSStreamReference`, 최대 20MB)으로 가져간다. 서버 호출은 그대로 1회. 이유: Blazor Server SignalR 메시지 기본 한도(32KB)를 넘으면 붙여넣기가 버려지고 회로가 끊길 수 있음 (500행 × 10열 ≈ 100KB). 붙여넣기·여러 행 삭제·행 복사 중에는 그리드를 다시 그리지 않고 끝날 때 한 번 그린다. 셀마다 값 변경 이벤트는 그대로 발생 | 2026-10-28 |
| A-27 | 4.11 | 결정 | **회로당 서버 메모리 상한 100MB** (GS-058). 측정: 대량 조회 화면(10,000행 페이징, 중간까지 스크롤) 회로당 관리 힙 증가 약 1.5MB | 2026-10-28 |
| A-28 | 3-E, 4.11 | 결정 | **엑셀 내보내기(.xlsx)는 저장 API 서버가 만든다** (GS-054). 그리드는 현재 화면의 컬럼 정보만 준다: `GetExportRequest()` → `GridExportRequest(Columns, Sort, Culture)`. 컬럼은 화면 순서이고, 필드·헤더 키·**사용자 언어 헤더**·셀 타입·서식·너비(리사이즈 반영)·통화 필드·집계·Combo 목록을 담는다. Button 셀은 뺀다. 앱이 조회 조건과 함께 저장 API에 보내고 파일 다운로드를 처리한다. 셀 타입·집계·정렬 방향은 JSON에 이름으로 쓴다 | 2026-10-28 |
| A-29 | 3-E, A-28 | 결정 | 엑셀 내보내기 데이터는 저장 API가 조회 조건으로 다시 조회한 **저장된 데이터**. 화면의 저장 전 변경분은 넣지 않는다 (그리드는 행을 보내지 않음) | 2026-10-28 |
| A-30 | 7.2 O-01~O-03 | 결정 | 오픈 이슈를 잠정안대로 확정. **O-01** MVP Undo는 행 단위 변경 취소(`RevertRow`)만 (셀 단위 Undo/Redo는 P1). **O-02** 삭제 표시 행은 그리드 옵션 `DeletedRows`, 기본 숨김 (취소선 선택, 페이징 중은 A-24). **O-03** 미저장 변경은 브라우저에 보관하지 않고 경고만 (A-21의 표시 플래그만 보관) | 2026-10-29 |
| A-31 | 3-D (P1) | 결정 | **헤더 필터 주체**: 메모리 모드는 그리드가 거르고, 페이징 모드는 조건을 `ItemsProvider` 요청에 담아 서버가 거른다 (정렬 A-22·A-24와 같은 분담). 편집·추가 행은 조건과 관계없이 유지 | 2026-10-29 |
| A-32 | 3-D (P1) | 결정 | **그룹핑·소계**는 메모리 모드에서만 그리드가 그린다 (그룹 머리 행·소계·접기). 페이징 중에는 끈다 (조회 집계는 SQL) | 2026-10-29 |
| A-33 | 3-B (P1), O-01 | 결정 | **Undo/Redo (Ctrl+Z / Ctrl+Y)**: 셀 값 변경·붙여넣기·일괄 삭제·행 추가를 각각 한 단계로 되돌린다. 저장 성공(`AcceptChanges`)·다시 조회(`Load`) 때 이력을 비우고, 이력은 50단계 | 2026-10-29 |
| A-34 | 3-E (P1), A-28 | 결정 | **CSV 내보내기**도 저장 API 서버가 만든다. 그리드는 A-28 요청에 형식(`csv`)만 더한다. 데이터는 A-29와 같이 저장된 데이터 | 2026-10-29 |
| A-35 | 3-E, A-22·A-23 (P1) | 보완 | **헤더 키보드·자동 맞춤·ARIA (GS-064~GS-066)**: 본문에서 Ctrl+↑(어느 행에서나) 또는 첫 행에서 ↑ → 같은 열 헤더. 헤더에서 ←→ 이동(서버 호출 없음), Enter 정렬·Shift+Enter 다중 정렬(클릭과 같은 순환), ↓ 본문 첫 행 같은 열. 헤더 경계 더블클릭은 헤더·**그려진** 셀·합계 중 가장 긴 내용에 맞춤(최소 40px), 바뀌면 `OnColumnResized` 1회. 그리드 `aria-rowcount` = 헤더 + 본문 행(페이징이면 서버 전체 건수 + 추가 행) + 합계 행, 활성 셀·선택 범위·활성 헤더에 `aria-selected` | 2026-10-29 |
| A-36 | 3-A, R1 (P1) | 보완 | **컬럼 숨김·이동·레이아웃 (GS-067~GS-069)**: 앱이 `SetColumnVisible(필드, 보임)`으로 숨김 (화면·합계·키보드·붙여넣기·내보내기에서 빠지고 값·행 상태는 그대로, 다시 보이면 제자리). 헤더를 5px 넘게 끌면 이동: 드래그 중은 브라우저만, 놓을 때 서버 1회 → `OnColumnMoved(필드, 화면 위치)`. 고정 열 영역과 일반 영역 사이는 넘지 않고 영역 끝으로 맞춤. `GetLayout()` → `GridLayout(Columns[필드·리사이즈 너비·숨김], Sort)`, `ApplyLayout(layout)`. 저장 위치는 앱·저장 API | 2026-10-29 |
| A-37 | 3-A (P1) | 보완 | **다단 헤더 (GS-070)**: `<QuroColumnGroup HeaderKey>`로 컬럼(과 그룹)을 묶으면 그 위에 그룹 헤더 행을 그린다. 그룹 칸 너비 = 묶인 **보이는** 컬럼 너비의 합 (리사이즈·숨김 반영, 모두 숨기면 칸 없음). 그룹 없는 컬럼 위는 빈 칸. 헤더 행 수만큼 `aria-rowindex`가 밀리고 그리드에 `data-header-rows`. 그룹 안 컬럼은 그 그룹 안에서만 이동, 그룹을 가르는 이동은 하지 않음 | 2026-10-29 |
| A-38 | 3-E, 4.2 (P1) | 보완 | **테마·다크 모드 (GS-080·GS-081)**: 그리드 색은 CSS 변수로만 정의. 앱은 `--quro-*`(bg·text·border·head-bg·active·selected·error·error-line·warning·warning-line·check-line·check-mark·shadow)를 그리드나 바깥 요소에 두어 바꾼다. 없으면 기본값 (MudBlazor 팔레트 대체값은 A-67로 제거, 다크 모드는 앱이 다크 값의 토큰을 둔다). 선택·오류·경고 배경은 강조색을 배경에 섞어(`color-mix`) 글자 명암비 4.5:1 이상 | 2026-10-29 |
| A-39 | 4.4, A-33 (P1) | 보완 | **Undo/Redo 구현 (GS-076~GS-078)**: 이력은 QuroDataSet에 둔다 (`Undo`·`Redo`·`CanUndo`·`CanRedo`·`BeginUndoStep`). 단계마다 건드린 행의 전후 모습(값·상태·목록 위치)을 남겨 되살리므로 행 상태 규칙(6장)이 그대로 지켜진다. 그리드는 사용자 동작 하나(커밋 + 앱의 `OnCellValueChanged` 재계산, 붙여넣기, 일괄 삭제, 행 추가·복사, 체크 토글, 목록·코드도움 확정)를 한 단계로 묶는다. Ctrl+Z / Ctrl+Y·Ctrl+Shift+Z는 편집 중이 아닐 때 서버 1회, 편집 중에는 입력란 기본 Undo. `RevertRow`도 한 단계 | 2026-10-29 |
| A-40 | 3-D, A-31 (P1) | 보완 | **헤더 필터 구현 (GS-071~GS-073)**: 헤더 오른쪽 필터 버튼(Button 컬럼 제외) 또는 헤더에서 Alt+↓ → 헤더 아래 패널. 입력은 컬럼 타입별로 Text·Popup = 포함(대소문자 무시), Number·Currency = 최소·최대(사용자 로캘), Date = 시작·종료(양 끝 포함, 빈 날짜 제외), Combo·Check = 값 목록. 컬럼끼리 AND. 패널 안 입력은 브라우저에서만, 열기·적용(Enter·적용 버튼)·해제·닫기(Esc·바깥 클릭) 때 서버 1회. 해석 실패면 그 입력에 `aria-invalid`, 패널 유지. 필터 중인 헤더에 `data-filtered`. API: `Filters`, `SetFilters`·`ClearFilters`(이벤트 없음), `OnFilterChanged(Filters)`, `ColumnFilter(Field, Contains, Min, Max, From, To, Values)`. 메모리 모드는 적용 시점에 거르고 합계·`aria-rowcount`는 보이는 행 기준. 페이징 모드는 `GridItemsRequest.Filters`로 서버가 거르고 처음부터 다시 조회. 내보내기 요청(`GridExportRequest.Filters`)에도 담는다 | 2026-10-29 |
| A-41 | D-03 | 변경 | **라이선스: LGPL v3 → MIT.** 저장소 루트 `LICENSE.md`(MIT, Copyright (c) 2026 CHO Y. JUNG), 패키지 `PackageLicenseExpression` = `MIT`. 새 의존성은 MIT와 함께 배포할 수 있는 라이선스만(MIT·Apache 2.0·BSD 등). GPL 계열·상용은 계속 금지. Clean-room 규칙(2장)은 그대로 | 2026-10-29 |
| A-42 | 3-D, A-32 (P1) | 보완 | **그룹핑·소계 구현 (GS-074·GS-075, 2026-10-29 제안 승인)**: 앱이 `SetGroupBy(필드 목록)`으로 지정 (목록 순서 = 단 순서, 여러 단). `ClearGroupBy`·`GroupColumns`·`ExpandAll`·`CollapseAll`, 이벤트 없음. 그룹 머리 행 = "헤더: 값 (건수)" (리소스 키 `quro.group.header`, 빈 값 `quro.group.empty`, Combo는 명칭, Check는 체크·해제 문구), 그룹 끝에 소계 행 (`Aggregate.Sum` 컬럼, 첫 칸 `quro.group.subtotal`). 그룹 순서는 값 오름차순(Combo는 명칭, 그 컬럼을 내림차순 정렬 중이면 내림차순, 빈 값은 맨 뒤), 그룹 안은 현재 순서. 그룹을 정한 뒤 그룹 값을 편집해도 행은 옮기지 않고 소계만 갱신 (A-22와 같은 원칙), `SetGroupBy`·`Load` 때 다시 정함. 키보드: ↑↓가 그룹 머리 행에도 서고 소계 행은 건너뜀(서버 호출 없음), Space·Enter·클릭 펼침·접기, ← 접기, → 펼치기 (서버 1회). 머리 행·소계 행은 편집·붙여넣기·범위 선택·복사 대상이 아니고 `aria-rowcount`에는 센다. 페이징 중에는 `SetGroupBy`를 무시 (A-32). CSS `.quro-group-head-row`(`aria-expanded`, `data-group-level`), `.quro-subtotal-row` | 2026-10-29 |
| A-43 | 3-E, A-34 (P1) | 보완 | **CSV 내보내기 구현 (GS-079, 2026-10-29 제안 승인)**: `enum ExportFormat { Xlsx, Csv }` (JSON은 소문자 `"xlsx"`·`"csv"`), `GridExportRequest`의 마지막 필드 `Format`(기본 `Xlsx`), `GetExportRequest(ExportFormat format = Xlsx)`. 기존 `GetExportRequest()` 호출은 그대로 xlsx (GS-054 변화 없음). 컬럼·정렬·필터·로캘은 형식과 관계없이 같다. CSV의 구분자·인코딩(예: Excel용 UTF-8 BOM)·따옴표 처리는 저장 API가 정한다 | 2026-10-29 |
| A-44 | 신규 (화면 정의) | 결정 | **TOON 화면 정의 v0.2 (2026-10-29 스테이블영 확정)**: 설계서 `docs/QuroGrid_TOON_ScreenDef_v0.2.md`, Harness `tests/scenarios/GS-D.md` v0.2. QuroERP TO BE 설계서 v0.1의 스키마(`screen`·`toolbar`·`form`·`grids[]{columns,rules,validate}`)를 기준으로 한다. ① 그리드는 QuroGrid 유지 (TO BE 설계서 D1의 MudBlazor·Radzen 권고는 채택하지 않음, D-01). ② 파서·불변 모델·의미 검증·`FieldModeResolver`·규칙 식 평가는 `Quro.Grid.Definition` (I-02 해소). UI 무관이라 소비자 서버도 같은 패키지로 검증·재계산한다. ③ 저장 payload는 QuroDataSet 형식(`inserted`·`updated`·`deleted` + 행 `version`, Spec 4.4) 유지. TO BE 설계서의 `header + lines[{rowState}]`는 소비자가 변환한다. ④ `rules`의 `calc`·`sum` 식은 그리드가 평가하지 않는다. `RuleExpression`(화이트리스트 식, `decimal`, null = 0)을 앱이 `OnCellValueChanged`에서 쓴다 — 열 규칙이며 Spec의 "셀 수식 v1 범위 밖"과 다르다. ⑤ 레거시 변환기(xml2toon)·toonlint는 이 저장소에 두지 않는다 (QuroERP 쪽 비공개 도구, I-07). ⑥ 그리드 연결 공개 API는 `<QuroGrid Definition="@gridDef">` 파라미터 (consumer-contract 4장 `[미정]` 해소, 구현은 GS-D11부터). 그리드 갭 G-01~G-05(추가 행만 편집, 배타 범위, 클립보드 끄기, 열별 필터·정렬·그룹 허용, CSV 올리기)는 각각 공개 API 승인 후 GS로 진행 | 2026-10-29 |
| A-45 | 신규 (화면 정의 연결) | 결정 | **그리드 갭 공개 API (2026-10-29 스테이블영 승인)**: G-01 `QuroColumn.NewRowOnly` (추가 행에서만 편집, 기본 false). G-02 `FieldRule.MinExclusive`·`MaxExclusive`(bool)·`NotEqual`(decimal?) + 리소스 키 `quro.validation.minExclusive`·`maxExclusive`·`notEqual`. G-03 `QuroGrid.Clipboard` (복사·붙여넣기, 기본 true). G-04 `QuroColumn.Filterable`·`Sortable`·`Groupable` (기본 true). D-5는 PR 하나로 GS-D11~GS-D15. D-6(검증 CLI·패키지 배포)은 T-12 결정 후. 구현에서 더한 것(A-46에서 승인): `FieldRule.MessageKey` (`validate.msg`를 범위·ne 오류 키로), `QuroDataSet` 필드 이름을 대소문자 무시로도 찾음 (정의 id camelCase ↔ DTO PascalCase, 설계서 P2) | 2026-10-29 |
| A-46 | 신규 (화면 정의 연결 후속) | 결정 | **D-5 후속 결정 (2026-10-29 스테이블영)**: (1) `FieldRule.MessageKey` 유지. (2) `QuroDataSet` 필드 이름은 정확히 같은 이름이 먼저(대소문자 구분), 없을 때만 대소문자 무시로 찾고, 대소문자만 다른 속성이 둘 이상이면 정확한 이름만. (3) `initRows` 빈 행은 `QuroDataSet.AddBlankRow`로 넣는다. 추가할 때의 값 그대로인 동안은 변경이 아니다: `Validate`·`GetChanges`·`HasChanges`가 건너뛰고, `AcceptChanges` 뒤에도 추가 행으로 남는다. Undo 단계가 아니다. 값을 되돌리면(Undo 포함) 다시 손대지 않은 행. `AddRow`·Insert 키·행 복사는 그대로 변경. (4) 정의 열의 코드도움·목록은 `QuroGrid.LookupFor`((lookup id, 입력) → 코드 목록)와 `OptionsFor`(source → `ComboOption` 목록). 정의·파라미터가 바뀔 때만 열마다 한 번 만든다 (R4). (5) T-12 잠정안 채택: `dotnet quro-deflint <폴더>` .NET 도구 (D-6, GS-D16) | 2026-10-29 |
| A-47 | 신규 (G-05) | 결정 | **CSV 올리기 `QuroGrid.ImportRows(fields, rows)` → `ImportResult(AddedRowKeys, Skipped)` (2026-10-29 스테이블영 승인, GS-D17)**: 앱이 파일을 읽고 CSV 헤더를 필드 이름에 맞춘다 (그리드는 파일·CSV를 다루지 않는다). 행은 끝에 추가(페이징 중에는 맨 위, A-24), 행마다 `OnRowAdding`/`OnRowAdded`. 셀은 붙여넣기 경로: 그리드 `Culture`로 읽고 값 변경 이벤트, 읽기 전용·해석 실패·취소 셀은 건너뛰고 `PasteKeys`로 `Skipped`에. 모든 셀을 건너뛴 행도 남긴다. Undo 한 단계, 그리기 1회 (R3). `Clipboard`·`AddRowsOnPaste`와 무관하고 `OnPasting`/`OnPasted`는 발생하지 않는다 (호출하는 앱이 결과를 받음). 그리드 열이 아니거나 Button 열인 필드는 아무것도 바꾸기 전에 `ArgumentException` | 2026-10-29 |
| A-48 | 신규 (T-08) | 결정 | **코드도움 정의 `*.lookup.toon` v0.3 (2026-10-29 스테이블영 승인)**: 문서 `docs/QuroGrid_TOON_LookupDef_v0.3_Proposal.md`. (1) 코드도움 1개 = `{id}.lookup.toon` 1개. (2) 절 `lookup`(id·title·version·api·key·text·minChars·perm)·`params`·`queries`·`columns`. (3) 규칙 `action=lookup`의 대상 열은 같은 id의 결과 열에서 값을 받는다 (명시 매핑은 필요하면 v0.4). (4) 오류 V-33(구조)·V-34(카탈로그)·V-35(조회·인수)·V-36(대상)·V-37(정의 없음), 공개 API `LookupDefLoader`·`LookupDef` 레코드·`ScreenDefValidator(catalog, lookups)`·`Validate(LookupDef)` — 비파괴 추가, `IQuroDefinitionCatalog`는 그대로. (5) 범위는 파서·검증기·`quro-deflint`, 그리드 컴포넌트는 바꾸지 않음. 묶음 L-1~L-3 후 `0.3.0-alpha.1` | 2026-10-29 |
| A-49 | 신규 (소비자 계약 2·3장) | 결정 | **앱 데이터는 컴포넌트 파라미터로 (2026-10-29 스테이블영 결정 b1)**: (1) `AddQuroGrid` DI 등록과 `IQuroCodeListSource`·`IQuroQueryResolver` 인터페이스를 만들지 않는다. 공통코드 목록은 `OptionsFor`(직접 열은 `QuroColumn.Options`), 코드 조회는 `LookupFor`(직접 열은 `QuroColumn.Lookup`), 페이징 조회는 `ItemsProvider`, 문구는 `Localizer`. 앱 서비스의 이름·등록은 앱이 정한다. (2) 카탈로그 인터페이스 이름은 `IQuroDefinitionCatalog`로 확정. (3) 코드도움 정의 파일 이름은 `{id}.lookup.toon` 그대로 (A-48). 소비자 문서·파일이 이 이름을 따른다 |
| A-50 | 신규 (Control Spec) | 보완 | **QuroGrid & QuroDataSet Control Spec Draft v0.1 수신 (2026-10-30)**: 원문 `docs/QuroGrid_Control_Spec_Draft_v0.1.md`, 대조·질의 `docs/QuroGrid_Control_Spec_v0.1_Review.md`. 기존 결정과 같은 항목은 그대로, 충돌하지 않는 신규 항목(DataSet `LoadAsync`·`Clear`, 집계 Count·Avg·Min·Max, 열 정렬, 행 번호·행 상태 표시, 연속 같은 값 합쳐 보이기, 잘린 텍스트 툴팁)은 v1 백로그로 받는다. **충돌 항목(서버 페이징 A-24, 셀 식·조건부 스타일 A-44 ④, CSV 가져오기 A-47, 트리 그리드, 현재 행 공유 바인딩, 표시·편집 방식 분리, TOON `dataset` 절 등)은 질의 Q-01~Q-24 답변 전까지 기존 결정을 유지한다.** QuroUI 컨트롤(텍스트 박스·콤보·날짜·정적 텍스트)의 위치·구현 방식도 Q-03·Q-21 대기 | 2026-10-30 |
| A-51 | Control Spec 1·2, Q-01 | 결정 | **DataSet 현재 행·변경 알림 (여러 컨트롤 바인딩)**: `QuroDataSet`에 `CurrentRowKey`·`MoveTo(rowKey)`와 C# 이벤트 `CurrentRowChanged`·`ValueChanged`·`RowAdded`·`RowDeleted`·`Loaded`·`Saved`를 둔다 (UI 무관 유지). 그리드 활성 행 = DataSet 현재 행 (양방향). `OnSelectionChanged`(A-13)는 그대로이고 내부에서 `MoveTo`를 부른다. 페이징 중 놓인 행이 현재 행이면 행 키로 유지 | 2026-10-30 |
| A-52 | Control Spec 2.1, Q-02 | 결정 | **형식 있는 `QuroDataSet<T>` 유지** + 열 메타 API `Columns` (`DataSetColumn(Name, Type, Key, Required, Default, MaxLength, Scale)`). `T`에서 만들고 화면 정의로 보완한다. 기본값은 `AddRow` 때 채운다. 형식 없는 DataSet은 만들지 않는다. **보완 (2026-10-30 스테이블영 결정)**: 열 메타는 설명 정보이며 `Validate`는 `SetRule`만 본다(열 `Required`·`MaxLength` 검사는 QuroUI 바인딩 때 다시 정함). 그리드 연결 때 정의 `req`가 열 `Required`를 채운다(GS-D22, `false`는 지우지 않음). `MaxLength`·`Default`·`Scale`은 TOON v0.2에 키가 없어 정의에서 채우지 않는다(`fmt`는 표시용, T-13) | 2026-10-30 |
| A-53 | Control Spec 8~11, Q-03 | 결정 | **QuroUI 컨트롤(텍스트 박스·콤보·날짜·정적 텍스트·마스크)은 이 저장소의 새 패키지 `Quro.UI`** (RCL, NuGet). 의존 `Quro.UI` → `Quro.Grid.Data`. 그리드 셀 에디터와의 공통 부분은 나중에 정리 | 2026-10-30 |
| A-54 | Control Spec 2.2, Q-06 | 결정 | **정렬·필터는 그리드 화면 순서에 둔다** (A-22·A-31·A-40 유지, 다른 컨트롤에는 영향 없음). DataSet에는 정렬·필터 조건으로 행 키 목록을 돌려주는 조회 함수만 추가한다 (순서·상태는 바꾸지 않음) | 2026-10-30 |
| A-55 | Control Spec 3.1·6, Q-08 | 결정 | **서버 페이징(`ItemsProvider`, A-24) 유지.** Control Spec의 "v1 = 가상 스크롤만"은 "v1에서 서버 페이징을 요구하지 않음"으로 읽는다 | 2026-10-30 |
| A-56 | Control Spec 3.2, Q-09 | 결정 | **셀 타입은 `Type`(CellType) 하나 + `ReadOnly` 유지** (표시·편집 방식을 나누지 않음). 새 타입 `Mask`(+ 열 `Mask` 패턴)·`Image`·`Label`(정적) 추가. Number·Currency·Popup·Button 유지. Image 값은 URL 문자열, 클릭은 `OnCellClick` | 2026-10-30 |
| A-57 | Control Spec 3.2·9, Q-10 | 결정 | **콤보 DataSet 바인딩 추가 (비파괴)**: 열 `OptionsSource`(코드 DataSet)·`ValueField`·`TextField`, 연동 콤보는 `FilterField`(부모 값으로 거름). 기존 `Options`·`OptionsFor`(A-49)도 유지 | 2026-10-30 |
| A-58 | Control Spec 3.2·7(8), Q-11 | 결정 | **선택 즉시 편집은 그리드 옵션 `EditOnSelect`, 기본 끔.** 켜면 셀에 서는 즉시 브라우저 에디터가 열리고(서버 호출 없음, R2), 화살표는 에디터 밖으로 이동, Shift+화살표 범위 선택 유지 | 2026-10-30 |
| A-59 | Control Spec 3.3·7(2)(3), Q-14 | 결정 | **A-44 ④ 유지 — 그리드는 식을 평가하지 않는다.** 열 파라미터 `Compute`(행 → 값, 앱 함수, **표시 전용**: 편집·저장 안 함)와 `CellStyle`(행·값 → 스타일 클래스, 앱 함수; 글자색·배경·굵게만, 임의 CSS 금지)을 추가. TOON 식은 `Quro.Grid.Definition`이 함수로 만들어 넘긴다. 저장할 계산은 지금처럼 `rules`의 `calc` | 2026-10-30 |
| A-60 | Control Spec 4, Q-16 | 결정 | **TOON v0.2 이름 유지**, v0.3에서 비파괴 추가: `datasets[N]{id,api,...}`, 열 `align`·`frozen`, 형식(Q-19). Control Spec 이름 대응: `captionKey` → `label`, `editType` → `type`/`edit`, `visible` → `hidden`. 공개 저장소 예시의 `api`는 중립 이름 | 2026-10-30 |
| A-61 | Control Spec 6, Q-17 | 변경 | **트리 그리드를 v1에 포함** (구: v1 범위 외). 데이터 = 평면 행 + 부모 키 열(`ParentField`). 전체 적재만 (자식 지연 조회 없음). 트리 모드에서는 페이징·그룹핑 끔, 정렬은 형제 안에서. 편집은 자식 추가·삭제만, 끌어 옮기기 없음 | 2026-10-30 |
| A-62 | 4.10, A-13, A-51 | 변경 | **활성 행 알림은 항상 보낸다 (GS-085, 2026-10-30 스테이블영 결정 1)**: DataSet 현재 행(A-51)을 맞추기 위해 JS는 앱의 `OnSelectionChanged` 구독과 관계없이 활성 행이 멈춘 뒤 100ms에 서버로 1회 알린다 (구 A-13: "앱이 구독한 경우에만"). 같은 행 안의 이동·입력은 여전히 서버 호출 없음. 서버는 `DataSet.MoveTo` 후 구독한 경우에만 `OnSelectionChanged`. 반대로 앱(또는 다른 컨트롤)이 `MoveTo`하면 그리드가 렌더 반영 뒤 활성 셀을 그 행의 같은 열로 옮긴다: 포커스는 가져오지 않고, `OnSelectionChanged`는 내지 않으며, 편집 중이거나 화면에 없는 행(걸러짐·접힘·숨긴 삭제 행)이면 옮기지 않는다. 페이징 중 그려지지 않은 행은 그 위치로 스크롤한 뒤 옮긴다 | 2026-10-30 |
| A-63 | Control Spec 8, A-52·A-53, Q-21 | 결정 | **QuroUI 텍스트 박스 (2026-10-30 스테이블영 결정 1~4)**: ① 공개 API `QuroTextBox<TItem>`(`DataSet`·`Field`·`PlaceholderKey`·`Localizer`·`CharType`(`Any`·`Digits`·`Alpha`·`AlphaNumeric`)·`MaxLength`·`Password`·`AutoSelect`·`ReadOnly`), 문자열 열만, 필수·최대 길이 기본값은 열 메타. ② `Validate`가 열 메타 `Required`도 검사 (GS-089, A-52 보완을 뒤집음). `MaxLength`는 검사하지 않음. ③ 문자 거르기·전체 선택은 `quro-ui.js` (R2). ④ TOON v0.3 그리드 열 키는 폼 필드와 같은 이름 `max`·`default` + `scale` (구현은 컨트롤이 필요할 때). **Q-21 잠정안과 다름**: `MudTextField`는 키 입력마다 서버로 4프레임을 보내(R2 위반, GS-U04 측정) 텍스트 박스는 자체 `<input>` + MudBlazor CSS 변수로 만든다. `Quro.UI`는 지금 MudBlazor를 참조하지 않는다 | 2026-10-30 |
| A-64 | Control Spec 9, A-57·A-63, Q-23 | 결정 | **QuroUI 콤보 박스 (2026-10-30 스테이블영 결정 1~5)**: ① 공개 API `QuroComboBox<TItem, TCode>`(`DataSet`·`Field`·`OptionsSource`·`ValueField`·`TextField`·`FilterField`·`ParentField`·`EmptyKey`·`ShowCode`·`ListColumns`(`QuroListColumn(Field, HeaderKey)`)·`Multiple`·`Separator`·`PlaceholderKey`·`Localizer`·`ReadOnly`). ② 목록은 코드 DataSet만 (`ComboOption` 목록은 `Quro.Grid`에 있어 받지 않는다). ③ 부모 값이 바뀌어 자식 값이 목록을 벗어나면 null로 지움(`SetValue` 1회, Undo 가능), 행 이동·Load로는 지우지 않음. ④ 다중 선택 = 구분자 문자열 (Q-23 (a) 확정), `Separator` 기본 `\|`, 코드 목록 순서. ⑤ 자유 입력 없음: 고르지 않고 포커스를 잃으면 마지막 선택으로 돌아감. 거르기·화살표·Enter·Esc는 `quro-ui.js`(R2), 자체 input·목록 (A-63과 같은 이유로 MudSelect·MudAutocomplete를 쓰지 않음). 다중 선택 입력 칸은 읽기 전용(거르기는 단일 선택만) | 2026-10-30 |
| A-65 | Control Spec 10, A-53·A-63, Q-24 | 결정 | **QuroUI 날짜 선택기 (2026-10-30 스테이블영 결정 1~4)**: ① 공개 API `QuroDatePicker<TItem>`(`DataSet`·`Field`·`ToField`·`Month`·`Culture`·`PlaceholderKey`·`Localizer`·`ReadOnly`), `DateOnly`·`DateTime`(nullable 포함) 열, 월 모드는 문자열 열. ② Q-24 확정: 기간 = 열 2개(`Field` 시작, `ToField` 끝), 월 모드 = `yyyy-MM` 문자열, 표시는 culture 짧은 날짜(별도 설정 없음). ③ 입력: culture 짧은 날짜·`yyyy-MM-dd`·숫자만(culture 순서), 읽을 수 없으면 마지막 값으로 돌아감. ④ 끝이 시작보다 앞이 되는 변경은 거부(그 칸은 마지막 값). 달력은 `quro-ui.js`가 `Intl`로 그리고 달 이동은 서버 호출 없음(R2), 자체 input (A-63과 같은 이유로 `MudDatePicker`를 쓰지 않음) | 2026-10-30 |
| A-66 | Control Spec 11, A-53 | 결정 | **QuroUI 라벨·정적 텍스트 (2026-10-30 스테이블영 결정 1~3)**: ① 컴포넌트 2개 — `QuroLabel`(`TextKey`·`Localizer`, 라벨에 `TItem`을 강제하지 않으려고 분리)과 `QuroStaticText<TItem>`(`DataSet`·`Field`·`Format`·`Culture`). ② `Format`이 없으면 정수 천 단위 구분, 실수 천 단위 구분 + 열 메타 `Scale` 자릿수(없으면 값의 자릿수), `DateOnly`·`DateTime`은 culture 짧은 날짜, 그 밖은 culture `ToString()`. ③ 숫자 열은 `quro-ui-static-number`(오른쪽 정렬). 브라우저 스크립트 없음 | 2026-10-30 |
| A-67 | 4.2, 4.6, A-17·A-38, CLAUDE.md 4장 | 변경 | **MudBlazor 제거 (2026-10-31 스테이블영 결정 1~5)**: ① QuroGrid 저장소(패키지·샘플·테스트)에서 MudBlazor를 모두 뺀다. `Quro.Grid` 패키지는 MudBlazor를 참조하지 않는다. QuroERP 자체 사용은 QuroERP가 정한다 ② 그리드 셀 달력은 quro-grid.js가 브라우저에서 그린다(`Intl`, 문화권 달 이름·요일·주 시작 요일). 달 넘김은 서버 호출 없음(R2), 날짜를 고르면 `PickDate`(yyyy-MM-dd) 1회. Combo 목록은 그리드가 `role=listbox` 목록으로 그리고 항목 클릭은 JS가 받아 `PickOption` 1회(R5) ③ CSS의 `--mud-palette-*` 대체값을 지운다. 색은 `--quro-*` 토큰 → 기본값. 다크 모드는 앱이 다크 값의 토큰을 둔다(GS-081) ④ BREAKING(의존성): 0.5.0-alpha.1. `Quro.Grid`를 통해 MudBlazor를 받던 앱은 자체 `MudBlazor` 참조를 추가 ⑤ GS-090: `src`·`tests`에 MudBlazor 참조가 없음을 테스트로 지킨다 | 2026-10-31 |
| A-68 | 4.6, A-17·A-67 | 보완 | **셀 선택 목록 키보드 (GS-091, 2026-10-31 스테이블영 결정 2, QuroERP 요청)**: 달력·Combo 목록이 열려 있으면 화살표는 강조 이동 — 달력 ←→ 하루·↑↓ 한 주(달을 넘으면 그 달을 그림), 목록 ↑↓(끝에서 멈춤). Enter는 강조 항목을 고르고 바로 확정(이동 없음). 강조는 현재 값(달력은 값이 없으면 오늘)에서 시작. 강조 이동은 브라우저만(R2), 고를 때 서버 1회. Esc는 편집 취소, 글자·Alt·Ctrl·Shift 조합은 에디터가 받는다 | 2026-10-31 |
| A-69 | Control Spec 11, A-53·A-63·A-66 | 결정 | **QuroUI 마스크 박스 (2026-10-31 스테이블영 결정 1~5, 권고안 승인)**: ① 공개 API `QuroMaskBox<TItem>`(`DataSet`·`Field`·`Mask`·`PlaceholderKey`·`Localizer`·`AutoSelect`·`ReadOnly`), 문자열 열만 ② 마스크 문자 `0` 숫자·`A` 모든 언어의 글자(`QuroCharType.Alpha`와 같음)·`*` 글자나 숫자·`\` 다음 문자 고정·그 밖은 고정 문자 ③ 저장 값은 고정 문자를 뺀 입력 문자만(a) ④ 마스크 끝까지 채워야 확정, 덜 채우거나 맞지 않으면 마지막 값으로(날짜 선택기와 같음), 빈 칸은 null. 길이가 달라지는 값은 앱이 마스크를 고르거나 텍스트 박스 ⑤ `QuroStaticText.Mask`로 저장 값을 고정 문자와 함께 표시(맞지 않으면 그대로). 입력 중 고정 문자 넣기·맞지 않는 문자 버리기·커서 유지는 quro-ui.js(R2) | 2026-10-31 |

## 2. 이벤트 API 보완 (4.10)

| 이벤트 | 시점 · 용도 | 취소 |
|---|---|---|
| `OnPasted` (A-04) | 붙여넣기 후. 추가된 행 키, 건너뛴 셀(행 키·필드·사유 리소스 키) 전달 | — |
| `OnSelectionChanged` (A-13) | 활성 행 변경. 행 키와 행 객체 전달 | — |
| `OnRowDeleted` (A-14) | 행 삭제 후. 삭제 호출 1회에 한 번, 실제 삭제된 행 키 목록(취소 행 제외) | — |
| `OnPopupRequested` (A-16) | 코드도움 요청. 입력 문자열, 후보(Lookup 결과), 계기(`Commit`·`F4`) 전달. 앱이 `Value`에 고른 코드 | — (Value 없음 = 미확정) |
| `OnCellButtonClick` (A-19) | Button 셀 클릭. 행 키·필드·행 객체 | — |
| `OnSortChanged` (A-22) | 사용자가 헤더로 정렬을 바꾼 뒤. 정렬 목록(`SortColumn(필드, 방향)`, 비면 해제). 앱의 `SetSort`·`ClearSort`로는 발생하지 않음 | — |
| `OnColumnResized` (A-23) | 경계 드래그가 끝난 뒤. 필드·새 너비(px) | — |
| `OnFilterChanged` (A-40) | 사용자가 필터 패널에서 조건을 적용·해제한 뒤. 현재 필터 목록(컬럼마다 하나, 적용 순서). 앱의 `SetFilters`·`ClearFilters`로는 발생하지 않음 | — |
| `ItemsProvider` (A-24, 파라미터) | 페이징 조회 함수. `GridItemsRequest(StartIndex, Count, Sort, Filters)` → `GridItemsResult(Items, TotalCount, Totals)`. 시작 행은 PageSize의 배수 | — |
| `OnLeaving` (A-20) | 미저장 변경이 있을 때 앱 안 이동 전. 이동할 주소(`TargetLocation`) 전달 | `Cancel` |

사유 리소스 키: `quro.paste.readonly`(읽기 전용·삭제 표시 행), `quro.paste.invalid`(로캘 해석 실패), `quro.paste.cancelled`(`OnCellValueChanging`에서 취소)

## 3. 잠정 사항 (구현은 했으나 결정 대기)

| 항목 | 잠정안 |
|---|---|
| `AddRowOnLastEnter` 기본값 (A-02) | 끔 |
| Popup 0건 일치 시 입력값 (A-05) | 확정하지 않고 편집 상태 유지, 안내는 앱이 `CellMark`로 |
| Popup 빈 입력 (A-16) | 조회 없이 그대로 확정 (코드 지우기) |
| Popup 셀 붙여넣기·`Lookup` 없는 Popup 컬럼 (A-16) | 조회 없이 입력 문자열 그대로 |
| F4를 편집 중이 아닐 때 (A-16) | 셀에 표시된 값을 입력 문자열로 전달 |
| 선택 목록이 열린 채 그리드 밖을 누름 (A-17) | 편집·목록 유지 (Esc로 취소) |
| Button 셀 Tab 이동 (A-19) | Tab 순서에서 제외 (화살표·클릭으로 이동) |
| 연결 상태 감지 (A-21) | 앱의 `#components-reconnect-modal`(템플릿 기본 `ReconnectModal`)이 내는 `components-reconnect-state-changed` 이벤트로 감지. 이 요소가 없는 앱(기본 재연결 표시)에서는 잠금·안내가 동작하지 않음 |
| 링크 이동 확인 (A-20) | 향상된 탐색(enhanced navigation) 링크는 서버를 거치지 않아 JS가 클릭 때 확인. 같은 이동에 두 번 묻지 않도록 2초 안의 서버 확인은 생략 |
| 정렬 값 비교 (A-22) | 빈 값이 가장 작음(오름차순 맨 앞). 문자열은 표시 로캘·대소문자 무시. Combo는 명칭, Check는 저장 값. 같으면 조회 순서 |
| 헤더 키보드 정렬 (A-22) | 없음 (마우스만). 접근성 보완은 P1 |
| 그리드 밖에서 추가된 행 (A-22) | 정렬 중 앱이 `DataSet.AddRow`로 직접 넣은 행은 화면 끝 |
| 리사이즈 후 앱이 `Width`를 바꿈 (A-23) | 사용자가 정한 너비 유지 |
| 페이징 중 행 추가 위치 (A-24) | Insert·행 복사·붙여넣기로 추가한 행 모두 맨 위 (A-12·A-15의 "바로 아래"는 페이징이 아닐 때만) |
| 페이징 중 삭제 표시 행 (A-24, O-02) | `DeletedRows` 설정과 관계없이 자리에 남아 취소선 (가상 스크롤 위치 유지) |
| 페이징 중 마지막 셀 Enter 새 행 (A-24) | 동작하지 않음 (그려진 마지막 행이 실제 마지막 행이 아닐 수 있음) |
| 페이징 중 범위 선택·복사 (A-24·A-25) | 일괄 삭제는 화면 밖까지 (A-25). Ctrl+C·범위 표시(배경색)는 그려진 행까지 |
| 페이징 적재 범위 (A-24) | 요청 범위 앞뒤 1페이지. 행 ID → 행 키 색인은 다시 조회 전까지 유지 (놓았다 다시 적재해도 같은 행 키) |
| 페이징 중 합계 (A-24) | 서버가 그 필드 합계를 주지 않으면 비움. 행 높이는 고정(`RowHeight`, 기본 28px) |
| 페이징 화면 다시 조회 (A-24) | 저장 후 등은 그리드 `ReloadAsync()`. 페이징 중 앱이 `DataSet.Load`를 직접 부르지 않는다 |
| 여러 그리드가 있는 화면 (A-20) | 그리드마다 확인. 화면 단위 한 번 묻기는 앱이 `OnLeaving`으로 |
| 헤더에 있을 때 다른 키 (A-35) | 문자·F2·Insert·Tab·Ctrl+Delete 등은 무시 (편집·행 추가 없음). 마우스 헤더 클릭 정렬은 헤더로 옮기지 않음 |
| 페이징 중 자동 맞춤 (A-35) | 그려진 행만 잼 (화면 밖 행은 적재되지 않음) |
| 헤더 클릭 정렬 시점 (A-36) | 누를 때 → **놓을 때** (드래그 이동과 구분). 5px 안의 움직임은 클릭 |
| 레이아웃에 없는 새 컬럼 (A-36) | 정의상 앞 컬럼 바로 뒤 (맨 앞 컬럼이면 맨 앞). 너비는 정의 너비 |
| `ApplyLayout` 이벤트 (A-36) | `OnSortChanged`·`OnColumnResized`·`OnColumnMoved` 발생 안 함 (`SetSort`와 같음). 페이징이면 정렬 조건으로 다시 조회 |
| 활성 셀의 컬럼을 숨김 (A-36) | 같은 행의 첫 열로 |
| 컬럼 선택 메뉴 (A-36) | 그리드에 없음. 앱이 자체 메뉴로 만들고 `SetColumnVisible`을 부른다 |
| 그룹 헤더 클릭·드래그 (A-37) | 없음 (정렬·리사이즈·그룹째 이동 안 함). 그룹 폭은 컬럼 리사이즈로만 |
| 고정 열 경계에 걸친 그룹 (A-37) | 고정 쪽·일반 쪽 두 칸으로 나누고 둘 다 그룹 이름 표시 |
| 레이아웃과 그룹 (A-37) | `GridLayout`에 그룹 정보 없음 (그룹은 화면 정의). 적용한 순서가 그룹을 가르면 그룹 칸이 나뉘어 그려짐 |
| 테마 섞음 비율 (A-38) | 선택 16%·오류 12%·경고 14% (강조색을 배경에). `color-mix` 지원 브라우저 필요 (Chrome·Edge 111+, Firefox 113+, Safari 16.2+) |
| 셀 선택 목록 색 (A-38·A-67) | 달력·Combo 목록은 그리드 토큰(`--qg-*`)을 따른다 (A-67 전에는 MudBlazor 테마) |
| Undo 중 이벤트 (A-39) | 내지 않는다 (`OnCellValueChanging`·`OnCellValueChanged`·`OnRowAdded` 등). 앱이 재계산한 값도 이력에서 함께 되살아남 |
| `RejectChanges`와 이력 (A-39) | 이력을 비운다 (저장 성공·다시 조회와 같음) |
| Undo 이력과 셀 표시 (A-39) | `SetCellMark`는 이력에 없음 (값만 되살림, 표시는 앱이 다시 판정) |
| 페이징 중 Undo (A-39) | 놓았다가 다시 적재한 행은 행 키로 찾아 되살림 |
| 필터 후 편집·추가 행 (A-40) | 메모리 모드는 적용 시점에 거르고, 그 뒤 편집해 조건에서 벗어난 행·추가한 행은 그대로 보임 (정렬 A-22와 같음). 다시 조회(`Load`)하면 다시 적용 |
| 텍스트 필터 비교 (A-40) | 표시 문자열 또는 저장 값에 포함되면 맞음 (대소문자 무시). 빈 값은 맞지 않음 |
| 값 목록 전부·전무 (A-40) | 값을 모두 고르거나 하나도 고르지 않으면 그 컬럼은 거르지 않음 |
| 숫자 범위 빈 값 (A-40) | 최소·최대 중 하나라도 있으면 빈 값 행은 맞지 않음 |
| 패널이 열리기 전 입력 (A-40) | 필터 버튼을 누르고 패널이 열리기 전에 친 키는 패널 입력으로 넘김 (편집 시작 중 입력과 같음) |
| 필터 패널 문구 (A-40) | 리소스 키 `quro.filter.open`·`apply`·`clear`·`contains`·`min`·`max`·`from`·`to`·`checked`·`unchecked` |
| 접힌 그룹 (A-42) | 머리 행과 소계 행은 보이고 안쪽 행·안쪽 단은 숨김. 아래 합계 행은 접힌 그룹의 행도 합산 |
| 그룹 건수·소계 (A-42) | 삭제 표시 행은 빼고 셈 (`DeletedRows=Show`여도) |
| 그룹핑 중 추가 행 (A-42) | 행 아래 추가(Insert)·행 복사는 그 행의 그룹. 위치 없이 추가한 행·붙여넣기로 추가한 행은 처음 그려질 때의 값으로 그룹을 정함 |
| `SetGroupBy` 뒤 펼침 상태 (A-42) | 모두 펼침. 접힘 상태는 그룹 ID(단별 값 경로)로 기억하며 `SetGroupBy`·`Load`에서 비움 |
| 그룹과 레이아웃·내보내기·Undo (A-42) | `GridLayout`·`GridExportRequest`에 그룹 정보 없음, 펼침·접기는 Undo 이력에 없음 (나중에 깨지지 않게 추가 가능) |
| 컬럼 헤더에서 ↓ (A-42) | 그룹핑 중이면 첫 그룹 머리 행으로 (GS-064의 "본문 첫 행"과 같은 규칙) |
| 혼합 통화 합계 행 | 첫 행 통화의 소수 자릿수로 표시 |
| `Definition`과 파라미터 (A-45) | 정의에 있는 값(`frozen`·`deletedRows`·`clipboard`·`pageSize`>0, `sum` 열이 있으면 `ShowFooter`)이 같은 파라미터보다 우선 |
| `initRows` 빈 행 (A-45·A-46) | 처음 연결할 때 DataSet이 비어 있으면 1회, `AddBlankRow`. 손대지 않은 동안은 `HasChanges`·`Validate`·`GetChanges` 대상이 아니다 |
| 한 열의 validate 여러 줄 (A-45) | 범위는 합치고 메시지는 첫 줄의 `msg` |
| validate 오류의 셀 표시 (A-45) | 그리드가 자동으로 표시하지 않는다. 앱이 `Validate()` 결과로 `SetCellMark` (Spec 4.9와 같은 원칙) |
| `quro-deflint` 카탈로그 파일 (A-46) | JSON 문자열 배열 5개(`resourceKeys`·`codeMajors`·`lookupIds`·`handlers`·`permissions`) 모두 필수. 없거나 형식이 틀리면 종료 코드 2. `--catalog`가 없으면 로드 검사(V-01~V-18)만 |
| `quro-deflint` 출력 (A-46) | 오류는 표준 출력 한 줄씩 `파일:줄: V-xx 메시지`(파일은 넘긴 폴더 기준, `/` 구분, 경로 순), 요약(`N screen(s), M lookup(s), K error(s)`)은 표준 오류. 0 = 통과, 1 = 오류, 2 = 사용법. lookup(A-48): 로드 실패 lookup이 있으면 화면 교차 검사(V-35~V-37)를 건너뛰고 요약에 알린다 (없는 정의로 V-37이 쏟아지지 않게). 같은 id의 lookup 파일이 둘이면 뒤 파일에 V-33 |
| 정의 열의 코드도움·목록 (A-45·A-46) | `lookup` 열은 Popup 셀 + `LookupFor`, `select` 열은 Combo 셀 + `OptionsFor`. 파라미터가 없으면 함수·목록 없음 (앱이 `OnPopupRequested`로 처리). `OptionsFor`가 null을 주면 목록 없음 |
| 큰 붙여넣기 중 앱 화면 (A-26) | 셀마다 값 변경 이벤트를 받은 앱 화면은 이벤트마다 다시 그려진다 (Blazor `EventCallback` 동작). 그리드는 그리지 않지만, 앱 화면에 변경분 전체 직렬화 같은 무거운 표시가 있으면 느려진다 (샘플의 저장 payload 미리보기로 500행 × 10열 약 7초 확인 → 붙여넣기 성능 화면에서 뺌). 앱 지침으로 둠 |
| Control Spec 질의 미답 (A-50) | Q-04 `Normal` 유지 · Q-05 추가 행 삭제 즉시 제거 · Q-07 `SaveAsync` 먼저 검증 + 오류 셀 자동 표시 · Q-12 `SelectionMode` 기본 `Range`, 선택 체크는 화면 상태, 페이징 중 헤더 체크는 적재된 행만 · Q-13 위쪽 고정 행 P1, 다중 레코드 행 P2 · Q-15 `On…` 이름 유지 + 대응표 · Q-18 `Wrap`·`AutoRowHeight`는 메모리 모드만 · Q-19 TOON `formats` + `ViewId`·`SetView` · Q-20 `ImportRows` 유지 · Q-21 MudBlazor를 감싸 구현 · Q-22 연노랑 필수 배경을 그리드에도, 빨간 별 없음 · Q-23 다중 선택 = 구분자 문자열 (A-64에서 확정) · Q-24 기간 = 열 2개, 월 모드 = `yyyy-MM` (A-65에서 확정) |
