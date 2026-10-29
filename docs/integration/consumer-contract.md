# QuroGrid 소비자 계약 (Consumer Contract)

> 정본. QuroERP를 비롯한 소비자는 이 문서만 보고 QuroGrid를 쓸 수 있어야 한다.
> 공개 API를 바꾸는 PR은 이 문서와 `CHANGELOG.md`를 함께 고친다. (규칙: `.claude/rules/public-api.md`)
> 상태: v0.1 초안 — 이름은 구현과 함께 확정한다. `[미정]` 표시는 바뀔 수 있다.

## 1. 패키지
| 패키지 | 내용 | 소비자 참조 위치 (QuroERP) |
|---|---|---|
| `Quro.Grid.Data` | QuroDataSet, RowState, ChangeSet, SaveResult | `QuroErp.Web` (전이 참조로 충분) |
| `Quro.Grid.Definition` | TOON 화면 정의 파서·검증기·불변 모델 | `QuroErp.Web` (전이 참조로 충분) |
| `Quro.Grid` | Blazor 컴포넌트(RCL), quro-grid.js | `QuroErp.Web` |
| `Quro.Grid.DefLint` | .NET 도구 `quro-deflint` (화면 정의 린트, CI용) | 저장소 도구 매니페스트 (`dotnet tool install --local`) |

- 대상 프레임워크: `net10.0`. 렌더 모드: Interactive Server만 지원.
- 정적 자산: `_content/Quro.Grid/quro-grid.js`, `_content/Quro.Grid/quro-grid.css`
- 배포 피드: 로컬 폴더 피드 `/WRK/_nuget` (I-06). nuget.org 공개는 1.0 이후 별도 결정.

## 2. 등록 [미정 — 이름]
```csharp
// Program.cs (QuroErp.Web)
builder.Services.AddQuroGrid(options =>
{
    options.DefinitionRoot = Path.Combine(builder.Environment.ContentRootPath, "Definitions");
    options.EnterDirection = EnterDirection.Right;   // O-05
});
builder.Services.AddSingleton<IQuroCodeListSource, ErpCodeListSource>();   // 공통코드 목록 (앱 구현)
builder.Services.AddSingleton<IQuroQueryResolver, ErpQueryResolver>();     // queryId → 앱 API 호출 (I-03)
builder.Services.AddSingleton<IQuroDefinitionCatalog, ErpDefinitionCatalog>(); // 리소스 키·queryId 목록 (검증용)
```

## 3. 앱(소비자)이 구현해야 하는 것
| 추상화 [미정 — 이름] | 책임 | QuroGrid가 하지 않는 것 |
|---|---|---|
| `IQuroCodeListSource` | Major 코드 → (값, 표시명) 목록, 사용자 언어별 | 공통코드 테이블·API를 모름 |
| `IQuroQueryResolver` | `queryId` + 파라미터 → 행 목록 (페이징) | HTTP·SQL을 모름 |
| `IQuroDefinitionCatalog` | 존재하는 리소스 키·queryId·lookupId 목록 | resx·API를 직접 읽지 않음 |
| `IStringLocalizer` | 리소스 키 → 문구 | 문구를 갖지 않음 |
| 저장 URL | `QuroDataSet.SaveAsync(http, url)`에 전달 | 엔드포인트 이름을 모름 |
| 업무 판정 | Minus 재고, 금액 계산, 저장 가능 여부 → `SetCellMark`, `OnCellValueChanged` | 판정하지 않음 |

