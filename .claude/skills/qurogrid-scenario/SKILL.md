---
name: qurogrid-scenario
description: QuroGrid 기능을 GS 시나리오(GS-xxx, GS-Dxx) 하나 단위로 테스트 먼저 구현할 때 사용. 새 기능, 버그 수정, QuroERP에서 온 요청 모두 이 절차로 시작한다.
---

# GS 시나리오 구현 절차

## 1. 시나리오 확정
1. `tests/scenarios/`에서 해당 GS 문서를 찾는다. 없으면 먼저 문서를 만든다:
   ```markdown
   ## GS-0xx 제목
   - 계층: xUnit | bUnit | Playwright
   - Given / When / Then
   - 근거: Spec 절 번호 또는 Addendum ID
   ```
2. 근거가 Spec에 없거나 오픈 이슈(O-xx, T-xx, I-xx)에 걸리면 **멈추고 질문**한다.

## 2. 테스트 먼저
- 테스트 이름 = 시나리오 ID: `GS_0xx_<What>_<Condition>` / `GS_D0x_<What>`
- 계층별 위치: Data → `tests/Quro.Grid.Data.Tests`, 정의 → `tests/Quro.Grid.Definition.Tests`, 컴포넌트 → `tests/Quro.Grid.Tests`, 키보드·붙여넣기 → `tests/Quro.Grid.E2E`
- `dotnet test --filter "FullyQualifiedName~GS_0xx"` 로 **실패를 확인**하고 실패 메시지를 기록한다.

## 3. 최소 구현
- 해당 프로젝트 책임 범위 안에서만 (CLAUDE.md 4장 의존 방향).
- 경로별 규칙(`.claude/rules/`)이 자동 로드되므로 반드시 따른다.
- 미결 결정에 기대는 코드는 옵션으로 열고 `// DECISION(O-xx|T-xx|I-xx)` 주석.

## 4. 확인
```bash
dotnet test --filter "FullyQualifiedName~GS_0xx"
dotnet test
```

## 5. 마무리
- 스킬 `qurogrid-review` 실행.
- 공개 API가 바뀌었으면 `docs/integration/consumer-contract.md`·`CHANGELOG.md` 갱신.
- 보고: 시나리오 ID, 실패→통과 증거, 변경 파일, Spec과 다른 점, 미결 결정에 기댄 부분.
