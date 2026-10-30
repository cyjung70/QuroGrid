# QuroGrid & QuroDataSet Control Spec v0.1 — 반영 검토와 질의 목록

- 대상: `docs/QuroGrid_Control_Spec_Draft_v0.1.md` (이하 "Control Spec", 2026-10-30 수신)
- 비교 기준: Spec v1 (`QuroGrid_FeatureMatrix_Spec_v1.pdf` Draft v0.2), Addendum A.22 (A-01~A-49), TOON 설계서 v0.2, 현재 `main` 코드 (`0.3.0-alpha.1`)
- 문서 상태: v0.2 (2026-10-30). **Critical 질의 11건 결정 — 모두 잠정안 채택 (Addendum A-51~A-61)**. 나머지 13건은 잠정안을 기본값으로 쓰며 결정 대기 (Addendum 3장)
- 수신 원문은 그대로 보관하되, 레거시 제품명 1곳만 일반 표현("legacy XML screen definitions")으로 바꿨다 (공개 저장소 규칙)

---

## 1. 요약

| 구분 | 건수 | 뜻 |
|---|---|---|
| ✅ 구현됨 | 27 (+ 일부 구현 2) | 현재 코드·Addendum과 같다. 할 일 없음 (이름만 다른 것 포함) |
| ➕ 신규 (충돌 없음) | 15 + QuroUI 9 | 기존 결정과 부딪히지 않는 추가. 비파괴 공개 API 추가라 4장 항목은 질의 답 없이 GS 시나리오로 진행 가능 |
| ⚠ 충돌 | 22 (경미 3 포함) | Addendum·Spec·현재 공개 API와 다르다. **질의 답변 전 착수하지 않음** |

가장 큰 변화는 세 가지다.

1. **DataSet 중심 바인딩**: 한 DataSet을 그리드·텍스트 박스·콤보 등 여러 컨트롤이 함께 쓰고, **현재 행**을 공유한다 (Control Spec 1·2.1). 지금은 활성 행이 그리드 안 상태다.
2. **v1 범위 확대**: 트리 그리드, 셀 식(계산 표시)·조건부 스타일 식, 다중 레코드 행, 여러 형식(레이아웃) 전환, 행 선택 방식 등이 v1로 들어왔다. 일부는 기존 "v1 범위 밖"·A-44 ④ 결정과 반대다.
3. **QuroUI 컨트롤**(텍스트 박스·콤보·날짜·정적 텍스트, 다음은 마스크): 그리드 밖 새 컴포넌트군이다. 패키지·저장소 위치가 정해지지 않았다.

### Clean-room 주의

Control Spec은 여러 곳에서 "Nexacro style"을 기준으로 든다. Clean-room 규칙상 **동작(무엇을 하는가)만** 가져오고 Nexacro 런타임·라이브러리 분석은 하지 않는다. 또 참고 그리드의 속성 이름을 식별자로 쓰지 않는다 (`displaytype`·`edittype`·`suppress`·`autoenter`·`bind` 등). 이 문서의 이름 제안은 모두 QuroGrid 고유 이름이다.

필수 입력의 **연노랑 배경**(8장)은 상태(필수·편집 가능)에서 계산한 CSS 토큰(`--quro-required-bg`)으로 그린다. 레거시 헤더 색 값(예: `#fff799`)을 그대로 의미로 쓰지 않는다.

---

## 2. 항목별 대조

표기: ✅ 구현됨 · ➕ 신규 · ⚠ 충돌 · 근거 = 현재 코드·Addendum. 질의 = 아래 3장 번호.

### 2.1 설계 원칙 (Control Spec 1)

| # | 항목 | 판정 | 근거 · 메모 | 질의 |
|---|---|---|---|---|
| 1-1 | DataSet이 데이터를 갖고 그리드는 보여 주기만 | ✅ | `QuroGrid DataSet="@ds"`, 데이터는 `QuroDataSet<T>` | |
| 1-2 | 한 DataSet을 여러 컨트롤이 바인딩, 변경이 모든 컨트롤에 반영 | ⚠ | DataSet에 변경 알림·현재 행이 없다. 활성 행은 그리드 상태 (A-13) | Q-01 |
| 1-3 | 그리드·DataSet은 이 저장소의 별도 컴포넌트, QuroERP가 가져다 씀 | ✅ | `Quro.Grid` · `Quro.Grid.Data` 패키지 | Q-03 (QuroUI 위치) |

### 2.2 QuroDataSet (Control Spec 2)