## 4. 컴포넌트 · 이벤트 (요약)
- `<QuroGrid DataSet="…">` + `<QuroColumn>` 직접 선언, 또는 `<QuroGrid Definition="@gridDef">` (`GridDef`는 `Quro.Grid.Definition`, Addendum A-44·A-45). 둘을 같이 쓰면 정의 열 뒤에 직접 선언한 열
- 그리드 옵션: `Clipboard` (복사·붙여넣기, 기본 true, G-03)
- CSV 올리기 (G-05, A-47): `Task<ImportResult> ImportRows(IReadOnlyList<string> fields, IReadOnlyList<string[]> rows)`. 앱이 파일을 읽고 헤더를 필드 이름에 맞춰 부른다. 행은 끝에 추가, 셀은 붙여넣기와 같이 그리드 `Culture`로 읽고 이벤트 발생, 건너뛴 셀은 `ImportResult.Skipped`(`PasteSkip`, `PasteKeys`). Undo 한 단계. `Clipboard`와 무관, `OnPasting`/`OnPasted` 없음. 모르는 필드·Button 열은 `ArgumentException`
- 정의 열의 데이터 (A-46): `LookupFor` (`Func<string, string, Task<IReadOnlyList<string>>>`: lookup id, 입력 문자열 → 일치 코드. `Lookup`과 같은 규칙), `OptionsFor` (`Func<string, IReadOnlyList<ComboOption>?>`: select 열의 source(예 `code:S910`) → 목록). 정의나 이 파라미터가 바뀔 때만 다시 묻는다. 목록을 다시 읽으려면 새 함수를 넘긴다
- 컬럼 옵션: `NewRowOnly` (추가 행에서만 편집, G-01), `Filterable`·`Sortable`·`Groupable` (기본 true, G-04)
- 검증 규칙 `FieldRule`: `Required`, `Min`·`Max` (+ `MinExclusive`·`MaxExclusive`), `NotEqual`, `MessageKey` (범위·ne 오류 키를 바꿈, 필수는 그대로). 기본 키 `quro.validation.required`·`min`·`max`·`minExclusive`·`maxExclusive`·`notEqual` (G-02)
- `QuroDataSet` 필드 이름은 정확히 같은 이름을 먼저(대소문자 구분), 없으면 대소문자를 무시하고 찾는다 (정의 id `qty` → 속성 `Qty`). 대소문자만 다른 속성이 둘 이상이면 정확한 이름만 (A-46)
- `QuroDataSet.AddBlankRow(template)`: 입력용 빈 행 (A-46). 추가할 때의 값 그대로인 동안은 `Validate`·`GetChanges`·`HasChanges`가 건너뛰고 `AcceptChanges` 뒤에도 추가 행으로 남는다. Undo 단계가 아니다. `initRows`가 이것을 쓴다
- 이벤트: `OnCellValueChanging`(취소 가능), `OnCellValueChanged`, `OnRowAdding`/`OnRowAdded`, `OnRowDeleting`(취소 가능), `OnPopupRequested`, `OnPasting`(취소 가능), `OnSelectionChanged`
- 셀 표시: `SetCellMark(row, field, CellMark(Error|Warning, resourceKey))`

### 4.1 헤더 필터 (Addendum A-31·A-40)
| 공개 API | 설명 |
|---|---|
| `IReadOnlyList<ColumnFilter> Filters` | 현재 필터 (컬럼마다 하나, 적용 순서) |
| `SetFilters(filters)` · `ClearFilters()` | 앱이 지정·해제. `OnFilterChanged`는 발생하지 않음 |
| `OnFilterChanged` (`FilterChangedArgs.Filters`) | 사용자가 패널에서 적용·해제한 뒤 |
| `ColumnFilter(Field)` { `Contains`, `Min`, `Max`, `From`, `To`, `Values` } | 컬럼 타입별 조건. 컬럼끼리 AND, 범위는 양 끝 포함. `Values`는 언어 무관 문자열 (Combo 코드, Check 저장 값) |
| `GridItemsRequest.Filters` | 페이징 모드: 소비자의 조회 API가 이 조건으로 거른다 (WHERE). 결과 `TotalCount`·`Totals`도 거른 결과 기준 |
| `GridExportRequest.Filters` | 내보내기 요청에 담기는 조건 |
| 리소스 키 | `quro.filter.open`·`apply`·`clear`·`contains`·`min`·`max`·`from`·`to`·`checked`·`unchecked` (소비자 `IStringLocalizer`에 둔다) |
| CSS | `.quro-filter-button`, `.quro-filter-panel`, 헤더 `[data-filtered=true]` |

- `FilterInput`·`OpenFilter`·`CloseFilter`·`ApplyFilter`는 quro-grid.js가 부르는 진입점이다. 소비자는 직접 부르지 않는다.

