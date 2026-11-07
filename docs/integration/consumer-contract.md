# QuroGrid 소비자 계약 (Consumer Contract)

> 정본. QuroERP를 비롯한 소비자는 이 문서만 보고 QuroGrid를 쓸 수 있어야 한다.
> 공개 API를 바꾸는 PR은 이 문서와 `CHANGELOG.md`를 함께 고친다. (규칙: `.claude/rules/public-api.md`)
> 상태: v0.1 초안 — 이름은 구현과 함께 확정한다. `[미정]` 표시는 바뀔 수 있다.

## 1. 패키지
| 패키지 | 내용 | 소비자 참조 위치 (QuroERP) |
|---|---|---|
| `Quro.Grid.Data` | QuroDataSet, IQuroDataSet, RowState, ChangeSet, SaveResult, LoadResult, QuroMask | `QuroErp.Web` (전이 참조로 충분) |
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
- 선택 즉시 편집 (A-58, GS-098, 0.5.0-alpha.8부터): 그리드 파라미터 `EditOnSelect` (기본 false). 켜면 편집 가능한 셀(Text·Number·Currency·Mask·Popup·Date·Combo, 읽기 전용·삭제 표시 행 제외)에 서는 즉시 quro-grid.js가 브라우저 에디터(`.quro-editor.quro-select-editor`)를 연다. 서버 호출 없음(R2). 글자는 전체 선택되어 입력하면 덮어쓰고, 화살표는 셀을 떠나며(Shift+화살표 범위 선택), F2나 글자 클릭은 커서 모드(화살표가 글자 안에서 움직임). 셀을 떠날 때(화살표·Tab·Enter·클릭·그리드 밖) 글자가 바뀐 경우에만 `CommitEdit` 1회, 실패하면 에디터 유지·이동 없음. Esc는 셀 값으로 되돌림(서버 호출 없음). 에디터는 한 셀만 선택됐을 때만 있다. 글자가 그대로면 Ctrl+C/V·Ctrl+Z·Insert·Ctrl+Delete는 그리드 동작. Date·Combo 목록은 Alt+↓(서버 에디터로 넘김). 켜면 셀에 `data-edit`(보이는 글자와 다른 편집 글자, 예 `Format="N0"`의 `1,235` ↔ `1234.5`)·`data-mask`, 그리드에 `data-edit-on-select`. TOON 키는 v0.3에서
- 행 읽기 전용 (GS-099, A-70, 0.5.0-alpha.8부터): 그리드 파라미터 `RowReadOnly` (`Func<object, bool>`, 인자 = 행 항목, true = 잠김). 잠긴 행은 `ReadOnly` 열과 같다: 셀 `aria-readonly="true"`, 에디터·`CommitEdit`·Check 토글·목록·F4 거부, `EditOnSelect` 에디터 없음(열려 있으면 닫힘), 붙여넣기는 `PasteKeys.ReadOnly`로 건너뜀, Tab은 건너뜀. 포커스·범위 선택·Ctrl+C·Button/Image 클릭·행 삭제(`OnRowDeleting`)는 그대로. 잠금은 행 상태가 아니다(변경 표시 없음). 그리드가 그릴 때마다 함수를 부르므로 값 변경 없이 잠금만 바꾸면 `Refresh()`를 부른다(바뀐 행만 다시 그림). 행 클래스 `quro-row-readonly`, 회색 토큰 `--quro-readonly-bg`(기본 `--quro-check-line` 14% 혼합). `CellStyle` Highlight와 오류·경고 표시 배경이 이긴다. TOON 키는 v0.3에서
- 행 번호 열 (GS-100, A-71, 0.5.0-alpha.9부터): 그리드 파라미터 `ShowRowNumbers` (기본 false)와 선택 `RowNumberHeaderKey` (리소스 키, 없으면 빈 헤더). 표시 전용: `QuroColumn`도 DataSet 필드도 아니고 행 상태를 바꾸지 않는다. 번호는 화면 순서 1..N(정렬·필터·그룹 뒤, 그룹 머리·소계 행은 세지 않음, 접힌 행은 빠짐), 서버 페이징은 절대 위치(추가 행 포함). 추가·삭제·Undo 뒤 갱신. 숨긴 삭제 표시 행은 빠지고, 보이면(`DeletedRows`) 번호 유지. 왼쪽 고정, 포커스 없음(클릭·화살표·Tab이 건너뜀, 열린 에디터 유지), 복사·붙여넣기·내보내기·정렬·필터·리사이즈 대상 아님. 폭은 자릿수에 맞고 최소 2자리. 켜면 첫 칸은 클래스 `quro-row-number`, 본문은 `role="rowheader"`, 다른 셀 `aria-colindex`와 그리드 `aria-colcount`가 1 늘어난다. 고정 열 `left`는 `calc(var(--qg-row-number-width) + Npx)`. TOON 키는 v0.3에서
- 행 상태 열 (GS-106, A-74, 0.5.0-alpha.10부터): 그리드 파라미터 `ShowRowState` (기본 false). 왼쪽 고정 열에 행마다 기호 하나: 신규 `+`, 수정 `●`, 삭제 `−`, 변경 없음은 빈칸. 행 번호와 함께면 번호 다음. 표시 전용: `QuroColumn`도 DataSet 필드도 아니고, 포커스를 받지 않으며(클릭·화살표·Tab이 건너뜀) 복사·붙여넣기·내보내기·정렬·필터에서 빠진다. 헤더 글자 없음, 그룹 머리·소계·합계 행은 빈칸, 페이징은 적재된 행만. 삭제 기호는 `DeletedRows`가 삭제 행을 보여 줄 때만 보인다. 툴팁과 접근성 이름은 앱 로컬라이저의 `GridKeys.RowStateInserted`(`quro.rowstate.inserted`)·`RowStateUpdated`(`quro.rowstate.updated`)·`RowStateDeleted`(`quro.rowstate.deleted`). 키가 없으면 키 이름이 보인다. 색 토큰 `--quro-row-state-inserted`·`--quro-row-state-updated`·`--quro-row-state-deleted`. 셀 `quro-row-state`, 속성 `data-state`(`inserted`·`updated`·`deleted`), 폭 변수 `--qg-row-state-width`. 앱이 DataSet을 직접 바꾸면(`AcceptChanges`·`SetValue` 등) 다른 셀 표시와 마찬가지로 그리드가 다시 그려질 때 반영된다: 앱의 이벤트 처리기 안이면 자동, 그 밖이면 `Refresh()`
- 열 집계 (GS-108, A-76, 0.5.0-alpha.10부터): `Aggregate`에 `Count`, `Avg`, `Min`, `Max`를 추가했다(`None`, `Sum`의 숫자 값은 그대로). 대상 행은 `Sum`과 같다: 삭제 표시 행과 필터로 걸러진 행을 빼고, 접힌 그룹·트리 행은 포함한다. `Compute` 열은 계산 값을 쓴다. `Count`: 값이 있는 행 수(null과 빈 글자 제외, 체크 해제 값은 값으로 셈). 천 단위 구분 정수로 표시하며 열 `Format`을 쓰지 않는다. `Button` 열은 비운다. `Avg`: 숫자의 평균이며 빈 값은 건너뛴다(0으로 보지 않음). `Min`·`Max`: 숫자 또는 날짜. 셋은 열 `Format`으로 표시하므로 `N0` 열의 평균은 반올림되어 보인다. `Currency` 열은 `Sum`처럼 첫 행 통화 자릿수를 쓴다. `Avg`·`Min`·`Max`는 `Number`·`Currency`·`Date`·`Text`·`Label` 열에서만 동작한다. 맞지 않는 조합(글자 값, 날짜의 `Avg`, `Combo`·`Check`·`Popup`·`Mask`·`Image`·`Button` 열)과 값이 없는 경우는 예외 없이 빈칸이고, `Count`는 값이 없으면 0, `Sum`은 지금처럼 0이다. 합계 행은 값만 보여 주며 어떤 집계인지 표시하지 않는다. **서버 페이징 그리드에서는 새 집계를 비운다**(적재된 행만 알고, 서버 값을 저장 전 편집에 맞게 고칠 수 없음). `Sum`은 지금처럼 서버 합계 + 변경분. `ExportColumn.Aggregate`에 새 값이 그대로 실리고 JSON에는 이름(`"Count"` 등)으로 쓰므로, **새 집계를 쓰는 화면의 내보내기 서버 코드가 이 값을 처리해야 한다.** TOON은 기존 `sum` 키뿐이며 새 키는 v0.3
- 셀 정렬 (GS-102, A-72, 0.5.0-alpha.9부터): 기본은 열 타입으로 정한다. `Number`·`Currency`와 필드가 숫자 형식인 `Text`·`Label`은 오른쪽(`quro-align-right`), `Check`·`Combo`·`Button`·`Image`는 가운데(`quro-align-center`), 나머지(`Date`·`Popup`·`Mask` 포함)는 왼쪽(클래스 없음). `Compute` 열은 `Type`만 본다. `QuroColumn.Align`(`ColumnAlign?`, `Quro.Grid.Definition`)과 TOON `align`이 기본을 덮어쓴다. 본문·합계·소계 셀과 에디터가 따르고, 헤더는 왼쪽 그대로, Combo 목록·달력은 왼쪽. 세로 정렬은 아래 항목
- 셀 세로 정렬 (GS-109, A-77, 0.5.0-alpha.10부터): `QuroColumn.VAlign`(`ColumnVAlign?`, `Quro.Grid.Definition`): `Top`, `Middle`, `Bottom`. null과 `Top`은 지금처럼 위쪽이고 클래스가 없다. `Middle`은 `quro-valign-middle`, `Bottom`은 `quro-valign-bottom`. 본문·합계·소계 셀과 열린 에디터가 따르고, 헤더와 행 번호·행 상태 열은 그대로다. **글자보다 높은 행에서만 보인다.** 일반 그리드의 행은 내용만큼 높고 `RowHeight`는 페이징 그리드의 행 높이만 정하므로, `RowHeight`를 키운 페이징 그리드, 이미지가 있는 행, 앱 CSS로 높인 행에서 보인다. 기본 `RowHeight` 28에서는 차이가 약 3px. CSS `align-content`를 쓰며(2024년 이후 브라우저), 지원하지 않는 브라우저는 깨지지 않고 위쪽에 남는다. TOON 키는 v0.3
- 같은 값 합쳐 보이기 (GS-110, A-78, 0.5.0-alpha.10부터): `QuroColumn.MergeRepeated` (`bool`, 기본 false). 위 셀과 같은 값을 빈칸처럼 그리고 사이 선을 없앤다. **표시만 합친다**: 셀을 실제로 병합하지 않으므로 편집·선택·이동·복사·붙여넣기·정렬·필터·스크린 리더·내보내기는 모든 셀의 실제 값을 그대로 쓴다. 현재 셀, 선택 범위, 열린 에디터는 값을 보여 준다. 같다는 기준은 바로 위아래 행의 **보이는 글자**(`Combo`는 명칭, `Number`는 서식 적용 후, `Compute`는 계산 값)이고 빈 값은 합치지 않는다. 묶음이 끝나는 곳: 왼쪽 합침 열의 값이 바뀔 때, 삭제 표시 행, 오류·경고 표시(`SetCellMark`)가 있는 셀, 그룹 머리 행·소계 행. `Check`·`Button`·`Image` 열에는 효과가 없고, 페이징 그리드와 트리 그리드는 이 설정을 무시한다. CSS 클래스: `quro-merge-repeat`(글자 숨김), `quro-merge-continues`(아래 선 없음). **스크롤로 묶음의 첫 행이 사라지면 맨 위 셀이 비어 보인다**(클릭하면 값이 보임). 값을 편집하면 합침 상태가 바뀐 이웃 행만 다시 그린다. TOON 키는 v0.3
- 잘린 글자 툴팁 (GS-111, A-79, 0.5.0-alpha.10부터): 항상 켜져 있고 파라미터가 없다. 포인터가 글자가 잘린 본문·합계·소계·열 헤더 셀에 있는 동안 `quro-grid.js`가 셀에 `title`(보이는 글자 전체)을 넣고 벗어나면 뺀다. 브라우저 기본 툴팁이므로 지연 시간과 모양은 브라우저가 정한다. 서버 호출은 없다. 오류·경고 표시(`SetCellMark`)가 있는 셀은 그 메시지를 유지한다. 에디터가 열린 셀, `Check`·`Button`·`Image` 셀, 행 번호·행 상태 열에는 없다. `MergeRepeated`로 글자가 숨겨진 셀은 잘리지 않아도 값을 보여 준다. 포인터를 그대로 둔 채 값이나 열 폭이 바뀌면 포인터가 다른 셀로 갔다 올 때 다시 계산한다
- 크기·스크롤 버튼 (GS-101, A-73, 0.5.0-alpha.9부터): 그리드 파라미터 `Width`(CSS 값, `Height`와 같음. 없으면 열에 맞추고 부모 폭까지)와 `ScrollButtons`(기본 false). 켜면 스크롤할 곳이 남은 방향에만 ▲▼◀▶ 버튼(`.quro-scroll-button`, `data-scroll`=`up`·`down`·`left`·`right`, 보일 때 `data-visible`)이 나타나고, 기본 스크롤바는 그대로 있다. 한 번 누르면 한 행 또는 한 열(고정 열 뒤 기준), 누르고 있으면 반복. 서버 호출 없음, 포커스 받지 않음, 열린 에디터 유지. 키보드 조작 대상 아님(키보드는 셀 이동으로 스크롤). 그리드 루트는 항상 `.quro-grid-frame` 안에 있다(버튼이 없으면 `display: contents`). 켜면 `Width`·`Height`는 프레임에 간다. `.quro-grid`는 이제 `box-sizing: border-box`라 `Height`가 테두리를 포함한다(2px 차이). 리소스 키 `quro.scroll.up`·`down`·`left`·`right`(`GridKeys.ScrollUp` 등, 소비자 `IStringLocalizer`에 둔다). TOON 키는 v0.3에서
- CSV 올리기 (G-05, A-47): `Task<ImportResult> ImportRows(IReadOnlyList<string> fields, IReadOnlyList<string[]> rows)`. 앱이 파일을 읽고 헤더를 필드 이름에 맞춰 부른다. 행은 끝에 추가, 셀은 붙여넣기와 같이 그리드 `Culture`로 읽고 이벤트 발생, 건너뛴 셀은 `ImportResult.Skipped`(`PasteSkip`, `PasteKeys`). Undo 한 단계. `Clipboard`와 무관, `OnPasting`/`OnPasted` 없음. 모르는 필드·Button 열은 `ArgumentException`
- 정의 열의 데이터 (A-46): `LookupFor` (`Func<string, string, Task<IReadOnlyList<string>>>`: lookup id, 입력 문자열 → 일치 코드. `Lookup`과 같은 규칙), `OptionsFor` (`Func<string, IReadOnlyList<ComboOption>?>`: select 열의 source(예 `code:S910`) → 목록). 정의나 이 파라미터가 바뀔 때만 다시 묻는다. 목록을 다시 읽으려면 새 함수를 넘긴다
- 컬럼 옵션: `NewRowOnly` (추가 행에서만 편집, G-01), `Filterable`·`Sortable`·`Groupable` (기본 true, G-04)
- 계산 열·조건부 스타일 (A-59, GS-092·GS-093, 0.5.0-alpha.3부터): 그리드는 식을 평가하지 않고 앱 함수만 부른다.
  - `Compute` (`Func<object, object?>`, 인자 = 행 항목 `TItem`): 표시 전용 값. 열은 항상 읽기 전용(편집·붙여넣기·저장 없음, 붙여넣기는 `PasteKeys.ReadOnly`로 건너뜀), `Field`는 식별자일 뿐 DataSet 필드가 아니어도 된다. 표시는 열 `Type`·`Format`. 헤더 필터·정렬·그룹과 내보내기 요청에서 빠진다. 합계 행 `Sum`은 계산 값을 더한다(서버 페이징 중에는 비움). 행이 바뀌어 다시 그려질 때 다시 계산(서버 호출 늘지 않음). 저장할 계산(금액)은 지금처럼 `OnCellValueChanged`
  - `CellStyle` (`Func<object, object?, CellTone>`, 인자 = 행 항목, 셀 값 — `Compute` 열이면 계산 값): `[Flags] enum CellTone { None, Danger, Success, Muted, Highlight, Bold }`. 셀 클래스 `quro-tone-danger`·`-success`·`-muted`·`-highlight`·`-bold`. 색 토큰 `--quro-tone-danger`(기본 `--quro-error-line`)·`--quro-tone-success`·`--quro-tone-muted`(기본 `--quro-check-line`)·`--quro-tone-highlight`. 오류·경고 표시(`SetCellMark`) 배경이 `Highlight`보다 우선. 임의 CSS는 받지 않는다