| # | 항목 | 판정 | 근거 · 메모 | 질의 |
|---|---|---|---|---|
| 2-1 | 컬럼 메타: 이름·타입(string/int/decimal/date/datetime/bool)·키·필수·기본값·최대 길이·소수 자릿수 | ⚠ | 현재는 형식 있는 `QuroDataSet<T>` (타입 = `T`의 속성). 필수·범위는 `SetRule(field, FieldRule)` (A-10). 기본값·소수 자릿수·컬럼 목록 API 없음 | Q-02 |
| 2-2 | 행: 현재 값·원래 값·행 상태 | ✅ | `GetValue`·`GetOriginal`·`GetState` | |
| 2-3 | 행 상태 이름 `Unchanged` | ⚠ | 현재 `RowState.Normal` (Spec 4.4). 이름 변경은 BREAKING | Q-04 |
| 2-4 | 현재 행 (모든 컨트롤 공유) | ➕ | 1-2와 함께 | Q-01 |
| 2-5 | REST API(JSON)로 조회, 비우기 | ➕ | `Load(IEnumerable<T>)`만 있음. URL 조회 `LoadAsync(http, url)`·`Clear()` 추가 (저장은 이미 `SaveAsync(http, url)`, A-08). URL은 앱이 넘김 | |
| 2-6 | 행 추가, 위치 지정 삽입 | ✅ | `AddRow(template, afterRowKey)` | |
| 2-7 | 행 삭제 = Deleted 표시, 저장 때 제거 | ⚠ (경미) | `Inserted` 행은 즉시 제거 (Spec 4.4·dataset 규칙). Control Spec 문장은 모든 행을 Deleted로 읽힘 | Q-05 |
| 2-8 | 셀 값 읽기·쓰기 | ✅ | `GetValue`·`SetValue` | |
| 2-9 | 한 행·전체 변경 취소 | ✅ | `RevertRow`·`RejectChanges` | |
| 2-10 | 변경 행만 가져오기 | ✅ | `GetChanges()` | |
| 2-11 | 저장 성공 후 모두 Unchanged | ✅ | `AcceptChanges()` (`SaveAsync` 2xx에서 호출) | |
| 2-12 | 필터·정렬 (클라이언트, 원래 순서 보존) | ⚠ | 지금은 **그리드**가 화면 순서로 처리 (A-22·A-31·A-40). 원칙(원래 순서 보존)은 같고 위치가 다름. 여러 컨트롤 바인딩이면 DataSet 쪽이 맞음 | Q-06 |
| 2-13 | 컬럼별 합계·건수·평균·최소·최대 | ➕ | `Aggregate`는 `None`·`Sum`뿐이고 그리드가 계산. DataSet 집계 함수 + `Aggregate.Count/Avg/Min/Max` 추가 | |
| 2-14 | 검증: 필수·최대 길이·데이터 타입·숫자 범위 | ✅ | `FieldRule` (A-10·A-45). 데이터 타입은 `TryParseText` 경로 | |
| 2-15 | 저장 전 검증, 오류는 행·열 반환 | ⚠ (경미) | `Validate()` → `ValidationError(행, 필드, 키)` ✅. 다만 `SaveAsync`는 지금 검증하지 않고 보낸다 (앱이 먼저 `Validate`) | Q-07 |
| 2-16 | DataSet 이벤트 RowChanged·ValueChanged·RowAdded·RowDeleted·Loaded·Saved | ➕ | DataSet 이벤트 없음 (그리드 이벤트만). UI 무관 C# 이벤트로 추가 가능. RowChanged는 Q-01에 의존 | Q-01 |

### 2.3 QuroGrid 표시 (Control Spec 3.1)

| # | 항목 | 판정 | 근거 · 메모 | 질의 |
|---|---|---|---|---|
| 3-1 | 다국어 헤더·너비·보임/숨김 | ✅ | `HeaderKey`·`Width`·`SetColumnVisible` (A-36) | |
| 3-2 | 가로 정렬(좌·중·우)·세로 정렬(상·중·하) | ➕ | 컬럼 `Align` 파라미터 없음 (타입별 기본만) | |
| 3-3 | 왼쪽 고정 열 | ✅ | `FrozenColumns` | |
| 3-4 | 위쪽 고정 행 | ➕ | 없음. 가상 스크롤 밖에 그리는 고정 행 영역 필요 | Q-13 |
| 3-5 | 다단 헤더 | ✅ | `QuroColumnGroup` (A-37) | |
| 3-6 | 한 레코드를 두 줄 이상으로 (다중 레코드 행) | ➕ | 없음. 키보드·붙여넣기·범위 선택·고정 행 높이(A-24)에 모두 영향 | Q-13 |
| 3-7 | 행 번호 열·행 상태 표시(신규·수정·삭제) | ➕ | 없음. 그리드 옵션 2개로 추가 | |
| 3-8 | 열별 숫자·날짜 서식, 천 단위 구분 | ✅ | `Format`, 사용자 `Culture` (4.8) | |
| 3-9 | 합계 행 (sum·count 등) | ✅ / ➕ | `ShowFooter` + `Aggregate.Sum` ✅. count 등은 2-13 | |
| 3-10 | 그룹 소계 행 | ✅ | `SetGroupBy` (A-42, 메모리 모드) | |
| 3-11 | 같은 값 연속 셀 합쳐 보이기 (Control Spec "suppress") | ➕ | 없음. 표시만 합치고 편집·선택은 셀 단위. 이름은 고유 이름(예: `MergeRepeated`) | |
| 3-12 | v1 = 가상 스크롤만 (서버 페이징은 다음 버전) | ⚠ | 서버 페이징 `ItemsProvider`는 **이미 구현·배포** (A-24, O-04, GS-058) | Q-08 |

