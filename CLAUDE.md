# CLAUDE.md — QuroGrid

QuroERP Community용 공통 그리드 컴포넌트 **QuroGrid**와 공통 입력 컨트롤 **QuroUI**(텍스트 박스·콤보·날짜·정적 텍스트·마스크, A-53) 저장소다. QuroERP(`/WRK/QuroERP`)는 이 저장소를 **NuGet 패키지로 가져다 쓰는 소비자**다.

기준 문서 (우선순위 순):
1. `docs/QuroGrid_Spec_v1_Addendum.pdf` — 결정 사항. Spec 본문보다 우선
2. `docs/QuroGrid_FeatureMatrix_Spec_v1.pdf` — Spec v1 초안 (2026-10-28)
3. `docs/QuroGrid_TOON_ScreenDef_v0.2.md` (PDF 동봉) — 화면 정의(TOON) 설계서 v0.2. Harness: `tests/scenarios/GS-D.md`
   - `docs/QuroGrid_Control_Spec_Draft_v0.1.md` — Control Spec 초안 v0.1. 반영 결정은 Addendum A-50~A-61, 대조·질의는 `docs/QuroGrid_Control_Spec_v0.1_Review.md`
4. 이 파일과 `.claude/rules/*.md`

이 파일과 Spec이 충돌하면 **Spec이 우선**이며 충돌 사실을 보고한다. 새 결정은 Addendum에 추가한다.
상세 규칙은 `.claude/rules/`에 있고 해당 경로 파일을 다룰 때 자동으로 로드된다.

---

## 1. 확정 결정사항 (변경 금지)

| ID | 항목 | 결정 |
|---|---|---|
| D-01 | 제작 방식 | 자체 제작. **명세 → Harness(테스트) → 구현** 3단계. Clean-room |
| D-02 | 렌더 모드 | **Blazor Server** (SignalR 회로) |
| D-03 | 라이선스 | **MIT** (Addendum A-41, `LICENSE.md`) |

기술 스택: .NET 10 / Blazor Server. UI 라이브러리(MudBlazor 등)에 의존하지 않는다 (A-67). 저장 API(ASP.NET Core + Dapper + PostgreSQL)는 **이 저장소 밖**(QuroERP)이다.

---

## 2. 절대 규칙 — Clean-room · 라이선스

참고 그리드에서는 **동작만** 가져오고 **코드는** 가져오지 않는다. 상세: `.claude/rules/clean-room.md` (항상 로드)

- 금지: dhtmlxGrid 소스 복사·포팅, FarPoint 역공학, AG Grid Enterprise 코드 참고, Nexacro 런타임 분석
- 새 의존성은 MIT 호환 확인 후 PR에 라이선스 기록. GPL 계열·상용 금지
- 이 규칙에 어긋날 수 있는 요청은 작업을 멈추고 이유를 보고한다

---

## 3. 개발 방식 — Harness Engineering

1. **테스트 먼저.** 시나리오(GS-xxx, GS-Dxx) 테스트를 쓰고 실패를 확인한다.
2. 통과시키는 최소 구현.
3. 기존 테스트를 깨뜨리지 않는다. 테스트 수정이 필요하면 이유를 보고한다.
4. 시나리오 문서(`tests/scenarios/`)와 테스트 ID를 일치시킨다. 예: `GS_004_ExcelPaste_3x4`, `GS_D01_LoadInvoiceScreen_Counts`

절차는 스킬 `qurogrid-scenario`를 따른다.

| 계층 | 도구 | 대상 |
|---|---|---|
| 단위 | xUnit | QuroDataSet, 화면 정의 파서·검증기 |
| 컴포넌트 | bUnit | QuroGrid 렌더링, 포맷, 합계, 이벤트 |
| E2E | Playwright | 키보드, 범위 선택, 붙여넣기, 코드도움, 저장 흐름 |
| 성능 | Playwright + 측정 스크립트 | Spec 4.11 목표치, 회로당 메모리 |
| API 통합 | 범위 밖 | 저장 API는 QuroERP 쪽. DataSet 저장 호출은 가짜 HTTP 핸들러로 단위 테스트 |

---

## 4. 아키텍처 — 4개 층

