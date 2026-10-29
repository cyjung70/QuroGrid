# QuroGrid 코드도움 정의 `*.lookup.toon` — v0.3 제안 (T-08)

- 상태: **제안 (승인 대기)**. 승인되면 설계서 `QuroGrid_TOON_ScreenDef_v0.2.md`의 T-08을 이 문서로 닫고, 구현은 묶음 L-1~L-3으로 한다
- 날짜: 2026-10-29
- 관련: 설계서 v0.2 2장(경계)·5.4절(데이터 출처)·4.4절 `rules action=lookup`·6.2절 T-04·T-08, Addendum A-44·A-46, Harness `tests/scenarios/GS-D.md`

---

## 1. 왜 필요한가

v0.2는 코드도움(lookup)을 **id 존재만** 검사한다 (V-22, 카탈로그 `LookupIds`). 그래서 다음 오류가 런타임까지 남는다.

| # | 지금 잡지 못하는 오류 | 예 (골든 화면) |
|---|---|---|
| 1 | 규칙이 부르는 조회가 그 코드도움에 없다 | `item.byCustomer(customer)`인데 `item`에 `byCustomer` 조회가 없음 |
| 2 | 조회 인수 개수가 다르다 | `byCustomer`는 인수 1개인데 `item.byCustomer()` |
| 3 | 채울 대상 열이 코드도움 결과에 없다 | `target = itemName\|unitPrice\|stdPrice`인데 결과에 `stdPrice`가 없음 |
| 4 | 팝업 목록의 열·제목이 화면마다 다르게 하드코딩된다 | 같은 품목 팝업을 화면마다 따로 만듦 |

`lookup.toon`은 코드도움 하나의 **모양**(제목, 결과 열, 조회와 인수, 코드·이름 열)을 한 번 적어 두는 파일이다. 데이터 조회 자체는 여전히 앱(QuroERP `ILookupProvider`)이 한다 (A-44: 그리드는 데이터를 조회하지 않는다).

## 2. 경계 (v0.2 2장과 같은 원칙)

| 항목 | 소유 | QuroGrid가 하는 일 |
|---|---|---|
| `*.lookup.toon` 파일 | QuroERP | 없음 (테스트용 골든만) |
| 파서·검증기·불변 모델 `LookupDef` | **QuroGrid** (`Quro.Grid.Definition`) | 로드, 구조 검사, 화면과의 교차 검사 |
| `api` 해석, 실제 조회, 권한 판정 | QuroERP | 없음 (불투명 문자열) |
| 팝업 화면 (MudDialog 등) | QuroERP | 없음. 그리드는 `OnPopupRequested`·`LookupFor`만 (A-16·A-46) |
| CI 검사 | QuroGrid `quro-deflint` | 폴더의 `*.lookup.toon`도 로드하고 교차 검사 |

## 3. 파일 단위

**제안: 코드도움 1개 = 파일 1개, 이름 `{id}.lookup.toon`** (예 `item.lookup.toon`). 화면 정의의 P1(One Screen, One File)과 같은 원칙이며, 리뷰·충돌·검사 오류 위치가 파일 하나로 모인다. id는 카탈로그 `LookupIds`와 같은 전역 이름공간이고 camelCase다.

대안: 모든 코드도움을 `lookup.toon` 하나에 (v0.2 T-04 문구). 파일 수는 줄지만 한 파일이 커지고 여러 사람이 같은 파일을 고친다.

## 4. 스키마 — `*.lookup.toon` v0.3

표기는 v0.2 3장의 strict TOON 서브셋 그대로 (`[N]` 필수, 줄 맨 앞 `#` 주석, 따옴표 규칙).

```toon
# QuroERP lookup definition (TOON v3 subset)
lookup:
  id: item
  title: mm.item.lookup.title
  version: 1
  api: /api/mm/items/lookup
  key: itemCd
  text: itemName
  minChars: 1
params[1]{id,type,req}:
  customer,text,false
queries[2]{id,params}:
  all,""
  byCustomer,customer
columns[4]{id,label,type,width,fmt}:
  itemCd,mm.item.itemCd,text,120,""
  itemName,mm.item.itemName,text,250,""
  unitPrice,mm.item.unitPrice,money,90,"#,##0.00"
  stdPrice,mm.item.stdPrice,money,90,"#,##0.00"
```

