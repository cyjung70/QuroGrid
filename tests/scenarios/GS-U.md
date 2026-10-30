# QuroUI 시나리오 (GS-Uxx)

- 근거: `docs/QuroGrid_Control_Spec_Draft_v0.1.md` 8~11장, Addendum A-53·A-63~A-66·A-69
- 패키지: `Quro.UI` (RCL, `Quro.UI` → `Quro.Grid.Data`). 자체 input + `--quro-*` CSS 변수 (A-63, Q-21 잠정안과 다름. MudBlazor 대체값은 A-67로 제거)
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

## 3. 날짜 선택기 `QuroDatePicker<TItem>` (Control Spec 10장, A-65)

| ID | 시나리오 | 기대 결과 | 계층 | 근거 |
|---|---|---|---|---|
| GS-U10 | 날짜 바인딩·표시·입력 | `DateOnly`·`DateTime`(nullable 포함) 열. 현재 행 값을 `Culture`(기본 현재 culture)의 짧은 날짜(`d`)로 보인다. 입력 확정 때 `SetValue` 1회: culture 짧은 날짜, `yyyy-MM-dd`, 숫자 8자리(culture 순서: ko `yyyyMMdd`, en-US `MMddyyyy`). 같은 값이면 호출 없음. 읽을 수 없는 입력은 마지막 값으로 돌아감. 빈 입력은 nullable이면 null, 아니면 돌아감. 그 밖의 열 형식은 `ArgumentException`. 필수·읽기 전용은 텍스트 박스와 같다 | bUnit | 10장, Q-24 ③, A-65 ③ |
| GS-U11 | 월 모드 | `Month`면 문자열 열에 `yyyy-MM` 저장. 표시는 culture 짧은 날짜에서 일을 뺀 형식(ko `2026-10`, en-US `10/2026`). 입력: 그 형식, `yyyy-MM`, 숫자 6자리(culture 순서). 문자열이 아닌 열은 `ArgumentException` | bUnit | 10장, Q-24 ② |
| GS-U12 | 기간 | `ToField`면 입력 칸 2개(시작 `Field`, 끝 `ToField`, 같은 종류 열). 끝이 시작보다 앞이 되는 변경은 거부하고 그 칸은 마지막 값으로 돌아감 | bUnit | 10장, Q-24 ①, A-65 ④ |
| GS-U13 | 브라우저 달력 | 칸을 누르면 달력(culture 언어의 월·요일 이름, culture 주 시작 요일). 이전·다음 달 이동은 서버 호출 없음(R2). 날짜를 누르면 값 1회 설정·닫힘. 월 모드는 12달 표. Esc로 닫힘 | E2E | 10장, R2 |

- 달력은 `quro-ui.js`가 `Intl.DateTimeFormat`으로 그린다. 고른 날짜는 `yyyy-MM-dd`(월 모드 `yyyy-MM`)로 서버에 보내고 서버가 culture 형식으로 다시 보인다.

## 4. 라벨·정적 텍스트 `QuroLabel`·`QuroStaticText<TItem>` (Control Spec 11장, A-66)

| ID | 시나리오 | 기대 결과 | 계층 | 근거 |
|---|---|---|---|---|
| GS-U14 | 다국어 라벨 | `QuroLabel`은 `<label class="quro-ui-label">`에 `Localizer[TextKey]`. 해석기가 없거나 번역이 없으면 키 그대로 | bUnit | 11장, A-66 ① |
| GS-U15 | 열 값 표시 | `QuroStaticText`는 DataSet 현재 행의 `Field` 값을 `<span class="quro-ui-static">`에 보인다. `MoveTo`·`Load`·`SetValue` 뒤 새 값. 값 null·현재 행 없음 → 빈 칸. `Format`이 있으면 `Culture`(기본 현재 culture)로 적용. 없는 필드는 `ArgumentException`. 숫자 열이면 `quro-ui-static-number`(오른쪽 정렬). JS 없음 | bUnit | 11장, A-66 ①③ |
| GS-U16 | 기본 형식 | `Format`이 없으면: 정수는 천 단위 구분, 실수는 천 단위 구분 + 열 메타 `Scale` 자릿수(없으면 값의 자릿수), `DateOnly`·`DateTime`은 culture 짧은 날짜, 그 밖은 culture `ToString()` | bUnit | 11장, A-66 ② |

## 5. 마스크 박스 `QuroMaskBox<TItem>` (Control Spec 11장, A-69)

| ID | 시나리오 | 기대 결과 | 계층 | 근거 |
|---|---|---|---|---|
| GS-U17 | DataSet 열 바인딩·확정 | 저장된 입력 문자를 고정 문자와 함께 보인다(`1234567890` → `123-45-67890`). 행 이동·`SetValue` 뒤 새 값. 마스크 끝까지 채운 입력(고정 문자 있든 없든) 확정 → 입력 문자만 `SetValue` 1회. 빈 칸 → null. 덜 채움·자리에 맞지 않음·더 김 → 변경 없이 마지막 값. 문자열이 아닌 열·없는 필드·입력 자리 없는 마스크는 `ArgumentException` | bUnit | 11장, A-69 ①③④ |
| GS-U18 | 마스크 문자·속성 | `0` 숫자, `A` 모든 언어의 글자, `*` 글자나 숫자, `\` 다음 문자 고정. 자리에 맞지 않는 문자는 거부. 맞지 않는 저장 값은 그대로 표시. `maxlength` = 마스크 길이, `data-quro-mask`. 필수·읽기 전용·자리 표시·`AutoSelect`는 텍스트 박스와 같다. 읽기 전용이면 변경 무시 | bUnit | 11장, A-69 ①② |
| GS-U19 | 브라우저 입력 | 입력 중 고정 문자를 넣고 맞지 않는 문자는 버림(`010x1234` → `010-1234`), 마스크 끝에서 멈춤. 고정 문자 없이 붙여넣기 → 형식을 갖춤. 가운데 입력 뒤 커서는 같은 입력 문자 뒤. 입력 중 서버 호출 없음(R2), 포커스 이탈·Enter 때 1회. 덜 채우고 떠나면 마지막 값 | E2E | 11장, R2, A-69 |
| GS-U20 | 정적 텍스트 마스크 | `QuroStaticText.Mask`면 저장 값을 고정 문자와 함께 보인다. 맞지 않는 값은 그대로 | bUnit | 11장, A-69 ⑤ |