```
앱 (소비자)      그리드 바깥: 툴바, 다이얼로그, 코드도움 팝업, 테마 토큰 (--quro-*). 앱이 고른 UI 라이브러리 사용 가능
   ▼
QuroGrid         Blazor 컴포넌트: 컬럼 정의, 헤더/본문/합계 렌더링, Virtualize, 이벤트
   ▲▼ JS Interop (편집 커밋 · 붙여넣기 · 포커스 동기화만)
quro-grid.js     얇은 JS 층: 키보드 이동, 포커스/선택 표시, 클립보드, 리사이즈, 고정 열 스크롤
   ▼
QuroDataSet      순수 C# (UI 의존 금지): 행 상태, 원본값, 검증, 변경분 → REST(저장 API)
```

- **QuroDataSet은 Blazor·JS를 참조하지 않는다.**
- **QuroGrid·QuroUI 패키지는 MudBlazor 등 UI 라이브러리를 참조하지 않는다** (A-67, GS-090). 셀 달력·Combo 목록도 자체 구현
- **QuroGrid는 QuroERP를 모른다.** 업무 용어·엔드포인트 URL·테이블명·코드 Major를 코드에 넣지 않는다. 앱이 주입한다.

### 폴더 구조

```
src/
  Quro.Grid.Data/        QuroDataSet, RowState, ChangeSet, Validation   (UI 무관)  → NuGet
  Quro.Grid.Definition/  TOON 화면 정의 파서·검증기·불변 모델·규칙 식 (UI 무관)  → NuGet  [I-02 결정: A-44]
  Quro.Grid/             QuroGrid.razor, QuroColumn, 셀 타입, 이벤트      → NuGet (RCL)
    wwwroot/quro-grid.js   소비자에서는 _content/Quro.Grid/quro-grid.js
  Quro.UI/               QuroUI 컨트롤 (DataSet 열 바인딩, 텍스트·콤보·날짜는 자체 input, 라벨·정적 텍스트)  → NuGet (RCL)  [A-53·A-63~A-66]
  Quro.Grid.Sample/      Blazor Server 샘플 (패키지로 배포하지 않음)
tests/
  Quro.Grid.Data.Tests/  Quro.Grid.Definition.Tests/  Quro.Grid.Tests/  Quro.Grid.E2E/
  scenarios/             GS-xxx, GS-Dxx 시나리오 문서
docs/
  integration/consumer-contract.md   QuroERP가 읽는 공개 계약 (아래 6장)
scripts/
  pack-local.ps1          로컬 피드로 패키지 배포
```

의존 방향: `Quro.Grid` → `Quro.Grid.Definition` → `Quro.Grid.Data`, `Quro.UI` → `Quro.Grid.Data`. 반대 방향 금지.

---

## 5. 경로별 규칙 (자동 로드)

| 파일 | 대상 경로 | 내용 |
|---|---|---|
| `.claude/rules/clean-room.md` | 항상 | 금지 목록, 라이선스 허용 예 |
| `.claude/rules/blazor-server-r1-r7.md` | `src/Quro.Grid/**` | R1~R7, 반려 코드 |
| `.claude/rules/dataset.md` | `src/Quro.Grid.Data/**` | 행 상태 표, 필수 API, 저장 payload |
| `.claude/rules/grid-features.md` | `src/Quro.Grid/**`, `tests/**` | 셀 타입, 키보드, 다국어, Minus 표시, 이벤트, 성능 |
| `.claude/rules/screen-definition-toon.md` | `src/Quro.Grid.Definition/**`, `**/*.toon` | TOON 스키마 규칙 |
| `.claude/rules/public-api.md` | `src/Quro.Grid*/**/*.cs` | 공개 API·버전 규칙 (소비자 보호) |

---

## 6. 소비자(QuroERP) 계약

- QuroERP는 **패키지**(`Quro.Grid`, `Quro.Grid.Data`, `Quro.Grid.Definition`, `Quro.UI`)만 참조한다. 이 저장소 소스를 복사하지 않는다.
- 공개 API(public 타입·멤버, 컴포넌트 파라미터, 이벤트, TOON 스키마)는 `docs/integration/consumer-contract.md`가 정본이다. 바꾸면 그 문서와 `CHANGELOG.md`를 같은 PR에서 고친다.
- 버전은 SemVer. 1.0 전에는 `0.MINOR.PATCH-alpha.N`. 공개 API를 깨는 변경은 MINOR를 올리고 CHANGELOG에 `BREAKING`으로 적는다.
- 배포: 스킬 `qurogrid-release` (로컬 피드 `/WRK/_nuget`). **같은 버전 번호로 다시 배포하지 않는다** (NuGet 캐시 때문에 소비자에게 반영되지 않음).
- QuroERP에서 요청이 오면 이 저장소에서 GS 시나리오부터 만든다. QuroERP 저장소의 파일은 고치지 않는다.

