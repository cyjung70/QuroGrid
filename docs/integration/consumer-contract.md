# QuroGrid 소비자 계약 (Consumer Contract)

> 정본. QuroERP를 비롯한 소비자는 이 문서만 보고 QuroGrid를 쓸 수 있어야 한다.
> 공개 API를 바꾸는 PR은 이 문서와 `CHANGELOG.md`를 함께 고친다. (규칙: `.claude/rules/public-api.md`)
> 상태: v0.1 초안 — 이름은 구현과 함께 확정한다. `[미정]` 표시는 바뀔 수 있다.

## 1. 패키지
| 패키지 | 내용 | 소비자 참조 위치 (QuroERP) |
|---|---|---|
| `Quro.Grid.Data` | QuroDataSet, IQuroDataSet, RowState, ChangeSet, SaveResult, QuroMask | `QuroErp.Web` (전이 참조로 충분) |
| `Quro.Grid.Definition` | TOON 화면 정의 파서·검증기·불변 모델 | `QuroErp.Web` (전이 참조로 충분) |
| `Quro.Grid` | Blazor 컴포넌트(RCL), quro-grid.js | `QuroErp.Web` |
| `Quro.UI` | QuroUI 컨트롤(RCL): `QuroTextBox`, `QuroComboBox`, `QuroDatePicker`, `QuroLabel`, `QuroStaticText`, `QuroMaskBox`, quro-ui.js (A-53·A-63~A-66·A-69) | `QuroErp.Web` |
| `Quro.Grid.DefLint` | .NET 도구 `quro-deflint` (화면 정의 린트, CI용) | 저장소 도구 매니페스트 (`dotnet tool install --local`) |

- 대상 프레임워크: `net10.0`. 렌더 모드: Interactive Server만 지원.
- UI 라이브러리 의존 없음 (A-67, 0.5.0-alpha.1부터): 패키지는 MudBlazor를 참조하지 않는다. 셀 달력·Combo 목록은 그리드가 그린다. 앱이 MudBlazor를 쓰면 자체 패키지 참조와 `AddMudServices()`를 둔다
- 테마: 색은 `--quro-*` 토큰을 그리드·컨트롤이나 바깥 요소에 두어 바꾼다. 없으면 기본값(밝은 색). 다크 모드는 앱이 다크 값의 토큰을 둔다 (A-38·A-67)
- 정적 자산: `_content/Quro.Grid/quro-grid.js`, `_content/Quro.Grid/quro-grid.css`, `_content/Quro.UI/quro-ui.css` (앱이 `<link>`로 넣는다), `_content/Quro.UI/quro-ui.js` (컨트롤이 가져온다)
- 배포 피드: 로컬 폴더 피드 `/WRK/_nuget` (I-06). nuget.org 공개는 1.0 이후 별도 결정.

## 2. 등록 (A-49)
QuroGrid는 DI 등록 메서드(`AddQuroGrid`)와 서비스 인터페이스를 두지 않는다. 앱은 정적 자산을 넣고, 데이터와 동작은 컴포넌트 파라미터로 넘긴다.
```razor
@* App.razor (QuroErp.Web) *@
<link rel="stylesheet" href="_content/Quro.Grid/quro-grid.css" />
<script src="_content/Quro.Grid/quro-grid.js"></script>

@* 화면 *@
<QuroGrid DataSet="@lines" Definition="@gridDef"
          Localizer="@L" Culture="@culture" EnterDirection="EnterDirection.Right"
          OptionsFor="@codes.OptionsFor" LookupFor="@lookups.FindAsync" />
```
- 앱의 공통코드·코드도움 서비스(이름·등록 방식은 앱이 정한다)가 아래 함수 모양을 구현해 파라미터로 넘긴다. QuroGrid는 그 서비스의 타입을 모른다.
- 정의 로드·카탈로그 검사는 앱이 시작할 때 `ScreenDefLoader`·`LookupDefLoader`·`ScreenDefValidator`로 한다 (6장). 정의 폴더 위치도 앱이 정한다.

