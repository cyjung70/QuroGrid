# Changelog

QuroGrid 공개 API 변경 기록. 형식: 버전 — 날짜, 그 아래 Added / Changed / Fixed / `BREAKING:`.
소비자(QuroERP)는 버전을 올리기 전에 이 파일을 읽는다.

## Unreleased
- Added: 그리드 활성 행 ↔ `QuroDataSet.CurrentRowKey` 양방향 연동 (A-62, GS-085). 앱이 `MoveTo`하면 그리드 활성 셀이 그 행으로 옮겨진다 (포커스는 그대로). 소비자 조치: 없음
- Changed: 활성 행 알림을 `OnSelectionChanged` 구독과 관계없이 보낸다 (행이 멈춘 뒤 100ms에 1회, A-62). 같은 행 안의 이동·입력은 그대로 서버 호출 없음. 소비자 조치: 없음
- Added: `QuroDataSet.CurrentRowKey`·`MoveTo`, 이벤트 `CurrentRowChanged`·`ValueChanged`·`RowAdded`·`RowDeleted`·`Loaded`·`Saved`, 인수 `CurrentRowChangedEventArgs`·`DataSetValueChangedEventArgs`·`DataSetRowEventArgs`·`DataSetSavedEventArgs` — 여러 컨트롤이 한 DataSet의 현재 행을 함께 쓰고 변경을 받는다 (A-51, GS-082~GS-084). 기존 동작은 그대로. 소비자 조치: 없음
- Changed (문서만): 소비자 계약 2·3장 확정 (A-49). `AddQuroGrid`·`IQuroCodeListSource`·`IQuroQueryResolver`는 만들지 않는다 — 앱은 `OptionsFor`·`LookupFor`·`ItemsProvider`·`Localizer` 파라미터로 넘긴다. 코드도움 정의 파일 이름은 `{id}.lookup.toon` 필수. 코드 변경 없음. 소비자 조치: 공통코드·코드도움 서비스를 파라미터 모양에 맞추고, lookup 파일을 `<id>.lookup.toon`으로

## 0.3.0-alpha.1 — 2026-10-29
코드도움 정의 `*.lookup.toon` v0.3 (T-08, A-48, 묶음 L-1~L-3). BREAKING 없음. 소비자 조치: 없음 (쓰려면 `{id}.lookup.toon` 파일과 카탈로그 키를 추가)
- Changed: `quro-deflint`가 `*.lookup.toon`도 검사한다 (V-01~V-03·V-33, 카탈로그가 있으면 V-34와 화면 교차 검사 V-35~V-37, A-48). 표준 오류 요약이 `N screen(s), M lookup(s), K error(s)`로 바뀜. 소비자 조치: 없음
- Added: `ScreenDefValidator.Validate(LookupDef)` (V-34), 생성자 `ScreenDefValidator(catalog, lookups)` — 화면 × lookup 교차 검사 V-35~V-37 (GS-D20·GS-D21, A-48). 기존 생성자 동작은 그대로. 소비자 조치: 없음
- Added: `LookupDefLoader.Load`, `LookupDef`·`LookupParamDef`·`LookupQueryDef`·`LookupColumnDef`, `LookupParamType` — 코드도움 정의 `*.lookup.toon` (T-08, GS-D18·GS-D19, A-48). 구조 오류 V-33. 소비자 조치: 없음

## 0.2.0-alpha.2 — 2026-10-29
CSV 올리기 `ImportRows` (G-05). BREAKING 없음. 소비자 조치: 없음
- Added: `QuroGrid.ImportRows(fields, rows)`, `ImportResult` — CSV 올리기 (G-05, GS-D17, A-47). 앱이 파싱한 행을 붙여넣기 경로로 끝에 추가. 소비자 조치: 없음

