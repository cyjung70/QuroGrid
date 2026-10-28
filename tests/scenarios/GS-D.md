# GS-D — 화면 정의(TOON) Harness 시나리오 v0.2

> 기준: `docs/QuroGrid_TOON_ScreenDef_v0.2.md` (이하 **설계서**). TOBE 입력: QuroERP TO BE 설계서 v0.1 5장 H1~H8 (QuroHarness 보관, 비공개).
> 테스트 이름: `GS_D0x_<What>` (예: `GS_D01_LoadSalesOrderScreen_Counts`). 테스트를 먼저 쓰고 실패를 확인한 뒤 구현한다.
> 상태: 설계서 6.1절 확정 (Addendum A-44), T-11 = `<QuroGrid Definition>`. GS-D11~GS-D15 완료 (A-45·A-46). T-12 결정 (A-46): GS-D16 착수.

## 1. Harness H1~H8 ↔ 이 저장소

TOBE의 Harness 8개 중 QuroGrid가 판정할 수 있는 것은 여기서, QuroERP 자산(DTO·DB·API)이 필요한 것은 QuroERP에서 돌린다.

| H | 검사 (TOBE 5장) | 여기서 | QuroERP에서 | GS-D |
|---|---|---|---|---|
| H1 | TOON 린트: `[N]`, 헤더 수, id 유일, 열거값 | **전부** (`ScreenDefLoader`, V-01~V-18) | CI가 CLI로 실행 | D01~D05 |
| H2 | 계약: `grid.bind`·열 id ↔ DTO ↔ Dapper ↔ DB | 메타 안의 계약 (규칙 대상·입력 열 존재, V-15~V-17) | DTO·DB 리플렉션 (`Editable_grid_column_maps_to_dto_property`) | D05, D07 |
| H3 | i18n 커버리지 | 키 형식 + 주입 카탈로그 존재 (V-21) | ko·en 리소스 파일 대조로 카탈로그 생성 | D03 |
| H4 | 소스 참조 | `code:`·`lookup:`·`on`·`perm` 카탈로그 존재 (V-19, V-22~V-24) | 공통코드 시드·`ILookupProvider` 등록으로 카탈로그 생성 | D04 |
| H5 | 변환 골든 (xml2toon) | — | **전부** — QuroHarness `harness/` (I-07) | — |
| H6 | 렌더: mode × state | `FieldModeResolver` 매트릭스 (xUnit), 그리드 열 편집 상태 (bUnit) | `QuroForm` 렌더 (bUnit) | D07, D11, D12 |
| H7 | 보안: SQL 금지, 식 안전 | TOON 안 SQL (V-30), 식 화이트리스트 | API 문자열 연결 정적 분석 | D06, D08 |
| H8 | 서버 권위 계산 | 같은 식을 서버에서 쓸 수 있음 (`RuleExpression`은 UI 무관) | 조작된 금액 요청 → 저장값 = 서버 계산 | D09 |

## 2. 골든 파일

위치: `tests/Quro.Grid.Definition.Tests/Golden/`. 필드명은 QuroERP glossary 영문 식별자 (camelCase. 레거시 snake_case 열 이름 금지).

| 파일 | 내용 | 출처 |
|---|---|---|
| `sd/sd.so.entry.screen.toon` | 설계서 4장 스키마의 전체 예 (폼 22, 그리드 1, 열 10, rules 4, validate 3) | TOBE 2.7 예제(QuroHarness 보관본)에서 AS IS 주석을 뺀 것 + 할인 차감식. `customer`(select)의 `max 40`은 V-11에 걸려 비움 |
| `bad/*.toon` | 시나리오별 오류 1개만 넣은 파일 | 이 문서. D-2부터는 골든 텍스트 한 곳을 바꾸는 방식(테스트 코드 안)을 주로 쓴다 |
| `catalog.json` | 테스트 카탈로그 (리소스 키, code Major, lookup id, 핸들러, 권한) | 이 문서 |

카탈로그 내용 (요약): 골든 파일의 모든 `label`·`msg`·`title` 키, `S200 A300 S600 S800 S900 S910`, `salesOrder customer item discount`, `loadCustomer calcDueDate`, `SD.SO.Read SD.SO.Write SD.SO.ReadOnlyTerms`.

## 3. 시나리오

### 3.1 파서·구조 (xUnit, `Quro.Grid.Definition.Tests`) — 묶음 D-1(GS-D01·GS-D02 ✅)·D-2(GS-D03 구조·GS-D05·GS-D07 ✅)·D-3(GS-D03 키·GS-D04·GS-D06 ✅)