### 4.1 `lookup`

| 키 | 필수 | 형식 | 설명 |
|---|---|---|---|
| `id` | 예 | camelCase | 파일명 `{id}.lookup.toon`과 같아야 함 |
| `title` | 예 | 리소스 키 | 팝업 제목 |
| `version` | 예 | 정수 ≥ 1 | 이 정의의 버전 |
| `api` | 예 | `/`로 시작하는 경로 | 불투명. 앱이 조회에 씀 |
| `key` | 예 | `columns`의 id | 고른 행에서 셀에 넣을 코드 (Popup 셀의 값) |
| `text` | 아니오 | `columns`의 id | 코드 옆에 보일 이름 |
| `minChars` | 아니오 (1) | 정수 ≥ 0 | Tab 자동 조회(A-05)를 시작할 최소 글자 수. 0 = 빈 입력도 조회 |
| `perm` | 아니오 | 권한 id | 이 코드도움을 쓸 권한 (판정은 앱) |

### 4.2 `params[N]{id,type,req}`
조회 인수. `type`: `text` \| `number` \| `date`. `req`: 불. 인수 값은 화면 규칙이 넘긴다 (`item.byCustomer(customer)` → `customer` 폼 필드 값).

### 4.3 `queries[N]{id,params}`
이름 있는 조회. `params`는 `|`로 이은 `params` id 목록(순서 = 호출 인수 순서), 없으면 `""`. 화면 규칙 `expr`의 `{lookupId}.{query}(인수…)`가 여기를 가리킨다. 규칙 없이 열 `lookup`만 있으면 **첫 조회**를 쓴다.

### 4.4 `columns[N]{id,label,type,width,fmt}`
팝업 목록의 열이자 **조회 결과 필드**. `type`은 화면 열과 같은 값(`text`·`number`·`money`·`date`·`checkbox`), `label`은 리소스 키. 화면 규칙 `action=lookup`의 `target` 열은 **같은 id의 결과 열**에서 값을 받는다 (5장).

## 5. 화면과의 연결 — 대상 매핑

**제안: 같은 id끼리 매핑.** `rules: itemCd.change,lookup,"itemName|unitPrice|stdPrice","item.byCustomer(customer)"` → 결과 열 `itemName`·`unitPrice`·`stdPrice`가 같은 id의 그리드 열로. 코드 열(`itemCd`)은 셀 자신이므로 `key`로 채운다.

대안: 명시 매핑 `target` = `"name=itemName|price=unitPrice"` (`화면열=결과열`). 이름이 다를 때 유연하지만 식 문법이 늘고, 대부분은 같은 이름이다. 필요하면 v0.4에서 비파괴로 더할 수 있다 (`=`가 있을 때만 명시 매핑).

## 6. 검사 (오류 코드 추가)

`LookupDefLoader.Load(path, text)` → `DefinitionResult<LookupDef>` (화면과 같은 형식, `ToString()` = `파일:줄: V-xx 메시지`).

| 코드 | 위치 | 내용 |
|---|---|---|
| V-01~V-03 | lookup 로드 | 화면과 같음 (개수, 키, 값 형식) |
| V-33 | lookup 로드 | `id`가 파일명과 다름 / `key`·`text`가 `columns`에 없음 / `queries.params`가 `params`에 없음 / id 중복 |
| V-34 | lookup 카탈로그 | `title`·`label`이 리소스 키가 아니거나 카탈로그에 없음 (V-21과 같은 규칙), `perm`이 카탈로그에 없음 |
| V-35 | 화면 교차 | 규칙 `expr`의 조회가 그 lookup의 `queries`에 없음, 또는 인수 개수가 다름 |
| V-36 | 화면 교차 | 규칙 `action=lookup`의 `target` 열이 lookup `columns`에 없음 |
| V-37 | 화면 교차 | 화면이 참조하는 lookup id(열 `lookup`, `lookup:`, 규칙)의 정의 파일이 없음 (정의 집합을 넘긴 경우만) |

