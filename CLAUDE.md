# CLAUDE.md — QuroGrid

QuroERP Community용 공통 그리드 컴포넌트 **QuroGrid** 저장소입니다.
기준 문서: `docs/QuroGrid_FeatureMatrix_Spec_v1.pdf` (Spec v1 초안, 2026-10-28). 이 파일과 Spec이 충돌하면 **Spec이 우선**이며, 충돌 사실을 보고합니다.

---

## 1. 확정 결정사항 (변경 금지)

| ID | 항목 | 결정 |
|---|---|---|
| D-01 | 제작 방식 | 자체 제작. **명세 → Harness(테스트) → 구현** 3단계. Clean-room |
| D-02 | 렌더 모드 | **Blazor Server** (SignalR 회로) |
| D-03 | 라이선스 | **LGPL v3** |

기술 스택: .NET / Blazor Server + ASP.NET Core + REST API + Dapper + PostgreSQL, UI 보조로 MudBlazor.

---

## 2. 절대 규칙 (Clean-room · 라이선스)

참고 그리드에서는 **동작(무엇을 하는가)만** 가져오고, **코드(어떻게 구현했는가)는 가져오지 않는다.**

- **금지:** dhtmlxGrid 소스 코드의 복사, 포팅, REA 변환 결과의 반입 (GPL 계열 → LGPL v3와 비호환)
- **금지:** FarPoint Spread 바이너리 디컴파일·역공학 (상용 EULA)
- **금지:** AG Grid **Enterprise** 기능 코드 참고·복사 (Community는 MIT, 개념 참고만 허용)
- **금지:** Nexacro 런타임·라이브러리 코드 분석 (Dataset **개념**만 차용)
- 참고 그리드의 원본 소스를 이 저장소에 두지 않는다.
- 새 의존성을 추가하기 전에 라이선스가 LGPL v3와 호환되는지 확인하고, PR 설명에 라이선스를 적는다.
  - 허용 예: MudBlazor(MIT), Dapper(Apache 2.0), Npgsql(PostgreSQL), ClosedXML(MIT), xUnit·bUnit·Playwright
  - GPL 계열 또는 상용 라이브러리는 추가하지 않는다.

이 규칙에 어긋날 수 있는 요청을 받으면 작업을 멈추고 이유를 보고한다.

---

## 3. 개발 방식 — Harness Engineering

1. **테스트 먼저.** 기능 구현 전에 해당 시나리오(GS-xxx)의 테스트를 작성하고, 실패하는 것을 확인한다.
2. 테스트를 통과시키는 최소 구현을 한다.
3. 기존 테스트를 깨뜨리는 변경은 하지 않는다. 테스트를 수정해야 한다면 이유를 보고한다.
4. 시나리오는 `tests/scenarios/`의 문서와 테스트 ID를 일치시킨다 (예: `GS_004_ExcelPaste_3x4`).

| 계층 | 도구 | 대상 |
|---|---|---|
| 단위 | xUnit | QuroDataSet (행 상태, 원본값, 변경분, 검증) |
| 컴포넌트 | bUnit | QuroGrid 렌더링, 포맷, 합계, 이벤트 |
| E2E | Playwright | 키보드, 범위 선택, 붙여넣기, 코드도움, 저장 흐름 |
| 성능 | Playwright + 측정 스크립트 | Spec 4.11 목표치, 회로당 메모리 |
| API 통합 | xUnit + PostgreSQL 컨테이너 | 변경분 저장 트랜잭션, 버전 충돌, Minus 재고 |

---

## 4. 아키텍처 — 4개 층

```
MudBlazor        그리드 바깥: 툴바, 다이얼로그, 코드도움 팝업, DatePicker/Select 에디터, 테마
   ▼
QuroGrid         Blazor 컴포넌트: 컬럼 정의, 헤더/본문/합계 렌더링, Virtualize, 이벤트
   ▲▼ JS Interop (편집 커밋 · 붙여넣기 · 포커스 동기화만)
quro-grid.js     얇은 JS 층: 키보드 이동, 포커스/선택 표시, 클립보드, 리사이즈, 고정 열 스크롤
   ▼
QuroDataSet      순수 C# (UI 의존 금지): 행 상태, 원본값, 검증, 변경분 → REST → Dapper → PostgreSQL
```