## 3. 앱(소비자)이 구현해야 하는 것
| 앱이 넘기는 것 | 모양 | 책임 | QuroGrid가 하지 않는 것 |
|---|---|---|---|
| `OptionsFor` (그리드) | `Func<string, IReadOnlyList<ComboOption>?>` | select 열 source(예 `code:S910`) → (값, 표시명) 목록, 사용자 언어별. 직접 선언한 열은 `QuroColumn.Options` | 공통코드 테이블·API를 모름 |
| `LookupFor` (그리드) | `Func<string, string, Task<IReadOnlyList<string>>>` | lookup id, 입력 문자열 → 일치 코드 (0건·1건·복수, Tab·F4 규칙). 직접 선언한 열은 `QuroColumn.Lookup`. 팝업 목록은 `OnPopupRequested` | HTTP·SQL을 모름 |
| `ItemsProvider` (그리드, 페이징) | `Func<GridItemsRequest, Task<GridItemsResult<TItem>>>` | 정렬·필터·페이지 → 행과 `TotalCount`·`Totals` | 조회 API를 모름 |
| `IQuroDefinitionCatalog` (검증기) | 문자열 집합 5개 (6장) | 존재하는 리소스 키·code Major·lookup id·핸들러·권한 | resx·API를 직접 읽지 않음 |
| `Localizer` (그리드) | `IStringLocalizer` | 리소스 키 → 문구 | 문구를 갖지 않음 |
| 저장 URL | `QuroDataSet.SaveAsync(http, url)`에 전달 | 저장 API 호출 | 엔드포인트 이름을 모름 |
| 업무 판정 | `SetCellMark`, `OnCellValueChanged` | Minus 재고, 금액 계산, 저장 가능 여부 | 판정하지 않음 |

## 4. 컴포넌트 · 이벤트 (요약)
- `<QuroGrid DataSet="…">` + `<QuroColumn>` 직접 선언, 또는 `<QuroGrid Definition="@gridDef">` (`GridDef`는 `Quro.Grid.Definition`, Addendum A-44·A-45). 둘을 같이 쓰면 정의 열 뒤에 직접 선언한 열
- 그리드 옵션: `Clipboard` (복사·붙여넣기, 기본 true, G-03)
- CSV 올리기 (G-05, A-47): `Task<ImportResult> ImportRows(IReadOnlyList<string> fields, IReadOnlyList<string[]> rows)`. 앱이 파일을 읽고 헤더를 필드 이름에 맞춰 부른다. 행은 끝에 추가, 셀은 붙여넣기와 같이 그리드 `Culture`로 읽고 이벤트 발생, 건너뛴 셀은 `ImportResult.Skipped`(`PasteSkip`, `PasteKeys`). Undo 한 단계. `Clipboard`와 무관, `OnPasting`/`OnPasted` 없음. 모르는 필드·Button 열은 `ArgumentException`
- 정의 열의 데이터 (A-46): `LookupFor` (`Func<string, string, Task<IReadOnlyList<string>>>`: lookup id, 입력 문자열 → 일치 코드. `Lookup`과 같은 규칙), `OptionsFor` (`Func<string, IReadOnlyList<ComboOption>?>`: select 열의 source(예 `code:S910`) → 목록). 정의나 이 파라미터가 바뀔 때만 다시 묻는다. 목록을 다시 읽으려면 새 함수를 넘긴다
- 컬럼 옵션: `NewRowOnly` (추가 행에서만 편집, G-01), `Filterable`·`Sortable`·`Groupable` (기본 true, G-04)
- 계산 열·조건부 스타일 (A-59, GS-092·GS-093, 0.5.0-alpha.3부터): 그리드는 식을 평가하지 않고 앱 함수만 부른다.
  - `Compute` (`Func<object, object?>`, 인자 = 행 항목 `TItem`): 표시 전용 값. 열은 항상 읽기 전용(편집·붙여넣기·저장 없음, 붙여넣기는 `PasteKeys.ReadOnly`로 건너뜀), `Field`는 식별자일 뿐 DataSet 필드가 아니어도 된다. 표시는 열 `Type`·`Format`. 헤더 필터·정렬·그룹과 내보내기 요청에서 빠진다. 합계 행 `Sum`은 계산 값을 더한다(서버 페이징 중에는 비움). 행이 바뀌어 다시 그려질 때 다시 계산(서버 호출 늘지 않음). 저장할 계산(금액)은 지금처럼 `OnCellValueChanged`
  - `CellStyle` (`Func<object, object?, CellTone>`, 인자 = 행 항목, 셀 값 — `Compute` 열이면 계산 값): `[Flags] enum CellTone { None, Danger, Success, Muted, Highlight, Bold }`. 셀 클래스 `quro-tone-danger`·`-success`·`-muted`·`-highlight`·`-bold`. 색 토큰 `--quro-tone-danger`(기본 `--quro-error-line`)·`--quro-tone-success`·`--quro-tone-muted`(기본 `--quro-check-line`)·`--quro-tone-highlight`. 오류·경고 표시(`SetCellMark`) 배경이 `Highlight`보다 우선. 임의 CSS는 받지 않는다