### 2.4 QuroGrid 편집 (Control Spec 3.2)

| # | 항목 | 판정 | 근거 · 메모 | 질의 |
|---|---|---|---|---|
| 4-1 | 열마다 표시 방식·편집 방식 두 설정 | ⚠ | 현재 `Type`(CellType) 하나 + `ReadOnly`. 두 설정으로 나누면 공개 API 변경 | Q-09 |
| 4-2 | 타입: 텍스트·정적 라벨·콤보·날짜·마스크·체크·이미지·읽기 전용 | ⚠ / ➕ | 현재 `Text`·`Number`·`Currency`·`Date`·`Combo`·`Check`·`Popup`·`Button`. 마스크·이미지·정적 라벨이 신규. Control Spec 목록에 Number·Currency·Popup·Button이 없음 | Q-09 |
| 4-3 | 콤보 목록 = 콤보 DataSet + 코드 열 + 이름 열 바인딩, 화면 열 때 1회 조회 | ⚠ | A-49: 목록은 `ComboOption` 목록(`Options`·`OptionsFor`)으로 앱이 넘김 | Q-10 |
| 4-4 | 키보드: 화살표·Tab·Enter 이동, F2·문자 입력 편집, Esc 취소 | ✅ | Spec 4.7, A-01 | |
| 4-5 | 셀 선택 즉시 편집 시작 (7장 8번 "auto enter") | ⚠ | 현재는 F2·문자 입력으로 편집 시작. 4-4와 Control Spec 안에서도 겹침. 범위 선택·Ctrl+C·Space 체크와 상호작용 | Q-11 |
| 4-6 | 행 추가·삭제, 엑셀 호환 복사·붙여넣기 | ✅ | Insert·Ctrl+Delete·Ctrl+C/V (A-03·A-04·A-26) | |
| 4-7 | 필수·오류 셀 강조, 오류 메시지 툴팁 | ⚠ (경미) | 오류 표시는 앱이 `SetCellMark`로 (Spec 4.9·A-45 잠정). 그리드가 `Validate()` 결과를 자동으로 그리지 않음 | Q-07 |
| 4-8 | 셀 단위 읽기 전용 규칙 (예: 저장 후 키 열 읽기 전용) | ✅ | `NewRowOnly` (A-45 G-01), 정의의 `lockWhen` (`FieldModeResolver`) | |

### 2.5 기타 기능 (Control Spec 3.3) · 이벤트 (3.4)

| # | 항목 | 판정 | 근거 · 메모 | 질의 |
|---|---|---|---|---|
| 5-1 | 헤더 클릭 정렬, 열 필터 | ✅ | A-22·A-40 | |
| 5-2 | 열 리사이즈·이동, 사용자별 레이아웃 저장 | ✅ | A-23·A-36 `GetLayout`·`ApplyLayout` (저장은 앱) | |
| 5-3 | 행 선택: 단일·다중·체크 박스 열 / 선택 방식 옵션 행·셀·다중 행·영역 (7장 1번) | ➕ | 현재는 셀 + 영역(범위) 선택. `SelectionMode` 옵션·선택 행 API 추가 | Q-12 |
| 5-4 | 엑셀 내보내기 | ✅ | A-28 (파일은 저장 API 서버가 만듦) | |
| 5-5 | 값에 따른 셀 스타일 (예: 음수 빨강) | ⚠ | 7장 3번 "식으로 지정"과 같은 문제 | Q-14 |
| 5-6 | 이벤트 CellClick·CellDoubleClick·CellEditStart·CellEditEnd | ➕ | 없음. 구독할 때만 서버로 보냄 (R1·R5) | Q-15 |
| 5-7 | 이벤트 RowSelected·BeforeRowDelete(취소 가능) | ✅ | `OnSelectionChanged` (A-13)·`OnRowDeleting` | Q-15 |

### 2.6 TOON 정의 (Control Spec 4) · 다국어 (5)

| # | 항목 | 판정 | 근거 · 메모 | 질의 |
|---|---|---|---|---|
| 6-1 | `dataset` 절 (id·api·columns{name,type,key,required,length}) | ⚠ | v0.2에는 DataSet 절·`api`가 없다. 그리드 열이 DataSet 스키마를 겸함 | Q-16 |
| 6-2 | `grid` 열 필드 (field·captionKey·width·align·editType·format·frozen·visible) | ⚠ | v0.2 열은 `id,label,type,width,edit,req,fmt,lookup,sum,hidden` (A-44 확정, 구현·검증기 V-xx 있음) | Q-16 |
| 6-3 | 캡션은 언어 키, 실행 시 다국어 서비스가 해석 | ✅ | `label` = 리소스 키 (P6·V-21), `Localizer` | |
| 6-4 | 모든 캡션·메시지·툴팁은 언어 키, 날짜·숫자 서식은 사용자 로캘 | ✅ | Spec 4.8, grid-features 규칙 | |