- **QuroDataSet은 Blazor·MudBlazor·JS를 참조하지 않는다.** UI 없이 단위 테스트가 가능해야 한다.
- **MudBlazor는 그리드 본체 렌더링에 쓰지 않는다.** (MudDataGrid 사용 금지 — 조회 전용 화면은 앱 쪽에서 별도 판단)

### 권장 폴더 구조 (초안)

```
src/
  Quro.Grid.Data/        QuroDataSet, RowState, ChangeSet, Validation   (UI 무관)
  Quro.Grid/             QuroGrid.razor, QuroColumn, 셀 타입, 이벤트
    wwwroot/quro-grid.js
  Quro.Grid.Sample/      Blazor Server 샘플 (구매오더 라인 화면)
tests/
  Quro.Grid.Data.Tests/  xUnit
  Quro.Grid.Tests/       bUnit
  Quro.Grid.E2E/         Playwright
  scenarios/             GS-xxx 시나리오 문서
docs/
  QuroGrid_FeatureMatrix_Spec_v1.pdf
```

---

## 5. Blazor Server 설계 원칙 (R1~R7) — 리뷰 필수 항목

| # | 원칙 | 구현 규칙 |
|---|---|---|
| R1 | 소유권 분리 | JS: 포커스·선택·스크롤·리사이즈 상태 / 서버: 데이터 값·행 상태·검증 |
| R2 | 커밋 시점에만 전송 | 편집 중 키 입력은 브라우저에서만 처리. 셀 이탈(Enter·Tab·클릭) 때 1회 전송 |
| R3 | 일괄 전송 | 붙여넣기·행 복사·일괄 삭제는 호출 1회 |
| R4 | 렌더 범위 최소화 | 셀마다 컴포넌트 생성 금지. 행 단위 `RenderFragment` + `@key` + `ShouldRender` |
| R5 | 이벤트 위임 | 셀마다 `@onclick` 금지. 그리드 단위로 받고 JS가 행·열 좌표 해석 |
| R6 | 서버 메모리 보호 | 대량 조회는 서버측 페이징(`Virtualize` ItemsProvider). DataSet에는 적재 범위 + 변경 행만 유지 |
| R7 | 연결 끊김 대비 | 재연결 중 입력 잠금 + 안내. 미저장 변경 경고 |

다음 코드는 리뷰에서 반려한다:
- 키 입력(`@onkeydown`, `@oninput`)마다 서버를 호출하는 코드
- 셀 단위 컴포넌트 또는 셀 단위 이벤트 핸들러
- 붙여넣기를 셀마다 JS Interop으로 보내는 코드
- 불필요한 `StateHasChanged()` 반복 호출

---

## 6. QuroDataSet 규칙

| 상태 | 규칙 |
|---|---|
| `Normal` | 값 변경 → `Updated` |
| `Inserted` | 값 변경해도 `Inserted` 유지. 삭제 시 목록에서 즉시 제거 (서버 전송 안 함) |
| `Updated` | 모든 값이 원본과 같아지면 `Normal` 복귀 |
| `Deleted` | 저장 시 DELETE. 화면 처리 방식은 오픈 이슈 O-02 |

- 필수 API: `GetState`, `GetOriginal`, `SetValue`, `AddRow`, `DeleteRow`, `RevertRow`, `Validate`, `GetChanges`, `AcceptChanges`, `RejectChanges`
- 저장 payload: `{ inserted[], updated[], deleted[] }`. 수정·삭제 행은 `version`을 포함한다.
- 서버 저장은 **한 트랜잭션**에서 삭제 → 수정 → 추가 순서. `version` 불일치 시 충돌 오류.
- `AcceptChanges()`는 저장 성공 응답을 받은 뒤에만 호출한다.

---

## 7. 기능 규칙

### 셀 타입 (P0)
`Text`, `Number`, `Currency`, `Date`, `Combo`, `Check`, `Popup`, `Button`
- 편집 중인 셀 **하나에만** 에디터를 띄운다.
- `Currency`의 소수 자릿수는 통화 마스터를 따른다 (로캘과 분리).
- `Popup`: 코드 입력 후 Tab → 1건 일치면 자동 확정, 0건·복수면 팝업.

### 키보드 맵 (P0)
Enter(확정·이동), Tab/Shift+Tab, 화살표, F2(편집, 값 유지), 문자 입력(덮어쓰기), Esc(취소), Shift+화살표/드래그(범위 선택), Ctrl+C/V(엑셀 형식), Insert(행 추가), Ctrl+Delete(행 삭제), F4(코드도움).