---

## 7. 오픈 이슈 — 스스로 결정하지 않는다

스테이블영 결정 전까지 임의로 정하지 않는다. 필요하면 옵션으로 열고 잠정안을 기본값으로 쓰되 보고한다.

| ID | 내용 | 상태 |
|---|---|---|
| O-01 | MVP Undo/Redo 범위 | 결정 (2026-10-29): `RevertRow`만. 셀 단위 Undo/Redo는 P1 |
| O-02 | 삭제 표시 행 화면 처리 | 결정 (2026-10-29): 그리드 옵션, 기본 숨김 |
| O-03 | 미저장 변경 브라우저 보관 | 결정 (2026-10-29): 보관 안 함, 경고만 |
| O-04 | 서버측 페이징 기준 | 결정 (2026-10-28, A-24): 화면별 설정 |
| O-05 | Enter 기본 이동 방향 | 결정 (2026-10-28): 오른쪽. 옵션으로 아래쪽 |
| C-01~C-05 | TOON 설계서 v0.2 6.1절 (QuroERP TO BE 설계서와의 충돌) | 결정 (2026-10-29, A-44): QuroGrid 유지, 파서는 `Quro.Grid.Definition`, 저장 형식 유지, 그리드는 식을 평가하지 않음, 변환기는 QuroHarness |
| T-01~T-13 | TOON 설계서 v0.2 6.2절 | T-11 결정 (A-44): `<QuroGrid Definition="@gridDef">`. 나머지는 잠정안 |
| I-01~I-06 | QuroERP 연동 (가이드 PDF 5장) | I-02 결정 (A-44): `Quro.Grid.Definition`. 나머지 대기 |

아키텍처·라이선스·데이터 모델·공개 API에 영향을 주는 **Critical 결정**은 구현 전에 스테이블영에게 확인한다.

---

## 8. 작업 완료 기준 (Definition of Done)

스킬 `qurogrid-review` 체크리스트를 통과한다. 핵심:
- [ ] 관련 GS 테스트를 먼저 작성했고 통과한다 · 전체 `dotnet test` 통과
- [ ] R1~R7 위반 없음 · 하드코딩 UI 문자열 없음 · Clean-room 위반 없음
- [ ] 새 의존성 라이선스 기록
- [ ] 공개 API 변경 시 consumer-contract.md · CHANGELOG.md 갱신
- [ ] Spec과 다르게 구현한 부분은 PR에 명시

## 9. 자주 쓰는 명령

솔루션: `QuroGrid.slnx`. NuGet 소스는 저장소 `nuget.config`로 nuget.org만 쓴다.

```bash
dotnet build
dotnet test
dotnet test tests/Quro.Grid.Data.Tests
dotnet test tests/Quro.Grid.Tests
pwsh tests/Quro.Grid.E2E/bin/Debug/net10.0/playwright.ps1 install   # 최초 1회, 빌드 후
dotnet test tests/Quro.Grid.E2E
dotnet run --project src/Quro.Grid.Sample
pwsh scripts/pack-local.ps1 -Version 0.3.0-alpha.1                  # 로컬 피드 배포
```

## 10. 문서 산출물

설계서·분석 결과 등 문서 산출물의 최종 포맷은 **PDF**이며, 다운로드 가능한 파일로 제공한다.

## 11. Reporting language (English + Korean)

Every report addressed to the user must be written in English first, followed by a full Korean translation.
This applies to the end-of-task summary, progress updates, questions and decisions for the user, and PR descriptions.

Format:
1. English section as usual.
2. A horizontal rule (`---`), then the heading `### 한국어 번역`.
3. The Korean translation, with the same structure as the English part: same headings, bullets and order, nothing added or left out.

Translation rules:
- Do not translate code, file names, paths, table/column names (e.g. `sys_app_user`, `lang_code`), migration names, commands, test names, use-case IDs (e.g. BC-UC-003), or URLs. Keep them as-is.
- Translate faithfully. Do not summarize or shorten the Korean part.
- Decisions or questions for the user ("Decisions for you") must appear in both languages. Number them identically so the user can answer by number.
- Commit messages and code comments stay English-only.