### 2.7 v1 결정 (Control Spec 6) · Nexacro 기능 질의 (7)

| # | 항목 | 판정 | 근거 · 메모 | 질의 |
|---|---|---|---|---|
| 7-1 | DataSet이 REST URL을 받아 직접 호출 (서버측 HttpClient) | ✅ / ➕ | 저장 `SaveAsync(http, url)` ✅ (A-08). 조회는 2-5 | |
| 7-2 | 트리 그리드 v1 포함 | ⚠ | grid-features 규칙 "v1 범위 외: 트리 그리드" | Q-17 |
| 7-3 | v1 가상 스크롤만, 서버 페이징은 다음 버전 | ⚠ | 3-12와 같음 | Q-08 |
| 7-4 | 변경 행만 한 묶음으로 저장 | ✅ | `{inserted, updated, deleted}` (Spec 4.4) | |
| 7-5 | (1) 선택 방식 옵션 | ➕ | 5-3 | Q-12 |
| 7-6 | (2) 셀 식 — 계산 값 표시 (수량 × 단가) | ⚠ | grid-features "셀 수식은 v1 범위 밖", A-44 ④ "그리드는 식을 평가하지 않는다 (앱이 `RuleExpression`으로 `OnCellValueChanged`에서)" | Q-14 |
| 7-7 | (3) 조건부 스타일 — 식으로 색·글꼴 | ⚠ | 같은 원칙 (A-44 ④). 지금은 앱이 `SetCellMark` | Q-14 |
| 7-8 | (4) 헤더 체크 박스 — 전체 선택·해제 | ➕ | 체크 열(Check 셀 또는 선택 체크 열) 헤더. 페이징 중 범위 | Q-12 |
| 7-9 | (5) 줄 바꿈은 셀 속성, 행 높이 자동은 그리드 옵션 | ⚠ | 가상 스크롤은 고정 행 높이 (`RowHeight` 28px, A-24 잠정). 자동 높이는 스크롤 위치 계산과 성능 목표(4.11)에 영향 | Q-18 |
| 7-10 | (6) 여러 형식 — 한 그리드에 여러 레이아웃, 화면이 전환 | ➕ | `GridLayout`(A-36)은 사용자 레이아웃. 정의(TOON)에 형식 여러 개 + 전환 API 필요 | Q-19 |
| 7-11 | (7) 본문 셀 병합 v1 제외 | ✅ | 없음 (변화 없음) | |
| 7-12 | (8) 선택 즉시 편집 | ⚠ | 4-5 | Q-11 |
| 7-13 | (9) 엑셀 내보내기만, v1 가져오기 없음 | ⚠ | `ImportRows(fields, rows)` (A-47, GS-D17)가 이미 공개 API. 단 파일을 읽는 것은 앱이고 그리드는 행만 받음 | Q-20 |
| 7-14 | (10) 데이터 없음 문구 없음 — 빈 그리드 | ✅ | 현재도 문구 없음 | |
| 7-15 | (11) 잘린 셀 텍스트에 마우스를 올리면 전체 표시 | ➕ | 없음. JS만 (서버 호출 없음). 오류 툴팁(`CellMark`)이 있으면 오류 우선 | |
| 7-16 | (12) 행 끌어 놓기 순서 변경 v1 제외 | ✅ | 없음 (변화 없음) | |

### 2.8 QuroUI 컨트롤 (Control Spec 8~11)

모두 ➕ 신규다. 공통으로 Q-01(현재 행 바인딩)·Q-03(패키지 위치)·Q-21(MudBlazor 사용)에 의존한다.

| # | 컨트롤 | 항목 | 메모 | 질의 |
|---|---|---|---|---|
| 8-1 | 텍스트 박스 | DataSet 열 바인딩, 일괄 저장에 포함 | 현재 행 필요 | Q-01 |
| 8-2 | | 허용 문자 종류·최대 길이·암호 모드·자리 표시 문구(다국어)·포커스 시 전체 선택 | 신규 | |
| 8-3 | | 필수 = 연노랑 배경(빨간 별 없음), 비어 있으면 저장 막음 · 읽기 전용 = 회색 배경 | 저장 막기는 DataSet `Validate`(필수 규칙). 배경은 상태에서 계산한 CSS 토큰 | Q-22 |
| 9-1 | 콤보 | 공통 코드 목록 (그리드 콤보와 같음), 전체를 한 번에 조회 | 4-3과 같은 출처 문제 | Q-10 |
| 9-2 | | 입력으로 목록 거르기·맨 위 빈 선택("전체"/"선택")·코드+이름 함께 표시(옵션)·여러 열 목록 | 신규. 빈 선택 문구는 리소스 키 | |
| 9-3 | | 연동 콤보 (부모 선택 → 자식 목록) | 자식 목록 출처(앱 함수 vs 콤보 DataSet 필터) | Q-10 |
| 9-4 | | 체크 박스 다중 선택 | DataSet 한 열에 여러 값 저장 형식 미정 | Q-23 |
| 10-1 | 날짜 | 기간 선택(시작~끝)·월 모드·직접 입력·언어별 서식 | 기간은 열 2개 바인딩, 월 모드 저장 값 형식 미정 | Q-24 |
| 11-1 | 정적 텍스트 | 다국어 라벨, DataSet 열 값 표시 | 신규. 다음은 마스크 컴포넌트 | |