### 4.2 그룹핑·소계 (Addendum A-32·A-42, 메모리 모드만)
| 공개 API | 설명 |
|---|---|
| `SetGroupBy(IReadOnlyList<string> fields)` · `ClearGroupBy()` | 그룹 컬럼 지정 (목록 순서 = 단 순서). 이벤트 없음. 페이징 중에는 무시 |
| `IReadOnlyList<string> GroupColumns` | 현재 그룹 컬럼 |
| `ExpandAll()` · `CollapseAll()` | 모든 그룹 펼치기·접기 |
| 리소스 키 | `quro.group.header` (`{0}` 헤더, `{1}` 값, `{2}` 건수), `quro.group.subtotal`, `quro.group.empty`. Check 값은 `quro.filter.checked`·`unchecked` |
| CSS | `.quro-group-head-row` (`aria-expanded`, `data-group-level`), `.quro-group-head-cell`, `.quro-subtotal-row`, `.quro-subtotal` |

- 소계는 `Aggregate.Sum` 컬럼만. 그룹 값을 편집해도 행은 그 그룹에 남는다 (다시 `SetGroupBy`하면 새 값으로).
- `ExpandGroup(groupId, expanded)`는 quro-grid.js가 부르는 진입점이다.

### 4.3 내보내기 (Addendum A-28·A-29·A-34·A-43)
| 공개 API | 설명 |
|---|---|
| `GetExportRequest(ExportFormat format = ExportFormat.Xlsx)` | 현재 화면의 컬럼(화면 순서·사용자 언어 헤더·너비)·정렬·필터·로캘. 앱이 조회 조건과 함께 저장 API에 보낸다 |
| `GridExportRequest(Columns, Sort, Culture, Filters, Format)` | `Format` 기본 `Xlsx`. JSON은 `"xlsx"`·`"csv"` |
| `ExportFormat` | `Xlsx`, `Csv` |

- 파일은 저장 API가 저장된 데이터로 만든다 (화면의 저장 전 변경분은 넣지 않음, A-29). CSV의 구분자·인코딩·따옴표 처리도 저장 API가 정한다.

## 5. 저장 계약
- payload `{ inserted[], updated[], deleted[] }`, 수정·삭제 행은 `version` 포함
- 응답: 2xx → `AcceptChanges`, 409 → `Conflict`, 그 밖 → `Failed`(변경분 유지). 성공 후 재조회는 앱
- 서버는 한 트랜잭션에서 삭제 → 수정 → 추가, `version` 불일치 시 409