| ID | 시나리오 | 입력 | 기대 결과 | H |
|---|---|---|---|---|
| GS-D01 | 골든 로드 | `sd/sd.so.entry.screen.toon` | 오류 0. `Form.Fields` 22, `Grids` 1, `lines.Columns` 10, `Rules` 4, `Validate` 3, `Toolbar` 9. `Key`=`soNo`, `LockWhen` 파싱됨. 열 `qty`의 `Fmt`=`#,##0.###` (값 안 쉼표 보존) | H1 |
| GS-D02 | 개수 불일치 | `columns[10]`에 9행 / 표 행 하나에 값 9개 / `toolbar[9]`에 8개 | 각각 V-01, 메시지에 `파일명:줄`. 오류 3개를 한 번에 보고 (첫 오류에서 멈추지 않음) | H1 |
| GS-D03 | 열거값·구조·리소스 키 | `mode: required` / `type: combo` / `span` 범위 밖·비숨김 0 / id 중복(열↔폼, 그리드↔폼) / `screen.id`≠파일명 / `key` 없음 / `max`가 date에 / `req`인데 `edit=false` / `sum`이 text에 / 그리드 명령인데 그리드 없음 (D-2) · `label`에 `Item Code`(문구) / 카탈로그에 없는 label·title·msg 키 (D-3) | V-03 ×4, V-04 ×2, V-05, V-10, V-11, V-12, V-13, V-14 · V-21 ×4. 문법·키 오류(V-01~V-03)가 있으면 구조 검사는 건너뜀 | H1 H3 |
| GS-D04 | 참조·토큰 | `code:S999` / `lookup:vendor` / 열 `lookup` 미등록 / 규칙 `lookup` 식의 id 미등록 / `on: noSuchHandler` / `default: @tody` / `perm.write`·`readOnlyFor.perm` 미등록 / `select` 열·필드에 `source` 없음 / `number` 열에 `lookup` | V-22 ×4, V-23, V-03 (토큰 문법, 로드 시), V-24 ×2, V-19 ×3 | H4 |
| GS-D05 | 규칙 대상 | `calc` 대상 열 없음 / `calc` 대상이 `edit=true` / `sum` 대상 폼 필드가 `mode=req` / `lookup` 대상 목록에 없는 열 (R5) / `calc` 입력 열 없음 / `calc` 순환 (a←b, b←a) / validate `requires` 필드 없음 | V-15, V-15, V-16, V-15, V-15, V-17, V-18 | H2 |
| GS-D06 | SQL·Clean-room | 값에 `select name from codes where id = 1` / 열 id가 clean-room 금지 식별자 / 열 id `item_code`(snake_case) / 값 `#ffff00` | V-30, V-31, V-31, V-32 | H7 |
| GS-D07 | mode × state 매트릭스 | `FieldModeResolver.Resolve`의 7 mode × 3 state × permReadOnly 2 = 42조합 (`[Theory]`) | 설계서 5.2 표와 일치. 그리드 열 환산: `edit=new` + Inserted → 편집, + Normal → 읽기 | H6 |

### 3.2 식 (xUnit) — 묶음 D-4 (GS-D08~GS-D10 ✅)

| ID | 시나리오 | 입력 | 기대 결과 | H |
|---|---|---|---|---|
| GS-D08 | 식 화이트리스트 | `qty * unitPrice * (1 - dcRate/100)` 파싱 / `System.IO.File` / `qty + "a"` / `eval(x)` / 다른 그리드 열 | 첫째만 성공. 나머지 파싱 오류 `FormatException` (실행 단계까지 가지 않음). 정의 안에서는 다른 그리드 열 = V-15, 문법 오류 = V-18 | H7 |
| GS-D09 | 계산 값 | qty 3, unitPrice 12.50, dcRate 10 → 33.75 / dcRate null → 37.50 / qty 빈 값 → 0 / `x / 0` → null + 경고 / `sum(amount)`에 빈 행 60개 섞임 → 숫자 (NaN 없음, R11) | 모두 `decimal`. 같은 입력이면 서버·클라이언트 결과 같음. `round`는 0에서 먼 쪽 반올림 (2.5 → 3) | H8 |
| GS-D10 | lockWhen | `status in [RE,CL]` status=RE → Locked, =SO → 아님 / `status = RE and type != X` / 없는 필드 | 참·거짓 일치, 마지막은 V-18 | H6 |

### 3.3 그리드 연결 (bUnit, `Quro.Grid.Tests`) — 묶음 D-5 (GS-D11~GS-D15 ✅, A-45)