---

## 3. 질의 목록 (스테이블영 결정 요청)

### 결정 현황 (2026-10-30)

| 질의 | 결정 | Addendum |
|---|---|---|
| Q-01 | 잠정안 (a) DataSet 현재 행·이벤트 | A-51 |
| Q-02 | 잠정안 (a) `QuroDataSet<T>` 유지 + `Columns` | A-52 |
| Q-03 | 잠정안 (a) 새 패키지 `Quro.UI` | A-53 |
| Q-06 | 잠정안 (b) 정렬·필터는 그리드 | A-54 |
| Q-08 | 유지 | A-55 |
| Q-09 | 잠정안 (a) `Type` 유지 + Mask·Image·Label | A-56 |
| Q-10 | 잠정안 (b) 콤보 DataSet 바인딩 추가 | A-57 |
| Q-11 | 잠정안 (a) `EditOnSelect` 기본 끔 | A-58 |
| Q-14 | 잠정안 (a) A-44 ④ 유지 + `Compute`·`CellStyle` | A-59 |
| Q-16 | 잠정안 (a) v0.2 이름 유지, v0.3 추가 | A-60 |
| Q-17 | 잠정안 (부모 키 열, 전체 적재, 트리 모드 페이징·그룹핑 끔) | A-61 |
| Q-04·05·07·12·13·15·18~24 | 미답 — 잠정안을 기본값으로 사용 | 3장 잠정 사항 |

각 질의에 선택지와 **잠정안**(답이 없을 때 쓸 기본값)을 둔다. ★ = Critical (아키텍처·데이터 모델·공개 API, 구현 전 확인 필수).

### A. 아키텍처 · 데이터 모델

**Q-01 ★ 여러 컨트롤 바인딩과 현재 행** (1-2, 2-4, 2-16, 8~11장)
한 DataSet을 그리드·텍스트 박스·콤보가 함께 쓰려면 DataSet에 현재 행과 변경 알림이 있어야 한다.
- (a) `QuroDataSet`에 `CurrentRowKey`·`MoveTo(rowKey)`와 C# 이벤트(`CurrentRowChanged`·`ValueChanged`·`RowAdded`·`RowDeleted`·`Loaded`·`Saved`)를 둔다. 그리드 활성 행 = DataSet 현재 행 (양방향). UI 무관 규칙은 지킨다.
- (b) DataSet은 그대로 두고, 화면 단위 바인딩 객체(예: `QuroBindingContext`)를 `Quro.Grid`에 둔다.
- 잠정안: **(a)**. 그리드의 `OnSelectionChanged`(A-13)는 그대로 두고 내부에서 `MoveTo`를 부른다. 페이징 중 현재 행이 놓인(released) 행이면 다시 적재될 때까지 행 키로 유지.

**Q-02 ★ DataSet 형식: 형식 있는 `QuroDataSet<T>` vs 열 정의 DataSet** (2-1)
Control Spec은 열 목록(이름·타입·키·필수·기본값·길이·소수 자릿수)을 가진 DataSet으로 읽힌다 (형식 없는 행). 현재는 `T`의 속성이 열이다.
- (a) `QuroDataSet<T>` 유지 + 열 메타 API 추가 (`Columns`: `DataSetColumn(Name, Type, Key, Required, Default, MaxLength, Scale)`, `T`에서 만들고 정의로 보완). 기본값은 `AddRow` 때 채움.
- (b) 형식 없는 `QuroDataSet`(사전 행)을 새로 만들고 TOON `dataset` 절에서 생성. `QuroDataSet<T>`는 그 위의 형식 있는 보기.
- (c) (b)로 교체 (BREAKING).
- 잠정안: **(a)**. QuroERP는 DTO 형식으로 이미 쓰고 있고, 형식 없는 행은 `decimal`·날짜 처리와 컴파일 검사를 잃는다.

**Q-03 ★ QuroUI 컨트롤(텍스트 박스·콤보·날짜·정적 텍스트·마스크)의 위치** (1-3, 8~11장)
- (a) 이 저장소에 새 패키지 `Quro.UI` (RCL, `Quro.Grid.Data` 참조). 의존: `Quro.UI` → `Quro.Grid.Data`. 그리드 셀 에디터와 공통 부분은 나중에 정리.
- (b) `Quro.Grid` 패키지 안에 함께 둔다.
- (c) 새 저장소(QuroUI)로 분리하고 `Quro.Grid.Data`를 패키지로 참조.
- 잠정안: **(a)**. 저장소 이름·CLAUDE.md 범위("공통 그리드 컴포넌트")도 함께 고쳐야 하므로 결정 후 Addendum에 적는다.

