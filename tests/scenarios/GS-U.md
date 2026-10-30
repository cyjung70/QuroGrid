# QuroUI 시나리오 (GS-Uxx)

- 근거: `docs/QuroGrid_Control_Spec_Draft_v0.1.md` 8~11장, Addendum A-53·A-63
- 패키지: `Quro.UI` (RCL, `Quro.UI` → `Quro.Grid.Data`). 자체 input + MudBlazor CSS 변수 (A-63, Q-21 잠정안과 다름)
- 테스트 이름은 시나리오 ID로 시작한다: `GS_U01_...`

## 1. 텍스트 박스 `QuroTextBox<TItem>` (Control Spec 8장, A-63)

| ID | 시나리오 | 기대 결과 | 계층 | 근거 |
|---|---|---|---|---|
| GS-U01 | DataSet 열 바인딩 | DataSet 현재 행의 `Field` 값을 보인다. `MoveTo`·`Load`·다른 곳의 `SetValue`·`RevertRow` 뒤 새 값. 입력 확정(change: 포커스 이탈·Enter) 때 `SetValue` 1회 → 행 `Updated`. 현재 행이 없으면 빈 칸·비활성. 없는 필드·문자열이 아닌 열은 `ArgumentException`. 컨트롤을 없애면 DataSet 이벤트 구독을 푼다 | bUnit | 8장, A-51 |
| GS-U02 | 필수·읽기 전용 표시 | 열 메타 `Required`면 `quro-ui-required`(연노랑 배경 `--quro-required-bg`, 빨간 별 없음). `ReadOnly` 또는 현재 행이 `Deleted`면 `readonly` + `quro-ui-readonly`(회색 `--quro-readonly-bg`), 이때 필수 표시는 없음. 저장 막기는 `Validate`(열 `Required`, GS-089) | bUnit | 8장, A-52·A-63, Q-22 |
| GS-U03 | 입력 속성 | `maxlength` = `MaxLength` 파라미터, 없으면 열 메타 `MaxLength`. `Password`면 `type=password`. 자리 표시 문구 = `Localizer[PlaceholderKey]`(Localizer 없으면 키). `CharType`·`AutoSelect`는 `data-quro-chars`·`data-quro-autoselect` 속성으로 브라우저에 넘기고, `quro-ui.js` `attach`는 첫 렌더에 1회 | bUnit | 8장, A-63 |
| GS-U04 | 브라우저 입력 | `Digits` 박스에 `12a3b` 입력·붙여넣기 → `123`. `Alpha`는 글자만, `AlphaNumeric`은 글자·숫자. 입력하는 동안 서버 호출 없음(R2), 포커스 이탈 때 1회. `AutoSelect`면 포커스(Tab·클릭) 때 전체 선택. `MaxLength`보다 길게 입력되지 않음 | E2E | 8장, R2 |

- `CharType.Alpha`는 모든 언어의 글자(유니코드 문자)다. 한글 IME 조합 중에는 거르지 않고 조합이 끝날 때 거른다.
- `MaxLength`는 `Validate`가 검사하지 않는다 (입력 제한은 브라우저).

## 2. 콤보 박스 `QuroComboBox<TItem, TCode>` (Control Spec 9장, A-57·A-64)

| ID | 시나리오 | 기대 결과 | 계층 | 근거 |
|---|---|---|---|---|
| GS-U05 | 코드 DataSet 목록·바인딩 | 목록 = `OptionsSource`의 행(삭제 표시 행 제외, 행 순서), 값 `ValueField`·글자 `TextField`. 입력 칸은 현재 행 값의 글자, 목록에 없는 값은 값 그대로. 선택 확정(숨은 `change`, 보인 목록의 순번) 때 `SetValue` 1회. 같은 값이면 호출 없음. `MoveTo`·코드 DataSet `Load` 뒤 새 값·새 목록. 필수·읽기 전용은 텍스트 박스와 같다(`quro-ui-required`·`quro-ui-readonly`, 읽기 전용이면 선택 무시). 없는 필드, `Field`와 `ValueField` 형식이 다르면 `ArgumentException` | bUnit | 9장, A-57 |
| GS-U06 | 빈 선택·코드 표시·여러 열 | `EmptyKey`면 맨 위에 `Localizer[EmptyKey]` 항목, 고르면 null. `ShowCode`면 "01 Seoul". `ListColumns`면 펼친 목록에 머리글(`HeaderKey`)과 열마다 칸 | bUnit | 9장 |
| GS-U07 | 연동 콤보 | 목록 = `OptionsSource[FilterField]`가 바인딩 행 `ParentField` 값과 같은 코드. 부모 값이 null이면 빈 목록. 부모 값이 바뀌어 현재 값이 목록에 없으면 null로 지움(`SetValue` 1회, Undo 가능). 행 이동·Load로는 지우지 않음 | bUnit | 9장, A-57, A-64 ③ |
| GS-U08 | 다중 선택 | `Multiple`이면 항목에 체크 표시, 고른 코드를 `Separator`(기본 `\|`)로 이어 코드 목록 순서로 문자열 열에 저장, 아무것도 없으면 null. 입력 칸은 글자를 `, `로 이어 보인다. 부모가 바뀌면 목록에 남은 코드만 남김. 문자열 열이 아니면 `ArgumentException` | bUnit | 9장, Q-23, A-64 ④ |
| GS-U09 | 브라우저 동작 | 입력으로 목록을 거름(코드·글자 포함 검색, 대소문자 무시) — 서버 호출 없음(R2). ↓↑로 이동, Enter로 선택, Esc로 닫고 원래 글자. 마우스로 선택. 맞는 항목 없이 포커스를 잃으면 마지막 선택 글자로 돌아감. 다중 선택은 Space·클릭으로 체크 | E2E | 9장, R2, A-64 ⑤ |

- 목록 전체를 한 번 렌더한다. 입력 중 서버에서 목록을 불러오지 않는다 (9장).
- 다중 선택 입력 칸은 읽기 전용(거르기 없음)이다. 거르기는 단일 선택만.
