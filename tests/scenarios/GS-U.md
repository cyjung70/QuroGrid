# QuroUI 시나리오 (GS-Uxx)

- 근거: `docs/QuroGrid_Control_Spec_Draft_v0.1.md` 8~11장, Addendum A-53·A-63
- 패키지: `Quro.UI` (RCL, `Quro.UI` → `Quro.Grid.Data`). MudBlazor를 감싸 구현 (Q-21 잠정안)
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