**Q-04 행 상태 이름 `Unchanged` vs `Normal`** (2-3)
- (a) `Normal` 유지 (Spec 4.4·현재 공개 API). 문서에서 "Unchanged = Normal"로 적는다.
- (b) `Unchanged`로 바꾼다 (BREAKING, `0.4.0`).
- 잠정안: **(a)**.

**Q-05 추가 행 삭제** (2-7)
`Inserted` 행을 삭제하면 지금은 바로 목록에서 지운다 (서버에 보낼 것이 없음). Control Spec 문장대로 저장 때까지 남길까?
- 잠정안: **현재대로 바로 제거** (Spec 4.4). 저장 전 삭제 취소는 Undo(A-39)로 된다.

**Q-06 ★ 필터·정렬의 위치** (2-12)
- (a) DataSet에 보기(view) 순서·필터를 두고(원래 순서·상태 보존), 그리드·다른 컨트롤이 같은 보기를 쓴다. 그리드 헤더 정렬·필터(A-22·A-40)는 DataSet 보기를 바꾼다.
- (b) 지금처럼 그리드 화면 순서만 (다른 컨트롤에는 영향 없음). DataSet에는 정렬·필터된 행 목록을 돌려주는 조회 함수만 추가.
- 잠정안: **(b)** — 이미 구현된 페이징 분담(A-24·A-31)과 충돌이 적다. (a)를 고르면 Q-01 (a)와 함께 진행.

**Q-07 저장 전 검증과 오류 표시** (2-15, 4-7)
- (a) `SaveAsync`가 먼저 `Validate()`를 부르고, 오류가 있으면 HTTP 없이 `SaveResult.Invalid(errors)`를 돌려준다. 그리드는 그 오류를 `CellMark(Error)`로 자동 표시하고 첫 오류 셀로 이동한다.
- (b) 지금처럼 앱이 `Validate()` 후 `SetCellMark` (A-45 잠정).
- 잠정안: **(a)**. 텍스트 박스의 "필수 비면 저장 막음"(8장)과도 맞는다. `SaveResult` 상태 추가는 비파괴.

### B. 범위 충돌 (Addendum · Spec과 반대)

**Q-08 ★ 서버 페이징** (3-12, 7-3)
Control Spec은 "v1 = 가상 스크롤만, 서버 페이징은 다음 버전"이지만 `ItemsProvider` 페이징은 이미 구현·배포됐다 (A-24, O-04 "화면별 설정").
- (a) 유지한다. Control Spec 문장은 "v1에서 서버 페이징을 요구하지 않음"으로 읽는다.
- (b) 공개 API에서 숨기거나 `[Obsolete]` 표시 (BREAKING에 가까움).
- 잠정안: **(a)**.

**Q-09 ★ 표시 방식·편집 방식 분리와 셀 타입 목록** (4-1, 4-2)
- (a) `Type`(CellType) 유지 + `ReadOnly` 유지. 새 타입 `Mask`(+ `Mask` 패턴 파라미터)·`Image`·`Label`(정적) 추가. Number·Currency·Popup·Button은 그대로.
- (b) 열에 `Display`(표시 방식)와 `Editor`(편집 방식)를 따로 두고, `Type`은 둘 다 같은 값의 줄임으로 남긴다.
- 잠정안: **(a)**. 조합(예: 표시는 콤보 이름, 편집은 Popup)이 실제로 필요한 화면이 있으면 (b).
- 함께 결정: 이미지 셀의 값 (URL 문자열? 앱이 주는 함수 `ImageFor(value)`?). 잠정안: 값 = URL 문자열, 클릭은 `OnCellClick`.

**Q-10 ★ 콤보 목록의 출처** (4-3, 9-1, 9-3)
- (a) 지금처럼 `ComboOption(Value, TextKey/Text)` 목록 (`Options`·`OptionsFor`, A-49). 앱이 공통 코드를 화면 열 때 1회 조회해 넘긴다. 연동 콤보는 `OptionsFor`에 부모 값을 넘기는 오버로드.
- (b) 콤보 DataSet 바인딩 추가: `OptionsSource="@codeDs"`·`ValueField`·`TextField` (+ 연동은 `FilterField`). (a)도 유지.
- 잠정안: **(b)를 추가** (비파괴). Q-01의 바인딩 모델과 맞고 연동 콤보가 쉽다.

**Q-11 ★ 선택 즉시 편집 (auto enter)** (4-5, 7-12)
Control Spec 3.2(F2·문자 입력으로 편집)와 7장 8번(선택 즉시 편집)이 겹친다.
- (a) 그리드 옵션 `EditOnSelect`, **기본 끔**. 켜면 셀에 서면 바로 브라우저 에디터가 열리고(서버 호출 없음, R2), 화살표는 에디터 밖으로 이동, Shift+화살표 범위 선택은 유지.
- (b) 옵션, 기본 켬.
- (c) 항상 켬.
- 잠정안: **(a)**. 기본 켬이면 현재 E2E(GS 키보드 시나리오) 다수가 바뀐다.