- Mask 셀 (A-56, GS-095, 0.5.0-alpha.7부터): `Type="CellType.Mask"` + `Mask` 패턴(QuroMaskBox 4.8과 같은 문법: `0` 숫자, `A` 모든 언어의 글자, `*` 글자나 숫자, `\` 다음 문자 고정, 그 밖은 고정 문자). **문자열** 열만. 저장 값은 고정 문자를 뺀 입력 문자만(`123-45-67890` → `1234567890`), 빈 입력은 null. 셀·에디터·헤더 필터·그룹 머리는 고정 문자와 함께 보이고(맞지 않는 저장 값은 그대로), 필터는 보이는 글자와 저장 값 모두로 찾는다. 확정·붙여넣기는 고정 문자가 있든 없든 마스크 끝까지 맞아야 하며, 덜 채우거나 맞지 않으면 확정하지 않고 편집을 유지(Number와 같음, QuroMaskBox는 마지막 값으로 되돌림), 붙여넣기는 `PasteKeys.Invalid`로 건너뜀. 입력 중 고정 문자 넣기·맞지 않는 문자 버리기는 quro-grid.js(R2, 에디터 `data-mask`). 셀 `data-type="mask"`. `Mask` 없이 `CellType.Mask`, 다른 타입에 `Mask`, 입력 자리 없는 마스크, 문자열이 아닌 열은 `ArgumentException`. 마스크 해석은 `Quro.Grid.Data`의 `QuroMask`(`Parse`·`TryRead`·`Format`·`Length`)로 QuroMaskBox와 같다
- Image 셀 (A-56, GS-096, 0.5.0-alpha.7부터): `Type="CellType.Image"`, **문자열** 열의 값 = 이미지 URL. 행 높이(`--quro-row-height`, 기본 28px)에서 6px 뺀 높이로 비율을 유지해 그린다(CSS 클래스 `quro-image`). 대체 텍스트는 선택 `TextField`(그리드 행의 필드, 없으면 빈 문자열 = 장식). 빈 값이면 아무것도 그리지 않는다. 편집·붙여넣기 없음(붙여넣기는 `PasteKeys.ReadOnly`, `aria-readonly`), Ctrl+C는 URL을 복사. 이미지 클릭·셀에서 Enter·Space → `OnCellClick`(`CellClickArgs<TItem>`: `RowKey`·`Field`·`Item`·`Value`), 서버 호출 1회, 삭제 표시 행에서는 발생하지 않음. **`OnCellClick`은 Image 셀만** 낸다(다른 셀 클릭은 서버를 부르지 않음). 셀 `data-type="image"`. 문자열이 아닌 열·없는 `TextField`는 `ArgumentException`
- 코드 DataSet Combo (A-57, GS-094, 0.5.0-alpha.6부터): `QuroColumn`의 `OptionsSource`(`IQuroDataSet`, 예: `QuroDataSet<CityCode>`)·`ValueField`(저장되는 코드)·`TextField`(표시). 목록 = 삭제 표시가 아닌 코드 행, 행 순서. 셀 글자·에디터 목록·입력·붙여넣기(코드나 글자, 대소문자 무시)·헤더 필터 값·내보내기 `Options`가 모두 이 목록을 쓴다. 코드 DataSet이 바뀌면(`Load`·`SetValue`·행 추가·삭제) 그리드가 스스로 다시 그린다. 연동 콤보: `FilterField`(코드 행의 부모 값 필드)와 `ParentField`(그리드 행의 부모 값 필드)를 함께 — 그 행의 부모 값과 `FilterField`가 같은 코드만, 부모 값이 null이면 빈 목록. 부모 값이 바뀌어(그리드 편집·붙여넣기·앱의 `SetValue`) 자식 코드가 목록에서 빠지면 자식 셀을 null로(`SetValue` 1회, 같은 Undo 단계, `RevertRow`로 되돌림, `ReadOnly` 열은 그대로). `Options`와 함께 쓰거나, `ValueField`·`TextField`가 없거나, 없는 필드거나, `FilterField`·`ParentField` 한쪽만이면 `ArgumentException`. `Options`·`OptionsFor`는 그대로. 다른 사용자 언어의 글자는 코드 DataSet을 다시 `Load`
- 검증 규칙 `FieldRule`: `Required`, `Min`·`Max` (+ `MinExclusive`·`MaxExclusive`), `NotEqual`, `MessageKey` (범위·ne 오류 키를 바꿈, 필수는 그대로). 기본 키 `quro.validation.required`·`min`·`max`·`minExclusive`·`maxExclusive`·`notEqual` (G-02)
- `QuroDataSet` 필드 이름은 정확히 같은 이름을 먼저(대소문자 구분), 없으면 대소문자를 무시하고 찾는다 (정의 id `qty` → 속성 `Qty`). 대소문자만 다른 속성이 둘 이상이면 정확한 이름만 (A-46)
- `QuroDataSet.AddBlankRow(template)`: 입력용 빈 행 (A-46). 추가할 때의 값 그대로인 동안은 `Validate`·`GetChanges`·`HasChanges`가 건너뛰고 `AcceptChanges` 뒤에도 추가 행으로 남는다. Undo 단계가 아니다. `initRows`가 이것을 쓴다
- `QuroDataSet` 현재 행·변경 알림 (A-51, GS-082~GS-084): `int? CurrentRowKey`, `MoveTo(rowKey)`(없는 키는 `KeyNotFoundException`), C# 이벤트 `CurrentRowChanged`(`OldRowKey`·`NewRowKey`), `ValueChanged`(`RowKey`·`Field`·`OldValue`·`NewValue`, 값이 실제로 바뀔 때만, `RevertRow`·`RejectChanges`·`Undo`·`Redo` 포함), `RowAdded`·`RowDeleted`(`RowKey`; 되돌리기로 들어오고 빠진 행 포함, 저장 후 Deleted 행 제거에서는 다시 내지 않음), `Loaded`, `Saved`(`Result`, `SaveAsync` 성공 후). `Load` 뒤 현재 행은 첫 행(없으면 null). 현재 행이 목록에서 빠지면 같은 위치의 다음 행 → 앞 행 → null. 페이징으로 놓인 행이면 행 키 유지. 이벤트는 호출한 스레드(회로)에서 동기로 발생한다. 그리드 활성 행과 양방향으로 맞춘다 (A-62, GS-085): 그리드에서 행을 옮기면 `MoveTo`, 앱이 `MoveTo`하면 그리드 활성 셀이 그 행의 같은 열로 (포커스·`OnSelectionChanged` 없음, 편집 중·화면에 없는 행이면 그대로)
- `IQuroDataSet` (A-57, 0.5.0-alpha.6부터): `QuroDataSet<T>`가 구현하는 제네릭 아닌 읽기 전용 인터페이스. `RowKeys`, `GetValue(rowKey, field)`, `GetState(rowKey)`, `GetColumn(field)`(없는 필드는 `ArgumentException`), 이벤트 `ValueChanged`·`RowAdded`·`RowDeleted`·`Loaded`. 행 형식을 모르는 컴포넌트(그리드 Combo 열)가 다른 DataSet을 읽을 때 쓴다
- `QuroDataSet` 열 메타 (A-52, GS-086~GS-088): `IReadOnlyList<DataSetColumn> Columns`(`T`의 읽기·쓰기 공개 속성, 선언 순서), `GetColumn(field)`, `SetColumn(column)`(이 DataSet에만, 이름은 속성 이름으로 맞춤; 없는 필드·형식 불일치·바꿀 수 없는 기본값은 `ArgumentException`). `DataSetColumn(Name, Type, Key, Required, Default, MaxLength, Scale)` — `T`의 `[Key]`·`[Required]`·`[MaxLength]`/`[StringLength]`·`[DefaultValue]`에서 만들고 `Scale`은 null. 템플릿 없이 `AddRow`·`AddBlankRow`하면 `Default`를 채운다. `Validate`는 `SetRule` 규칙과 열 `Required`를 함께 본다(같은 필드 오류는 1건, `MaxLength`는 검사하지 않음, A-63·GS-089)
- 이벤트: `OnCellValueChanging`(취소 가능), `OnCellValueChanged`, `OnRowAdding`/`OnRowAdded`, `OnRowDeleting`(취소 가능), `OnPopupRequested`, `OnPasting`(취소 가능), `OnSelectionChanged`, `OnCellButtonClick`(Button 셀), `OnCellClick`(Image 셀, A-56)
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
| `ExportColumn(…, Options, Mask = null)` | `Mask`는 Mask 셀의 마스크 (A-56, 0.5.0-alpha.7부터). 저장 값은 입력 문자만이므로 저장 API가 고정 문자를 넣어 보인다 |
| `GridExportRequest(Columns, Sort, Culture, Filters, Format)` | `Format` 기본 `Xlsx`. JSON은 `"xlsx"`·`"csv"` |
| `ExportFormat` | `Xlsx`, `Csv` |

- 파일은 저장 API가 저장된 데이터로 만든다 (화면의 저장 전 변경분은 넣지 않음, A-29). CSV의 구분자·인코딩·따옴표 처리도 저장 API가 정한다.

### 4.4 QuroUI 텍스트 박스 (Addendum A-53·A-63, GS-U01~GS-U04)
```razor
<QuroTextBox TItem="Customer" DataSet="@header" Field="zip" CharType="QuroCharType.Digits" AutoSelect="true"
             PlaceholderKey="sd.so.shipZip.hint" Localizer="@L" />
```
| 파라미터 | 설명 |
|---|---|
| `DataSet`·`Field` (필수) | DataSet 현재 행(A-51)의 문자열 열. 없는 필드·문자열이 아닌 열은 `ArgumentException` |
| `PlaceholderKey`·`Localizer` | 빈 칸 안내 문구의 리소스 키와 해석기. 해석기가 없으면 키를 그대로 |
| `CharType` | `Any`(기본)·`Digits`(0-9)·`Alpha`(모든 언어 글자)·`AlphaNumeric`. 브라우저가 입력·붙여넣기 때 거른다(IME 조합이 끝날 때) |
| `MaxLength` | null이면 열 메타 `MaxLength` |
| `Password`·`AutoSelect`·`ReadOnly` | 가림 입력 / 포커스 때 전체 선택 / 읽기 전용(회색). 현재 행이 `Deleted`여도 읽기 전용 |

- 값은 입력 확정(포커스 이탈·Enter) 때 `SetValue` 1회. 입력하는 동안 서버 호출 없음 (R2). 현재 행이 없으면 비활성
- 열 메타 `Required`면 연노랑 배경(빨간 별 없음), 저장 막기는 `Validate`(GS-089)
- CSS 클래스 `quro-ui-textbox`·`quro-ui-input`·`quro-ui-required`·`quro-ui-readonly`, 변수 `--quro-required-bg`·`--quro-readonly-bg`(그 밖 `--quro-bg`·`--quro-text`·`--quro-border`·`--quro-active`·`--quro-placeholder`). 없으면 기본값 (A-67)

### 4.5 QuroUI 콤보 박스 (Addendum A-57·A-64, GS-U05~GS-U09)
```razor
<QuroComboBox TItem="Customer" TCode="CityCode" DataSet="@header" Field="city" OptionsSource="@cityCodes"
              ValueField="Code" TextField="Name" FilterField="Country" ParentField="country"
              EmptyKey="common.select" ShowCode="true" Localizer="@L" />
```
| 파라미터 | 설명 |
|---|---|
| `DataSet`·`Field` (필수) | DataSet 현재 행(A-51)의 열. 형식은 `ValueField`와 같아야 한다(nullable 무시). 다르면 `ArgumentException` |
| `OptionsSource`·`ValueField`·`TextField` (필수) | 코드 DataSet과 값·글자 필드. 목록 = 삭제 표시가 아닌 행, 행 순서. 코드 DataSet이 바뀌면(`Load`·`SetValue`·행 추가·삭제) 목록도 바뀐다 |
| `FilterField`·`ParentField` | 연동 콤보: 코드 행 `FilterField` = 바인딩 행 `ParentField`인 코드만 (문자열로 비교, 부모가 null이면 빈 목록). 둘 다 지정하거나 둘 다 비운다. 부모 값이 바뀌어 값이 목록을 벗어나면 null로 지운다(`SetValue` 1회, Undo 가능). 행 이동·`Load`로는 지우지 않는다 |
| `EmptyKey` | 맨 위 빈 항목(“전체”·“선택”)의 리소스 키. 고르면 null |
| `ShowCode` | “01 Seoul”처럼 코드와 글자 |
| `ListColumns` | 펼친 목록의 열 `QuroListColumn(Field, HeaderKey)` (머리글 + 열마다 칸) |
| `Multiple`·`Separator` | 체크 다중 선택. 고른 코드를 `Separator`(기본 `\|`)로 이어 코드 목록 순서로 문자열 열에 저장, 없으면 null (Q-23). 문자열 열만 |
| `PlaceholderKey`·`Localizer`·`ReadOnly` | 텍스트 박스와 같다. `Localizer`는 `EmptyKey`·머리글 키도 해석 |

- 목록 전체를 한 번 렌더한다. 입력으로 거르기(코드·글자 포함, 대소문자 무시)·↓↑·Enter·Esc·마우스는 브라우저(`quro-ui.js`), 서버 호출은 선택할 때 1회 (R2). 같은 값을 고르면 호출 없음
- 자유 입력은 받지 않는다: 고르지 않고 포커스를 잃거나 Esc면 마지막 선택 글자로 돌아간다. 다중 선택 입력 칸은 읽기 전용(Space·클릭으로 체크, 거르기 없음)
- CSS 클래스 `quro-ui-combo`·`quro-ui-combo-list`·`quro-ui-option`·`quro-ui-combo-empty`·`quro-ui-combo-head`·`quro-ui-check`, 상태 `quro-ui-required`·`quro-ui-readonly`, 변수는 4.4와 같고 `--quro-hover-bg` 추가

### 4.6 QuroUI 날짜 선택기 (Addendum A-65, GS-U10~GS-U13)
```razor
<QuroDatePicker TItem="Order" DataSet="@header" Field="orderDate" />
<QuroDatePicker TItem="Filter" DataSet="@filter" Field="fromDate" ToField="toDate" />
<QuroDatePicker TItem="Close" DataSet="@close" Field="closeMonth" Month="true" />
```
| 파라미터 | 설명 |
|---|---|
| `DataSet`·`Field` (필수) | DataSet 현재 행(A-51)의 `DateOnly`·`DateTime`(nullable 포함) 열. 월 모드는 문자열 열. 그 밖은 `ArgumentException` |
| `ToField` | 기간의 끝 열(`Field`가 시작, 같은 종류 열). 입력 칸 2개. 끝이 시작보다 앞이 되는 변경은 거부하고 그 칸은 마지막 값으로 돌아간다 |
| `Month` | 월 모드: `yyyy-MM` 문자열로 저장, 달력은 12달 |
| `Culture` | 표시·입력·달력의 culture. null이면 현재 culture |
| `PlaceholderKey`·`Localizer`·`ReadOnly` | 텍스트 박스와 같다 |

- 표시: culture 짧은 날짜(`d`, 표준 ko-KR `2026. 10. 30.`, en-US `10/30/2026`). 월 모드는 거기서 일을 뺀 형식(ko `2026. 10`, en-US `10/2026`). `DateTime` 열에는 자정 값을 넣는다. 형식은 넘긴 `CultureInfo`의 `ShortDatePattern`을 따른다: `new CultureInfo("ko-KR")`는 서버 Windows 사용자 설정을 따를 수 있으므로, ko에서 `2026-10-30`을 원하면 앱이 `DateTimeFormat.ShortDatePattern = "yyyy-MM-dd"`인 culture를 넘긴다
- 입력 확정(포커스 이탈·Enter) 때 `SetValue` 1회: culture 짧은 날짜, `yyyy-MM-dd`, 숫자만(culture 순서: ko `20261030`, en-US `10302026`). 월 모드는 그 형식, `yyyy-MM`, 숫자 6자리. 읽을 수 없는 입력·nullable이 아닌 열의 빈 입력은 마지막 값으로 돌아간다. 같은 값이면 호출 없음
- 달력은 브라우저(`quro-ui.js`, `Intl`)가 culture 언어의 월·요일 이름과 culture 주 시작 요일로 그린다. 달 이동은 서버 호출 없음(R2), 날짜를 누르면 1회. 칸 클릭·Alt+↓로 열고 Esc로 닫는다
- CSS 클래스 `quro-ui-date`·`quro-ui-date-pop`·`quro-ui-cal-*`, 상태 `quro-ui-required`(시작·끝 중 하나라도 필수)·`quro-ui-readonly`, 변수는 4.4·4.5와 같다

### 4.7 QuroUI 라벨·정적 텍스트 (Addendum A-66, GS-U14~GS-U16)
```razor
<QuroLabel TextKey="sd.so.custName" Localizer="@L" />
<QuroStaticText TItem="Customer" DataSet="@header" Field="credit" Format="N0" Culture="@ci" />
```
| 컴포넌트 · 파라미터 | 설명 |
|---|---|
| `QuroLabel` `TextKey` (필수)·`Localizer` | `<label class="quro-ui-label">`에 `Localizer[TextKey]`. 해석기·번역이 없으면 키 그대로 |
| `QuroStaticText<TItem>` `DataSet`·`Field` (필수) | DataSet 현재 행(A-51)의 값을 `<span class="quro-ui-static">`에 읽기 전용으로. 행 이동·`Load`·값 변경을 따른다. null·현재 행 없음은 빈 칸. 없는 필드는 `ArgumentException` |
| `Format`·`Culture` | .NET 형식 문자열과 culture(기본 현재 culture). `Format`이 없으면 정수 천 단위 구분, 실수 천 단위 구분 + 열 메타 `Scale` 자릿수(없으면 값의 자릿수), 날짜는 culture 짧은 날짜, 그 밖은 `ToString()` |

- 숫자 열은 `quro-ui-static-number`(오른쪽 정렬). 변수 `--quro-label`(라벨 색), `--quro-text`. 브라우저 스크립트 없음
- `Mask` (A-69): 문자열 값을 마스크의 고정 문자와 함께 보인다(4.8과 같은 규칙). 맞지 않는 값은 그대로

### 4.8 QuroUI 마스크 박스 (Addendum A-69, GS-U17~GS-U20)
```razor
<QuroMaskBox TItem="Vendor" DataSet="@header" Field="bizNo" Mask="000-00-00000" PlaceholderKey="mm.vendor.bizNo" Localizer="@L" />
```
| 파라미터 | 설명 |
|---|---|
| `DataSet`·`Field` (필수) | DataSet 현재 행(A-51)의 **문자열** 열. 문자열이 아니거나 없는 필드는 `ArgumentException` |
| `Mask` (필수) | `0` 숫자, `A` 모든 언어의 글자, `*` 글자나 숫자, `\` 다음 문자를 고정 문자로, 그 밖은 고정 문자. 입력 자리가 없으면 `ArgumentException` |
| `PlaceholderKey`·`Localizer`·`AutoSelect`·`ReadOnly` | 텍스트 박스(4.4)와 같다 |

- 저장 값: 고정 문자를 뺀 입력 문자만 (`123-45-67890` → `1234567890`). 마스크 끝까지 채우면 확정(포커스 이탈·Enter) 때 `SetValue` 1회. 빈 칸은 null. 덜 채우거나 자리에 맞지 않으면 마지막 값으로 돌아간다(변경 없음)
- 고정 문자 없이 붙여넣은 값(`1234567890`)도 받는다. 마스크에 맞지 않는 저장 값은 그대로 보인다
- 브라우저(`quro-ui.js`)가 입력 중 고정 문자를 넣고, 자리에 맞지 않는 문자를 버리고, 커서를 같은 입력 문자 뒤에 둔다. 입력 중 서버 호출 없음(R2). `maxlength` = 마스크 길이
- 길이가 달라지는 값(10·11자리 전화번호 등)은 앱이 마스크를 고르거나 텍스트 박스를 쓴다
- CSS 클래스 `quro-ui-mask`·`quro-ui-input`, 상태 `quro-ui-required`·`quro-ui-readonly`, 변수는 4.4와 같다

## 5. 저장 계약
- payload `{ inserted[], updated[], deleted[] }`, 수정·삭제 행은 `version` 포함
- 응답: 2xx → `AcceptChanges`, 409 → `Conflict`, 그 밖 → `Failed`(변경분 유지). 성공 후 재조회는 앱
- 서버는 한 트랜잭션에서 삭제 → 수정 → 추가, `version` 불일치 시 409

## 6. 화면 정의(TOON) 계약
- 스키마: `docs/QuroGrid_TOON_ScreenDef_v0.2.md` 3·4·5장 (Addendum A-44). 시나리오: `tests/scenarios/GS-D.md`
- 정의 파일(`*.screen.toon`)은 소비자 저장소 소유. id는 소비자 glossary의 영문 camelCase 식별자
- 파서·검증기·`FieldModeResolver`·`RuleExpression`은 `Quro.Grid.Definition` (UI 무관 — 소비자 서버도 참조 가능). 공개 타입 목록은 설계서 9장
- 로드: `ScreenDefLoader.Load(path, text)` → `DefinitionResult<ScreenDef>` (`Value`는 오류가 없을 때만, `Errors`는 모든 오류를 줄 순서로, 각 오류 `ToString()` = `파일:줄: V-xx 메시지`). 로드는 문법·키·값(V-01~V-03)과, 그것이 없을 때 구조(V-04~V-18)까지 검사한다. 카탈로그 검사(V-19~V-32)는 `new ScreenDefValidator(catalog).Validate(def)` — 오류 목록, 비면 통과
- 카탈로그: 앱이 `IQuroDefinitionCatalog`를 구현한다 (`ResourceKeys`·`CodeMajors`·`LookupIds`·`Handlers`·`Permissions`, 각각 `IReadOnlySet<string>`). resx·공통코드 시드·`*.lookup.toon` 정의·핸들러·권한 목록에서 만든다. QuroGrid는 이 값들을 해석하지 않고 존재만 확인한다
- 식: `RuleExpression.Parse(expr)`를 앱 시작 시 한 번 파싱해 캐시하고, 표시용 계산은 `OnCellValueChanged`에서 `Evaluate(열 값, 열 전체 값)`로, 저장값은 서버가 같은 코드로 다시 계산한다 (A-44). 결과 null = 0으로 나누기 → 경고. `LockExpression.Parse(lockWhen).Evaluate(폼 값)`이 참이면 `FormState.Locked`
- 표시 상태: `FieldModeResolver.Resolve(mode, FormState, permReadOnly)` → `FieldState(Visible, Editable, Required)` (설계서 5.2). 폼 렌더러(QuroERP)가 쓴다. 앱은 시작할 때 모든 정의를 로드하고 오류가 있으면 시작을 멈춘다 (T-07 잠정)
- 그리드 연결 `<QuroGrid Definition="@gridDef">` (설계서 7.1): 열 id = `Field`, `label` = `HeaderKey`, `edit` false/new = `ReadOnly`/`NewRowOnly`, `lookup` → Popup 셀, `select` → Combo 셀, `money`+`currency` → Currency 셀, `sum` → 합계, `hidden` → 열 없음(DataSet 필드는 있음). 그리드 옵션은 정의 값이 파라미터보다 우선. `validate`의 `gt ge lt le ne`는 DataSet 규칙(메시지 = `msg`). `initRows`는 DataSet이 비었을 때 1회. `req: true`는 DataSet 열 메타 `Required`도 채운다(숨은 열 포함, `false`는 `T`·앱 값을 지우지 않음, 정의 하나에 1회, A-52·GS-D22). 규칙 식·`requires`·`stock`·코드도움 데이터·`Options`는 앱 (A-44·A-45)
- 소비자가 맡는 것: 화면 레지스트리·폼 렌더러, `code:`·`lookup:`·`@` 기본값 토큰 해석, 권한 판정, 저장 커맨드 변환(`ChangeSet` → 소비자 DTO), 서버 권위 재계산
- 코드도움 정의 (A-48, 설계 `docs/QuroGrid_TOON_LookupDef_v0.3_Proposal.md`): 파일 `{id}.lookup.toon` 하나에 코드도움 하나 (파일 이름 = `lookup.id`, 접미사 `.lookup.toon` 필수 — `quro-deflint`는 이 이름만 찾고, 다른 `.toon` 파일은 읽지 않는다. 폴더 구성은 소비자 자유, 예 `lookups/<module>/<id>.lookup.toon`). `LookupDefLoader.Load(path, text)` → `DefinitionResult<LookupDef>` (V-01~V-03, V-33). 모델 `LookupDef`(`Id`·`Title`·`Version`·`Api`·`Key`·`Text`·`MinChars`·`Perm`·`Params`·`Queries`·`Columns`·`Source`), `LookupParamDef`, `LookupQueryDef`, `LookupColumnDef`, `LookupParamType`. 카탈로그 검사 `new ScreenDefValidator(catalog).Validate(lookupDef)` (V-34: `title`·`label` 리소스 키와 `perm`, 그 밖에 V-30~V-32). 화면 교차 검사는 `new ScreenDefValidator(catalog, lookups)`(lookup id → `LookupDef`)로 만든 검증기의 `Validate(screenDef)`: 규칙 `{lookupId}.{query}(인수)`의 조회·인수 개수 V-35, 규칙 대상 열이 lookup 결과 열에 없음 V-36, 화면이 쓰는 lookup(열 `lookup`, `lookup:` source, 규칙)의 정의 없음 V-37. 기존 생성자는 교차 검사를 하지 않는다
- 검증 CLI (T-12, A-46, GS-D16): `dotnet quro-deflint <폴더> [--catalog catalog.json]`. 폴더 아래(하위 폴더 포함) `*.lookup.toon`과 `*.screen.toon`을 모두 로드하고 (lookup: V-01~V-03·V-33, `--catalog`가 있으면 V-34. 폴더에 lookup이 있고 모두 로드되면 화면 교차 검사 V-35~V-37, A-48), `--catalog`가 있으면 카탈로그 검사(V-19~V-32)까지 한다. 표준 출력은 오류 한 줄씩 `파일:줄: V-xx 메시지` (파일 경로는 넘긴 폴더 기준, `/` 구분), 요약·사용법은 표준 오류. 종료 코드 0 = 오류 없음, 1 = 오류, 2 = 사용법 오류(폴더 없음, 알 수 없는 옵션, 카탈로그를 읽을 수 없음)
- 카탈로그 파일 (`--catalog`): JSON 객체, 문자열 배열 5개 `resourceKeys`·`codeMajors`·`lookupIds`·`handlers`·`permissions` (모두 필수, 빈 배열 가능). 예: `tests/Quro.Grid.Definition.Tests/Golden/catalog.json`. 소비자가 빌드 때 resx·공통코드 시드·lookup 등록·핸들러·권한 목록에서 만든다

## 7. 버전 정책
- SemVer. 1.0 전 `0.MINOR.PATCH-alpha.N`. 깨지는 변경 = MINOR 증가 + CHANGELOG `BREAKING:`
- 가능하면 한 버전 동안 `[Obsolete]`로 유지 후 제거
- 같은 버전 번호 재배포 없음
