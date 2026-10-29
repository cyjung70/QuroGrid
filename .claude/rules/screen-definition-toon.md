---
paths:
  - "src/Quro.Grid.Definition/**"
  - "tests/Quro.Grid.Definition.Tests/**"
  - "**/*.toon"
---

# 화면 정의(TOON) 규칙 — 파서·검증기 쪽

기준: `docs/QuroGrid_TOON_ScreenDef_v0.2.md` 3~5·8·9장, 시나리오 `tests/scenarios/GS-D.md`, 결정 Addendum A-44. 6.2절 T-xx 미결 항목은 잠정안으로 구현하고 `// DECISION(T-xx)` 주석을 단다.

## 이 프로젝트가 하는 일
- TOON 텍스트 → 불변 `ScreenDef` (strict 모드: `[N]` 개수·표 행 값 수 불일치 = 로드 실패, `파일명:줄` 보고, 오류를 모아서 한 번에)
- 의미 검증 V-01~V-32: 열거값, id 유일, `screen.key` 존재, `rules` 대상(`calc`는 `edit=false` 열, `sum`은 `mode=ro` 폼 필드), 식 순환, 리소스 키·`code:`·`lookup:`·핸들러·권한의 카탈로그 존재, SQL 문장, Clean-room 금지 식별자
- `FieldModeResolver` (mode × FormState × 읽기 권한), `RuleExpression`·`LockExpression` (화이트리스트 식, `decimal`, null = 0)
- 그리드 연결은 `Quro.Grid`의 `<QuroGrid Definition="@gridDef">`가 `GridDef`를 읽어서 한다 (T-11)

## 이 프로젝트가 하지 않는 일
- **화면 정의 파일(.toon)을 갖지 않는다.** 실제 화면 정의는 QuroERP 저장소 소유다. 테스트용 골든 파일만 `tests/Quro.Grid.Definition.Tests/Golden/`에 둔다.
- 리소스 키·code Major·lookup id·핸들러·권한의 존재 여부는 **주입받은 카탈로그**(`IQuroDefinitionCatalog`)로 검사한다. QuroERP의 resx·API를 직접 읽지 않는다.
- SQL을 다루지 않는다. `screen.api`·`code:`·`lookup:`·`@` 토큰은 불투명한 참조다 (해석은 앱).
- 식을 그리드 안에서 평가하지 않는다. 표시용 계산은 앱이 `OnCellValueChanged`에서 `RuleExpression`으로, 저장값은 서버가 다시 계산한다 (A-44).
- 레거시 변환기(xml2toon)·toonlint를 두지 않는다 (QuroHarness 소관, 비공개).
- 업무 용어를 코드에 넣지 않는다. 골든 파일 id는 QuroERP glossary 영문 식별자의 camelCase. **공개 저장소**이므로 AS IS 레거시 이름(테이블·열·JSP)을 골든·테스트·문서에 쓰지 않는다.

## 표기
- 모든 배열 `[N]`, 주석은 줄 맨 앞 `#`만, 키에 하이픈 금지, 쉼표·콜론·`#`·따옴표가 들어가는 값은 `"…"`로 감싼다 (설계서 3장)
- 정의 파일을 범용 TOON 인코더로 다시 저장하지 않는다 (주석·빈 칸 소실)
- TOON 스펙은 Working Draft. 파서는 정의 파일에 쓰는 부분집합만 지원하고 공식 픽스처로 검증한다 (T-06 잠정)