**Q-12 행 선택 방식과 체크 열** (5-3, 7-5, 7-8)
- 제안: 그리드 옵션 `SelectionMode { Cell, Row, MultiRow, Range }`, 선택 행 API `SelectedRowKeys`·`OnSelectedRowsChanged`, 선택 체크 열 `ShowSelectCheck`와 헤더 체크(전체 선택·해제).
- 질의 ①: 기본값. 잠정안 **`Range`** (현재 동작).
- 질의 ②: 선택 체크는 DataSet 값이 아니라 화면 상태로 둘까 (저장·변경 상태에 영향 없음)? 잠정안 **화면 상태**. Check 셀 열 헤더의 전체 체크는 DataSet 값을 바꾸는 별도 기능.
- 질의 ③: 페이징 중 헤더 체크 "전체"의 범위. 잠정안 **적재된 행만** + 앱에 알림.

**Q-13 위쪽 고정 행과 다중 레코드 행** (3-4, 3-6)
둘 다 가상 스크롤·키보드·붙여넣기 구조를 크게 바꾼다.
- 질의 ①: v1(P0/P1) 범위인가, P2인가? 잠정안: **위쪽 고정 행 P1, 다중 레코드 행 P2**.
- 질의 ②: 다중 레코드 행에서 Excel 붙여넣기·범위 선택 기준 (레코드 기준 vs 화면 줄 기준). 잠정안: 레코드의 논리 열 순서 기준.

**Q-14 ★ 셀 식과 조건부 스타일 식** (5-5, 7-6, 7-7)
A-44 ④·C-04(2026-10-29): "그리드는 식을 평가하지 않는다. 앱이 `RuleExpression`을 `OnCellValueChanged`에서 쓴다." Control Spec은 그리드 기능으로 결정했다.
- (a) A-44 ④ 유지. 대신 열 파라미터 `Compute`(행 → 값, 앱 함수)와 `CellStyle`(행·값 → 스타일 클래스, 앱 함수)를 추가한다. TOON 정의의 식은 `Quro.Grid.Definition`이 함수로 만들어 넘긴다 (그리드는 식 문법을 모름).
- (b) A-44 ④를 바꿔 그리드가 `RuleExpression`을 직접 평가 (정의 열의 `expr`·`style` 식).
- 잠정안: **(a)**. 계산 열은 저장 값이 아닌 표시 값인가(편집 불가·저장 안 함)? 잠정안 **표시 전용**. 저장해야 하는 계산(금액)은 지금처럼 `rules`의 `calc`.
- 함께 결정: 스타일 범위 — 글자색·배경·굵게만 (CSS 클래스·토큰), 임의 CSS 금지. 잠정안 그대로.

**Q-15 이벤트 이름** (5-6, 5-7)
- (a) 현재 `On…` 이름 유지, 새 이벤트도 `OnCellClick`·`OnCellDoubleClick`·`OnCellEditStarted`·`OnCellEditEnded`. Control Spec 이름과의 대응표를 consumer-contract에 적는다 (RowSelected = `OnSelectionChanged`, BeforeRowDelete = `OnRowDeleting`).
- (b) Control Spec 이름으로 바꾼다 (BREAKING).
- 잠정안: **(a)**. 클릭 이벤트는 구독할 때만 서버로 보낸다 (R1·R5).

**Q-16 ★ TOON 스키마: `dataset` 절과 열 필드 이름** (6-1, 6-2)
v0.2는 A-44로 확정·구현됐다 (`label`·`edit`·`fmt`·`hidden`, 검증기 V-01~V-37).
- (a) v0.2 이름 유지. `datasets[N]{id,api,...}`와 열 필드 `align`·`frozen`·형식(Q-19)을 **v0.3에 추가**(비파괴). Control Spec의 `captionKey`·`editType`·`visible`은 `label`·`type`/`edit`·`hidden`에 대응시킨다.
- (b) Control Spec 이름으로 바꾼다 (v0.3, 기존 정의 파일·검증기 BREAKING).
- 잠정안: **(a)**. `api`는 엔드포인트라 공개 저장소 예시는 중립 이름으로.

**Q-17 ★ 트리 그리드 v1** (7-2)
기존 규칙은 "v1 범위 외". v1에 넣는다면:
- 질의 ①: 데이터 형태 — (a) 평면 행 + 부모 키 열(`ParentField`), (b) 수준 열(`LevelField`) + 순서. 잠정안 **(a)**.
- 질의 ②: 자식 지연 조회(펼칠 때 API) 필요? 잠정안 **v1은 전체 적재만**.
- 질의 ③: 트리와 함께 쓰지 않는 기능 — 페이징·그룹핑·정렬(형제 안에서만?). 잠정안 **트리 모드에서 페이징·그룹핑 끔, 정렬은 형제 안에서**.
- 질의 ④: 편집(행 추가 시 위치·들여쓰기 변경) 범위. 잠정안 **자식 추가·삭제만, 끌어 옮기기 없음 (7장 12번과 같음)**.