- Mask 셀 (A-56, GS-095, 0.5.0-alpha.7부터): `Type="CellType.Mask"` + `Mask` 패턴(QuroMaskBox 4.8과 같은 문법: `0` 숫자, `A` 모든 언어의 글자, `*` 글자나 숫자, `\` 다음 문자 고정, 그 밖은 고정 문자). **문자열** 열만. 저장 값은 고정 문자를 뺀 입력 문자만(`123-45-67890` → `1234567890`), 빈 입력은 null. 셀·에디터·헤더 필터·그룹 머리는 고정 문자와 함께 보이고(맞지 않는 저장 값은 그대로), 필터는 보이는 글자와 저장 값 모두로 찾는다. 확정·붙여넣기는 고정 문자가 있든 없든 마스크 끝까지 맞아야 하며, 덜 채우거나 맞지 않으면 확정하지 않고 편집을 유지(Number와 같음, QuroMaskBox는 마지막 값으로 되돌림), 붙여넣기는 `PasteKeys.Invalid`로 건너뜀. 입력 중 고정 문자 넣기·맞지 않는 문자 버리기는 quro-grid.js(R2, 에디터 `data-mask`). 셀 `data-type="mask"`. `Mask` 없이 `CellType.Mask`, 다른 타입에 `Mask`, 입력 자리 없는 마스크, 문자열이 아닌 열은 `ArgumentException`. 마스크 해석은 `Quro.Grid.Data`의 `QuroMask`(`Parse`·`TryRead`·`Format`·`Length`)로 QuroMaskBox와 같다
- Label 셀 (A-56, GS-097, 0.5.0-alpha.7부터): `Type="CellType.Label"` = 정적 텍스트. `ReadOnly` Text 셀과 같이 동작한다(포커스·범위 선택·Ctrl+C·헤더 필터·정렬·그룹, `Format` 적용, 편집·붙여넣기 없음 — 붙여넣기는 `PasteKeys.ReadOnly`, Tab은 건너뜀). 다른 점은 모양뿐: 셀 `data-type="label"`, 글자색 토큰 `--quro-label`(QuroLabel과 같은 토큰, 기본 `#5b6475`). `CellStyle` 톤 색이 있으면 톤이 이긴다. 값은 앱이 `SetValue`로 바꾼다
- Image 셀 (A-56, GS-096, 0.5.0-alpha.7부터): `Type="CellType.Image"`, **문자열** 열의 값 = 이미지 URL. 행 높이(`--quro-row-height`, 기본 28px)에서 6px 뺀 높이로 비율을 유지해 그린다(CSS 클래스 `quro-image`). 대체 텍스트는 선택 `TextField`(그리드 행의 필드, 없으면 빈 문자열 = 장식). 빈 값이면 아무것도 그리지 않는다. 편집·붙여넣기 없음(붙여넣기는 `PasteKeys.ReadOnly`, `aria-readonly`), Ctrl+C는 URL을 복사. 이미지 클릭·셀에서 Enter·Space → `OnCellClick`(`CellClickArgs<TItem>`: `RowKey`·`Field`·`Item`·`Value`), 서버 호출 1회, 삭제 표시 행에서는 발생하지 않음. **`OnCellClick`은 Image 셀만** 낸다(다른 셀 클릭은 서버를 부르지 않음). 셀 `data-type="image"`. 문자열이 아닌 열·없는 `TextField`는 `ArgumentException`
- 코드 DataSet Combo (A-57, GS-094, 0.5.0-alpha.6부터): `QuroColumn`의 `OptionsSource`(`IQuroDataSet`, 예: `QuroDataSet<CityCode>`)·`ValueField`(저장되는 코드)·`TextField`(표시). 목록 = 삭제 표시가 아닌 코드 행, 행 순서. 셀 글자·에디터 목록·입력·붙여넣기(코드나 글자, 대소문자 무시)·헤더 필터 값·내보내기 `Options`가 모두 이 목록을 쓴다. 코드 DataSet이 바뀌면(`Load`·`SetValue`·행 추가·삭제) 그리드가 스스로 다시 그린다. 연동 콤보: `FilterField`(코드 행의 부모 값 필드)와 `ParentField`(그리드 행의 부모 값 필드)를 함께 — 그 행의 부모 값과 `FilterField`가 같은 코드만, 부모 값이 null이면 빈 목록. 부모 값이 바뀌어(그리드 편집·붙여넣기·앱의 `SetValue`) 자식 코드가 목록에서 빠지면 자식 셀을 null로(`SetValue` 1회, 같은 Undo 단계, `RevertRow`로 되돌림, `ReadOnly` 열은 그대로). `Options`와 함께 쓰거나, `ValueField`·`TextField`가 없거나, 없는 필드거나, `FilterField`·`ParentField` 한쪽만이면 `ArgumentException`. `Options`·`OptionsFor`는 그대로. 다른 사용자 언어의 글자는 코드 DataSet을 다시 `Load`
- 검증 규칙 `FieldRule`: `Required`, `Min`·`Max` (+ `MinExclusive`·`MaxExclusive`), `NotEqual`, `MessageKey` (범위·ne 오류 키를 바꿈, 필수는 그대로). 기본 키 `quro.validation.required`·`min`·`max`·`minExclusive`·`maxExclusive`·`notEqual` (G-02)
- `QuroDataSet` 필드 이름은 정확히 같은 이름을 먼저(대소문자 구분), 없으면 대소문자를 무시하고 찾는다 (정의 id `qty` → 속성 `Qty`). 대소문자만 다른 속성이 둘 이상이면 정확한 이름만 (A-46)
- `QuroDataSet.AddBlankRow(template)`: 입력용 빈 행 (A-46). 추가할 때의 값 그대로인 동안은 `Validate`·`GetChanges`·`HasChanges`가 건너뛰고 `AcceptChanges` 뒤에도 추가 행으로 남는다. Undo 단계가 아니다. `initRows`가 이것을 쓴다
- `QuroDataSet` 현재 행·변경 알림 (A-51, GS-082~GS-084): `int? CurrentRowKey`, `MoveTo(rowKey)`(없는 키는 `KeyNotFoundException`), C# 이벤트 `CurrentRowChanged`(`OldRowKey`·`NewRowKey`), `ValueChanged`(`RowKey`·`Field`·`OldValue`·`NewValue`, 값이 실제로 바뀔 때만, `RevertRow`·`RejectChanges`·`Undo`·`Redo` 포함), `RowAdded`·`RowDeleted`(`RowKey`; 되돌리기로 들어오고 빠진 행 포함, 저장 후 Deleted 행 제거에서는 다시 내지 않음), `Loaded`, `Saved`(`Result`, `SaveAsync` 성공 후). `Load` 뒤 현재 행은 첫 행(없으면 null). 현재 행이 목록에서 빠지면 같은 위치의 다음 행 → 앞 행 → null. 페이징으로 놓인 행이면 행 키 유지. 이벤트는 호출한 스레드(회로)에서 동기로 발생한다. 그리드 활성 행과 양방향으로 맞춘다 (A-62, GS-085): 그리드에서 행을 옮기면 `MoveTo`, 앱이 `MoveTo`하면 그리드 활성 셀이 그 행의 같은 열로 (포커스·`OnSelectionChanged` 없음, 편집 중·화면에 없는 행이면 그대로)
- `IQuroDataSet` (A-57, 0.5.0-alpha.6부터): `QuroDataSet<T>`가 구현하는 제네릭 아닌 읽기 전용 인터페이스. `RowKeys`, `GetValue(rowKey, field)`, `GetState(rowKey)`, `GetColumn(field)`(없는 필드는 `ArgumentException`), 이벤트 `ValueChanged`·`RowAdded`·`RowDeleted`·`Loaded`. 행 형식을 모르는 컴포넌트(그리드 Combo 열)가 다른 DataSet을 읽을 때 쓴다
- 행 키 조회 (GS-103, A-54, 0.5.0-alpha.10부터): `QuroDataSet<T>.QueryRowKeys(Func<T, bool>? filter = null, Comparison<T>? sort = null, bool includeDeleted = false)`. 조건에 맞는 행 키를 새 목록으로 돌려준다. 읽기 전용: 행 순서·행 상태·현재 행·Undo가 그대로이고 이벤트가 없다. `sort`가 없으면 `RowKeys` 순서, 있으면 안정 정렬. 삭제 표시 행은 `includeDeleted: true`일 때만. 이 DataSet에 있는 행만 찾으므로 페이징 화면에서는 적재 행과 변경 행뿐이다. 그리드의 정렬·필터와는 별개이고 화면에 보이는 내용을 알려 주지 않는다. `IQuroDataSet`에는 없다
- REST 조회·비우기 (GS-107, A-75, 0.5.0-alpha.10부터): `Task<LoadResult> QuroDataSet<T>.LoadAsync(HttpClient http, string requestUri, CancellationToken cancellationToken = default)`. GET으로 최상위 JSON 행 배열을 받아 `Load`처럼 교체한다(웹 기본값: 이름 대소문자 무시). 감싼 객체 등 다른 형식은 앱이 직접 가져와 `Load`를 호출한다. URL은 앱이 넘긴다. `LoadResult(LoadStatus Status, HttpStatusCode? StatusCode, int RowCount)`, `LoadStatus`: `Loaded`, `Failed`(2xx 아님), `Invalid`(2xx지만 행 배열이 아님), `Superseded`(더 나중의 `LoadAsync`·`Load`·`Clear`가 먼저여서 버림). `Loaded` 말고는 행·변경분·현재 행·Undo가 그대로다. 네트워크 오류와 취소는 `SaveAsync`처럼 예외를 던진다. **성공하면 저장하지 않은 변경분을 경고 없이 버리므로 앱이 먼저 `HasChanges`를 확인한다.** 겹치는 호출은 가장 나중 호출이 이긴다(나중 호출이 실패해도 이전 응답은 버림). 페이징 그리드에서는 쓰지 않고 `ReloadAsync()`를 쓴다(A-24). 로딩 표시는 앱이 한다. `void Clear()`는 `Load([])`와 같다: 변경분을 포함해 모든 행을 없애고 현재 행·Undo를 비우며 `Loaded`를 발생시킨다. 둘 다 `IQuroDataSet`에는 없다. 그리드 표시는 `Load`와 같이 다음 렌더에 반영된다: 앱의 이벤트 처리기 안이면 자동, 그 밖이면 `Refresh()`
- DataSet 집계 (GS-108, A-76, 0.5.0-alpha.10부터): `decimal Sum(string field)`, `int Count(string field)`, `decimal? Average(string field)`, `object? Min(string field)`, `object? Max(string field)` (`QuroDataSet<T>`, `IQuroDataSet`에는 없음). 삭제 표시 행을 빼고 현재 값을 읽는다. 그리드 필터와 `Compute` 열은 모른다. `Count`는 null과 빈 글자를 세지 않는다. `Sum`·`Average`는 숫자만, `Min`·`Max`는 숫자 또는 날짜만 보고 나머지는 건너뛴다(예외 없음). `decimal`에 담을 수 없는 `float`·`double`(NaN, 무한대, 매우 큰 값)은 숫자로 보지 않는다. 값이 없으면 `Sum`·`Count`는 0, 나머지는 null. `Min`·`Max`는 필드의 원래 형식으로 돌려준다(`int`면 `int`). 없는 필드는 행이 없어도 `ArgumentException`. 읽기 전용이며 이벤트가 없다. 주의: 글자 필드에서 그리드의 `Aggregate.Sum`은 지금처럼 숫자로 변환한다(`"12"`는 12, 변환할 수 없으면 예외). DataSet `Sum`은 글자를 건너뛴다
- 트리 표시 (GS-104, A-61, 0.5.0-alpha.10부터): 그리드 파라미터 `TreeKeyField`(행 자신의 키 필드)·`TreeParentField`(부모 키 필드), 둘 다 주면 트리 모드. `TreeColumn`(들여쓰기와 토글을 그릴 열, 기본은 처음 보이는 열). 메서드 `bool ExpandTreeRow(int rowKey, bool expanded)`(자식이 없거나 표시되지 않은 행이면 false), `ExpandAllTree()`, `CollapseAllTree()`. 처음에는 모두 펼침이고, 펼침 상태는 키 값에 붙어 `Load` 후에도 유지된다. 부모가 비었거나 DataSet에 없거나 숨겨진 삭제 표시 행이거나 부모 순환에 든 행은 최상위로 보이며 예외가 없다. 형제 순서는 목록 순서이고 정렬은 형제 안에서만 적용된다. 필터는 맞는 행과 그 조상을 보여 주되 접힌 행은 접힌 채로 둔다. 합계는 필터를 통과한 모든 행을 더한다: 접힌 행은 포함, 조상만으로 보이는 행은 제외. 부모 행에 자식 합을 넣어 두면 이중으로 더해지므로 앱이 주의한다. `ShowRowNumbers`는 보이는 행을 센다. ▸ 클릭은 그 셀로 옮긴 뒤 펼침·접기, Ctrl+→ 펼침·Ctrl+← 접기는 행의 어느 셀에서나 (에디터가 커서 모드이면 입력칸이 받음). 각각 서버 호출 1회. 그냥 ←→는 셀 이동 그대로. 접어서 활성 행이 가려지면 보이는 행으로 옮긴다. 루트는 `role="treegrid"`, 행은 `aria-level`과 자식이 있을 때 `aria-expanded`. CSS 클래스 `quro-tree-cell`·`quro-tree-toggle`, CSS 변수 `--qg-tree-level`. `ItemsProvider`와 함께 쓰면 렌더 때 `InvalidOperationException`. 트리 모드에서 `SetGroupBy`는 아무것도 하지 않는다. `SetValue`·셀 편집·Undo로 부모 필드가 바뀌면 트리를 다시 만든다. TOON 키는 v0.3으로 미룬다
- 트리 편집 (GS-105, A-61, 0.5.0-alpha.10부터): `Task<int?> AddChildRow(int parentRowKey)`는 부모 필드를 채워 부모의 마지막 자손 뒤에 보이는 행을 추가하고 부모와 그 조상을 펼친다. 그리드가 트리가 아니거나, 부모를 모르거나, 삭제 표시 행이거나, 부모의 키 값이 아직 비어 있으면 행을 추가하지 않고 null. 새 행의 키 값은 앱이 채운다(`OnRowAdding` 또는 편집). 키가 빈 행은 자식을 가질 수 없다. Insert와 `AddRow(afterRowKey)`는 그 행의 형제(부모 값 복사, 그 행의 자식들 아래에 보임), Ctrl+Insert는 자식. 위치 없는 `AddRow()`와 `AddRowOnLastEnter`는 최상위. 부모 값은 `OnRowAdded` 전에 채워지고 `OnRowAdding`에서는 아직 없다. `CopyRows`는 부모를 비워 최상위에 넣는다(값 복사이므로 키 필드도 복사된다: 중복 키면 첫 행이 자식을 가지므로 `OnRowAdding`에서 앱이 새 키를 준다). `ImportRows`는 최상위(가져오는 열에 부모 필드가 있으면 그 값을 따른다). 붙여넣기는 보이는 행만 채우고 새 행을 만들지 않으며 남는 행은 버린다(`AddRowsOnPaste` 무시). 그리드의 행 삭제(Ctrl+Delete, `DeleteRows`, `DeleteSelectedRows`)는 선택 행의 자손까지 접힌 행·필터로 숨은 행을 포함해 Undo 한 단계로 삭제한다. `OnRowDeleting`은 행마다 부모부터 발생하고, 취소하면 그 뒤 행은 묻지 않고 그 선택 하위 전체를 남긴다. 경고는 앱이 `OnRowDeleting`에서 띄운다. `DataSet.DeleteRow`를 직접 부르면 그 행만 삭제되고 자식은 최상위로 보인다
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

- 소계는 집계(`Aggregate`)가 있는 컬럼마다 그 집계로 계산한다 (A-76, 집계 열이 하나도 없으면 소계 행 없음). 그룹 값을 편집해도 행은 그 그룹에 남는다 (다시 `SetGroupBy`하면 새 값으로).
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