| ID | 시나리오 | 입력 | 기대 결과 | H |
|---|---|---|---|---|
| GS-D11 | 열 매핑 | 골든 `lines` 그리드를 `<QuroGrid Definition>`으로 연결 | 보이는 열 10, 헤더는 `label` 키의 문구, `amount`는 읽기 전용·합계 행 있음, `itemCd`는 Popup 셀, `qty` 표시는 `fmt` + 사용자 로캘. `LookupFor`는 lookup id(`item`, `discount`)로, `OptionsFor`는 source로 열마다 1회 (A-46) | H6 |
| GS-D12 | `edit=new` (G-01) | `itemCd`를 `edit=new`로 바꾼 정의, 기존 행 1 + 추가 행 1 | 추가 행에서만 편집기 열림. 기존 행 F2·커밋·붙여넣기는 무시, `aria-readonly` | H6 |
| GS-D13 | 규칙 calc 연결 | qty 2 → 5 입력 커밋 | `amount` 즉시 갱신 (서버 왕복 1회, R2: 표시용), `OnCellValueChanged` 1회. 계산은 앱이 `RuleExpression`으로 (A-44) | H8 |
| GS-D14 | validate `gt 0` (G-02) | qty 0 커밋 | 메시지 키 `msg.qty.zero`가 `Validate()` 결과에 포함, 앱이 그 결과로 `SetCellMark` → 셀 오류 표시 (Data: `GS_D14_*` 배타 범위·ne·`MessageKey`·camelCase 필드) | H6 |
| GS-D15 | 그리드 옵션 | `initRows: 3` / `clipboard: false` (G-03) / `frozen: 2` | 빈 행 3 (그리기 1회, R3, DataSet이 비었을 때만, `AddBlankRow`: 손대기 전에는 `HasChanges`·`Validate`·`GetChanges` 대상 아님, Data `GS_D15_*`) / Ctrl+V·복사 무시 / 왼쪽 2열 고정. 열 옵션 `Filterable`·`Sortable`·`Groupable=false` (G-04) | H6 |

### 3.4 CLI (xUnit) — 묶음 D-6 (T-12 결정: A-46)

| ID | 시나리오 | 입력 | 기대 결과 | H |
|---|---|---|---|---|
| GS-D16 | 폴더 검증 CLI | 골든 폴더 + `bad/` 폴더, `--catalog catalog.json` | 정상 폴더 종료 코드 0, `bad/`는 1. 출력 한 줄 = `파일:줄: V-xx 메시지` (CI 주석으로 쓸 수 있는 형식) | H1~H4 H7 |

## 4. 여기서 다루지 않는 것 (QuroERP)

| 항목 | 이유 |
|---|---|
| H2 DTO·Dapper·DB 대조 | QuroERP의 DTO·스키마가 필요 |
| H5 xml2toon 골든 | 레거시 변환기는 QuroHarness 도구 (I-07, 비공개) |
| H6 `QuroForm` 렌더 | 폼 렌더러는 QuroERP |
| H7 API 문자열 연결 정적 분석 | API 코드가 QuroERP |
| H8 조작 요청 통합 테스트 | 저장 API가 QuroERP |
| 이전 GS-D11 (레거시 변환기 골든) | 같은 이유로 폐기. 번호는 새 시나리오에 다시 씀 |

## 5. v0.1 → v0.2 번호 대응

| v0.1 | v0.2 |
|---|---|
| GS-D01 골든 `sd/invoice.toon` (cond 1, fields 30, refs 1) | GS-D01 골든 `sd.so.entry` (TOBE 스키마) |
| GS-D02 `columns[10]`에 9행 | GS-D02 (개수 불일치 3종) |
| GS-D03 `mode: required` | GS-D03 |
| GS-D04 headerKey 카탈로그 없음 | GS-D03 (리소스 키) |
| GS-D05 queries·ref 없음 | GS-D04 (참조) |
| GS-D06 summary.target이 ro 아님 | GS-D05 (`sum` 대상) |
| GS-D07 DataSetSchema 추출 | GS-D11 (그리드 연결에 포함) |
| GS-D08 `edit: new` | GS-D12 |
| GS-D09 금액 열 변경 → 합계 | GS-D13 |
| GS-D10 refs 3행 확정, `AddRows` 1회 | 규칙 `lookup`은 앱 조회 — GS-D15의 `AddRows` 1회 원칙만 유지 |
| GS-D11 레거시 변환기 | 폐기 (QuroERP) |