**Q-18 줄 바꿈과 자동 행 높이** (7-9)
가상 스크롤은 고정 행 높이를 전제로 한다.
- 잠정안: 줄 바꿈 열 속성 `Wrap` + 그리드 옵션 `AutoRowHeight`는 **메모리 모드에서만**, 그려진 행만 재고 스크롤 위치는 추정. 페이징 중에는 무시. 성능 목표(10,000행 첫 화면 1초)는 `AutoRowHeight` 끔 기준.

**Q-19 여러 형식(레이아웃) 전환** (7-10)
- 잠정안: TOON 그리드에 `formats[N]{id,columns}`(열 목록·순서·너비·숨김), 그리드 파라미터 `FormatId`·메서드 `SetFormat(id)`. 사용자 레이아웃(`GetLayout`, A-36)은 형식별로 저장. 이름은 열 `Format`(서식)과 겹치므로 공개 API에서는 `View`(`ViewId`·`SetView`)를 제안.

**Q-20 CSV 가져오기 `ImportRows` (A-47)** (7-13)
Control Spec "v1 가져오기 없음"과 반대로 이미 공개 API다.
- (a) 유지 — 그리드에는 사용자용 가져오기 UI가 없고, 앱이 파일을 읽어 행을 넘기는 API일 뿐이다.
- (b) 숨김·폐기 (BREAKING).
- 잠정안: **(a)**.

### C. QuroUI 컨트롤

**Q-21 QuroUI 컨트롤 구현 방식** (8~11장)
- (a) MudBlazor 컴포넌트(`MudTextField`·`MudAutocomplete`/`MudSelect`·`MudDatePicker`)를 감싸 DataSet 바인딩·다국어·필수 표시를 더한다.
- (b) 그리드 셀 에디터처럼 자체 HTML + 얇은 JS.
- 잠정안: **(a)** (CLAUDE.md 4장: 그리드 바깥은 MudBlazor). 다만 연노랑 필수 배경·여러 열 목록·월 모드는 MudBlazor 기본에 없어 감싸는 쪽에서 만든다.

**Q-22 필수 표시 색** (8-3)
- 질의 ①: 텍스트 박스의 연노랑 필수 배경을 **그리드 필수 셀에도** 같이 적용? 잠정안 **같이 적용** (토큰 `--quro-required-bg`, 다크 모드는 A-38 방식으로 섞음).
- 질의 ②: 빨간 별은 그리드 헤더에서도 없앨까? 잠정안 **없음 (배경만)**.

**Q-23 콤보 다중 선택 저장 값** (9-4)
- (a) 구분자 문자열 (예: `"01,03"`, 구분자 옵션).
- (b) 목록 타입 열 (`IReadOnlyList<string>`).
- 잠정안: **(a)**, 저장 API·DB 열을 바꾸지 않아도 된다.

**Q-24 날짜 컨트롤 기간·월 모드 값** (10-1)
- 질의 ①: 기간은 DataSet 열 2개(시작·끝)에 바인딩? 잠정안 **예** (`FromField`·`ToField`).
- 질의 ②: 월 모드 저장 값. (a) `yyyy-MM` 문자열, (b) 그 달 1일 날짜. 잠정안 **(a)**.
- 질의 ③: 영어 "월-일-년"은 사용자 `Culture`의 짧은 날짜 서식을 그대로 쓴다 (en-US). 별도 설정 없음 — 이대로 확정해도 되는가? 잠정안 **예**.

---

## 4. 질의 없이 진행 가능한 항목 (➕, 공개 API 추가)

답변을 기다리는 동안 GS 시나리오로 진행할 수 있는 비파괴 추가. 착수 순서 제안:

1. DataSet 조회·비우기 `LoadAsync(http, url)`·`Clear()` (2-5)
2. DataSet 집계 함수 + `Aggregate.Count`·`Avg`·`Min`·`Max` (2-13, 3-9)
3. 열 가로·세로 정렬 `Align`·`VAlign` (3-2)
4. 행 번호 열·행 상태 표시 옵션 (3-7)
5. 같은 값 연속 셀 합쳐 보이기 `MergeRepeated` (3-11)
6. 잘린 텍스트 툴팁 (7-15, JS만)

각 항목은 공개 API 추가이므로 consumer-contract·CHANGELOG를 같은 PR에서 고치고, 버전은 `0.4.0-alpha.N`(신규 기능 MINOR)로 낸다.

## 5. 답변 후 처리

- 답을 받은 질의는 Addendum에 A-51부터 결정으로 적고, 바뀐 결정(A-24·A-44 ④·A-47 등)은 "변경"으로 표시한다.
- grid-features 규칙의 "v1 범위 외"(셀 수식·트리)와 CLAUDE.md 범위(QuroUI)를 결정에 맞춰 고친다.
- 결정마다 GS 시나리오 번호를 배정한다 (그리드 GS-082~, 정의 GS-D22~, QuroUI는 새 접두사 제안 `GS-U01~`).