### 다국어 · 로캘
- 헤더·메시지는 문자열을 하드코딩하지 않고 **리소스 키**(`HeaderKey`)를 쓴다.
- 표시는 사용자 `CultureInfo`, 저장 값은 언어 무관 형식(ISO 날짜, 소수점 `.`).
- 붙여넣기는 사용자 로캘로 해석한다 (`1.234,5` vs `1,234.5`).

### Minus 재고 허용 옵션
- OFF: 가용 재고 초과 시 셀 오류 + 저장 차단 / ON: 셀 경고 표시 + 저장 허용
- 최종 판정은 반드시 서버 저장 트랜잭션에서 다시 검사한다.

### 이벤트 API
`OnCellValueChanging`(취소 가능), `OnCellValueChanged`, `OnRowAdding`/`OnRowAdded`, `OnRowDeleting`(취소 가능), `OnPopupRequested`, `OnPasting`(취소 가능), `OnSelectionChanged`

### v1 범위 외 (구현하지 않음)
셀 수식, 피벗, 트리 그리드, 인쇄 레이아웃. 요청이 오면 Spec 범위 밖임을 알린다.

---

## 8. 성능 목표 (초안, 벤치 결과로 확정)

| 항목 | 목표 |
|---|---|
| 셀 이동 키 반응 | 서버 왕복 없이 즉시 |
| 셀 편집 커밋 왕복 | 150ms 이하 (사내망) |
| 10,000행 조회 후 첫 화면 | 1초 이하 (서버 페이징) |
| 500행 × 10열 붙여넣기 | 2초 이하, 서버 호출 1회 |

성능에 영향을 주는 변경은 벤치 결과를 PR에 첨부한다.

---

## 9. 오픈 이슈 — 스스로 결정하지 않는다

아래 항목은 **스테이블영 결정 전까지 임의로 정하지 않는다.** 구현이 필요하면 옵션(설정값)으로 열어 두고 기본값은 괄호 안의 잠정안을 쓰되, 그 사실을 보고한다.

| ID | 내용 | 잠정안 |
|---|---|---|
| O-01 | MVP Undo/Redo 범위 | 행 단위 변경 취소(`RevertRow`)만 |
| O-02 | 삭제 표시 행 화면 처리 | 그리드 옵션 (기본: 숨김) |
| O-03 | 연결 끊김 대비 미저장 변경 브라우저 보관 | 보관 안 함, 경고만 |
| O-04 | 서버측 페이징 기준 | 화면별 설정 |
| O-05 | Enter 키 기본 이동 방향 | 그리드 옵션 (기본값 미정 — 질문할 것) |

그 밖에 아키텍처·라이선스·데이터 모델에 영향을 주는 **Critical 결정**도 구현 전에 스테이블영에게 확인한다.

---

## 10. 작업 완료 기준 (Definition of Done)

- [ ] 관련 GS 시나리오 테스트가 먼저 작성되었고 통과한다
- [ ] 전체 테스트(`dotnet test`) 통과
- [ ] R1~R7 위반 없음
- [ ] 하드코딩된 UI 문자열 없음 (리소스 키 사용)
- [ ] 새 의존성의 라이선스 확인 및 기록
- [ ] 참고 그리드 코드 유입 없음 (Clean-room)
- [ ] Spec과 다르게 구현한 부분은 PR에 명시

## 11. 자주 쓰는 명령

솔루션: `QuroGrid.slnx` (.NET 10 기본 형식). NuGet 소스는 저장소의 `nuget.config`로 nuget.org만 쓴다 (전역 피드 상속 안 함).

```bash
dotnet build
dotnet test                              # 전체
dotnet test tests/Quro.Grid.Data.Tests
dotnet test tests/Quro.Grid.Tests
pwsh tests/Quro.Grid.E2E/bin/Debug/net10.0/playwright.ps1 install   # 최초 1회, 빌드 후
dotnet test tests/Quro.Grid.E2E          # Playwright 브라우저 설치 필요
dotnet run --project src/Quro.Grid.Sample
```

## 12. 문서 산출물

설계서·분석 결과 등 문서 산출물의 최종 포맷은 **PDF**이며, 다운로드 가능한 파일로 제공한다.