SQL(V-30)·Clean-room(V-31)·색(V-32) 검사는 lookup 파일에도 그대로 적용한다.

## 7. 공개 API (모두 비파괴 추가, `Quro.Grid.Definition`)

```csharp
public static class LookupDefLoader
{
    public static DefinitionResult<LookupDef> Load(string path, string text);
}
public sealed record LookupDef(string Id, string Title, int Version, string Api, string Key, string? Text, int MinChars,
    string? Perm, IReadOnlyList<LookupParamDef> Params, IReadOnlyList<LookupQueryDef> Queries,
    IReadOnlyList<LookupColumnDef> Columns, SourceInfo Source);
public sealed record LookupParamDef(string Id, LookupParamType Type, bool Req, SourceInfo Source);
public sealed record LookupQueryDef(string Id, IReadOnlyList<string> Params, SourceInfo Source);
public sealed record LookupColumnDef(string Id, string Label, ColumnType Type, int Width, string? Fmt, SourceInfo Source);
public enum LookupParamType { Text, Number, Date }

public sealed partial class ScreenDefValidator
{
    // 기존 생성자는 그대로. 정의 집합을 넘기면 V-35~V-37 교차 검사를 더한다
    public ScreenDefValidator(IQuroDefinitionCatalog catalog, IReadOnlyDictionary<string, LookupDef> lookups);
    public IReadOnlyList<DefinitionError> Validate(LookupDef def);   // V-34·V-30~V-32
}
```

- `IQuroDefinitionCatalog`는 바꾸지 않는다 (멤버를 더하면 구현한 소비자가 깨진다).
- 그리드 쪽(`Quro.Grid`)은 이번 범위에서 바꾸지 않는다. 앱은 `LookupDef`로 팝업 제목·열을 그리고, `LookupFor`(A-46)에서 `api`·`queries`로 조회한다. `minChars`를 그리드가 직접 쓰게 할지는 L-3 이후 별도 결정.

## 8. `quro-deflint`

폴더 아래 `*.lookup.toon`도 로드한다. 정의가 하나라도 있으면 화면 검사에 넘겨 V-35~V-37 교차 검사를 한다. 출력 형식·종료 코드는 그대로 (GS-D16).

## 9. 구현 묶음 (승인 후)

| 묶음 | 내용 | 시나리오 (`GS-D.md` 3.5절로 추가) |
|---|---|---|
| L-1 | `LookupDefLoader`·모델, V-01~V-03·V-33 | GS-D18 골든 `item.lookup.toon` 로드, GS-D19 구조 오류 |
| L-2 | `Validate(LookupDef)` V-34, 교차 검사 V-35~V-37 | GS-D20 카탈로그, GS-D21 골든 화면 × 골든 lookup 교차 |
| L-3 | `quro-deflint`가 lookup 파일도 검사, 배포 `0.3.0-alpha.1` | GS-D16 확장 |

각 묶음은 브랜치·PR 1개, 테스트 먼저. 골든 lookup 파일은 공개 저장소용 중립 이름만 쓴다 (`item`, `customer`, `salesOrder`, `discount`).

## 10. 결정해 주실 것

1. 파일 단위: 코드도움 1개 = `{id}.lookup.toon` 1개 (3장)
2. 스키마: 4장의 `lookup`·`params`·`queries`·`columns`
3. 대상 매핑: 같은 id끼리 (5장). 명시 매핑은 필요할 때 v0.4에서
4. 오류 코드 V-33~V-37과 공개 API (6·7장), `IQuroDefinitionCatalog`는 그대로
5. 범위: 파서·검증기·`quro-deflint`까지. 그리드 컴포넌트는 바꾸지 않음. 묶음 L-1~L-3, 끝나면 `0.3.0-alpha.1`