## 0.2.0-alpha.1 — 2026-10-29
화면 정의(TOON) 묶음 D-1~D-6. 새 패키지 `Quro.Grid.Definition`·`Quro.Grid.DefLint`, `Quro.Grid`가 `Quro.Grid.Definition`을 참조. BREAKING 없음. 소비자 조치: 새 리소스 키 3개(`quro.validation.minExclusive`·`maxExclusive`·`notEqual`) 문구 추가
- Added: `Quro.Grid.DefLint` 패키지 — .NET 도구 `quro-deflint <폴더> [--catalog catalog.json]` (T-12, GS-D16, A-46). CI에서 화면 정의를 검사한다. 출력 `파일:줄: V-xx 메시지`, 종료 코드 0/1/2
- Added: `QuroGrid.LookupFor`·`OptionsFor` — 정의 열(`lookup`·`select`)의 코드 조회 함수·목록을 앱이 준다 (A-46)
- Added: `QuroDataSet.AddBlankRow` — 손대지 않은 동안 `Validate`·`GetChanges`·`HasChanges`가 건너뛰는 입력용 빈 행. `initRows`가 이것을 쓴다 (A-46)
- Added: `<QuroGrid Definition="@gridDef">` (T-11, GS-D11~GS-D15, A-45) — 정의에서 열·고정 열·삭제 행 표시·클립보드·합계 행·페이지 크기·검증 규칙·`initRows`. `Quro.Grid`가 `Quro.Grid.Definition`을 참조
- Added: `QuroGrid.Clipboard` (G-03), `QuroColumn.NewRowOnly` (G-01), `QuroColumn.Filterable`·`Sortable`·`Groupable` (G-04)
- Added: `FieldRule.MinExclusive`·`MaxExclusive`·`NotEqual`·`MessageKey`, `ValidationKeys.MinExclusive`·`MaxExclusive`·`NotEqual` (G-02). 소비자 조치: 새 리소스 키 3개 `quro.validation.minExclusive`·`maxExclusive`·`notEqual`의 문구를 `IStringLocalizer`에 추가
- Changed: `QuroDataSet` 필드 이름을 정확히 같은 이름이 없으면 대소문자 무시로도 찾는다 (정의 id camelCase ↔ DTO PascalCase). 기존 이름은 그대로 동작
- Added: `Quro.Grid.Definition` 패키지 (GS-D01·GS-D02, A-44) — `ScreenDefLoader.Load`, `DefinitionResult<T>`, `DefinitionError`, `SourceInfo`, 모델 `ScreenDef`·`PermDef`·`FormDef`·`FieldDef`·`ReadOnlyRule`·`GridDef`·`ColumnDef`·`RuleDef`·`ValidateDef`, 열거형 `ScreenLayout`·`ToolbarCommand`·`FieldType`·`FieldMode`·`CaseRule`·`ColumnType`·`ColumnEdit`·`ColumnAlign`·`RuleAction`·`DeletedRowsMode`. strict TOON 서브셋(설계서 3장), 오류 V-01~V-03. 소비자 조치: 없음 (새 패키지)
- Added: `RuleExpression.Parse`·`Evaluate(valueOf, columnValues)`, `LockExpression.Parse`·`Evaluate` (GS-D08~GS-D10) — 화이트리스트 식, `decimal`, 빈 값 0, 0으로 나누기 = null. `ScreenDefLoader.Load`가 식·lockWhen 문법과 lockWhen 필드를 V-18로 보고. 소비자 조치: 없음
- Added: `ScreenDefValidator.Validate`, `IQuroDefinitionCatalog` (GS-D03·GS-D04·GS-D06) — 카탈로그 검사 V-19~V-24, SQL·Clean-room·색 V-30~V-32. `ScreenDefLoader.Load`가 기본값 토큰 문법(설계서 5.5)을 V-03으로 보고. `ReadOnlyRule`에 `Source` 추가 (아직 배포 전이라 소비자 조치 없음)
- Added: `FieldModeResolver.Resolve`, `FormState`, `FieldState` (GS-D07). `ScreenDefLoader.Load`가 구조 검사 V-04~V-18도 보고 (GS-D03·GS-D05, lockWhen 필드는 D-4). 소비자 조치: 없음
- Changed: 화면 정의(TOON) 계약을 v0.2 스키마로 (문서만, Addendum A-44) — consumer-contract 4·6장. `Quro.Grid.Definition`·`<QuroGrid Definition>`은 구현 예정이라 아직 API 변화 없음

## 0.1.0-alpha.1 — 2026-10-29
첫 배포. `Quro.Grid.Data`, `Quro.Grid` (P0 + P1 전체). `Quro.Grid.Definition`은 아직 없음 (I-02)
- Added: 모든 공개 API에 XML 문서 주석 (IntelliSense)
- Added: `docs/integration/consumer-contract.md` 초안
- Added: 헤더 필터 (GS-071~GS-073, A-40) — `Filters`, `SetFilters`, `ClearFilters`, `OnFilterChanged`, `ColumnFilter`, `FilterChangedArgs`, `GridItemsRequest.Filters`, `GridExportRequest.Filters`, 리소스 키 `quro.filter.*`. 소비자 조치: 페이징 화면의 `ItemsProvider`는 `request.Filters`를 조회 조건에 반영해야 한다 (반영하지 않으면 필터가 무시된 결과가 보인다)
- Fixed: 페이징 모드에서 정렬·필터를 바꾼 뒤 행이 다시 그려지지 않던 문제 (PR #25)
- Added: 그룹핑·소계 (GS-074·GS-075, A-42) — `SetGroupBy`, `ClearGroupBy`, `GroupColumns`, `ExpandAll`, `CollapseAll`, 리소스 키 `quro.group.*`, CSS `.quro-group-head-row`·`.quro-subtotal-row`. 메모리 모드만
- Added: CSV 내보내기 요청 (GS-079, A-43) — `ExportFormat` (`Xlsx`·`Csv`, JSON `"xlsx"`·`"csv"`), `GridExportRequest.Format` (기본 `Xlsx`), `GetExportRequest(ExportFormat format = Xlsx)`. 기존 호출은 그대로 xlsx. 소비자 조치: 저장 API가 `format`을 읽어 CSV를 만든다
- Changed: 라이선스 MIT (`LICENSE.md`, Addendum A-41) — `Directory.Build.props`의 `PackageLicenseExpression`도 `MIT`