## 6. 화면 정의(TOON) 계약
- 스키마: `docs/QuroGrid_TOON_ScreenDef_v0.2.md` 3·4·5장 (Addendum A-44). 시나리오: `tests/scenarios/GS-D.md`
- 정의 파일(`*.screen.toon`)은 소비자 저장소 소유. id는 소비자 glossary의 영문 camelCase 식별자
- 파서·검증기·`FieldModeResolver`·`RuleExpression`은 `Quro.Grid.Definition` (UI 무관 — 소비자 서버도 참조 가능). 공개 타입 목록은 설계서 9장
- 로드: `ScreenDefLoader.Load(path, text)` → `DefinitionResult<ScreenDef>` (`Value`는 오류가 없을 때만, `Errors`는 모든 오류를 줄 순서로, 각 오류 `ToString()` = `파일:줄: V-xx 메시지`). 로드는 문법·키·값(V-01~V-03)과, 그것이 없을 때 구조(V-04~V-18)까지 검사한다. 카탈로그 검사(V-19~V-32)는 `new ScreenDefValidator(catalog).Validate(def)` — 오류 목록, 비면 통과
- 카탈로그: 앱이 `IQuroDefinitionCatalog`를 구현한다 (`ResourceKeys`·`CodeMajors`·`LookupIds`·`Handlers`·`Permissions`, 각각 `IReadOnlySet<string>`). resx·공통코드 시드·`ILookupProvider` 등록·핸들러·권한 목록에서 만든다. QuroGrid는 이 값들을 해석하지 않고 존재만 확인한다
- 식: `RuleExpression.Parse(expr)`를 앱 시작 시 한 번 파싱해 캐시하고, 표시용 계산은 `OnCellValueChanged`에서 `Evaluate(열 값, 열 전체 값)`로, 저장값은 서버가 같은 코드로 다시 계산한다 (A-44). 결과 null = 0으로 나누기 → 경고. `LockExpression.Parse(lockWhen).Evaluate(폼 값)`이 참이면 `FormState.Locked`
- 표시 상태: `FieldModeResolver.Resolve(mode, FormState, permReadOnly)` → `FieldState(Visible, Editable, Required)` (설계서 5.2). 폼 렌더러(QuroERP)가 쓴다. 앱은 시작할 때 모든 정의를 로드하고 오류가 있으면 시작을 멈춘다 (T-07 잠정)
- 그리드 연결 `<QuroGrid Definition="@gridDef">` (설계서 7.1): 열 id = `Field`, `label` = `HeaderKey`, `edit` false/new = `ReadOnly`/`NewRowOnly`, `lookup` → Popup 셀, `select` → Combo 셀, `money`+`currency` → Currency 셀, `sum` → 합계, `hidden` → 열 없음(DataSet 필드는 있음). 그리드 옵션은 정의 값이 파라미터보다 우선. `validate`의 `gt ge lt le ne`는 DataSet 규칙(메시지 = `msg`). `initRows`는 DataSet이 비었을 때 1회. 규칙 식·`requires`·`stock`·코드도움 데이터·`Options`는 앱 (A-44·A-45)
- 소비자가 맡는 것: 화면 레지스트리·폼 렌더러, `code:`·`lookup:`·`@` 기본값 토큰 해석, 권한 판정, 저장 커맨드 변환(`ChangeSet` → 소비자 DTO), 서버 권위 재계산
- 카탈로그 `IQuroDefinitionCatalog`: 리소스 키, code Major, lookup id, 핸들러 id, 권한 id [이름 미정]
- 코드도움 정의 (A-48, 설계 `docs/QuroGrid_TOON_LookupDef_v0.3_Proposal.md`): 파일 `{id}.lookup.toon` 하나에 코드도움 하나. `LookupDefLoader.Load(path, text)` → `DefinitionResult<LookupDef>` (V-01~V-03, V-33). 모델 `LookupDef`(`Id`·`Title`·`Version`·`Api`·`Key`·`Text`·`MinChars`·`Perm`·`Params`·`Queries`·`Columns`·`Source`), `LookupParamDef`, `LookupQueryDef`, `LookupColumnDef`, `LookupParamType`. 카탈로그 검사 `new ScreenDefValidator(catalog).Validate(lookupDef)` (V-34: `title`·`label` 리소스 키와 `perm`, 그 밖에 V-30~V-32). 화면 교차 검사는 `new ScreenDefValidator(catalog, lookups)`(lookup id → `LookupDef`)로 만든 검증기의 `Validate(screenDef)`: 규칙 `{lookupId}.{query}(인수)`의 조회·인수 개수 V-35, 규칙 대상 열이 lookup 결과 열에 없음 V-36, 화면이 쓰는 lookup(열 `lookup`, `lookup:` source, 규칙)의 정의 없음 V-37. 기존 생성자는 교차 검사를 하지 않는다
- 검증 CLI (T-12, A-46, GS-D16): `dotnet quro-deflint <폴더> [--catalog catalog.json]`. 폴더 아래(하위 폴더 포함) `*.screen.toon`을 모두 로드하고, `--catalog`가 있으면 카탈로그 검사(V-19~V-32)까지 한다. 표준 출력은 오류 한 줄씩 `파일:줄: V-xx 메시지` (파일 경로는 넘긴 폴더 기준, `/` 구분), 요약·사용법은 표준 오류. 종료 코드 0 = 오류 없음, 1 = 오류, 2 = 사용법 오류(폴더 없음, 알 수 없는 옵션, 카탈로그를 읽을 수 없음)
- 카탈로그 파일 (`--catalog`): JSON 객체, 문자열 배열 5개 `resourceKeys`·`codeMajors`·`lookupIds`·`handlers`·`permissions` (모두 필수, 빈 배열 가능). 예: `tests/Quro.Grid.Definition.Tests/Golden/catalog.json`. 소비자가 빌드 때 resx·공통코드 시드·lookup 등록·핸들러·권한 목록에서 만든다

## 7. 버전 정책
- SemVer. 1.0 전 `0.MINOR.PATCH-alpha.N`. 깨지는 변경 = MINOR 증가 + CHANGELOG `BREAKING:`
- 가능하면 한 버전 동안 `[Obsolete]`로 유지 후 제거
- 같은 버전 번호 재배포 없음
